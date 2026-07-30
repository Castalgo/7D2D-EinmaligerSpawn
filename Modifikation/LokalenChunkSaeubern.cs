using System;
using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.Network;
using UnityEngine;

namespace EinmaligerSpawn.LocalClear
{
    public static class LokalenChunkSaeubern
    {
        private class TrackingDaten
        {
            public string ChunkId = "";
            public float ZeitImChunk = 0f;
        }

        private static Dictionary<int, TrackingDaten> spielerTracking = new Dictionary<int, TrackingDaten>();

        // Speichert dauerhaft, ob ein Spieler seinen Schutz verloren hat (Performance-Boost)
        private static Dictionary<int, bool> playerProtectionLost = new Dictionary<int, bool>();

        private static float checkTimer = 0f; // Timer zum Drosseln der Update-Frequenz

        // HILFSMETHODE: Berechnet den individuellen Multiplikator für den Spawnschutz
        private static float ErmittleDrosselungsFaktor(EntityPlayer player)
        {
            int pid = player.entityId;

            if (!playerProtectionLost.ContainsKey(pid))
                playerProtectionLost[pid] = false;

            // Wenn der Schutz bereits weg ist, geben wir sofort Faktor 1 zurück
            if (playerProtectionLost[pid])
                return 1f;

            int safeZoneLevel = GamePrefs.GetInt(EnumGamePrefs.PlayerSafeZoneLevel);
            int safeZoneHours = GamePrefs.GetInt(EnumGamePrefs.PlayerSafeZoneHours);
            float tagesLaengeEchtzeit = GamePrefs.GetInt(EnumGamePrefs.DayNightLength);

            float echteMinutenProGameStunde = tagesLaengeEchtzeit / 24f;
            float schutzZeitInEchtenMinuten = safeZoneHours * echteMinutenProGameStunde;

            bool levelVerbraucht = player.Progression.Level > safeZoneLevel;
            bool zeitVerbraucht = player.totalTimePlayed > schutzZeitInEchtenMinuten;

            if (levelVerbraucht && zeitVerbraucht)
            {
                playerProtectionLost[pid] = true; // Dauerhaft speichern!
                return 1f;
            }

            if (levelVerbraucht || zeitVerbraucht)
            {
                return 6f; // Halber Schutz
            }

            return 12f; // Voller Schutz
        }

        public static void OnGameUpdate()
        {
            // Abbruch, wenn das Feature über die Config deaktiviert wurde
            if (!ModEinstellungen.LokalerChunkClearAktiv)
                return;

            // Abbruch, wenn wir nicht auf dem Server sind (Client-Only)
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                return;

            // Abbruch, wenn die Welt oder Spieler noch nicht initialisiert sind
            if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.Players == null)
                return;

            checkTimer += Time.deltaTime;
            if (checkTimer < 0.5f)
                return;

            float vergangeneZeit = checkTimer;
            checkTimer = 0f;

            foreach (EntityPlayer player in GameManager.Instance.World.Players.list)
            {
                int playerId = player.entityId;
                if (!spielerTracking.ContainsKey(playerId))
                {
                    spielerTracking[playerId] = new TrackingDaten();
                }

                TrackingDaten daten = spielerTracking[playerId];

                Vector3i pos = player.GetBlockPosition();
                int cx = pos.x >> 4;
                int cz = pos.z >> 4;
                string aktuellerChunk = $"{cx}_{cz}";

                if (daten.ChunkId != aktuellerChunk)
                {
                    daten.ChunkId = aktuellerChunk;
                    daten.ZeitImChunk = 0f;
                    continue;
                }

                // Statt Time.deltaTime rechnen wir den angesammelten Block von z.B. 0.51 Sekunden drauf
                daten.ZeitImChunk += vergangeneZeit;

                // NEU: Dynamische Berechnung der benötigten Zeit (Basis: 4 Sekunden)
                float drosselungsFaktor = ErmittleDrosselungsFaktor(player);
                float benoetigteZeit = 4f * drosselungsFaktor;

                if (daten.ZeitImChunk >= benoetigteZeit)
                {
                    bool erfolgreich = PruefeUndSaeubere(aktuellerChunk, player);
                    // Den Timer nach einer Überprüfung zurücksetzen, auch wenn sie fehlschlug
                    daten.ZeitImChunk = 0f;
                }
            }
        }

