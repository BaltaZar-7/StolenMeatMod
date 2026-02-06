#nullable disable
using MelonLoader;
using MelonLoader.Utils;
using System.IO;

namespace StolenMeatMod
{
    /// <summary>
    /// MelonMod entry point. Configuration and setup only - all runtime logic delegated to StolenMeatManager.
    /// </summary>
    public class Main : MelonMod
    {
        internal static bool DebugEnabled { get; private set; }

        public StolenMeatManager mManager;

        #region MelonMod Lifecycle

        public override void OnInitializeMelon()
        {
            InitializeDebugMode();
            InitializeSettings();
            InitializeManager();

            LogStartup();
        }

        public override void OnUpdate()
        {
            mManager?.Update();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            mManager?.OnSceneInitialized(sceneName);
        }

        public override void OnDeinitializeMelon()
        {
            mManager?.Shutdown();
            mManager = null;
        }

        #endregion

        #region Initialization

        private void InitializeDebugMode()
        {
            string debugPath = Path.Combine(MelonEnvironment.UserDataDirectory, Constants.DebugFileName);
            DebugEnabled = File.Exists(debugPath);
        }

        private void InitializeSettings()
        {
            StolenMeatSettings.OnLoad();
        }

        private void InitializeManager()
        {
            mManager = new StolenMeatManager();
            mManager.Initialize();
        }

        private void LogStartup()
        {
            string mode = DebugEnabled ? "(DEBUG)" : "";
            MelonLogger.Msg($"[StolenMeatMod] Loaded {mode}");
        }

        #endregion

        #region Shared Utilities

        internal static void DebugLog(string msg)
        {
            if (DebugEnabled)
                MelonLogger.Msg(Constants.DebugLogPrefix + msg);
        }

        internal static float DespawnLimitMinutes
        {
            get
            {
                if (StolenMeatSettings.Instance == null)
                    return Constants.DefaultDespawnHours * 60f;
                return StolenMeatSettings.Instance.DespawnHours * 60f;
            }
        }

        internal static float DespawnRollChance
        {
            get
            {
                if (StolenMeatSettings.Instance == null)
                    return Constants.DefaultDespawnChance;
                return StolenMeatSettings.Instance.DespawnChancePercent / 100f;
            }
        }

        #endregion
    }
}
