#nullable disable
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StolenMeatMod
{
    [HarmonyPatch(typeof(GearItem), nameof(GearItem.Drop))]
    internal static class GearItem_Drop_Patch
    {
        private static void Postfix(GearItem __instance)
        {
            if (!GameManager.IsOutDoorsScene(GameManager.m_ActiveScene))
                return;

            if (!FoodUtils.IsValidFoodTarget(__instance))
                return;

            if (EnvironmentUtils.IsItemInIndoorEnvironment(__instance))
            {
                Main.DebugLog("[Drop] Ignored (item indoors)");
                return;
            }

            MelonCoroutines.Start(RegisterAfterDrop(__instance));
        }

        private static IEnumerator RegisterAfterDrop(GearItem gi)
        {
            if (gi == null)
                yield break;

            int safety = 0;
            while (gi != null && gi.m_InPlayerInventory)
            {
                yield return null;
                if (++safety > 30)
                    yield break;
            }

            if (gi == null)
                yield break;

            string guid = FoodUtils.GetObjectGuid(gi);
            if (string.IsNullOrEmpty(guid))
                yield break;

            SaveDataManager.RegisterMeat(GameManager.m_ActiveScene, gi);
            Main.DebugLog("[Drop] Food registered with GUID " + guid);

            if (SaveDataManager.MeatByScene.TryGetValue(GameManager.m_ActiveScene, out Dictionary<string, MeatInfo> meatInScene)
                && meatInScene.TryGetValue(guid, out MeatInfo meat))
            {
                EnvironmentUtils.ApplyFirePauseIfNeeded(meat, gi);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerManager), "AddItemToPlayerInventory")]
    internal static class PlayerManager_AddItem_Patch
    {
        private static void Prefix(
            ref PlayerManager __instance,
            ref GearItem gi,
            ref bool trackItemLooted,
            ref bool enableNotificationFlag)
        {
            if (!FoodUtils.IsValidFoodTarget(gi))
                return;

            SaveDataManager.RemoveMeat(GameManager.m_ActiveScene, FoodUtils.GetObjectGuid(gi));
            Main.DebugLog("[Pickup] Food removed");
        }
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.ManualStart))]
    internal static class GearItem_ManualStart_Patch
    {
        private static void Prefix(GearItem __instance)
        {
            if (!FoodUtils.IsValidFoodTarget(__instance))
                return;

            if (!SaveDataManager.MeatByScene.TryGetValue(GameManager.m_ActiveScene, out Dictionary<string, MeatInfo> meatInScene))
                return;

            string guid = FoodUtils.GetObjectGuid(__instance);
            if (string.IsNullOrEmpty(guid))
                return;

            if (!meatInScene.TryGetValue(guid, out MeatInfo meat))
                return;

            if (meat.ElapsedMinutes < Main.DespawnLimitMinutes)
                return;

            float roll = UnityEngine.Random.value;
            if (roll < Main.DespawnRollChance)
            {
                UnityEngine.Object.Destroy(__instance.gameObject);
                meatInScene.Remove(guid);
                Main.DebugLog($"[SceneLoad] Expired food destroyed roll={roll:F2}");
            }
            else
            {
                meat.ElapsedMinutes = 0f;
                Main.DebugLog($"[SceneLoad] Despawn avoided roll={roll:F2}, timer reset");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerManager), "PlaceMeshInWorld")]
    internal static class PlayerManager_PlaceMesh_Patch
    {
        private static void Postfix(PlayerManager __instance)
        {
            GameObject go = __instance.m_ObjectToPlace;
            if (go == null)
                return;

            GearItem gi = go.GetComponent<GearItem>();
            if (!FoodUtils.IsValidFoodTarget(gi))
                return;

            bool isIndoor = EnvironmentUtils.IsItemInIndoorEnvironment(gi);
            string scene = GameManager.m_ActiveScene;
            string guid = FoodUtils.GetObjectGuid(gi);
            if (string.IsNullOrEmpty(guid))
                return;

            if (isIndoor)
            {
                SaveDataManager.RemoveMeat(scene, guid);
                Main.DebugLog("[Place] Removed (item placed indoors)");
                return;
            }

            if (!SaveDataManager.MeatByScene.TryGetValue(scene, out Dictionary<string, MeatInfo> meatInScene))
            {
                SaveDataManager.RegisterMeat(scene, gi);

                if (SaveDataManager.MeatByScene.TryGetValue(scene, out Dictionary<string, MeatInfo> sceneMeat)
                    && sceneMeat.TryGetValue(guid, out MeatInfo meat))
                {
                    EnvironmentUtils.ApplyFirePauseIfNeeded(meat, gi);
                }

                Main.DebugLog("[Place] Food registered");

                if (EnvironmentUtils.IsNearBurningFire(gi.transform.position))
                {
                    Main.DebugLog("[Register] Near fire - timer effectively paused");
                }
                return;
            }

            if (meatInScene.ContainsKey(guid))
            {
                Main.DebugLog("[Place] Already tracked");
                return;
            }

            SaveDataManager.RegisterMeat(scene, gi);
            Main.DebugLog("[Place] Food registered");
        }
    }
}
