using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace EinmaligerSpawn.Config
{
    public static class ModEinstellungen
    {
        public static float BuffUpdateIntervall = 2f;
        public static bool ChatNachrichtenAktiv = true;
        public static int GlobalesZombieLimit = 18;
        public static bool GlobalScanAbgeschlossen = false;
        public static bool KartenOverlayAktiv = true;
        public static bool LokalerChunkClearAktiv = true;
        public static int ProgressBuffRadius = 120;
        public static float SpawnCheckIntervall = 5f;
        public static bool TaktischerKillAktiv = true;
        public static bool ZeigeLokalenFortschritt = true;

        public static void Laden(string saveDir)
        {
            // Prüft zuverlässig und völlig unabhängig von anwesenden Spielern, 
            // ob dies ein reiner Dedicated Server (ohne UI) ist.
            bool isDedicated = GameManager.IsDedicatedServer;

            string configPfad = Path.Combine(saveDir, "EinmaligerSpawn_Config.json");
            if (File.Exists(configPfad))
            {
                try
                {
                    string json = File.ReadAllText(configPfad);
                    var config = JsonConvert.DeserializeObject<ConfigDaten>(json);
                    if (config != null)
                    {
                        BuffUpdateIntervall = config.BuffUpdateIntervall;
                        GlobalesZombieLimit = config.GlobalesZombieLimit;
                        GlobalScanAbgeschlossen = config.GlobalScanAbgeschlossen;
                        LokalerChunkClearAktiv = config.LokalerChunkClearAktiv;
                        ProgressBuffRadius = config.ProgressBuffRadius;
                        SpawnCheckIntervall = config.SpawnCheckIntervall;
                        TaktischerKillAktiv = config.TaktischerKillAktiv;

                        // Optische Features & UI werden bei reinen Servern immer zwangsweise deaktiviert
                        ChatNachrichtenAktiv = isDedicated ? false : config.ChatNachrichtenAktiv;
                        KartenOverlayAktiv = isDedicated ? false : config.KartenOverlayAktiv;
                        ZeigeLokalenFortschritt = isDedicated ? false : config.ZeigeLokalenFortschritt;
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"[EinmaligerSpawn] Fehler beim Laden der lokalen Config: {e.Message}");
                }
            }
            else
            {
                // Standardwerte, falls noch keine Config existiert
                BuffUpdateIntervall = 2f;
                GlobalesZombieLimit = 18;
                GlobalScanAbgeschlossen = false;
                LokalerChunkClearAktiv = true;
                ProgressBuffRadius = 120;
                SpawnCheckIntervall = 5f;
                TaktischerKillAktiv = true;

                // Per Default deaktiviert, wenn es ein reiner Server ist
                ChatNachrichtenAktiv = !isDedicated;
                KartenOverlayAktiv = !isDedicated;
                ZeigeLokalenFortschritt = !isDedicated;
            }
        }

        public static void Speichern()
        {
            string saveDir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(saveDir)) return;

            string configPfad = Path.Combine(saveDir, "EinmaligerSpawn_Config.json");

            try
            {
                // Prüfen und Ordner erstellen
                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[EinmaligerSpawn] Fehler beim Finden oder Erstellen des Ordners.: {e.Message}");
                return;
            }

            try
            {
                var config = new ConfigDaten
                {
                    // alphabetische Reihenfolge der Eigenschaften
                    BuffUpdateIntervall = BuffUpdateIntervall,
                    ChatNachrichtenAktiv = ChatNachrichtenAktiv,
                    GlobalesZombieLimit = GlobalesZombieLimit,
                    GlobalScanAbgeschlossen = GlobalScanAbgeschlossen,
                    KartenOverlayAktiv = KartenOverlayAktiv,
                    LokalerChunkClearAktiv = LokalerChunkClearAktiv,
                    ProgressBuffRadius = ProgressBuffRadius,
                    SpawnCheckIntervall = SpawnCheckIntervall,
                    TaktischerKillAktiv = TaktischerKillAktiv,
                    ZeigeLokalenFortschritt = ZeigeLokalenFortschritt,
                };

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPfad, json);
            }
            catch (Exception e)
            {
                Log.Error($"[EinmaligerSpawn] Fehler beim Speichern der lokalen Config: {e.Message}");
            }
        }

        private class ConfigDaten
        {
            public float BuffUpdateIntervall { get; set; } = 2f;
            public bool ChatNachrichtenAktiv { get; set; } = true;
            public int GlobalesZombieLimit { get; set; } = 18;
            public bool GlobalScanAbgeschlossen { get; set; } = false;
            public bool KartenOverlayAktiv { get; set; } = true;
            public bool LokalerChunkClearAktiv { get; set; } = true;
            public int ProgressBuffRadius { get; set; } = 120;
            public float SpawnCheckIntervall { get; set; } = 5f;
            public bool TaktischerKillAktiv { get; set; } = true;
            public bool ZeigeLokalenFortschritt { get; set; } = true;
        }
    }
}