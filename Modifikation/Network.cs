using System.Collections.Generic;
using EinmaligerSpawn.Benachrichtigungen;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.PoiTracker;
using UnityEngine;

namespace EinmaligerSpawn.Network
{
    // Synchronisiert den Spawnbarkeits-Status einzelner Chunks vom Server zum Client, 
    // damit die lokalen ChunkClearManager-Listen der Spieler auf dem gleichen Stand bleiben.
    public class NetPackageChunkSync : NetPackage
    {
        private List<string> gesaeuberteChunks = new List<string>();

        // --- Config-Werte ---
        private bool isLoginSync = false;
        private int globalesZombieLimit;
        private bool lokalerChunkClearAktiv;
        private float spawnCheckIntervall;
        private bool taktischerKillAktiv;

        public NetPackageChunkSync() { }

        public NetPackageChunkSync SetupForLogin(List<string> alleChunks)
        {
            this.gesaeuberteChunks = new List<string>(alleChunks);
            this.isLoginSync = true;

            // Beim Login erzwingt der Server seine Config-Werte auf dem Client, 
            // um Cheating durch modifizierte lokale Configs zu verhindern.
            this.globalesZombieLimit = ModEinstellungen.GlobalesZombieLimit;
            this.lokalerChunkClearAktiv = ModEinstellungen.LokalerChunkClearAktiv;
            this.spawnCheckIntervall = ModEinstellungen.SpawnCheckIntervall;
            this.taktischerKillAktiv = ModEinstellungen.TaktischerKillAktiv;

            return this;
        }

        public NetPackageChunkSync SetupForLive(string einzelnerChunk)
        {
            this.gesaeuberteChunks.Clear();
            this.gesaeuberteChunks.Add(einzelnerChunk);
            this.isLoginSync = false;

            return this;
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            System.IO.BinaryWriter baseWriter = _writer;

            baseWriter.Write(this.isLoginSync);

            // Config-Sync wird nur einmalig beim Login gesendet, 
            // um die Paketgröße im laufenden Spiel (Live-Sync) minimal zu halten.
            if (this.isLoginSync)
            {
                baseWriter.Write(this.globalesZombieLimit);
                baseWriter.Write(this.lokalerChunkClearAktiv);
                baseWriter.Write(this.spawnCheckIntervall);
                baseWriter.Write(this.taktischerKillAktiv);
            }

            baseWriter.Write(gesaeuberteChunks.Count);
            foreach (string chunkId in gesaeuberteChunks)
            {
                baseWriter.Write(chunkId);
            }
        }

        public override void read(PooledBinaryReader _reader)
        {
            System.IO.BinaryReader baseReader = _reader;

            this.isLoginSync = baseReader.ReadBoolean();

            if (this.isLoginSync)
            {
                this.globalesZombieLimit = baseReader.ReadInt32();
                this.lokalerChunkClearAktiv = baseReader.ReadBoolean();
                this.spawnCheckIntervall = baseReader.ReadSingle();
                this.taktischerKillAktiv = baseReader.ReadBoolean();
            }

            int anzahl = baseReader.ReadInt32();
            gesaeuberteChunks.Clear();
            for (int i = 0; i < anzahl; i++)
            {
                gesaeuberteChunks.Add(baseReader.ReadString());
            }
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null) return;
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            if (this.isLoginSync)
            {
                ModEinstellungen.GlobalesZombieLimit = this.globalesZombieLimit;
                ModEinstellungen.LokalerChunkClearAktiv = this.lokalerChunkClearAktiv;
                ModEinstellungen.SpawnCheckIntervall = this.spawnCheckIntervall;
                ModEinstellungen.TaktischerKillAktiv = this.taktischerKillAktiv;

                Log.Out("[EinmaligerSpawn] Netzwerk: Die Server-Regeln (Config) wurden erfolgreich empfangen und übernommen.");
            }

