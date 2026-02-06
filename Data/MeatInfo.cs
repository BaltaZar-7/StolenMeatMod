#nullable disable

namespace StolenMeatMod
{
    /// <summary>
    /// Tracks a single piece of meat dropped in the world for despawn timing.
    /// </summary>
    [System.Serializable]
    internal class MeatInfo
    {
        public string Scene;
        public string ObjectGuid;
        public float ElapsedMinutes;
    }
}