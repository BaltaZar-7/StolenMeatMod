#nullable disable
using Il2Cpp;
using System.Collections.Generic;

namespace StolenMeatMod
{
    /// <summary>
    /// Primary runtime driver for the Stolen Meat mod.
    /// Coordinates sub-managers and handles update timing.
    /// </summary>
    public class StolenMeatManager
    {
        #region Singleton

        private static StolenMeatManager mInstance;
        public static StolenMeatManager Instance => mInstance;

        #endregion

        #region Sub-Managers

        private MeatTrackingManager mMeatTracker;
        private PredatorSpawnManager mPredatorSpawner;

        #endregion

        #region Lifecycle

        internal void Initialize()
        {
            mInstance = this;

            mPredatorSpawner = new PredatorSpawnManager();
            mMeatTracker = new MeatTrackingManager(mPredatorSpawner.OnMeatDespawned);

            Main.DebugLog("[Manager] StolenMeatManager initialized");
        }

        internal void Shutdown()
        {
            mMeatTracker = null;
            mPredatorSpawner = null;
            mInstance = null;

            Main.DebugLog("[Manager] StolenMeatManager shutdown");
        }

        #endregion

        #region Update

        internal void Update()
        {
            if (!ShouldUpdate(out float delta))
                return;

            Main.DebugLog($"[Manager] Tick +{delta:F1} min");

            ProcessMeatDespawns(delta);
            ProcessSpawnRegionDespawns(delta);
        }

        private bool ShouldUpdate(out float delta)
        {
            delta = 0f;

            if (!IsValidGameState())
                return false;

            float nowMinutes = GetCurrentIngameMinutes();
            if (nowMinutes <= 0f)
                return false;

            if (!HasValidLastUpdate())
            {
                SaveDataManager.LastGlobalMinutes = nowMinutes;
                return false;
            }

            delta = nowMinutes - SaveDataManager.LastGlobalMinutes;
            if (delta < Constants.UpdateIntervalMinutes)
                return false;

            SaveDataManager.LastGlobalMinutes = nowMinutes;
            return true;
        }

        #endregion

        #region Despawn Processing

        private void ProcessMeatDespawns(float delta)
        {
            List<MeatInfo> toDespawn = mMeatTracker.Update(delta);

            foreach (MeatInfo meat in toDespawn)
            {
                SaveDataManager.RemoveMeat(meat.Scene, meat.ObjectGuid);
            }
        }

        private void ProcessSpawnRegionDespawns(float delta)
        {
            List<SpawnRegionInfo> toDespawn = mPredatorSpawner.Update(delta);

            foreach (SpawnRegionInfo spawn in toDespawn)
            {
                SaveDataManager.RemoveSpawn(spawn.Scene, spawn.ObjectGuid);
            }
        }

        #endregion

        #region Scene Events

        internal void OnSceneInitialized(string sceneName)
        {
            mPredatorSpawner?.OnSceneInitialized(sceneName);
        }

        #endregion

        #region State Checks

        private bool IsValidGameState()
        {
            if (string.IsNullOrEmpty(GameManager.m_ActiveScene))
                return false;

            if (GameManager.m_ActiveScene.Contains(Constants.MainMenuScene))
                return false;

            return true;
        }

        private bool HasValidLastUpdate()
        {
            return SaveDataManager.LastGlobalMinutes > 0f;
        }

        private float GetCurrentIngameMinutes()
        {
            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null)
                return 0f;

            return tod.GetHoursPlayedNotPaused() * 60f;
        }

        #endregion
    }
}
