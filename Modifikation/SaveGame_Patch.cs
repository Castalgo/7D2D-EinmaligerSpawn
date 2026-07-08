// Speichervorgang erweitern, um die Liste der bereits gespawnten Chunks zu sichern

using HarmonyLib;
using UnityEngine;
using EinmaligerSpawn.Modifikation;

namespace zzz_EinmaligerSpawn.Patches
{
    [HarmonyPatch(typeof(GameManager), "SaveGame")]
    public class Patch_GameManager_SaveGame
    {
        public static void Prefix()
        {
            try
            {
                string savePath = ChunkSpawnManager.GetSaveFolderPath();
                ChunkSpawnManager.SaveToDisk(savePath);
                Debug.Log("[zzz_EinmaligerSpawn] SpawnedChunks vor Speichern gesichert.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[zzz_EinmaligerSpawn] Fehler beim Speichern der SpawnedChunks: {ex.Message}");
            }
        }
    }
}