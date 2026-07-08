// Alle Entitiys (Zombies und Tiere) werden persistent, damit sie nicht despawnen beim unloaden eines Chunks.

using HarmonyLib;
using UnityEngine;

namespace EinmaligerSpawn.Modifikation
{
    /// <summary>
    /// Verhindert, dass EntityAlive-Instanzen (z. B. Zombies oder Tiere) automatisch despawnen,
    /// wenn sie sich außerhalb des aktiven Spielerbereichs befinden.
    /// </summary>
    [HarmonyPatch(typeof(EntityAlive))]
    public class PersistenteObjekte
    {
        /// <summary>
        /// Verhindert das automatische Despawnen von Zombies und Tieren.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("ShouldDespawn")]
        public static bool ShouldDespawn_Prefix(ref bool __result)
        {
            // Immer false zurückgeben = Nie despawnen
            __result = false;
            return false; // Originalmethode wird übersprungen
        }

        /// <summary>
        /// Verhindert, dass Entities beim Unload aus der Welt gelöscht werden.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(EntityAlive.OnEntityUnload))]
        public static bool OnEntityUnload_Prefix(EntityAlive __instance)
        {
            // Überspringt den Despawn-Vorgang vollständig
            return false;
        }
    }
}