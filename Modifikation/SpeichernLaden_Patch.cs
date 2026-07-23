using HarmonyLib;
using EinmaligerSpawn.Manager;
using EinmaligerSpawn.LootBagMarker;

namespace EinmaligerSpawn.Patches
{
    // Patch für das Speichern
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

                // Einstellungen für dieses Savegame speichern
                ModEinstellungen.Speichern();
            }
        }
    }

    // Patch für das Laden des Spielstands
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

                // Einstellungen für diese Welt laden
                ModEinstellungen.Laden(savePath);                
            }
        }
    }

    // Patch für das dynamische Überschreiben der Spawns
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

                // Limit-Werte (Default 4) neu überschreiben
                DynamischesSpawnLimit.InitialisiereWerte();

                // Karte basierend auf den gerade geladenen Einstellungen aktualisieren
                KartenOverlayManager.Wiederherstellen();

                // LootbagMarker basierend auf den Einstellungen wiederherstellen
                LootbagMarkerManager.Wiederherstellen();
            }
        }
    }

    // Patch für das Aufräumen beim Verlassen ins Hauptmenü
    [HarmonyPatch(typeof(GameManager), "SaveAndCleanupWorld")]
    public class Patch_CleanupWorld
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            UnityEngine.Debug.Log("[EinmaligerSpawn] Spiel wird verlassen. Leere den Arbeitsspeicher...");

            // 1. Update-Schleife für die nächste Sitzung wieder freigeben
            DynamischesSpawnLimit.IstInitialisiert = false;

            // 2. Statisches Gedächtnis des Karten-Overlays löschen
            KartenOverlayManager.LoescheAlleMarker();

            // 3. Spieler-Tracking (4-Sekunden-Clear) zurücksetzen
            LokalenChunkSaeubern.Reset();

            // 4. UI-Marker für Lootbags zerstören und das statische Gedächtnis leeren
            LootbagMarkerManager.EntferneAlleMarker();

            // 5. Temporäres Zombie-Gedächtnis leeren (sicherheitshalber)
            if (ChunkDatenbank.ZombieUrsprung != null)
            {
                ChunkDatenbank.ZombieUrsprung.Clear();
            }
            if (ChunkDatenbank.ToteZombiesProChunk != null)
                ChunkDatenbank.ToteZombiesProChunk.Clear();
        }
    }

}