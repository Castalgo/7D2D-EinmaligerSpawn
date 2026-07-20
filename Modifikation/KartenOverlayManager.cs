using System.Collections.Generic;
using UnityEngine;

namespace EinmaligerSpawn.Manager
{
    public static class KartenOverlayManager
    {
        // Speichert die aktiven Marker, damit wir sie wieder löschen können
        private static Dictionary<string, NavObject> aktiveMarker = new Dictionary<string, NavObject>();
        public static bool IstAktiv { get; private set; } = false;

        public static void SetzeModus(bool aktiv)
        {
            // Wenn der Zustand schon dem gewünschten entspricht, tun wir nichts
            if (IstAktiv == aktiv) return;

            IstAktiv = aktiv;
            ModEinstellungen.KartenOverlayAktiv = IstAktiv;
            ModEinstellungen.Speichern();

            if (IstAktiv)
            {
                ZeichneAlleMarker();
                Debug.Log("[EinmaligerSpawn] Karten-Overlay AKTIVIERT.");
            }
            else
            {
                LoescheAlleMarker();
                Debug.Log("[EinmaligerSpawn] Karten-Overlay DEAKTIVIERT.");
            }
        }

        public static void Reload()
        {
            Debug.Log("[EinmaligerSpawn] Erzwinge Neuladen der Karten-Marker...");
            LoescheAlleMarker();

            if (IstAktiv)
            {
                ZeichneAlleMarker();
                Debug.Log("[EinmaligerSpawn] Marker erfolgreich neu gezeichnet.");
            }
            else
            {
                Debug.Log("[EinmaligerSpawn] Overlay ist aktuell deaktiviert. Marker wurden nur gelöscht.");
            }
        }

        public static void Wiederherstellen()
        {
            IstAktiv = ModEinstellungen.KartenOverlayAktiv;
            if (IstAktiv)
            {
                ZeichneAlleMarker();
                Debug.Log("[EinmaligerSpawn] Karten-Overlay aus lokaler Config wiederhergestellt (AKTIV).");
            }
        }

        private static void ZeichneAlleMarker()
        {
            foreach (var kvp in ChunkDatenbank.ToteZombiesProChunk)
            {
                // Wenn der Chunk als ausgerottet gilt[cite: 20]
                if (kvp.Value >= 1)
                {
                    ZeichneMarker(kvp.Key);
                }
            }
        }

        public static void ZeichneMarker(string chunkId)
        {
            if (aktiveMarker.ContainsKey(chunkId)) return;

            string[] teile = chunkId.Split('_');
            if (teile.Length == 2 && int.TryParse(teile[0], out int cx) && int.TryParse(teile[1], out int cz))
            {
                // Exakte Mitte des 16x16 Chunks berechnen[cite: 20]
                float xMitte = (cx * 16f) + 8f;
                float zMitte = (cz * 16f) + 8f;
                Vector3 chunkZentrum = new Vector3(xMitte, 0, zMitte);

                // Marker setzen (Wir nutzen die Vector3 Überladung von RegisterNavObject)[cite: 20]
                NavObject marker = NavObjectManager.Instance.RegisterNavObject("chunk_cleared_marker", chunkZentrum, "", false);
                aktiveMarker[chunkId] = marker;
            }
        }

        private static void LoescheAlleMarker()
        {
            foreach (var marker in aktiveMarker.Values)
            {
                NavObjectManager.Instance.UnRegisterNavObject(marker);
            }
            aktiveMarker.Clear();
        }
    }
}