#nullable disable
using Il2Cpp;
using Il2CppTLD.PDID;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace StolenMeatMod
{
    /// <summary>
    /// Factory for creating and configuring predator spawn regions.
    /// </summary>
    internal static class SpawnRegionFactory
    {
        private const int DifficultyLevelCount = 5;

        internal static SpawnRegion Create(Vector3 position, string guid, int spawnCount = 1)
        {
            GameObject go = CreateGameObject(position, guid);
            SpawnRegion region = ConfigureRegion(go, spawnCount);
            Main.DebugLog($"[SpawnRegionFactory] Created region {guid} at {position} with count {spawnCount}");
            return region;
        }

        #region GameObject Creation

        private static GameObject CreateGameObject(Vector3 position, string guid)
        {
            GameObject go = new GameObject("PredatorSpawnRegion");
            go.transform.position = position;
            RegisterGuid(go, guid);
            return go;
        }

        private static void RegisterGuid(GameObject go, string guid)
        {
            ObjectGuid objectGuid = go.AddComponent<ObjectGuid>();
            objectGuid.m_Guid = guid;
            PdidTable.RuntimeRegister(objectGuid, objectGuid.m_Guid);
        }

        #endregion

        #region Region Configuration

        private static SpawnRegion ConfigureRegion(GameObject go, int spawnCount)
        {
            SpawnRegion region = go.AddComponent<SpawnRegion>();
            ConfigureDifficulty(region, spawnCount);
            ConfigureWolfType(region);
            ConfigureTiming(region);
            return region;
        }

        private static void ConfigureDifficulty(SpawnRegion region, int spawnCount)
        {
            region.m_DifficultySettings = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<SpawnRegion.DifficultyProperties>(DifficultyLevelCount);

            for (int i = 0; i < DifficultyLevelCount; i++)
            {
                region.m_DifficultySettings[i] = new SpawnRegion.DifficultyProperties
                {
                    m_MaxRespawnsPerDay = 0,
                    m_MaxSimultaneousSpawnsDay = spawnCount,
                    m_MaxSimultaneousSpawnsNight = spawnCount
                };
            }
        }

        private static void ConfigureWolfType(SpawnRegion region)
        {
            region.m_SpawnablePrefabName = Constants.WildlifeWolf;
            region.m_AiSubTypeSpawned = AiSubType.Wolf;
            region.m_AiTypeSpawned = AiType.Predator;
            region.m_SpawnablePrefab = Addressables.LoadAssetAsync<GameObject>(Constants.WildlifeWolf).WaitForCompletion();
        }

        private static void ConfigureTiming(SpawnRegion region)
        {
            region.m_ElapasedHoursNextRespawnAllowed = float.PositiveInfinity;
            region.m_ElapsedHoursAtLastActiveReRoll = float.PositiveInfinity;
            region.m_HoursNextTrapReset = float.PositiveInfinity;
            region.m_HoursReRollActive = float.PositiveInfinity;
            region.m_NumHoursBetweenRespawns = float.PositiveInfinity;
        }

        #endregion
    }
}
