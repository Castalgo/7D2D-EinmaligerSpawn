using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EinmaligerSpawn.Benachrichtigungen;
using EinmaligerSpawn.JSONSpeichern;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.Network;

namespace EinmaligerSpawn.ChunkDatenbank
{
    public static class ChunkClearManager
    {
        // Türsteher-Prinzip: Beide Dictionaries sind nun strikt privat
        private static Dictionary<string, int> chunkClearLevel = new Dictionary<string, int>();
        private static Dictionary<int, string> ursprungsChunksLebenderZombies = new Dictionary<int, string>();

        // ==========================================
        // 1. HILFSMETHODEN
        // ==========================================

        public static string GetChunkId(Vector3i pos)
        {
            return $"{pos.x >> 4}_{pos.z >> 4}";
        }

        public static int GetChunkLevel(string chunkId)
        {
            if (chunkClearLevel.TryGetValue(chunkId, out int level))
            {
                return level;
            }
            return 0;
        }

        // ==========================================
        // 2. LIVE-TRACKING (LEBENDE ZOMBIES)
        // ==========================================

        public static void AddUrsprungsChunkLebenderZombie(int entityId, string chunkId)
        {
            ursprungsChunksLebenderZombies[entityId] = chunkId;
        }

        public static string GetUrsprungsChunkLebenderZombie(int entityId)
        {
            if (ursprungsChunksLebenderZombies.TryGetValue(entityId, out string chunkId))
            {
                return chunkId;
            }
            return null;
        }

        public static void RemoveUrsprungsChunkLebenderZombie(int entityId)
        {
            ursprungsChunksLebenderZombies.Remove(entityId);
        }

        // ==========================================
        // 3. CHUNK-STATUS & KILLS (AKTIONEN)
        // ==========================================

        // Ersetzt AddToterZombieNachID
        public static void AddRegulaerenKill(string chunkId)
        {
            if (!chunkClearLevel.ContainsKey(chunkId))
            {
                chunkClearLevel[chunkId] = 0;
            }

            chunkClearLevel[chunkId]++;
            int abriegelungsLimit = 1;

            if (chunkClearLevel[chunkId] == abriegelungsLimit)
            {
                Log.Warning($"[EinmaligerSpawn] ERFOLG! Chunk {chunkId} zählt jetzt als dauerhaft ausgerottet!");

                // Kapselung: Zentrale UI-Benachrichtigung
                NotificationManager.SendeChunkClear(chunkId);

                // Kapselung: Zentrales Map-Update für Minimap & Weltkarte
                KartenOverlay.RequestMapUpdate();

                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
                }
            }
        }

        // Ersetzt VerbucheTaktischenKill
        public static void AddTaktischenKill(string chunkId, bool istNachbar)
        {
            if (chunkClearLevel.ContainsKey(chunkId) && chunkClearLevel[chunkId] >= 1)
            {
                chunkClearLevel[chunkId]++;
                return;
            }

            chunkClearLevel[chunkId] = 1;

            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
            }

            string logMsg = istNachbar ? $"Nachbar {chunkId} zusätzlich gesichert!" : $"Todes-Chunk {chunkId} wurde gesichert.";
            Log.Warning($"[EinmaligerSpawn] Taktischer Bonus: {logMsg}");

            // Kapselung: Zentrale UI-Benachrichtigung
            NotificationManager.SendeTaktischenKill(chunkId, istNachbar);

