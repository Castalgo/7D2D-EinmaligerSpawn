// Wie die POI-Spawns orgnaisiert sind:
// Ebene 1: Das Gebäude (der POI): besitzt Liste mit sleeperVolumes
// Ebene 2: Die einzelnen Räume (die SleeperVolumes): nutzt groupCountList als Spawnziel (wie lange er spawnen soll), nutzt numSpawned um Spawns zu zählen
// gespawnte Zombies landen in respawnMap
// wenn die respawnMap leer ist und numSpawned >= groupCountList ist, wird der Raum als "ausgerottet" markiert

using HarmonyLib;

namespace EinmaligerSpawn.SpawnBlocker
{
    [HarmonyPatch(typeof(SleeperVolume), "Reset")]
    public class SleeperVolume_Reset_Patch
    {
        // 1. PREFIX: Läuft VOR dem Vanilla-Reset
        [HarmonyPrefix]
        public static void Prefix(SleeperVolume __instance, out int __state)
        {
            // Vanilla-Werte auslesen
            int gespawnt = __instance.numSpawned;
            int nochAmLeben = __instance.respawnMap != null ? __instance.respawnMap.Count : 0;

            // Echte Kills berechnen
            int echteKills = gespawnt - nochAmLeben;

            // Sicherheits-Check: Falls Vanilla-Bugs auftreten, gehen wir nicht ins Minus
            if (echteKills < 0) echteKills = 0;

            // Den berechneten Kill-Wert für den Postfix zwischenspeichern
            __state = echteKills;
        }

        // 2. POSTFIX: Läuft direkt NACH dem Vanilla-Reset
        [HarmonyPostfix]
        public static void Postfix(SleeperVolume __instance, int __state)
        {
            // Vanilla hat den Raum jetzt komplett resettet.
            // numSpawned ist 0 und die respawnMap ist leer.

            if (__state > 0)
            {
                // Wir unterschieben der Engine unsere ausgerechneten Kills.
                // Vanilla denkt nun, es hätte diesen Teil der Arbeit bereits erledigt.
                __instance.numSpawned = __state;
            }
        }
    }
}