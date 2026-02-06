#nullable disable
global using static StolenMeatMod.Constants;
using System.Collections.Generic;

namespace StolenMeatMod
{
    /// <summary>
    /// Centralized constants for gear names, wildlife prefabs, and configuration defaults.
    /// </summary>
    internal static class Constants
    {
        #region Gear Names

        internal const string GearCuredMeat = "GEAR_CuredMeat";
        internal const string GearAnimalFat = "GEAR_AnimalFat";

        internal static readonly HashSet<string> AnimalQuarterNames = new HashSet<string>
        {
            "GEAR_WolfQuarter",
            "GEAR_BearQuarter",
            "GEAR_MooseQuarter",
            "GEAR_StagQuarter",
            "GEAR_TimberwolfQuarter",
            "GEAR_CougarQuarter"
        };

        #endregion

        #region Wildlife

        internal const string WildlifeWolf = "WILDLIFE_Wolf";
        internal const int PredatorQuantity = 5;
        internal const int SpawnedPredatorRadius = 250;
        internal const int MaxSimultaneousSpawns = 3;
        internal const int CaloriesPerPredator = 1000;
        internal const int AdditionalPredatorSpawnDuration = 500;

        #endregion

        #region Scenes

        internal const string MainMenuScene = "MainMenu";

        #endregion

        #region Timing

        internal const float UpdateIntervalMinutes = 1f;

        #endregion

        #region Defaults

        internal const float DefaultDespawnHours = 8f;
        internal const float DefaultDespawnChance = 0.4f;

        #endregion

        #region Debug

        internal const string DebugFileName = "stolenmeat.debug";
        internal const string DebugLogPrefix = "[StolenMeat DEBUG] ";

        #endregion
    }
}
