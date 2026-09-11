using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.PoiTracker;
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

                                // 5. Müllabfuhr in unserer Mod (Kapselung über den Manager)
                                ChunkClearManager.RemoveUrsprungsChunkLebenderZombie(__instance.entityId);

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

namespace EinmaligerSpawn.Logging
{
    // =========================================================================================
    // QUEST-START LOGGING
    // Loggt den Moment, in dem ein Spieler eine Quest am Rally Marker startet
    // =========================================================================================
    [HarmonyPatch(typeof(ObjectiveRallyPoint), "RallyPointActivate")]
    public class Quest_RallyMarker_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ObjectiveRallyPoint __instance, bool activate)
        {
            // Die Engine übergibt 'activate = false', falls die Quest blockiert ist (z. B. durch Bedrolls).
            // Wir loggen nur erfolgreiche Starts.
            if (!activate) return;

            // Die übergeordnete Quest des Rally-Markers abrufen
            Quest ownerQuest = __instance.OwnerQuest;
            if (ownerQuest == null || ownerQuest.OwnerJournal == null || ownerQuest.OwnerJournal.OwnerPlayer == null) return;

            EntityPlayer starter = ownerQuest.OwnerJournal.OwnerPlayer;

            // Gebäudedaten auslesen
            string poiName = "Unbekannter POI";
            if (ownerQuest.QuestClass != null)
            {
                poiName = ownerQuest.QuestClass.Name;
            }

            // Party-Mitglieder ermitteln
            string partyMembers = "Keine (Solo)";
            if (starter.Party != null && starter.Party.MemberList != null && starter.Party.MemberList.Count > 1)
            {
                List<string> memberNames = new List<string>();
                foreach (EntityPlayer member in starter.Party.MemberList)
                {
                    if (member.entityId != starter.entityId)
                    {
                        memberNames.Add(member.EntityName);
                    }
                }
                partyMembers = string.Join(", ", memberNames);
            }

            // Die finale Ausgabe in die Server-Konsole
            Log.Out($"[ES Debug Queststart] Spieler '{starter.EntityName}' hat die Quest '{poiName}' (Code: {ownerQuest.QuestCode}) am Marker gestartet. Aktive Party-Mitglieder: {partyMembers}");
        }
    }
}