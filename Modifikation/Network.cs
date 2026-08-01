using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.Config;
using UnityEngine;

namespace EinmaligerSpawn.Network
{
    public class NetPackageChunkSync : NetPackage
    {
        private List<string> gesaeuberteChunks = new List<string>();

        // --- NEU: Config-Werte ---
        private bool isLoginSync = false;
        private bool chatNachrichtenAktiv;
        private int globalesZombieLimit;
        private bool lokalerChunkClearAktiv;
        private float spawnCheckIntervall;
        private bool taktischerKillAktiv;

        // 1. Standard-Konstruktor (Zwingend notwendig zum Empfangen)
        public NetPackageChunkSync()
        {
        }

        // 2. Konstruktor für Phase 1 (Login: Schickt die komplette Liste + Config)
        public NetPackageChunkSync(List<string> alleChunks)
        {
            this.gesaeuberteChunks = alleChunks;
            this.isLoginSync = true; // Schalter aktivieren!

            // Aktuelle Server-Werte in das Paket laden
            this.chatNachrichtenAktiv = ModEinstellungen.ChatNachrichtenAktiv;
            this.globalesZombieLimit = ModEinstellungen.GlobalesZombieLimit;
            this.lokalerChunkClearAktiv = ModEinstellungen.LokalerChunkClearAktiv;
            this.spawnCheckIntervall = ModEinstellungen.SpawnCheckIntervall;
            this.taktischerKillAktiv = ModEinstellungen.TaktischerKillAktiv;
        }

        // 3. Konstruktor für Phase 2 (Live-Update: Schickt nur einen Chunk, KEINE Config)
        public NetPackageChunkSync(string einzelnerChunk)
        {
            this.gesaeuberteChunks.Add(einzelnerChunk);
            this.isLoginSync = false; // Schalter bleibt aus
        }

        // =================================================================
        // SCHREIBEN (Der Server packt den Briefumschlag)
        // =================================================================
        public override void write(PooledBinaryWriter _writer)
        {
            System.IO.BinaryWriter baseWriter = _writer;

            // 1. Wir schreiben zuerst den Schalter auf den Umschlag
            baseWriter.Write(this.isLoginSync);

            // 2. Wenn es ein Login-Sync ist, schreiben wir die 5 Config-Werte
            if (this.isLoginSync)
            {
                baseWriter.Write(this.chatNachrichtenAktiv);
                baseWriter.Write(this.globalesZombieLimit);
                baseWriter.Write(this.lokalerChunkClearAktiv);
                baseWriter.Write(this.spawnCheckIntervall);
                baseWriter.Write(this.taktischerKillAktiv);
            }

            // 3. Danach schreiben wir wie gewohnt die Chunks
            baseWriter.Write((ushort)gesaeuberteChunks.Count);
            foreach (string chunkId in gesaeuberteChunks)
            {
                baseWriter.Write(chunkId);
            }
        }

        // =================================================================
        // LESEN (Der Client öffnet den Briefumschlag)
        // =================================================================
        public override void read(PooledBinaryReader _reader)
        {
            // 1. Schalter auslesen
            this.isLoginSync = _reader.ReadBoolean();

            // 2. Wenn es ein Login-Sync ist, Config-Werte auspacken
            if (this.isLoginSync)
            {
                this.chatNachrichtenAktiv = _reader.ReadBoolean();
                this.globalesZombieLimit = _reader.ReadInt32();
                this.lokalerChunkClearAktiv = _reader.ReadBoolean();
                this.spawnCheckIntervall = _reader.ReadSingle();
                this.taktischerKillAktiv = _reader.ReadBoolean();
            }

            // 3. Chunks auspacken
            ushort anzahl = _reader.ReadUInt16();
            gesaeuberteChunks.Clear();
            for (int i = 0; i < anzahl; i++)
            {
                gesaeuberteChunks.Add(_reader.ReadString());
            }
        }

        // =================================================================
        // VERARBEITEN (Was der Client tun soll)
        // =================================================================
        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null) return;
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            // --- TEIL 1: Config übernehmen (Nur beim Login) ---
            if (this.isLoginSync)
            {
                ModEinstellungen.ChatNachrichtenAktiv = this.chatNachrichtenAktiv;
                ModEinstellungen.GlobalesZombieLimit = this.globalesZombieLimit;
                ModEinstellungen.LokalerChunkClearAktiv = this.lokalerChunkClearAktiv;
                ModEinstellungen.SpawnCheckIntervall = this.spawnCheckIntervall;
                ModEinstellungen.TaktischerKillAktiv = this.taktischerKillAktiv;

                Log.Out("[EinmaligerSpawn] Netzwerk: Die Server-Regeln (Config) wurden erfolgreich empfangen und übernommen.");
            }

            // --- TEIL 2: Chunks übernehmen (Immer) ---
            bool datenGeaendert = false;
            foreach (string chunkId in gesaeuberteChunks)
            {
                if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                {
                    KillCounter.ToteZombiesProChunk[chunkId] = 1;
                    datenGeaendert = true;
                }
            }

            if (datenGeaendert && KartenOverlay.IstAktiv)
            {
                KartenOverlay.ErzwingeRedraw();
            }
        }

        // =================================================================
        // LÄNGE (Paketgröße berechnen)
        // =================================================================
        public override int GetLength()
        {
            int length = 1; // 1 Byte für den isLoginSync bool

            if (this.isLoginSync)
            {
                // 3x bool (3) + 1x int (4) + 1x float (4) = 11 Bytes
                length += 11;
            }

            // Bestehende Berechnung für Chunks
            length += 2 + (gesaeuberteChunks.Count * 10);
            return length;
        }
    }
}