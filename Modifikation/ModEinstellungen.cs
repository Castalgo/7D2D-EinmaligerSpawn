using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace EinmaligerSpawn.Manager
{
    public static class ModEinstellungen
    {
        public static bool KartenOverlayAktiv = false;
        public static bool TaktischerKillAktiv = true;
        public static bool ChatNachrichtenAktiv = true;
        public static bool LokalerChunkClearAktiv = true;

        // NEU: Die konfigurierbaren Werte für den Autospawner
        public static int GlobalesZombieLimit = 18;
        public static float SpawnCheckIntervall = 15f;

        public static void Laden(string saveDir)
        {
            string configPfad = Path.Combine(saveDir, "ModConfig.json");
            if (File.Exists(configPfad))
            {
                try
                {
                    string json = File.ReadAllText(configPfad);
                    var config = JsonConvert.DeserializeObject<ConfigDaten>(json);
                    if (config != null)
                    {
                        KartenOverlayAktiv = config.KartenOverlayAktiv;
                        TaktischerKillAktiv = config.TaktischerKillAktiv;
                        ChatNachrichtenAktiv = config.ChatNachrichtenAktiv;

                        // NEU
                        GlobalesZombieLimit = config.GlobalesZombieLimit;
                        SpawnCheckIntervall = config.SpawnCheckIntervall;
                        LokalerChunkClearAktiv = config.LokalerChunkClearAktiv;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EinmaligerSpawn] Fehler beim Laden der lokalen Config: {e.Message}");
                }
            }
            else
            {
                // Standardwerte, falls noch keine Config existiert
                KartenOverlayAktiv = false;
                TaktischerKillAktiv = true;
                ChatNachrichtenAktiv = true;
                LokalerChunkClearAktiv = true;
                GlobalesZombieLimit = 18;
                SpawnCheckIntervall = 15f;
            }
        }

        public static void Speichern()
        {
            string saveDir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(saveDir)) return;

            string configPfad = Path.Combine(saveDir, "ModConfig.json");
            try
            {
                var config = new ConfigDaten
                {
                    KartenOverlayAktiv = KartenOverlayAktiv,
                    TaktischerKillAktiv = TaktischerKillAktiv,
                    ChatNachrichtenAktiv = ChatNachrichtenAktiv,
                    GlobalesZombieLimit = GlobalesZombieLimit,
                    SpawnCheckIntervall = SpawnCheckIntervall,
                    LokalerChunkClearAktiv = LokalerChunkClearAktiv,
                };
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPfad, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EinmaligerSpawn] Fehler beim Speichern der lokalen Config: {e.Message}");
            }
        }

        private class ConfigDaten
        {
            public bool KartenOverlayAktiv { get; set; }
            public bool TaktischerKillAktiv { get; set; } = true;
            public bool ChatNachrichtenAktiv { get; set; } = true;
            public bool LokalerChunkClearAktiv { get; set; } = true;

            // NEU: Standardwerte für die Serialisierung
            public int GlobalesZombieLimit { get; set; } = 18;
            public float SpawnCheckIntervall { get; set; } = 15f;
        }
    }
}