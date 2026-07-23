using System.Reflection;
using EinmaligerSpawn.LootBagMarker;
using EinmaligerSpawn.Manager;
using HarmonyLib;
using UnityEngine;

namespace EinmaligerSpawn
{
    public class EinmaligerSpawnInit : IModApi
    {
        public void InitMod(Mod mod)
        {
            Debug.Log("[EinmaligerSpawn] Initialisiere Mod-Logik...");

            var harmony = new Harmony("com.castalgo.einmaligerspawn");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // HIER IST DER FIX: Das Schlüsselwort "ref" wurde zum Parameter hinzugefügt
            ModEvents.GameUpdate.RegisterHandler((ref ModEvents.SGameUpdateData data) =>
            {
                AutoSpawner.OnGameUpdate();
                LokalenChunkSaeubern.OnGameUpdate();
                LootbagMarkerManager.OnGameUpdate();
            });

            Debug.Log("[EinmaligerSpawn] Alle Patches erfolgreich geladen!");
        }
    }
}