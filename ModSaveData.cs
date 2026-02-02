#nullable disable
using System.Collections.Generic;

namespace StolenMeatMod
{
    internal class ModSaveData
    {
        public Dictionary<string, List<MeatInfo>> MeatByScene
            = new Dictionary<string, List<MeatInfo>>();

        public float LastGlobalMinutes;
    }
}