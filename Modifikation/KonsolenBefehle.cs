using System;
using System.Collections.Generic;
using EinmaligerSpawn.Manager;
using UnityEngine;

namespace EinmaligerSpawn.Commands
{
    public class ConsoleCmdEinmaligerSpawn : ConsoleCmdAbstract
    {
        public override string[] getCommands()
        {
            return new string[] { "es" };
        }

        public override string getDescription()
        {
            return "Verwaltet den Einmaligen Spawn. Nutze 'es range' (Prozent) oder 'es spawn' (Aktiver Radar-Test).";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null) return;

            if (_params.Count > 0)
            {
                string subCommand = _params[0].ToLower();

                // -----------------------------------------------------------------
                // BEFEHL: es range
                // -----------------------------------------------------------------
                if (subCommand == "range")
                {
                    int radiusMeter = 80;

                    if (_params.Count > 1)
                    {
                        int.TryParse(_params[1], out radiusMeter);
                    }

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

                                if (ChunkDatenbank.ToteZombiesProChunk.ContainsKey(chunkId) &&
                                    ChunkDatenbank.ToteZombiesProChunk[chunkId] >= 1)
                                {
                                    x_Gesperrt++;
                                }
                            }
                        }
                    }

                    float prozentFloat = y_Gesamt > 0 ? ((float)x_Gesperrt / y_Gesamt) * 100f : 0f;
                    int prozent = Mathf.RoundToInt(prozentFloat);

                    UnityEngine.Debug.Log($"=== Spawn-Radar ({radiusMeter}m) ===");
                    UnityEngine.Debug.Log($"Status: {x_Gesperrt}/{y_Gesamt} ({prozent}%)");
                }
                // -----------------------------------------------------------------
                // BEFEHL: es spawn
                // -----------------------------------------------------------------
                else if (subCommand == "spawn")
                {
                    if (_senderInfo.RemoteClientInfo != null)
                    {
                        UnityEngine.Debug.LogWarning("[EinmaligerSpawn] ES Spawner kann nur vom Host/Lokal ausgeführt werden.");
                        return;
                    }

                    Vector3i playerPos = player.GetBlockPosition();
                    int playerChunkX = playerPos.x >> 4;
                    int playerChunkZ = playerPos.z >> 4;

                    UnityEngine.Debug.Log($"[ES Spawner] Starte aktiven Spawn-Check um Zentrum {playerChunkX}_{playerChunkZ}...");

                    int radius = 5; // 5 Chunks = ca. 80 Meter
                    int zombieClassID = EntityClass.FromString("zombieArlene");
                    GameRandom rand = GameManager.Instance.World.GetGameRandom();

                    for (int x = -radius; x <= radius; x++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            // Das 3x3 Zentrum ignorieren
                            if (Mathf.Abs(x) <= 1 && Mathf.Abs(z) <= 1)
                                continue;

                            int targetCx = playerChunkX + x;
                            int targetCz = playerChunkZ + z;
                            string chunkId = $"{targetCx}_{targetCz}";

                            bool isAusgerottet = ChunkDatenbank.ToteZombiesProChunk.ContainsKey(chunkId) &&
                                                 ChunkDatenbank.ToteZombiesProChunk[chunkId] >= 1;

                            if (!isAusgerottet)
                            {
                                int minX = targetCx * 16;
                                int minZ = targetCz * 16;

                                // 1. Check: Ist der Chunk überhaupt geladen?
                                Chunk chunk = (Chunk)GameManager.Instance.World.ChunkCache.GetChunkSync(targetCx, targetCz);
                                if (chunk == null)
                                {
                                    UnityEngine.Debug.Log($"[ES Spawner] FEHLSCHLAG: Chunk {chunkId} abgewiesen! Grund: Zonengrenze (Chunk nicht im Arbeitsspeicher).");
                                    continue;
                                }

                                bool spawnFound = false;
                                Vector3 spawnPos = Vector3.zero;

                                int poiCount = 0;
                                int waterCount = 0;
                                int terrainCount = 0;
                                string letzterPoiName = "Unbekannt";

                                // 2. Suchschleife: 50x Random + 4x Ecken (Total 54)
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

                                    // POI-Check inklusive Namensabfrage
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

                                        // Zuweisung in die Dictionary-Datenbank
                                        ChunkDatenbank.ZombieUrsprung[zombie.entityId] = chunkId;

                                        string direction = GetHimmelsrichtung(x, z);

                                        // HIER IST DIE EINZIGE WARNING
                                        UnityEngine.Debug.LogWarning($"[ES Spawner] GEFAHR! Spawn erfolgreich in {chunkId}. Himmelsrichtung: {direction}. Spawn erfolgreich abgeschlossen.");
                                        return;
                                    }
                                }
                                else
                                {
                                    string grund = "Unbekannt";
                                    if (poiCount >= waterCount && poiCount >= terrainCount) grund = $"Blockiert durch POI '{letzterPoiName}' (traf {poiCount}x auf Gebäude-Zone)";
                                    else if (waterCount >= poiCount && waterCount >= terrainCount) grund = $"Blockiert durch Wasser (traf {waterCount}x auf See/Fluss)";
                                    else grund = $"Blockiert durch Terrain (traf {terrainCount}x auf Steilhang/Kollision)";

                                    UnityEngine.Debug.Log($"[ES Spawner] FEHLSCHLAG: Chunk {chunkId} abgewiesen nach 54 Versuchen! Grund: {grund}");
                                }
                            }
                        }
                    }

                    UnityEngine.Debug.Log("[ES Spawner] Scan abgeschlossen. Keine gefundene Lücke konnte besiedelt werden.");
                }
                else
                {
                    UnityEngine.Debug.Log("Unbekannter Befehl. Bitte nutze 'es range' oder 'es spawn'.");
                }
            }
            else
            {
                UnityEngine.Debug.Log("Bitte nutze 'es range' oder 'es spawn'.");
            }
        }

        // -----------------------------------------------------------------
        // HILFSMETHODEN
        // -----------------------------------------------------------------
        private string GetHimmelsrichtung(int x, int z)
        {
            if (x >= -1 && x <= 1 && z > 1) return "Norden";
            if (x >= -1 && x <= 1 && z < -1) return "Süden";
            if (z >= -1 && z <= 1 && x > 1) return "Osten";
            if (z >= -1 && z <= 1 && x < -1) return "Westen";

            if (x > 1 && z > 1) return "Nordosten";
            if (x < -1 && z > 1) return "Nordwesten";
            if (x > 1 && z < -1) return "Südosten";
            if (x < -1 && z < -1) return "Südwesten";

            return "Unbekannt";
        }
    }
}