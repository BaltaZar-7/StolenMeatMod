using Il2Cpp;
using MelonLoader;
using UnityEngine;

internal static class MeatDebugHelper
{
    internal static void LogAllMeatInScene()
    {
        FoodItem[] foods = Resources.FindObjectsOfTypeAll<FoodItem>();
        MelonLogger.Msg($"Found FoodItems: {foods.Length}");

        foreach (FoodItem food in foods)
        {
            if (!food.m_IsMeat)
                continue;

            GearItem gear = food.GetComponent<GearItem>();
            if (gear == null)
                continue;

            MelonLogger.Msg(
                $"[MeatDebug] Meat found | Gear={gear.name} | Pos={gear.transform.position}");
        }
    }
}