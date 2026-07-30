using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.Network;

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
                Log.Warning($"[EinmaligerSpawn] ERFOLG! Chunk {chunkId} zählt jetzt als dauerhaft ausgerottet!");

                // Holt die aktuelle In-Game-Zeit (z.B. Tag 4, 14:35)
                ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
                string feedbackMsg = $"[00FF00][{timeString}] Chunk {chunkId} zählt jetzt als dauerhaft ausgerottet.[-]";
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);

                if (KartenOverlay.IstAktiv)
                {
                    KartenOverlay.ErzwingeRedraw();
                }

                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(new NetPackageChunkSync(chunkId));
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

            // Live-Update für die Karte auslösen
            if (KartenOverlay.IstAktiv)
            {
                KartenOverlay.ErzwingeRedraw();
            }

            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(new NetPackageChunkSync(chunkId));
            }

            // Chatnachricht und Konsole vorbereiten
            ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
            string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
            string feedbackMsg;

            if (istNachbar)
            {
                Log.Warning($"[EinmaligerSpawn] Taktischer Bonus: Nachbar {chunkId} zusätzlich gesichert!");
                feedbackMsg = $"[00FF00][{timeString}] Flächensäuberungsbonus: Angrenzendes Gebiet {chunkId} clear.[-]";
            }
            else
            {
                Log.Warning($"[EinmaligerSpawn] Taktischer Clear! Todes-Chunk {chunkId} wurde gesichert.");
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

        // Berechnet den prozentualen Clear-Status in einem bestimmten Umkreis
        public static (int gesperrt, int gesamt, int prozent) BerechneLokalenFortschritt(EntityPlayer player, int radiusMeter = 120)
        {
            Vector3i playerPos = player.GetBlockPosition();
            int px = playerPos.x;
            int pz = playerPos.z;

            int playerChunkX = px >> 4;
            int playerChunkZ = pz >> 4;

            int chunkSuchRadius = Mathf.CeilToInt((float)radiusMeter / 16f);
            int maxDistSq = radiusMeter * radiusMeter;

            int x_Gesperrt = 0;
            int y_Gesamt = 0;

            for (int cx = playerChunkX - chunkSuchRadius; cx <= playerChunkX + chunkSuchRadius; cx++)
            {
                for (int cz = playerChunkZ - chunkSuchRadius; cz <= playerChunkZ + chunkSuchRadius; cz++)
                {
                    int minX = cx * 16;
                    int maxX = minX + 15;
                    int minZ = cz * 16;
                    int maxZ = minZ + 15;

                    int dx = Math.Max(0, Math.Max(minX - px, px - maxX));
                    int dz = Math.Max(0, Math.Max(minZ - pz, pz - maxZ));

                    if (dx * dx + dz * dz <= maxDistSq)
                    {
                        y_Gesamt++;
                        string chunkId = $"{cx}_{cz}";

                        if (ToteZombiesProChunk.ContainsKey(chunkId) && ToteZombiesProChunk[chunkId] >= 1)
                        {
                            x_Gesperrt++;
                        }
                    }
                }
            }

            float prozentFloat = y_Gesamt > 0 ? ((float)x_Gesperrt / y_Gesamt) * 100f : 0f;
            int prozent = Mathf.RoundToInt(prozentFloat);

            return (x_Gesperrt, y_Gesamt, prozent);
        }
    }
}