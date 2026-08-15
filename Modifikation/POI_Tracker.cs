using System;
using System.Collections.Generic;
using System.IO;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.Minimap_Patch;
using EinmaligerSpawn.Network;
using Newtonsoft.Json;
using UnityEngine;
using static EinmaligerSpawn.Network.NetPackagePoiSync;

namespace EinmaligerSpawn.PoiTracker
{
    // Verwaltet den Status der gesäuberten POIs ressourcenschonend über deren eindeutige ID.
    public static class PoiDatenbank
    {
        // Speichert lediglich: PrefabInstance.id -> IstGecleart
        public static Dictionary<int, bool> GecleartePOIs = new Dictionary<int, bool>();

        // Markiert einen POI dauerhaft als gesäubert.
        public static void SetzeGecleart(int poiId)
        {
            if (!GecleartePOIs.ContainsKey(poiId))
            {
                GecleartePOIs[poiId] = true;
            }
        }

        // Prüft, ob ein POI bereits gesäubert wurde. Standardwert ist false.
        public static bool IstGecleart(int poiId)
        {
            return GecleartePOIs.TryGetValue(poiId, out bool gecleart) && gecleart;
        }

        // Nur Server: Lädt die POI-Datenbank aus der JSON-Datei
        public static void Load(string saveDir)
        {
            string path = Path.Combine(saveDir, "ausgerottetePOIs.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    GecleartePOIs = JsonConvert.DeserializeObject<Dictionary<int, bool>>(json) ?? new Dictionary<int, bool>();
                    Log.Out($"[EinmaligerSpawn] {GecleartePOIs.Count} POI-Daten erfolgreich geladen.");
                }
                catch (Exception e)
                {
                    Log.Error($"[EinmaligerSpawn] Fehler beim Laden der POIs: {e.Message}");
                }
            }
            else
            {
                GecleartePOIs.Clear();
            }
        }

        // Nur Server: Speichert die POI-Datenbank in einer JSON-Datei
        public static void Save(string saveDir)
        {
            try
            {
                string path = Path.Combine(saveDir, "ausgerottetePOIs.json");
                string json = JsonConvert.SerializeObject(GecleartePOIs, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Log.Error($"[EinmaligerSpawn] Fehler beim Speichern der POIs: {e.Message}");
            }
        }
    }

    // Berechnet lokal auf dem Client, ob der Spieler sich in einem ungesäuberten POI befindet, 
    // und platziert dynamisch die entsprechenden XML-Map-Marker.
    public class PoiRadarManager : MonoBehaviour
    {
        private float updateTimer = 0f;
        private const float UpdateIntervall = 2f;

        // Speicher für die Marker-Objekte
        private Dictionary<int, NavObject> aktiveMarker = new Dictionary<int, NavObject>();

        // CLIENT-Gedächtnis: Hier landen die Koordinaten aus dem NetPackage
        public static Dictionary<int, Vector3> ClientZiele = new Dictionary<int, Vector3>();

        // SERVER-Gedächtnis: Verhindert, dass wir denselben Raum jede Sekunde neu funken
        private static Dictionary<int, Vector3> ServerLetzteZiele = new Dictionary<int, Vector3>();

