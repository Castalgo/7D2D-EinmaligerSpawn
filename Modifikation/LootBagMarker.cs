using System.Collections.Generic;
using UnityEngine;

namespace EinmaligerSpawn.LootBagMarker
{
    public static class LootbagMarkerManager
    {
        public static bool IstAktiv { get; private set; } = false;

        // Unser Gedächtnis: Entity-ID -> UI-Marker
        private static Dictionary<int, NavObject> aktiveLootbagMarker = new Dictionary<int, NavObject>();

        private static float checkTimer = 0f;
        private const float CHECK_INTERVALL = 2.0f; // Das Radar scannt alle 2 Sekunden

        public static void SetzeModus(bool aktiv)
        {
            if (IstAktiv == aktiv) return;

            IstAktiv = aktiv;

            // NEU: Status in der lokalen Config speichern
            Manager.ModEinstellungen.LootbagMarkerAktiv = IstAktiv;
            Manager.ModEinstellungen.Speichern();

            if (IstAktiv)
            {
                Debug.Log("[EinmaligerSpawn] Lootbag-Marker AKTIVIERT.");
                checkTimer = CHECK_INTERVALL;
            }
            else
            {
                Debug.Log("[EinmaligerSpawn] Lootbag-Marker DEAKTIVIERT. Lösche Marker...");
                EntferneAlleMarker();
            }
        }

        // Wird vom Lade-Patch aufgerufen
        public static void Wiederherstellen()
        {
            IstAktiv = Manager.ModEinstellungen.LootbagMarkerAktiv;
            if (IstAktiv)
            {
                Debug.Log("[EinmaligerSpawn] Lootbag-Marker aus lokaler Config wiederhergestellt (AKTIV).");
                checkTimer = CHECK_INTERVALL; // Zwingt das Radar zum sofortigen Scan
            }
        }

        public static void OnGameUpdate()
        {
            if (!IstAktiv) return;

            if (GameManager.Instance == null || GameManager.Instance.World == null) return;

            checkTimer += Time.deltaTime;
            if (checkTimer >= CHECK_INTERVALL)
            {
                checkTimer = 0f;
                ScanLootbags();
            }
        }

        private static void ScanLootbags()
        {
            // Temporäre Liste der Bags, die in DIESER Sekunde physisch in der Welt existieren
            List<int> aktuellGefundeneBags = new List<int>();

            foreach (Entity ent in GameManager.Instance.World.Entities.list)
            {
                // Dank der Vererbung filtern wir hier schon 99% des Welt-Mülls heraus
                if (ent is EntityLootContainer bag)
                {
                    string lootList = bag.GetLootList()?.ToLower() ?? "";

                    // SICHERHEITSFILTER: 
                    // Spieler-Rucksäcke haben meist "backpack" im Namen oder leere Listen.
                    // Zombie-Bags haben Namen wie "zombieLootDropRegular".
                    if (!string.IsNullOrEmpty(lootList) && !lootList.Contains("backpack"))
                    {
                        int bagId = bag.entityId;
                        aktuellGefundeneBags.Add(bagId);

                        // Wenn der Bag noch keinen Marker hat, setzen wir einen!
                        if (!aktiveLootbagMarker.ContainsKey(bagId))
                        {
                            // Wir nutzen die Systemklasse "supply_drop" für das Navigations-Icon
                            NavObject marker = NavObjectManager.Instance.RegisterNavObject("supply_drop", bag.transform, "ui_game_symbol_treasure", false);
                            aktiveLootbagMarker[bagId] = marker;
                        }
                    }
                }
            }

            // AUFRÄUMEN: Wir vergleichen unser Gedächtnis mit der Realität
            List<int> zuLoeschen = new List<int>();
            foreach (var kvp in aktiveLootbagMarker)
            {
                // Wenn ein gemerkter Bag nicht mehr in der aktuellen Welt gefunden wurde...
                if (!aktuellGefundeneBags.Contains(kvp.Key))
                {
                    zuLoeschen.Add(kvp.Key);

                    // ... löschen wir seinen UI-Marker
                    if (NavObjectManager.Instance != null)
                    {
                        NavObjectManager.Instance.UnRegisterNavObject(kvp.Value);
                    }
                }
            }

            // Die gelöschten Bags nun auch aus unserem Skript-Gedächtnis entfernen
            foreach (int id in zuLoeschen)
            {
                aktiveLootbagMarker.Remove(id);
            }
        }

        public static void EntferneAlleMarker()
        {
            if (NavObjectManager.Instance != null)
            {
                foreach (var marker in aktiveLootbagMarker.Values)
                {
                    NavObjectManager.Instance.UnRegisterNavObject(marker);
                }
            }
            aktiveLootbagMarker.Clear();
        }
    }
}