using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace EinmaligerSpawn.Patches
{
    // Wir hängen uns in den QuestEventManager, wo der Händler seine Quest-Gebäude auswählt
    [HarmonyPatch(typeof(QuestEventManager))]
    public class Trader_Quest_Filter_Patch
    {
        [HarmonyPatch("GetPrefabsForTrader")]
        [HarmonyPostfix]
        public static void Postfix(ref List<PrefabInstance> __result)
        {
            // Wenn die Liste ohnehin leer ist, müssen wir nichts tun
            if (__result == null || __result.Count == 0) return;

            // Wir bauen eine neue Liste für die gefilterten Gebäude
            List<PrefabInstance> gefilterteListe = new List<PrefabInstance>();

            foreach (PrefabInstance haus in __result)
            {
                bool istKomplettLeer = true;

                // Wir prüfen alle Sleeper-Volumen in diesem Haus
                foreach (SleeperVolume volumen in haus.sleeperVolumes)
                {
                    // Sobald wir auch nur ein EINZIGES Volumen finden, das noch NICHT gesäubert wurde...
                    if (!volumen.wasCleared)
                    {
                        // ... wissen wir: In diesem Haus gibt es noch Feinde.
                        istKomplettLeer = false;
                        break;
                    }
                }

                // Wenn das Haus nicht komplett leer ist (oder gar keine Sleeper hatte), darf es eine Quest werden!
                if (!istKomplettLeer || haus.sleeperVolumes.Count == 0)
                {
                    gefilterteListe.Add(haus);
                }
                else
                {
                    Debug.Log($"[EinmaligerSpawn] Händler-Filter: Das Gebäude '{haus.name}' wurde bereits ausgerottet und wird nicht mehr als Quest angeboten.");
                }
            }

            // Wir überschreiben das Ergebnis der Engine mit unserer streng gefilterten Liste
            __result = gefilterteListe;
        }
    }
}