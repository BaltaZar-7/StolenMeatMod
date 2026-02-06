#nullable disable
using Il2Cpp;
using Il2CppTLD.PDID;

namespace StolenMeatMod
{
    /// <summary>
    /// Static utilities for food/meat validation and GUID operations.
    /// </summary>
    internal static class FoodUtils
    {
        internal static bool IsValidFoodTarget(GearItem gi)
        {
            if (gi == null)
                return false;

            if (gi.m_InPlayerInventory || gi.m_InsideContainer)
                return false;

            if (StolenMeatSettings.Instance.IncludeAnimalQuarters && IsAnimalQuarter(gi))
                return true;

            if (!StolenMeatSettings.Instance.IncludeCuredMeat && gi.name.Contains(Constants.GearCuredMeat))
                return false;

            if (!StolenMeatSettings.Instance.IncludeFat && gi.name.Contains(Constants.GearAnimalFat))
                return false;

            return IsMeatOrFish(gi);
        }

        internal static bool IsAnimalQuarter(GearItem gi)
        {
            if (gi == null)
                return false;

            return Constants.AnimalQuarterNames.Contains(gi.name);
        }

        internal static bool IsMeatOrFish(GearItem gi)
        {
            FoodItem food = gi.GetComponent<FoodItem>();
            if (food == null)
                return false;

            return food.m_IsMeat || food.m_IsFish;
        }

        internal static string GetObjectGuid(GearItem gi)
        {
            if (gi == null)
                return string.Empty;

            ObjectGuid guidComp = gi.gameObject.GetComponent<ObjectGuid>();
            if (guidComp == null)
            {
                guidComp = ForceGuidSetup(gi);
                if (guidComp == null)
                    return string.Empty;
            }

            return guidComp.Get();
        }

        private static ObjectGuid ForceGuidSetup(GearItem gi)
        {
            gi.ForceGUIDSetup();
            return gi.gameObject.GetComponent<ObjectGuid>();
        }
    }
}