        private static bool PruefeUndSaeubere(string chunkId, EntityPlayer player)
        {
            if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId) && KillCounter.ToteZombiesProChunk[chunkId] >= 1)
                return false;

            if (KillCounter.ZombieUrsprung.ContainsValue(chunkId))
                return false;

            foreach (Entity ent in GameManager.Instance.World.Entities.list)
            {
                if (ent is EntityEnemy || ent is EntityZombie)
                {
                    EntityAlive enemyAlive = ent as EntityAlive;
                    if (enemyAlive != null && enemyAlive.IsAlive())
                    {
                        if (enemyAlive.GetAttackTarget() == player)
                        {
                            return false;
                        }

                        Vector3i entPos = ent.GetBlockPosition();
                        string entChunk = $"{entPos.x >> 4}_{entPos.z >> 4}";
                        if (entChunk == chunkId)
                        {
                            return false;
                        }
                    }
                }
            }

            KillCounter.ToteZombiesProChunk[chunkId] = 1;

            // Log-Text leicht angepasst, da es nicht mehr fix 4s sind
            Log.Out($"[EinmaligerSpawn] Walkthrough-Clear: Chunk {chunkId} wurde durch friedliche Präsenz von '{player.EntityName}' gesäubert.");

            if (ModEinstellungen.ChatNachrichtenAktiv) // nur wenn die Chatnachrichten aktiviert sind, wird die Nachricht gesendet
            {
                // Chatnachricht
                ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
                string timeString = $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
                string feedbackMsg = $"[00FF00][{timeString}] Walkthrough-Clear: Chunk {chunkId} wurde von '{player.EntityName}' als gesäubert verifiziert.[-]";
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1, feedbackMsg, null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
            }

            if (ModEinstellungen.KartenOverlayAktiv)
            {
                KartenOverlay.ErzwingeRedraw();
            }

            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(new NetPackageChunkSync(chunkId));
            }

            return true;
        }

        public static void Reset()
        {
            spielerTracking.Clear();
            playerProtectionLost.Clear(); // NEU: Muss bei Welt-Neustart geleert werden
        }

        public static void Diagnose(EntityPlayerLocal player)
        {
            // 1. Feature deaktiviert?
            if (!ModEinstellungen.LokalerChunkClearAktiv)
            {
                Log.Warning("[EinmaligerSpawn] Diagnose: 'localclear' ist in der Config deaktiviert (OFF).");
                return;
            }

            Vector3i pos = player.GetBlockPosition();
            int cx = pos.x >> 4;
            int cz = pos.z >> 4;
            string chunkId = $"{cx}_{cz}";

            // 2. Chunk bereits clear?
            if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId) && KillCounter.ToteZombiesProChunk[chunkId] >= 1)
            {
                int kills = KillCounter.ToteZombiesProChunk[chunkId];
                Log.Out($"[EinmaligerSpawn] Diagnose: Dieser Chunk ({chunkId}) ist bereits als gesäubert markiert! Registrierte Kills hier: {kills}");

                // NEU: Karten-Update für den aktuellen Chunk und die 8 Nachbarn erzwingen
                if (ModEinstellungen.KartenOverlayAktiv)
                {
                    int geupdateteChunks = 0;

                    for (int x = -1; x <= 1; x++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            string neighborChunkId = $"{cx + x}_{cz + z}";

                            // Sicherheitscheck: Nur Nachbarn zeichnen, die auch wirklich clear sind
                            if (KillCounter.ToteZombiesProChunk.ContainsKey(neighborChunkId) && KillCounter.ToteZombiesProChunk[neighborChunkId] >= 1)
                            {
                                KartenOverlay.ErzwingeRedraw();
                                geupdateteChunks++;
                            }
                        }
                    }
                    Log.Out($"[EinmaligerSpawn] Diagnose: Karten-Overlay für {geupdateteChunks} gesäuberte(n) Chunk(s) im direkten Umfeld (3x3 Raster) wurde synchronisiert.");
                }

                return;
            }

            // Wir sammeln alle Zombies, die den Clear blockieren, um sie am Ende zu markieren
            List<Entity> blockierendeZombies = new List<Entity>();
            bool ursprungGefunden = false;
            bool angreiferGefunden = false;
            bool bewohnerGefunden = false;

            foreach (Entity ent in GameManager.Instance.World.Entities.list)
            {
                if ((ent is EntityEnemy || ent is EntityZombie) && ent.IsAlive())
                {
                    bool istBlockierer = false;
                    EntityAlive enemyAlive = ent as EntityAlive;

                    // 3. Ursprungs-Chunk aktiv?
                    if (KillCounter.ZombieUrsprung.TryGetValue(ent.entityId, out string uChunk) && uChunk == chunkId)
                    {
                        ursprungGefunden = true;
                        istBlockierer = true;
                    }

                    // 4. Spieler im Kampf?
                    if (enemyAlive != null && enemyAlive.GetAttackTarget() == player)
                    {
                        angreiferGefunden = true;
                        istBlockierer = true;
                    }

                    // 5. Zombie physisch im Chunk?
                    Vector3i entPos = ent.GetBlockPosition();
                    string entChunk = $"{entPos.x >> 4}_{entPos.z >> 4}";
                    if (entChunk == chunkId)
                    {
                        bewohnerGefunden = true;
                        istBlockierer = true;
                    }

                    if (istBlockierer)
                    {
                        blockierendeZombies.Add(ent);
                    }
                }
            }

            // 6. Blockierer auswerten und markieren
            if (blockierendeZombies.Count > 0)
            {
                string grund = "";
                if (angreiferGefunden) grund += "- Du bist im Kampf (Wirst anvisiert)\n";
                if (bewohnerGefunden) grund += "- Mindestens ein Feind hält sich in deinem Chunk auf\n";
                if (ursprungGefunden) grund += "- Ein Zombie, der zu diesem Chunk gehört, lebt noch\n";

                Log.Warning($"[EinmaligerSpawn] Diagnose für {chunkId} fehlgeschlagen!\nGründe:\n{grund}");
                Log.Warning($"[EinmaligerSpawn] Setze temporäre Radar-Marker auf {blockierendeZombies.Count} störende Feind(e)...");

                // Das Radar-Icon holen (wie bei es where)
                string magicClassName = "supply_drop";
                if (NavObjectClass.NavObjectClassList != null)
                {
                    foreach (NavObjectClass noc in NavObjectClass.NavObjectClassList)
                    {
                        if (noc.RequirementType == NavObjectClass.RequirementTypes.None && noc.CompassSettings != null)
                        {
                            magicClassName = noc.NavObjectClassName;
                            break;
                        }
                    }
                }

                // Alle Störenfriede markieren
                foreach (Entity feind in blockierendeZombies)
                {
                    NavObjectManager.Instance.RegisterNavObject(magicClassName, feind.transform, "ui_game_symbol_enemy_dot", false);
                }

                return;
            }

            // 7. Timer-Prüfung (Wenn keine Zombies stören, liegt es an der Zeit)

            // NEU: Wir müssen in der Diagnose die korrekte, skalierte Zeit berechnen
            float drosselungsFaktor = ErmittleDrosselungsFaktor(player);
            float benoetigteZeit = 4f * drosselungsFaktor;

            if (spielerTracking.TryGetValue(player.entityId, out TrackingDaten daten))
            {
                if (daten.ChunkId == chunkId)
                {
                    Log.Out($"[EinmaligerSpawn] Diagnose: Alles ruhig! Dein Timer steht aktuell bei: {daten.ZeitImChunk:0.0} / {benoetigteZeit:0.0} Sekunden.");
                }
                else
                {
                    Log.Out($"[EinmaligerSpawn] Diagnose: Alles ruhig! Du hast diesen Chunk aber gerade erst betreten. (0.0 / {benoetigteZeit:0.0} Sekunden).");
                }
            }
        }
    }
}