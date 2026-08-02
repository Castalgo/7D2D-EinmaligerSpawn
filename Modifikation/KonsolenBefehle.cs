using System;
using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.LocalClear;
using EinmaligerSpawn.Network;
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
                Log.Out("[EinmaligerSpawn] Deine persönliche Eroberungs-Karte (Overlay) ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                KartenOverlay.SetzeModus(false);
                Log.Out("[EinmaligerSpawn] Deine persönliche Eroberungs-Karte (Overlay) ist nun DEAKTIVIERT.");
            }
            else if (state == "reload")
            {
                KartenOverlay.Reload();
                Log.Out("[EinmaligerSpawn] Deine Karte (Marker) wurde erfolgreich neu geladen.");
            }
            else
            {
                Log.Out("Ungültiger Parameter. Bitte nutze 'es map on', 'es map off' oder 'es map reload'.");
            }
        }

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
                Log.Out("[EinmaligerSpawn] Lokaler Fortschritts-Buff ist nun AKTIVIERT.");

                if (player != null && !player.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
                    player.Buffs.AddBuff("buffEinmaligerSpawnProgress");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.ZeigeLokalenFortschritt = false;
                ModEinstellungen.Speichern();
                Log.Out("[EinmaligerSpawn] Lokaler Fortschritts-Buff ist nun DEAKTIVIERT.");

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
                    Log.Out($"[EinmaligerSpawn] Das Update-Intervall für den Fortschritts-Buff wurde auf {neuerTimer} Sekunden gesetzt.");
                }
            }
            else if (state == "radius")
            {
                if (_params.Count >= 3 && int.TryParse(_params[2], out int neuerRadius))
                {
                    neuerRadius = Mathf.Clamp(neuerRadius, 16, 1000);
                    ModEinstellungen.ProgressBuffRadius = neuerRadius;
                    ModEinstellungen.Speichern();
                    Log.Out($"[EinmaligerSpawn] Der Suchradius für den Fortschritts-Buff wurde auf {neuerRadius} Meter gesetzt.");
                }
            }
        }

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
                Log.Out($"[ES Spawner] Universal-Radar: Nächster Feind (Typ: {closestEnemy.GetType().Name}) ist {Mathf.RoundToInt(closestDist)}m entfernt.");
            }
            else
            {
                Log.Out("[ES Spawner] Universal-Radar: Keine lebenden Feinde im Umfeld.");
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
            "Nutze 'esa limit <Zahl>' um das globale Autospawn-Limit für Zombies auf dem Server festzulegen.\n" +
            "Nutze 'esa localclear <on/off/reason [name]>' für den autom. 4s-Clear (on/off) oder zur Fehlerdiagnose (reason).\n" +
            "Nutze 'esa msg <on/off>' um die globalen Chat-Nachrichten der Mod für alle ein- oder auszuschalten.\n" +
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

        // IsExecuteOnClient wird NICHT überschrieben -> Standard: false (läuft auf dem Server)
        // DefaultPermissionLevel wird NICHT überschrieben -> Standard: 0 (nur Admins)

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
                case "limit":
                    CmdLimit(_params, _senderInfo);
                    break;
                case "localclear":
                case "walkclear":
                    CmdLocalClear(_params, _senderInfo);
                    break;
                case "message":
                case "msg":
                    CmdMsg(_params, _senderInfo);
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

        private void CmdCheatClear(List<string> _params, CommandSenderInfo _senderInfo)
        {
            EntityPlayer targetPlayer = null;
            int radiusMeter = 20;
            bool isReset = false;
            string searchName = null;

            // --- Intelligente Parameter-Auswertung ---
            // Startet bei Index 1, da Index 0 der Befehl "cheat_clear" ist
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
                    // Wenn es weder "clear/reset" noch eine Zahl ist, ist es der Spielername
                    searchName = p;
                }
            }

            // --- Spieler ermitteln ---
            if (!string.IsNullOrEmpty(searchName))
            {
                // Name wurde übergeben -> Suche den Spieler auf dem Server
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
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[EinmaligerSpawn] Konnte den Spieler '{searchName}' nicht finden.");
                    return;
                }
            }
            else if (_senderInfo.RemoteClientInfo != null)
            {
                // Kein Name übergeben, aber ein Remote-Admin (Mitspieler) hat den Befehl gesendet
                GameManager.Instance.World.Players.dict.TryGetValue(_senderInfo.RemoteClientInfo.entityId, out targetPlayer);
            }
            else
            {
                // Fallback für den lokalen Host
                targetPlayer = GameManager.Instance.World.GetPrimaryPlayer();
            }

            // Sicherheitsprüfung (z. B. wenn die dedizierte Konsole keinen Namen angibt)
            if (targetPlayer == null)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[EinmaligerSpawn] Server-Konsole benötigt einen Spielernamen! Nutzung: 'esa cheat_clear [Spielername] [Radius] [clear/reset]'");
                return;
            }

            // --- Die eigentliche Logik ---
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

                    // Kreisberechnung für die Entfernung
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
                            AutoSpawner.RemoveChunkFromCache(chunkId);
                        }
                        else
                        {
                            if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                                KillCounter.ToteZombiesProChunk[chunkId] = 0;

                            KillCounter.ToteZombiesProChunk[chunkId]++;
                            newlyModified++;

                            // Netzwerk-Update an alle Clients
                            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageChunkSync>().SetupForLive(chunkId));
                        }
                    }
                }
            }

            string actionText = isReset ? "reaktiviert (Reset)" : "neu ausgerottet (Clear)";
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[EinmaligerSpawn] Ich habe {totalChecked} Chunks im Umkreis von {targetPlayer.EntityName} geprüft und {newlyModified} {actionText}.");
        }

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
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[EinmaligerSpawn] Globales Autospawn-Limit wurde auf {neuesLimit} gesetzt.");
        }

        private void CmdLocalClear(List<string> _params, CommandSenderInfo _senderInfo)
        {
            string currentStatus = ModEinstellungen.LokalerChunkClearAktiv ? "ON" : "OFF";

            if (_params.Count < 2)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"Aktueller Status: {currentStatus}. Bitte nutze 'esa localclear on/off/reason'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "reason" || state == "grund")
            {
                EntityPlayer targetPlayer = null;
                if (_params.Count >= 3)
                {
                    string searchName = _params[2].ToLower();
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
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[EinmaligerSpawn] Starte Diagnose für Spieler: {targetPlayer.EntityName}");
                    LokalenChunkSaeubern.Diagnose(targetPlayer);
                }
                return;
            }

            if (state == "on" || state == "true")
            {
                ModEinstellungen.LokalerChunkClearAktiv = true;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[EinmaligerSpawn] Lokaler Chunk-Clear ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.LokalerChunkClearAktiv = false;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[EinmaligerSpawn] Lokaler Chunk-Clear ist nun DEAKTIVIERT.");
            }
        }

        private void CmdMsg(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count < 2)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"Aktueller Status: {(ModEinstellungen.ChatNachrichtenAktiv ? "ON" : "OFF")}. Bitte nutze 'esa msg on/off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.ChatNachrichtenAktiv = true;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[EinmaligerSpawn] Globale Chat-Nachrichten sind nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.ChatNachrichtenAktiv = false;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[EinmaligerSpawn] Globale Chat-Nachrichten sind nun DEAKTIVIERT.");
            }
        }

        private void CmdRangeAdmin(List<string> _params, CommandSenderInfo _senderInfo)
        {
            int radiusMeter = 120;
            string searchName = null;
            EntityPlayer targetPlayer = null;

            // --- Intelligente Parameter-Auswertung ---
            // Startet bei Index 1, da Index 0 der Befehl "range" ist
            for (int i = 1; i < _params.Count; i++)
            {
                string p = _params[i].ToLower();

                if (int.TryParse(p, out int parsedRadius))
                {
                    radiusMeter = Mathf.Clamp(parsedRadius, 1, 10000);
                }
                else
                {
                    // Wenn es keine Zahl ist, muss es der Spielername sein
                    searchName = p;
                }
            }

            // --- Spieler ermitteln ---
            if (!string.IsNullOrEmpty(searchName))
            {
                // Name wurde übergeben -> Suche den Spieler auf dem Server
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
                    SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[EinmaligerSpawn] Konnte den Spieler '{searchName}' nicht finden.");
                    return;
                }
            }
            else if (_senderInfo.RemoteClientInfo != null)
            {
                // Kein Name übergeben, aber ein Remote-Admin (Mitspieler) hat den Befehl gesendet
                GameManager.Instance.World.Players.dict.TryGetValue(_senderInfo.RemoteClientInfo.entityId, out targetPlayer);
            }
            else
            {
                // Fallback für den lokalen Host (Singleplayer / lokaler Server)
                targetPlayer = GameManager.Instance.World.GetPrimaryPlayer();
            }

            // Sicherheitsprüfung (wenn die dedizierte Konsole keinen Namen angibt)
            if (targetPlayer == null)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[EinmaligerSpawn] Server-Konsole benötigt einen Spielernamen! Nutzung: 'esa range [Spielername] [Radius]'");
                return;
            }

            // --- Die eigentliche Logik ---
            Vector3i pos = targetPlayer.GetBlockPosition();
            var ergebnis = KillCounter.BerechneLokalenFortschritt(pos.x >> 4, pos.z >> 4, radiusMeter);

            SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"=== Admin Spawn-Radar ({radiusMeter}m) für {targetPlayer.EntityName} ===");
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"Status: {ergebnis.gesperrt}/{ergebnis.gesamt} ({ergebnis.prozent}%)");
        }

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
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[EinmaligerSpawn] Taktischer Kill ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.TaktischerKillAktiv = false;
                ModEinstellungen.Speichern();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[EinmaligerSpawn] Taktischer Kill ist nun DEAKTIVIERT.");
            }
        }

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
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output($"[EinmaligerSpawn] Autospawn-Intervall wurde auf {neuerTimer} Sekunden gesetzt.");
        }
    }
}