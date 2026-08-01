using HarmonyLib;

namespace EinmaligerSpawn.SpawnBlocker
{
    [HarmonyPatch(typeof(SleeperVolume))]
    public class POI_Sleeper_Kontrolle
    {
        // Wir blockieren die neue Reset-Methode, die für den Respawn zuständig ist
        [HarmonyPatch("Reset")]
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // Verhindert, dass das Spiel ein gesäubertes Gebäude (SleeperVolume) nach Ablauf des Respawn-Timers wieder zurücksetzt.
            // Das Gebäude bleibt dauerhaft leer.
            // Methode darf von Server und Client aufgerufen werden, weil sie außer alles ablehnen nichts tut.
            return false;
        }
    }
}