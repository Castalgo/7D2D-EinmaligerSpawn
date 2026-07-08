// Umgebungs_Respawn_deaktivieren.cs
// Wiederholtes Spawnen an derselben Stelle unterbinden
// Koordinaten in Spawntabelle schreiben

using HarmonyLib;
using EinmaligerSpawn.Modifikation;

[HarmonyPatch(typeof(EntitySpawner))]
public class Umgebungs_Respawn_deaktivieren
{
    // Patch die Hauptmethode, die systemweite Spawns erzeugt
    [HarmonyPrefix]
    [HarmonyPatch("SpawnEntities")]
    static bool Prefix_SpawnEntities(EntitySpawner __instance, Vector3i _chunkPos, ChunkAreaBiomeSpawnData _spawnData)
    {
        // Wenn der Chunk bereits vom einmaligen Spawn registriert wurde → keine erneute Spawns
        if (ChunkSpawnManager.IsChunkAlreadySpawned(_chunkPos))
        {
            return false; // unterdrückt das eigentliche Spawning
        }

        // Andernfalls: Einmal zulassen, aber Chunk merken
        ChunkSpawnManager.MarkChunkAsSpawned(_chunkPos);
        return true; // Spawn wird durchgeführt
    }
}