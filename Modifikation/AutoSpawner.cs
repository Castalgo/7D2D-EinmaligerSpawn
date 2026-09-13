using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using UnityEngine;

namespace EinmaligerSpawn.ZombieSpawner
{
    public static class AutoSpawner
    {
        private static float timeSinceLastCheck = 0f;
        private static Dictionary<int, float> playerSpawnTimers = new Dictionary<int, float>();
        private static Dictionary<int, bool> playerProtectionLost = new Dictionary<int, bool>();
        private static Dictionary<int, string> letzterFehlschlagLog = new Dictionary<int, string>();
        private static readonly int[] ScanRingPrioritaeten = { 2, 3, 4, 5, 1, 0 };

        public static void OnGameUpdate()
        {
            // Sicherheitsabfrage: Wenn das Spiel noch nicht vollständig initialisiert ist, wird der Spawn-Check übersprungen.
            if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.Players == null)
                return;

            // Server-only: Client kicken
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                return;

            // Keine Spawns (und kein GC) während eines Blutmondes, weil die Spawnrate bereits erhöht ist und Blutmondzombies sich anders verhalten
            if (SkyManager.IsBloodMoonVisible())
                return;

            // Zeitintervall prüfen
            timeSinceLastCheck += Time.deltaTime;
            if (timeSinceLastCheck < ModEinstellungen.SpawnCheckIntervall)
                return;

            float passedTime = timeSinceLastCheck;
            timeSinceLastCheck = 0f;

            int currentZombies = 0;
            foreach (Entity entity in GameManager.Instance.World.Entities.list) // Zählt alle aktiven Enemies
            {
                if ((entity is EntityEnemy || entity is EntityZombie) && !entity.IsDead())
                {
                    currentZombies++;
                }
            }

            // Despawnte Zombies aus dem Gedächtnis löschen
            BiomSpawnBlocker.ZombieGarbageCollector.BereinigeGeisterZombies();

            // =====================================================================
            // DOKUMENTATION: GEWOLLTES ÜBERSCHREITEN DES GLOBALEN ZOMBIE-LIMITS
            // =====================================================================
            // WICHTIG: Damit alle Spieler immer Zombies vor sich haben und nicht
            // nur Spieler mit einem alphabetisch vorderen Namen, bekommen entweder
            // alle Spieler 1 Zombie oder es bekommt niemanden einen. Das Serverlimit
            // wird nicht erreicht, weil unser Methodenlimit weit darunter ist.
            // =====================================================================
            if (currentZombies >= ModEinstellungen.GlobalesZombieLimit)
                return;

            // Spawn-Check für jeden Spieler durchführen
            List<EntityPlayer> players = GameManager.Instance.World.Players.list;
            foreach (EntityPlayer player in players)
            {
                // Kapselung: Die Hilfsmethode übernimmt alle Checks (Timer, Quest, Tot, Level)
                if (SollSpielerZombieErhalten(player, passedTime))
                {
                    FuehreSpawnAus(player, currentZombies);
                }
            }
        }

        // =====================================================================
        // ZENTRALE HILFSMETHODEN
        // =====================================================================
        // Vanilla-like: Dart-Maschine für 50 Random-Punkte. Mod zustzlich: plus 4 Ecken. 
        // Generiert Koordinaten on-the-fly (ohne RAM-Zuweisung) und reicht sie an den Aufrufer durch.
        public static IEnumerable<Vector2i> Generiere54Darts(GameRandom rand)
        {
            for (int i = 0; i < 54; i++)
            {
                int localX;
                int localZ;

                if (i < 50)
                {
                    localX = rand.RandomRange(0, 16);
                    localZ = rand.RandomRange(0, 16);
                }
                else
                {
                    if (i == 50) { localX = 0; localZ = 0; }
                    else if (i == 51) { localX = 0; localZ = 15; }
                    else if (i == 52) { localX = 15; localZ = 0; }
                    else { localX = 15; localZ = 15; }
                }

                yield return new Vector2i(localX, localZ);
            }
        }

        // Engine-Abfragen, ob ein spezifischer Block physisch bebaubar/begehbar ist
        public static bool IstPhysischePositionSpawntauglich(Chunk physChunk, int localX, int localZ, int worldX, int worldZ, out int y)
        {
            y = (int)(physChunk.GetHeight(localX, localZ) + 1);

            if (physChunk.IsWater(localX, y - 1, localZ)) return false; // Überspringe, wenn im Wasser
            if (!physChunk.CanMobsSpawnAtPos(localX, y, localZ, false, true)) return false; // Überspringe, wenn der Block nicht für Mobs geeignet ist

            Vector3 worldPos = new Vector3(worldX, (float)y, worldZ);
            PrefabInstance prefab = GameManager.Instance.World.GetPOIAtPosition(worldPos, null, null);
            if (prefab != null) return false; // Überspringe, wenn in einem POI

            return true;
        }

