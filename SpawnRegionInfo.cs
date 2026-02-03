#nullable disable
using Newtonsoft.Json;
using UnityEngine;

namespace StolenMeatMod
{
    [System.Serializable]
    class SpawnRegionInfo
    {
        public string Scene;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public string ObjectGuid;
        public float ElapsedMinutes;

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
    }
}