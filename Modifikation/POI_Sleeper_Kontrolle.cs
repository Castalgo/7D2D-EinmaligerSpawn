using HarmonyLib;
using EinmaligerSpawn.PoiTracker;

namespace EinmaligerSpawn.SpawnBlocker
{
    [HarmonyPatch(typeof(SleeperVolume), "Reset")]
    public class POI_Sleeper_Kontrolle
    {
        // 1. PREFIX: Läuft VOR der Vanilla-Methode
        [HarmonyPrefix]
        public static bool Prefix(SleeperVolume __instance, out int __state)
        {
            // Wir sichern den aktuellen Fortschritt in der __state Variable
            __state = __instance.numSpawned;

            // Gehört das Volume zu einem gültigen POI?
            if (__instance.prefabInstance != null)
            {
                // Ist das komplette Gebäude laut Datenbank tot?
                if (PoiDatenbank.IstGecleart(__instance.prefabInstance.id))
                {
                    return false; // Reset hart blockieren. POI bleibt leer.
                }
            }

            // Ist dieser spezifische Raum bereits zu 100% gesäubert?
            if (__instance.wasCleared)
            {
                return false; // Reset hart blockieren. Raum bleibt leer.
            }

            // FÜR ALLE ANDEREN RÄUME (auch die verklemmten): 
            // Wir lassen den Reset zu, damit sich die Engine entbuggen kann!
            return true;
        }

        // 2. POSTFIX: Läuft direkt NACH der Vanilla-Methode
        [HarmonyPostfix]
        public static void Postfix(SleeperVolume __instance, int __state)
        {
            // Vanilla hat den Raum jetzt erfolgreich repariert und resettet.
            // Dabei hat Vanilla aber auch numSpawned auf 0 gesetzt.

            // Wenn in diesem Raum vorher schon Zombies gespawnt waren...
            if (__state > 0)
            {
                // ... überschreiben wir die 0 einfach wieder mit unserem gesicherten Wert!
                __instance.numSpawned = __state;
            }
        }
    }
}