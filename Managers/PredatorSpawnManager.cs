#nullable disable
using Il2Cpp;
using Il2CppTLD.Gameplay;
using Il2CppTLD.PDID;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

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
                SpawnRegion region = SpawnRegionFactory.Create(info.Position, info.ObjectGuid, info.CurrentPopulation);
                region.gameObject.SetActive(true);
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

                info.RecalculateCurrentPopulation(region);

                if (info.ShouldDestroy)
                    DestroySpawnRegion(info, region, toDespawn);
            }
        }

        #endregion

        #region Spawn Region Destruction

        private void DestroySpawnRegion(SpawnRegionInfo info, SpawnRegion region, List<SpawnRegionInfo> toDespawn)
        {
            LogDestruction(info, region);
            DespawnAllPredators(region);
            region.m_Spawns.Clear();
            GameManager.GetSpawnRegionManager().Remove(region);
            toDespawn.Add(info);
        }

        private void LogDestruction(SpawnRegionInfo info, SpawnRegion region)
        {
            MelonLogger.Msg($"[PredatorSpawn] Destroying region. simultaneousSpawns({region.GetMaxSimultaneousSpawnsDay()}) - respawns({region.m_NumRespawnsPending}) - trapped({region.m_NumTrapped}) = pop({info.CurrentPopulation})");
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

                int prevCapacity = info.SpawnCapacity;
                float prevElapsedMinutes = info.ElapsedMinutes;
                info.AccumulatedCalories += calories;
                float additionalHoursAccumulated = calories / StolenMeatSettings.Instance.AdditionalPredatorSpawnDuration;
                float additionalMinutesAccumulated = additionalHoursAccumulated * 60f;
                info.ElapsedMinutes -= additionalMinutesAccumulated;
                int newCapacity = info.SpawnCapacity;

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
                if (!RollSpawnChance())
                {
                    Main.DebugLog("[PredatorSpawn] Spawn chance roll failed");
                    continue;
                }

                if (!TryFindVictimRegion(position, out SpawnRegion victim))
                {
                    Main.DebugLog("[PredatorSpawn] No victim region found");
                    break;
                }

                StealFromRegion(victim);
                int newMax = region.GetMaxSimultaneousSpawnsDay() + 1;
                UpdateRegionPopulation(region, newMax);

                Main.DebugLog($"[PredatorSpawn] Wolf stolen for region {info.ObjectGuid}, spawnRegion max now {newMax}");
            }
        }

        #endregion

        #region Victim Region Finding

        private bool TryFindVictimRegion(Vector3 position, out SpawnRegion result)
        {
            result = null;
            float closestDist = float.MaxValue;

            foreach (SpawnRegion region in GameManager.GetSpawnRegionManager().m_SpawnRegions)
            {
                if (!IsValidVictim(region))
                    continue;

                float dist = Vector3.Distance(position, region.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    result = region;
                }
            }

            if (result != null)
                Main.DebugLog($"[PredatorSpawn] Selected victim '{result.name}' guid={GetRegionGuid(result)} at {closestDist:F0}m");

            return result != null;
        }

        private bool IsValidVictim(SpawnRegion region)
        {
            string guid = GetRegionGuid(region);
            if (IsOwnSpawnRegion(guid))
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{region.name}' guid={guid}: is our own spawn region");
                return false;
            }
            if (!region.isActiveAndEnabled)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{region.name}' guid={guid}: not active/enabled");
                return false;
            }
            if (region.m_AiSubTypeSpawned != AiSubType.Wolf)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{region.name}' guid={guid}: subtype is {region.m_AiSubTypeSpawned}, not Wolf");
                return false;
            }
            if (region.m_WolfTypeSpawned != WolfType.Normal)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{region.name}' guid={guid}: wolf type is {region.m_WolfTypeSpawned}, not Normal");
                return false;
            }
            if (region.m_WildlifeMode != WildlifeMode.Normal)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{region.name}' guid={guid}: wildlife mode is {region.m_WildlifeMode}, not Normal");
                return false;
            }
            if (region.CalculateTargetPopulation() <= 0)
            {
                Main.DebugLog($"[PredatorSpawn] Victim rejected '{region.name}' guid={guid}: target population is {region.CalculateTargetPopulation()}");
                return false;
            }
            return true;
        }

        private static string GetRegionGuid(SpawnRegion region)
        {
            ObjectGuid guidComp = region.gameObject.GetComponent<ObjectGuid>();
            return guidComp != null ? guidComp.m_Guid : "no-guid";
        }

        private static bool IsOwnSpawnRegion(string guid)
        {
            if (!SaveDataManager.SpawnRegionsByScene.TryGetValue(GameManager.m_ActiveScene, out Dictionary<string, SpawnRegionInfo> spawns))
                return false;

            return spawns.ContainsKey(guid);
        }

        private void StealFromRegion(SpawnRegion region)
        {
            UniStormWeatherSystem weather = GameManager.m_TimeOfDay.m_WeatherSystem;
            float nextRespawn = weather.m_ElapsedHours + weather.m_ElapsedHoursAccumulator + region.GetNumHoursBetweenRespawns();
            region.m_ElapasedHoursNextRespawnAllowed = nextRespawn;
            region.m_NumRespawnsPending++;
            Main.DebugLog($"[PredatorSpawn] Stole from victim '{region.name}' guid={GetRegionGuid(region)}: respawnsPending now={region.m_NumRespawnsPending}, nextRespawnAt={nextRespawn:F2}h");
        }

        #endregion

        #region Helpers

        private bool RollSpawnChance()
        {
            return Utils.RollChance((float)StolenMeatSettings.Instance.PredatorSpawnChance);
        }

        private bool IsAtMaxSpawns(Dictionary<string, SpawnRegionInfo> spawns)
        {
            return spawns.Count >= StolenMeatSettings.Instance.MaxSimultaneousSpawns;
        }

        private SpawnRegionInfo FindClosestInRadius(Vector3 position, Dictionary<string, SpawnRegionInfo> spawns)
        {
            SpawnRegionInfo closest = null;
            float closestDist = float.MaxValue;
            float maxRadius = StolenMeatSettings.Instance.SpawnedPredatorRadius;

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
