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
            return "Verwaltet den Einmaligen Spawn. Nutze 'es range [x]' (Prozent), 'es spawn [x]', 'es where' oder 'es cheat_clear [x]'.";
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

                    int requestedZombies = 1;
                    if (_params.Count > 1)
                    {
                        if (int.TryParse(_params[1], out int parsedCount))
                        {
                            requestedZombies = Mathf.Clamp(parsedCount, 1, 16);
                        }
                    }

                    AutoSpawner.FuehreSpawnAus(player, requestedZombies, true);
                }
                // -----------------------------------------------------------------
                // BEFEHL: es where
                // -----------------------------------------------------------------
                else if (subCommand == "where")
                {
                    float closestDist = float.MaxValue;
                    Entity closestEnemy = null;

                    foreach (Entity ent in GameManager.Instance.World.Entities.list)
                    {
                        if ((ent is EntityEnemy || ent is EntityZombie) && ent.IsAlive())
                        {
                            float dist = Vector3.Distance(player.position, ent.position);
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                closestEnemy = ent;
                            }
                        }
                    }

                    if (closestEnemy != null)
                    {
                        string magicClassName = "supply_drop";
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

                        NavObjectManager.Instance.RegisterNavObject(magicClassName, closestEnemy.transform, "ui_game_symbol_enemy_dot", false);

                        UnityEngine.Debug.Log($"[ES Spawner] Universal-Radar: Nächster Feind (Typ: {closestEnemy.GetType().Name}, ID: {closestEnemy.entityId}) ist {Mathf.RoundToInt(closestDist)}m entfernt.");
                        UnityEngine.Debug.Log($"[ES Spawner] Marker erfolgreich über Systemklasse '{magicClassName}' gesetzt!");
                    }
                    else
                    {
                        UnityEngine.Debug.Log("[ES Spawner] Universal-Radar: Keine lebenden Feinde in deinem geladenen Umfeld gefunden.");
                    }
                }
                // -----------------------------------------------------------------
                // BEFEHL: es cheat_clear
                // -----------------------------------------------------------------
                else if (subCommand == "cheat_clear")
                {
                    int radiusMeter = 20; // Standardwert geändert

                    if (_params.Count > 1)
                    {
                        if (int.TryParse(_params[1], out int parsedRadius))
                        {
                            // Begrenzung auf maximal 256 Meter
                            radiusMeter = Mathf.Clamp(parsedRadius, 1, 256);
                        }
                    }

                    Vector3i playerPos = player.GetBlockPosition();
                    int px = playerPos.x;
                    int pz = playerPos.z;

                    int playerChunkX = px >> 4;
                    int playerChunkZ = pz >> 4;

                    int chunkSuchRadius = Mathf.CeilToInt((float)radiusMeter / 16f);
                    int maxDistSq = radiusMeter * radiusMeter;

                    int newlyCleared = 0;
                    int totalChecked = 0;

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
                                totalChecked++;
                                string chunkId = $"{cx}_{cz}";

                                if (!ChunkDatenbank.ToteZombiesProChunk.ContainsKey(chunkId))
                                {
                                    ChunkDatenbank.ToteZombiesProChunk[chunkId] = 0;
                                    newlyCleared++;
                                }

                                // Stumpf +1 addieren
                                ChunkDatenbank.ToteZombiesProChunk[chunkId]++;
                            }
                        }
                    }

                    UnityEngine.Debug.Log($"=== Cheat Clear ({radiusMeter}m) ===");
                    UnityEngine.Debug.LogWarning($"[ES Spawner] Ich habe {totalChecked} Chunks geprüft und {newlyCleared} neu ausgerottet.");
                }
                else
                {
                    UnityEngine.Debug.Log("Bitte nutze 'es range [x]', 'es spawn [x]', 'es where' oder 'es cheat_clear [x]'.");
                }
            }
        }
    }
}