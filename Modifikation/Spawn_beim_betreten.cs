// Spawn_beim_betreten.cs: prüft, ob beim ersten Betreten gespawnt werden muss

using HarmonyLib;
using UnityEngine;
using EinmaligerSpawn.Modifikation;

[HarmonyPatch(typeof(Chunk))]
public class Spawn_beim_betreten
{
    // Patch: Wird aufgerufen, wenn ein Chunk erstmals aktiv wird
    [HarmonyPostfix]
    [HarmonyPatch("SetVisible")]
    static void Postfix_SetVisible(Chunk __instance, bool bVisible)
    {
        if (!bVisible) return;
        if (__instance == null || GameManager.Instance.World == null) return;

        Vector3i chunkPos = __instance.GetWorldPos();

        // Prüfe über zentralen Manager, ob dort schon gespawnt wurde
        if (ChunkSpawnManager.IsChunkAlreadySpawned(chunkPos))
            return;

        // Jetzt einmalig spawnen lassen
        SpawnEinmalInChunk(__instance);

        // Danach registrieren, damit nie wieder gespawnt wird
        ChunkSpawnManager.MarkChunkAsSpawned(chunkPos);
    }

    // Eigentliche Logik für einmaliges Spawnen
    private static void SpawnEinmalInChunk(Chunk chunk)
    {
        World world = GameManager.Instance.World;
        if (world == null) return;

        Vector3 chunkCenter = chunk.GetWorldPos() + new Vector3(8, 0, 8);
        EntityPlayer player = world.GetPrimaryPlayer();

        if (player != null)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 spawnPos = chunkCenter + new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f));
                Vector3i blockPos = new Vector3i((int)spawnPos.x, (int)player.position.y, (int)spawnPos.z);

                Entity entity = EntityFactory.CreateEntity(EntityClass.FromString("zombieBoe"), blockPos);
                if (entity != null)
                {
                    world.SpawnEntityInWorld(entity);
                }
            }
        }
    }
}