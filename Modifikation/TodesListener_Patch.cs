using System;
using System.Collections;
using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.Minimap_Patch;
using EinmaligerSpawn.Network;
using EinmaligerSpawn.PoiTracker;
using HarmonyLib;
using UnityEngine;

namespace EinmaligerSpawn.SpawnBlocker
{
    [HarmonyPatch(typeof(EntityAlive), "SetDead")]
    public class TodesListener_Patch
    {
        //  Wir schalten uns VOR die Engine-Logik
        [HarmonyPrefix]
        public static void Prefix(EntityAlive __instance, out bool __state)
        {
            // Nur Server. Clienten rauswerfen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                __state = false; // Dummy-Wert, wird nicht verwendet
                return;
            }

            // Merken, ob der Zombie VOR diesem Aufruf bereits tot war (!IsAlive bedeutet tot)
            __state = !__instance.IsAlive();
        }

        // Wir fangen das __state Ergebnis aus dem Prefix hier auf
        [HarmonyPostfix]
        public static void Postfix(EntityAlive __instance, bool __state)
        {
            // Nur Server. Clienten rauswerfen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            // ENGINE-QUIRK FIX: Wenn der Zombie vorher schon tot war -> SOFORT ABBRECHEN!
            // Das verhindert, dass Ragdoll- oder Fallschaden die Belohnungen doppelt triggern.
            if (__state) return;

            // Prüfen: Ist es überhaupt ein Zombie? (Egal ob Biom, POI oder geladen)
            if (__instance is EntityEnemy || __instance is EntityZombie)
            {
                // =========================================================
                // TEIL 1: POI-CLEAR PRÜFUNG (Neu mit Verzögerung)
                // =========================================================
                PrefabInstance poi = GameManager.Instance.World.GetPOIAtPosition(__instance.position);
                if (poi != null && poi.sleeperVolumes != null && poi.sleeperVolumes.Count > 0)
                {
                    // Startet die Prüfung losgelöst vom aktuellen Frame
                    GameManager.Instance.StartCoroutine(CheckPoiDelayed(poi));
                }

                // =========================================================
                // TEIL 2: CHUNK-CLEAR PRÜFUNG (Dein bisheriger Code)
                // =========================================================

                // Abbruch, wenn der taktische Kill in der Config deaktiviert ist
                if (!ModEinstellungen.TaktischerKillAktiv) return;

                // 1. Chunk-Koordinaten des Todesortes berechnen
                Vector3 todesPos = __instance.position;
                int tCx = Utils.Fastfloor(todesPos.x / 16f);
                int tCz = Utils.Fastfloor(todesPos.z / 16f);
                string todesChunkId = $"{tCx}_{tCz}";

                string ursprungsChunk;

                // 2. Woher kommt der Zombie?
                if (KillCounter.ZombieUrsprung.TryGetValue(__instance.entityId, out ursprungsChunk))
                {
                    // Er stammt aus unserem regulären Biom-Spawn -> Aus dem RAM löschen
                    KillCounter.ZombieUrsprung.Remove(__instance.entityId);
                }
                else
                {
                    // Er ist ein POI-Zombie, ein geladener Zombie oder Blutmond-Zombie
                    // -> Wir deklarieren seinen Todesort zu seiner Heimat.
                    ursprungsChunk = todesChunkId;
                }

                // 3. REGEL 1: Den regulären Kill IMMER im Ursprungs-Chunk verbuchen
                KillCounter.AddToterZombieNachID(ursprungsChunk, 1);

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
                        if (KillCounter.ToteZombiesProChunk.ContainsKey(nachbarId) && KillCounter.ToteZombiesProChunk[nachbarId] >= 1)
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
                            // Die Datenbank übernimmt jetzt das Speichern und die Map
                            KillCounter.VerbucheTaktischenKill(nachbarId, true);

                            return; // Nachbar belohnt -> Fertig!
                        }
                    }

                    // FALLBACK SZENARIO A: Kein leerer Nachbar gefunden.
                    // Todes-Chunk bekommt den Bonus-Kill (geht somit z. B. von 0 auf 2)
                    KillCounter.ToteZombiesProChunk[todesChunkId]++;
                }
                else
                {
                    // ---------------------------------------------------------
                    // SZENARIO B: Gekitet! Zombie stirbt restlos in einem FREMDEN Chunk
                    // -> Der Todes-Chunk bekommt den Bonus-Kill.
                    // ---------------------------------------------------------
                    if (!KillCounter.ToteZombiesProChunk.ContainsKey(todesChunkId) || KillCounter.ToteZombiesProChunk[todesChunkId] < 1)
                    {
                        // Die Datenbank übernimmt das Setzen auf 1 und das Map-Update
                        KillCounter.VerbucheTaktischenKill(todesChunkId, false);
                    }
                    else
                    {
                        // Chunk war ohnehin schon clear -> Er bekommt einfach den Bonus-Kill addiert
                        KillCounter.ToteZombiesProChunk[todesChunkId]++;
                    }
                }
            }
        }

        // Die ausgelagerte asynchrone Prüfung
        private static IEnumerator CheckPoiDelayed(PrefabInstance poi)
        {
            // Gibt der Engine 1 Sekunde Zeit, um den Tod des Zombies zu verarbeiten und das Flag zu setzen
            yield return new WaitForSeconds(1.0f);

            // Abbruch, falls das Gebäude in der Zwischenzeit durch einen anderen Kill-Thread bereits gesichert wurde
            if (PoiDatenbank.IstGecleart(poi.id)) yield break;

            bool istKomplettLeer = true;
            foreach (SleeperVolume volumen in poi.sleeperVolumes)
            {
                if (!volumen.wasCleared)
                {
                    istKomplettLeer = false;
                    break;
                }
            }

            if (istKomplettLeer)
            {
                PoiDatenbank.SetzeGecleart(poi.id);
                Log.Warning($"[EinmaligerSpawn] POI '{poi.name}' (ID: {poi.id}) wurde restlos gesäubert!");

                // Chatnachricht im Einzelspieler und für den Host im Multiplayer
                if (!GameManager.IsDedicatedServer && ModEinstellungen.ChatNachrichtenAktiv)
                {
                    ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                    string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";

                    // Passe 'chunkId' an den Namen der Variable an, die du in der Methode für die Chunk-Koordinaten nutzt
                    string feedbackMsg = $"[00FF00][{timeString}] POI {poi.name} wurde restlos gesäubert![-]";

                    GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                }

                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(poi.id));
                }

                // erzwingt Minimap Update, sofern Minimap Mod aktiv
                SimpleMinimap_Patch.ErzwingeRedraw = true;
            }
        }
    }
}