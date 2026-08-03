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

        // Server only: Berechnet den individuellen Multiplikator für den Spawnschutz
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

        // Server only: Wird in jedem GameUpdate aufgerufen, um die Spielerposition zu tracken
        public static void OnGameUpdate()
        {
            // Abbruch, wenn das Feature über die Config deaktiviert wurde
            if (!ModEinstellungen.LokalerChunkClearAktiv)
                return;

            // Abbruch, wenn wir nicht der Server sind (nur der Server darf die Logik ausführen)
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

                daten.ZeitImChunk += vergangeneZeit;

                float drosselungsFaktor = ErmittleDrosselungsFaktor(player);
                float benoetigteZeit = 4f * drosselungsFaktor;

                if (daten.ZeitImChunk >= benoetigteZeit)
                {
                    bool erfolgreich = PruefeUndSaeubere(aktuellerChunk, player);
                    daten.ZeitImChunk = 0f;
                }
            }
        }

        // Server only: Prüft, ob der Chunk gesäubert werden kann, und markiert ihn als gesäubert
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

            Log.Out($"[EinmaligerSpawn] Walkthrough-Clear: Chunk {chunkId} wurde durch friedliche Präsenz von '{player.EntityName}' gesäubert.");

            // CHAT ENTFERNT: Die Clients werten das nun über das Netzwerkpaket lokal aus.

            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
            }

            return true;
        }

        public static void Reset()
        {
            spielerTracking.Clear();
            playerProtectionLost.Clear();
        }

        public static void Diagnose(EntityPlayer player)
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Log.Warning("[EinmaligerSpawn] Diagnose abgebrochen: Dieser Befehl steht nur dem Host der Sitzung zur Verfügung.");
                return;
            }

            if (!ModEinstellungen.LokalerChunkClearAktiv)
            {
                Log.Warning("[EinmaligerSpawn] Diagnose: 'localclear' ist in der Config deaktiviert (OFF).");
                return;
            }

            Vector3i pos = player.GetBlockPosition();
            int cx = pos.x >> 4;
            int cz = pos.z >> 4;
            string chunkId = $"{cx}_{cz}";

            if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId) && KillCounter.ToteZombiesProChunk[chunkId] >= 1)
            {
                int kills = KillCounter.ToteZombiesProChunk[chunkId];
                Log.Out($"[EinmaligerSpawn] Diagnose für {player.EntityName}: Dieser Chunk ({chunkId}) ist bereits als gesäubert markiert! Registrierte Kills hier: {kills}");
                return;
            }

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

                    if (KillCounter.ZombieUrsprung.TryGetValue(ent.entityId, out string uChunk) && uChunk == chunkId)
                    {
                        ursprungGefunden = true;
                        istBlockierer = true;
                    }

                    if (enemyAlive != null && enemyAlive.GetAttackTarget() == player)
                    {
                        angreiferGefunden = true;
                        istBlockierer = true;
                    }

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

            if (blockierendeZombies.Count > 0)
            {
                string grund = "";
                if (angreiferGefunden) grund += $"- Der Spieler ({player.EntityName}) ist im Kampf (Wird anvisiert)\n";
                if (bewohnerGefunden) grund += "- Mindestens ein Feind hält sich im Chunk auf\n";
                if (ursprungGefunden) grund += "- Ein Zombie, der zu diesem Chunk gehört, lebt noch\n";

                Log.Warning($"[EinmaligerSpawn] Diagnose für {player.EntityName} in {chunkId} fehlgeschlagen!\nGründe:\n{grund}");
                Log.Warning($"[EinmaligerSpawn] Setze temporäre Radar-Marker auf {blockierendeZombies.Count} störende Feind(e)...");

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

                foreach (Entity feind in blockierendeZombies)
                {
                    NavObjectManager.Instance.RegisterNavObject(magicClassName, feind.transform, "ui_game_symbol_enemy_dot", false);
                }

                return;
            }

            float drosselungsFaktor = ErmittleDrosselungsFaktor(player);
            float benoetigteZeit = 4f * drosselungsFaktor;

            if (spielerTracking.TryGetValue(player.entityId, out TrackingDaten daten))
            {
                if (daten.ChunkId == chunkId)
                {
                    Log.Out($"[EinmaligerSpawn] Diagnose für {player.EntityName}: Alles ruhig! Der Timer steht aktuell bei: {daten.ZeitImChunk:0.0} / {benoetigteZeit:0.0} Sekunden.");
                }
                else
                {
                    Log.Out($"[EinmaligerSpawn] Diagnose für {player.EntityName}: Alles ruhig! Der Spieler hat diesen Chunk aber gerade erst betreten. (0.0 / {benoetigteZeit:0.0} Sekunden).");
                }
            }
        }
    }
}