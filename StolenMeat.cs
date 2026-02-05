#nullable disable
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using Il2CppTLD.PDID;
using MelonLoader;
using MelonLoader.Utils;
using Microsoft.VisualBasic;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static Il2Cpp.CarcassSite;
using static Il2Cpp.PlayerVoice;
using static Il2CppTMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace StolenMeatMod
{
    public class Main : MelonMod
    {
        internal const float UPDATE_INTERVAL_MINUTES = 1f;

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


        internal static bool IsItemInIndoorEnvironment<T>(T tItem) where T : MonoBehaviour
        {
            if (tItem == null)
                return false;

            Weather weather = GameManager.GetWeatherComponent();
            if (weather != null && weather.IsIndoorScene())
                return true;

            Collider itemCollider = tItem.GetComponent<Collider>();
            if (itemCollider == null)
                return false;

            Collider[] nearby = Physics.OverlapSphere(
                itemCollider.bounds.center,
                itemCollider.bounds.extents.magnitude
            );

            for (int i = 0; i < nearby.Length; i++)
            {
                Collider other = nearby[i];
                if (other == itemCollider)
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

            List<MeatInfo> meatsToDespawn = new List<MeatInfo>();
            List<SpawnRegionInfo> spawnRegionsToDespawn = new List<SpawnRegionInfo>();

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
                            meatsToDespawn.Add(meat);
                            UnityEngine.Object.Destroy(gi.gameObject);
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

            foreach (KeyValuePair<string, Dictionary<string, SpawnRegionInfo>> kvp in SaveDataManager.SpawnRegionsByScene)
            {
                string scene = kvp.Key;
                Dictionary<string, SpawnRegionInfo> spawnInScene = kvp.Value;
                if (spawnInScene == null || spawnInScene.Count == 0)
                    continue;

                bool isActiveScene = scene == GameManager.m_ActiveScene;

                if (!isActiveScene)
                {
                    foreach (SpawnRegionInfo spawnRegionInfo in spawnInScene.Values)
                    {
                        spawnRegionInfo.ElapsedMinutes += delta; 
                        if (spawnRegionInfo.ElapsedMinutes >= StolenMeatSettings.Instance.PredatorSpawnDuration * 60f)
                        {
                            spawnRegionsToDespawn.Add(spawnRegionInfo);
                        }
                    }
                    continue;
                }

                foreach (SpawnRegionInfo spawnRegionInfo in spawnInScene.Values)
                {
                    spawnRegionInfo.ElapsedMinutes += delta;

                    GameObject spawnGameObject = PdidTable.GetGameObject(spawnRegionInfo.ObjectGuid);
                    if (spawnGameObject == null)
                        continue;

                    SpawnRegion spawnRegion = spawnGameObject.GetComponent<SpawnRegion>();
                    if (spawnRegion == null)
                        continue;

                    spawnRegionInfo.PredatorsKilled = StolenMeatSettings.Instance.PredatorQuantity - spawnRegion.GetMaxSimultaneousSpawnsDay() + spawnRegion.m_NumRespawnsPending;
                    DebugLog($"Predators Killed: {spawnRegionInfo.PredatorsKilled} = {StolenMeatSettings.Instance.PredatorQuantity} - {spawnRegion.GetMaxSimultaneousSpawnsDay()} + {spawnRegion.m_NumRespawnsPending}");

                    if (spawnRegionInfo.PredatorsKilled >= StolenMeatSettings.Instance.PredatorQuantity
                        || spawnRegionInfo.ElapsedMinutes >= StolenMeatSettings.Instance.PredatorSpawnDuration * 60f)
                    {
                        for (int i = 0, iMax = spawnRegion.m_Spawns.Count; i < iMax; i++)
                        {
                            GameObject predatorObject = spawnRegion.m_Spawns[i].gameObject;
                            BaseAi baseAi = predatorObject.GetComponent<BaseAi>();
                            if (baseAi != null)
                            {
                                baseAi.Despawn();
                                BaseAiManager.Remove(baseAi);
                            }
                            GameObject.Destroy(predatorObject);
                        }
                        spawnRegion.m_Spawns.Clear();
                        
                        GameManager.GetSpawnRegionManager().Remove(spawnRegion); //this effectively neuters the region, as regions are run by the manager mechanically.
                        spawnRegionsToDespawn.Add(spawnRegionInfo);
                    }
                }
            }

            foreach (MeatInfo meat in meatsToDespawn)
            {
                SaveDataManager.RemoveMeat(meat.Scene, meat.ObjectGuid);
            }

            foreach (SpawnRegionInfo spawnRegionInfo in spawnRegionsToDespawn)
            {
                SaveDataManager.RemoveSpawn(spawnRegionInfo.Scene, spawnRegionInfo.ObjectGuid);
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
                if (!Utils.RollChance((float)StolenMeatSettings.Instance.PredatorSpawnChance))
                    return;

                if (!SaveDataManager.SpawnRegionsByScene.TryGetValue(GameManager.m_ActiveScene, out Dictionary<string, SpawnRegionInfo> thisSceneSpawns))
                {
                    thisSceneSpawns = new Dictionary<string, SpawnRegionInfo>();
                    SaveDataManager.SpawnRegionsByScene.Add(GameManager.m_ActiveScene, thisSceneSpawns);
                }

                if (thisSceneSpawns.Values.Count >= StolenMeatSettings.Instance.MaxSimultaneousSpawns)
                    return;

                SpawnRegionInfo closestSpawnInRange = null;
                float closestDist = float.MaxValue;
                foreach (SpawnRegionInfo info in thisSceneSpawns.Values)
                {
                    float dist = Vector3.Distance(position, info.Position);
                    if (dist <= StolenMeatSettings.Instance.SpawnedPredatorRadius || dist < closestDist)
                    {
                        closestDist = dist;
                        closestSpawnInRange = info;
                    }
                }

                if (closestSpawnInRange != null)
                {
                    if (Utils.RollChance((float)StolenMeatSettings.Instance.PredatorRefreshChance))
                    {
                        RefreshPack(closestSpawnInRange);
                    }
                    return;
                }

                if (!TryGetClosestSpawnRegionToStealFrom(position, out SpawnRegion spawnRegionToStealFrom))
                {
                    DebugLog($"Could not find a spawn region to steal population from, will not spawn predator here!");
                    return;
                }
                StealFromSpawnRegion(spawnRegionToStealFrom);

                string spawnRegionGuid = Guid.NewGuid().ToString();
                SpawnRegion predatorSpawnRegion = GenerateSpawnRegion(position, spawnRegionGuid);
                thisSceneSpawns.Add(spawnRegionGuid, new SpawnRegionInfo
                {
                    Scene = GameManager.m_ActiveScene,
                    Position = position,
                    ObjectGuid = spawnRegionGuid,
                    ElapsedMinutes = (float)StolenMeatSettings.Instance.PredatorSpawnDuration
                });
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }


        internal static bool TryGetClosestSpawnRegionToStealFrom(Vector3 position, out SpawnRegion spawnRegionToStealFrom)
        {
            spawnRegionToStealFrom = null;
            float closestDist = float.MaxValue;
            foreach (SpawnRegion spawnRegionToCompare in GameManager.GetSpawnRegionManager().m_SpawnRegions)
            {
                if (!spawnRegionToCompare.isActiveAndEnabled) continue;
                if (spawnRegionToCompare.m_AiSubTypeSpawned != AiSubType.Wolf) continue;
                if (spawnRegionToCompare.m_WolfTypeSpawned != WolfType.Normal) continue;
                if (spawnRegionToCompare.m_WildlifeMode != WildlifeMode.Normal) continue;
                if (spawnRegionToCompare.CalculateTargetPopulation() <= 0) continue;
                float dist = Vector3.Distance(position, spawnRegionToCompare.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    spawnRegionToStealFrom = spawnRegionToCompare;
                }
            }
            return spawnRegionToStealFrom != null;
        }


        internal static void StealFromSpawnRegion(SpawnRegion spawnRegion)
        {
            UniStormWeatherSystem weatherSystem = GameManager.m_TimeOfDay.m_WeatherSystem;
            spawnRegion.m_ElapasedHoursNextRespawnAllowed = weatherSystem.m_ElapsedHours + weatherSystem.m_ElapsedHoursAccumulator + spawnRegion.GetNumHoursBetweenRespawns();
            spawnRegion.m_NumRespawnsPending++;
        }


        internal static SpawnRegion GenerateSpawnRegion(Vector3 position, string guid, int predatorsKilled = 0)
        {
            GameObject go = new GameObject("PredatorSpawnRegion");
            go.transform.position = position;
            ObjectGuid objectGuid = go.AddComponent<ObjectGuid>();
            objectGuid.m_Guid = guid;
            PdidTable.RuntimeRegister(objectGuid, objectGuid.m_Guid);
            SpawnRegion spawnRegion = go.AddComponent<SpawnRegion>();
            spawnRegion.m_DifficultySettings = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<SpawnRegion.DifficultyProperties>(5);
            for (int i = 0, iMax = 5; i < iMax; i++)
            {
                spawnRegion.m_DifficultySettings[i] = new SpawnRegion.DifficultyProperties();
                spawnRegion.m_DifficultySettings[i].m_MaxRespawnsPerDay = 0;
                spawnRegion.m_DifficultySettings[i].m_MaxSimultaneousSpawnsDay = StolenMeatSettings.Instance.PredatorQuantity - predatorsKilled;
                spawnRegion.m_DifficultySettings[i].m_MaxSimultaneousSpawnsNight = StolenMeatSettings.Instance.PredatorQuantity - predatorsKilled;
            }
            spawnRegion.m_SpawnablePrefabName = "WILDLIFE_Wolf";
            spawnRegion.m_AiSubTypeSpawned = AiSubType.Wolf;
            spawnRegion.m_AiTypeSpawned = AiType.Predator;
            spawnRegion.m_ElapasedHoursNextRespawnAllowed = float.PositiveInfinity;
            spawnRegion.m_ElapsedHoursAtLastActiveReRoll = float.PositiveInfinity;
            spawnRegion.m_HoursNextTrapReset = float.PositiveInfinity;
            spawnRegion.m_HoursReRollActive = float.PositiveInfinity;
            spawnRegion.m_NumHoursBetweenRespawns = float.PositiveInfinity;
            spawnRegion.m_SpawnablePrefab = Addressables.LoadAssetAsync<GameObject>("WILDLIFE_Wolf").WaitForCompletion();
            return spawnRegion;
        }


        internal static void RefreshPack(SpawnRegionInfo spawnRegionInfo)
        {
            Main.DebugLog($"Refreshing pack at {spawnRegionInfo.Position} with guid {spawnRegionInfo.ObjectGuid}");
            spawnRegionInfo.ElapsedMinutes = 0f;

            GameObject spawnRegionObject = PdidTable.GetGameObject(spawnRegionInfo.ObjectGuid);
            if (spawnRegionObject == null)
                return;

            SpawnRegion spawnRegion = spawnRegionObject.GetComponent<SpawnRegion>();
            if (spawnRegion == null) 
                return;

            if (!TryGetClosestSpawnRegionToStealFrom(spawnRegion.transform.position, out SpawnRegion spawnRegionToStealFrom))
            {
                DebugLog($"Could not find a spawn region to steal population from, will not spawn predator here!");
                return;
            }
            spawnRegionToStealFrom.m_NumRespawnsPending++;

            spawnRegionInfo.PredatorsKilled = Mathf.Max(0, spawnRegionInfo.PredatorsKilled - 1);
            for (int i = 0, iMax = 5; i < iMax; i++)
            {
                spawnRegion.m_DifficultySettings[i].m_MaxSimultaneousSpawnsDay = StolenMeatSettings.Instance.PredatorQuantity - spawnRegionInfo.PredatorsKilled;
                spawnRegion.m_DifficultySettings[i].m_MaxSimultaneousSpawnsNight = StolenMeatSettings.Instance.PredatorQuantity - spawnRegionInfo.PredatorsKilled;
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (SaveDataManager.SpawnRegionsByScene == null) return;
            if (!SaveDataManager.SpawnRegionsByScene.TryGetValue(sceneName, out Dictionary<string, SpawnRegionInfo> spawnsInScene)) return;
            if (spawnsInScene == null) return;
            if (spawnsInScene.Count == 0) return;
            foreach (SpawnRegionInfo spawnRegionInfo in spawnsInScene.Values)
            {
                SpawnRegion spawnRegion = GenerateSpawnRegion(spawnRegionInfo.Position, spawnRegionInfo.ObjectGuid, spawnRegionInfo.PredatorsKilled);
                spawnRegion.gameObject.SetActive(true);
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