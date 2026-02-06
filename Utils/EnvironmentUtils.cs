#nullable disable
using Il2Cpp;
using UnityEngine;

namespace StolenMeatMod
{
    /// <summary>
    /// Static utilities for environment detection (indoor spaces, fires).
    /// </summary>
    internal static class EnvironmentUtils
    {
        internal static bool IsItemInIndoorEnvironment<T>(T item) where T : MonoBehaviour
        {
            if (item == null)
                return false;

            if (IsIndoorScene())
                return true;

            return IsInsideIndoorTrigger(item);
        }

        internal static bool IsIndoorScene()
        {
            Weather weather = GameManager.GetWeatherComponent();
            return weather != null && weather.IsIndoorScene();
        }

        internal static bool IsInsideIndoorTrigger<T>(T item) where T : MonoBehaviour
        {
            Collider itemCollider = item.GetComponent<Collider>();
            if (itemCollider == null)
                return false;

            Collider[] nearby = Physics.OverlapSphere(
                itemCollider.bounds.center,
                itemCollider.bounds.extents.magnitude
            );

            for (int i = 0; i < nearby.Length; i++)
            {
                if (IsValidIndoorTrigger(nearby[i], itemCollider))
                    return true;
            }

            return false;
        }

        private static bool IsValidIndoorTrigger(Collider other, Collider itemCollider)
        {
            if (other == itemCollider)
                return false;

            IndoorSpaceTrigger trigger = other.GetComponent<IndoorSpaceTrigger>();
            if (trigger == null)
                return false;

            return !trigger.m_DontCountAsInterior;
        }

        internal static bool IsNearBurningFire(Vector3 position)
        {
            FireManager fm = GameManager.GetFireManagerComponent();
            if (fm == null)
                return false;

            return fm.PointInRadiusOfBurningFire(position);
        }

        internal static void ApplyFirePauseIfNeeded(MeatInfo meat, GearItem gi)
        {
            if (meat == null || gi == null)
                return;

            FireManager fireMgr = GameManager.GetFireManagerComponent();
            if (fireMgr == null)
                return;

            if (!fireMgr.PointInRadiusOfBurningFire(gi.transform.position))
                return;

            float remainingMinutes = GetFireRemainingMinutes(fireMgr, gi.transform.position);
            meat.ElapsedMinutes = -remainingMinutes;

            Main.DebugLog($"[FirePause] Applied fire pause GUID={meat.ObjectGuid} remaining={remainingMinutes:F1} min");
        }

        private static float GetFireRemainingMinutes(FireManager fireMgr, Vector3 position)
        {
            Fire closestFire = fireMgr.GetClosestFire(position);
            if (closestFire == null)
                return 0f;

            return closestFire.GetRemainingLifeTimeHours() * 60f;
        }
    }
}