        // =====================================================================


        // Extra-Zombies für Spieler spawnen? Ja/Nein/Vielleicht (Timer, Quests, Anfänger-Buff).
        // Gibt true zurück, wenn alle Bedingungen für einen Spawn-Versuch erfüllt sind.
        private static bool SollSpielerZombieErhalten(EntityPlayer player, float passedTime)
        {
            int pid = player.entityId;

            // Initialisierung des Timers für den Spieler, falls noch nicht vorhanden.
            if (!playerSpawnTimers.ContainsKey(pid))
            {
                playerSpawnTimers[pid] = 0f;
                return false; // Spieler bekommt einmalig keinen Zombie, falls er noch in die Welt lädt (Spawnschutz)
            }

            // Keine Spawns für tote Spieler. Zusätzlich Timer zurücksetzen.
            if (player.IsDead())
            {
                playerSpawnTimers[pid] = 0f;
                return false;
            }

            // Abfrage, wie viel Zeit seit dem letzten Spawn-Check vergangen ist
            playerSpawnTimers[pid] += passedTime;

            // Abrufen der Questinformationen des Spielers, um zu prüfen, ob er sich in einem Questgebiet befindet.
            if (player.QuestJournal != null && player.QuestJournal.quests != null)
            {
                for (int q = 0; q < player.QuestJournal.quests.Count; q++) // für alle Quests des Spielers prüfen
                {
                    Quest quest = player.QuestJournal.quests[q];
                    if (quest != null && quest.RallyMarkerActivated)
                    {
                        Rect questBounds = quest.GetLocationRect(); // Ort des POI
                        if (questBounds != Rect.zero && questBounds.Contains(new Vector2(player.position.x, player.position.z)))
                        {
                            // Spieler ist in einer Quest. Timer wird zurückgesetzt.
                            playerSpawnTimers[pid] = 0f;
                            return false;
                        }
                    }
                }
            }

            // Initialisierung des Anfänger-Schutzes, falls noch nicht vorhanden.
            if (!playerProtectionLost.ContainsKey(pid))
                playerProtectionLost[pid] = false;

            float drosselungsFaktor = 1f;

            // Newbie-Schutz aus Sandboxeinstellungen berücksichtigen
            if (!playerProtectionLost[pid])
            {
                int safeZoneLevel = GamePrefs.GetInt(EnumGamePrefs.PlayerSafeZoneLevel);
                int safeZoneHours = GamePrefs.GetInt(EnumGamePrefs.PlayerSafeZoneHours);
                float tagesLaengeEchtzeit = GamePrefs.GetInt(EnumGamePrefs.DayNightLength);
                float echteMinutenProGameStunde = tagesLaengeEchtzeit / 24f;
                float schutzZeitInEchtenMinuten = safeZoneHours * echteMinutenProGameStunde;

                bool levelVerbraucht = player.Progression.Level > safeZoneLevel;
                bool zeitVerbraucht = player.totalTimePlayed > schutzZeitInEchtenMinuten;

                if (levelVerbraucht && zeitVerbraucht)
                {
                    drosselungsFaktor = 1f;
                    playerProtectionLost[pid] = true;
                }
                else if (player.Progression.Level == 1)
                {
                    drosselungsFaktor = 100f;
                }
                else if (levelVerbraucht || zeitVerbraucht)
                {
                    drosselungsFaktor = 15f;
                }
                else
                {
                    drosselungsFaktor = 30f;
                }
            }

            float requiredInterval = ModEinstellungen.SpawnCheckIntervall * drosselungsFaktor;

            // Wenn die erforderliche Zeit seit dem letzten Spawn-Check erreicht ist, wird ein Spawn-Versuch unternommen.
            if (playerSpawnTimers[pid] >= requiredInterval)
            {
                playerSpawnTimers[pid] = 0f;
                return true;
            }

            return false; // Zeit noch nicht abgelaufen
        }

