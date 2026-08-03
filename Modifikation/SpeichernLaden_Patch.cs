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
                    KillCounter.Save(savePath);
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
                    KillCounter.Load(savePath);
                    PoiDatenbank.Load(savePath); // Neu hinzugefügt
                }

                // Einstellungen für diese Welt laden
                ModEinstellungen.Laden(savePath);
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
                // 1. Dynamisches Spawn-Limit zurücksetzen 
                DynamischesSpawnLimit.IstInitialisiert = false;
                // 2. Autospawner zurücksetzen (RAM Cache der gescannten Chunks)
                AutoSpawner.Reset();
                // 3. Spieler-Tracking (4-Sekunden-Clear) zurücksetzen
                LokalenChunkSaeubern.Reset();
            }

            // 4. Temporäres Zombie-Gedächtnis leeren (sicherheitshalber)
            if (KillCounter.ZombieUrsprung != null)
            {
                KillCounter.ZombieUrsprung.Clear();
            }
            if (KillCounter.ToteZombiesProChunk != null)
                KillCounter.ToteZombiesProChunk.Clear();

            // 5. POI-Datenbank leeren
            if (PoiDatenbank.GecleartePOIs != null)
            {
                PoiDatenbank.GecleartePOIs.Clear();
            }
        }
    }

    // Patch für das Senden der Begrüßungs-Daten beim Login eines Mitspielers
    [HarmonyPatch(typeof(GameManager), "PlayerSpawnedInWorld")]
    public class Patch_PlayerSpawnedInWorld
    {
        [HarmonyPostfix]
        public static void Postfix(ClientInfo _cInfo, RespawnType _respawnReason, Vector3i _pos, int _entityId)
        {
            // =================================================================
            // TEIL 1: Einmalige Server-Initialisierung
            // =================================================================
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                if (!DynamischesSpawnLimit.IstInitialisiert)
                {
                    DynamischesSpawnLimit.IstInitialisiert = true;
                    DynamischesSpawnLimit.InitialisiereWerte();
                    Log.Out("[EinmaligerSpawn] Late-Init: Spawns wurden für die Session initialisiert (Server).");
                }
            }

            // =================================================================
            // TEIL 2: Lokale UI-Aktualisierung (nur für den Spieler am PC)
            // =================================================================
            EntityPlayerLocal localPlayer = GameManager.Instance.World.GetPrimaryPlayer();

            // Wir prüfen, ob der Spieler, der gerade gespawnt ist, WIRKLICH der lokale Spieler am PC ist
            if (localPlayer != null && localPlayer.entityId == _entityId)
            {
                // 1. Karte basierend auf der lokalen Config aktualisieren
                KartenOverlay.Wiederherstellen();

                // 2. Lokalen Buff beim Spawnen aufräumen, falls deaktiviert
                if (!ModEinstellungen.ZeigeLokalenFortschritt)
                {
                    if (localPlayer.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
                    {
                        localPlayer.Buffs.RemoveBuff("buffEinmaligerSpawnProgress");
                    }
                }

                // =================================================================
                // NEU: POI Radar Manager lokal an den Spieler hängen
                // =================================================================
                if (localPlayer.gameObject.GetComponent<PoiRadarManager>() == null)
                {
                    localPlayer.gameObject.AddComponent<PoiRadarManager>();
                }

                Log.Out("[EinmaligerSpawn] Late-Init: Kartenoverlay, lokaler Fortschrittsbuff und POI-Radar wurden initialisiert.");
            }

            // =================================================================
            // TEIL 3: Netzwerk-Sync für beigetretene Clients
            // =================================================================

            // 1. Sicherheitscheck: Nur der Server darf Daten verschicken!
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            // 2. Ist es überhaupt ein externer Mitspieler (Client)? 
            // (Wenn der Host selbst spawnt, ist _cInfo = null. Der Host hat die Daten ja eh schon im RAM)
            if (_cInfo == null) return;

            // 3. Wir holen uns alle Chunk-Namen (Keys), die der Server in seinem Gedächtnis hat
            List<string> relevanteChunks = new List<string>();
            foreach (var kvp in KillCounter.ToteZombiesProChunk)
            {
                if (kvp.Value >= 1)
                {
                    relevanteChunks.Add(kvp.Key);
                }
            }

            // 4. Briefumschlag packen (Phase 1 Konstruktor mit der kompletten Liste)
            NetPackageChunkSync package = NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLogin(relevanteChunks);

            // 5. Das Paket GANZ GEZIELT nur an diesen einen Spieler senden
            _cInfo.SendPackage(package);

            Log.Out($"[EinmaligerSpawn] Netzwerk: Sende komplettes Chunk-Gedächtnis ({relevanteChunks.Count} Einträge) an Spieler {_cInfo.playerName}...");

            // 6. POI-Gedächtnis an den Client senden
            List<int> relevantePOIs = new List<int>(PoiDatenbank.GecleartePOIs.Keys);
            NetPackagePoiSync poiPackage = NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLogin(relevantePOIs);
            _cInfo.SendPackage(poiPackage);

            Log.Out($"[EinmaligerSpawn] Netzwerk: Sende komplettes POI-Gedächtnis ({relevantePOIs.Count} Einträge) an Spieler {_cInfo.playerName}...");
        }
    }
}