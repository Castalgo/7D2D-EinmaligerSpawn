using HarmonyLib;
using EinmaligerSpawn.Manager;

namespace EinmaligerSpawn.Patches
{
    // 1. Der Patch für das Speichern (Dein Original - lebenswichtig!)
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

    // 2. Der Patch für das Laden des Spielstands (Dein Original)
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

    // 3. Der NEUE Patch für das dynamische Überschreiben der Spawns
    [HarmonyPatch(typeof(GameManager), "Update")]
    public class Patch_GameManager_Update
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Prüfen: Haben wir es schon gemacht? Ist die Welt geladen? Ist der Spieler da?
            if (!DynamischesSpawnLimit.IstInitialisiert &&
                GameManager.Instance != null &&
                GameManager.Instance.World != null &&
                GameManager.Instance.World.Players.dict.Count > 0)
            {
                DynamischesSpawnLimit.IstInitialisiert = true; // Sperre aktivieren, damit es nur 1x läuft

                // Hier rufen wir jetzt unser Master-Skript auf (statt der alten Diagnose)
                DynamischesSpawnLimit.InitialisiereWerte();
            }
        }
    }
}