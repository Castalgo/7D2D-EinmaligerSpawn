using System;
using System.Reflection;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.KartenOverlayManager;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;

namespace EinmaligerSpawn.Minimap_Patch
{
    public static class SimpleMinimap_Patch
    {
        // Variable: Setze diesen auf true, wenn ein MapUpdate erzwungen werden soll
        public static bool ErzwingeRedraw = false;

        // Wird von deiner Hauptklasse (IModApi) aufgerufen
        public static void VersuchePatch(Harmony harmony)
        {
            // Sicherheitsriegel 1: Wir suchen nach einer Klasse, die GANZ SICHER nur in dieser spezifischen Mod existiert.
            Type uniqueModType = AccessTools.TypeByName("SimpleMinimapInit");

            if (uniqueModType != null)
            {
                // Die Mod ist da! Jetzt holen wir uns die eigentliche UI-Klasse
                Type minimapType = AccessTools.TypeByName("XUiC_Minimap");

                if (minimapType != null)
                {
                    // Die Ziel-Methode suchen
                    MethodInfo renderOriginal = AccessTools.Method(minimapType, "RenderTerrain");

                    // Sicherheitsriegel 2: Gibt es diese Methode (noch)? (Schutz vor zukünftigen Updates der fremden Mod)
                    if (renderOriginal != null)
                    {
                        MethodInfo postfix = AccessTools.Method(typeof(SimpleMinimap_Patch), nameof(RenderTerrainPostfix));

                        // Pre–Patch manuell anwenden
                        harmony.Patch(renderOriginal, postfix: new HarmonyMethod(postfix));

                        // der Prefix zum Erzwingen des Updates
                        MethodInfo updateOriginal = AccessTools.Method(minimapType, "UpdateTerrain");
                        if (updateOriginal != null)
                        {
                            MethodInfo updatePrefix = AccessTools.Method(typeof(SimpleMinimap_Patch), nameof(UpdateTerrainPrefix));
                            harmony.Patch(updateOriginal, prefix: new HarmonyMethod(updatePrefix));
                        }

                        Log.Out("[EinmaligerSpawn] Kompatibilität: SimpleMinimapUAV gefunden und Raster erfolgreich integriert.");
                    }
                    else
                    {
                        Log.Out("[EinmaligerSpawn] Kompatibilität: SimpleMinimapUAV gefunden, aber 'RenderTerrain' fehlt (veraltete oder zu neue Version?). Patch abgebrochen.");
                    }
                }
            }
            else
            {
                Log.Out("[EinmaligerSpawn] Kompatibilität: SimpleMinimapUAV ist nicht installiert. Ignoriere Minimap-Patch.");
            }
        }

        // Prefix: Läuft jeden Frame ab, BEVOR die Minimap ihre Timer checkt
        public static void UpdateTerrainPrefix(ref bool ___texValid)
        {
            // Wenn unsere Mod einen Redraw verlangt...
            if (ErzwingeRedraw)
            {
                // ... hebeln wir die Optimierung der Minimap aus und erzwingen das Neuzeichnen
                ___texValid = false;

                // Trigger sofort wieder zurücksetzen, damit es danach wieder optimiert weiterläuft
                ErzwingeRedraw = false;
            }
        }

        // Unser Postfix, der die MapTexture abfängt, nachdem die Minimap-Mod sie generiert hat.
        // Mit ___ (drei Unterstrichen) zwingen wir Harmony, uns die privaten Variablen der Instanz zu geben.
        public static void RenderTerrainPostfix(object __instance, ref Texture2D ___mapTexture, ref int ___renderChunkX, ref int ___renderChunkZ)
        {
            // Soll das Overlay überhaupt gezeichnet werden?
            if (!KartenOverlay.IstAktiv) return;

            // Rohe Pixel-Daten der fremden Textur abrufen
            NativeArray<Color32> rawTextureData = ___mapTexture.GetRawTextureData<Color32>();

            // Die fremde Mod iteriert über 48x48 Chunks (768x768 Pixel)
            for (int chunkZ_offset = 0; chunkZ_offset < 48; chunkZ_offset++)
            {
                int worldChunkZ = ___renderChunkZ + chunkZ_offset;

                for (int chunkX_offset = 0; chunkX_offset < 48; chunkX_offset++)
                {
                    int worldChunkX = ___renderChunkX + chunkX_offset;
                    string chunkId = $"{worldChunkX}_{worldChunkZ}";

                    // Prüfen, ob unser Chunk als "gecleart" gilt
                    if (KillCounter.ToteZombiesProChunk.TryGetValue(chunkId, out int kills) && kills >= 1)
                    {
                        // NEU: Ist dieser Chunk frisch gecleart (und soll gelb/orange leuchten)?
                        bool isNeuGecleart = KartenOverlay.NeuGeclearteChunks.ContainsKey(chunkId);

                        // Start-Index für diesen Chunk im 1D-Array der Textur
                        // Formel aus der fremden Mod: i * 16 * 768 + j * 16
                        int startPixelIndex = chunkZ_offset * 16 * 768 + chunkX_offset * 16;

                        // Jeden Pixel des 16x16 Chunks übermalen
                        for (int pZ = 0; pZ < 16; pZ++)
                        {
                            int rowOffset = startPixelIndex + (pZ * 768);

                            for (int pX = 0; pX < 16; pX++)
                            {
                                int pixelIndex = rowOffset + pX;
                                Color32 originalPixel = rawTextureData[pixelIndex];

                                // 1. Transparente Pixel ignorieren
                                // 2. Den grauen "Fog of War" der fremden Mod (RGB 45, 45, 45) ignorieren!
                                if (originalPixel.a > 0 && !(originalPixel.r == 45 && originalPixel.g == 45 && originalPixel.b == 45))
                                {
                                    float deckkraft = 0.55f;
                                    byte r = (byte)(originalPixel.r * deckkraft);
                                    byte g = (byte)Mathf.Clamp((originalPixel.g * deckkraft) + 80f, 0, 255);
                                    byte b = (byte)(originalPixel.b * deckkraft);

                                    if (isNeuGecleart)
                                    {
                                        // Gelb/Orange erzeugen: Wir heben zusätzlich zur Grün-Färbung den Rot-Kanal maximal an
                                        r = (byte)Mathf.Clamp((originalPixel.r * deckkraft) + 120f, 0, 255);
                                    }

                                    rawTextureData[pixelIndex] = new Color32(r, g, b, originalPixel.a);
                                }
                            }
                        }
                    }
                }
            }

            // WICHTIG: Die Textur nach der Manipulation aktualisieren!
            ___mapTexture.Apply(false);
        }
    }
}