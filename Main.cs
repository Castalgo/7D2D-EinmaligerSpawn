using EinmaligerSpawn.Modifikation; // Für Zugriff auf ChunkSpawnManager
using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace zzz_EinmaligerSpawn
{
    public class zzz_EinmaligerSpawn : IModApi
    {
        public void InitMod(Mod mod)
        {
            UnityEngine.Debug.Log("[zzz_EinmaligerSpawn] Initialisiere...");
            var harmony = new Harmony("com.zzz.EinmaligerSpawn");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            UnityEngine.Debug.Log("[zzz_EinmaligerSpawn] Patch erfolgreich geladen");
        }
    }
}

namespace zzz_EinmaligerSpawn.Patches
{
    // Patch: Lade SpawnedChunks beim Laden des Savegames
    [HarmonyPatch(typeof(GameManager), "OnGameLoaded")]
    public class Patch_GameManager_OnGameLoaded
    {
        public static void Postfix()
        {
            string savePath = ChunkSpawnManager.GetSaveFolderPath();
            ChunkSpawnManager.LoadFromDisk(savePath);
            UnityEngine.Debug.Log("[zzz_EinmaligerSpawn] SpawnedChunks aus Savegame geladen.");
        }
    }
}