            bool datenGeaendert = false;
            foreach (string chunkId in gesaeuberteChunks)
            {
                if (!ChunkClearManager.HatChunkEintrag(chunkId))
                {
                    // Trägt den Chunk lokal ein. Minimap-Redraw ist im Wrapper bereits enthalten.
                    ChunkClearManager.VerarbeiteScannerBatch(new List<string> { chunkId }, 1);
                    datenGeaendert = true;

                    // Unterdrückt Chat-Nachrichten während des Logins
                    // Kapselung: Zentrale UI-Benachrichtigung (Config-Check passiert intern)
                    if (!this.isLoginSync)
                    {
                        NotificationManager.SendeChunkClear(chunkId);
                    }
                }
            }

            // Kapselung: Zentrales Map-Update für Minimap & Weltkarte wird nur einmal am Ende der Schleife ausgelöst, 
            // um bei massenhaften Updates (z. B. Admin-Reset) die FPS nicht in den Keller zu treiben.
            if (datenGeaendert)
            {
                KartenOverlay.RequestMapUpdate();
            }
        }

        public override int GetLength()
        {
            int length = 1;

            if (this.isLoginSync)
            {
                length += 10;
            }

            length += 4 + (gesaeuberteChunks.Count * 10);
            return length;
        }
    }

    // Übermittelt den aktuellen POI-Fortschritt (1 = gesäubert, 2 = Quest-Endspurt) an alle Clients.
    public class NetPackagePoiSync : NetPackage
    {
        private Dictionary<int, byte> syncPois = new Dictionary<int, byte>();
        private bool isLoginSync = false;

        public NetPackagePoiSync() { }

        public NetPackagePoiSync SetupForLogin(List<int> allePois)
        {
            this.syncPois.Clear();

            // Filtert Gebäude mit Status 0 (unangetastet) aus, 
            // da der Standardwert bei Clients ohnehin 0 ist. Das spart massiv Bandbreite beim Login.
            foreach (int poiId in allePois)
            {
                byte status = PoiDatenbank.LeseStatus(poiId);
                if (status > 0)
                {
                    this.syncPois.Add(poiId, status);
                }
            }

            this.isLoginSync = true;
            return this;
        }

        public NetPackagePoiSync SetupForLive(int einzelnerPoi, byte neuerStatus = 1)
        {
            this.syncPois.Clear();
            this.syncPois.Add(einzelnerPoi, neuerStatus);
            this.isLoginSync = false;
            return this;
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            System.IO.BinaryWriter baseWriter = _writer;
            baseWriter.Write(this.isLoginSync);

            baseWriter.Write(syncPois.Count);
            foreach (var kvp in syncPois)
            {
                baseWriter.Write(kvp.Key);
                baseWriter.Write(kvp.Value);
            }
        }

        public override void read(PooledBinaryReader _reader)
        {
            System.IO.BinaryReader baseReader = _reader;

            this.isLoginSync = baseReader.ReadBoolean();

            int anzahl = baseReader.ReadInt32();
            this.syncPois.Clear();

            for (int i = 0; i < anzahl; i++)
            {
                this.syncPois.Add(baseReader.ReadInt32(), baseReader.ReadByte());
            }
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null) return;
            // WICHTIG: Der Server sendet dieses Paket, wertet es aber nicht selbst aus, 
            // da er seine eigene Datenbank vor dem Senden bereits aktualisiert hat.
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            foreach (var kvp in syncPois)
            {
                int poiId = kvp.Key;
                byte empfangenerStatus = kvp.Value;
                byte lokalerStatus = PoiDatenbank.LeseStatus(poiId);

                if (lokalerStatus != empfangenerStatus)
                {
                    // Kapselung: Client trägt Status ein und löst lokalen UI-Sync (Minimap) aus.
                    PoiDatenbank.VerarbeitePoiStatus(poiId, empfangenerStatus);

                    if (empfangenerStatus == 1 && !this.isLoginSync)
                    {
                        string poiName = "Unbekannt";
                        PrefabInstance poi = GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefab(poiId);
                        if (poi != null) poiName = poi.name;

                        // Kapselung: Zentrale UI-Benachrichtigung
                        NotificationManager.SendePoiClear(poiName);
                    }
                }
            }
        }

        public override int GetLength()
        {
            return 1 + 4 + (syncPois.Count * 5);
        }
    }

    // Sendet dem Client auf Anfrage die exakte Koordinate und den Radar-Typ (2D oder 3D).
    // Wird benötigt, da Clients nicht alle Entities/Sleeper eines weitläufigen POIs verlässlich in ihrem RAM haben.
    public class NetPackagePoiRadarUpdate : NetPackage
    {
        private int poiId;
        private Vector3 zielKoordinate;
        private string markerKlasse;

        public NetPackagePoiRadarUpdate() { }

        public NetPackagePoiRadarUpdate Setup(int _poiId, Vector3 _zielKoordinate, string _markerKlasse)
        {
            this.poiId = _poiId;
            this.zielKoordinate = _zielKoordinate;
            this.markerKlasse = _markerKlasse;
            return this;
        }

        public override int GetLength() { return 30; }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            System.IO.BinaryWriter baseWriter = _writer;
            baseWriter.Write(this.poiId);
            baseWriter.Write(this.zielKoordinate.x);
            baseWriter.Write(this.zielKoordinate.y);
            baseWriter.Write(this.zielKoordinate.z);
            baseWriter.Write(this.markerKlasse);
        }

        public override void read(PooledBinaryReader _reader)
        {
            System.IO.BinaryReader baseReader = _reader;
            this.poiId = baseReader.ReadInt32();
            this.zielKoordinate = new Vector3(baseReader.ReadSingle(), baseReader.ReadSingle(), baseReader.ReadSingle());
            this.markerKlasse = baseReader.ReadString();
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null) return;
            // Client trägt die Server-Antwort in sein lokales Gedächtnis ein, 
            // damit der PoiRadarManager die NavObjects flüssig im Update()-Tick zeichnen kann.
            PoiTracker.PoiRadarManager.ClientZiele[this.poiId] = this.zielKoordinate;
            PoiTracker.PoiRadarManager.ClientMarkerKlassen[this.poiId] = this.markerKlasse;
        }
    }

    // Der Client bittet den Server, einen POI zu überprüfen. Der Server als alleinige Autorität entscheidet.
    public class NetPackageRequestPoiCheck : NetPackage
    {
        private int poiId;
        private byte requestedStatus;

        public NetPackageRequestPoiCheck() { }

        public NetPackageRequestPoiCheck Setup(int _poiId, byte _requestedStatus)
        {
            this.poiId = _poiId;
            this.requestedStatus = _requestedStatus;
            return this;
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            System.IO.BinaryWriter baseWriter = _writer;
            baseWriter.Write(this.poiId);
            baseWriter.Write(this.requestedStatus);
        }

        public override void read(PooledBinaryReader _reader)
        {
            System.IO.BinaryReader baseReader = _reader;
            this.poiId = baseReader.ReadInt32();
            this.requestedStatus = baseReader.ReadByte();
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            DynamicPrefabDecorator decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            PrefabInstance poi = decorator?.GetPrefab(this.poiId);
            if (poi == null) return;

            if (this.requestedStatus == 1)
            {
                // Zählt die tatsächlichen Sleeper im serverseitigen RAM (nicht render-abhängig).
                int totalValid = 0;
                int clearedValid = 0;
                if (poi.sleeperVolumes != null)
                {
                    foreach (SleeperVolume vol in poi.sleeperVolumes)
                    {
                        if (vol.IsTrigger || vol.isQuestExclude) continue;
                        totalValid++;
                        if (vol.wasCleared) clearedValid++;
                    }
                }

                if (totalValid > 0 && clearedValid >= totalValid)
                {
                    if (PoiDatenbank.LeseStatus(this.poiId) != 1)
                    {
                        Log.Out($"[EinmaligerSpawn] Server-Prüfung bestätigt: POI '{poi.name}' ist leer (Status 1).");
                        PoiDatenbank.VerarbeitePoiStatus(this.poiId, 1);
                    }
                }
            }
            else if (this.requestedStatus == 2)
            {
                // Status 2 bedeutet: Gebäude durch Quest-Marker reaktiviert.
                if (PoiDatenbank.LeseStatus(this.poiId) == 0)
                {
                    bool questAuthentifiziert = false;
                    EntityPlayer requestingPlayer = null;

                    if (this.Sender != null)
                    {
                        GameManager.Instance.World.Players.dict.TryGetValue(this.Sender.entityId, out requestingPlayer);
                    }
                    else if (!GameManager.IsDedicatedServer)
                    {
                        requestingPlayer = GameManager.Instance.World.GetPrimaryPlayer();
                    }

                    // SECURITY-CHECK: Der Server verifiziert zwingend im eigenen Journal des Spielers, 
                    // ob dieser dort wirklich eine abschlussbereite Quest besitzt. Verhindert Exploit-Cheating.
                    if (requestingPlayer != null && requestingPlayer.QuestJournal != null && requestingPlayer.QuestJournal.quests != null)
                    {
                        foreach (Quest q in requestingPlayer.QuestJournal.quests)
                        {
                            if (q.CurrentState == Quest.QuestState.ReadyForTurnIn)
                            {
                                if (q.GetPositionData(out Vector3 qPos, Quest.PositionDataTypes.POIPosition))
                                {
                                    PrefabInstance qPoi = GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefabAtPosition(qPos);
                                    if (qPoi != null && qPoi.id == this.poiId)
                                    {
                                        questAuthentifiziert = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (questAuthentifiziert)
                    {
                        Log.Out($"[EinmaligerSpawn] Server verifiziert Quest-Abschluss: POI '{poi.name}' (Status 2).");
                        PoiDatenbank.VerarbeitePoiStatus(this.poiId, 2);
                    }
                    else
                    {
                        string playerName = requestingPlayer != null ? requestingPlayer.EntityName : "Unbekannt";
                        Log.Warning($"[EinmaligerSpawn] SECURITY-BLOCK: Client '{playerName}' hat versucht, POI '{poi.name}' als Quest abzuschließen, besitzt dort aber keine gültige Quest!");
                    }
                }
            }
            else if (this.requestedStatus == 3)
            {
                int totalValid = 0;
                int clearedValid = 0;
                bool hasUnclearedBossRoom = false;
                SleeperVolume nextTarget = null;

                if (poi.sleeperVolumes != null)
                {
                    foreach (SleeperVolume vol in poi.sleeperVolumes)
                    {
                        if (vol.IsTrigger || vol.isQuestExclude) continue;

                        totalValid++;
                        if (vol.wasCleared) clearedValid++;
                        else
                        {
                            if (vol.isPriority) hasUnclearedBossRoom = true;
                            if (nextTarget == null) nextTarget = vol;
                        }
                    }
                }

                if (totalValid > 0 && clearedValid >= totalValid)
                {
                    if (PoiDatenbank.LeseStatus(this.poiId) != 1)
                    {
                        PoiDatenbank.VerarbeitePoiStatus(this.poiId, 1);
                    }
                }
                else if (nextTarget != null && this.Sender != null)
                {
                    // Berechnet die Warnstufe (Markerfarbe) auf Basis dynamischer Schwellenwerte.
                    // Die Berechnung erfolgt auf dem Server, um Diskrepanzen bei Clients (z.B. durch Lag) zu vermeiden.
                    string berechneteKlasse = "es_poi_map_only";
                    if (poi.prefab != null && poi.prefab.DifficultyTier > 0)
                    {
                        byte poiStatus = PoiDatenbank.LeseStatus(poi.id);
                        if (poiStatus == 2)
                        {
                            berechneteKlasse = "es_poi_local";
                        }
                        else
                        {
                            int remaining = totalValid - clearedValid;
                            int threshold = (totalValid < 5) ? 1 : (totalValid < 10) ? 2 : (totalValid < 20) ? 3 : 4;
                            if (!hasUnclearedBossRoom && remaining <= threshold)
                            {
                                berechneteKlasse = "es_poi_local";
                            }
                        }
                    }

                    // Die Radar-Daten sind für den Spielfortschritt irrelevant und hoch dynamisch, 
                    // daher gehen sie nur an den spezifischen Anfrager zurück und nicht an alle Clients.
                    this.Sender.SendPackage(NetPackageManager.GetPackage<NetPackagePoiRadarUpdate>().Setup(this.poiId, nextTarget.Center, berechneteKlasse));
                }
            }
        }

        public override int GetLength() { return 5; }
    }
}