using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using EinmaligerSpawn.PoiTracker; // Wichtig für den Zugriff auf die PoiDatenbank

namespace EinmaligerSpawn.Trader
{
    // Wir hängen uns in den QuestEventManager, wo der Händler seine Quest-Gebäude auswählt
    [HarmonyPatch(typeof(QuestEventManager))]
    public class Trader_Quest_Filter_Patch
    {
        [HarmonyPatch("GetPrefabsForTrader")]
        [HarmonyPostfix]
        public static void Postfix(ref List<PrefabInstance> __result)
        {
            // Server only. Client rauswerfen
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            // Wenn die Liste ohnehin leer ist, müssen wir nichts tun
            if (__result == null || __result.Count == 0) return;

            // Wir bauen eine neue Liste für die gefilterten Gebäude
            List<PrefabInstance> gefilterteListe = new List<PrefabInstance>();

            // Zähler für die Zusammenfassung
            int geblockteAnzahl = 0;

            foreach (PrefabInstance haus in __result)
            {
                // Status abfragen (0 = Aktiv, 1 = 100% Gecleart, 2 = Quest-Sperre)
                byte poiStatus = PoiDatenbank.LeseStatus(haus.id);

                if (poiStatus == 0)
                {
                    // Gebäude ist völlig unberührt -> darf als Quest angeboten werden
                    gefilterteListe.Add(haus);
                }
                else
                {
                    // Status 1 (100% clear) oder 2 (Quest-Sperre) blockieren den Händler
                    geblockteAnzahl++;
                }
            }

            // Einmalige Ausgabe nach der Schleife
            if (geblockteAnzahl > 0)
            {
                Log.Out($"[EinmaligerSpawn] Händler-Filter: {geblockteAnzahl} blockierte Gebäude wurden aus der Quest-Auswahl entfernt.");
            }

            // Wir überschreiben das Ergebnis der Engine mit unserer streng gefilterten Liste
            __result = gefilterteListe;
        }
    }
}