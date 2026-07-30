using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.KartenOverlayManager;

namespace EinmaligerSpawn.Network
{
    public class NetPackageChunkSync : NetPackage
    {
        private List<string> gesaeuberteChunks = new List<string>();

        // 1. Standard-Konstruktor (Zwingend notwendig, damit die Engine leere Pakete zum Empfangen bauen kann)
        public NetPackageChunkSync()
        {
        }

        // 2. Konstruktor für Phase 1 (Login: Schickt die komplette Liste)
        public NetPackageChunkSync(List<string> alleChunks)
        {
            this.gesaeuberteChunks = alleChunks;
        }

        // 3. Konstruktor für Phase 2 (Live-Update: Schickt nur einen einzigen, neuen Chunk)
        public NetPackageChunkSync(string einzelnerChunk)
        {
            this.gesaeuberteChunks.Add(einzelnerChunk);
        }

        // =================================================================
        // SCHREIBEN (Der Server packt den Briefumschlag)
        // =================================================================
        public override void write(PooledBinaryWriter _writer)
        { 
            System.IO.BinaryWriter baseWriter = _writer;

            // Zuerst schreiben wir auf den Umschlag, wie viele Chunks drin sind
            baseWriter.Write((ushort)gesaeuberteChunks.Count);

            // Danach schreiben wir jeden einzelnen Chunk-Namen als Text (String) hinein
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
            // Zuerst lesen wir, wie viele Chunks wir gleich auspacken müssen
            ushort anzahl = _reader.ReadUInt16();

            gesaeuberteChunks.Clear();
            for (int i = 0; i < anzahl; i++)
            {
                // Wir lesen jeden Chunk-Namen aus und packen ihn in unsere Liste
                gesaeuberteChunks.Add(_reader.ReadString());
            }
        }

        // =================================================================
        // VERARBEITEN (Was der Client mit der ausgepackten Liste tun soll)
        // =================================================================
        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null) return;

            // Sicherheitscheck: Der Server schickt die Daten, er soll sie nicht selbst empfangen!
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            bool datenGeaendert = false;

            // Wir tragen alle empfangenen Chunks in das lokale Dictionary des Clients ein
            foreach (string chunkId in gesaeuberteChunks)
            {
                if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                {
                    KillCounter.ToteZombiesProChunk[chunkId] = 1;
                    datenGeaendert = true;
                }
            }

            // Wenn es neue Daten gab, sagen wir der Karte, dass sie sich neu zeichnen soll
            if (datenGeaendert && KartenOverlay.IstAktiv)
            {
                KartenOverlay.ErzwingeRedraw();
            }
        }

        // =================================================================
        // LÄNGE (Die Engine will grob wissen, wie groß das Paket im Netzwerk ist)
        // =================================================================
        public override int GetLength()
        {
            // 2 Bytes für die Anzahl + ca. 10 Bytes pro Chunk-Name
            return 2 + (gesaeuberteChunks.Count * 10);
        }
    }
}