using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;

namespace EinmaligerSpawn.KartenOverlayManager
{
    // ==================================================================================
    // STEUERUNG & BERECHNUNG
    // ==================================================================================
    public static class KartenOverlay
    {
        public static bool IstAktiv { get; private set; } = false;
        public static XUiC_MapArea AktuelleMapArea { get; set; } = null;

        // --- NEU: Tracking für die gelben Chunks ---
        private static HashSet<string> knownClearedChunks = new HashSet<string>();
        public static Dictionary<string, float> NeuGeclearteChunks = new Dictionary<string, float>();
        private static bool isInitialized = false;
        private static float nextUpdate = 0f;

        // Der lokale Hintergrund-Tracker
        public static void OnGameUpdate()
        {
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + 1f;

            if (GameManager.Instance == null || GameManager.Instance.World == null) return;

            bool needsRedraw = false;

            // 1. Bereinigung: Falls Chunks per Admin-Cheat 'reset' zurückgesetzt wurden
            if (knownClearedChunks.Count > KillCounter.ToteZombiesProChunk.Count)
            {
                List<string> removed = new List<string>();
                foreach (string key in knownClearedChunks)
                {
                    if (!KillCounter.ToteZombiesProChunk.ContainsKey(key) || KillCounter.ToteZombiesProChunk[key] < 1)
                    {
                        removed.Add(key);
                    }
                }
                foreach (string key in removed)
                {
                    knownClearedChunks.Remove(key);
                    NeuGeclearteChunks.Remove(key);
                    needsRedraw = true; // Karte aktualisieren, da der Chunk wieder unsichtbar wird
                }
            }

            // 2. Suche nach NEUEN Chunks
            foreach (var kvp in KillCounter.ToteZombiesProChunk)
            {
                if (kvp.Value >= 1 && !knownClearedChunks.Contains(kvp.Key))
                {
                    knownClearedChunks.Add(kvp.Key);

                    // Nur gelb färben, wenn das Spiel bereits initial geladen wurde (verhindert komplett gelbe Map beim Login)
                    if (isInitialized)
                    {
                        NeuGeclearteChunks[kvp.Key] = Time.time;
                    }
                }
            }
            isInitialized = true;

            // 3. Suche nach ABGELAUFENEN Chunks (20 Sekunden)
            List<string> expired = new List<string>();
            foreach (var kvp in NeuGeclearteChunks)
            {
                if (Time.time - kvp.Value >= 20f)
                {
                    expired.Add(kvp.Key);
                    needsRedraw = true; // Zwingt die Karte, den Chunk nun grün zu zeichnen
                }
            }

            // Löschen der abgelaufenen Chunks
            foreach (string key in expired)
            {
                NeuGeclearteChunks.Remove(key);
            }

            // Map Redraw feuern, wenn sich an den Farben etwas geändert hat
            if (needsRedraw && IstAktiv && AktuelleMapArea != null)
            {
                ErzwingeRedraw();
            }
        }