        void Update()
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= UpdateIntervall)
            {
                updateTimer = 0f;
                AktualisiereMarker();
            }
        }

        private void AktualisiereMarker()
        {
            // Client only. Dedicated Server rausschmeißen
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null) return;

            // Abbruch, falls NavObjecte noch lädt (grafische Benutzeroberfläche)
            if (!NavObjectManager.HasInstance) return;

            // Abbruch, falls das POI-System der Engine noch lädt (Welt geladen)
            DynamicPrefabDecorator decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            if (decorator == null) return;

            List<PrefabInstance> allePois = new List<PrefabInstance>();
            decorator.GetAllPrefabs(allePois);

            // Abbruch, falls die aktuelle Karte überhaupt keine POIs besitzt (z. B. in einer leeren Testwelt)
            if (allePois.Count == 0) return;

            Vector3 playerPos = player.position;
            HashSet<int> aktuellePoiIds = new HashSet<int>();
            bool isServer = SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;

            foreach (PrefabInstance poi in allePois)
            {
                // Alle POIs ohne SleeperVolumes sind uninteressant, da sie keine Räume enthalten, die gecleart werden können.
                if (poi.sleeperVolumes == null || poi.sleeperVolumes.Count == 0)
                {
                    continue;
                }

                // FOG OF WAR CHECK
                if (player.ChunkObserver != null && player.ChunkObserver.mapDatabase != null)
                {
                    int chunkX = World.toChunkXZ((int)poi.boundingBoxPosition.x);
                    int chunkZ = World.toChunkXZ((int)poi.boundingBoxPosition.z);
                    long chunkKey = WorldChunkCache.MakeChunkKey(chunkX, chunkZ);

                    if (!player.ChunkObserver.mapDatabase.Contains(chunkKey)) continue;
                }

                // 1. OBERSTE REGEL: Unsere Datenbank hat immer Recht!
                if (PoiDatenbank.IstGecleart(poi.id)) continue;

                // 2. SERVER-LOGIK: Auto-Detect & Ziel-Ermittlung
                if (isServer)
                {
                    if (poi.sleeperVolumes == null || poi.sleeperVolumes.Count == 0)
                    {
                        PoiDatenbank.SetzeGecleart(poi.id);
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(poi.id));
                        continue;
                    }

                    SleeperVolume naechsterAktiverRaum = null;
                    foreach (SleeperVolume volumen in poi.sleeperVolumes)
                    {
                        if (!volumen.wasCleared)
                        {
                            naechsterAktiverRaum = volumen;
                            break;
                        }
                    }

                    // POI ist restlos gecleart
                    if (naechsterAktiverRaum == null)
                    {
                        PoiDatenbank.SetzeGecleart(poi.id);
                        Log.Out($"[EinmaligerSpawn] Auto-Detect: POI '{poi.name}' (ID: {poi.id}) als gesäubert erkannt.");
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(poi.id));

                        if (!GameManager.IsDedicatedServer && ModEinstellungen.ChatNachrichtenAktiv)
                        {
                            ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                            string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
                            string feedbackMsg = $"[00FF00][{timeString}] POI {poi.name} wurde gecleart![-]";
                            GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                        }

                        // erzwingt Minimap Update, sofern Minimap Mod aktiv
                        SimpleMinimap_Patch.ErzwingeRedraw = true;

                        ServerLetzteZiele.Remove(poi.id); // Aufräumen
                        continue;
                    }

                    // Es gibt einen aktiven Raum! Koordinate ermitteln
                    Vector3 zielMitte = new Vector3(naechsterAktiverRaum.Center.x, naechsterAktiverRaum.Center.y, naechsterAktiverRaum.Center.z);

                    // Hat sich das Ziel für diesen POI geändert? (Oder ist es ganz neu?)
                    if (!ServerLetzteZiele.TryGetValue(poi.id, out Vector3 letztesZiel) || letztesZiel != zielMitte)
                    {
                        ServerLetzteZiele[poi.id] = zielMitte;

                        // Den Host selbst müssen wir nicht per Netzwerk informieren, wir schreiben es ihm direkt ins Client-Gedächtnis
                        ClientZiele[poi.id] = zielMitte;

                        // Info an alle Clients schicken
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiRadarUpdate>().Setup(poi.id, zielMitte));
                    }
                }

                // =========================================================
                // 3. MARKER ZEICHNEN (CLIENT & HOST)
                // =========================================================
                aktuellePoiIds.Add(poi.id);

                Vector3 centerPoiPos = new Vector3(
                    poi.boundingBoxPosition.x + (poi.boundingBoxSize.x / 2f),
                    poi.boundingBoxPosition.y + (poi.boundingBoxSize.y / 2f),
                    poi.boundingBoxPosition.z + (poi.boundingBoxSize.z / 2f)
                );

                Bounds poiBounds = new Bounds(centerPoiPos, new Vector3(poi.boundingBoxSize.x, poi.boundingBoxSize.y, poi.boundingBoxSize.z));
                bool isInside = poiBounds.Contains(playerPos);

                string benoetigteKlasse;
                Vector3 markerPos;

                if (!isInside)
                {
                    // Fall A (Draußen): Haus-Symbol 
                    benoetigteKlasse = "es_poi_global";
                    markerPos = centerPoiPos;
                }
                else
                {
                    // Fall B (Drinnen): Roter Punkt
                    benoetigteKlasse = "es_poi_local";

                    // Wir bedienen uns einfach an den Koordinaten, die uns der Server per NetPackage gefunkt hat!
                    if (ClientZiele.TryGetValue(poi.id, out Vector3 empfangenesZiel))
                    {
                        markerPos = empfangenesZiel;
                    }
                    else
                    {
                        // Falls das Netzwerkpaket noch eine Millisekunde braucht, kurzer Fallback auf die Hausmitte
                        markerPos = centerPoiPos;
                    }
                }

                // FLACKER-SCHUTZ & NULL-CHECK
                if (aktiveMarker.TryGetValue(poi.id, out NavObject alterMarker))
                {
                    // WICHTIG: Hat die 7DTD-Engine den Marker im Hintergrund gelöscht, weil wir zu weit weg waren?
                    if (alterMarker == null || alterMarker.NavObjectClass == null)
                    {
                        aktiveMarker.Remove(poi.id);
                        NavObject neuerMarker = NavObjectManager.Instance.RegisterNavObject(benoetigteKlasse, markerPos, "", false);
                        if (neuerMarker != null) aktiveMarker[poi.id] = neuerMarker;
                    }
                    else if (alterMarker.NavObjectClass.NavObjectClassName != benoetigteKlasse)
                    {
                        NavObjectManager.Instance.UnRegisterNavObject(alterMarker);
                        NavObject neuerMarker = NavObjectManager.Instance.RegisterNavObject(benoetigteKlasse, markerPos, "", false);
                        if (neuerMarker != null) aktiveMarker[poi.id] = neuerMarker;
                    }
                    else
                    {
                        alterMarker.TrackedPosition = markerPos;
                    }
                }
                else
                {
                    NavObject neuerMarker = NavObjectManager.Instance.RegisterNavObject(benoetigteKlasse, markerPos, "", false);
                    if (neuerMarker != null) aktiveMarker.Add(poi.id, neuerMarker);
                }
            }

            // AUFRÄUMEN
            List<int> zuLoeschen = new List<int>();
            foreach (var kvp in aktiveMarker)
            {
                if (!aktuellePoiIds.Contains(kvp.Key))
                {
                    NavObjectManager.Instance.UnRegisterNavObject(kvp.Value);
                    zuLoeschen.Add(kvp.Key);
                }
            }

            foreach (int id in zuLoeschen)
            {
                aktiveMarker.Remove(id);
                ClientZiele.Remove(id); // Speicher-Leck verhindern
            }
        }

        void OnDestroy()
        {
            foreach (var kvp in aktiveMarker)
            {
                NavObjectManager.Instance.UnRegisterNavObject(kvp.Value);
            }
            aktiveMarker.Clear();
            ClientZiele.Clear();
            ServerLetzteZiele.Clear();
        }
    }
}