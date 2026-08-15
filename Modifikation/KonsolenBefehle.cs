using System;
using System.Collections.Generic;
using System.Diagnostics;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.LocalClear;
using EinmaligerSpawn.Network;
using EinmaligerSpawn.PoiTracker;
using EinmaligerSpawn.ZombieSpawner;
using UnityEngine;

namespace EinmaligerSpawn.Commands
{
    // =========================================================================================
    // 1. KLASSE: CLIENT / USER BEFEHLE (Ausführung lokal beim Spieler)
    // =========================================================================================
    public class ConsoleCmdEinmaligerSpawn : ConsoleCmdAbstract
    {
        private const string HilfeText =
            "=== Client / User Befehle ===\n" +
            "Nutze 'es map <on/off/reload>' um das persönliche Karten-Overlay zu steuern oder Marker neu zu laden.\n" +
            "Nutze 'es msg <on/off>' um deine lokalen Chat-Nachrichten der Mod ein- oder auszuschalten.\n" +
            "Nutze 'es progressbuff <on/off/time <sek>/radius <m>>' um den HUD-Fortschritt zu steuern, das Intervall oder den Suchradius anzupassen.\n" +
            "Nutze 'es range [radius] [name]' ODER 'es range [radius] [chunkX] [chunkZ]' um den Säuberungsfortschritt im Umkreis (Standard 120m) zu prüfen.\n" +
            "Nutze 'es where' als Universal-Radar, um den nähesten aktiven Zombie zu markieren.\n" +
            "\n(Für Server-Einstellungen nutze den Befehl 'esa help')";

        public override string[] getCommands()
        {
            return new string[] { "es" };
        }

        public override string getDescription()
        {
            return "Lokale Spieler-Befehle für die 'EinmaligerSpawn'-Mod. Nutze 'es help' für eine Übersicht.";
        }

        public override string getHelp()
        {
            return HilfeText;
        }

        public override int DefaultPermissionLevel
        {
            get { return 1000; } // Jeder darf das nutzen
        }

        public override bool IsExecuteOnClient
        {
            get { return true; } // Zwingt die Engine, das UI lokal zu laden
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();

            if (_params.Count == 0)
            {
                Log.Out(HilfeText);
                return;
            }

            string subCommand = _params[0].ToLower();

            switch (subCommand)
            {
                case "map":
                    CmdMap(_params);
                    break;
                case "message":
                case "msg":
                    CmdMsg(_params);
                    break;
                case "progressbuff":
                    CmdProgressBuff(player, _params);
                    break;
                case "range":
                    CmdRange(player, _params);
                    break;
                case "where":
                    CmdWhere(player);
                    break;
                default:
                    Log.Out(HilfeText);
                    break;
            }
        }

