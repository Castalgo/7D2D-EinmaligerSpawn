using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.Minimap_Patch;
using EinmaligerSpawn.Network;
using Newtonsoft.Json;
using UnityEngine;

namespace EinmaligerSpawn.ChunkDatenbank
{
    public static class KillCounter
    {
        // Speichert die Anzahl der GETÖTETEN Zombies pro Chunk
        public static Dictionary<string, int> ToteZombiesProChunk = new Dictionary<string, int>();

        // Das temporäre Gedächtnis (Entity-ID -> Ursprungs-Chunk-ID)
        public static Dictionary<int, string> ZombieUrsprung = new Dictionary<int, string>();

        public static string GetChunkId(Vector3i pos)
        {
            return $"{pos.x >> 4}_{pos.z >> 4}";
        }

        // Nur Server: Zählt einen Kill direkt über die Chunk-ID hoch
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
                Log.Warning($"[EinmaligerSpawn] ERFOLG! Chunk {chunkId} zählt jetzt als dauerhaft ausgerottet!");

                // Chatnachricht im Einzelspieler und für den Host im Multiplayer
                if (!GameManager.IsDedicatedServer && ModEinstellungen.ChatNachrichtenAktiv)
                {
                    ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                    string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";

                    // Passe 'chunkId' an den Namen der Variable an, die du in der Methode für die Chunk-Koordinaten nutzt
                    string feedbackMsg = $"[00FF00][{timeString}] Gebiet {chunkId} wurde dauerhaft gesäubert![-]";

                    GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                }

                // erzwingt Minimap Update, sofern Minimap Mod aktiv
                SimpleMinimap_Patch.ErzwingeRedraw = true;

                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
                }
            }
        }

        // Nur Server: Verarbeitet die taktischen Kills (Nachbar-Clear oder Gekitet) sauber an einem Ort
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

            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
            }

            if (istNachbar)
            {
                Log.Warning($"[EinmaligerSpawn] Taktischer Bonus: Nachbar {chunkId} zusätzlich gesichert!");

                // Chatnachricht im Einzelspieler und für den Host im Multiplayer
                if (!GameManager.IsDedicatedServer && ModEinstellungen.ChatNachrichtenAktiv)
                {
                    ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                    string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";

                    // Passe 'chunkId' an den Namen der Variable an, die du in der Methode für die Chunk-Koordinaten nutzt
                    string feedbackMsg = $"[00FF00][{timeString}] Taktischer Clear: Nachbar {chunkId} wurde zusätzlich gesichert![-]";

                    GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                }
            }
            else
            {
                Log.Warning($"[EinmaligerSpawn] Taktischer Clear! Todes-Chunk {chunkId} wurde gesichert.");

                // Chatnachricht im Einzelspieler und für den Host im Multiplayer
                if (!GameManager.IsDedicatedServer && ModEinstellungen.ChatNachrichtenAktiv)
                {
                    ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                    string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";

                    // Passe 'chunkId' an den Namen der Variable an, die du in der Methode für die Chunk-Koordinaten nutzt
                    string feedbackMsg = $"[00FF00][{timeString}] Taktischer Clear: Todes-Ort {chunkId} wurde gesäubert![-]";

                    GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                }
            }

            // erzwingt Minimap Update, sofern Minimap Mod aktiv
            SimpleMinimap_Patch.ErzwingeRedraw = true;

        }

        // Nur Server: Prüft, ob in diesem Chunk noch gespawnt werden darf
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

        // Nur Server: Lädt die Chunk-Datenbank aus der JSON-Datei
        public static void Load(string saveDir)
        {
            string path = Path.Combine(saveDir, "ausgerotteteChunks.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    ToteZombiesProChunk = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                    Log.Out($"[EinmaligerSpawn] {ToteZombiesProChunk.Count} Chunk-Daten erfolgreich geladen.");
                }
                catch (Exception e)
                {
                    Log.Error($"[EinmaligerSpawn] Fehler beim Laden der Chunks: {e.Message}");
                }
            }
            else
            {
                ToteZombiesProChunk.Clear();
            }
        }

        // Nur Server: Speichert die Chunk-Datenbank in einer JSON-Datei
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
                Log.Error($"[EinmaligerSpawn] Fehler beim Speichern der Chunks: {e.Message}");
            }
        }

        // Client: Berechnet den prozentualen Clear-Status in einem bestimmten Umkreis
        public static (int gesamt, int gesperrt, float prozent) BerechneLokalenFortschritt(int centerChunkX, int centerChunkZ, int radiusMeter)
        {
            int chunkSuchRadius = UnityEngine.Mathf.CeilToInt((float)radiusMeter / 16f);

            int gesamtChunks = 0;
            int gesperrteChunks = 0;

            for (int cx = centerChunkX - chunkSuchRadius; cx <= centerChunkX + chunkSuchRadius; cx++)
            {
                for (int cz = centerChunkZ - chunkSuchRadius; cz <= centerChunkZ + chunkSuchRadius; cz++)
                {
                    gesamtChunks++;
                    string chunkId = $"{cx}_{cz}";

                    if (ToteZombiesProChunk.TryGetValue(chunkId, out int kills) && kills > 0)
                    {
                        gesperrteChunks++;
                    }
                }
            }

            float prozent = gesamtChunks > 0 ? (float)Math.Round(((float)gesperrteChunks / gesamtChunks) * 100f, 1) : 0f;
            return (gesamtChunks, gesperrteChunks, prozent);
        }
    }
}