        // Versucht, einen Zombie in der Nähe des Spielers zu spawnen, unter Berücksichtigung der globalen Zombieanzahl und der Chunk-Bedingungen.
        private static void FuehreSpawnAus(EntityPlayer player, int currentZombies)
        {
            Vector3i playerPos = player.GetBlockPosition();
            int playerChunkX = playerPos.x >> 4;
            int playerChunkZ = playerPos.z >> 4;

            string logPrefix = $"[ES AutoSpawner] Globale Zombies ({currentZombies}/{ModEinstellungen.GlobalesZombieLimit}).";

            GameRandom rand = GameManager.Instance.World.GetGameRandom();
            bool irgeneinChunkGeladen = false;

            // Ermittelt alle Mitspieler, um sicherzustellen, dass Zombies nicht zu nah an ihnen spawnen.
            List<EntityPlayer> andereSpieler = new List<EntityPlayer>();
            foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
            {
                if (p.entityId != player.entityId && !p.IsDead())
                {
                    andereSpieler.Add(p);
                }
            }

            // Die Ringe (Abstände) werden in der Reihenfolge der Priorität gescannt, um zuerst die am weitesten entfernten Chunks zu prüfen (wie in Vanilla).
            foreach (int radius in ScanRingPrioritaeten)
            {
                List<Vector3i> ringChunks = new List<Vector3i>();

                for (int x = -radius; x <= radius; x++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(z) != radius)
                            continue;
                        ringChunks.Add(new Vector3i(playerChunkX + x, 0, playerChunkZ + z));
                    }
                }

                // Mische die Chunks innerhalb des Rings, um eine zufällige Spawnreihenfolge zu gewährleisten.
                for (int i = 0; i < ringChunks.Count; i++)
                {
                    int rndIndex = rand.RandomRange(i, ringChunks.Count);
                    Vector3i temp = ringChunks[i];
                    ringChunks[i] = ringChunks[rndIndex];
                    ringChunks[rndIndex] = temp;
                }

