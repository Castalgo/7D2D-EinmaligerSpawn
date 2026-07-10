using HarmonyLib;
using EinmaligerSpawn.Manager;

namespace EinmaligerSpawn.Patches
{
    // ---------------------------------------------------------
    // TEIL 1: Das Schloss (Verhindert neue Spawns)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(SpawnManagerBiomes))]
    public class BiomeSpawn_Kontrolle
    {
        [HarmonyPatch("SpawnUpdate")]
        [HarmonyPrefix]
        public static bool Prefix(string _spawnerName, bool _isSpawnEnemy, ChunkAreaBiomeSpawnData _spawnData)
        {
            if (_spawnData != null && _spawnData.chunk != null)
            {
                Vector3i chunkPos = _spawnData.chunk.GetWorldPos();

                // Prüft, ob das dynamische Limit für diesen Chunk bereits erreicht wurde
                if (ChunkDatenbank.IstChunkAusgerottet(chunkPos, DynamischesSpawnLimit.MaxKills))
                {
                    return false; // Chunk ist ausgerottet -> Blockiert den Spawn
                }
            }

            return true; // Lässt das Spiel normal spawnen
        }
    }

    // ---------------------------------------------------------
    // TEIL 2: Der Tracker (Das Radar beim Spawnen)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(World), "SpawnEntityInWorld")]
    public class Spawn_Tracker_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Entity _entity)
        {
            // Wir filtern nach Zombies (ignorieren Tiere, Spieler etc.)
            if (_entity is EntityZombie)
            {
                // 1. Wir ermitteln den Chunk, in dem der Zombie gerade das Licht der Welt erblickt
                string startChunk = ChunkDatenbank.GetChunkId(_entity.GetBlockPosition());

                // 2. Wir speichern die ID des Zombies und seinen Geburts-Chunk im Gedächtnis
                ChunkDatenbank.ZombieUrsprung[_entity.entityId] = startChunk;
            }
        }
    }
}