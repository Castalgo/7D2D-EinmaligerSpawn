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

    public class NetPackagePoiSync : NetPackage
    {
        private List<int> gesaeubertePOIs = new List<int>();
        private bool isLoginSync = false;

        public NetPackagePoiSync() { }

        public NetPackagePoiSync SetupForLogin(List<int> allePois)
        {
            this.gesaeubertePOIs = new List<int>(allePois);
            this.isLoginSync = true;
            return this;
        }

        public NetPackagePoiSync SetupForLive(int einzelnerPoi)
        {
            this.gesaeubertePOIs.Clear();
            this.gesaeubertePOIs.Add(einzelnerPoi);
            this.isLoginSync = false;
            return this;
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            System.IO.BinaryWriter baseWriter = _writer;
            baseWriter.Write(this.isLoginSync);

            baseWriter.Write((ushort)gesaeubertePOIs.Count);
            foreach (int poiId in gesaeubertePOIs)
            {
                baseWriter.Write(poiId);
            }
        }

        public override void read(PooledBinaryReader _reader)
        {
            System.IO.BinaryReader baseReader = _reader;

            this.isLoginSync = baseReader.ReadBoolean();

            ushort anzahl = baseReader.ReadUInt16();
            gesaeubertePOIs.Clear();
            for (int i = 0; i < anzahl; i++)
            {
                gesaeubertePOIs.Add(baseReader.ReadInt32());
            }
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null) return;

            foreach (int poiId in gesaeubertePOIs)
            {
                bool warSchonGecleart = PoiDatenbank.IstGecleart(poiId);

                if (!warSchonGecleart)
                {
                    PoiDatenbank.SetzeGecleart(poiId);

                    // LOKALE CHAT-NACHRICHT FÜR DEN SPIELER
                    if (!this.isLoginSync && !GameManager.IsDedicatedServer && ModEinstellungen.ChatNachrichtenAktiv)
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

                    // erzwingt Minimap Update, sofern Minimap Mod aktiv
                    SimpleMinimap_Patch.ErzwingeRedraw = true;
                }
            }

            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && !this.isLoginSync)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                    NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(gesaeubertePOIs[0])
                );
            }
        }

        public override int GetLength()
        {
            return 1 + 2 + (gesaeubertePOIs.Count * 4);
        }
    }

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
}
