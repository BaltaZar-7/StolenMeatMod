#nullable disable
using System.Collections.Generic;

namespace StolenMeatMod
{
    internal class ModSaveData
    {
        public Dictionary<string, Dictionary<string, MeatInfo>> MeatByScene
            = new Dictionary<string, Dictionary<string, MeatInfo>>();

        public Dictionary<string, Dictionary<string, SpawnRegionInfo>> SpawnsByScene
            = new Dictionary<string, Dictionary<string, SpawnRegionInfo>>();

        public float LastGlobalMinutes;
    }
}