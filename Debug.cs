#nullable disable
using MelonLoader;
using MelonLoader.Utils;
using System.IO;

namespace StolenMeatMod
{
    internal static class DebugHelper
    {
        private static bool _debugEnabled = false;
        private static readonly string DebugFile = Path.Combine(MelonEnvironment.UserDataDirectory, "StolenMeat.debug");

        internal static void Init()
        {
            _debugEnabled = File.Exists(DebugFile);
            MelonLogger.Msg("[StolenMeatMod] Debug enabled = " + _debugEnabled);
        }

        internal static void Log(string msg)
        {
            if (_debugEnabled)
                MelonLogger.Msg("[StolenMeatMod DEBUG] " + msg);
        }

    }
}