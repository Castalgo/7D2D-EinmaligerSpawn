// Respawn von Zombies in POIs deaktivieren

using HarmonyLib;

namespace zzz_EinmaligerSpawn.Patches
{
    [HarmonyPatch(typeof(SleeperVolume))]
    public class Patch_SleeperRespawn
    {
        // Verhindert das Respawnen von Sleepern in einem POI
        [HarmonyPatch("Respawn")]
        [HarmonyPrefix]
        public static bool Prefix_Respawn()
        {
            // Debug.Log("[zzz_EinmaligerSpawn] Respawn unterdrückt.");
            return false; // überspringt die Originalfunktion → Sleeper respawnen nicht mehr
        }
    }
}