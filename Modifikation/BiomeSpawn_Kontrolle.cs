using HarmonyLib;
using EinmaligerSpawn.Manager;

namespace EinmaligerSpawn.Patches
{
    // Wir zielen auf den neuen SpawnManagerBiomes ab
    [HarmonyPatch(typeof(SpawnManagerBiomes))]
    public class BiomeSpawn_Kontrolle
    {
        // Wir nutzen exakt die Parameter aus deiner Diagnose
        [HarmonyPatch("SpawnUpdate")]
        [HarmonyPrefix]
        public static bool Prefix(string _spawnerName, bool _isSpawnEnemy, ChunkAreaBiomeSpawnData _spawnData)
        {
            // Sicherheitsprüfung, damit es keine NullReference-Fehler gibt
            if (_spawnData != null && _spawnData.chunk != null)
            {
                // Wir holen uns die Chunk-Koordinate direkt aus dem Chunk-Objekt
                Vector3i chunkPos = _spawnData.chunk.GetWorldPos();

                // 8 ist unser XML-Maxcount für Zombies
                if (ChunkDatenbank.IstChunkAusgerottet(chunkPos, 8))
                {
                    return false; // Blockiert den Spawn, da die Wildnis hier ausgerottet ist
                }
            }

            return true; // Lässt das Spiel normal spawnen
        }
    }

    // Der Trigger: Zählt hoch, wenn ein Zombie stirbt
    [HarmonyPatch(typeof(EntityAlive), "SetDead")]
    public class Zombie_Tod_Zaehler
    {
        public static void Postfix(EntityAlive __instance)
        {
            if (__instance is EntityZombie) // Ignoriert Tiere
            {
                ChunkDatenbank.AddToterZombie(__instance.GetBlockPosition());
            }
        }
    }
}