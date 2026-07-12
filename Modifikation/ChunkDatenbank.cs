using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;

namespace EinmaligerSpawn.Manager
{
    public static class ChunkDatenbank
    {
        // Speichert die Anzahl der GETÖTETEN Zombies pro Chunk
        public static Dictionary<string, int> ToteZombiesProChunk = new Dictionary<string, int>();

        // Das temporäre Gedächtnis (Entity-ID -> Ursprungs-Chunk-ID)
        public static Dictionary<int, string> ZombieUrsprung = new Dictionary<int, string>();

        public static string GetChunkId(Vector3i pos)
        {
            return $"{pos.x >> 4}_{pos.z >> 4}";
        }

        // Zählt einen Kill direkt über die Chunk-ID hoch
        public static void AddToterZombieNachID(string chunkId, int maxZombies)
        {
            if (!ToteZombiesProChunk.ContainsKey(chunkId))
            {
                ToteZombiesProChunk[chunkId] = 0;
            }

            ToteZombiesProChunk[chunkId]++;

            // Kompromisslose Rückeroberung: Wildnis-Chunks verriegeln nach exakt 1 Kill.
            int abriegelungsLimit = 1;

            if (ToteZombiesProChunk[chunkId] == abriegelungsLimit)
            {
                Debug.LogWarning($"[EinmaligerSpawn] ERFOLG! Chunk {chunkId} zählt jetzt als dauerhaft ausgerottet!");
            }
        }

        // Prüft, ob in diesem Chunk noch gespawnt werden darf
        public static bool IstChunkAusgerottet(Vector3i pos, int maxZombies)
        {
            string id = GetChunkId(pos);
            if (ToteZombiesProChunk.ContainsKey(id))
            {
                // Sobald auch nur 1 Kill registriert wurde, blockiert der Chunk neue Biom-Spawns
                return ToteZombiesProChunk[id] >= 1;
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
                    // Nutzt IntroSort: Wandelt das Dictionary intern in ein flaches Array um, 
                    // sortiert es extrem schnell und speicherschonend, und erstellt ein neues Dictionary für den Export.
                    var sortedChunks = ToteZombiesProChunk
                        .OrderBy(kvp => kvp.Key)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    string json = JsonConvert.SerializeObject(sortedChunks, Formatting.Indented);
                    File.WriteAllText(path, json);
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

                // Wir erstellen eine sortierte Kopie. SortedDictionary sortiert Strings alphabetisch.
                var sortedChunks = new SortedDictionary<string, int>(ToteZombiesProChunk);

                // Wir serialisieren die sortierte Kopie, nicht das Original
                string json = JsonConvert.SerializeObject(sortedChunks, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EinmaligerSpawn] Fehler beim Speichern der Chunks: {e.Message}");
            }
        }
    }
}