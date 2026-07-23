using HarmonyLib;
using EinmaligerSpawn.Manager;

namespace EinmaligerSpawn.Patches
{
    // ---------------------------------------------------------
    // TEIL 1: Der "Terrain-Fake" (Das präzise Schloss)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(World), "GetRandomSpawnPositionInAreaMinMaxToPlayers")]
    public class World_GetRandomSpawnPosition_Patch
    {
        // Wir fangen 'out Chunk _chunk' über 'ref Chunk _chunk' ab und prüfen das boolsche Ergebnis
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, ref Chunk _chunk)
        {
            // Hat die Engine einen gültigen Spawn-Platz gefunden?
            if (__result && _chunk != null)
            {
                Vector3i chunkPos = _chunk.GetWorldPos();

                // Wir prüfen EXAKT diesen einen 16x16 Chunk
                if (ChunkDatenbank.IstChunkAusgerottet(chunkPos, DynamischesSpawnLimit.MaxKills))
                {
                    // VETO! Wir sabotieren die Platzsuche. 
                    // Die Engine denkt, der Bauplatz sei ungültig und sucht woanders weiter, 
                    // ohne dass der globale Biome-Manager abgeschaltet wird.
                    __result = false;
                }
            }
        }
    }

    // ---------------------------------------------------------
    // TEIL 2: Die physische Rückmeldung (Die korrekte Zuordnung)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(SpawnManagerBiomes), "OnEntitySpawned")]
    public class BiomeSpawn_OnEntitySpawned_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Entity __0) // Parameter __1 (der Manager) wird komplett ignoriert!
        {
            if (__0 != null && __0 is EntityEnemy)
            {
                // Wir lesen die echten physischen Koordinaten des Zombies in der Welt ab
                Vector3i physischePosition = __0.GetBlockPosition();

                // Wir wandeln diese Weltkoordinaten in unsere 16x16 Chunk-ID um
                string exakterChunkID = ChunkDatenbank.GetChunkId(physischePosition);

                // Der Kill landet zwingend in dem Chunk, in dem der Zombie die Füße auf den Boden setzt
                ChunkDatenbank.ZombieUrsprung[__0.entityId] = exakterChunkID;
            }
        }
    }
}