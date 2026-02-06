#nullable disable
using Il2Cpp;
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace StolenMeatMod
{
    /// <summary>
    /// Tracks a predator spawn region created when meat is stolen.
    /// Manages population, calorie accumulation, and expiration state.
    /// </summary>
    [Serializable]
    internal class SpawnRegionInfo
    {
        public string Scene;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public string ObjectGuid;
        public float ElapsedMinutes;
        public int PredatorsKilled;
        public float AccumulatedCalories;

        [JsonIgnore]
        public int MaxCapacity => Math.Min(
            (int)(AccumulatedCalories / StolenMeatSettings.Instance.CaloriesPerPredator),
            StolenMeatSettings.Instance.PredatorQuantity);

        [JsonIgnore]
        public int CurrentCapacity
        {
            get => MaxCapacity - PredatorsKilled;
            set => PredatorsKilled = MaxCapacity - value;
        }

        [JsonIgnore]
        public Vector3 Position
        {
            get => new Vector3(PositionX, PositionY, PositionZ);
            set
            {
                PositionX = value.x;
                PositionY = value.y;
                PositionZ = value.z;
            }
        }

        public bool AtMaxPopulation => CurrentCapacity >= MaxCapacity;
        public bool AtZeroPopulation => CurrentCapacity <= 0;
        public bool PastExpirationTime => ElapsedMinutes >= StolenMeatSettings.Instance.PredatorSpawnDuration * 60f;
        public bool ShouldDestroy => AtZeroPopulation || PastExpirationTime;

        public void RecalculateCurrentPopulation(SpawnRegion spawnRegion)
        {
            int before = CurrentCapacity;
            CurrentCapacity = spawnRegion.GetMaxSimultaneousSpawnsDay() - spawnRegion.m_NumTrapped - spawnRegion.m_NumRespawnsPending;
            if (CurrentCapacity != before)
                Main.DebugLog($"[SpawnRegionInfo] Population recalculated: {before} -> {CurrentCapacity} (maxDay={spawnRegion.GetMaxSimultaneousSpawnsDay()} trapped={spawnRegion.m_NumTrapped} respawnsPending={spawnRegion.m_NumRespawnsPending})");
        }
    }
}