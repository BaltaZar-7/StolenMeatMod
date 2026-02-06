#nullable disable
using Il2Cpp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public Dictionary<string, int> StolenSpawns = new Dictionary<string, int>();

        [JsonIgnore]
        internal int mLastKnownRespawnsRemaining;

        [JsonIgnore]
        public int MaxCapacity => Math.Min( (int)(AccumulatedCalories / CaloriesPerPredator), PredatorQuantity);

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

        [JsonIgnore]
        public int TotalStolenSpawns => StolenSpawns.Values.Sum();

        public void RecalculateCurrentPopulation(SpawnRegion spawnRegion)
        {
            int before = CurrentCapacity;
            CurrentCapacity = spawnRegion.GetMaxSimultaneousSpawnsDay() - spawnRegion.m_NumTrapped - spawnRegion.m_NumRespawnsPending;
            if (CurrentCapacity != before)
                Main.DebugLog($"[SpawnRegionInfo] Population recalculated: {before} -> {CurrentCapacity} (maxDay={spawnRegion.GetMaxSimultaneousSpawnsDay()} trapped={spawnRegion.m_NumTrapped} respawnsPending={spawnRegion.m_NumRespawnsPending})");
        }

        internal void AddStolenSpawn(string vanillaGuid)
        {
            if (StolenSpawns.ContainsKey(vanillaGuid))
                StolenSpawns[vanillaGuid]++;
            else
                StolenSpawns[vanillaGuid] = 1;
        }

        internal string RemoveRandomStolenSpawn()
        {
            if (StolenSpawns.Count == 0)
                return null;

            string key = StolenSpawns.Keys.First();
            StolenSpawns[key]--;
            if (StolenSpawns[key] <= 0)
                StolenSpawns.Remove(key);

            return key;
        }

        internal void TransferStolenSpawns(SpawnRegionInfo victim, int count)
        {
            for (int i = 0; i < count; i++)
            {
                string vanillaGuid = victim.RemoveRandomStolenSpawn();
                if (vanillaGuid == null)
                    break;

                AddStolenSpawn(vanillaGuid);
            }
        }

        internal Dictionary<string, int> DrainStolenSpawns()
        {
            var result = new Dictionary<string, int>(StolenSpawns);
            StolenSpawns.Clear();
            return result;
        }
    }
}