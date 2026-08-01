using System.Reflection;
using EinmaligerSpawn.HUD;
using EinmaligerSpawn.LocalClear;
using EinmaligerSpawn.Network;
using EinmaligerSpawn.ZombieSpawner;
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

            // Dem Spiel unser neues Netzwerk-Paket offiziell bekannt machen

            // Patch für alle gemoddeten Änderungen, die auf GameUpdate angewiesen sind
            ModEvents.GameUpdate.RegisterHandler((ref ModEvents.SGameUpdateData data) =>
            {
                AutoSpawner.OnGameUpdate();
                LokalenChunkSaeubern.OnGameUpdate();
                FortschrittsBuff.OnGameUpdate();
                // KartenOverlay wird im SpeichernLaden_Patch.cs beim Welt betreten aufgerufen
            });

            Log.Out("[EinmaligerSpawn] Alle Patches erfolgreich geladen!");
        }
    }
}