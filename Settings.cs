#nullable disable
using MelonLoader;
using ModSettings;

namespace StolenMeatMod
{
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
        [Name("Spawn Predators on Stolen Meat")]
        [Description("Should predators spawn upon meat being stolen? Default: Yes")]
        public bool ShouldSpawnPredators = true;

        [Name("Refresh Predator Spawns on Stolen Meat")]
        [Description("Should predators refresh upon meat being stolen within their radius? Default: Yes")]
        public bool ShouldRefreshPredators = true;

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

        [Name("Predator Spawn Quantity")]
        [Slider(1, 5)]
        [Description("Predators spawned. Default: 1")]
        public int PredatorQuantity = 1;


        protected override void OnConfirm()
        {
            base.OnConfirm();
            Save();

            MelonLogger.Msg(
                $"[StolenMeatSettings] Saved | Hours={DespawnHours} | Chance={DespawnChancePercent} | Cured={IncludeCuredMeat}"
            );
        }

        internal static void OnLoad()
        {
            Instance = new StolenMeatSettings();
            Instance.AddToModSettings("Stolen Meat Mod");
        }
    }
}