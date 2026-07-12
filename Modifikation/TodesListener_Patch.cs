using System.Collections.Generic;
using HarmonyLib;
using EinmaligerSpawn.Manager;
using UnityEngine;

namespace EinmaligerSpawn.Patches
{
    [HarmonyPatch(typeof(EntityAlive), "SetDead")]
    public class TodesListener_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityAlive __instance)
        {
            // Prüfen: Ist es ein Zombie UND steht er in unserem Gedächtnis?
            if (__instance is EntityZombie && ChunkDatenbank.ZombieUrsprung.ContainsKey(__instance.entityId))
            {
                // ---------------------------------------------------------
                // SCHRITT 1: Der Ursprung (Die alte Heimat verriegeln)
                // ---------------------------------------------------------
                string ursprungsChunk = ChunkDatenbank.ZombieUrsprung[__instance.entityId];
                ChunkDatenbank.AddToterZombieNachID(ursprungsChunk, DynamischesSpawnLimit.MaxKills);

                // Zombie aus dem Gedächtnis löschen (Er ist ja jetzt tot)
                ChunkDatenbank.ZombieUrsprung.Remove(__instance.entityId);


                // ---------------------------------------------------------
                // SCHRITT 2: Der Todes-Chunk (Taktische Säuberung)
                // ---------------------------------------------------------
                Vector3 todesPos = __instance.position;

                // Chunk-Koordinaten des Todesortes berechnen
                int tCx = Utils.Fastfloor(todesPos.x / 16f);
                int tCz = Utils.Fastfloor(todesPos.z / 16f);
                string todesChunkId = $"{tCx}_{tCz}";

                // Wir prüfen das Schlachtfeld nur, wenn der Todesort NICHT der Ursprungsort ist.
                // (Wäre es derselbe Chunk, hat Schritt 1 ihn ja ohnehin gerade verriegelt).
                if (ursprungsChunk != todesChunkId)
                {
                    // Wir bauen eine virtuelle Box, die exakt 16x16 Meter groß ist (genau 1 Chunk) 
                    // und unendlich hoch/tief (256 Meter), platziert in der Mitte des Todes-Chunks.
                    float centerX = (tCx * 16f) + 8f;
                    float centerZ = (tCz * 16f) + 8f;
                    Bounds chunkBounds = new Bounds(new Vector3(centerX, 128f, centerZ), new Vector3(16f, 256f, 16f));

                    // NATIVE ENGINE-ABFRAGE: Wer lebt in dieser Box, AUSSER dem Zombie, der gerade stirbt?
                    List<EntityAlive> lebendeEntitaeten = GameManager.Instance.World.GetLivingEntitiesInBounds(__instance, chunkBounds);

                    bool weitereFeindeVorhanden = false;

                    if (lebendeEntitaeten != null)
                    {
                        foreach (EntityAlive ent in lebendeEntitaeten)
                        {
                            // Wir ignorieren Tiere oder Mitspieler, uns interessieren nur Feinde
                            if (ent is EntityEnemy || ent is EntityZombie)
                            {
                                weitereFeindeVorhanden = true;
                                break;
                            }
                        }
                    }

                    // Belohnung für den Spieler: Das Gebiet ist komplett feindfrei!
                    if (!weitereFeindeVorhanden)
                    {
                        if (!ChunkDatenbank.ToteZombiesProChunk.ContainsKey(todesChunkId))
                        {
                            ChunkDatenbank.ToteZombiesProChunk[todesChunkId] = 0;
                            Debug.LogWarning($"[EinmaligerSpawn] Taktischer Clear! Chunk {todesChunkId} wurde durch Flächensäuberung gesichert.");
                        }
                        
                        // Stumpf +1 addieren, egal wie hoch der Wert schon ist
                        ChunkDatenbank.ToteZombiesProChunk[todesChunkId]++;
                    }
                }
            }
        }
    }
}