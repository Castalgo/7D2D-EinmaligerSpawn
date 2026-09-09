using System;
using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.Minimap_Patch;
using EinmaligerSpawn.PoiTracker;
using UnityEngine;

namespace EinmaligerSpawn.Network
{
    // Synchronisiert den Spawnbarkeits-Status einzelner Chunks vom Server zum Client, 
    // damit die lokalen KillCounter-Listen der Spieler auf dem gleichen Stand bleiben.
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

            if (this.isLoginSync)
            {
                baseWriter.Write(this.globalesZombieLimit);
                baseWriter.Write(this.lokalerChunkClearAktiv);
                baseWriter.Write(this.spawnCheckIntervall);
                baseWriter.Write(this.taktischerKillAktiv);
            }

            baseWriter.Write((ushort)gesaeuberteChunks.Count);
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

            ushort anzahl = baseReader.ReadUInt16();
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
                if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                {
                    KillCounter.ToteZombiesProChunk[chunkId] = 1;
                    datenGeaendert = true;

                    // LOKALE CHAT-NACHRICHT FÜR DEN SPIELER
                    if (!this.isLoginSync && (ModEinstellungen.ChatNachrichtenModus == 2 || ModEinstellungen.ChatNachrichtenModus == 3))
                    {
                        ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                        string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
                        string feedbackMsg = $"[00FF00][{timeString}] Gebiet {chunkId} wurde dauerhaft gesäubert![-]";

                        GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                    }

                    // erzwingt Minimap Update, sofern Minimap Mod aktiv
                    SimpleMinimap_Patch.ErzwingeRedraw = true;
                }
            }

            if (datenGeaendert && KartenOverlay.IstAktiv)
            {
                KartenOverlay.ErzwingeRedraw();
            }
        }

        public override int GetLength()
        {
            int length = 1;

            if (this.isLoginSync)
            {
                length += 10;
            }

            length += 2 + (gesaeuberteChunks.Count * 10);
            return length;
        }
    }

    // Übermittelt den aktuellen POI-Fortschritt (1 = gesäubert, 2 = Quest-Endspurt) an alle Clients.
    // Aktualisiert die lokale Datenbank, zeichnet die Minimap neu und triggert Erfolgsnachrichten im Chat.
    public class NetPackagePoiSync : NetPackage
    {
        // Speichert jetzt die POI-ID (Key) und den zugehörigen Status (Value: 1 oder 2)
        private Dictionary<int, byte> syncPois = new Dictionary<int, byte>();
        private bool isLoginSync = false;

        public NetPackagePoiSync() { }

        public NetPackagePoiSync SetupForLogin(List<int> allePois)
        {
            this.syncPois.Clear();

            // FILTER: Sendet alle POIs, die einen Status > 0 haben (also 1 oder 2)
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

            baseWriter.Write((ushort)syncPois.Count);
            foreach (var kvp in syncPois)
            {
                baseWriter.Write(kvp.Key);   // 4 Bytes (Int32)
                baseWriter.Write(kvp.Value); // 1 Byte (Byte)
            }
        }

        public override void read(PooledBinaryReader _reader)
        {
            System.IO.BinaryReader baseReader = _reader;

            this.isLoginSync = baseReader.ReadBoolean();

            ushort anzahl = baseReader.ReadUInt16();
            this.syncPois.Clear();

            for (int i = 0; i < anzahl; i++)
            {
                this.syncPois.Add(baseReader.ReadInt32(), baseReader.ReadByte());
            }
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null) return;

            // WICHTIG: Nur Clients werten dieses Paket aus. 
            // Der Server hat seine eigene Datenbank bereits VOR dem Senden lokal aktualisiert.
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            foreach (var kvp in syncPois)
            {
                int poiId = kvp.Key;
                byte empfangenerStatus = kvp.Value;
                byte lokalerStatus = PoiDatenbank.LeseStatus(poiId);

                // Nur verarbeiten, wenn sich der Status wirklich geändert hat
                if (lokalerStatus != empfangenerStatus)
                {
                    PoiDatenbank.SetzeStatus(poiId, empfangenerStatus);

                    // CHAT-NACHRICHT NUR BEI STATUS 1 (Komplett gesäubert)
                    if (empfangenerStatus == 1 && !this.isLoginSync && (ModEinstellungen.ChatNachrichtenModus == 1 || ModEinstellungen.ChatNachrichtenModus == 3))
                    {
                        string poiName = "Unbekannt";
                        PrefabInstance poi = GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefab(poiId);
                        if (poi != null) poiName = poi.name;

                        ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                        string feedbackMsg = $"[00FF00][Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}] POI '{poiName}' wurde restlos gesäubert![-]";

                        // Schreibt die Nachricht in das lokale Chat-Fenster des Clients
                        GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                    }

                    SimpleMinimap_Patch.ErzwingeRedraw = true;
                }
            }
        }

        public override int GetLength()
        {
            // 1 (bool) + 2 (ushort) + (Anzahl * 5 Bytes pro Eintrag)
            return 1 + 2 + (syncPois.Count * 5);
        }
    }

    // Sendet dem Client auf Anfrage die exakte Koordinate und den Radar-Typ (2D oder 3D).
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
            // Client trägt die Server-Antwort in sein lokales Gedächtnis ein
            PoiTracker.PoiRadarManager.ClientZiele[this.poiId] = this.zielKoordinate;
            PoiTracker.PoiRadarManager.ClientMarkerKlassen[this.poiId] = this.markerKlasse;
        }
    }

    // Der Client bittet den Server, einen POI zu überprüfen. Der Server entscheidet.
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
                        PoiDatenbank.SetzeStatus(this.poiId, 1);
                        Log.Out($"[EinmaligerSpawn] Server-Prüfung bestätigt: POI '{poi.name}' ist leer (Status 1).");
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(this.poiId, 1));
                    }
                }
            }
            else if (this.requestedStatus == 2)
            {
                if (PoiDatenbank.LeseStatus(this.poiId) == 0)
                {
                    PoiDatenbank.SetzeStatus(this.poiId, 2);
                    Log.Out($"[EinmaligerSpawn] Server registriert Quest-Abschluss: POI '{poi.name}' (Status 2).");
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(this.poiId, 2));
                }
            }
            else if (this.requestedStatus == 3)
            {
                // RADAR-ANFRAGE: Server analysiert den Raum für den anfragenden Client
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
                        PoiDatenbank.SetzeStatus(this.poiId, 1);
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(this.poiId, 1));
                    }
                }
                else if (nextTarget != null && this.Sender != null)
                {
                    // Server entscheidet die Radar-Farbe basierend auf den echten Server-Zahlen
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

                    // Antwort gezielt nur an den suchenden Client senden
                    this.Sender.SendPackage(NetPackageManager.GetPackage<NetPackagePoiRadarUpdate>().Setup(this.poiId, nextTarget.Center, berechneteKlasse));
                }
            }
        }

        public override int GetLength() { return 5; }
    }
}