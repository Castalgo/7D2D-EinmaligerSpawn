using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using HarmonyLib;
using UnityEngine;

namespace EinmaligerSpawn.BugFixes
{
    // =========================================================================================
    // AUTOMATISCHER VANILLA-GLITCH-FIX (SZENARIO: POI Zombie fällt aus der Map)
    // Repariert blockierte POI-Räume, wenn Zombies durch den Boden in die Unendlichkeit fallen
    // =========================================================================================
    [HarmonyPatch(typeof(Entity), "OnUpdatePosition")]
    public class Entity_OnUpdatePosition_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Entity __instance)
        {
            // Nur auf dem Server ausführen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            // 1. Vanilla-Bedingung: Objekt fällt gerade aus der Welt (Y < 0)
            if (!__instance.isEntityRemote && !__instance.IsDead() && !__instance.IsClientControlled() && __instance.position.y < 0f && __instance.IsDeadIfOutOfWorld())
            {
                // 2. Ist es ein Zombie/Feind?
                if (__instance is EntityAlive entityAlive && (entityAlive is EntityEnemy || entityAlive is EntityZombie))
                {
                    DynamicPrefabDecorator decorator = GameManager.Instance.GetDynamicPrefabDecorator();
                    if (decorator == null) return;

                    List<PrefabInstance> allPois = new List<PrefabInstance>();
                    decorator.GetPOIPrefabs(allPois);

                    // 3. Suche den POI-Raum, der diese einzigartige Entity-ID gespawnt hat
                    foreach (PrefabInstance poi in allPois)
                    {
                        if (poi.sleeperVolumes == null) continue;

                        foreach (SleeperVolume vol in poi.sleeperVolumes)
                        {
                            // Zugriff auf die private Liste des Vanilla-Raums
                            Traverse volumeTraverse = Traverse.Create(vol);
                            List<int> spawnedList = volumeTraverse.Field("entityIdList").GetValue<List<int>>();

                            if (spawnedList != null && spawnedList.Contains(__instance.entityId))
                            {
                                Log.Warning($"[EinmaligerSpawn] GLITCH ERKANNT: Zombie '{entityAlive.EntityName}' (ID: {__instance.entityId}) fiel im POI '{poi.name}' durch die Welt! Heile SleeperVolume für chirurgischen Respawn...");

                                // 4. Die Reparatur: Wir löschen die ID von der internen Vanilla-Liste
                                spawnedList.Remove(__instance.entityId);

                                // Wir zwingen den Raum zurück in den aktiven Such-Modus
                                volumeTraverse.Field("wasCleared").SetValue(false);

                                // 5. Müllabfuhr in unserer Mod
                                if (KillCounter.ZombieUrsprung.ContainsKey(__instance.entityId))
                                {
                                    KillCounter.ZombieUrsprung.Remove(__instance.entityId);
                                }

                                // Wir haben den Raum gefunden und repariert, Suche abbrechen!
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}