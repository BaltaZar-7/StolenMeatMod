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
        [Description("Chance that the meat will be stolen at each roll event. Default: 40%")]
        [Slider(10, 100)]
        public int DespawnChancePercent = 40;

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
        [Description("Chance for predator spawn region to appear on stolen meat. Default: 25%")]
        public int PredatorSpawnChance = 25;

        [Name("Predator Refresh Chance (%)")]
        [Slider(0, 100)]
        [Description("Chance for predator spawns to refresh if meat is consumed in their radius. This will reset the despawn timer and increase the current population if below maximum. Default: 75%")]
        public int PredatorRefreshChance = 75; 
        
        [Name("Maximum Predator Spawn Quantity")]
        [Slider(1, 5)]
        [Description("Maximum Predators spawned. All packs start at one but may increase to this number if additional meat is consumed in a predator's radius. Default: 3")]
        public int PredatorQuantity = 1;

        [Name("Spawned Predator Radius (meters)")]
        [Slider(50, 500)]
        [Description("Minimum distance required between two meat-spawned predators. Meat stolen within this radius of an existing predator spawn will not trigger a new predator spawn. Default: 100")]
        public int SpawnedPredatorRadius = 100;

        [Name("Max Predator Packs Spawned per Scene")]
        [Slider(5, 25)]
        [Description("Maximum allowed additional simultaneous predator packs per scene")]
        public int MaxSimultaneousSpawns = 10;

        [Name("Predator Spawn Duration")]
        [Slider(8, 72)]
        [Description("Time in hours before predator spawns disappear. Default: 24")]
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