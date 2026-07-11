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
            // Update: 'es where' zur Beschreibung hinzugefügt
            return "Verwaltet den Einmaligen Spawn. Nutze 'es range' (Prozent), 'es spawn [Anzahl]' oder 'es where'.";
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
                    Entity closestZombie = null;

                    foreach (KeyValuePair<int, string> kvp in ChunkDatenbank.ZombieUrsprung)
                    {
                        if (GameManager.Instance.World.Entities.dict.TryGetValue(kvp.Key, out Entity ent))
                        {
                            if (ent.IsAlive())
                            {
                                float dist = Vector3.Distance(player.position, ent.position);
                                if (dist < closestDist)
                                {
                                    closestDist = dist;
                                    closestZombie = ent;
                                }
                            }
                        }
                    }

                    if (closestZombie != null)
                    {
                        NavObjectManager.Instance.RegisterNavObject("tracking", closestZombie, "", false);
                        UnityEngine.Debug.Log($"[ES Spawner] Nächster Zombie (ID: {closestZombie.entityId}) ist {Mathf.RoundToInt(closestDist)}m entfernt. Kompass-Markierung gesetzt!");
                    }
                    else
                    {
                        UnityEngine.Debug.Log("[ES Spawner] Keine aktiven ES-Zombies in deiner Nähe gefunden.");
                    }
                } // HIER hat die schließende Klammer gefehlt!
                else
                {
                    UnityEngine.Debug.Log("Unbekannter Befehl. Bitte nutze 'es range', 'es spawn' oder 'es where'.");
                }
            }
            else
            {
                UnityEngine.Debug.Log("Bitte nutze 'es range', 'es spawn' oder 'es where'.");
            }
        }
    }
}