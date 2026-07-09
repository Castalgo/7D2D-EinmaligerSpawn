using HarmonyLib;
using EinmaligerSpawn.Manager;

namespace EinmaligerSpawn.Patches
{
    // Der angepasste Patch für das Speichern
    [HarmonyPatch(typeof(GameManager), "SaveWorld")]
    public class Patch_SaveGame
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            string savePath = GameIO.GetSaveGameDir();
            if (!string.IsNullOrEmpty(savePath))
            {
                ChunkDatenbank.Save(savePath);
            }
        }
    }

    // Der NEUE Patch für das Laden des Spielstands
    [HarmonyPatch(typeof(GameManager), "StartGame")]
    public class Patch_LoadGame
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            string savePath = GameIO.GetSaveGameDir();
            if (!string.IsNullOrEmpty(savePath))
            {
                ChunkDatenbank.Load(savePath);
            }
        }
    }
}