#nullable disable
using Il2Cpp;
using Il2CppTLD.Gameplay;
using Il2CppTLD.PDID;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Il2Cpp.PlayerVoice;

namespace StolenMeatMod
{
    /// <summary>
    /// Manages predator spawn regions - creation, calorie accumulation, and cleanup.
    /// </summary>
    internal class PredatorSpawnManager
    {
        #region Public API

        internal void OnMeatDespawned(Vector3 position, float calories)
        {
            AccumulateCalories(position, calories);
        }

        internal List<SpawnRegionInfo> Update(float delta)
        {
            List<SpawnRegionInfo> toDespawn = new List<SpawnRegionInfo>();

            foreach (KeyValuePair<string, Dictionary<string, SpawnRegionInfo>> kvp in SaveDataManager.SpawnRegionsByScene)
            {
                ProcessScene(kvp.Key, kvp.Value, delta, toDespawn);
            }

            return toDespawn;
        }

        internal void OnSceneInitialized(string sceneName)
        {
            if (!TryGetSceneSpawns(sceneName, out Dictionary<string, SpawnRegionInfo> spawns))
                return;

            foreach (SpawnRegionInfo info in spawns.Values)
            {
                SpawnRegion region = SpawnRegionFactory.Create(info.Position, info.ObjectGuid, info.CurrentCapacity);
                region.gameObject.SetActive(true);
                info.mLastKnownRespawnsRemaining = 0;
            }
        }

        #endregion

        #region Scene Processing

        private void ProcessScene(string scene, Dictionary<string, SpawnRegionInfo> spawns, float delta, List<SpawnRegionInfo> toDespawn)
        {
            if (spawns == null || spawns.Count == 0)
                return;

            bool isActiveScene = scene == GameManager.m_ActiveScene;

            if (!isActiveScene)
            {
                UpdateInactiveSpawns(spawns, delta, toDespawn);
                return;
            }

            ProcessActiveSpawns(spawns, delta, toDespawn);
        }

        private void UpdateInactiveSpawns(Dictionary<string, SpawnRegionInfo> spawns, float delta, List<SpawnRegionInfo> toDespawn)
        {
            foreach (SpawnRegionInfo info in spawns.Values)
            {
                info.ElapsedMinutes += delta;
                if (info.PastExpirationTime)
                    toDespawn.Add(info);
            }
        }

        private void ProcessActiveSpawns(Dictionary<string, SpawnRegionInfo> spawns, float delta, List<SpawnRegionInfo> toDespawn)
        {
            foreach (SpawnRegionInfo info in spawns.Values)
            {
                info.ElapsedMinutes += delta;

                if (!TryGetSpawnRegion(info, out SpawnRegion region))
                    continue;

                DetectDeaths(info, region);
                info.RecalculateCurrentPopulation(region);

                if (info.ShouldDestroy)
                    DestroySpawnRegion(info, region, toDespawn);
            }
        }

        private void DetectDeaths(SpawnRegionInfo info, SpawnRegion region)
        {
            int current = region.m_NumRespawnsPending;
            int deaths = current - info.mLastKnownRespawnsRemaining;
            info.mLastKnownRespawnsRemaining = current;

            for (int i = 0; i < deaths; i++)
            {
                string returnedGuid = info.RemoveRandomStolenSpawn();
                Main.DebugLog($"[PredatorSpawn] Wolf death detected in region {info.ObjectGuid}, discarded debt to vanilla guid={returnedGuid ?? "none"}, ledger total={info.TotalStolenSpawns}");
            }
        }

        #endregion

        #region Spawn Region Destruction

        private void DestroySpawnRegion(SpawnRegionInfo info, SpawnRegion region, List<SpawnRegionInfo> toDespawn)
        {
            LogDestruction(info, region);
            ReturnStolenSpawns(info);
            DespawnAllPredators(region);
            region.m_Spawns.Clear();
            GameManager.GetSpawnRegionManager().Remove(region);
            toDespawn.Add(info);
        }

