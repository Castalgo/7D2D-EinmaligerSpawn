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

                // Holt die aktuelle In-Game-Zeit (z.B. Tag 4, 14:35)
                ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
                string feedbackMsg = $"[00FF00][{timeString}] Chunk {chunkId} zählt jetzt als dauerhaft ausgerottet.[-]";
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);

                if (KartenOverlayManager.IstAktiv)
                {
                    KartenOverlayManager.ZeichneMarker(chunkId);
                }

            }
        }

        // Verarbeitet die taktischen Kills (Nachbar-Clear oder Gekitet) sauber an einem Ort
        public static void VerbucheTaktischenKill(string chunkId, bool istNachbar)
        {
            // Sicherheitsprüfung: Falls der Chunk ohnehin schon leer ist, nur hochzählen
            if (ToteZombiesProChunk.ContainsKey(chunkId) && ToteZombiesProChunk[chunkId] >= 1)
            {
                ToteZombiesProChunk[chunkId]++;
                return;
            }

            // Chunk auf gesäubert setzen
            ToteZombiesProChunk[chunkId] = 1;

            // NEU: Live-Update für die Karte auslösen
            if (KartenOverlayManager.IstAktiv)
            {
                KartenOverlayManager.ZeichneMarker(chunkId);
            }

            // Chatnachricht und Konsole vorbereiten
            ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
            string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
            string feedbackMsg;

            if (istNachbar)
            {
                Debug.LogWarning($"[EinmaligerSpawn] Taktischer Bonus: Nachbar {chunkId} zusätzlich gesichert!");
                feedbackMsg = $"[00FF00][{timeString}] Flächensäuberungsbonus: Angrenzendes Gebiet {chunkId} clear.[-]";
            }
            else
            {
                Debug.LogWarning($"[EinmaligerSpawn] Taktischer Clear! Todes-Chunk {chunkId} wurde gesichert.");
                feedbackMsg = $"[00FF00][{timeString}] Taktische Säuberung: Gebiet {chunkId} clear.[-]";
            }

            if (ModEinstellungen.ChatNachrichtenAktiv) // nur wenn die Chatnachrichten aktiviert sind, wird die Nachricht gesendet
            {
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
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
                    // Lese die JSON-Datei aus und befülle das Dictionary
                    string json = File.ReadAllText(path);
                    ToteZombiesProChunk = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                    Debug.Log($"[EinmaligerSpawn] {ToteZombiesProChunk.Count} Chunk-Daten erfolgreich geladen.");
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

                // Hochperformante Sortierung (IntroSort) speziell für riesige Listen beim Speichern
                var sortedChunks = ToteZombiesProChunk
                    .OrderBy(kvp => kvp.Key)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

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