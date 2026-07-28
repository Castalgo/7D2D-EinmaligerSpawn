using HarmonyLib;
using Unity.Collections;
using UnityEngine;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.ChunkDatenbank;

namespace EinmaligerSpawn.KartenOverlayManager
{
    // ==================================================================================
    // STEUERUNG (Ersetzt die alte Logik)
    // ==================================================================================
    public static class KartenOverlay
    {
        public static bool IstAktiv { get; private set; } = false;

        // Speichert die Referenz auf die aktuell offene Karte, um schnelle Redraws zu erzwingen
        public static XUiC_MapArea AktuelleMapArea { get; set; } = null;

        public static void SetzeModus(bool aktiv)
        {
            if (IstAktiv == aktiv) return;

            IstAktiv = aktiv;
            ModEinstellungen.KartenOverlayAktiv = IstAktiv;
            ModEinstellungen.Speichern();

            ErzwingeRedraw();
            Log.Out($"[EinmaligerSpawn] Karten-Overlay {(aktiv ? "AKTIVIERT" : "DEAKTIVIERT")}.");
        }

        public static void Reload()
        {
            ErzwingeRedraw();
            Log.Out("[EinmaligerSpawn] Karten-Redraw erzwungen.");
        }

        public static void Wiederherstellen()
        {
            IstAktiv = ModEinstellungen.KartenOverlayAktiv;
        }

        public static void ErzwingeRedraw()
        {
            if (AktuelleMapArea != null)
            {
                // Setzt bShouldRedrawMap auf true. Die Engine baut die Karte im nächsten Frame neu auf.
                AktuelleMapArea.bShouldRedrawMap = true;
            }
        }
    }

    // ==================================================================================
    // DER HARMONY PATCH: Direkter Eingriff in den rohen Byte-Speicher der Landkarte
    // ==================================================================================
    [HarmonyPatch(typeof(XUiC_MapArea))]
    public class KartenOverlayPatch
    {
        // Wir schnappen uns die Instanz, wenn die Karte geöffnet wird
        [HarmonyPatch("OnOpen")]
        [HarmonyPostfix]
        public static void OnOpenPostfix(XUiC_MapArea __instance)
        {
            KartenOverlay.AktuelleMapArea = __instance;
        }

        [HarmonyPatch("OnClose")]
        [HarmonyPostfix]
        public static void OnClosePostfix()
        {
            KartenOverlay.AktuelleMapArea = null;
        }

        // Wir klinken uns exakt NACH der Render-Berechnung der Engine ein
        [HarmonyPatch("updateMapSection")]
        [HarmonyPostfix]
        public static void UpdateMapSectionPostfix(
            XUiC_MapArea __instance,
            int mapStartX, int mapStartZ, int mapEndX, int mapEndZ,
            int drawnMapStartX, int drawnMapStartZ, int drawnMapEndX, int drawnMapEndZ)
        {
            // Ist der Schalter aus, greifen wir nicht ein. Die Karte bleibt Vanilla.
            if (!KartenOverlay.IstAktiv) return;

            // Direkter Zugriff auf das 2048x2048 Pixel-Array im Arbeitsspeicher
            NativeArray<Color32> rawTextureData = __instance.mapTexture.GetRawTextureData<Color32>();

            int zWelt = mapStartZ;
            int zTextur = drawnMapStartZ;

            // Wir durchlaufen das exakte Raster der Engine
            while (zWelt < mapEndZ)
            {
                int xWelt = mapStartX;
                int xTextur = drawnMapStartX;

                while (xWelt < mapEndX)
                {
                    // 1. Chunk-Koordinaten ermitteln
                    int chunkX = World.toChunkXZ(xWelt);
                    int chunkZ = World.toChunkXZ(zWelt);
                    string chunkId = $"{chunkX}_{chunkZ}";

                    // 2. Prüfen, ob dieser Chunk als gesäubert gilt
                    if (KillCounter.ToteZombiesProChunk.TryGetValue(chunkId, out int kills) && kills >= 1)
                    {
                        // 3. Wenn ja: Färbe die 16x16 Pixel (256 Pixel insgesamt) dieses Chunks ein
                        for (int pixelIndex = 0; pixelIndex < 256; pixelIndex++)
                        {
                            int pixelOffsetZ = pixelIndex / 16;
                            int pixelOffsetX = pixelIndex % 16;

                            // Die exakte 1D-Array-Berechnung der Engine für die 2048x2048 Textur
                            int texZ = (zTextur + pixelOffsetZ) * 2048;
                            int texX = xTextur + pixelOffsetX;
                            int eindimensionalerIndex = texZ + texX;

                            Color32 originalPixel = rawTextureData[eindimensionalerIndex];

                            // Die Engine setzt unentdeckte Gebiete (Fog of War) auf Alpha 0 (komplett transparent).
                            // Wir färben nur Pixel ein, die der Spieler auch tatsächlich aufgedeckt hat.
                            if (originalPixel.a > 0)
                            {
                                // x% der originalen Karte bleiben sichtbar (bessere Durchsichtigkeit)
                                float deckkraftHintergrund = 0.55f;

                                // Wir verdunkeln das Originalbild leicht und addieren einen satten Grün-Ton
                                byte r = (byte)(originalPixel.r * deckkraftHintergrund);
                                byte g = (byte)Mathf.Clamp((originalPixel.g * deckkraftHintergrund) + 80f, 0, 255);
                                byte b = (byte)(originalPixel.b * deckkraftHintergrund);

                                rawTextureData[eindimensionalerIndex] = new Color32(r, g, b, originalPixel.a);
                            }
                        }
                    }

                    // Engine-Sprung zum nächsten Chunk auf der X-Achse (WrapIndex sichert das Endlos-Scrollen)
                    xWelt += 16;
                    xTextur = Utils.WrapIndex(xTextur + 16, 2048);
                }

                // Engine-Sprung zum nächsten Chunk auf der Z-Achse
                zWelt += 16;
                zTextur = Utils.WrapIndex(zTextur + 16, 2048);
            }

            // HINWEIS: Wir müssen hier KEIN mapTexture.Apply() aufrufen! 
            // Die Engine erledigt das völlig automatisch in updateFullMap() und updateMapForScroll(), 
            // kurz nachdem unser Postfix durchgelaufen ist.
        }
    }
}