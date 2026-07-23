using EinmaligerSpawn.Manager;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace EinmaligerSpawn.Patches
{
    [HarmonyPatch(typeof(EntityAlive), "SetDead")]
    public class TodesListener_Patch
    {
        // NEU: Wir schalten uns VOR die Engine-Logik
        [HarmonyPrefix]
        public static void Prefix(EntityAlive __instance, out bool __state)
        {
            // Merken, ob der Zombie VOR diesem Aufruf bereits tot war (!IsAlive bedeutet tot)
            __state = !__instance.IsAlive();
        }

        // Wir fangen das __state Ergebnis aus dem Prefix hier auf
        [HarmonyPostfix]
        public static void Postfix(EntityAlive __instance, bool __state)
        {
            // ENGINE-QUIRK FIX: Wenn der Zombie vorher schon tot war -> SOFORT ABBRECHEN!
            // Das verhindert, dass Ragdoll- oder Fallschaden die Belohnungen doppelt triggern.
            if (__state) return;

            // Abbruch, wenn der taktische Kill in der Config deaktiviert ist
            if (!ModEinstellungen.TaktischerKillAktiv) return;

            // Prüfen: Ist es überhaupt ein Zombie? (Egal ob Biom, POI oder geladen)
            if (__instance is EntityEnemy || __instance is EntityZombie)
            {
                // 1. Chunk-Koordinaten des Todesortes berechnen
                Vector3 todesPos = __instance.position;
                int tCx = Utils.Fastfloor(todesPos.x / 16f);
                int tCz = Utils.Fastfloor(todesPos.z / 16f);
                string todesChunkId = $"{tCx}_{tCz}";

                string ursprungsChunk;

                // 2. Woher kommt der Zombie?
                if (ChunkDatenbank.ZombieUrsprung.TryGetValue(__instance.entityId, out ursprungsChunk))
                {
                    // Er stammt aus unserem regulären Biom-Spawn -> Aus dem RAM löschen
                    ChunkDatenbank.ZombieUrsprung.Remove(__instance.entityId);
                }
                else
                {
                    // Er ist ein POI-Zombie, ein geladener Zombie oder Blutmond-Zombie
                    // -> Wir deklarieren seinen Todesort zu seiner Heimat.
                    ursprungsChunk = todesChunkId;
                }

                // 3. REGEL 1: Den regulären Kill IMMER im Ursprungs-Chunk verbuchen
                ChunkDatenbank.AddToterZombieNachID(ursprungsChunk, 1);

                // ---------------------------------------------------------
                // GLOBALE PRÜFUNG: Ist exakt DIESER Chunk jetzt feindfrei?
                // ---------------------------------------------------------
                float centerX = (tCx * 16f) + 8f;
                float centerZ = (tCz * 16f) + 8f;
                Bounds todesBounds = new Bounds(new Vector3(centerX, 128f, centerZ), new Vector3(16f, 256f, 16f));

                List<EntityAlive> lebendeEntitaeten = GameManager.Instance.World.GetLivingEntitiesInBounds(__instance, todesBounds);
                if (lebendeEntitaeten != null)
                {
                    foreach (EntityAlive ent in lebendeEntitaeten)
                    {
                        if ((ent is EntityEnemy || ent is EntityZombie) && ent.IsAlive())
                        {
                            // REGEL 2: Wenn noch ein Feind in diesem Chunk steht -> Sofortiger Abbruch!
                            // Der Kill zählt somit NUR am Ursprungsort.
                            return;
                        }
                    }
                }

                // REGEL 3: Ab hier ist sicher: Der Todes-Chunk ist zu 100% leergeräumt!
                // Der Bonus-Kill (die Flächensäuberung) wird jetzt verteilt.
                // ---------------------------------------------------------

                if (ursprungsChunk == todesChunkId)
                {
                    // ---------------------------------------------------------
                    // SZENARIO A: Zombie (Biom oder POI) stirbt restlos in seiner Heimat
                    // -> Wir prüfen die 8 Nachbarn und schenken dem Spieler einen
                    // ---------------------------------------------------------
                    int[][] nachbarnOffsets = new int[][]
                    {
                        new int[] {-1, -1}, new int[] {0, -1}, new int[] {1, -1},
                        new int[] {-1, 0},                     new int[] {1, 0},
                        new int[] {-1, 1},  new int[] {0, 1},  new int[] {1, 1}
                    };

                    foreach (var offset in nachbarnOffsets)
                    {
                        int nX = tCx + offset[0];
                        int nZ = tCz + offset[1];
                        string nachbarId = $"{nX}_{nZ}";

                        // Hat der Nachbar-Chunk schon eine Historie?
                        if (ChunkDatenbank.ToteZombiesProChunk.ContainsKey(nachbarId) && ChunkDatenbank.ToteZombiesProChunk[nachbarId] >= 1)
                        {
                            continue; // nächstes Element von foreach
                        }

                        // Bounds für DIESEN Nachbar-Chunk bauen
                        float nCenterX = (nX * 16f) + 8f;
                        float nCenterZ = (nZ * 16f) + 8f;
                        Bounds nachbarBounds = new Bounds(new Vector3(nCenterX, 128f, nCenterZ), new Vector3(16f, 256f, 16f));

                        List<EntityAlive> lebendeNachbarn = GameManager.Instance.World.GetLivingEntitiesInBounds(__instance, nachbarBounds);
                        bool hatAktiveFeinde = false;

                        // lebt noch wer im Chunk?
                        if (lebendeNachbarn != null)
                        {
                            foreach (EntityAlive ent in lebendeNachbarn)
                            {
                                if ((ent is EntityEnemy || ent is EntityZombie) && ent.IsAlive())
                                {
                                    hatAktiveFeinde = true;
                                    break; // Abbruch der Schleife
                                }
                            }
                        }

                        if (!hatAktiveFeinde)
                        {
                            // Die Datenbank übernimmt jetzt das Speichern, die Map und den Chat
                            ChunkDatenbank.VerbucheTaktischenKill(nachbarId, true);

                            return; // Nachbar belohnt -> Fertig!
                        }
                    }

                    // FALLBACK SZENARIO A: Kein leerer Nachbar gefunden.
                    // Todes-Chunk bekommt den Bonus-Kill (geht somit z. B. von 0 auf 2)
                    ChunkDatenbank.ToteZombiesProChunk[todesChunkId]++;
                }
                else
                {
                    // ---------------------------------------------------------
                    // SZENARIO B: Gekitet! Zombie stirbt restlos in einem FREMDEN Chunk
                    // -> Der Todes-Chunk bekommt den Bonus-Kill.
                    // ---------------------------------------------------------
                    if (!ChunkDatenbank.ToteZombiesProChunk.ContainsKey(todesChunkId) || ChunkDatenbank.ToteZombiesProChunk[todesChunkId] < 1)
                    {
                        // Die Datenbank übernimmt das Setzen auf 1, Map-Update und den Chat
                        ChunkDatenbank.VerbucheTaktischenKill(todesChunkId, false);
                    }
                    else
                    {
                        // Chunk war ohnehin schon clear -> Er bekommt einfach den Bonus-Kill addiert
                        ChunkDatenbank.ToteZombiesProChunk[todesChunkId]++;
                    }
                }
            }
        }
    }
}