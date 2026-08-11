using System.Reflection;
using EinmaligerSpawn.HUD;
using EinmaligerSpawn.LocalClear;
using EinmaligerSpawn.Minimap_Patch;
using EinmaligerSpawn.Network;
using EinmaligerSpawn.ZombieSpawner;
using EinmaligerSpawn.KartenOverlayManager; // NEU
using HarmonyLib;
using UnityEngine;

namespace EinmaligerSpawn
{
    public class EinmaligerSpawnInit : IModApi
    {
        public void InitMod(Mod mod)
        {
            Log.Out("[EinmaligerSpawn] Initialisiere Mod-Logik...");

            var harmony = new Harmony("com.castalgo.einmaligerspawn");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Minimap-Patch initialisieren
            SimpleMinimap_Patch.VersuchePatch(harmony);

            // Patch für alle gemoddeten Änderungen, die auf GameUpdate angewiesen sind
            ModEvents.GameUpdate.RegisterHandler((ref ModEvents.SGameUpdateData data) =>
            {
                AutoSpawner.OnGameUpdate();
                LokalenChunkSaeubern.OnGameUpdate();
                FortschrittsBuff.OnGameUpdate();
                KartenOverlay.OnGameUpdate(); // NEU: Der Hintergrund-Tracker für gelbe Chunks
            });

            Log.Out("[EinmaligerSpawn] Alle Patches erfolgreich geladen!");
        }
    }
}