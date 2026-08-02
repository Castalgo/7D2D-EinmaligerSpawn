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

        // --- Config-Werte ---
        private bool isLoginSync = false;
        private bool chatNachrichtenAktiv;
        private int globalesZombieLimit;
        private bool lokalerChunkClearAktiv;
        private float spawnCheckIntervall;
        private bool taktischerKillAktiv;

        // 1. Standard-Konstruktor (Zwingend notwendig zum Empfangen)
        public NetPackageChunkSync() { }

        // 2. Konstruktor für Phase 1 (Login: Schickt die komplette Liste + Config)
        public NetPackageChunkSync SetupForLogin(List<string> alleChunks)
        {
            this.gesaeuberteChunks = new List<string>(alleChunks);
            this.isLoginSync = true;

            this.chatNachrichtenAktiv = ModEinstellungen.ChatNachrichtenAktiv;
            this.globalesZombieLimit = ModEinstellungen.GlobalesZombieLimit;
            this.lokalerChunkClearAktiv = ModEinstellungen.LokalerChunkClearAktiv;
            this.spawnCheckIntervall = ModEinstellungen.SpawnCheckIntervall;
            this.taktischerKillAktiv = ModEinstellungen.TaktischerKillAktiv;

            return this;
        }

        // 3. Konstruktor für Phase 2 (Live-Update: Schickt nur einen Chunk, KEINE Config)
        public NetPackageChunkSync SetupForLive(string einzelnerChunk)
        {
            this.gesaeuberteChunks.Clear();
            this.gesaeuberteChunks.Add(einzelnerChunk);
            this.isLoginSync = false;

            return this;
        }

        // =================================================================
        // SCHREIBEN (Der Server packt den Briefumschlag)
        // =================================================================
        public override void write(PooledBinaryWriter _writer)
        {
            // WICHTIG: Zwingend erforderlich, damit die Engine die ID 
            // in den Stream schreibt (exakt wie im Vanilla-Code).
            base.write(_writer);

            System.IO.BinaryWriter baseWriter = _writer;

            baseWriter.Write(this.isLoginSync);

            if (this.isLoginSync)
            {
                baseWriter.Write(this.chatNachrichtenAktiv);
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

        // =================================================================
        // LESEN (Der Client öffnet den Briefumschlag)
        // =================================================================
        public override void read(PooledBinaryReader _reader)
        {
            // WICHTIG: KEIN base.read aufrufen (exakt wie im Vanilla-Code).
            // Die Engine hat die ID zu diesem Zeitpunkt bereits ausgelesen!

            System.IO.BinaryReader baseReader = _reader;

            this.isLoginSync = baseReader.ReadBoolean();

            if (this.isLoginSync)
            {
                this.chatNachrichtenAktiv = baseReader.ReadBoolean();
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
            // Die nackte Paketlänge deiner Daten (kein base.GetLength!)
            int length = 1; // 1 Byte für isLoginSync

            if (this.isLoginSync)
            {
                // 3x bool (3) + 1x int (4) + 1x float (4) = 11 Bytes
                length += 11;
            }

            // 2 Bytes für ushort Count + (Anzahl * geschätzte String-Länge)
            length += 2 + (gesaeuberteChunks.Count * 10);
            return length;
        }
    }
}