#nullable disable
using Il2Cpp;
using Il2CppTLD.Gameplay;
using Il2CppTLD.PDID;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace StolenMeatMod
{
    /// <summary>
    /// Manages predator spawn regions - creation, refresh, and cleanup.
    /// </summary>
    internal class PredatorSpawnManager
    {
        #region Public API

        internal void OnMeatDespawned(Vector3 position)
        {
            MaybeSpawnPredator(position);
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

        #region Predator Spawning

        private void MaybeSpawnPredator(Vector3 position)
        {
            try
            {
                if (!RollSpawnChance())
                    return;

                Dictionary<string, SpawnRegionInfo> sceneSpawns = GetOrCreateSceneSpawns();

                if (IsAtMaxSpawns(sceneSpawns))
                    return;

                if (TryRefreshExistingPack(position, sceneSpawns))
                    return;

                TryCreateNewSpawn(position, sceneSpawns);
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }

        private bool RollSpawnChance()
        {
            return Utils.RollChance((float)StolenMeatSettings.Instance.PredatorSpawnChance);
        }

        private bool IsAtMaxSpawns(Dictionary<string, SpawnRegionInfo> spawns)
        {
            return spawns.Count >= StolenMeatSettings.Instance.MaxSimultaneousSpawns;
        }

        private bool TryRefreshExistingPack(Vector3 position, Dictionary<string, SpawnRegionInfo> spawns)
        {
            SpawnRegionInfo closest = FindClosestInRadius(position, spawns);
            if (closest == null)
            {
                Main.DebugLog($"[PredatorSpawn] No existing pack within {StolenMeatSettings.Instance.SpawnedPredatorRadius}m radius, will try new spawn");
                return false;
            }

            float dist = Vector3.Distance(position, closest.Position);
            Main.DebugLog($"[PredatorSpawn] Found existing pack at {dist:F0}m, rolling refresh chance ({StolenMeatSettings.Instance.PredatorRefreshChance}%)");

            if (Utils.RollChance((float)StolenMeatSettings.Instance.PredatorRefreshChance))
            {
                RefreshPack(closest);
            }
            else
            {
                Main.DebugLog("[PredatorSpawn] Refresh chance roll failed, no refresh or new spawn");
            }

            return true;
        }

        private void TryCreateNewSpawn(Vector3 position, Dictionary<string, SpawnRegionInfo> spawns)
        {
            if (!TryFindDonorRegion(position, out SpawnRegion donor))
            {
                Main.DebugLog("[PredatorSpawn] No donor region found, cannot spawn predator");
                return;
            }

            StealFromRegion(donor);
            RegisterNewSpawn(position, spawns);
        }

        #endregion

        #region Pack Management

        private void RefreshPack(SpawnRegionInfo info)
        {
            Main.DebugLog($"[PredatorSpawn] Refreshing pack at {info.Position} | pop={info.CurrentPopulation}/{StolenMeatSettings.Instance.PredatorQuantity} killed={info.PredatorsKilled} elapsed={info.ElapsedMinutes:F1}min");
            info.ElapsedMinutes = 0f;

            if (info.AtMaxPopulation)
            {
                Main.DebugLog($"[PredatorSpawn] Pack already at max population ({info.CurrentPopulation}), timer reset only");
                return;
            }

            if (!TryGetSpawnRegion(info, out SpawnRegion region))
            {
                Main.DebugLog($"[PredatorSpawn] Could not find SpawnRegion GameObject for guid {info.ObjectGuid}");
                return;
            }

            Main.DebugLog($"[PredatorSpawn] SpawnRegion state: maxDay={region.GetMaxSimultaneousSpawnsDay()} respawnsPending={region.m_NumRespawnsPending} trapped={region.m_NumTrapped} spawns={region.m_Spawns.Count}");

            if (!TryFindDonorRegion(region.transform.position, out SpawnRegion donor))
            {
                Main.DebugLog("[PredatorSpawn] No donor region for pack refresh");
                return;
            }

            Main.DebugLog($"[PredatorSpawn] Donor region: targetPop={donor.CalculateTargetPopulation()} respawnsPending={donor.m_NumRespawnsPending} spawns={donor.m_Spawns.Count}");

            donor.m_NumRespawnsPending++;
            info.CurrentPopulation++;
            UpdateRegionPopulation(region, info.CurrentPopulation);
            Main.DebugLog($"[PredatorSpawn] Pack refreshed: new pop={info.CurrentPopulation}, donor respawnsPending now={donor.m_NumRespawnsPending}");
        }

        private void RegisterNewSpawn(Vector3 position, Dictionary<string, SpawnRegionInfo> spawns)
        {
            string guid = Guid.NewGuid().ToString();
            SpawnRegionFactory.Create(position, guid);

            var info = new SpawnRegionInfo
            {
                Scene = GameManager.m_ActiveScene,
                Position = position,
                ObjectGuid = guid,
                ElapsedMinutes = StolenMeatSettings.Instance.PredatorSpawnDuration
            };
            info.CurrentPopulation = 1;
            spawns.Add(guid, info);

            Main.DebugLog($"[PredatorSpawn] Created new spawn region {guid}");
        }

        #endregion

        #region Donor Region Finding

        private bool TryFindDonorRegion(Vector3 position, out SpawnRegion result)
        {
            result = null;
            float closestDist = float.MaxValue;

            foreach (SpawnRegion region in GameManager.GetSpawnRegionManager().m_SpawnRegions)
            {
                if (!IsValidDonor(region))
                    continue;

                float dist = Vector3.Distance(position, region.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    result = region;
                }
            }

            if (result != null)
                Main.DebugLog($"[PredatorSpawn] Selected donor '{result.name}' guid={GetRegionGuid(result)} at {closestDist:F0}m");

            return result != null;
        }

        private bool IsValidDonor(SpawnRegion region)
        {
            string guid = GetRegionGuid(region);
            if (IsOwnSpawnRegion(guid))
            {
                Main.DebugLog($"[PredatorSpawn] Donor rejected '{region.name}' guid={guid}: is our own spawn region");
                return false;
            }
            if (!region.isActiveAndEnabled)
            {
                Main.DebugLog($"[PredatorSpawn] Donor rejected '{region.name}' guid={guid}: not active/enabled");
                return false;
            }
            if (region.m_AiSubTypeSpawned != AiSubType.Wolf)
            {
                Main.DebugLog($"[PredatorSpawn] Donor rejected '{region.name}' guid={guid}: subtype is {region.m_AiSubTypeSpawned}, not Wolf");
                return false;
            }
            if (region.m_WolfTypeSpawned != WolfType.Normal)
            {
                Main.DebugLog($"[PredatorSpawn] Donor rejected '{region.name}' guid={guid}: wolf type is {region.m_WolfTypeSpawned}, not Normal");
                return false;
            }
            if (region.m_WildlifeMode != WildlifeMode.Normal)
            {
                Main.DebugLog($"[PredatorSpawn] Donor rejected '{region.name}' guid={guid}: wildlife mode is {region.m_WildlifeMode}, not Normal");
                return false;
            }
            if (region.CalculateTargetPopulation() <= 0)
            {
                Main.DebugLog($"[PredatorSpawn] Donor rejected '{region.name}' guid={guid}: target population is {region.CalculateTargetPopulation()}");
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
            Main.DebugLog($"[PredatorSpawn] Stole from donor '{region.name}' guid={GetRegionGuid(region)}: respawnsPending now={region.m_NumRespawnsPending}, nextRespawnAt={nextRespawn:F2}h");
        }

        #endregion

        #region Helpers

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
