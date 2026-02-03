#nullable disable
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using Il2CppTLD.PDID;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static Il2Cpp.CarcassSite;
using static Il2Cpp.PlayerVoice;

namespace StolenMeatMod
{
    public class Main : MelonMod
    {
        internal const float UPDATE_INTERVAL_MINUTES = 10f;

        internal static bool DebugEnabled;

        // VALID TARGET

        internal static bool IsValidFoodTarget(GearItem gi)
        {
            if (gi == null)
                return false;

            if (gi.m_InPlayerInventory || gi.m_InsideContainer)
                return false;

            if (StolenMeatSettings.Instance.IncludeAnimalQuarters &&
                IsAnimalQuarter(gi))
                return true;

            if (!StolenMeatSettings.Instance.IncludeCuredMeat &&
                gi.name.Contains("GEAR_CuredMeat"))
                return false;

            if (!StolenMeatSettings.Instance.IncludeFat &&
                gi.name.Contains("GEAR_AnimalFat"))
                return false;

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
                gi.ForceGUIDSetup();
                guidComp = gi.gameObject.GetComponent<ObjectGuid>();
                if (guidComp == null)
                {
                    return string.Empty;
                }
            }

            return guidComp.Get();
        }
        internal static bool IsItemInIndoorEnvironment(GearItem gi)
        {
            if (gi == null)
                return false;

            Weather weather = GameManager.GetWeatherComponent();
            if (weather != null && weather.IsIndoorScene())
                return true;

            Collider meatCollider = gi.GetComponent<Collider>();
            if (meatCollider == null)
                return false;

            Collider[] nearby = Physics.OverlapSphere(
                meatCollider.bounds.center,
                meatCollider.bounds.extents.magnitude
            );

            for (int i = 0; i < nearby.Length; i++)
            {
                Collider other = nearby[i];
                if (other == meatCollider)
                    continue;

                IndoorSpaceTrigger trigger = other.GetComponent<IndoorSpaceTrigger>();
                if (trigger == null)
                    continue;

                if (trigger.m_DontCountAsInterior)
                    continue;

                return true;
            }

            return false;
        }
        internal static bool IsNearBurningFire(Vector3 pos)
        {
            FireManager fm = GameManager.GetFireManagerComponent();
            if (fm == null)
                return false;

            return fm.PointInRadiusOfBurningFire(pos);
        }
        // =========================
        // INIT
        // =========================

        public override void OnInitializeMelon()
        {
            DebugEnabled = File.Exists(
                Path.Combine(MelonEnvironment.UserDataDirectory, "stolenmeat.debug")
            );
            StolenMeatSettings.OnLoad();

            MelonLogger.Msg(DebugEnabled
                ? "[StolenMeatMod] Loaded (DEBUG)"
                : "[StolenMeatMod] Loaded");
        }

