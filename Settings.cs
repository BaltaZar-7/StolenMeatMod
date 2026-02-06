#nullable disable
using MelonLoader;
using ModSettings;

namespace StolenMeatMod
{
    /// <summary>
    /// User-configurable settings for the Stolen Meat Mod.
    /// </summary>
    internal class StolenMeatSettings : JsonModSettings
    {
        public static StolenMeatSettings Instance { get; private set; }

        [Section("Despawn Timer")]
        [Name("Despawn Time (hours)")]
        [Description("Number of hours until the event rolls for each meat. Default: 8 hours")]
        [Slider(2, 24)]
        public int DespawnHours = 8;

        [Section("Despawn Chance")]
        [Name("Despawn Chance (%)")]
        [Description("Chance that the meat will be stolen at each roll event. Default: 25%")]
        [Slider(10, 100)]
        public int DespawnChancePercent = 25;

        [Section("Meat Types")]
        [Name("Include Cured Meat")]
        [Description("Should Cured Meat get stolen? Default: Yes")]
        public bool IncludeCuredMeat = true;

        [Name("Include Fat")]
        [Description("Should Fat get stolen? Default: Yes")]
        public bool IncludeFat = true;

        [Name("Include Animal Quarters")]
        [Description("Should Animal Quarters get stolen? Default: No")]
        public bool IncludeAnimalQuarters = false;

        [Section("Predator Spawn Settings")]
        [Name("Predator Spawn Chance %")]
        [Slider(0, 100)]
        [Description("Chance for predator pack to appear on stolen meat if no nearby packs exist. Default: 10%")]
        public int PredatorSpawnChance = 10;

        [Name("Predator Spawn Duration")]
        [Slider(8, 72)]
        [Description("Initial Time in hours before predator spawns disappear. Default: 24")]
        public int PredatorSpawnDuration = 24;


        protected override void OnConfirm()
        {
            base.OnConfirm();
            Save();
        }

        internal static void OnLoad()
        {
            Instance = new StolenMeatSettings();
            Instance.AddToModSettings("Stolen Meat Mod");
        }
    }
}