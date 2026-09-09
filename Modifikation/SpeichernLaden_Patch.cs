using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.LocalClear;
using EinmaligerSpawn.Network;
using EinmaligerSpawn.PoiTracker;
using EinmaligerSpawn.ZombieSpawner;
using HarmonyLib;

namespace EinmaligerSpawn.SaveLoadPatches
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
                // nur der Server speichert die Kill-Datenbanken
                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                {
                    ChunkClearManager.Save(savePath);
                    PoiDatenbank.Save(savePath); // Neu hinzugefügt
                }

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
                // nur der Server lädt die Kill-Datenbanken
                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                {
                    ChunkClearManager.Load(savePath);
                    PoiDatenbank.Load(savePath);
                    ModEinstellungen.Laden(savePath); // Einstellungen für diese Welt laden
                }
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
            Log.Out("[EinmaligerSpawn] Spiel wird verlassen. Leere den Arbeitsspeicher...");

            // 1. NUR SERVER
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                // 1.1Scanner sofort hart abbrechen, falls er noch läuft!
                GlobalMapScanner.StoppeGlobalenScan();
                // 1.2 Dynamisches Spawn-Limit zurücksetzen 
                DynamischesSpawnLimit.IstInitialisiert = false;
                // 1.3 Autospawner zurücksetzen (RAM Cache der gescannten Chunks)
                AutoSpawner.Reset();
                // 1.4 Spieler-Tracking (4-Sekunden-Clear) zurücksetzen
                LokalenChunkSaeubern.Reset();
            }

            // 2. Temporäres Zombie-Gedächtnis leeren (sicherheitshalber)
            if (ChunkClearManager.ZombieUrsprung != null)
            {
                ChunkClearManager.ZombieUrsprung.Clear();
            }
            if (ChunkClearManager.ChunkClearLevel != null)
                ChunkClearManager.ChunkClearLevel.Clear();

            // 3. POI-Datenbank leeren
            if (PoiDatenbank.PoiZustaende != null)
            {
                PoiDatenbank.PoiZustaende.Clear();
            }

            // 4. Map-Tracker leeren (NEU)
            KartenOverlayManager.KartenOverlay.Reset();
        }
    }

    // Patch für das Senden der Begrüßungs-Daten beim Login eines Mitspielers
    [HarmonyPatch(typeof(GameManager), "PlayerSpawnedInWorld")]
    public class Patch_PlayerSpawnedInWorld
    {
        [HarmonyPrefix]
        public static void Prefix(ClientInfo _cInfo, RespawnType _respawnReason, Vector3i _pos, int _entityId)
        {
            Log.Out("[EinmaligerSpawn] PlayerSpawnedInWorld Prefix - Start");

            // =================================================================
            // PREFIX: Schwere Last & Netzwerk vor dem Erscheinen des Spielers
            // =================================================================

            // 1. Lokale Einstellungen laden, sobald die Welt-ID/Pfad bekannt ist
            string savePath = GameIO.GetSaveGameDir();
            if (!string.IsNullOrEmpty(savePath))
            {
                ModEinstellungen.Laden(savePath);
            }

            // 2. Netzwerk-Sync (Nur der Server schickt Daten an externe Mitspieler)
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && _cInfo != null)
            {
                // Chunk-Gedächtnis zusammenstellen
                List<string> relevanteChunks = new List<string>();
                foreach (var kvp in ChunkClearManager.ChunkClearLevel)
                {
                    if (kvp.Value >= 1)
                    {
                        relevanteChunks.Add(kvp.Key);
                    }
                }

                Log.Out($"[EinmaligerSpawn] Netzwerk (Prefix): Sende Chunk-Gedächtnis ({relevanteChunks.Count} Einträge) an {_cInfo.playerName}...");
                NetPackageChunkSync package = NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLogin(relevanteChunks);
                _cInfo.SendPackage(package);
                
                // POI-Gedächtnis zusammenstellen
                List<int> relevantePOIs = new List<int>(PoiDatenbank.PoiZustaende.Keys);
                Log.Out($"[EinmaligerSpawn] Netzwerk (Prefix): Sende POI-Gedächtnis ({relevantePOIs.Count} Einträge) an {_cInfo.playerName}...");
                NetPackagePoiSync poiPackage = NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLogin(relevantePOIs);
                _cInfo.SendPackage(poiPackage);
            }

            Log.Out("[EinmaligerSpawn] PlayerSpawnedInWorld Prefix - Ende");
        }

        [HarmonyPostfix]
        public static void Postfix(ClientInfo _cInfo, RespawnType _respawnReason, Vector3i _pos, int _entityId)
        {
            Log.Out("[EinmaligerSpawn] PlayerSpawnedInWorld Postfix - Start");

            // =================================================================
            // POSTFIX: Sichere UI- und GameObject-Zuweisungen nach dem Spawn
            // =================================================================

            // 1. Einmalige Server-Initialisierung & Start des Background-Scanners
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                if (!DynamischesSpawnLimit.IstInitialisiert)
                {
                    DynamischesSpawnLimit.IstInitialisiert = true;
                    DynamischesSpawnLimit.InitialisiereWerte();

                    GlobalMapScanner.StarteGlobalenScan();
                    Log.Out("[EinmaligerSpawn] Late-Init: Spawns wurden für die Session initialisiert (Server).");
                }
            }

            // 2. Lokale UI- und Komponenten-Aktualisierung für den Spieler am PC
            EntityPlayerLocal localPlayer = GameManager.Instance.World.GetPrimaryPlayer();

            if (localPlayer != null && localPlayer.entityId == _entityId)
            {
                // Karte basierend auf der geladenen Config aktualisieren
                ThreadManager.StartCoroutine(VerzoegerterMapRedraw());

                // Lokalen Buff beim Spawnen aufräumen, falls deaktiviert
                if (!ModEinstellungen.ZeigeLokalenFortschritt)
                {
                    if (localPlayer.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
                    {
                        localPlayer.Buffs.RemoveBuff("buffEinmaligerSpawnProgress");
                    }
                }

                Log.Out("[EinmaligerSpawn] Late-Init (Postfix): Lokaler Buff wurde verarbeitet Starte POI Radar Manager...");

                // POI Radar Manager lokal an den Spieler hängen
                if (localPlayer.gameObject.GetComponent<PoiRadarManager>() == null)
                {
                    localPlayer.gameObject.AddComponent<PoiRadarManager>();
                }

                Log.Out("[EinmaligerSpawn] Late-Init (Postfix): Kartenoverlay, lokaler Fortschrittsbuff und POI-Radar wurden initialisiert.");
            }

            Log.Out("[EinmaligerSpawn] PlayerSpawnedInWorld Postfix - Ende");


        }

        // Die neue Hintergrund-Routine
        private static System.Collections.IEnumerator VerzoegerterMapRedraw()
        {
            // Pausiert diese spezifische Methode für 5 Sekunden, das restliche Spiel läuft normal weiter
            yield return new UnityEngine.WaitForSeconds(5f);

            KartenOverlay.Wiederherstellen();
            KartenOverlay.ErzwingeRedraw();

            Log.Out("[EinmaligerSpawn] Late-Init (Verzögert): Kartenoverlay wurde nach 5 Sekunden erfolgreich geladen.");
        }
    }
}