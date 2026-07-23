using UnityEngine;

namespace EinmaligerSpawn.Manager
{
    public static class AutoSpawner
    {
        private static float timeSinceLastCheck = 0f;

        public static void OnGameUpdate()
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.Players == null)
                return;

            timeSinceLastCheck += Time.deltaTime;

            if (timeSinceLastCheck < ModEinstellungen.SpawnCheckIntervall)
                return;

            timeSinceLastCheck = 0f;

            int currentZombies = 0;
            foreach (Entity entity in GameManager.Instance.World.Entities.list)
            {
                if (entity is EntityEnemy || entity is EntityZombie)
                {
                    currentZombies++;
                }
            }

            if (currentZombies >= ModEinstellungen.GlobalesZombieLimit)
            {
                return;
            }

            UnityEngine.Debug.Log($"[AutoSpawner] Globale Zombies ({currentZombies}/{ModEinstellungen.GlobalesZombieLimit}). Feuere Spawn-Welle für alle {GameManager.Instance.World.Players.list.Count} Spieler ab...");

            foreach (EntityPlayer player in GameManager.Instance.World.Players.list)
            {
                FuehreSpawnAus(player, 1, false);
            }
        }

        public static void FuehreSpawnAus(EntityPlayer player, int requestedZombies, bool isManualCommand)
        {
            Vector3i playerPos = player.GetBlockPosition();
            int playerChunkX = playerPos.x >> 4;
            int playerChunkZ = playerPos.z >> 4;

            string logPrefix = isManualCommand ? "[ES Spawner]" : "[AutoSpawner]";

            if (isManualCommand)
            {
                UnityEngine.Debug.Log($"{logPrefix} Starte aktiven Spawn-Check um Zentrum {playerChunkX}_{playerChunkZ} für {requestedZombies} Zombie(s)...");
            }

            string magicClassName = "supply_drop";
            if (isManualCommand && NavObjectClass.NavObjectClassList != null)
            {
                foreach (NavObjectClass noc in NavObjectClass.NavObjectClassList)
                {
                    if (noc.RequirementType == NavObjectClass.RequirementTypes.None && noc.CompassSettings != null)
                    {
                        magicClassName = noc.NavObjectClassName;
                        break;
                    }
                }
            }

            int radius = 5;
            GameRandom rand = GameManager.Instance.World.GetGameRandom();

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    // DER ALTE 3x3 AUSSCHLUSS WURDE HIER ENTFERNT!

                    int targetCx = playerChunkX + x;
                    int targetCz = playerChunkZ + z;
                    string chunkId = $"{targetCx}_{targetCz}";

                    bool isAusgerottet = ChunkDatenbank.ToteZombiesProChunk.ContainsKey(chunkId) &&
                                         ChunkDatenbank.ToteZombiesProChunk[chunkId] >= 1;

                    if (!isAusgerottet)
                    {
                        if (requestedZombies == 1 && ChunkDatenbank.ZombieUrsprung.ContainsValue(chunkId))
                        {
                            continue;
                        }

                        int minX = targetCx * 16;
                        int minZ = targetCz * 16;

                        // Wir laden den "logischen" Chunk (der den Kill angerechnet bekommt)
                        Chunk logischerChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(targetCx, targetCz);
                        if (logischerChunk == null)
                        {
                            if (isManualCommand) UnityEngine.Debug.Log($"{logPrefix} FEHLSCHLAG: Chunk {chunkId} abgewiesen! Grund: Zonengrenze (nicht im RAM).");
                            continue;
                        }

                        byte biomeId = logischerChunk.GetBiomeId(8, 8);
                        BiomeDefinition biome = GameManager.Instance.World.Biomes.GetBiome(biomeId);
                        BiomeSpawnEntityGroupList groupList = null;

                        if (biome != null && BiomeSpawningClass.list.ContainsKey(biome.m_sBiomeName))
                        {
                            groupList = BiomeSpawningClass.list[biome.m_sBiomeName];
                        }

                        int gespawnteZombies = 0;
                        int poiCount = 0;
                        int waterCount = 0;
                        int terrainCount = 0;
                        string letzterPoiName = "Unbekannt";

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

                            int retryCount = 54;
                            for (int i = 0; i < retryCount; i++)
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

                                // -------------------------------------------------------------
                                // NEU: Der "Wegschiebe"-Mechanismus (Push away)
                                // -------------------------------------------------------------
                                Vector2 flatPlayer = new Vector2(playerPos.x, playerPos.z);
                                Vector2 flatTarget = new Vector2(worldX, worldZ);
                                float flatDist = Vector2.Distance(flatPlayer, flatTarget);

                                if (flatDist < 28f)
                                {
                                    // Berechne die Richtung vom Spieler zum anvisierten Punkt
                                    Vector2 dir = (flatTarget - flatPlayer).normalized;

                                    // Fallback, falls Spieler exakt auf dem Punkt steht
                                    if (dir == Vector2.zero)
                                        dir = new Vector2(rand.RandomFloat - 0.5f, rand.RandomFloat - 0.5f).normalized;

                                    // Schiebe das Ziel nach außen (28m + 1m Puffer)
                                    flatTarget = flatPlayer + dir * 29f;
                                    worldX = Mathf.RoundToInt(flatTarget.x);
                                    worldZ = Mathf.RoundToInt(flatTarget.y);
                                }
                                // -------------------------------------------------------------

                                // Da die neuen Koordinaten nun in einem anderen Chunk liegen können,
                                // müssen wir den *tatsächlichen* physikalischen Chunk für die Gelände-Prüfung laden
                                int physCx = worldX >> 4;
                                int physCz = worldZ >> 4;
                                Chunk physChunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(physCx, physCz);

                                if (physChunk == null)
                                {
                                    terrainCount++;
                                    continue;
                                }

                                int physLocalX = worldX - (physCx * 16);
                                int physLocalZ = worldZ - (physCz * 16);

                                int y = (int)(physChunk.GetHeight(physLocalX, physLocalZ) + 1);
                                Vector3 checkPosVec = new Vector3(worldX, (float)y, worldZ);

                                // Zusätzlicher 3D-Sicherheitscheck (z.B. Spieler steht auf Turm, Zombie drunter)
                                if (Vector3.Distance(checkPosVec, player.position) < 28f)
                                {
                                    continue;
                                }

                                PrefabInstance prefab = GameManager.Instance.World.GetPOIAtPosition(checkPosVec, null, null);
                                if (prefab != null)
                                {
                                    poiCount++;
                                    letzterPoiName = prefab.name;
                                    continue;
                                }

                                if (physChunk.IsWater(physLocalX, y - 1, physLocalZ))
                                {
                                    waterCount++;
                                    continue;
                                }

                                // Wir fragen nun den verschobenen physChunk, ob man da stehen kann
                                if (!physChunk.CanMobsSpawnAtPos(physLocalX, y, physLocalZ, false, true))
                                {
                                    terrainCount++;
                                    continue;
                                }

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

                                    // Das ist der entscheidende Punkt: Der Kill wird dem URSPRÜNGLICHEN logischen Chunk angerechnet!
                                    ChunkDatenbank.ZombieUrsprung[zombie.entityId] = chunkId;
                                    gespawnteZombies++;

                                    if (isManualCommand)
                                    {
                                        NavObjectManager.Instance.RegisterNavObject(magicClassName, zombie.transform, "ui_game_symbol_enemy_dot", false);
                                    }
                                }
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (gespawnteZombies > 0)
                        {
                            if (isManualCommand)
                            {
                                UnityEngine.Debug.LogWarning($"{logPrefix} GEFAHR! Spawn für '{player.EntityName}' erfolgreich (Verknüpft mit {chunkId}). Marker wurde(n) gesetzt!");
                                if (gespawnteZombies < requestedZombies)
                                {
                                    UnityEngine.Debug.Log($"{logPrefix} Hinweis: Konnte nur {gespawnteZombies}/{requestedZombies} Zombies platzieren.");
                                }
                            }
                            else
                            {
                                UnityEngine.Debug.LogWarning($"{logPrefix} GEFAHR! Spawn für '{player.EntityName}' erfolgreich (Verknüpft mit {chunkId}).");
                            }

                            return;
                        }
                        else
                        {
                            string grund = "Unbekannt";
                            if (poiCount >= waterCount && poiCount >= terrainCount) grund = $"Blockiert durch POI '{letzterPoiName}' (traf {poiCount}x auf Gebäude-Zone)";
                            else if (waterCount >= poiCount && waterCount >= terrainCount) grund = $"Blockiert durch Wasser (traf {waterCount}x auf See/Fluss)";
                            else grund = $"Blockiert durch Terrain (traf {terrainCount}x auf Steilhang/Kollision)";

                            if (isManualCommand) UnityEngine.Debug.Log($"{logPrefix} FEHLSCHLAG: Chunk {chunkId} abgewiesen nach 54 Versuchen! Grund: {grund}");

                            if (ChunkDatenbank.ToteZombiesProChunk.ContainsKey(chunkId))
                            {
                                ChunkDatenbank.ToteZombiesProChunk[chunkId]++;
                            }
                            else
                            {
                                ChunkDatenbank.ToteZombiesProChunk[chunkId] = 1;
                            }

                            UnityEngine.Debug.LogWarning($"{logPrefix} Chunk {chunkId} wurde bei '{player.EntityName}' automatisch als gesäubert markiert, weil er keine Spawns unterstüzt.");
                        }
                    }
                }
            }

            if (isManualCommand)
            {
                UnityEngine.Debug.Log($"{logPrefix} Scan abgeschlossen. Keine gefundene Lücke konnte besiedelt werden.");
            }
        }
    }
}