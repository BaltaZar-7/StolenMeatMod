#nullable disable
using UnityEngine;

namespace StolenMeatMod
{
    [System.Serializable]
    class SpawnRegionInfo
    {
        public string Scene;
        public string ObjectGuid;
        public Vector3 Position;
        public float DespawnTime;
    }
}