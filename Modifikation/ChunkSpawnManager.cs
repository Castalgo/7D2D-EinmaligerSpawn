// Bereitstellung einer Chunktabelle für bereits gespawnte Orte

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace EinmaligerSpawn.Modifikation
{
    public static class ChunkSpawnManager
    {
        public static HashSet<string> SpawnedChunks = new HashSet<string>();

        // Wird verwendet, um Chunk eindeutig zu identifizieren
        public static string GetChunkId(Vector3i position)
        {
            int chunkX = position.x >> 4;
            int chunkZ = position.z >> 4;
            return $"{chunkX}_{chunkZ}";
        }

        // Prüfen, ob ein Chunk bereits gespawnt wurde
        public static bool IsChunkAlreadySpawned(Vector3i position)
        {
            string id = GetChunkId(position);
            return SpawnedChunks.Contains(id);
        }

        // Markiere einen Chunk als gespawnt und speichere sofort
        public static void MarkChunkAsSpawned(Vector3i position)
        {
            string id = GetChunkId(position);
            if (SpawnedChunks.Add(id))
            {
                SaveToDisk(GetSaveFolderPath());
            }
        }

        // Lade gespeicherte Spawn-Informationen
        public static void LoadFromDisk(string saveGameFolder)
        {
            try
            {
                var path = Path.Combine(saveGameFolder, "Mods", "EinmaligerSpawn", "spawnedChunks.json");
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<ChunkSpawnData>(json);
                if (data?.spawnedChunks != null)
                    SpawnedChunks = new HashSet<string>(data.spawnedChunks);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[EinmaligerSpawn] Fehler beim Laden von spawnedChunks.json: {e.Message}");
            }
        }

        // Speichere Spawn-Informationen auf die Festplatte
        public static void SaveToDisk(string saveFolder)
        {
            try
            {
                string path = Path.Combine(saveFolder, "spawnedChunks.json");
                string json = JsonConvert.SerializeObject(SpawnedChunks, Formatting.Indented);
                File.WriteAllText(path, json);
                UnityEngine.Debug.Log("[EinmaligerSpawn] SpawnedChunks erfolgreich gespeichert.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[EinmaligerSpawn] Fehler beim Speichern von SpawnedChunks: {e.Message}");
            }
        }


        // Hole Savegame-Pfad
        public static string GetSaveFolderPath()
        {
            var world = GamePrefs.GetString(EnumGamePrefs.GameWorld);
            var gameName = GamePrefs.GetString(EnumGamePrefs.GameName);
            var basePath = GameIO.GetSaveGameDir();
            return Path.Combine(basePath, world, gameName);
        }
    }

    // Datenstruktur für JSON-Speicherung
    public class ChunkSpawnData
    {
        public string worldId;
        public List<string> spawnedChunks;
    }
}