using System;
using System.Collections.Generic;
using UnityEngine;

namespace EinmaligerSpawn.Manager
{
    public static class AutoSpawner
    {
        private static float timeSinceLastCheck = 0f;
        private const float CHECK_INTERVAL = 30f;
        private const int GLOBAL_ZOMBIE_LIMIT = 8;

        public static void OnGameUpdate()
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.Players == null)
                return;

            timeSinceLastCheck += Time.deltaTime;
            if (timeSinceLastCheck < CHECK_INTERVAL)
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

            if (currentZombies >= GLOBAL_ZOMBIE_LIMIT)
            {
                return;
            }

            UnityEngine.Debug.Log($"[AutoSpawner] Globale Zombies ({currentZombies}/{GLOBAL_ZOMBIE_LIMIT}). Feuere Spawn-Welle für alle {GameManager.Instance.World.Players.list.Count} Spieler ab...");

            foreach (EntityPlayer player in GameManager.Instance.World.Players.list)
            {
                // Welle feuert automatisch 1 Zombie pro Spieler ab (isManualCommand = false)
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

            int radius = 5;
            int zombieClassID = EntityClass.FromString("zombieArlene");
            GameRandom rand = GameManager.Instance.World.GetGameRandom();

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    // 3x3 Zentrum um den Spieler ignorieren
                    if (Mathf.Abs(x) <= 1 && Mathf.Abs(z) <= 1)
                        continue;

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

                        Chunk chunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(targetCx, targetCz);
                        if (chunk == null)
                        {
                            if (isManualCommand) UnityEngine.Debug.Log($"{logPrefix} FEHLSCHLAG: Chunk {chunkId} abgewiesen! Grund: Zonengrenze (nicht im RAM).");
                            continue;
                        }

                        int gespawnteZombies = 0;
                        int poiCount = 0;
                        int waterCount = 0;
                        int terrainCount = 0;
                        string letzterPoiName = "Unbekannt";

                        for (int zombieIdx = 0; zombieIdx < requestedZombies; zombieIdx++)
                        {
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

                                int y = (int)(chunk.GetHeight(localX, localZ) + 1);
                                int worldX = minX + localX;
                                int worldZ = minZ + localZ;

                                Vector3 checkPosVec = new Vector3(worldX, (float)y, worldZ);

                                PrefabInstance prefab = GameManager.Instance.World.GetPOIAtPosition(checkPosVec, null, null);
                                if (prefab != null)
                                {
                                    poiCount++;
                                    letzterPoiName = prefab.name;
                                    continue;
                                }

                                if (chunk.IsWater(localX, y - 1, localZ))
                                {
                                    waterCount++;
                                    continue;
                                }

                                if (!chunk.CanMobsSpawnAtPos(localX, y, localZ, false, true))
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
                                    ChunkDatenbank.ZombieUrsprung[zombie.entityId] = chunkId;
                                    gespawnteZombies++;

                                    // HIER: Dynamischer Marker nur beim Konsolenbefehl setzen
                                    if (isManualCommand)
                                    {
                                        string magicClassName = "supply_drop"; // Fallback
                                        if (NavObjectClass.NavObjectClassList != null)
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

                                        // Wir nutzen .transform für den Quest-Bypass und überschreiben das Icon auf Rot
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
                                UnityEngine.Debug.LogWarning($"{logPrefix} GEFAHR! Spawn für '{player.EntityName}' erfolgreich in {chunkId}. Marker wurde(n) gesetzt!");
                                if (gespawnteZombies < requestedZombies)
                                {
                                    UnityEngine.Debug.Log($"{logPrefix} Hinweis: Konnte nur {gespawnteZombies}/{requestedZombies} Zombies in diesem Chunk platzieren.");
                                }
                            }
                            else
                            {
                                UnityEngine.Debug.LogWarning($"{logPrefix} GEFAHR! Spawn für '{player.EntityName}' erfolgreich in {chunkId}.");
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

                            UnityEngine.Debug.Log($"{logPrefix} Chunk {chunkId} wurde bei '{player.EntityName}' automatisch als gesäubert markiert.");
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