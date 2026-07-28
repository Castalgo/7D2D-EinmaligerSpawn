using System.Reflection;
using EinmaligerSpawn.LootBagMarker;
using EinmaligerSpawn.ZombieSpawner;
using EinmaligerSpawn.LocalClear;
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

            // Patch für alle gemoddeten Änderungen, die auf GameUpdate angewiesen sind
            ModEvents.GameUpdate.RegisterHandler((ref ModEvents.SGameUpdateData data) =>
            {
                AutoSpawner.OnGameUpdate();
                LokalenChunkSaeubern.OnGameUpdate();
                LootbagMarkerManager.OnGameUpdate();
            });

            Log.Out("[EinmaligerSpawn] Alle Patches erfolgreich geladen!");
        }
    }
}