                // Prüfe jeden Chunk im Ring, ob er für einen Zombie-Spawn geeignet ist.
                foreach (Vector3i target in ringChunks)
                {
                    int targetCx = target.x;
                    int targetCz = target.z;
                    string chunkId = $"{targetCx}_{targetCz}";

                    // Wenn der Ziel-Chunk bereits als unspawnbar markiert ist, überspringe ihn.
                    if (ChunkClearManager.GetChunkLevel(chunkId) >= 1)
                        continue;

                    // Wenn der Ziel-Chunk bereits aktiv von einem anderen Zombie belegt ist, überspringe ihn.
                    if (ChunkClearManager.IstChunkAktivBelegt(chunkId))
                        continue;


                    // Prüfe, ob der Ziel-Chunk geladen ist. Wenn nicht, überspringe ihn.
                    Chunk logischerChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(targetCx, targetCz);
                    if (logischerChunk == null) continue; // nicht geladene Chunks überspringen

                    irgeneinChunkGeladen = true;

                    byte biomeId = logischerChunk.GetBiomeId(8, 8);
                    BiomeDefinition biome = GameManager.Instance.World.Biomes.GetBiome(biomeId);
                    BiomeSpawnEntityGroupList groupList = null;

                    // Prüfe, aus welchem Biom du die Zombie-Spawn-Gruppe nutzen musst
                    if (biome != null && BiomeSpawningClass.list.ContainsKey(biome.m_sBiomeName))
                    {
                        groupList = BiomeSpawningClass.list[biome.m_sBiomeName];
                    }

                    // Ruft die Zombie-Klasse aus der Biome-Spawn-Gruppe ab. Wenn keine Gruppe gefunden wird, wird ein Fallback-Zombie verwendet.
                    int zombieClassID = EntityClass.FromString("zombieArlene"); // Fallback, falls keine Gruppe gefunden wird
                    if (groupList != null)
                    {
                        foreach (BiomeSpawnEntityGroupData groupData in groupList.list) // Iteriere über alle Zombies in der Liste des Bioms
                        {
                            if (EntityGroups.IsEnemyGroup(groupData.entityGroupName))
                            {
                                int lastClassId = 0;
                                int rolledId = EntityGroups.GetRandomFromGroup(groupData.entityGroupName, ref lastClassId, null);
                                if (rolledId != 0)
                                {
                                    zombieClassID = rolledId;
                                    break;
                                }
                            }
                        }
                    }

                    // Spawn-Position innerhalb des Ziel-Chunks finden
                    bool spawnFound = VersucheSpawnPositionZuFinden(
                        targetCx, targetCz, player, andereSpieler, rand,
                        out Vector3 spawnPos, out bool zielVerschoben
                    );

                    // Wenn eine gültige Spawn-Position gefunden wurde, wird der Zombie erzeugt und in die Welt gespawnt.
                    if (spawnFound)
                    {
                        Entity zombie = EntityFactory.CreateEntity(zombieClassID, spawnPos, Vector3.zero);
                        if (zombie != null)
                        {
                            GameManager.Instance.World.SpawnEntityInWorld(zombie);
                            ChunkClearManager.AddUrsprungsChunkLebenderZombie(zombie.entityId, chunkId);

                            // Direkt loggen und abbrechen
                            Log.Out($"{logPrefix} 1 Zombie wurde bei {targetCx},{targetCz} für '{player.EntityName}' gespawnt.");
                            letzterFehlschlagLog.Remove(player.entityId);
                            return;
                        }
                    }
                    else
                    {
                        // ==========================================
                        // Chunk nur als unspawnbar (1) markieren, wenn alle Versuche 
                        // in SEINEM URSPRÜNGLICHEN GEBIET fehlschlugen.
                        // ==========================================
                        if (!zielVerschoben)
                        {
                            ChunkClearManager.VerarbeiteScannerBatch(new List<string> { chunkId }, 1);
                        }
                    }
                }
            }

            if (!irgeneinChunkGeladen)
            {
                string aktuelleFehlermeldung = $"{logPrefix} Konnte keinen Zombie für '{player.EntityName}' erzeugen, weil keine Chunks infrage kommen.";

                if (!letzterFehlschlagLog.ContainsKey(player.entityId) || letzterFehlschlagLog[player.entityId] != aktuelleFehlermeldung)
                {
                    Log.Out(aktuelleFehlermeldung);
                    letzterFehlschlagLog[player.entityId] = aktuelleFehlermeldung;
                }
            }
            else
            {
                Log.Out($"{logPrefix} Konnte keinen Zombie für '{player.EntityName}' erzeugen, weil kein gültiger Spawnchunk gefunden wurde.");
            }
        }

        // Sucht aktiv nach einer passenden Block-Koordinate innerhalb des Ziel-Chunks
        private static bool VersucheSpawnPositionZuFinden(
            int targetCx, int targetCz, EntityPlayer player, List<EntityPlayer> andereSpieler, GameRandom rand,
            out Vector3 spawnPos, out bool zielVerschoben)
        {
            spawnPos = Vector3.zero;
            zielVerschoben = false;

            int minX = targetCx * 16;
            int minZ = targetCz * 16;
            Vector3i playerPos = player.GetBlockPosition();

            // Versuche, eine gültige Spawnposition innerhalb des Chunks zu finden: 50 + 4 Versuche (Vanilla + Ecken)
            foreach (Vector2i dart in Generiere54Darts(rand))
            {
                int localX = dart.x;
                int localZ = dart.y;

                int worldX = minX + localX;
                int worldZ = minZ + localZ;

                Vector2 flatPlayer = new Vector2(playerPos.x, playerPos.z);
                Vector2 flatTarget = new Vector2(worldX, worldZ);
                float flatDist = Vector2.Distance(flatPlayer, flatTarget);

                // =====================================================================
                // DOKUMENTATION: GEWOLLTES ÜBERTRITT-VERHALTEN IN GECLEARTE CHUNKS
                // =====================================================================
                // Wenn ein Zombie wegen der 28-Meter-Bannmeile in einen benachbarten 
                // Chunk verschoben werden muss, prüfen wir absichtlich NICHT, ob 
                // dieser Ziel-Chunk bereits gesäubert wurde! 
                // 
                // Grund (Anti-Softlock): Wenn der Spieler exakt an der Grenze eines
                // ungesäuberten Chunks steht und alle Nachbarchunks bereits gecleart
                // sind, könnte der ungesäuberte Chunk niemals seinen Zombie spawnen.
                // Um diesen Softlock zu verhindern, tolerieren wir den "Übertritt" in
                // sauberes Gebiet. (Die Vanilla-Engine verhält sich hierbei identisch).
                // =====================================================================

                if (flatDist < 28f)
                {
                    Vector2 dir = (flatTarget - flatPlayer).normalized;
                    if (dir == Vector2.zero)
                        dir = new Vector2(rand.RandomFloat - 0.5f, rand.RandomFloat - 0.5f).normalized;

                    flatTarget = flatPlayer + dir * 29f;
                    worldX = Mathf.RoundToInt(flatTarget.x);
                    worldZ = Mathf.RoundToInt(flatTarget.y);

                    zielVerschoben = true;
                }

                int physCx = worldX >> 4;
                int physCz = worldZ >> 4;
                Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(physCx, physCz);

                if (physChunk == null)
                    continue;

                int physLocalX = worldX - (physCx * 16);
                int physLocalZ = worldZ - (physCz * 16);

                // Physische Eignung des Blocks über die zentrale Methode prüfen
                if (!IstPhysischePositionSpawntauglich(physChunk, physLocalX, physLocalZ, worldX, worldZ, out int y))
                    continue;

                Vector3 checkPosVec = new Vector3(worldX, (float)y, worldZ);

                if (Vector3.Distance(checkPosVec, player.position) < 28f) continue;

                bool zuNahAnAnderemSpieler = false;
                foreach (EntityPlayer p in andereSpieler)
                {
                    if (Vector3.Distance(checkPosVec, p.position) < 28f)
                    {
                        zuNahAnAnderemSpieler = true;
                        break;
                    }
                }
                if (zuNahAnAnderemSpieler)
                {
                    zielVerschoben = true; // muss auf true gesetzt werden, damit der Chunk nicht als unspawnbar markiert wird, falls alle Versuche fehlschlagen
                    continue; // Überspringe, wenn zu nah an einem anderen Spieler
                }

                spawnPos = new Vector3(worldX + 0.5f, (float)y, worldZ + 0.5f);
                return true;
            }

            return false; // Keine gültige Spawnposition gefunden
        }

        // Setzt alle internen Timer und Caches zurück, z.B. beim Welt-Exit oder Server-Neustart.
        public static void Reset()
        {
            timeSinceLastCheck = 0f;

            if (playerSpawnTimers != null) playerSpawnTimers.Clear();
            if (playerProtectionLost != null) playerProtectionLost.Clear();
            if (letzterFehlschlagLog != null) letzterFehlschlagLog.Clear();

            Log.Out("[ES AutoSpawner] Interner Cache und Timer wurden erfolgreich für die neue Sitzung geleert.");
        }
    }

    // Server only: Komplettscan der Welt, um unspawnbare Chunks im Vorfeld zu identifizieren und zu markieren.
    public static class GlobalMapScanner
    {
        // =========================================================================================
        // ARCHITEKTUR-NOTIZ: WARUM SPIELER-KILLS UND UNSPAWNBARES TERRAIN BEIDE DEN WERT 1 HABEN
        // =========================================================================================
        // Aktuell wird ein Chunk auf Level 1 gesetzt, wenn der Spieler ihn säubert (KillTracker) 
        // ODER wenn der MapScanner feststellt, dass dort wegen Wasser/Bergen ohnehin nichts spawnen 
        // kann (GlobalMapScanner).
        //
        // Warum wir das nicht trennen (z. B. Level 2 für Ozeane):
        // 1. Performance & Netzwerk: Ein simpler integer Zustand (0 = unsicher, >=1 = sicher) hält 
        //    die Logik im AutoSpawner rasend schnell und die NetPackages winzig. Zudem ist sie für
        //    unsere aktuellen Zwecke zu 100% ausreichend.
        // 2. UI-Logik: Dem Spieler ist es egal, ob ein Chunk sicher ist, weil er ihn gesäubert hat, 
        //    oder weil dort ohnehin nur Wasser ist. Die Minimap färbt beides als "Sichere Zone".
        // 3. Fortschritt: Unspawnbares Terrain treibt den %-Wert der Welteroberung künstlich nach 
        //    oben. Das ist ein akzeptierter Trade-off, da eine exakte Trennung tiefe Eingriffe in 
        //    das UI und die Savegame-Struktur erfordern würde. 
        // 4. Zukunft: Wenn wir später Belohnungnen Aufgrund von Killzahlen in einem Chunk vergeben
        //    wollen, dann können wir das Konzept immernch beliebig ändern. Aktuell ist es aber unnötig.
        // 
        // Sollte die Fortschrittsanzeige in Zukunft zu ungenau werden, muss hier angesetzt werden 
        // (Trennung in enum: Offen, Gesaeubert, Unspawnbar).
        // =========================================================================================
        private static Coroutine laufendeCoroutine = null;

        public static void StarteGlobalenScan()
        {
            if (ModEinstellungen.GlobalScanAbgeschlossen)
            {
                Log.Out("[ES MapScanner] Welt ist bereits komplett gescannt. Scanner bleibt deaktiviert.");
                return;
            }

            Log.Out("[ES MapScanner] Starte initialen World-Scan im Hintergrund...");
            laufendeCoroutine = ThreadManager.StartCoroutine(DoGlobalScanCoroutine());
        }

        public static void StoppeGlobalenScan()
        {
            if (laufendeCoroutine != null)
            {
                ThreadManager.StopCoroutine(laufendeCoroutine);
                laufendeCoroutine = null;
                Log.Out("[ES MapScanner] Scan wurde durch Welt-Exit hart gestoppt.");
            }
        }

        // Coroutine, die den globalen Scan der Welt durchführt, um unspawnbare Chunks zu identifizieren.
        private static System.Collections.IEnumerator DoGlobalScanCoroutine()
        {
            yield return new WaitForSeconds(10f); // 10 Sekunden Verzögerung, um Server laden zu lassen

            // Abrufen der Chunk- und Biome-Provider, um auf die Weltinformationen zuzugreifen
            IChunkProvider chunkProvider = GameManager.Instance.World.ChunkCache.ChunkProvider;
            IBiomeProvider biomeProvider = chunkProvider.GetBiomeProvider();

            // Abrufen der Weltgröße, um die Grenzen für den Scan festzulegen
            GameUtils.WorldInfo worldInfo = ((ChunkProviderAbstract)chunkProvider).WorldInfo;

            int weltGroesseX = worldInfo.WorldSize.x;
            int weltGroesseZ = worldInfo.WorldSize.y;

            int halfSizeX = weltGroesseX / 2;
            int halfSizeZ = weltGroesseZ / 2;

            int minChunkX = -halfSizeX / 16;
            int minChunkZ = -halfSizeZ / 16;
            int maxChunkX = (halfSizeX / 16) - 1;
            int maxChunkZ = (halfSizeZ / 16) - 1;

            List<PrefabInstance> overlappingPOIs = new List<PrefabInstance>();

            Log.Out("[ES MapScanner] PHASE 1: Starte mathematischen Vorab-Scan über die gesamte Karte...");
            int mathChunksProcessed = 0;

            // Iteriere über alle Chunks in der Welt und prüfe sie mathematisch auf Spawnbarkeit
            for (int mathX = minChunkX; mathX <= maxChunkX; mathX++)
            {
                for (int mathZ = minChunkZ; mathZ <= maxChunkZ; mathZ++)
                {
                    string chunkId = $"{mathX}_{mathZ}";

                    // wurde der Chunk bereits gescannt?
                    if (ChunkClearManager.HatChunkEintrag(chunkId))
                    {
                        continue;
                    }

                    // Prüfe mathematisch, ob der Chunk unspawnbar ist (z.B. Ozean, POI-Cluster)
                    if (!GlobalMapScanner.PruefeChunkMathematisch(mathX, mathZ, biomeProvider, overlappingPOIs))
                    {
                        ChunkClearManager.VerarbeiteScannerBatch(new List<string> { chunkId }, 1);
                    }

                    mathChunksProcessed++;

                    if (mathChunksProcessed >= 1000)
                    {
                        mathChunksProcessed = 0;
                        yield return null; // Coroutine-Pause
                    }
                }
            }
            Log.Out("[ES MapScanner] PHASE 1 ABGESCHLOSSEN! Alle unspawnbaren Ozeane und POI-Cluster wurden aussortiert.");

            Log.Out("[ES MapScanner] PHASE 2: Starte Deep-Scan für verbleibende Chunks...");

            int cx = minChunkX;
            int cz = minChunkZ;

            int chunksSinceLastSave = 0;
            List<Vector2i> chunkBatch = new List<Vector2i>();

            // Iteriere über alle Chunks in der Welt und prüfe sie physisch auf Spawnbarkeit
            while (cx <= maxChunkX)
            {
                while (cz <= maxChunkZ)
                {
                    string chunkId = $"{cx}_{cz}";

                    // Wenn der Chunk bereits in der Datenbank ist, überspringe ihn
                    if (!ChunkClearManager.HatChunkEintrag(chunkId))
                    {
                        chunkBatch.Add(new Vector2i(cx, cz));
                    }

                    // Wenn wir 20 Chunks gesammelt haben oder am Ende der Welt angekommen sind, verarbeite die Batch
                    if (chunkBatch.Count >= 20 || (cx == maxChunkX && cz == maxChunkZ && chunkBatch.Count > 0))
                    {
                        while (GameManager.Instance.World.ChunkCache.Count() >= 5000)
                        {
                            int ac = GameManager.Instance.World.ChunkCache.Count();
                            Log.Out($"[ES MapScanner] RAM-Schutz aktiv ({ac} geladene Chunks). Pausiere für 5 Sekunden...");
                            yield return new WaitForSeconds(5f);
                        }

                        // Lade alle Chunks in der Batch, falls sie noch nicht geladen sind
                        foreach (Vector2i pos in chunkBatch)
                        {
                            if (GameManager.Instance.World.ChunkCache.GetChunkSync(pos.x, pos.y) == null)
                            {
                                HashSetList<long> pendingChunks = chunkProvider.GetRequestedChunks();
                                if (pendingChunks != null)
                                {
                                    // Sicherheitsstopp, falls der Stack volläuft, weil das Abarbeiten hängt
                                    while (pendingChunks.list.Count > 100)
                                    {
                                        yield return new WaitForSeconds(0.1f);
                                    }
                                }
                                chunkProvider.RequestChunk(pos.x, pos.y);
                            }
                        }

                        // Warte, bis alle Chunks in der Batch geladen sind, oder breche nach 5 Sekunden ab
                        foreach (Vector2i pos in chunkBatch)
                        {
                            float timeout = 0f;
                            while (GameManager.Instance.World.ChunkCache.GetChunkSync(pos.x, pos.y) == null)
                            {
                                timeout += Time.deltaTime;
                                if (timeout > 5f) break;
                                yield return null;
                            }
                        }

                        // Prüfe jeden Chunk in der Batch physisch und speichere das Ergebnis in der Datenbank
                        foreach (Vector2i pos in chunkBatch)
                        {
                            Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(pos.x, pos.y);

                            // Wenn der Chunk geladen ist, prüfe ihn und speichere das Ergebnis
                            if (physChunk != null)
                            {
                                PruefeUndSpeichereChunk(pos.x, pos.y); // eigentliches Prüfen und Speichern des Chunks
                                chunksSinceLastSave++;

                                // Chunk wieder entladen, sofern keine Spieler in der Nähe sind
                                bool isNearPlayer = false;
                                foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
                                {
                                    int px = p.GetBlockPosition().x >> 4;
                                    int pz = p.GetBlockPosition().z >> 4;

                                    if (Mathf.Abs(px - pos.x) <= 8 && Mathf.Abs(pz - pos.y) <= 8)
                                    {
                                        isNearPlayer = true;
                                        break;
                                    }
                                }

                                if (!isNearPlayer)
                                {
                                    GameManager.Instance.World.m_ChunkManager.RemoveChunk(physChunk.Key);
                                }
                            }
                            else
                            {
                                chunksSinceLastSave++;
                            }
                        }

                        chunkBatch.Clear(); // Batch zurücksetzen

                        if (chunksSinceLastSave >= 500)
                        {
                            // Chunk-Datenbank sofort sichern, dann den Status
                            string saveDir = GameIO.GetSaveGameDir();
                            if (!string.IsNullOrEmpty(saveDir))
                            {
                                ChunkClearManager.Save(saveDir);
                            }

                            ModEinstellungen.Speichern();

                            chunksSinceLastSave = 0;
                            Log.Out($"[ES MapScanner] Deep-Scan: Zwischenstand bei Chunk {cx},{cz} abgeschlossen.");
                        }

                        yield return null;
                    }

                    cz++;
                }
                cz = minChunkZ;
                cx++;
            }

            // Am Ende des Scans: Prüfe, ob die Anzahl der erfassten Chunks mit der erwarteten Anzahl übereinstimmt
            int erwarteteChunks = (weltGroesseX / 16) * (weltGroesseZ / 16);
            int erfassteChunks = ChunkClearManager.GetDatenbankGroesse();

            if (erfassteChunks < erwarteteChunks)
            {
                int fehlendeChunks = erwarteteChunks - erfassteChunks;

                Log.Warning($"[ES MapScanner] Map-Scan unvollständig! Es fehlen {fehlendeChunks} Chunks (Erfasst: {erfassteChunks} / {erwarteteChunks}). Pausiere Scan bis zum Game-Neustart.");
                yield break;
            }
            else
            {
                // Chunk-Datenbank sichern
                string saveDir = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(saveDir))
                {
                    ChunkClearManager.Save(saveDir);
                }

                ModEinstellungen.GlobalScanAbgeschlossen = true;
                ModEinstellungen.Speichern();

                Log.Out($"[ES MapScanner] Globaler Map-Scan erfolgreich! Alle {erfassteChunks} Chunks wurden fehlerfrei analysiert.");
                yield break;
            }
        }

        // Prüft mathematisch, ob ein Chunk unspawnbar ist, indem er die Top-Blockwerte und POIs überprüft.
        private static bool PruefeChunkMathematisch(int cx, int cz, IBiomeProvider biomeProvider, List<PrefabInstance> overlappingPOIs)
        {
            int minWorldX = cx * 16;
            int minWorldZ = cz * 16;
            int maxWorldX = minWorldX + 15;
            int maxWorldZ = minWorldZ + 15;

            overlappingPOIs.Clear();
            GameManager.Instance.World.GetPOIsAtXZ(minWorldX, maxWorldX, minWorldZ, maxWorldZ, overlappingPOIs);

            GameRandom rand = GameManager.Instance.World.GetGameRandom();

            // Versuche, eine gültige Spawnposition innerhalb des Chunks zu finden: 50 + 4 Versuche (Vanilla + Ecken)
            foreach (Vector2i dart in AutoSpawner.Generiere54Darts(rand))
            {
                int localX = dart.x;
                int localZ = dart.y;
                int worldX = minWorldX + localX;
                int worldZ = minWorldZ + localZ;

                bool mathInPOI = false;
                for (int p = 0; p < overlappingPOIs.Count; p++)
                {
                    PrefabInstance pi = overlappingPOIs[p];
                    if (pi.prefab != null && pi.prefab.Tags.Test_AnySet(DynamicPrefabDecorator.streetTileTag)) continue;

                    if (worldX >= pi.boundingBoxPosition.x && worldX < pi.boundingBoxPosition.x + pi.boundingBoxSize.x &&
                        worldZ >= pi.boundingBoxPosition.z && worldZ < pi.boundingBoxPosition.z + pi.boundingBoxSize.z)
                    {
                        mathInPOI = true;
                        break;
                    }
                }

                if (mathInPOI) continue; // Überspringe, wenn die Testkoordinate in einem POI liegt

                // Prüfe, ob der Top-Block des Chunks Wasser ist. Wenn ja, überspringe diesen Punkt.
                BlockValue mathTopBlock = biomeProvider.GetTopmostBlockValue(worldX, worldZ);
                bool mathIsWater = mathTopBlock.Block != null && mathTopBlock.Block.blockMaterial.IsLiquid;

                if (mathIsWater) continue; // Überspringe, wenn die Testkoordinate Wasser ist

                return true;
            }

            return false;
        }

        // Prüft physisch, ob ein Chunk spawnbare Positionen enthält und speichert das Ergebnis in der Datenbank.
        private static void PruefeUndSpeichereChunk(int cx, int cz)
        {
            string chunkId = $"{cx}_{cz}";
            Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(cx, cz);

            if (physChunk == null) return; // Sicherheitscheck: Chunk ist nicht geladen, überspringe

            // Prüfe das Biom des Chunks, um zu sehen, ob es überhaupt Zombie-Spawns erlaubt
            byte biomeId = physChunk.GetBiomeId(8, 8);
            BiomeDefinition biome = GameManager.Instance.World.Biomes.GetBiome(biomeId);
            if (biome == null || !BiomeSpawningClass.list.ContainsKey(biome.m_sBiomeName))
            {
                if (!ChunkClearManager.HatChunkEintrag(chunkId))
                {
                    ChunkClearManager.VerarbeiteScannerBatch(new List<string> { chunkId }, 1);
                }
                return;
            }

            // Prüfe physisch, ob der Chunk spawnbare Positionen enthält
            bool validSpawnFound = false;
            int minX = cx * 16;
            int minZ = cz * 16;
            GameRandom rand = GameManager.Instance.World.GetGameRandom();

            // Versuche, eine gültige Spawnposition innerhalb des Chunks zu finden: 50 + 4 Versuche (Vanilla + Ecken)
            foreach (Vector2i dart in AutoSpawner.Generiere54Darts(rand))
            {
                int localX = dart.x;
                int localZ = dart.y;

                // DRY: Physische Eignung des Blocks über die zentrale Methode prüfen
                if (AutoSpawner.IstPhysischePositionSpawntauglich(physChunk, localX, localZ, minX + localX, minZ + localZ, out _))
                {
                    validSpawnFound = true;
                    break;
                }
            }

            // Speichert das Ergebnis des physischen Checks in der Datenbank
            if (!ChunkClearManager.HatChunkEintrag(chunkId))
            {
                int zielStatus = validSpawnFound ? 0 : 1;
                ChunkClearManager.VerarbeiteScannerBatch(new List<string> { chunkId }, zielStatus);
            }
        }
    }
}