            // Kapselung: Zentrales Map-Update für Minimap & Weltkarte
            KartenOverlay.RequestMapUpdate();
        }

        public static void VerarbeiteScannerBatch(List<string> chunkIds, int level)
        {
            foreach (string chunkId in chunkIds)
            {
                chunkClearLevel[chunkId] = level;

                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && level >= 1)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
                }
            }

            // Kapselung: Zentrales Map-Update für Minimap & Weltkarte
            KartenOverlay.RequestMapUpdate();
        }

        public static void VerarbeiteAdminBefehl(List<string> chunkIds, bool isReset)
        {
            bool datenGeaendert = false;

            foreach (string chunkId in chunkIds)
            {
                if (isReset)
                {
                    if (chunkClearLevel.ContainsKey(chunkId))
                    {
                        chunkClearLevel.Remove(chunkId);
                        datenGeaendert = true;
                    }
                }
                else
                {
                    if (!chunkClearLevel.ContainsKey(chunkId))
                    {
                        chunkClearLevel[chunkId] = 0;
                    }
                    chunkClearLevel[chunkId]++;
                    datenGeaendert = true;

                    if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                    {
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
                    }
                }
            }

            if (datenGeaendert)
            {
                // Kapselung: Zentrales Map-Update für Minimap & Weltkarte
                KartenOverlay.RequestMapUpdate();

                if (isReset)
                {
                    //// "ESa Cheat_Clear Reset" ist ein reiner Debugging-Befehl, der im Regelbetrieb nicht nötig sein sollte. Daher ist es in Ordnung, dass wir hier nur eine Warnung ausgeben, statt zu synchronisieren.
                    int spielerAnzahl = GameManager.Instance.World.Players.list.Count;
                    bool hatExterneClients = GameManager.IsDedicatedServer ? spielerAnzahl > 0 : spielerAnzahl > 1;

                    if (hatExterneClients)
                    {
                        string warnMsg = "[FFFF00]Warnung: Ein Chunk-Reset wurde durchgeführt! Eure lokalen Karten-Daten sind nun asynchron. Bitte verbindet euch einmal neu, um das Problem zu beheben.[-]";
                        // Kapselung: Zentrale UI-Benachrichtigung
                        NotificationManager.SendeGlobaleNachricht(warnMsg);
                    }
                }
            }
        }

        public static int GetAnzahlLebenderZombies()
        {
            return ursprungsChunksLebenderZombies.Count;
        }

        public static IEnumerable<int> GetAlleLebendenZombieIDs()
        {
            foreach (int id in ursprungsChunksLebenderZombies.Keys)
            {
                yield return id;
            }
        }

        // ==========================================
        // 4. LESE-ZUGRIFFE & AUSWERTUNG
        // ==========================================

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

                    if (GetChunkLevel(chunkId) > 0)
                    {
                        gesperrteChunks++;
                    }
                }
            }

            float prozent = gesamtChunks > 0 ? (float)Math.Round(((float)gesperrteChunks / gesamtChunks) * 100f, 1) : 0f;
            return (gesamtChunks, gesperrteChunks, prozent);
        }

        public static bool HatChunkEintrag(string chunkId) { return chunkClearLevel.ContainsKey(chunkId); }

        public static bool IstChunkAktivBelegt(string chunkId) { return ursprungsChunksLebenderZombies.ContainsValue(chunkId); }

        public static int GetDatenbankGroesse() { return chunkClearLevel.Count; }

        public static IEnumerable<string> GetAlleGesperrtenChunks()
        {
            foreach (var kvp in chunkClearLevel)
            {
                if (kvp.Value >= 1)
                {
                    yield return kvp.Key;
                }
            }
        }

        // ==========================================
        // 5. SYSTEM (SPEICHERN & LADEN)
        // ==========================================

        public static void Load(string saveDir)
        {
            string path = Path.Combine(saveDir, "ausgerotteteChunks.json");

            if (SicheresSpeichern.TryLoad(path, out Dictionary<string, int> geladeneDaten))
            {
                chunkClearLevel = geladeneDaten;
                Log.Out($"[EinmaligerSpawn] {chunkClearLevel.Count} Chunk-Daten erfolgreich geladen.");
            }
            else
            {
                chunkClearLevel.Clear();
                Log.Out("[EinmaligerSpawn] Beginne mit leerer Chunk-Datenbank.");
            }
        }

        public static void Save(string saveDir)
        {
            string path = Path.Combine(saveDir, "ausgerotteteChunks.json");

            // Sortierung beibehalten
            var sortedChunks = chunkClearLevel
                .OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            SicheresSpeichern.Save(path, sortedChunks);
        }

        public static int Debug_EntferneNullEintraege()
        {
            var zuLoeschendeKeys = chunkClearLevel
                .Where(kvp => kvp.Value == 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in zuLoeschendeKeys)
            {
                chunkClearLevel.Remove(key);
            }

            return zuLoeschendeKeys.Count;
        }

        public static bool EntferneNullEintrag(string chunkId)
        {
            if (chunkClearLevel.TryGetValue(chunkId, out int level) && level == 0)
            { chunkClearLevel.Remove(chunkId); return true; }
            return false;
        }

        public static void Reset()
        {
            chunkClearLevel.Clear();
            ursprungsChunksLebenderZombies.Clear();
        }
    }
}