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
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, ref Chunk _chunk)
        {
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
        // HIER KORRIGIERT: _position statt _pos
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, ref Vector3 _position)
        {
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
        [HarmonyPostfix]
        public static void Postfix(Entity _entity)
        {
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