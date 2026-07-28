using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using UnityEngine;

namespace EinmaligerSpawn.ZombieSpawner
{
    public enum ChunkScanStatus
    {
        Unbekannt = 0,
        Spawntauglich = 1,
        Spawntauglich_UmgebungFertig = 2,
        Gesaeubert_UmgebungFertig = 3
    }

    public static class AutoSpawner
    {
        private static float timeSinceLastCheck = 0f;
        private static Dictionary<int, float> playerSpawnTimers = new Dictionary<int, float>();
        private static Dictionary<int, bool> playerProtectionLost = new Dictionary<int, bool>();

        private static Dictionary<string, ChunkScanStatus> ChunkSpawnbarkeitCache = new Dictionary<string, ChunkScanStatus>();

        private static readonly int[] ScanRingPrioritaeten = { 2, 3, 4, 5, 1, 0 };

        public static void OnGameUpdate()
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.Players == null)
                return;

            timeSinceLastCheck += Time.deltaTime;
            if (timeSinceLastCheck < ModEinstellungen.SpawnCheckIntervall)
                return;

            float passedTime = timeSinceLastCheck;
            timeSinceLastCheck = 0f;

            int currentZombies = 0;
            foreach (Entity entity in GameManager.Instance.World.Entities.list)
            {
                if (entity is EntityEnemy || entity is EntityZombie)
                {
                    currentZombies++;
                }
            }

            List<EntityPlayer> players = GameManager.Instance.World.Players.list;

            // -------------------------------------------------------------
            // 1. Der asynchrone Hintergrund-Scanner (Dynamisches Budget)
            // -------------------------------------------------------------
            ScanBackground(players);

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
            // 2. Anfänger-Schutz-Buff
            // -------------------------------------------------------------

