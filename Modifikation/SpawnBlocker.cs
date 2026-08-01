using EinmaligerSpawn.ChunkDatenbank;
using HarmonyLib;
using UnityEngine;

namespace EinmaligerSpawn.SpawnBlocker
{
    // ---------------------------------------------------------
    // TEIL 1: Der Blocker für reguläre Biom-Zombies
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(World), "GetRandomSpawnPositionInAreaMinMaxToPlayers")]
    public class World_GetRandomSpawnPosition_Patch
    {
        // Server: Wenn der Chunk als Spawnort für einen normalen Zombie gepickt wurde prüfen wir, ob der Chunk bereits "ausgerottet" ist
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, ref Chunk _chunk)
        {
            // Server-only. Client rauswerfen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            if (__result && _chunk != null)
            {
                Vector3i chunkPos = _chunk.GetWorldPos();
                if (KillCounter.IstChunkAusgerottet(chunkPos, DynamischesSpawnLimit.MaxKills))
                {
                    __result = false;
                }
            }
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

            if (__result)
            {
                Vector3i spawnPos = new Vector3i(_position); // <-- HIER AUCH ANGEPASST
                string chunkId = KillCounter.GetChunkId(spawnPos);

                if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId) && KillCounter.ToteZombiesProChunk[chunkId] >= 1)
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
                if (!KillCounter.ZombieUrsprung.ContainsKey(_entity.entityId))
                {
                    Vector3i physischePosition = _entity.GetBlockPosition();
                    string exakterChunkID = KillCounter.GetChunkId(physischePosition);

                    KillCounter.ZombieUrsprung[_entity.entityId] = exakterChunkID;
                }
            }
        }
    }
}