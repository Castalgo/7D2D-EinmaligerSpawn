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

        public static string GetChunkId(Vector3i pos)
        {
            return $"{pos.x >> 4}_{pos.z >> 4}";
        }

        public static void AddToterZombie(Vector3i pos)
        {
            string id = GetChunkId(pos);
            if (!ToteZombiesProChunk.ContainsKey(id))
            {
                ToteZombiesProChunk[id] = 0;
            }
            ToteZombiesProChunk[id]++;
        }

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