            foreach (EntityPlayer player in players)
            {
                int pid = player.entityId;

                if (!playerSpawnTimers.ContainsKey(pid))
                    playerSpawnTimers[pid] = 0f;

                playerSpawnTimers[pid] += passedTime;

                float drosselungsFaktor = 1f;
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
                        playerProtectionLost[pid] = true;
                    }
                    else if (levelVerbraucht || zeitVerbraucht)
                    {
                        drosselungsFaktor = 8f;
                    }
                    else
                    {
                        drosselungsFaktor = 15f;
                    }
                }

                float requiredInterval = ModEinstellungen.SpawnCheckIntervall * drosselungsFaktor;

                // -------------------------------------------------------------
                // 3. Die Spawn-Routine
                // -------------------------------------------------------------
                if (playerSpawnTimers[pid] >= requiredInterval)
                {
                    playerSpawnTimers[pid] = 0f;
                    FuehreSpawnAus(player, 1, currentZombies);
                }
            }
        }

        private static void ScanBackground(List<EntityPlayer> players)
        {

            // ==========================================
            // SCHRITT 1: VORFILTERN (AFK-Spieler rauswerfen)
            // ==========================================
            List<EntityPlayer> aktiveSpieler = new List<EntityPlayer>();
            foreach (EntityPlayer p in players)
            {
                Vector3i pos = p.GetBlockPosition();
                string centerId = $"{pos.x >> 4}_{pos.z >> 4}";

                if (!ChunkSpawnbarkeitCache.TryGetValue(centerId, out ChunkScanStatus status) ||
                    (status != ChunkScanStatus.Spawntauglich_UmgebungFertig && status != ChunkScanStatus.Gesaeubert_UmgebungFertig))
                {
                    aktiveSpieler.Add(p);
                }
            }

            if (aktiveSpieler.Count == 0) return; // Niemand braucht Rechenleistung

            // ==========================================
            // SCHRITT 2: WERTE BERECHNEN (Listen bauen)
            // ==========================================
            Dictionary<EntityPlayer, List<Vector3i>> spielerKandidaten = new Dictionary<EntityPlayer, List<Vector3i>>();
            GameRandom rand = GameManager.Instance.World.GetGameRandom();

            foreach (EntityPlayer player in aktiveSpieler)
            {
                Vector3i playerPos = player.GetBlockPosition();
                int playerChunkX = playerPos.x >> 4;
                int playerChunkZ = playerPos.z >> 4;

                List<Vector3i> kandidaten = new List<Vector3i>();

                foreach (int radius in ScanRingPrioritaeten)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            if (Mathf.Abs(x) != radius && Mathf.Abs(z) != radius) continue;

                            int targetCx = playerChunkX + x;
                            int targetCz = playerChunkZ + z;
                            string chunkId = $"{targetCx}_{targetCz}";

                            if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId) && KillCounter.ToteZombiesProChunk[chunkId] >= 1) continue;
                            if (ChunkSpawnbarkeitCache.ContainsKey(chunkId)) continue;

                            Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(targetCx, targetCz);
                            if (physChunk == null) continue;

                            kandidaten.Add(new Vector3i(targetCx, 0, targetCz));
                        }
                    }
                }

                for (int i = 0; i < kandidaten.Count; i++)
                {
                    int rndIndex = rand.RandomRange(i, kandidaten.Count);
                    Vector3i temp = kandidaten[i];
                    kandidaten[i] = kandidaten[rndIndex];
                    kandidaten[rndIndex] = temp;
                }

                spielerKandidaten[player] = kandidaten;
            }

            // ==========================================
            // SCHRITT 3: COINS VERTEILEN
            // ==========================================
            int totalCoins = 30;
            int remainingPlayers = aktiveSpieler.Count;

            foreach (EntityPlayer player in aktiveSpieler)
            {
                if (totalCoins <= 0) break;

                int playerBudget = totalCoins / remainingPlayers;
                remainingPlayers--;

                List<Vector3i> kandidaten = spielerKandidaten[player];
                int verbrauchteCoins = 0;
                bool spawntauglichGefunden = false;

                foreach (Vector3i gewaehlterChunk in kandidaten)
                {
                    if (verbrauchteCoins >= playerBudget) break;

                    string targetId = $"{gewaehlterChunk.x}_{gewaehlterChunk.z}";
                    //Log.Out($"[AutoSpawner-DEBUG] Führe Tiefen-Scan für Chunk {targetId} bei Spieler '{player.EntityName}' aus...");

                    bool isSpawntauglich = PruefeUndSpeichereChunk(gewaehlterChunk.x, gewaehlterChunk.z);
                    verbrauchteCoins++;
                    totalCoins--;

                    if (isSpawntauglich)
                    {
                        spawntauglichGefunden = true;
                        //Log.Out($"[AutoSpawner-DEBUG] Chunk {targetId} ist spawntauglich. Suche für '{player.EntityName}' beendet.");
                        //Log.Out($"[AutoSpawner-DEBUG] Chunk {targetId} ist spawntauglich. Suche für '{player.EntityName}' beendet.");
                        break;
                    }
                }

                if (!spawntauglichGefunden && verbrauchteCoins == kandidaten.Count)
                {
                    Vector3i pos = player.GetBlockPosition();
                    string centerChunkId = $"{pos.x >> 4}_{pos.z >> 4}";

                    if (KillCounter.ToteZombiesProChunk.ContainsKey(centerChunkId) && KillCounter.ToteZombiesProChunk[centerChunkId] >= 1)
                    {
                        ChunkSpawnbarkeitCache[centerChunkId] = ChunkScanStatus.Gesaeubert_UmgebungFertig;
                    }
                    else
                    {
                        ChunkSpawnbarkeitCache[centerChunkId] = ChunkScanStatus.Spawntauglich_UmgebungFertig;
                    }
                    //Log.Out($"[AutoSpawner-DEBUG] Umgebung um {centerChunkId} vollständig gescannt.");
                }
            }
        }
        // Gibt nun einen bool zurück, damit der Scanner weiß, ob er weitersuchen muss
        private static bool PruefeUndSpeichereChunk(int cx, int cz)
        {
            string chunkId = $"{cx}_{cz}";
            Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(cx, cz);

            if (physChunk == null) return false;

            byte biomeId = physChunk.GetBiomeId(8, 8);
            BiomeDefinition biome = GameManager.Instance.World.Biomes.GetBiome(biomeId);
            if (biome == null || !BiomeSpawningClass.list.ContainsKey(biome.m_sBiomeName))
            {
                KillCounter.ToteZombiesProChunk[chunkId] = 1;
                Log.Out($"[AutoSpawner] Chunk {chunkId} ist unbewohnbar (Kein Biom/Spawn-Gruppe) und wurde automatisch als gecleart markiert.");

                if (ModEinstellungen.KartenOverlayAktiv)
                {
                    KartenOverlay.ErzwingeRedraw();
                }
                return false;
            }

            int[] offsets = { 3, 8, 13 };
            bool validSpawnFound = false;
            int minX = cx * 16;
            int minZ = cz * 16;

            foreach (int locX in offsets)
            {
                foreach (int locZ in offsets)
                {
                    int y = (int)(physChunk.GetHeight(locX, locZ) + 1);

                    if (physChunk.IsWater(locX, y - 1, locZ)) continue;
                    if (!physChunk.CanMobsSpawnAtPos(locX, y, locZ, false, true)) continue;

                    Vector3 worldPos = new Vector3(minX + locX, y, minZ + locZ);
                    PrefabInstance prefab = GameManager.Instance.World.GetPOIAtPosition(worldPos, null, null);
                    if (prefab != null) continue;

                    validSpawnFound = true;
                    break;
                }
                if (validSpawnFound) break;
            }

            if (validSpawnFound)
            {
                ChunkSpawnbarkeitCache[chunkId] = ChunkScanStatus.Spawntauglich;
                return true;
            }
            else
            {
                KillCounter.ToteZombiesProChunk[chunkId] = 1;
                Log.Out($"[AutoSpawner] Chunk {chunkId} ist unbewohnbar (Wasser/Steilwand/POI) und wurde automatisch als gecleart markiert.");

                if (ModEinstellungen.KartenOverlayAktiv)
                {
                    KartenOverlay.ErzwingeRedraw();
                }
                return false;
            }
        }

        public static void FuehreSpawnAus(EntityPlayer player, int requestedZombies, int currentZombies)
        {
            Vector3i playerPos = player.GetBlockPosition();
            int playerChunkX = playerPos.x >> 4;
            int playerChunkZ = playerPos.z >> 4;
            string centerId = $"{playerChunkX}_{playerChunkZ}";

            // ==========================================
            // SPAM-SCHUTZ & EARLY EXIT
            // ==========================================
            if (ChunkSpawnbarkeitCache.TryGetValue(centerId, out ChunkScanStatus centerStatus))
            {
                if (centerStatus == ChunkScanStatus.Gesaeubert_UmgebungFertig)
                {
                    return;
                }
            }

            string logPrefix = $"[AutoSpawner] Globale Zombies ({currentZombies}/{ModEinstellungen.GlobalesZombieLimit}).";

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

                    if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId) && KillCounter.ToteZombiesProChunk[chunkId] >= 1)
                        continue;

                    if (requestedZombies == 1 && KillCounter.ZombieUrsprung.ContainsValue(chunkId))
                        continue;

                    if (!ChunkSpawnbarkeitCache.TryGetValue(chunkId, out ChunkScanStatus status))
                        continue;

                    if (status != ChunkScanStatus.Spawntauglich && status != ChunkScanStatus.Spawntauglich_UmgebungFertig)
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

                    for (int zombieIdx = 0; zombieIdx < requestedZombies; zombieIdx++)
                    {
                        int zombieClassID = EntityClass.FromString("zombieArlene");
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
                            }

                            int physCx = worldX >> 4;
                            int physCz = worldZ >> 4;
                            Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(physCx, physCz);
                            if (physChunk == null) continue;

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
                Log.Out($"{logPrefix} Konnte keinen Zombie für '{player.EntityName}' erzeugen, weil keine Chunks geladen sind.");
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
            if (ChunkSpawnbarkeitCache != null) ChunkSpawnbarkeitCache.Clear();

            Log.Out("[AutoSpawner] Interner Cache und Timer wurden erfolgreich für die neue Sitzung geleert.");
        }
    }
}