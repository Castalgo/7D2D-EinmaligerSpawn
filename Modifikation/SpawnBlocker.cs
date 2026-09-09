using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using HarmonyLib;
using UnityEngine;

namespace EinmaligerSpawn.SpawnBlocker
{
    // ---------------------------------------------------------
    // TEIL 1: Der Blocker für reguläre Biom-Zombies
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(SpawnManagerBiomes), "SpawnUpdate")]
    public class SpawnManagerBiomes_SpawnUpdate_Patch
    {
        // Server: Wir klinken uns VOR dem eigentlichen Spawn-Update ein.
        [HarmonyPrefix]
        public static bool Prefix(string _spawnerName, bool _isSpawnEnemy, ChunkAreaBiomeSpawnData _spawnData)
        {
            // Server-only. Client rauswerfen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return true;

            // Sicherheitsprüfung, falls die Engine Müll übergibt
            if (_spawnData == null || _spawnData.chunk == null) return true;

            // TIER-FILTER: Wenn es ein friedliches Tier ist, lassen wir die Vanilla-Engine IMMER laufen!
            if (!_isSpawnEnemy) return true;

            // FEIND-FILTER: Wir ermitteln die Chunk-ID der Biom-Area.
            Vector3i chunkPos = _spawnData.chunk.GetWorldPos();
            string chunkId = ChunkClearManager.GetChunkId(chunkPos);

            // Ist dieser Chunk bereits in der Datenbank und als ausgerottet markiert?
            if (ChunkClearManager.ChunkClearLevel.ContainsKey(chunkId) && ChunkClearManager.ChunkClearLevel[chunkId] >= 1)
            {
                // VETO! Der Chunk ist ausgerottet. Die gesamte Spawn-Methode für Biom-Zombies wird hier abgebrochen.
                return false;
            }

            // Chunk ist noch nicht ausgerottet, Vanilla darf ganz normal nach Koordinaten suchen und spawnen
            return true;
        }
    }

    // ---------------------------------------------------------
    // TEIL 2: Der Blocker für den AIDirector (Horden & Screamer)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(World), "GetMobRandomSpawnPosWithWater")]
    public class World_GetMobRandomSpawnPosWithWater_Patch
    {
        // Server: Wenn der Chunk als Spawnort für eine Horde gepickt wurde prüfen wir, ob der Chunk bereits "ausgerottet" ist
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, ref Vector3 _position)
        {
            // Server-only. Client rauswerfen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            // Diese Methode wird meist vom AIDirector aufgerufen.
            if (__result)
            {
                Vector3i spawnPos = new Vector3i(_position);
                string chunkId = ChunkClearManager.GetChunkId(spawnPos);

                if (ChunkClearManager.ChunkClearLevel.ContainsKey(chunkId) && ChunkClearManager.ChunkClearLevel[chunkId] >= 1)
                {
                    // VETO! Wir sabotieren die Koordinaten-Suche der Event-Horde
                    __result = false;
                }
            }
        }
    }

    // ---------------------------------------------------------
    // TEIL 3: Die universelle physische Rückmeldung (für ALLE Spawns)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(World), "SpawnEntityInWorld")]
    public class Universal_ZombieUrsprung_Patch
    {
        // Server: Wenn ein Zombie gespawnt wird, merken wir uns seinen Ursprungs-Chunk in einem temporären Dictionary
        [HarmonyPostfix]
        public static void Postfix(Entity _entity)
        {
            // Server-only. Client rauswerfen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            if (_entity != null && (_entity is EntityEnemy || _entity is EntityZombie))
            {
                // Nur eintragen, wenn der AutoSpawner die ID nicht schon reserviert hat
                if (!ChunkClearManager.ZombieUrsprung.ContainsKey(_entity.entityId))
                {
                    Vector3i physischePosition = _entity.GetBlockPosition();
                    string exakterChunkID = ChunkClearManager.GetChunkId(physischePosition);

                    ChunkClearManager.ZombieUrsprung[_entity.entityId] = exakterChunkID;
                }
            }
        }
    }

    // ---------------------------------------------------------
    // TEIL 4: Die Garbage Collection für despawnte Zombies
    // ---------------------------------------------------------
    public static class ZombieGarbageCollector
    {
        // Wird vom AutoSpawner in regelmäßigen Abständen aufgerufen
        public static void BereinigeGeisterZombies()
        {
            if (ChunkClearManager.ZombieUrsprung.Count == 0) return;

            List<int> geisterIds = new List<int>();

            foreach (int zombieId in ChunkClearManager.ZombieUrsprung.Keys)
            {
                // Prüft, ob die Entity-ID in der Welt noch existiert
                if (!GameManager.Instance.World.Entities.dict.ContainsKey(zombieId))
                {
                    geisterIds.Add(zombieId);
                }
            }

            if (geisterIds.Count > 0)
            {
                foreach (int id in geisterIds)
                {
                    ChunkClearManager.ZombieUrsprung.Remove(id);
                }
            }
        }
    }
}