        // Leert das Gedächtnis beim Verlassen des Spiels
        public static void Reset()
        {
            knownClearedChunks.Clear();
            NeuGeclearteChunks.Clear();
            isInitialized = false;
        }

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
                AktuelleMapArea.bShouldRedrawMap = true;
            }
        }

        // Berechnet den globalen Fortschritt: Anzahl der gesperrten Chunks / Gesamtanzahl der Chunks
        public static (int gesperrt, int gesamt, string prozentString) BerechneGlobalenFortschritt()
        {
            // Kartengröße der Welt auslesen (z.B. 6144)
            int worldSize = GamePrefs.GetInt(EnumGamePrefs.WorldGenSize);

            // Die Kantenlänge in Chunks + dein definierter Puffer von 2 Chunks (1 pro Seite)
            int chunksProSeite = (worldSize / 16) + 2;
            int y_Gesamt = chunksProSeite * chunksProSeite;
            int x_Gesperrt = 0;

            // Wir greifen über KillCounter auf das Dictionary zu
            foreach (var kvp in KillCounter.ToteZombiesProChunk)
            {
                if (kvp.Value >= 1)
                {
                    x_Gesperrt++;
                }
            }

            // Prozentwert berechnen und mit 2 Nachkommastellen als String formatieren
            float prozentFloat = y_Gesamt > 0 ? ((float)x_Gesperrt / y_Gesamt) * 100f : 0f;
            string prozentString = prozentFloat.ToString("0.00");

            return (x_Gesperrt, y_Gesamt, prozentString);
        }
    }

    // ==================================================================================
    // DER UI-ZEICHNER: Malt die globale Prozentzahl auf den Bildschirm
    // ==================================================================================
    public class MapGlobalProgressOverlay : MonoBehaviour
    {
        private GUIStyle style;
        private string cachedText = "";
        private float nextUpdate = 0f;

        void Update()
        {
            // Performance-Schutz: Wir berechnen die Chunks nur 1x pro Sekunde
            if (Time.time > nextUpdate)
            {
                nextUpdate = Time.time + 1f;
                // Ruft die Methode nun lokal aus der eigenen Klasse auf
                var ergebnis = KartenOverlay.BerechneGlobalenFortschritt();
                cachedText = $"Welt-Eroberung: {ergebnis.prozentString}% ({ergebnis.gesperrt:N0} / {ergebnis.gesamt:N0} Chunks)";
            }
        }

        // Zeichnung der Welt-Eroberung: {Prozente} {gecleart} / {Gesamt}
        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.LowerCenter
                };
            }

            // Schriftzug der Welt-Eroberung etwas verschieben:
            int paddingBottom = 108;          // Deutlich nach oben (rein in den schwarzen Balken)

            // 1. Schatten (Y-Position + 2 Pixel für den Versatz)
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height - paddingBottom + 2), cachedText, style);

            // 2. Grüner Text
            style.normal.textColor = new Color(0.2f, 1f, 0.2f);
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height - paddingBottom), cachedText, style);
        }
    }

    // ==================================================================================
    // DER HARMONY PATCH: Eingriff in die Map & Textur
    // ==================================================================================
    [HarmonyPatch(typeof(XUiC_MapArea))]
    public class KartenOverlayPatch
    {
        [HarmonyPatch("OnOpen")]
        [HarmonyPostfix]
        public static void OnOpenPostfix(XUiC_MapArea __instance)
        {
            KartenOverlay.AktuelleMapArea = __instance;

            // Hängt unseren Text-Zeichner an das Map-Fenster, sobald es geöffnet wird
            if (__instance.ViewComponent != null && __instance.ViewComponent.UiTransform != null)
            {
                var go = __instance.ViewComponent.UiTransform.gameObject;
                if (go.GetComponent<MapGlobalProgressOverlay>() == null)
                {
                    go.AddComponent<MapGlobalProgressOverlay>();
                }
            }
        }

        [HarmonyPatch("OnClose")]
        [HarmonyPostfix]
        public static void OnClosePostfix()
        {
            // Entfernt den Text-Zeichner sauber aus dem Speicher, wenn die Map zugeht
            if (KartenOverlay.AktuelleMapArea != null && KartenOverlay.AktuelleMapArea.ViewComponent != null)
            {
                var go = KartenOverlay.AktuelleMapArea.ViewComponent.UiTransform.gameObject;
                var overlay = go.GetComponent<MapGlobalProgressOverlay>();
                if (overlay != null)
                {
                    UnityEngine.Object.Destroy(overlay);
                }
            }

            KartenOverlay.AktuelleMapArea = null;
        }

        // Der eigentliche Patch, der die Map-Textur nachträglich einfärbt
        [HarmonyPatch("updateMapSection")]
        [HarmonyPostfix]
        public static void UpdateMapSectionPostfix(
            XUiC_MapArea __instance,
            int mapStartX, int mapStartZ, int mapEndX, int mapEndZ,
            int drawnMapStartX, int drawnMapStartZ, int drawnMapEndX, int drawnMapEndZ)
        {
            if (!KartenOverlay.IstAktiv) return;

            NativeArray<Color32> rawTextureData = __instance.mapTexture.GetRawTextureData<Color32>();

            int zWelt = mapStartZ;
            int zTextur = drawnMapStartZ;

            while (zWelt < mapEndZ)
            {
                int xWelt = mapStartX;
                int xTextur = drawnMapStartX;

                while (xWelt < mapEndX)
                {
                    int chunkX = World.toChunkXZ(xWelt);
                    int chunkZ = World.toChunkXZ(zWelt);
                    string chunkId = $"{chunkX}_{chunkZ}";

                    if (KillCounter.ToteZombiesProChunk.TryGetValue(chunkId, out int kills) && kills >= 1)
                    {
                        // NEU: Ist dieser Chunk frisch gecleart (und soll gelb leuchten)?
                        bool isNeuGecleart = KartenOverlay.NeuGeclearteChunks.ContainsKey(chunkId);

                        for (int pixelIndex = 0; pixelIndex < 256; pixelIndex++)
                        {
                            int pixelOffsetZ = pixelIndex / 16;
                            int pixelOffsetX = pixelIndex % 16;

                            int texZ = (zTextur + pixelOffsetZ) * 2048;
                            int texX = xTextur + pixelOffsetX;
                            int eindimensionalerIndex = texZ + texX;

                            Color32 originalPixel = rawTextureData[eindimensionalerIndex];

                            if (originalPixel.a > 0)
                            {
                                float deckkraftHintergrund = 0.55f;
                                byte r = (byte)(originalPixel.r * deckkraftHintergrund);
                                byte g = (byte)Mathf.Clamp((originalPixel.g * deckkraftHintergrund) + 80f, 0, 255);
                                byte b = (byte)(originalPixel.b * deckkraftHintergrund);

                                if (isNeuGecleart)
                                {
                                    // Gelb erzeugen: Wir heben zusätzlich zur Grün-Färbung den Rot-Kanal maximal an
                                    r = (byte)Mathf.Clamp((originalPixel.r * deckkraftHintergrund) + 120f, 0, 255);
                                }

                                rawTextureData[eindimensionalerIndex] = new Color32(r, g, b, originalPixel.a);
                            }
                        }
                    }

                    xWelt += 16;
                    xTextur = Utils.WrapIndex(xTextur + 16, 2048);
                }

                zWelt += 16;
                zTextur = Utils.WrapIndex(zTextur + 16, 2048);
            }
        }
    }
}