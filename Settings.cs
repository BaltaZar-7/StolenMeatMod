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

        [Name("Maximum Predator Spawn Quantity")]
        [Slider(1, 10)]
        [Description("Maximum Predators spawned per pack. All packs start at one but may increase to this number if enough additional meat is consumed in a predator's radius. Default: 3")]
        public int PredatorQuantity = 3;

        [Name("Spawned Predator Radius (meters)")]
        [Slider(50, 500)]
        [Description("Minimum distance required between two meat-spawned predators. Meat stolen within this radius of an existing predator spawn will not trigger a new predator spawn. Default: 250")]
        public int SpawnedPredatorRadius = 250;

        [Name("Max Predator Packs Spawned per Scene")]
        [Slider(1, 25)]
        [Description("Maximum allowed additional simultaneous predator packs per scene. Default: 3")]
        public int MaxSimultaneousSpawns = 3;

        [Name("Predator Spawn Duration")]
        [Slider(8, 72)]
        [Description("Initial Time in hours before predator spawns disappear. Default: 24")]
        public int PredatorSpawnDuration = 24;

        [Name("Calories Required per Predator")]
        [Slider(500, 2500, 21)]
        [Description("How many calories required to spawn each new predator in a pack, up to the maximum allowed. Default: 1000 calories per predator")]
        public int CaloriesPerPredator = 1000;

        [Name("Additional Predator Time per Calorie (Calories per Hour)")]
        [Slider(100, 2500, 25)]
        [Description("How much additional time do predator packs stick around for each calorie stolen (measured in calories per hour). Default: 500 calories per additional hour")]
        public int AdditionalPredatorSpawnDuration = 500;

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