        // =========================
        // RUNTIME UPDATE
        // =========================
        public override void OnUpdate()
        {
            if (string.IsNullOrEmpty(GameManager.m_ActiveScene))
                return;

            float nowMinutes = GetCurrentIngameMinutes();
            if (nowMinutes <= 0f)
                return;

            if (SaveDataManager.LastGlobalMinutes <= 0f)
            {
                SaveDataManager.LastGlobalMinutes = nowMinutes;
                return;
            }

            float delta = nowMinutes - SaveDataManager.LastGlobalMinutes;
            if (delta < UPDATE_INTERVAL_MINUTES)
                return;

            SaveDataManager.LastGlobalMinutes = nowMinutes;
            Main.DebugLog($"[Timer] Global tick +{delta:F1} min");

            foreach (KeyValuePair<string, Dictionary<string, MeatInfo>> kvp in SaveDataManager.MeatByScene)
            {
                string scene = kvp.Key;
                Dictionary<string, MeatInfo> meatInScene = kvp.Value;
                if (meatInScene == null || meatInScene.Count == 0)
                    continue;

                bool isActiveScene = scene == GameManager.m_ActiveScene;

                if (!isActiveScene)
                {
                    foreach (MeatInfo meat in meatInScene.Values)
                    {
                        meat.ElapsedMinutes += delta;
                    }
                    continue;
                }

                FireManager fireMgr = GameManager.GetFireManagerComponent();

                foreach (MeatInfo meat in meatInScene.Values)
                {
                    GameObject meatGameObject = PdidTable.GetGameObject(meat.ObjectGuid);
                    if (meatGameObject == null)
                        continue;

                    GearItem gi = meatGameObject.GetComponent<GearItem>();
                    if (gi == null)
                        continue;

                    if (fireMgr != null && fireMgr.PointInRadiusOfBurningFire(gi.transform.position))
                    {
                        Main.ApplyFirePauseIfNeeded(meat, gi);
                        continue;
                    }

                    meat.ElapsedMinutes += delta;

                    if (meat.ElapsedMinutes >= Main.DespawnLimitMinutes)
                    {
                        float roll = UnityEngine.Random.value;
                        if (roll < Main.DespawnRollChance)
                        {
                            UnityEngine.Object.Destroy(gi.gameObject);
                            meatInScene.Remove(meat.ObjectGuid);
                            MaybeSpawnPredator(gi.transform.position);

                            Main.DebugLog(
                                $"[Runtime] Expired food destroyed GUID={meat.ObjectGuid} roll={roll:F2}"
                            );
                        }
                        else
                        {
                            meat.ElapsedMinutes = 0f;
                            Main.DebugLog(
                                $"[Runtime] Despawn avoided GUID={meat.ObjectGuid} roll={roll:F2}, timer reset"
                            );
                        }
                    }
                }
            }
        }
        // Helpers
        internal static float GetCurrentIngameMinutes()
        {
            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null)
                return 0f;

