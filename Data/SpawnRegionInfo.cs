#nullable disable
using Il2Cpp;
using Newtonsoft.Json;
using UnityEngine;

namespace StolenMeatMod
{
    /// <summary>
    /// Tracks a predator spawn region created when meat is stolen.
    /// Manages population, expiration, and position state.
    /// </summary>
    [System.Serializable]
    internal class SpawnRegionInfo
    {
        public string Scene;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public string ObjectGuid;
        public float ElapsedMinutes;
        public int PredatorsKilled;

        [JsonIgnore] 
        public int CurrentPopulation
        {
            get =>  StolenMeatSettings.Instance.PredatorQuantity - PredatorsKilled;
            set =>  PredatorsKilled = StolenMeatSettings.Instance.PredatorQuantity - value;
        }


        [JsonIgnore]
        public Vector3 Position
        {
            get
            {
                return new Vector3(PositionX, PositionY, PositionZ);
            }
            set
            {
                PositionX = value.x;
                PositionY = value.y;
                PositionZ = value.z;
            }
        }

        public bool AtMaxPopulation => CurrentPopulation >= StolenMeatSettings.Instance.PredatorQuantity;
        public bool AtZeroPopulation => CurrentPopulation <= 0;
        public bool PastExpirationTime => ElapsedMinutes >= StolenMeatSettings.Instance.PredatorSpawnDuration * 60f;
        public bool ShouldDestroy => AtZeroPopulation || PastExpirationTime;

        public void RecalculateCurrentPopulation(SpawnRegion spawnRegion)
        {
            int before = CurrentPopulation;
            CurrentPopulation = spawnRegion.GetMaxSimultaneousSpawnsDay() - spawnRegion.m_NumTrapped - spawnRegion.m_NumRespawnsPending;
            if (CurrentPopulation != before)
                Main.DebugLog($"[SpawnRegionInfo] Population recalculated: {before} -> {CurrentPopulation} (maxDay={spawnRegion.GetMaxSimultaneousSpawnsDay()} trapped={spawnRegion.m_NumTrapped} respawnsPending={spawnRegion.m_NumRespawnsPending})");
        }
    }
}