        /// Steuert das persönliche Karten-Overlay oder lädt die Marker neu.
        /// Aufruf: es map <on/off/reload>
        private void CmdMap(List<string> _params)
        {
            if (GameManager.IsDedicatedServer) return;

            if (_params.Count < 2)
            {
                Log.Out("Bitte nutze 'es map on', 'es map off' oder 'es map reload'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                KartenOverlay.SetzeModus(true);
                Log.Out("[ES Map] Deine persönliche Eroberungs-Karte (Overlay) ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                KartenOverlay.SetzeModus(false);
                Log.Out("[ES Map] Deine persönliche Eroberungs-Karte (Overlay) ist nun DEAKTIVIERT.");
            }
            else if (state == "reload")
            {
                KartenOverlay.Reload();
                Log.Out("[ES Map] Deine Karte (Marker) wurde erfolgreich neu geladen.");
            }
            else
            {
                Log.Out("Ungültiger Parameter. Bitte nutze 'es map on', 'es map off' oder 'es map reload'.");
            }
        }

        /// Schaltet die lokalen Chat-Nachrichten der Mod ein oder aus.
        /// Aufruf: es msg <on/off>
        private void CmdMsg(List<string> _params)
        {
            if (GameManager.IsDedicatedServer) return;

            if (_params.Count < 2)
            {
                Log.Out($"Aktueller Status: {(ModEinstellungen.ChatNachrichtenAktiv ? "ON" : "OFF")}. Bitte nutze 'es msg on/off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.ChatNachrichtenAktiv = true;
                ModEinstellungen.Speichern();
                Log.Out("[ES Msg] Lokale Chat-Nachrichten sind nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.ChatNachrichtenAktiv = false;
                ModEinstellungen.Speichern();
                Log.Out("[ES Msg] Lokale Chat-Nachrichten sind nun DEAKTIVIERT.");
            }
        }

        /// Steuert den HUD-Fortschritts-Buff, dessen Intervall oder dessen Suchradius.
        /// Aufruf: es progressbuff <on/off/time <sek>/radius <meter>>
        private void CmdProgressBuff(EntityPlayerLocal player, List<string> _params)
        {
            if (GameManager.IsDedicatedServer) return;

            if (_params.Count < 2)
            {
                Log.Out($"Bitte nutze 'es progressbuff <on/off/time [sek]/radius [meter]>'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.ZeigeLokalenFortschritt = true;
                ModEinstellungen.Speichern();
                Log.Out("[ES Buff] Lokaler Fortschritts-Buff ist nun AKTIVIERT.");

                if (player != null && !player.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
                    player.Buffs.AddBuff("buffEinmaligerSpawnProgress");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.ZeigeLokalenFortschritt = false;
                ModEinstellungen.Speichern();
                Log.Out("[ES Buff] Lokaler Fortschritts-Buff ist nun DEAKTIVIERT.");

                if (player != null && player.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
                    player.Buffs.RemoveBuff("buffEinmaligerSpawnProgress");
            }
            else if (state == "time" || state == "timer")
            {
                if (_params.Count >= 3 && float.TryParse(_params[2].Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float neuerTimer))
                {
                    neuerTimer = Mathf.Clamp(neuerTimer, 0.1f, 10f);
                    ModEinstellungen.BuffUpdateIntervall = neuerTimer;
                    ModEinstellungen.Speichern();
                    Log.Out($"[ES Buff] Das Update-Intervall für den Fortschritts-Buff wurde auf {neuerTimer} Sekunden gesetzt.");
                }
            }
            else if (state == "radius")
            {
                if (_params.Count >= 3 && int.TryParse(_params[2], out int neuerRadius))
                {
                    neuerRadius = Mathf.Clamp(neuerRadius, 16, 1000);
                    ModEinstellungen.ProgressBuffRadius = neuerRadius;
                    ModEinstellungen.Speichern();
                    Log.Out($"[ES Buff] Der Suchradius für den Fortschritts-Buff wurde auf {neuerRadius} Meter gesetzt.");
                }
            }
        }

        /// Prüft den Säuberungsfortschritt im Umkreis.
        /// Aufruf: es range [radius] [name] ODER es range [radius] [chunkX] [chunkZ]
        private void CmdRange(EntityPlayerLocal localPlayer, List<string> _params)
        {
            if (GameManager.IsDedicatedServer) return;

            int radiusMeter = 120;
            EntityPlayer targetPlayer = localPlayer;
            string searchName = null;
            bool useChunkCoords = false;
            int targetChunkX = 0;
            int targetChunkZ = 0;

            if (_params.Count == 2)
            {
                if (int.TryParse(_params[1], out int parsedRadius)) radiusMeter = parsedRadius;
                else searchName = _params[1].ToLower();
            }
            else if (_params.Count == 3)
            {
                if (int.TryParse(_params[1], out int val1) && int.TryParse(_params[2], out int val2))
                {
                    useChunkCoords = true;
                    targetChunkX = val1;
                    targetChunkZ = val2;
                }
                else if (int.TryParse(_params[1], out int val3))
                {
                    radiusMeter = val3;
                    searchName = _params[2].ToLower();
                }
            }

            if (useChunkCoords)
            {
                var ergebnisChunk = KillCounter.BerechneLokalenFortschritt(targetChunkX, targetChunkZ, radiusMeter);
                Log.Out($"=== Spawn-Radar ({radiusMeter}m) für Chunk [{targetChunkX}, {targetChunkZ}] ===\nStatus: {ergebnisChunk.gesperrt}/{ergebnisChunk.gesamt} ({ergebnisChunk.prozent}%)");
                return;
            }

            if (!string.IsNullOrEmpty(searchName))
            {
                foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
                {
                    if (p.EntityName.ToLower().Contains(searchName))
                    {
                        targetPlayer = p;
                        break;
                    }
                }
            }

            if (targetPlayer == null) return;

            Vector3i pos = targetPlayer.GetBlockPosition();
            var ergebnisSpieler = KillCounter.BerechneLokalenFortschritt(pos.x >> 4, pos.z >> 4, radiusMeter);
            Log.Out($"=== Spawn-Radar ({radiusMeter}m) für {targetPlayer.EntityName} ===\nStatus: {ergebnisSpieler.gesperrt}/{ergebnisSpieler.gesamt} ({ergebnisSpieler.prozent}%)");
        }

        /// Markiert als Universal-Radar den nähesten aktiven Zombie in der Umgebung.
        /// Aufruf: es where
        private void CmdWhere(EntityPlayerLocal player)
        {
            if (GameManager.IsDedicatedServer || player == null) return;

            float closestDist = float.MaxValue;
            Entity closestEnemy = null;

            foreach (Entity ent in GameManager.Instance.World.Entities.list)
            {
                if ((ent is EntityEnemy || ent is EntityZombie) && ent.IsAlive())
                {
                    float dist = Vector3.Distance(player.position, ent.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestEnemy = ent;
                    }
                }
            }

            if (closestEnemy != null)
            {
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

                NavObjectManager.Instance.RegisterNavObject(magicClassName, closestEnemy.transform, "ui_game_symbol_enemy_dot", false);
                Log.Out($"[ES Where] Universal-Radar: Nächster Feind (Typ: {closestEnemy.GetType().Name}) ist {Mathf.RoundToInt(closestDist)}m entfernt.");
            }
            else
            {
                Log.Out("[ES Where] Universal-Radar: Keine lebenden Feinde im Umfeld.");
            }
        }
    }


    // =========================================================================================
    // 2. KLASSE: ADMIN / SERVER BEFEHLE (Ausführung auf dem Server)
    // =========================================================================================
    public class ConsoleCmdEinmaligerSpawnAdmin : ConsoleCmdAbstract
    {
        private const string HilfeText =
            "=== Server / Admin Befehle ===\n" +
            "Nutze 'esa cheat_clear [Spieler] [radius] [reset]' um Chunks im Umkreis auf 'gesäubert' zu setzen oder den Status zu löschen.\n" +
            "Nutze 'esa cheat_loud [Spieler/Coords] [Räume]' um schlafende Zombies im nächsten POI (max. 80m) zu wecken und aufzuscheuchen.\n" +
            "Nutze 'esa limit <Zahl>' um das globale Autospawn-Limit für Zombies auf dem Server festzulegen.\n" +
            "Nutze 'esa localclear <on/off/reason [name]>' für den autom. 4s-Clear (on/off) oder zur Fehlerdiagnose (reason).\n" +
            "Nutze 'esa range [Spieler] [radius]' um den geclearten Bereich um einen Spieler zu berechnen.\n" +
            "Nutze 'esa tactical <on/off>' um den serverseitigen Bonus-Clear (Taktischer Kill) ein- oder auszuschalten.\n" +
            "Nutze 'esa timer <Sekunden>' um das serverseitige Autospawn-Überprüfungsintervall anzupassen.";

        public override string[] getCommands()
        {
            return new string[] { "esa" };
        }

        public override string getDescription()
        {
            return "Server-Befehle für die 'EinmaligerSpawn'-Mod. Nutze 'esa help' für eine Übersicht.";
        }

        public override string getHelp()
        {
            return HilfeText;
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count == 0)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output(HilfeText);
                return;
            }

            string subCommand = _params[0].ToLower();

            switch (subCommand)
            {
                case "cheat_clear":
                    CmdCheatClear(_params, _senderInfo);
                    break;
                case "cheat_loud":
                case "loud":
                    CmdCheatLoud(_params, _senderInfo);
                    break;
                case "limit":
                    CmdLimit(_params, _senderInfo);
                    break;
                case "localclear":
                case "walkclear":
                    CmdLocalClear(_params, _senderInfo);
                    break;
                case "range":
                    CmdRangeAdmin(_params, _senderInfo);
                    break;
                case "tactical":
                case "taktik":
                    CmdTactical(_params, _senderInfo);
                    break;
                case "time":
                case "timer":
                    CmdTimer(_params, _senderInfo);
                    break;
                default:
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output(HilfeText);
                    break;
            }
        }

        /// Zwingt das nächste POI, seine schlafenden Zombies zu wecken und auf den Spieler zu hetzen
        /// Aufruf: esa cheat_loud [Spielername/X Z] [AnzahlRäume]
        private void CmdCheatLoud(List<string> _params, CommandSenderInfo _senderInfo)
        {
            int maxRaeume = 1;
            EntityPlayer targetPlayer = null;
            bool useCoords = false;
            int targetX = 0, targetZ = 0;
            string searchName = null;

            // Parameter Parsing
            if (_params.Count >= 4) // esa cheat_loud X Z Räume
            {
                if (int.TryParse(_params[1], out targetX) && int.TryParse(_params[2], out targetZ))
                {
                    useCoords = true;
                    int.TryParse(_params[3], out maxRaeume);
                }
            }
            else if (_params.Count == 3) // esa cheat_loud X Z (1 Raum) ODER esa cheat_loud Name Räume
            {
                if (int.TryParse(_params[1], out targetX) && int.TryParse(_params[2], out targetZ))
                {
                    useCoords = true;
                    maxRaeume = 1;
                }
                else
                {
                    searchName = _params[1].ToLower();
                    int.TryParse(_params[2], out maxRaeume);
                }
            }
            else if (_params.Count == 2) // esa cheat_loud Räume ODER esa cheat_loud Name
            {
                if (int.TryParse(_params[1], out maxRaeume))
                {
                    // Ist eine Zahl -> Der Befehl richtet sich an den Sender selbst
                }
                else
                {
                    searchName = _params[1].ToLower();
                    maxRaeume = 1; // Default
                }
            }

            // Spielerauflösung (falls keine Koordinaten genutzt werden)
            if (!useCoords)
            {
                if (!string.IsNullOrEmpty(searchName))
                {
                    foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
                    {
                        if (p.EntityName.ToLower().Contains(searchName))
                        {
                            targetPlayer = p;
                            break;
                        }
                    }
                    if (targetPlayer == null)
                    {
                        string msg = $"[ESa loud] Konnte Spieler '{searchName}' nicht finden.";
                        SingletonMonoBehaviour<SdtdConsole>.Instance.Output(msg);
                        Log.Out(msg);
                        return;
                    }
                }
                else if (_senderInfo.RemoteClientInfo != null)
                {
                    GameManager.Instance.World.Players.dict.TryGetValue(_senderInfo.RemoteClientInfo.entityId, out targetPlayer);
                }
                else
                {
                    targetPlayer = GameManager.Instance.World.GetPrimaryPlayer();
                }

                if (targetPlayer == null)
                {
                    string msg = "[ESa loud] Dedicated Server erfordert einen Spielernamen oder Koordinaten!";
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output(msg);
                    Log.Out(msg);
                    return;
                }
            }

            Vector2 startPos2D = useCoords ? new Vector2(targetX, targetZ) : new Vector2(targetPlayer.position.x, targetPlayer.position.z);

            // Den nächsten aktiven POI finden
            DynamicPrefabDecorator decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            if (decorator == null) return;

            List<PrefabInstance> allPois = new List<PrefabInstance>();
            decorator.GetPOIPrefabs(allPois);

            PrefabInstance closestPoi = null;
            float closestDistSq = float.MaxValue;

            foreach (PrefabInstance poi in allPois)
            {
                // Ignoriere Gebäude, die wir bereits endgültig gecleart haben
                if (PoiDatenbank.IstGecleart(poi.id)) continue;
                if (poi.sleeperVolumes == null || poi.sleeperVolumes.Count == 0) continue;

                // Überprüfen, ob es laut Vanilla noch aktive Räume gibt
                bool hasUncleared = false;
                foreach (SleeperVolume vol in poi.sleeperVolumes)
                {
                    if (!vol.wasCleared)
                    {
                        hasUncleared = true;
                        break;
                    }
                }
                if (!hasUncleared) continue;

                // Distanz berechnen
                float minX = poi.boundingBoxPosition.x;
                float maxX = minX + poi.boundingBoxSize.x;
                float minZ = poi.boundingBoxPosition.z;
                float maxZ = minZ + poi.boundingBoxSize.z;

                float dx = Mathf.Max(0, Mathf.Max(minX - startPos2D.x, startPos2D.x - maxX));
                float dz = Mathf.Max(0, Mathf.Max(minZ - startPos2D.y, startPos2D.y - maxZ));

                float distSq = dx * dx + dz * dz;

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestPoi = poi;
                }
            }

            if (closestPoi == null)
            {
                string msg = "[ESa loud] Kein aktiver POI auf der Karte gefunden.";
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output(msg);
                Log.Out(msg);
                return;
            }

            // Harte Grenze: Maximal 80 Meter
            if (closestDistSq > 80f * 80f)
            {
                float actualDist = Mathf.Sqrt(closestDistSq);
                string msg = $"[ESa loud] Abbruch: Der nächste aktive POI '{closestPoi.name}' ist {actualDist:0}m entfernt (Max. 80m erlaubt). Geh näher ran!";
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output(msg);
                Log.Out(msg);
                return;
            }

            // Die Ausführung: Räume aktivieren
            int triggeredRooms = 0;

            foreach (SleeperVolume vol in closestPoi.sleeperVolumes)
            {
                if (vol.wasCleared) continue;

                // false = triggert die normale Vanilla-Spawn-Warteschlange (inklusive Auto-Clean bei 0 Spawns)
                vol.TouchGroup(GameManager.Instance.World, targetPlayer, false);
                triggeredRooms++;

                if (maxRaeume > 0 && triggeredRooms >= maxRaeume)
                {
                    break;
                }
            }

            // AGGRO-SCHLEIFE: Weckt zusätzlich alle bereits existierenden (physisch vorhandenen) Zombies im POI
            Bounds poiBounds = new Bounds(
                new Vector3(closestPoi.boundingBoxPosition.x + closestPoi.boundingBoxSize.x / 2f,
                            closestPoi.boundingBoxPosition.y + closestPoi.boundingBoxSize.y / 2f,
                            closestPoi.boundingBoxPosition.z + closestPoi.boundingBoxSize.z / 2f),
                new Vector3(closestPoi.boundingBoxSize.x, closestPoi.boundingBoxSize.y, closestPoi.boundingBoxSize.z)
            );

            int additionalAggro = 0;
            foreach (Entity ent in GameManager.Instance.World.Entities.list)
            {
                if (ent is EntityAlive enemy && (ent is EntityEnemy || ent is EntityZombie) && enemy.IsAlive())
                {
                    if (poiBounds.Contains(enemy.position))
                    {
                        enemy.IsSleeping = false;
                        if (targetPlayer != null)
                        {
                            enemy.SetAttackTarget(targetPlayer, 1200);
                        }
                        additionalAggro++;
                    }
                }
            }

            string targetName = useCoords ? $"Koordinate [{targetX}, {targetZ}]" : targetPlayer?.EntityName ?? "Unbekannt";
            string finalMsg = $"[ESa loud] Cheat Loud: {triggeredRooms} Raum/Räume im POI '{closestPoi.name}' für {targetName} getriggert! ({additionalAggro} bereits existierende Feinde aufgeweckt). Asynchroner Spawnvorgang gestartet.";
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output(finalMsg);
            Log.Out(finalMsg);

            // Automatisches Radar verzögert ausführen, damit die Zombies Zeit zum Spawnen haben
            GameManager.Instance.StartCoroutine(VerzoegertesWhere(_senderInfo));
        }

        // Hilfsmethode von CmdCheatLoud, um den Radar-Befehl nach einer kurzen Verzögerung auszuführen
        private System.Collections.IEnumerator VerzoegertesWhere(CommandSenderInfo _senderInfo)
        {
            // Wir warten 2 Sekunden, bis die Vanilla UpdateSpawn-Warteschlange abgearbeitet ist
            yield return new UnityEngine.WaitForSeconds(2f);

            // Führt den Befehl über das Spielsystem exakt so aus, als hätte der Sender ihn selbst eingetippt
            SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync("es where", _senderInfo.RemoteClientInfo);
        }

        /// Setzt Chunks im Umkreis auf gesäubert oder löscht den Status.
        /// Aufruf: esa cheat_clear [Spieler] [Radius] [clear/reset]
        private void CmdCheatClear(List<string> _params, CommandSenderInfo _senderInfo)
        {
            EntityPlayer targetPlayer = null;
            int radiusMeter = 20;
            bool isReset = false;
            string searchName = null;

            for (int i = 1; i < _params.Count; i++)
            {
                string p = _params[i].ToLower();

                if (p == "clear" || p == "reset")
                {
                    isReset = (p == "reset");
                }
                else if (int.TryParse(p, out int parsedRadius))
                {
                    radiusMeter = Mathf.Clamp(parsedRadius, 1, 256);
                }
                else
                {
                    searchName = p;
                }
            }

            if (!string.IsNullOrEmpty(searchName))
            {
                foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
                {
                    if (p.EntityName.ToLower().Contains(searchName))
                    {
                        targetPlayer = p;
                        break;
                    }
                }

                if (targetPlayer == null)
                {
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[ESa range] Konnte den Spieler '{searchName}' nicht finden.");
                    return;
                }
            }
            else if (_senderInfo.RemoteClientInfo != null)
            {
                GameManager.Instance.World.Players.dict.TryGetValue(_senderInfo.RemoteClientInfo.entityId, out targetPlayer);
            }
            else
            {
                targetPlayer = GameManager.Instance.World.GetPrimaryPlayer();
            }

            if (targetPlayer == null)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ESa range] Server-Konsole benötigt einen Spielernamen! Nutzung: 'esa cheat_clear [Spielername] [Radius] [clear/reset]'");
                return;
            }

            Vector3i playerPos = targetPlayer.GetBlockPosition();
            int playerChunkX = playerPos.x >> 4;
            int playerChunkZ = playerPos.z >> 4;
            int chunkSuchRadius = Mathf.CeilToInt((float)radiusMeter / 16f);
            int maxDistSq = radiusMeter * radiusMeter;

            int newlyModified = 0;
            int totalChecked = 0;

            for (int cx = playerChunkX - chunkSuchRadius; cx <= playerChunkX + chunkSuchRadius; cx++)
            {
                for (int cz = playerChunkZ - chunkSuchRadius; cz <= playerChunkZ + chunkSuchRadius; cz++)
                {
                    int minX = cx * 16;
                    int maxX = minX + 15;
                    int minZ = cz * 16;
                    int maxZ = minZ + 15;

                    int dx = Math.Max(0, Math.Max(minX - playerPos.x, playerPos.x - maxX));
                    int dz = Math.Max(0, Math.Max(minZ - playerPos.z, playerPos.z - maxZ));

                    if (dx * dx + dz * dz <= maxDistSq)
                    {
                        totalChecked++;
                        string chunkId = $"{cx}_{cz}";

                        if (isReset)
                        {
                            if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                            {
                                KillCounter.ToteZombiesProChunk.Remove(chunkId);
                                newlyModified++;
                            }
                        }
                        else
                        {
                            if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                                KillCounter.ToteZombiesProChunk[chunkId] = 0;

                            KillCounter.ToteZombiesProChunk[chunkId]++;
                            newlyModified++;

                            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
                        }
                    }
                }
            }

            string actionText = isReset ? "reaktiviert (Reset)" : "neu ausgerottet (Clear)";
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[ESa range] Ich habe {totalChecked} Chunks im Umkreis von {targetPlayer.EntityName} geprüft und {newlyModified} {actionText}.");
        }

        /// Legt das globale Autospawn-Limit für Zombies auf dem Server fest.
        /// Aufruf: esa limit <Zahl>
        private void CmdLimit(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count < 2 || !int.TryParse(_params[1], out int neuesLimit))
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"Aktuelles Limit: {ModEinstellungen.GlobalesZombieLimit}. Bitte nutze 'esa limit <Zahl>'.");
                return;
            }

