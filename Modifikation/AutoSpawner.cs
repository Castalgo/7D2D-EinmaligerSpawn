using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.Network;
using UnityEngine;

namespace EinmaligerSpawn.ZombieSpawner
{
    public static class AutoSpawner
    {
        private static float timeSinceLastCheck = 0f;
        private static Dictionary<int, float> playerSpawnTimers = new Dictionary<int, float>();
        private static Dictionary<int, bool> playerProtectionLost = new Dictionary<int, bool>();

        private static readonly int[] ScanRingPrioritaeten = { 2, 3, 4, 5, 1, 0 };

        public static void OnGameUpdate()
        {
            // Spiel überhaupt geladen?
            if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.Players == null)
                return;

            // Bist du der Server? (Clienten sollen keine Zombies spawnen)
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                return;

            // Blutmond Aktiv?
            if (SkyManager.IsBloodMoonVisible())
            {
                return; // Bricht die gesamte Methode SOFORT für alle Spieler ab.
            }

            // Zeitgeber um nur alle X Sekunden ausgeführt zu werden (Drosselung)
            timeSinceLastCheck += Time.deltaTime;
            if (timeSinceLastCheck < ModEinstellungen.SpawnCheckIntervall)
                return;

            float passedTime = timeSinceLastCheck; // für Anfänger-Schutz-Buff nötig
            timeSinceLastCheck = 0f; // Zeit zurücksetzen

            int currentZombies = 0;
            foreach (Entity entity in GameManager.Instance.World.Entities.list)
            {
                if (entity is EntityEnemy || entity is EntityZombie)
                {
                    currentZombies++;
                }
            }

            // -------------------------------------------------------------
            // 1. Garbage-Collector: Geister-Zombies entfernen, die nicht mehr existieren
            // -------------------------------------------------------------
            SpawnBlocker.ZombieGarbageCollector.BereinigeGeisterZombies();

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

            // -------------------------------------------------------------
            // 2. Spawn-Logik mit Quest-Override und Anfänger-Schutz
            // -------------------------------------------------------------

            List<EntityPlayer> players = GameManager.Instance.World.Players.list;
            foreach (EntityPlayer player in players)
            {
                int pid = player.entityId;

                if (!playerSpawnTimers.ContainsKey(pid))
                    playerSpawnTimers[pid] = 0f;

                playerSpawnTimers[pid] += passedTime;

                // Basis-Wert
                float drosselungsFaktor = 1f;

                // ABSOLUTER OVERRIDE: Ist der Spieler aktiv IN einer gestarteten Quest?
                bool isInQuest = false;
                if (player.QuestJournal != null)
                {
                    Quest aktiveQuest = player.QuestJournal.FindActiveQuest();

                    // Quest da UND Ausrufezeichen bereits geklickt?
                    if (aktiveQuest != null && aktiveQuest.RallyMarkerActivated)
                    {
                        // Vanilla-Methode für die POI-Grenzen holen (inklusive der 5-Block-Toleranz)
                        Rect questBounds = aktiveQuest.GetLocationRect();

                        // Steht der Spieler physisch in diesem Bereich?
                        if (questBounds != Rect.zero && questBounds.Contains(new Vector2(player.position.x, player.position.z)))
                        {
                            isInQuest = true;
                        }
                    }
                }

                if (isInQuest)
                {
                    // Höchste Priorität: Spieler während der Quest komplett in Ruhe lassen.
                    // WICHTIG: Timer nullen, damit nach der Quest nicht sofort ein Spawn nachgeholt wird!
                    playerSpawnTimers[pid] = 0f;
                    continue;
                }

                // ANFÄNGER-SCHUTZ: 
                // Dieser Block wird nur noch erreicht, wenn isInQuest == false ist.
                if (!playerProtectionLost.ContainsKey(pid))
                    playerProtectionLost[pid] = false;

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
                        playerProtectionLost[pid] = true; // Schutz dauerhaft deaktivieren
                    }
                    else if (player.Progression.Level == 1) // Level 1
                    {
                        drosselungsFaktor = 100f;
                    }
                    else if (levelVerbraucht || zeitVerbraucht) // Zeit oder Level verbraucht, aber nicht beides
                    {
                        drosselungsFaktor = 15f;
                    }
                    else // Newbie-Schutz ab Level 2 und unter 7h
                    {
                        drosselungsFaktor = 30f;
                    }
                }

