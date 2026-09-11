using System;
using System.Collections.Generic;
using System.IO;
using EinmaligerSpawn.Benachrichtigungen;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.JSONSpeichern;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.Network;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace EinmaligerSpawn.PoiTracker
{
    // Verwaltet den Status der gesäuberten POIs ressourcenschonend über deren eindeutige ID.
    public static class PoiDatenbank
    {
        // Türsteher-Prinzip: Dictionary ist nun privat
        private static Dictionary<int, byte> poiZustaende = new Dictionary<int, byte>();

        // Setzt einen spezifischen Status für den POI
        public static void SetzeStatus(int poiId, byte status)
        {
            poiZustaende[poiId] = status;
        }

        // Liest den aktuellen Status aus (Standardwert ist 0)
        public static byte LeseStatus(int poiId)
        {
            return poiZustaende.TryGetValue(poiId, out byte status) ? status : (byte)0;
        }

        // Hilfsmethode für das Radar: Reagiert nur auf 100 % gesäuberte Gebäude
        public static bool IstKomplettGecleart(int poiId)
        {
            return LeseStatus(poiId) == 1;
        }

        public static void VerarbeitePoiStatus(int poiId, byte status)
        {
            SetzeStatus(poiId, status);

            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                    NetPackageManager.GetPackage<NetPackagePoiSync>().SetupForLive(poiId, status)
                );
            }

            // Wenn das Gebäude komplett ausgerottet wurde, erzwingen wir ein Karten-Update
            if (status == 1)
            {
                // Kapselung: Zentrales Map-Update für Minimap & Weltkarte
                KartenOverlay.RequestMapUpdate();
            }
        }

        // ==========================================
        // ITERATOREN & SYSTEM (Neu)
        // ==========================================
        public static IEnumerable<int> GetAllePoiIds()
        {
            foreach (int id in poiZustaende.Keys)
            {
                yield return id;
            }
        }

        public static void Reset()
        {
            poiZustaende.Clear();
        }

        // Nur Server: Lädt die POI-Datenbank aus der JSON-Datei
        public static void Load(string saveDir)
        {
            string path = Path.Combine(saveDir, "ausgerottetePOIs.json");

            if (SicheresSpeichern.TryLoad(path, out Dictionary<int, byte> geladeneDaten))
            {
                poiZustaende = geladeneDaten;
                Log.Out($"[EinmaligerSpawn] {poiZustaende.Count} POI-Daten erfolgreich geladen.");
            }
            else
            {
                poiZustaende.Clear();
                Log.Out("[EinmaligerSpawn] Beginne mit leerer POI-Datenbank.");
            }
        }

        // Nur Server: Speichert die POI-Datenbank in einer JSON-Datei
        public static void Save(string saveDir)
        {
            string path = Path.Combine(saveDir, "ausgerottetePOIs.json");
            SicheresSpeichern.Save(path, poiZustaende);
        }
    }

    // =========================================================================================
    // QUEST-ABSCHLUSS IN POI_TRACKER SCHREIBEN (LIVE-UPDATE)
    // Setzt Status 2, sobald der POI physisch abgeschlossen ist (ReadyForTurnIn).
    // =========================================================================================
    [HarmonyPatch(typeof(Quest), "RefreshQuestCompletion")]
    public class Quest_Abschluss_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Quest __instance)
        {
            // 1. Die IsServer-Abfrage ist bewusst gelöscht! Der Client führt dies aus.
            if (__instance.CurrentState != Quest.QuestState.ReadyForTurnIn) return;

            if (__instance.GetPositionData(out Vector3 poiPos, Quest.PositionDataTypes.POIPosition))
            {
                DynamicPrefabDecorator decorator = GameManager.Instance.GetDynamicPrefabDecorator();
                if (decorator != null)
                {
                    PrefabInstance poi = decorator.GetPrefabAtPosition(poiPos);

                    // 2. Client prüft lokal, ob das Gebäude noch den Status 0 hat
                    if (poi != null && PoiDatenbank.LeseStatus(poi.id) == 0)
                    {
                        // 3. Client sendet das neue Prüf-Paket an den Server (Status 2 anfordern)
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                            NetPackageManager.GetPackage<NetPackageRequestPoiCheck>().Setup(poi.id, 2)
                        );
                    }
                }
            }
        }
    }

    // Berechnet lokal auf dem Client (oder fragt den Server), ob der Spieler sich in einem 
    // ungesäuberten POI befindet, und platziert dynamisch die entsprechenden XML-Map-Marker.
    public class PoiRadarManager : MonoBehaviour
    {
        private float updateTimer = 0f;
        private const float UpdateIntervall = 2f;

        private Dictionary<int, NavObject> aktiveMarker = new Dictionary<int, NavObject>();

        // CLIENT-Gedächtnis: Hier landen die Koordinaten und Typen aus dem NetPackage
        public static Dictionary<int, Vector3> ClientZiele = new Dictionary<int, Vector3>();
        public static Dictionary<int, string> ClientMarkerKlassen = new Dictionary<int, string>();

        private static Dictionary<string, bool> lokalerSleeperCache = new Dictionary<string, bool>();

        void Update()
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= UpdateIntervall)
            {
                updateTimer = 0f;
                AktualisiereMarker();
            }
        }

        private bool HatLokaleSleeper(string prefabName)
        {
            if (lokalerSleeperCache.TryGetValue(prefabName, out bool hatSleeper)) return hatSleeper;

            PathAbstractions.AbstractedLocation location = PathAbstractions.PrefabsSearchPaths.GetLocation(prefabName, null, null);
            if (location.Type == PathAbstractions.EAbstractedLocationType.None)
            {
                lokalerSleeperCache[prefabName] = false;
                return false;
            }

            string xmlPath = location.FullPathNoExtension + ".xml";
            if (File.Exists(xmlPath))
            {
                string xmlContent = File.ReadAllText(xmlPath);
                bool containsSleeper = xmlContent.Contains("SleeperVolume");
                lokalerSleeperCache[prefabName] = containsSleeper;
                return containsSleeper;
            }

            lokalerSleeperCache[prefabName] = false;
            return false;
        }

        private void AktualisiereMarker()
        {
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null || !NavObjectManager.HasInstance) return;

            DynamicPrefabDecorator decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            if (decorator == null) return;

            List<PrefabInstance> allePois = new List<PrefabInstance>();
            decorator.GetAllPrefabs(allePois);
            if (allePois.Count == 0) return;

            Vector3 playerPos = player.position;
            HashSet<int> aktuellePoiIds = new HashSet<int>();
            bool isServer = SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;

            foreach (PrefabInstance poi in allePois)
            {
                string saubererName = poi.name;
                int dotIndex = saubererName.LastIndexOf('.');
                if (dotIndex > 0) saubererName = saubererName.Substring(0, dotIndex);

                if (!HatLokaleSleeper(saubererName)) continue;
                if (PoiDatenbank.IstKomplettGecleart(poi.id)) continue;

                if (player.ChunkObserver != null && player.ChunkObserver.mapDatabase != null)
                {
                    int chunkX = World.toChunkXZ((int)poi.boundingBoxPosition.x);
                    int chunkZ = World.toChunkXZ((int)poi.boundingBoxPosition.z);
                    long chunkKey = WorldChunkCache.MakeChunkKey(chunkX, chunkZ);
                    if (!player.ChunkObserver.mapDatabase.Contains(chunkKey)) continue;
                }

                aktuellePoiIds.Add(poi.id);

                Vector3 centerPoiPos = new Vector3(
                    poi.boundingBoxPosition.x + (poi.boundingBoxSize.x / 2f),
                    poi.boundingBoxPosition.y + (poi.boundingBoxSize.y / 2f),
                    poi.boundingBoxPosition.z + (poi.boundingBoxSize.z / 2f)
                );

                Bounds poiBounds = new Bounds(centerPoiPos, new Vector3(poi.boundingBoxSize.x, poi.boundingBoxSize.y, poi.boundingBoxSize.z));
                poiBounds.Expand(30f);
                bool isInside = poiBounds.Contains(playerPos);

                string benoetigteKlasse = "es_poi_global";
                Vector3 markerPos = centerPoiPos;

                if (isInside)
                {
                    if (isServer)
                    {
                        // HOST-LOGIK: Host liest Sleeper-Volumen direkt fehlerfrei aus dem RAM aus
                        int totalValid = 0;
                        int clearedValid = 0;
                        bool hasUnclearedBossRoom = false;
                        SleeperVolume nextTarget = null;

                        if (poi.sleeperVolumes != null)
                        {
                            foreach (SleeperVolume vol in poi.sleeperVolumes)
                            {
                                if (vol.IsTrigger || vol.isQuestExclude) continue;

                                totalValid++;
                                if (vol.wasCleared) clearedValid++;
                                else
                                {
                                    if (vol.isPriority) hasUnclearedBossRoom = true;
                                    if (nextTarget == null) nextTarget = vol;
                                }
                            }
                        }

                        if (totalValid > 0 && clearedValid >= totalValid)
                        {
                            if (PoiDatenbank.LeseStatus(poi.id) != 1)
                            {
                                // Kapselung: Ersetzt SetzeStatus, NetPackage-Aufruf und Map-Redraw
                                PoiDatenbank.VerarbeitePoiStatus(poi.id, 1);

                                // Kapselung: Zentrale UI-Benachrichtigung
                                NotificationManager.SendePoiClear(poi.name);
                            }
                            continue; // Marker löschen
                        }
                        else if (nextTarget != null)
                        {
                            markerPos = nextTarget.Center;
                            benoetigteKlasse = "es_poi_map_only";

                            if (poi.prefab != null && poi.prefab.DifficultyTier > 0)
                            {
                                byte poiStatus = PoiDatenbank.LeseStatus(poi.id);
                                if (poiStatus == 2)
                                {
                                    benoetigteKlasse = "es_poi_local";
                                }
                                else
                                {
                                    int remaining = totalValid - clearedValid;
                                    int threshold = (totalValid < 5) ? 1 : (totalValid < 10) ? 2 : (totalValid < 20) ? 3 : 4;

                                    if (!hasUnclearedBossRoom && remaining <= threshold)
                                    {
                                        benoetigteKlasse = "es_poi_local";
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // CLIENT-LOGIK: Fragt Server nach Radar-Updates (Status 3)
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                            NetPackageManager.GetPackage<NetPackageRequestPoiCheck>().Setup(poi.id, 3)
                        );

                        // Nutzt die letzte erhaltene Antwort des Servers aus dem Gedächtnis
                        if (ClientZiele.TryGetValue(poi.id, out Vector3 empfangenesZiel) && ClientMarkerKlassen.TryGetValue(poi.id, out string empfangeneKlasse))
                        {
                            markerPos = empfangenesZiel;
                            benoetigteKlasse = empfangeneKlasse;
                        }
                    }
                }

                // FLACKER-SCHUTZ & ZEICHNEN
                if (aktiveMarker.TryGetValue(poi.id, out NavObject vorhandenerMarker))
                {
                    if (vorhandenerMarker == null || vorhandenerMarker.NavObjectClass == null || vorhandenerMarker.NavObjectClass.NavObjectClassName != benoetigteKlasse)
                    {
                        if (vorhandenerMarker != null) NavObjectManager.Instance.UnRegisterNavObject(vorhandenerMarker);
                        aktiveMarker.Remove(poi.id);

                        NavObject neuerMarker = NavObjectManager.Instance.RegisterNavObject(benoetigteKlasse, markerPos, "", false);
                        if (neuerMarker != null) aktiveMarker[poi.id] = neuerMarker;
                    }
                    else
                    {
                        vorhandenerMarker.TrackedPosition = markerPos;
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
                ClientZiele.Remove(id);
                ClientMarkerKlassen.Remove(id);
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
            ClientMarkerKlassen.Clear();
        }
    }
}