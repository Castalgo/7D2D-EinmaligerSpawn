using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace EinmaligerSpawn.Manager
{
    public static class ChunkDatenbank
    {
        // Speichert die Anzahl der GETÖTETEN Zombies pro Chunk
        public static Dictionary<string, int> ToteZombiesProChunk = new Dictionary<string, int>();

        // NEU: Das temporäre Gedächtnis (Entity-ID -> Ursprungs-Chunk-ID)
        public static Dictionary<int, string> ZombieUrsprung = new Dictionary<int, string>();

        public static string GetChunkId(Vector3i pos)
        {
            return $"{pos.x >> 4}_{pos.z >> 4}";
        }

        // NEU: Zählt einen Kill direkt über die Chunk-ID hoch
        public static void AddToterZombieNachID(string chunkId, int maxZombies)
        {
            if (!ToteZombiesProChunk.ContainsKey(chunkId))
            {
                ToteZombiesProChunk[chunkId] = 0;
            }

            ToteZombiesProChunk[chunkId]++;

            if (ToteZombiesProChunk[chunkId] == maxZombies)
            {
                Debug.Log($"[EinmaligerSpawn] ERFOLG! Chunk {chunkId} wurde soeben dauerhaft ausgerottet! ({maxZombies}/{maxZombies} Kills)");
            }
        }

        // (Die alte AddToterZombie-Methode haben wir entfernt, da wir jetzt die neue nutzen)

        public static bool IstChunkAusgerottet(Vector3i pos, int maxZombies)
        {
            string id = GetChunkId(pos);
            if (ToteZombiesProChunk.ContainsKey(id))
            {
                return ToteZombiesProChunk[id] >= maxZombies;
            }
            return false;
        }

        public static void Load(string saveDir)
        {
            string path = Path.Combine(saveDir, "ausgerotteteChunks.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    ToteZombiesProChunk = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                    Debug.Log($"[EinmaligerSpawn] {ToteZombiesProChunk.Count} Chunk-Daten geladen.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EinmaligerSpawn] Fehler beim Laden der Chunks: {e.Message}");
                }
            }
            else
            {
                ToteZombiesProChunk.Clear();
            }
        }

        public static void Save(string saveDir)
        {
            try
            {
                string path = Path.Combine(saveDir, "ausgerotteteChunks.json");
                string json = JsonConvert.SerializeObject(ToteZombiesProChunk, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EinmaligerSpawn] Fehler beim Speichern der Chunks: {e.Message}");
            }
        }
    }
}