                // -------------------------------------------------------------
                // 3. Die Spawn-Routine
                // -------------------------------------------------------------

                float requiredInterval = ModEinstellungen.SpawnCheckIntervall * drosselungsFaktor;

                if (playerSpawnTimers[pid] >= requiredInterval)
                {
                    playerSpawnTimers[pid] = 0f;
                    FuehreSpawnAus(player, currentZombies);
                }
            }
        }

        private static void FuehreSpawnAus(EntityPlayer player, int currentZombies)
        {
            Vector3i playerPos = player.GetBlockPosition();
            int playerChunkX = playerPos.x >> 4;
            int playerChunkZ = playerPos.z >> 4;

            string logPrefix = $"[ES AutoSpawner] Globale Zombies ({currentZombies}/{ModEinstellungen.GlobalesZombieLimit}).";

            GameRandom rand = GameManager.Instance.World.GetGameRandom();
            bool irgeneinChunkGeladen = false;

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

                for (int i = 0; i < ringChunks.Count; i++)
                {
                    int rndIndex = rand.RandomRange(i, ringChunks.Count);
                    Vector3i temp = ringChunks[i];
                    ringChunks[i] = ringChunks[rndIndex];
                    ringChunks[rndIndex] = temp;
                }

                foreach (Vector3i target in ringChunks)
                {
                    int targetCx = target.x;
                    int targetCz = target.z;
                    string chunkId = $"{targetCx}_{targetCz}";

                    // 1. Priorität: DB-Check
                    if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId) && KillCounter.ToteZombiesProChunk[chunkId] >= 1)
                        continue;

                    // 2. Priorität: Ist für diesen Chunk bereits ein Zombie aktiv?
                    if (KillCounter.ZombieUrsprung.ContainsValue(chunkId))
                        continue;

                    int minX = targetCx * 16;
                    int minZ = targetCz * 16;

                    Chunk logischerChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(targetCx, targetCz);
                    if (logischerChunk == null) continue;

                    // Wir haben mindestens einen physisch geladenen Chunk gefunden!
                    irgeneinChunkGeladen = true;

                    byte biomeId = logischerChunk.GetBiomeId(8, 8);
                    BiomeDefinition biome = GameManager.Instance.World.Biomes.GetBiome(biomeId);
                    BiomeSpawnEntityGroupList groupList = null;

                    if (biome != null && BiomeSpawningClass.list.ContainsKey(biome.m_sBiomeName))
                    {
                        groupList = BiomeSpawningClass.list[biome.m_sBiomeName];
                    }

                    int gespawnteZombies = 0;

                    for (int zombieIdx = 0; zombieIdx < 1; zombieIdx++)
                    {
                        int zombieClassID = EntityClass.FromString("zombieArlene"); // Fallback Zombie
                        if (groupList != null)
                        {
                            foreach (BiomeSpawnEntityGroupData groupData in groupList.list)
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

                        bool spawnFound = false;
                        Vector3 spawnPos = Vector3.zero;

                        // ==========================================
                        // Tracker für blockierte Bewertungen
                        // ==========================================
                        bool zielVerschoben = false;

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

                            int worldX = minX + localX;
                            int worldZ = minZ + localZ;

                            Vector2 flatPlayer = new Vector2(playerPos.x, playerPos.z);
                            Vector2 flatTarget = new Vector2(worldX, worldZ);
                            float flatDist = Vector2.Distance(flatPlayer, flatTarget);

                            if (flatDist < 28f)
                            {
                                Vector2 dir = (flatTarget - flatPlayer).normalized;
                                if (dir == Vector2.zero)
                                    dir = new Vector2(rand.RandomFloat - 0.5f, rand.RandomFloat - 0.5f).normalized;

                                flatTarget = flatPlayer + dir * 29f;
                                worldX = Mathf.RoundToInt(flatTarget.x);
                                worldZ = Mathf.RoundToInt(flatTarget.y);

                                // Markiert, dass wir den Dart wegen Spielernähe in einen Nachbarchunk schieben mussten
                                zielVerschoben = true;
                            }

                            int physCx = worldX >> 4;
                            int physCz = worldZ >> 4;
                            Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(physCx, physCz);

                            // Wenn der verschobene Chunk nicht im RAM ist, wird dieser Versuch übersprungen
                            if (physChunk == null)
                            {
                                continue;
                            }

                            int physLocalX = worldX - (physCx * 16);
                            int physLocalZ = worldZ - (physCz * 16);
                            int y = (int)(physChunk.GetHeight(physLocalX, physLocalZ) + 1);
                            Vector3 checkPosVec = new Vector3(worldX, (float)y, worldZ);

                            if (Vector3.Distance(checkPosVec, player.position) < 28f) continue;

                            PrefabInstance prefab = GameManager.Instance.World.GetPOIAtPosition(checkPosVec, null, null);
                            if (prefab != null) continue;

                            if (physChunk.IsWater(physLocalX, y - 1, physLocalZ)) continue;
                            if (!physChunk.CanMobsSpawnAtPos(physLocalX, y, physLocalZ, false, true)) continue;

                            spawnFound = true;
                            spawnPos = new Vector3(worldX + 0.5f, (float)y, worldZ + 0.5f);
                            break;
                        }

                        if (spawnFound)
                        {
                            Entity zombie = EntityFactory.CreateEntity(zombieClassID, spawnPos, Vector3.zero);
                            if (zombie != null)
                            {
                                GameManager.Instance.World.SpawnEntityInWorld(zombie);
                                KillCounter.ZombieUrsprung[zombie.entityId] = chunkId;
                                gespawnteZombies++;
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
                                KillCounter.ToteZombiesProChunk[chunkId] = 1;
                                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
                            }
                        }
                    }

                    if (gespawnteZombies > 0)
                    {
                        Log.Out($"{logPrefix} {gespawnteZombies} Zombie(s) wurde(n) bei {targetCx},{targetCz} für '{player.EntityName}' gespawnt.");
                        return;
                    }
                }
            }

            // Fehler-Reporting, falls kein Spawn durchgeführt werden konnte
            if (!irgeneinChunkGeladen)
            {
                Log.Out($"{logPrefix} Konnte keinen Zombie für '{player.EntityName}' erzeugen, weil keine Chunks infrage kommen.");
            }
            else
            {
                Log.Out($"{logPrefix} Konnte keinen Zombie für '{player.EntityName}' erzeugen, weil kein gültiger Spawnchunk gefunden wurde.");
            }
        }

        public static void Reset()
        {
            timeSinceLastCheck = 0f;

            if (playerSpawnTimers != null) playerSpawnTimers.Clear();
            if (playerProtectionLost != null) playerProtectionLost.Clear();

            Log.Out("[ES AutoSpawner] Interner Cache und Timer wurden erfolgreich für die neue Sitzung geleert.");
        }
    }

    // =========================================================================================
    // Der neue, einmalige globale Background-Scanner
    // =========================================================================================
    public static class GlobalMapScanner
    {
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

        private static System.Collections.IEnumerator DoGlobalScanCoroutine()
        {
            // 10 Sekunden warten, damit der Server in der Startphase nicht überlastet wird
            yield return new WaitForSeconds(10f);

            IChunkProvider chunkProvider = GameManager.Instance.World.ChunkCache.ChunkProvider;
            IBiomeProvider biomeProvider = chunkProvider.GetBiomeProvider();

            // --- ALTE v1.0 LOGIK (funzt nicht mehr) ---
            // Vector3i minSize, maxSize;
            // chunkProvider.GetWorldExtent(out minSize, out maxSize);
            // int minChunkX = minSize.x >> 4;
            // int minChunkZ = minSize.z >> 4;
            // int maxChunkX = maxSize.x >> 4;
            // int maxChunkZ = maxSize.z >> 4;


            // +++ NEUE STATISCHE LOGIK EINFÜGEN +++
            // Wir greifen auf die festen Welt-Metadaten der V3-Architektur zu
            GameUtils.WorldInfo worldInfo = ((ChunkProviderAbstract)chunkProvider).WorldInfo;

            // Vector2i speichert die Flächendiagonale in x und y (wobei y in 2D unserem 3D-Z entspricht)
            int weltGroesseX = worldInfo.WorldSize.x;
            int weltGroesseZ = worldInfo.WorldSize.y;

            int halfSizeX = weltGroesseX / 2;
            int halfSizeZ = weltGroesseZ / 2;

            // Berechnung der absoluten Chunk-Grenzen (Weltmitte ist 0,0. Ein Chunk = 16 Blöcke)
            int minChunkX = -halfSizeX / 16;
            int minChunkZ = -halfSizeZ / 16;
            int maxChunkX = (halfSizeX / 16) - 1;
            int maxChunkZ = (halfSizeZ / 16) - 1;

            // Wird einmalig erstellt und immer wiederverwendet, um RAM-Müll (Garbage Collection) zu vermeiden
            List<PrefabInstance> overlappingPOIs = new List<PrefabInstance>();

            // =========================================================================================
            // PHASE 1: REINER MATHE-SCAN (Ganze Karte, blitzschnell, merkt sich keine X/Z Positionen)
            // =========================================================================================
            Log.Out("[ES MapScanner] PHASE 1: Starte mathematischen Vorab-Scan über die gesamte Karte...");
            int mathChunksProcessed = 0;

            for (int mathX = minChunkX; mathX <= maxChunkX; mathX++)
            {
                for (int mathZ = minChunkZ; mathZ <= maxChunkZ; mathZ++)
                {
                    string chunkId = $"{mathX}_{mathZ}";

                    // Bereits in der Datenbank? Überspringen!
                    if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                    {
                        continue;
                    }

                    // Mathe-Filter (Ohne Chunk laden!)
                    if (!GlobalMapScanner.PruefeChunkMathematisch(mathX, mathZ, biomeProvider, overlappingPOIs))
                    {
                        // Der Mathe-Filter sagt: "Hier gibt es zu 100 % keinen Platz."
                        KillCounter.ToteZombiesProChunk[chunkId] = 1;
                    }

                    mathChunksProcessed++;

                    // Alle 1000 Chunks kurz einen Frame an die Engine abgeben, damit der Server/die UI nicht einfriert
                    if (mathChunksProcessed >= 1000)
                    {
                        mathChunksProcessed = 0;
                        yield return null;
                    }
                }
            }
            Log.Out("[ES MapScanner] PHASE 1 ABGESCHLOSSEN! Alle unspawnbaren Ozeane und POI-Cluster wurden aussortiert.");


            // =========================================================================================
            // PHASE 2: DEEP-SCAN (Langsames Laden der restlichen Wackelkandidaten mit Speicherung)
            // =========================================================================================
            Log.Out("[ES MapScanner] PHASE 2: Starte Deep-Scan für verbleibende Chunks...");

            int cx = minChunkX;
            int cz = minChunkZ;

            int chunksSinceLastSave = 0;
            List<Vector2i> chunkBatch = new List<Vector2i>();

            while (cx <= maxChunkX)
            {
                while (cz <= maxChunkZ)
                {
                    string chunkId = $"{cx}_{cz}";

                    // Wenn er hier noch in der Datenbank ist, hat ihn Phase 1 oder ein alter Scan schon erledigt
                    if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                    {
                        cz++;
                        continue;
                    }

                    // ==========================================
                    // PHYSISCHE WACKELKANDIDATEN IN DEN BATCH
                    // ==========================================
                    chunkBatch.Add(new Vector2i(cx, cz));

                    // Wenn wir 20 Kandidaten gesammelt haben, werfen wir die Festplatte an
                    if (chunkBatch.Count >= 20 || (cx == maxChunkX && cz == maxChunkZ && chunkBatch.Count > 0))
                    {
                        while (GameManager.Instance.World.ChunkCache.Count() >= 5000)
                        {
                            int ac = GameManager.Instance.World.ChunkCache.Count();
                            Log.Out($"[ES MapScanner] RAM-Schutz aktiv ({ac} geladene Chunks). Pausiere für 5 Sekunden...");
                            yield return new WaitForSeconds(5f);
                        }

                        // ==========================================
                        // ASYNCHRONES LADEN & AUSWERTEN
                        // ==========================================

                        // Lade-Aufträge für alle fehlenden Chunks im Batch erteilen
                        foreach (Vector2i pos in chunkBatch)
                        {
                            if (GameManager.Instance.World.ChunkCache.GetChunkSync(pos.x, pos.y) == null)
                            {
                                // --- DROSSELVENTIL START ---
                                // Verhindert Thread-Kollisionen bei zu schneller Anfrage
                                HashSetList<long> pendingChunks = chunkProvider.GetRequestedChunks();
                                if (pendingChunks != null)
                                {
                                    // Wir fragen die interne Liste nach ihrer Größe
                                    while (pendingChunks.list.Count > 100)
                                    {
                                        // Gibt dem Main-Thread kurz Pause, damit die Engine abarbeiten kann
                                        yield return new WaitForSeconds(0.1f);
                                    }
                                }
                                // --- DROSSELVENTIL ENDE ---

                                chunkProvider.RequestChunk(pos.x, pos.y);
                            }
                        }

                        // Warten, bis die Engine die Chunks von der Festplatte in den Cache geladen hat
                        foreach (Vector2i pos in chunkBatch)
                        {
                            float timeout = 0f;
                            while (GameManager.Instance.World.ChunkCache.GetChunkSync(pos.x, pos.y) == null)
                            {
                                timeout += Time.deltaTime;
                                if (timeout > 5f) break; // Notfall-Abbruch nach 5 Sekunden pro Chunk
                                yield return null; // Pausiert die Coroutine für einen Frame, Server läuft flüssig weiter
                            }
                        }

                        // Jetzt sind die Chunks sicher im RAM -> Abarbeiten wie vorher!
                        foreach (Vector2i pos in chunkBatch)
                        {
                            Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(pos.x, pos.y);

                            if (physChunk != null)
                            {
                                PruefeUndSpeichereChunk(pos.x, pos.y);
                                chunksSinceLastSave++;

                                // Prüfen ob Spieler in der Nähe ist, um ihn ggf. nicht aus dem RAM zu werfen
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
                                    // Deinen Chunk sauber wieder aus dem RAM werfen
                                    GameManager.Instance.World.m_ChunkManager.RemoveChunk(physChunk.Key);
                                }
                            }
                            else
                            {
                                // DOKU: NICHT LÖSCHEN
                                // Du darfst hier auf keinen Fall den Chunk auf 1 stellen, nur weil er nicht lädt. Sonst beim Lade-Fehler mitten auf der Karte
                                // mehrere Chunks als geecleart markiert, obwohl sie regulär spawnen könnten.
                                chunksSinceLastSave++;
                            }
                        }

                        chunkBatch.Clear();

                        // Fortschritt in Phase 2 speichern
                        if (chunksSinceLastSave >= 500)
                        {
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

            // ==========================================
            // KASSENSTURZ UND ABSCHLUSS-PRÜFUNG
            // ==========================================

            // Die theoretische Gesamtanzahl aller Chunks auf dieser Karte
            // (z. B. 8192 / 16 = 512. Und 512 * 512 = 262.144 Chunks)
            int erwarteteChunks = (weltGroesseX / 16) * (weltGroesseZ / 16);

            // Die tatsächlich in deiner Datenbank hinterlegten Chunks
            int erfassteChunks = KillCounter.ToteZombiesProChunk.Count; // (Oder wie dein Dictionary exakt heißt)

            if (erfassteChunks < erwarteteChunks)
            {
                int fehlendeChunks = erwarteteChunks - erfassteChunks;

                Log.Warning($"[AutoSpawner] Map-Scan unvollständig! Es fehlen {fehlendeChunks} Chunks (Erfasst: {erfassteChunks} / {erwarteteChunks}). Pausiere Scan bis zum Neustart.");
                yield break; // Abbruch
            }
            else 
            { 

                // ==========================================
                // Regulärer, erfolgreicher Abschluss
                // ==========================================

                ModEinstellungen.GlobalScanAbgeschlossen = true;
                ModEinstellungen.Speichern();

                Log.Out($"[AutoSpawner] Globaler Map-Scan erfolgreich! Alle {erfassteChunks} Chunks wurden fehlerfrei analysiert.");
                yield break;
            }
        }

        private static bool PruefeChunkMathematisch(int cx, int cz, IBiomeProvider biomeProvider, List<PrefabInstance> overlappingPOIs)
        {
            int minWorldX = cx * 16;
            int minWorldZ = cz * 16;
            int maxWorldX = minWorldX + 15;
            int maxWorldZ = minWorldZ + 15;

            // Liste leeren und POIs im Chunk-Bereich abfragen
            overlappingPOIs.Clear();
            GameManager.Instance.World.GetPOIsAtXZ(minWorldX, maxWorldX, minWorldZ, maxWorldZ, overlappingPOIs);

            GameRandom rand = GameManager.Instance.World.GetGameRandom();

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

                int worldX = minWorldX + localX;
                int worldZ = minWorldZ + localZ;

                // POI-Kollisionsprüfung
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

                if (mathInPOI) continue;

                // Wasser-Kollisionsprüfung
                BlockValue mathTopBlock = biomeProvider.GetTopmostBlockValue(worldX, worldZ);
                bool mathIsWater = mathTopBlock.Block != null && mathTopBlock.Block.blockMaterial.IsLiquid;

                if (mathIsWater) continue;

                // Mindestens ein Punkt hat überlebt -> Chunk KÖNNTE spawntauglich sein (0)
                return true;
            }

            // Alle 54 Darts sind im Wasser oder in Gebäuden gelandet -> Chunk ist tot (1)
            return false;
        }

        private static void PruefeUndSpeichereChunk(int cx, int cz)
        {
            string chunkId = $"{cx}_{cz}";
            Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(cx, cz);

            if (physChunk == null) return; // Chunk nicht geladen, kann nicht geprüft werden

            byte biomeId = physChunk.GetBiomeId(8, 8);
            BiomeDefinition biome = GameManager.Instance.World.Biomes.GetBiome(biomeId);
            if (biome == null || !BiomeSpawningClass.list.ContainsKey(biome.m_sBiomeName))
            {
                if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                    KillCounter.ToteZombiesProChunk[chunkId] = 1;

                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));

                return; // Biom ungültig
            }

            bool validSpawnFound = false;
            int minX = cx * 16;
            int minZ = cz * 16;
            GameRandom rand = GameManager.Instance.World.GetGameRandom();

            // 50 Proben, exakt wie im echten Spawner, plus die 4 Ecken
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
                    // Die 4 Ecken prüfen
                    if (i == 50) { localX = 0; localZ = 0; }
                    else if (i == 51) { localX = 0; localZ = 15; }
                    else if (i == 52) { localX = 15; localZ = 0; }
                    else { localX = 15; localZ = 15; }
                }

                int y = (int)(physChunk.GetHeight(localX, localZ) + 1);

                // Ist Wasser oder ein anderer unmöglicher Untergrund vorhanden?
                if (physChunk.IsWater(localX, y - 1, localZ)) continue;
                if (!physChunk.CanMobsSpawnAtPos(localX, y, localZ, false, true)) continue;

                // prüfen ob wir im POI (Prefab) sind
                Vector3 worldPos = new Vector3(minX + localX, y, minZ + localZ);
                PrefabInstance prefab = GameManager.Instance.World.GetPOIAtPosition(worldPos, null, null);
                if (prefab != null) continue;

                validSpawnFound = true;
                break;
            }

            if (validSpawnFound)
            {
                // Chunk in DB schreiben
                if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                {
                    KillCounter.ToteZombiesProChunk[chunkId] = 0;
                }

                return;
            }
            else
            {
                if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                    KillCounter.ToteZombiesProChunk[chunkId] = 1;

                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));

                return;
            }
        }
    }
}