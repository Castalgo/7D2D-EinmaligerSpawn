using System.Collections.Generic;
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
        public static void Postfix(ref bool __result, int _minDistance, ref Chunk _chunk)
        {
            // Server-only. Client rauswerfen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            // VANILLA-TRICK: _minDistance ist 28 für Feinde und 48 für Tiere.
            // Ist der Wert über 30, wissen wir: Die Engine sucht gerade Platz für ein Tier!
            if (_minDistance > 30)
            {
                return; // Wir lassen die Methode unangetastet, Tiere dürfen hier spawnen.
            }

            if (__result && _chunk != null)
            {
                Vector3i chunkPos = _chunk.GetWorldPos();
                if (KillCounter.IstChunkAusgerottet(chunkPos, DynamischesSpawnLimit.MaxKills))
                {
                    // VETO! Wir sabotieren die Koordinaten-Suche für Zombies.
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

            // Diese Methode wird AUSSCHLIESSLICH vom AIDirector für Horden aufgerufen (immer Feinde).
            // Wir brauchen hier also keinen Tier-Filter.
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

    // ---------------------------------------------------------
    // TEIL 4: Die Garbage Collection für despawnte Zombies
    // ---------------------------------------------------------
    public static class ZombieGarbageCollector
    {
        // Wird vom AutoSpawner in regelmäßigen Abständen aufgerufen
        public static void BereinigeGeisterZombies()
        {
            if (KillCounter.ZombieUrsprung.Count == 0) return;

            List<int> geisterIds = new List<int>();

            foreach (int zombieId in KillCounter.ZombieUrsprung.Keys)
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
                    KillCounter.ZombieUrsprung.Remove(id);
                }

                // Optional: Deaktivieren, wenn es zu viel im Log spamt
                // Log.Out($"[EinmaligerSpawn] Garbage Collection: {geisterIds.Count} despawnte Geister-Zombies aus dem Gedächtnis gelöscht.");
            }
        }
    }
}
