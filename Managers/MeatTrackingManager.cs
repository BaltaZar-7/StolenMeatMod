#nullable disable
using Il2Cpp;
using Il2CppTLD.PDID;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StolenMeatMod
{
    /// <summary>
    /// Manages meat tracking, elapsed time updates, and despawn decisions.
    /// </summary>
    internal class MeatTrackingManager
    {
        private readonly Action<Vector3, float> mOnMeatDespawned;

        internal MeatTrackingManager(Action<Vector3, float> onMeatDespawned)
        {
            mOnMeatDespawned = onMeatDespawned;
        }

        #region Public API

        internal List<MeatInfo> Update(float delta)
        {
            List<MeatInfo> meatsToDespawn = new List<MeatInfo>();

            foreach (KeyValuePair<string, Dictionary<string, MeatInfo>> kvp in SaveDataManager.MeatByScene)
            {
                ProcessScene(kvp.Key, kvp.Value, delta, meatsToDespawn);
            }

            return meatsToDespawn;
        }

        #endregion

        #region Scene Processing

        private void ProcessScene(string scene, Dictionary<string, MeatInfo> meatInScene, float delta, List<MeatInfo> toDespawn)
        {
            if (meatInScene == null || meatInScene.Count == 0)
                return;

            bool isActiveScene = scene == GameManager.m_ActiveScene;

            if (!isActiveScene)
            {
                UpdateInactiveMeats(meatInScene, delta);
                return;
            }

            ProcessActiveMeats(meatInScene, delta, toDespawn);
        }

        private void UpdateInactiveMeats(Dictionary<string, MeatInfo> meatInScene, float delta)
        {
            foreach (MeatInfo meat in meatInScene.Values)
                meat.ElapsedMinutes += delta;
        }

        private void ProcessActiveMeats(Dictionary<string, MeatInfo> meatInScene, float delta, List<MeatInfo> toDespawn)
        {
            FireManager fireMgr = GameManager.GetFireManagerComponent();

            foreach (MeatInfo meat in meatInScene.Values)
            {
                ProcessSingleMeat(meat, delta, fireMgr, toDespawn);
            }
        }

        #endregion

        #region Single Meat Processing

        private void ProcessSingleMeat(MeatInfo meat, float delta, FireManager fireMgr, List<MeatInfo> toDespawn)
        {
            if (!TryGetGearItem(meat, out GearItem gi))
                return;

            if (IsNearActiveFire(fireMgr, gi))
            {
                EnvironmentUtils.ApplyFirePauseIfNeeded(meat, gi);
                return;
            }

            meat.ElapsedMinutes += delta;

            if (meat.ElapsedMinutes >= DespawnLimitMinutes)
                TryDespawnMeat(meat, gi, toDespawn);
        }

        private bool TryGetGearItem(MeatInfo meat, out GearItem gi)
        {
            gi = null;
            GameObject meatGameObject = PdidTable.GetGameObject(meat.ObjectGuid);
            if (meatGameObject == null)
                return false;

            gi = meatGameObject.GetComponent<GearItem>();
            return gi != null;
        }

        private bool IsNearActiveFire(FireManager fireMgr, GearItem gi)
        {
            return fireMgr != null && fireMgr.PointInRadiusOfBurningFire(gi.transform.position);
        }

        #endregion

        #region Despawn Logic

        private void TryDespawnMeat(MeatInfo meat, GearItem gi, List<MeatInfo> toDespawn)
        {
            float roll = UnityEngine.Random.value;

            if (roll < DespawnRollChance)
            {
                ExecuteDespawn(meat, gi, toDespawn, roll);
            }
            else
            {
                ResetTimer(meat, roll);
            }
        }

        private void ExecuteDespawn(MeatInfo meat, GearItem gi, List<MeatInfo> toDespawn, float roll)
        {
            toDespawn.Add(meat);
            Vector3 position = gi.transform.position;
            float calories = FoodUtils.GetCalories(gi);
            UnityEngine.Object.Destroy(gi.gameObject);

            Main.DebugLog($"[MeatTracking] Expired food destroyed GUID={meat.ObjectGuid} roll={roll:F2} calories={calories:F0}");

            mOnMeatDespawned?.Invoke(position, calories);
        }

        private void ResetTimer(MeatInfo meat, float roll)
        {
            meat.ElapsedMinutes = 0f;
            Main.DebugLog($"[MeatTracking] Despawn avoided GUID={meat.ObjectGuid} roll={roll:F2}, timer reset");
        }

        #endregion

        #region Configuration

        private static float DespawnLimitMinutes
        {
            get
            {
                if (StolenMeatSettings.Instance == null)
                    return Constants.DefaultDespawnHours * 60f;
                return StolenMeatSettings.Instance.DespawnHours * 60f;
            }
        }

        private static float DespawnRollChance
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