            return tod.GetHoursPlayedNotPaused() * 60f;
        }

        internal void MaybeSpawnPredator(Vector3 position)
        {
            try
            {
                if (!SaveDataManager.SpawnsByScene.TryGetValue(GameManager.m_ActiveScene, out Dictionary<string, SpawnRegionInfo> thisSceneSpawns))
                {
                    thisSceneSpawns = new Dictionary<string, SpawnRegionInfo>();
                    SaveDataManager.SpawnsByScene.Add(GameManager.m_ActiveScene, thisSceneSpawns);
                }

                if (thisSceneSpawns.Values.Count >= StolenMeatSettings.Instance.MaxSimultaneousSpawns)
                {
                    Main.DebugLog($"Too many existing predator spawns! Skipping creating new predator spawn.");
                    return;
                }

                foreach (SpawnRegionInfo info in thisSceneSpawns.Values)
                {
                    float dist = Vector3.Distance(position, info.Position);
                    Main.DebugLog($"Dist from dropped meat at {position} to existing predator region at {info.Position}: {dist}");
                    if (dist <= StolenMeatSettings.Instance.SpawnedPredatorRadius)
                    {
                        Main.DebugLog($"Dropped meat too close to existing predator spawn at {info.Position}! Skipping creating new predator spawn.");
                        return;
                    }
                }

                string spawnRegionGuid = Guid.NewGuid().ToString();
                SpawnRegion predatorSpawnRegion = GenerateSpawnRegion(position, spawnRegionGuid);
                thisSceneSpawns.Add(spawnRegionGuid, new SpawnRegionInfo
                {
                    Scene = GameManager.m_ActiveScene,
                    Position = position,
                    ObjectGuid = spawnRegionGuid,
                    DespawnTime = (float)StolenMeatSettings.Instance.PredatorSpawnDuration
                });
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }


            Main.DebugLog($"Triggering predator spawn! To be implemented...");

            // TODO: Create new spawn region with a method that is used both here and on scene load, then spawn the predator
            // TODO: Capture spawn region for periodic checking of "is this spawn region still valid? is the spawn still alive?" and if not, nuke both the spawn region and the entry in the dictionary
        }


        internal static SpawnRegion GenerateSpawnRegion(Vector3 position, string guid)
        {
            GameObject go = new GameObject("PredatorSpawnRegion");
            go.transform.position = position;
            ObjectGuid objectGuid = go.AddComponent<ObjectGuid>();
            objectGuid.m_Guid = guid;
            SpawnRegion spawnRegion = go.AddComponent<SpawnRegion>();
            spawnRegion.m_DifficultySettings = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<SpawnRegion.DifficultyProperties>(5);
            for (int i = 0, iMax = 5; i < iMax; i++)
            {
                spawnRegion.m_DifficultySettings[i] = new SpawnRegion.DifficultyProperties();
                spawnRegion.m_DifficultySettings[i].m_MaxRespawnsPerDay = 0;
                spawnRegion.m_DifficultySettings[i].m_MaxSimultaneousSpawnsDay = 1;
                spawnRegion.m_DifficultySettings[i].m_MaxSimultaneousSpawnsNight = 1;
            }
            spawnRegion.m_SpawnablePrefabName = "WILDLIFE_Wolf";
            spawnRegion.m_AiSubTypeSpawned = AiSubType.Wolf;
            spawnRegion.m_AiTypeSpawned = AiType.Predator;
            spawnRegion.m_SpawnablePrefab = Addressables.LoadAssetAsync<GameObject>("WILDLIFE_Wolf").WaitForCompletion();
            return spawnRegion;
        }


        [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSceneWithLoadingScreen), new Type[] { typeof(string) })]
        private static class GameManagerPatches_LoadSceneWithLoadingScreen
        {
            private static void Prefix(string sceneName)
            {
                if (!SaveDataManager.SpawnsByScene.TryGetValue(sceneName, out Dictionary<string, SpawnRegionInfo> spawnsInScene)) return;
                if (spawnsInScene == null) return;
                if (spawnsInScene.Count == 0) return;
                foreach (SpawnRegionInfo spawnRegionInfo in spawnsInScene.Values)
                {
                    GenerateSpawnRegion(spawnRegionInfo.Position, spawnRegionInfo.ObjectGuid);
                }
            }
        }



        internal static void DebugLog(string msg)
        {
            if (DebugEnabled)
                MelonLogger.Msg("[StolenMeat DEBUG] " + msg);
        }
        internal static float DespawnLimitMinutes
        {
            get
            {
                if (StolenMeatSettings.Instance == null)
                    return 8f * 60f;

                return StolenMeatSettings.Instance.DespawnHours * 60f;
            }
        }

        internal static float DespawnRollChance
        {
            get
            {
                if (StolenMeatSettings.Instance == null)
                    return 0.4f;

                return StolenMeatSettings.Instance.DespawnChancePercent / 100f;
            }
        }
        internal static readonly HashSet<string> AnimalQuarterNames = new HashSet<string>
        {
            "GEAR_WolfQuarter",
            "GEAR_BearQuarter",
            "GEAR_MooseQuarter",
            "GEAR_StagQuarter",
            "GEAR_TimberwolfQuarter",
            "GEAR_CougarQuarter"
        };
        internal static bool IsAnimalQuarter(GearItem gi)
        {
            if (gi == null)
                return false;

            return AnimalQuarterNames.Contains(gi.name);
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

            Fire closestFire = fireMgr.GetClosestFire(gi.transform.position);
            float remainingMinutes =
                closestFire != null
                    ? closestFire.GetRemainingLifeTimeHours() * 60f
                    : 0f;

            meat.ElapsedMinutes = 0f - remainingMinutes;

            Main.DebugLog(
                $"[FirePause] Applied fire pause GUID={meat.ObjectGuid} remaining={remainingMinutes:F1} min"
            );
        }
    }


    // Patches

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.Drop))]
    internal static class GearItem_Drop_Patch
    {
        private static void Postfix(GearItem __instance)
        {
            if (!GameManager.IsOutDoorsScene(GameManager.m_ActiveScene))
                return;

            if (!Main.IsValidFoodTarget(__instance))
                return;

            if (Main.IsItemInIndoorEnvironment(__instance))
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

            // wait till out of inventory
            int safety = 0;
            while (gi != null && gi.m_InPlayerInventory)
            {
                yield return null;
                if (++safety > 30)
                    yield break;
            }

            if (gi == null)
                yield break;

            string guid = Main.GetObjectGuid(gi);
            if (string.IsNullOrEmpty(guid))
                yield break;

            SaveDataManager.RegisterMeat(
                GameManager.m_ActiveScene,
                gi
            );

            Main.DebugLog("[Drop] Food registered with GUID " + guid);

            if (SaveDataManager.MeatByScene.TryGetValue(
                    GameManager.m_ActiveScene,
                    out Dictionary<string, MeatInfo> meatInScene)
                && meatInScene.TryGetValue(guid, out MeatInfo meat))
            {
                Main.ApplyFirePauseIfNeeded(meat, gi);
            }
        }
    }
    [HarmonyPatch(typeof(PlayerManager), "AddItemToPlayerInventory")]
    internal static class PlayerManager_AddItem_Patch
    {
        static void Prefix(
            ref PlayerManager __instance,
            ref GearItem gi,
            ref bool trackItemLooted,
            ref bool enableNotificationFlag)
        {
            if (!Main.IsValidFoodTarget(gi))
                return;

            SaveDataManager.RemoveMeat(
                GameManager.m_ActiveScene,
                Main.GetObjectGuid(gi)
            );

            Main.DebugLog("[Pickup] Food removed");
        }
    }
    [HarmonyPatch(typeof(GearItem), nameof(GearItem.ManualStart))]
    internal static class GearItem_ManualStart_Patch
    {
        static void Prefix(GearItem __instance)
        {
            if (!Main.IsValidFoodTarget(__instance))
                return;

            if (!SaveDataManager.MeatByScene.TryGetValue(
                    GameManager.m_ActiveScene,
                    out Dictionary<string, MeatInfo> meatInScene))
                return;

            string guid = Main.GetObjectGuid(__instance);
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

                Main.DebugLog(
                    $"[SceneLoad] Expired food destroyed roll={roll:F2}"
                );
            }
            else
            {
                meat.ElapsedMinutes = 0f;
                Main.DebugLog(
                    $"[SceneLoad] Despawn avoided roll={roll:F2}, timer reset"
                );
            }

            return;

        }
    }
    [HarmonyPatch(typeof(PlayerManager), "PlaceMeshInWorld")]
    internal static class PlayerManager_PlaceMesh_Patch
    {
        static void Postfix(PlayerManager __instance)
        {
            GameObject go = __instance.m_ObjectToPlace;
            if (go == null)
                return;

            GearItem gi = go.GetComponent<GearItem>();
            if (!Main.IsValidFoodTarget(gi))
                return;

            bool isIndoor = Main.IsItemInIndoorEnvironment(gi);

            string scene = GameManager.m_ActiveScene;
            string guid = Main.GetObjectGuid(gi);
            if (string.IsNullOrEmpty(guid))
                return;

            // ===== INDOOR - delete tracking
            if (isIndoor)
            {
                SaveDataManager.RemoveMeat(scene, guid);
                Main.DebugLog("[Place] Removed (item placed indoors)");
                return;
            }

            // ===== OUTDOOR - register
            if (!SaveDataManager.MeatByScene.TryGetValue(scene, out Dictionary<string, MeatInfo> meatInScene))
            {
                SaveDataManager.RegisterMeat(scene, gi);

                if (SaveDataManager.MeatByScene.TryGetValue(
                    scene,
                    out Dictionary<string, MeatInfo> sceneMeat))
                {
                    if (sceneMeat.TryGetValue(guid, out MeatInfo meat))
                    {
                        Main.ApplyFirePauseIfNeeded(meat, gi);
                    }
                }

                Main.DebugLog("[Place] Food registered");

                bool nearFire = Main.IsNearBurningFire(gi.transform.position);
                if (nearFire)
                {
                    Main.DebugLog("[Register] Near fire → timer effectively paused");
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