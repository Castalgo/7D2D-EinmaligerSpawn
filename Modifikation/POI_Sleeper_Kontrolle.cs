using HarmonyLib;

namespace EinmaligerSpawn.Patches
{
    [HarmonyPatch(typeof(SleeperVolume))]
    public class POI_Sleeper_Kontrolle
    {
        // Wir blockieren die neue Reset-Methode, die für den Respawn zuständig ist
        [HarmonyPatch("Reset")]
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // Verhindert, dass das Spiel ein gesäubertes Gebäude (SleeperVolume) 
            // nach Ablauf des Respawn-Timers wieder zurücksetzt.
            // Das Gebäude bleibt dauerhaft leer.
            return false;
        }
    }
}