            neuesLimit = Mathf.Max(1, neuesLimit);
            ModEinstellungen.GlobalesZombieLimit = neuesLimit;
            ModEinstellungen.Speichern();
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[ESa limit] Globales Autospawn-Limit wurde auf {neuesLimit} gesetzt.");
        }

        /// Steuert den automatischen 4s-Clear oder startet eine Fehlerdiagnose.
        /// Aufruf: esa localclear <on/off/reason [Spieler]>
        private void CmdLocalClear(List<string> _params, CommandSenderInfo _senderInfo)
        {
            string currentStatus = ModEinstellungen.LokalerChunkClearAktiv ? "ON" : "OFF";
            bool fromUI = false;
            string searchName = null;

            // Parameter filtern, um "ui" zu erkennen
            List<string> cleanParams = new List<string>();
            for (int i = 1; i < _params.Count; i++)
            {
                string p = _params[i].ToLower();
                if (p == "ui")
                {
                    fromUI = true;
                }
                else
                {
                    cleanParams.Add(p);
                }
            }

            if (cleanParams.Count == 0)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"Aktueller Status: {currentStatus}. Bitte nutze 'esa localclear on/off/reason'.");
                return;
            }

            string state = cleanParams[0];

            if (state == "reason" || state == "grund")
            {
                EntityPlayer targetPlayer = null;
                if (cleanParams.Count >= 2)
                {
                    searchName = cleanParams[1].ToLower();
                    foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
                    {
                        if (p.EntityName.ToLower().Contains(searchName))
                        {
                            targetPlayer = p;
                            break;
                        }
                    }
                }
                else
                {
                    targetPlayer = GameManager.Instance.World.GetPrimaryPlayer();
                }

                if (targetPlayer != null)
                {
                    string msg = $"[ESa localclear] Starte Diagnose für Spieler: {targetPlayer.EntityName}";
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output(msg);

                    // Aufruf der Diagnose mit dem neuen fromUI-Parameter
                    LokalenChunkSaeubern.Diagnose(targetPlayer, fromUI);
                }
                else
                {
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[ESa localclear] Konnte den Spieler '{searchName}' nicht finden.");
                }
                return;
            }

            if (state == "on" || state == "true")
            {
                ModEinstellungen.LokalerChunkClearAktiv = true;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ESa localclear] Lokaler Chunk-Clear ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.LokalerChunkClearAktiv = false;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ESa localclear] Lokaler Chunk-Clear ist nun DEAKTIVIERT.");
            }
        }

        /// Berechnet den geclearten Bereich um einen bestimmten Spieler.
        /// Aufruf: esa range [Spieler] [Radius]
        private void CmdRangeAdmin(List<string> _params, CommandSenderInfo _senderInfo)
        {
            int radiusMeter = 120;
            string searchName = null;
            EntityPlayer targetPlayer = null;
            bool fromUI = false;

            for (int i = 1; i < _params.Count; i++)
            {
                string p = _params[i].ToLower();

                if (p == "ui")
                {
                    fromUI = true;
                }
                else if (int.TryParse(p, out int parsedRadius))
                {
                    radiusMeter = Mathf.Clamp(parsedRadius, 1, 10000);
                }
                else
                {
                    searchName = p;
                }
            }

            if (!string.IsNullOrEmpty(searchName))
            {
                foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
                {
                    if (p.EntityName.ToLower().Contains(searchName))
                    {
                        targetPlayer = p;
                        break;
                    }
                }

                if (targetPlayer == null)
                {
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[ESa range] Konnte den Spieler '{searchName}' nicht finden.");
                    return;
                }
            }
            else if (_senderInfo.RemoteClientInfo != null)
            {
                GameManager.Instance.World.Players.dict.TryGetValue(_senderInfo.RemoteClientInfo.entityId, out targetPlayer);
            }
            else
            {
                targetPlayer = GameManager.Instance.World.GetPrimaryPlayer();
            }

            if (targetPlayer == null)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ESa range] Server-Konsole benötigt einen Spielernamen! Nutzung: 'esa range [Spielername] [Radius] [ui]'");
                return;
            }

            Vector3i pos = targetPlayer.GetBlockPosition();
            var ergebnis = KillCounter.BerechneLokalenFortschritt(pos.x >> 4, pos.z >> 4, radiusMeter);

            string msg1 = $"Spieler {targetPlayer.EntityName} hat um sich herum ({radiusMeter}m)";
            string msg2 = $"{ergebnis.gesperrt} von {ergebnis.gesamt} ({ergebnis.prozent}%) gecleart.";

            // F1-Konsole (wird IMMER gemacht)
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output(msg1);
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output(msg2);

            // Globaler Spielchat (NUR wenn aus dem Menü aufgerufen, dank "fromUI" Erkennung)
            if (fromUI)
            {
                GameManager.Instance.ChatMessageServer(
                    null,
                    EChatType.Global,
                    -1,
                    msg1,
                    null,
                    EMessageSender.Server,
                    GeneratedTextManager.BbCodeSupportMode.Supported
                );

                GameManager.Instance.ChatMessageServer(
                    null,
                    EChatType.Global,
                    -1,
                    msg2,
                    null,
                    EMessageSender.Server,
                    GeneratedTextManager.BbCodeSupportMode.Supported
                );
            }
        }

        /// Schaltet den serverseitigen Bonus-Clear (Taktischer Kill) ein oder aus.
        /// Aufruf: esa tactical <on/off>
        private void CmdTactical(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count < 2)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"Aktueller Status: {(ModEinstellungen.TaktischerKillAktiv ? "ON" : "OFF")}. Bitte nutze 'esa tactical on/off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.TaktischerKillAktiv = true;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ESa tactical] Taktischer Kill ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.TaktischerKillAktiv = false;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ESa tactical] Taktischer Kill ist nun DEAKTIVIERT.");
            }
        }

        /// Passt das serverseitige Autospawn-Überprüfungsintervall an.
        /// Aufruf: esa timer <Sekunden>
        private void CmdTimer(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count < 2 || !float.TryParse(_params[1], out float neuerTimer))
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"Aktueller Timer: {ModEinstellungen.SpawnCheckIntervall}s. Bitte nutze 'esa timer <Sekunden>'.");
                return;
            }

            neuerTimer = Mathf.Max(1f, neuerTimer);
            ModEinstellungen.SpawnCheckIntervall = neuerTimer;
            ModEinstellungen.Speichern();
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[ESa timer] Autospawn-Intervall wurde auf {neuerTimer} Sekunden gesetzt.");
        }
    }
}