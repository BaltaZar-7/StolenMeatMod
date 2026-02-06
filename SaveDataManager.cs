#nullable disable
using ModData;
using Newtonsoft.Json;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using System;
using Il2CppTLD.PDID;
using UnityEngine;

namespace StolenMeatMod
{
    internal static class SaveDataManager
    {
        private static readonly ModDataManager ModData =
            new ModDataManager(nameof(StolenMeatMod));

        private const string SUFFIX = "meatdata";

        internal static Dictionary<string, Dictionary<string, MeatInfo>> MeatByScene
            = new Dictionary<string, Dictionary<string, MeatInfo>>();

        internal static Dictionary<string, Dictionary<string, SpawnRegionInfo>> SpawnRegionsByScene
            = new Dictionary<string, Dictionary<string, SpawnRegionInfo>>();

        internal static float LastGlobalMinutes;

        internal static void RegisterMeat(string scene, GearItem gi)
        {
            if (gi == null)
                return;

            string guid = Main.GetObjectGuid(gi);
            if (string.IsNullOrEmpty(guid))
            {
                Main.DebugLog("[RegisterMeat] GUID NULL");
                return;
            }

            Dictionary<string, MeatInfo> meatInScene;
            if (!MeatByScene.TryGetValue(scene, out meatInScene))
            {
                meatInScene = new Dictionary<string, MeatInfo>();
                MeatByScene.Add(scene, meatInScene);
            }

            if (meatInScene.ContainsKey(guid))
                return;

            meatInScene.Add(guid, new MeatInfo
            {
                Scene = scene,
                ObjectGuid = guid,
                ElapsedMinutes = 0f
            });

            Main.DebugLog("[RegisterMeat] Registered " + guid);
        }

        internal static void RemoveMeat(string scene, string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return;

            Dictionary<string, MeatInfo> meatInScene;
            if (!MeatByScene.TryGetValue(scene, out meatInScene))
                return;

            if (meatInScene == null) 
                return;

            meatInScene.Remove(guid);
        }


        internal static void RemoveSpawn(string scene, string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return;

            Dictionary<string, SpawnRegionInfo> spawnsInScene;
            if (!SpawnRegionsByScene.TryGetValue(scene, out spawnsInScene))
                return;

            if (spawnsInScene == null)
                return;

            Main.DebugLog("[RemoveSpawn] Removed Spawn " + guid);

            spawnsInScene.Remove(guid);
        }


        internal static void OnSaveGame()
        {
            string json = JsonConvert.SerializeObject(new ModSaveData
            {
                MeatByScene = MeatByScene,
                SpawnRegionsByScene = SpawnRegionsByScene,
                LastGlobalMinutes = LastGlobalMinutes
            });

            ModData.Save(json, SUFFIX);
            Main.DebugLog("[SaveData] Saved");
        }

        internal static void OnLoadGame()
        {
            string json = ModData.Load(SUFFIX);
            if (string.IsNullOrEmpty(json))
            {
                MeatByScene.Clear();
                SpawnRegionsByScene.Clear();
                LastGlobalMinutes = 0f;
                Main.DebugLog("[SaveData] No data");
                return;
            }

            ModSaveData data =
                JsonConvert.DeserializeObject<ModSaveData>(json);

            MeatByScene = data.MeatByScene ?? new Dictionary<string, Dictionary<string, MeatInfo>>();
            SpawnRegionsByScene = data.SpawnRegionsByScene ?? new Dictionary<string, Dictionary<string, SpawnRegionInfo>>();
            LastGlobalMinutes = data.LastGlobalMinutes;

            Main.DebugLog("[SaveData] Loaded");
        }


        internal static void OnNewgame()
        {
            MeatByScene.Clear();
            SpawnRegionsByScene.Clear();
            LastGlobalMinutes = 0f;
            Main.DebugLog("[SaveData] Clearing data for new game");
        }
    }

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.WriteSlotToDisk),
        new Type[] { typeof(SlotData), typeof(SaveGameSlots.Timestamp) })]
    internal class SavePatch
    {
        private static void Prefix()
        {
            SaveDataManager.OnSaveGame();
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSaveGameSlot),
        new Type[] { typeof(string), typeof(int) })]
    internal class LoadPatch
    {
        private static void Postfix()
        {
            SaveDataManager.OnLoadGame();
        }
    }

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.CreateSlot), new Type[] { typeof(string), typeof(SaveSlotType), typeof(uint), typeof(Episode) })]
    internal class NewGamePatch
    {
        private static void Postfix()
        {
            SaveDataManager.OnNewgame();
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.DoExitToMainMenu))]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadMainMenu))]
    internal class MainMenuPatch
    {
        private static void Postfix()
        {
            SaveDataManager.OnNewgame();
        }
    }
}