        private void ReturnStolenSpawns(SpawnRegionInfo info)
        {
            Dictionary<string, int> toReturn = info.DrainStolenSpawns();
            foreach (KeyValuePair<string, int> kvp in toReturn)
            {
                GameObject go = PdidTable.GetGameObject(kvp.Key);
                if (go == null)
                {
                    Main.DebugLog($"[PredatorSpawn] Could not find vanilla region guid={kvp.Key} to return {kvp.Value} spawn(s)");
                    continue;
                }

                SpawnRegion vanillaRegion = go.GetComponent<SpawnRegion>();
                if (vanillaRegion == null)
                    continue;

                vanillaRegion.m_NumRespawnsPending = Math.Max(0, vanillaRegion.m_NumRespawnsPending - kvp.Value);
                Main.DebugLog($"[PredatorSpawn] Returned {kvp.Value} spawn(s) to vanilla '{vanillaRegion.name}' guid={kvp.Key}, respawnsPending now={vanillaRegion.m_NumRespawnsPending}");
            }
        }

        private void LogDestruction(SpawnRegionInfo info, SpawnRegion region)
        {
            MelonLogger.Msg($"[PredatorSpawn] Destroying region. simultaneousSpawns({region.GetMaxSimultaneousSpawnsDay()}) - respawns({region.m_NumRespawnsPending}) - trapped({region.m_NumTrapped}) = pop({info.CurrentCapacity})");
        }

        private void DespawnAllPredators(SpawnRegion region)
        {
            for (int i = 0; i < region.m_Spawns.Count; i++)
            {
                DespawnPredator(region.m_Spawns[i].gameObject);
            }
        }

        private void DespawnPredator(GameObject predatorObject)
        {
            BaseAi baseAi = predatorObject.GetComponent<BaseAi>();
            if (baseAi != null)
            {
                baseAi.Despawn();
                BaseAiManager.Remove(baseAi);
            }
            GameObject.Destroy(predatorObject);
        }

        #endregion

        #region Calorie Accumulation

