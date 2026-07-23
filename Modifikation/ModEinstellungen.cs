using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace EinmaligerSpawn.Manager
{
    public static class ModEinstellungen
    {
        public static bool ChatNachrichtenAktiv = true;
        public static int GlobalesZombieLimit = 18;
        public static bool KartenOverlayAktiv = false; 
        public static bool LokalerChunkClearAktiv = true;
        public static bool LootbagMarkerAktiv = false;
        public static float SpawnCheckIntervall = 15f;
        public static bool TaktischerKillAktiv = true;

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
                        ChatNachrichtenAktiv = config.ChatNachrichtenAktiv;
                        GlobalesZombieLimit = config.GlobalesZombieLimit;
                        KartenOverlayAktiv = config.KartenOverlayAktiv;
                        LokalerChunkClearAktiv = config.LokalerChunkClearAktiv;
                        LootbagMarkerAktiv = config.LootbagMarkerAktiv;
                        SpawnCheckIntervall = config.SpawnCheckIntervall;
                        TaktischerKillAktiv = config.TaktischerKillAktiv;
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
                ChatNachrichtenAktiv = true;
                GlobalesZombieLimit = 18;
                KartenOverlayAktiv = false; 
                LokalerChunkClearAktiv = true;
                LootbagMarkerAktiv = false;
                SpawnCheckIntervall = 15f;
                TaktischerKillAktiv = true;
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
                    ChatNachrichtenAktiv = ChatNachrichtenAktiv,
                    GlobalesZombieLimit = GlobalesZombieLimit,
                    KartenOverlayAktiv = KartenOverlayAktiv,
                    LokalerChunkClearAktiv = LokalerChunkClearAktiv,
                    LootbagMarkerAktiv = LootbagMarkerAktiv,
                    SpawnCheckIntervall = SpawnCheckIntervall,
                    TaktischerKillAktiv = TaktischerKillAktiv,
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
            public bool ChatNachrichtenAktiv { get; set; } = true;
            public int GlobalesZombieLimit { get; set; } = 18;
            public bool KartenOverlayAktiv { get; set; }
            public bool LokalerChunkClearAktiv { get; set; } = true;
            public bool LootbagMarkerAktiv { get; set; } = false;
            public float SpawnCheckIntervall { get; set; } = 15f;
            public bool TaktischerKillAktiv { get; set; } = true;
        }
    }
}