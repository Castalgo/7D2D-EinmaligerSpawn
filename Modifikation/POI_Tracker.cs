using System;
using System.Collections.Generic;
using System.IO;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.Network;
using Newtonsoft.Json;
using UnityEngine;

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

                    // TODO: Hier später (ähnlich wie beim Chunk-Clear) das NetPackage an die Clients feuern, 
                    // wenn dies vom Server aufgerufen wird.
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

// Berechnet lokal auf dem Client die Distanzen zu ungesäuberten POIs 
// und platziert dynamisch die entsprechenden XML-Map-Marker.
public class PoiRadarManager : MonoBehaviour
    {
        private float updateTimer = 0f;
        private const float UpdateIntervall = 2f;
        private const float NahbereichDistanzSq = 48f * 48f;

        // Das Dictionary dient als "Gedächtnis". Es verknüpft die POI-ID mit dem gesetzten Marker.
        private Dictionary<int, NavObject> aktiveMarker = new Dictionary<int, NavObject>();

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
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null) return;

            DynamicPrefabDecorator decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            if (decorator == null) return;

            List<PrefabInstance> allePois = new List<PrefabInstance>();
            decorator.GetPOIPrefabs(allePois);

            if (allePois.Count == 0) return;

            Vector3 playerPos = player.position;

            // In dieser Liste merken wir uns, welche POIs in DIESER Sekunde noch einen Marker brauchen
            HashSet<int> aktuellePoiIds = new HashSet<int>();

            foreach (PrefabInstance poi in allePois)
            {
                // FOG OF WAR CHECK: Ist der Bereich schon erkundet?
                if (player.ChunkObserver != null && player.ChunkObserver.mapDatabase != null)
                {
                    // Weltkoordinaten in Chunk-Koordinaten umrechnen
                    int chunkX = World.toChunkXZ((int)poi.boundingBoxPosition.x);
                    int chunkZ = World.toChunkXZ((int)poi.boundingBoxPosition.z);

                    // Den eindeutigen Key für die Datenbank generieren
                    long chunkKey = WorldChunkCache.MakeChunkKey(chunkX, chunkZ);

                    // Wenn die Chunk-Datenbank diesen Key NICHT enthält, war der Spieler noch nie dort.
                    // Das POI bleibt versteckt, wir brechen für dieses Haus hier ab.
                    if (!player.ChunkObserver.mapDatabase.Contains(chunkKey))
                    {
                        continue;
                    }
                }

                if (PoiDatenbank.IstGecleart(poi.id)) continue;
                if (poi.sleeperVolumes == null || poi.sleeperVolumes.Count == 0) continue;

                // AUTO-DETECT FÜR ALTLASTEN
                bool hatAktiveRaeume = false;
                foreach (SleeperVolume volumen in poi.sleeperVolumes)
                {
                    if (!volumen.wasCleared)
                    {
                        hatAktiveRaeume = true;
                        break;
                    }
                }

                if (!hatAktiveRaeume)
                {
                    if (!PoiDatenbank.IstGecleart(poi.id))
                    {
                        PoiDatenbank.SetzeGecleart(poi.id);
                        Log.Out($"[EinmaligerSpawn] Auto-Detect: Altes POI '{poi.name}' (ID: {poi.id}) als gesäubert erkannt und nachgetragen.");

                        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                        {
                            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(poi.id));
                        }
                        else
                        {
                            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(poi.id));
                        }

                        // LOKALER CHAT FÜR DEN SPIELER, DER GERADE SCANT
                        if (!GameManager.IsDedicatedServer && ModEinstellungen.ChatNachrichtenAktiv)
                        {
                            ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                            string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
                            string feedbackMsg = $"[00FF00][{timeString}] POI {poi.name} wurde gecleart![-]";

                            // Neu: ChatMessageClient statt ChatMessageServer
                            GameManager.Instance.ChatMessageClient(EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
                        }
                    }
                    continue;
                }

                // POI braucht einen Marker! ID in unser aktuelles Gedächtnis aufnehmen.
                aktuellePoiIds.Add(poi.id);

                Vector3 poiPos = new Vector3(poi.boundingBoxPosition.x, poi.boundingBoxPosition.y, poi.boundingBoxPosition.z);
                float distSq = (playerPos - poiPos).sqrMagnitude;

                string benoetigteKlasse;
                Vector3 markerPos;

                Vector3 centerPoiPos = new Vector3(
                    poi.boundingBoxPosition.x + (poi.boundingBoxSize.x / 2f),
                    poi.boundingBoxPosition.y + (poi.boundingBoxSize.y / 2f),
                    poi.boundingBoxPosition.z + (poi.boundingBoxSize.z / 2f)
                );

                if (distSq > NahbereichDistanzSq)
                {
                    // Fall A (Weit weg): Haus-Symbol
                    benoetigteKlasse = "es_poi_global";
                    markerPos = centerPoiPos;
                }
                else
                {
                    // Fall B (Nahbereich): Roter Punkt im Raum
                    benoetigteKlasse = "es_poi_local";
                    SleeperVolume zielRaum = null;
                    foreach (SleeperVolume volume in poi.sleeperVolumes)
                    {
                        if (!volume.wasCleared)
                        {
                            zielRaum = volume;
                            break;
                        }
                    }

                    if (zielRaum != null)
                    {
                        // Koordinaten des Raums für den Marker verwenden
                        markerPos = new Vector3(zielRaum.Center.x, zielRaum.Center.y, zielRaum.Center.z);
                    }
                    else
                    {
                        markerPos = centerPoiPos; // Fallback
                    }
                }

                // -------------------------------------------------------------
                // FLACKER-SCHUTZ: Abgleich mit dem Dictionary
                // -------------------------------------------------------------
                if (aktiveMarker.TryGetValue(poi.id, out NavObject alterMarker))
                {
                    // Hat der Spieler die 48m-Grenze überschritten? (Haus vs. Punkt)
                    if (alterMarker.NavObjectClass.NavObjectClassName != benoetigteKlasse)
                    {
                        // Klasse hat gewechselt: Alten löschen, neuen zeichnen
                        NavObjectManager.Instance.UnRegisterNavObject(alterMarker);
                        NavObject neuerMarker = NavObjectManager.Instance.RegisterNavObject(benoetigteKlasse, markerPos, "", false);
                        if (neuerMarker != null) aktiveMarker[poi.id] = neuerMarker;
                    }
                    else
                    {
                        // Klasse ist gleich geblieben! Einfach nur die Position im Hintergrund updaten.
                        // Das Symbol bleibt statisch auf der UI und flackert nicht!
                        alterMarker.TrackedPosition = markerPos;
                    }
                }
                else
                {
                    // Komplett neuer POI im Radar
                    NavObject neuerMarker = NavObjectManager.Instance.RegisterNavObject(benoetigteKlasse, markerPos, "", false);
                    if (neuerMarker != null) aktiveMarker.Add(poi.id, neuerMarker);
                }
            }

            // =========================================================
            // DAS AUFRÄUMEN: Alte Marker entfernen
            // =========================================================
            List<int> zuLoeschen = new List<int>();
            foreach (var kvp in aktiveMarker)
            {
                // Wenn die ID nicht in der neuen Liste ist, sind wir zu weit weg oder das POI wurde gecleart
                if (!aktuellePoiIds.Contains(kvp.Key))
                {
                    NavObjectManager.Instance.UnRegisterNavObject(kvp.Value); // Von der Map löschen
                    zuLoeschen.Add(kvp.Key); // Zum Entfernen aus dem Dictionary vormerken
                }
            }

            // Dictionary bereinigen
            foreach (int id in zuLoeschen)
            {
                aktiveMarker.Remove(id);
            }
        }

        void OnDestroy()
        {
            // Beim Beenden des Spiels das Dictionary sauber leeren
            foreach (var kvp in aktiveMarker)
            {
                NavObjectManager.Instance.UnRegisterNavObject(kvp.Value);
            }
            aktiveMarker.Clear();
        }
    }
}