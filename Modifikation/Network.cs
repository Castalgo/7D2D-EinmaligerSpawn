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
                    if (!this.isLoginSync && ModEinstellungen.ChatNachrichtenAktiv)
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

        // NIMMT JETZT 2 ARGUMENTE AN (Abwärtskompatibel durch '= 1')
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
                    if (empfangenerStatus == 1 && !this.isLoginSync && !GameManager.IsDedicatedServer && ModEinstellungen.ChatNachrichtenAktiv)
                    {
                        string poiName = "Unbekannt";
                        PrefabInstance poi = GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefab(poiId);
                        if (poi != null)
                        {
                            poiName = poi.name;
                        }

                        ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                        string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
                        string feedbackMsg = $"[00FF00][{timeString}] POI '{poiName}' wurde restlos gesäubert![-]";

                        GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                    }

                    SimpleMinimap_Patch.ErzwingeRedraw = true;
                }
            }

            // SICHERHEIT: Server-Relay für Multiplayer-Live-Sync
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && !this.isLoginSync && this.syncPois.Count > 0)
            {
                foreach (var kvp in syncPois)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                        NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(kvp.Key, kvp.Value)
                    );
                    break; // Live-Sync betrifft immer nur einen POI
                }
            }
        }

        public override int GetLength()
        {
            // 1 (bool) + 2 (ushort) + (Anzahl * 5 Bytes pro Eintrag)
            return 1 + 2 + (syncPois.Count * 5);
        }
    }

    // Sendet dem Client kontinuierlich die exakte Vector3-Koordinate des nächsten ungesäuberten Raums,
    // damit das lokale Radar-System den 2D-Punkt oder 3D-Marker präzise in der Spielwelt platzieren kann.
    public class NetPackagePoiRadarUpdate : NetPackage
    {
            private int poiId;
            private Vector3 zielKoordinate;

            // Leerer Konstruktor für die Engine
            public NetPackagePoiRadarUpdate() { }

            // Setup für den Versand durch den Server
            public NetPackagePoiRadarUpdate Setup(int _poiId, Vector3 _zielKoordinate)
            {
                this.poiId = _poiId;
                this.zielKoordinate = _zielKoordinate;
                return this;
            }

            // Paket-Länge in Bytes (int = 4, Vector3 = 12 -> 16 Bytes)
            public override int GetLength()
            {
                return 16;
            }

            // Schreiben der Daten in den Stream (Server)
            public override void write(PooledBinaryWriter _writer)
            {
                base.write(_writer);
                System.IO.BinaryWriter baseWriter = _writer;

                baseWriter.Write(this.poiId);
                baseWriter.Write(this.zielKoordinate.x);
                baseWriter.Write(this.zielKoordinate.y);
                baseWriter.Write(this.zielKoordinate.z);
            }

            // Lesen der Daten aus dem Stream (Client)
            public override void read(PooledBinaryReader _reader)
            {
                System.IO.BinaryReader baseReader = _reader;

                this.poiId = baseReader.ReadInt32(); // Hier baseReader nutzen
                this.zielKoordinate = new Vector3(baseReader.ReadSingle(), baseReader.ReadSingle(), baseReader.ReadSingle());
            }

            // Ausführung, wenn das Paket ankommt
            public override void ProcessPackage(World _world, GameManager _callbacks)
            {
                // Trägt die empfangene Koordinate ins "Gedächtnis" des Clients ein
                if (_world == null) return;
                PoiTracker.PoiRadarManager.ClientZiele[this.poiId] = this.zielKoordinate;

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
            // SICHERHEIT: Nur der Server darf diese Prüfungen ausführen!
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            DynamicPrefabDecorator decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            PrefabInstance poi = decorator?.GetPrefab(this.poiId);
            if (poi == null) return;

            if (this.requestedStatus == 1)
            {
                // SERVER-VERIFIZIERUNG: Der Server zählt seine eigenen Sleeper-Volumen!
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

                // Wenn der Server bestätigt, dass der POI leer ist:
                if (totalValid > 0 && clearedValid >= totalValid)
                {
                    if (PoiDatenbank.LeseStatus(this.poiId) != 1)
                    {
                        PoiDatenbank.SetzeStatus(this.poiId, 1);
                        Log.Out($"[EinmaligerSpawn] Server-Prüfung bestätigt: POI '{poi.name}' ist leer (Status 1).");

                        // Jetzt verteilt der Server den Status an alle Clients
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(this.poiId, 1));
                    }
                }
                else
                {
                    Log.Warning($"[EinmaligerSpawn] Client-Meldung abgelehnt! POI '{poi.name}' ist serverseitig noch nicht leer.");
                }
            }
            else if (this.requestedStatus == 2)
            {
                // Quest ReadyForTurnIn: Quests werden vom Client verwaltet, wir vertrauen hier dem Besitzer der Quest.
                if (PoiDatenbank.LeseStatus(this.poiId) == 0)
                {
                    PoiDatenbank.SetzeStatus(this.poiId, 2);
                    Log.Out($"[EinmaligerSpawn] Server registriert Quest-Abschluss: POI '{poi.name}' (Status 2).");
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(this.poiId, 2));
                }
            }
        }

        public override int GetLength()
        {
            // 4 Bytes (Int32) + 1 Byte (Byte)
            return 5;
        }
    }
}