        private void AccumulateCalories(Vector3 position, float calories)
        {
            try
            {
                Dictionary<string, SpawnRegionInfo> sceneSpawns = GetOrCreateSceneSpawns();

                if (IsAtMaxSpawns(sceneSpawns))
                    return;

                SpawnRegionInfo info = FindOrCreateRegion(position, sceneSpawns);

                if (info.AccumulatedCalories <= 0f)
                {
                    if (RollSpawnChance())
                    {
                        Main.DebugLog("[AccumulateCalories] Spawn region activated!");
                    }
                    else
                    {
                        Main.DebugLog("[AccumulateCalories] Spawn chance failed, aborting. Next time!");
                        return;
                    }
                }

                int prevCapacity = info.MaxCapacity;
                float prevElapsedMinutes = info.ElapsedMinutes;
                info.AccumulatedCalories += calories;
                float additionalHoursAccumulated = calories / AdditionalPredatorSpawnDuration;
                float additionalMinutesAccumulated = additionalHoursAccumulated * 60f;
                info.ElapsedMinutes -= additionalMinutesAccumulated;
                int newCapacity = info.MaxCapacity;

                int newSlots = newCapacity - prevCapacity;

                Main.DebugLog($"[PredatorSpawn] Accumulated {calories:F0} cal on region {info.ObjectGuid} | total calories={info.AccumulatedCalories:F0} | capacity {prevCapacity}->{newCapacity} | elapsedMinutes {prevElapsedMinutes}->{info.ElapsedMinutes}");

                TrySpawnWolves(info, position, newSlots);
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }

        private SpawnRegionInfo FindOrCreateRegion(Vector3 position, Dictionary<string, SpawnRegionInfo> sceneSpawns)
        {
            SpawnRegionInfo existing = FindClosestInRadius(position, sceneSpawns);
            if (existing != null)
            {
                Main.DebugLog($"[PredatorSpawn] Found existing region {existing.ObjectGuid} at {Vector3.Distance(position, existing.Position):F0}m");
                return existing;
            }

            return RegisterNewRegion(position, sceneSpawns);
        }

        private SpawnRegionInfo RegisterNewRegion(Vector3 position, Dictionary<string, SpawnRegionInfo> sceneSpawns)
        {
            string guid = Guid.NewGuid().ToString();
            SpawnRegionFactory.Create(position, guid, 0);

            var info = new SpawnRegionInfo
            {
                Scene = GameManager.m_ActiveScene,
                Position = position,
                ObjectGuid = guid,
                ElapsedMinutes = 0f,
                AccumulatedCalories = 0f
            };
            sceneSpawns.Add(guid, info);

            Main.DebugLog($"[PredatorSpawn] Created new region {guid} at {position}");
            return info;
        }

        private void TrySpawnWolves(SpawnRegionInfo info, Vector3 position, int newSlots)
        {
            if (newSlots <= 0)
                return;

            if (!TryGetSpawnRegion(info, out SpawnRegion region))
                return;

            for (int i = 0; i < newSlots; i++)
            {
                if (!TryFindVictimRegion(info, position, out SpawnRegion victim))
                {
                    Main.DebugLog("[PredatorSpawn] No victim region found");
                    break;
                }

                StealFromRegion(info, victim);
                int newMax = region.GetMaxSimultaneousSpawnsDay() + 1;
                UpdateRegionPopulation(region, newMax);

                Main.DebugLog($"[PredatorSpawn] Wolf stolen for region {info.ObjectGuid}, spawnRegion max now {newMax}");
            }
        }

        #endregion

        #region Victim Region Finding

        private bool TryFindVictimRegion(SpawnRegionInfo stealingRegion, Vector3 position, out SpawnRegion result)
        {
            result = null;
            float closestDist = float.MaxValue;

            foreach (SpawnRegion victimRegion in GameManager.GetSpawnRegionManager().m_SpawnRegions)
            {
                if (!IsValidVictim(stealingRegion, victimRegion))
                    continue;

                float dist = Vector3.Distance(position, victimRegion.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    result = victimRegion;
                }
            }

            if (result != null)
                Main.DebugLog($"[PredatorSpawn] Selected victim '{result.name}' guid={GetRegionGuid(result)} at {closestDist:F0}m");

            return result != null;
        }

        private bool IsValidVictim(SpawnRegionInfo stealingRegionInfo, SpawnRegion victimRegion)
        {
            string victimGuid = GetRegionGuid(victimRegion);
            if (stealingRegionInfo.ObjectGuid == victimGuid)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{victimRegion.name}' guid={victimGuid}: is self");
                return false;
            }
            if (!victimRegion.isActiveAndEnabled)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{victimRegion.name}' guid={victimGuid}: not active/enabled");
                return false;
            }
            if (victimRegion.m_AiSubTypeSpawned != AiSubType.Wolf)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{victimRegion.name}' guid={victimGuid}: subtype is {victimRegion.m_AiSubTypeSpawned}, not Wolf");
                return false;
            }
            if (victimRegion.m_WolfTypeSpawned != WolfType.Normal)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{victimRegion.name}' guid={victimGuid}: wolf type is {victimRegion.m_WolfTypeSpawned}, not Normal");
                return false;
            }
            if (victimRegion.m_WildlifeMode != WildlifeMode.Normal)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{victimRegion.name}' guid={victimGuid}: wildlife mode is {victimRegion.m_WildlifeMode}, not Normal");
                return false;
            }
            if (GetOrCreateSceneSpawns().TryGetValue(victimGuid, out SpawnRegionInfo moddedVictim))
            {
                if (moddedVictim.CurrentCapacity <= 0)
                {
                    Main.DebugLog($"[PredatorSpawn] Victim rejected '{victimRegion.name}' guid={victimGuid}: modded capacity is {moddedVictim.CurrentCapacity}");
                    return false;
                }
            }
            else if (victimRegion.CalculateTargetPopulation() <= 0)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{victimRegion.name}' guid={victimGuid}: target population is {victimRegion.CalculateTargetPopulation()}");
                return false;
            }
            return true;
        }

        private static string GetRegionGuid(SpawnRegion region)
        {
            ObjectGuid guidComp = region.gameObject.GetComponent<ObjectGuid>();
            if (guidComp == null) return "null-objectguid";
            if (guidComp.m_Guid != null && guidComp.m_Guid != string.Empty) return guidComp.m_Guid;
            if (guidComp.PDID != null && guidComp.PDID != string.Empty) return guidComp.PDID;
            return "no-guid-found";
        }

        private void StealFromRegion(SpawnRegionInfo stealingRegion, SpawnRegion victimRegion)
        {
            string victimGuid = GetRegionGuid(victimRegion);

            if (GetOrCreateSceneSpawns().TryGetValue(victimGuid, out SpawnRegionInfo moddedVictim))
            {
                StealFromModdedRegion(stealingRegion, victimRegion, moddedVictim);
            }
            else
            {
                StealFromVanillaRegion(stealingRegion, victimRegion, victimGuid);
            }
        }

        private void StealFromVanillaRegion(SpawnRegionInfo stealingRegion, SpawnRegion victimRegion, string victimGuid)
        {
            UniStormWeatherSystem weather = GameManager.m_TimeOfDay.m_WeatherSystem;
            float nextRespawn = weather.m_ElapsedHours + weather.m_ElapsedHoursAccumulator + victimRegion.GetNumHoursBetweenRespawns();
            victimRegion.m_ElapasedHoursNextRespawnAllowed = nextRespawn;
            victimRegion.m_NumRespawnsPending++;
            stealingRegion.AddStolenSpawn(victimGuid);

            Main.DebugLog($"[PredatorSpawn] Stole from vanilla '{victimRegion.name}' guid={victimGuid}: respawnsPending now={victimRegion.m_NumRespawnsPending}, nextRespawnAt={nextRespawn:F2}h, thief ledger total={stealingRegion.TotalStolenSpawns}");
        }

        private void StealFromModdedRegion(SpawnRegionInfo thief, SpawnRegion victimRegion, SpawnRegionInfo moddedVictim)
        {
            moddedVictim.PredatorsKilled++;
            UpdateRegionPopulation(victimRegion, moddedVictim.CurrentCapacity);
            thief.TransferStolenSpawns(moddedVictim, 1);

            Main.DebugLog($"[PredatorSpawn] Stole from modded '{victimRegion.name}' guid={moddedVictim.ObjectGuid}: victim capacity now={moddedVictim.CurrentCapacity}, thief ledger total={thief.TotalStolenSpawns}");
        }

        #endregion

        #region Helpers

        private bool RollSpawnChance() =>Utils.RollChance((float)StolenMeatSettings.Instance.PredatorSpawnChance);

        private bool IsAtMaxSpawns(Dictionary<string, SpawnRegionInfo> spawns) => spawns.Count >= MaxSimultaneousSpawns;

        private SpawnRegionInfo FindClosestInRadius(Vector3 position, Dictionary<string, SpawnRegionInfo> spawns)
        {
            SpawnRegionInfo closest = null;
            float closestDist = float.MaxValue;
            float maxRadius = SpawnedPredatorRadius;

            foreach (SpawnRegionInfo info in spawns.Values)
            {
                float dist = Vector3.Distance(position, info.Position);
                if (dist <= maxRadius && dist < closestDist)
                {
                    closestDist = dist;
                    closest = info;
                }
            }

            return closest;
        }

        private Dictionary<string, SpawnRegionInfo> GetOrCreateSceneSpawns()
        {
            if (!SaveDataManager.SpawnRegionsByScene.TryGetValue(GameManager.m_ActiveScene, out Dictionary<string, SpawnRegionInfo> spawns))
            {
                spawns = new Dictionary<string, SpawnRegionInfo>();
                SaveDataManager.SpawnRegionsByScene.Add(GameManager.m_ActiveScene, spawns);
            }
            return spawns;
        }

        private bool TryGetSceneSpawns(string scene, out Dictionary<string, SpawnRegionInfo> spawns)
        {
            spawns = null;
            if (SaveDataManager.SpawnRegionsByScene == null)
                return false;
            if (!SaveDataManager.SpawnRegionsByScene.TryGetValue(scene, out spawns))
                return false;
            return spawns != null && spawns.Count > 0;
        }

        private bool TryGetSpawnRegion(SpawnRegionInfo info, out SpawnRegion region)
        {
            region = null;
            GameObject go = PdidTable.GetGameObject(info.ObjectGuid);
            if (go == null)
                return false;

            region = go.GetComponent<SpawnRegion>();
            return region != null;
        }

        private void UpdateRegionPopulation(SpawnRegion region, int population)
        {
            for (int i = 0; i < 5; i++)
            {
                region.m_DifficultySettings[i].m_MaxSimultaneousSpawnsDay = population;
                region.m_DifficultySettings[i].m_MaxSimultaneousSpawnsNight = population;
            }
        }

        #endregion
    }
}
