using System;
using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using UnityEngine;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.LocalClear;
using EinmaligerSpawn.ZombieSpawner;

namespace EinmaligerSpawn.Commands
{
    public class ConsoleCmdEinmaligerSpawn : ConsoleCmdAbstract
    {
        // -----------------------------------------------------------------
        // Die zentrale Variable für den Hilfetext (Konstante)
        // -----------------------------------------------------------------
        private const string HilfeText =
    "=== Client / User Befehle ===\n" +
    "Nutze 'es map <on/off/reload>' um das persönliche Karten-Overlay zu steuern oder Marker neu zu laden.\n" +
    "Nutze 'es progressbuff <on/off/time <sek>/radius <m>>' um den HUD-Fortschritt zu steuern, das Intervall oder den Suchradius anzupassen.\n" +
    "Nutze 'es range [radius] [name]' ODER 'es range [radius] [chunkX] [chunkZ]' um den Säuberungsfortschritt im Umkreis (Standard 120m) zu prüfen.\n" +
    "Nutze 'es where' als Universal-Radar, um den nähesten aktiven Zombie zu markieren.\n" +
    "\n=== Server / Admin Befehle ===\n" +
    "Nutze 'es cheat_clear [radius] [reset]' um Chunks im Umkreis auf 'gesäubert' zu setzen oder den Status zu löschen (Reset).\n" +
    "Nutze 'es limit <Zahl>' um das globale Autospawn-Limit für Zombies auf dem Server festzulegen.\n" +
    "Nutze 'es localclear <on/off/reason [name]>' für den autom. 4s-Clear (on/off) oder zur Fehlerdiagnose bei einem Spieler (reason).\n" +
    "Nutze 'es msg <on/off>' um die globalen Chat-Nachrichten der Mod für alle ein- oder auszuschalten.\n" +
    "Nutze 'es tactical <on/off>' um den serverseitigen Bonus-Clear (Taktischer Kill) ein- oder auszuschalten.\n" +
    "Nutze 'es timer <Sekunden>' um das serverseitige Autospawn-Überprüfungsintervall anzupassen.";

        public override string[] getCommands()
        {
            return new string[] { "es" };
        }

        public override string getDescription()
        {
            return "Steuert die Einstellungen und Werkzeuge der 'EinmaligerSpawn'-Mod. Nutze 'es help' zur Übersicht aller Commands.";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            // Wir holen uns den lokalen Spieler (auf einem Dedicated Server ist dieser einfach null).
            // WICHTIG: Hier kein 'if (player == null) return;' mehr, sonst sperren wir den Server aus!
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();

            if (_params.Count == 0)
            {
                PrintHelp();
                return;
            }

            string subCommand = _params[0].ToLower();

            // Alphabetisch sortiertes Switch-Statement
            switch (subCommand)
            {
                case "cheat_clear":
                    CmdCheatClear(_params, _senderInfo);
                    break;
                case "limit":
                    CmdLimit(_params);
                    break;
                case "localclear":
                case "walkclear":
                    CmdLocalClear(_params);
                    break;
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
                case "tactical":
                case "taktik":
                    CmdTactical(_params);
                    break;
                case "time":
                case "timer":
                    CmdTimer(_params);
                    break;
                case "where":
                    CmdWhere(player);
                    break;
                default:
                    PrintHelp();
                    break;
            }
        }

        private void PrintHelp()
        {
            Log.Out(HilfeText);
        }

        public override string getHelp()
        {
            return HilfeText;
        }

        // =================================================================
        // HELPER METHODEN (Alphabetisch sortiert)
        // =================================================================

        // -----------------------------------------------------------------
        // BEFEHL: es cheat_clear [radius] [reset]
        // -----------------------------------------------------------------
        // Dieser Befehl ändert die Server-Datenbank (KillCounter). 
        private void CmdCheatClear(List<string> _params, CommandSenderInfo _senderInfo)
        {
            
            // 1. Server only. Client rauswerfen.
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Admin-Befehl kann nur vom Server oder Host ausgeführt werden.");
                return;
            }

            // 2. ZIEL-SPIELER ERMITTELN (Wer hat den Befehl gesendet?)
            EntityPlayer targetPlayer = null;

            if (_senderInfo.RemoteClientInfo != null)
            {
                // Ein Remote-Admin (Mitspieler) hat den Befehl gesendet
                int entityId = _senderInfo.RemoteClientInfo.entityId;
                GameManager.Instance.World.Players.dict.TryGetValue(entityId, out targetPlayer);
            }
            else
            {
                // Der Host selbst (oder der Server über die Konsole) hat den Befehl gesendet
                targetPlayer = GameManager.Instance.World.GetPrimaryPlayer();
            }

            if (targetPlayer == null)
            {
                Log.Warning("[EinmaligerSpawn] Befehl fehlgeschlagen: Konnte die Position des ausführenden Spielers nicht ermitteln.");
                return;
            }

            int radiusMeter = 20;
            bool isReset = false;

            // 1. Parameter: Radius (optional)
            if (_params.Count > 1)
            {
                if (int.TryParse(_params[1], out int parsedRadius))
                {
                    radiusMeter = Mathf.Clamp(parsedRadius, 1, 256);
                }
            }

            // 2. Parameter: Modus (optional, checkt auf "reset")
            if (_params.Count > 2)
            {
                if (_params[2].ToLower() == "reset")
                {
                    isReset = true;
                }
            }

            Vector3i playerPos = targetPlayer.GetBlockPosition();
            int px = playerPos.x;
            int pz = playerPos.z;

            int playerChunkX = px >> 4;
            int playerChunkZ = pz >> 4;

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

                    int dx = Math.Max(0, Math.Max(minX - px, px - maxX));
                    int dz = Math.Max(0, Math.Max(minZ - pz, pz - maxZ));

                    if (dx * dx + dz * dz <= maxDistSq)
                    {
                        totalChecked++;
                        string chunkId = $"{cx}_{cz}";

                        if (isReset)
                        {
                            // --- RESET LOGIK ---
                            if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                            {
                                KillCounter.ToteZombiesProChunk.Remove(chunkId);
                                newlyModified++;
                            }
                            AutoSpawner.RemoveChunkFromCache(chunkId);
                        }
                        else
                        {
                            // --- CLEAR LOGIK ---
                            if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                            {
                                KillCounter.ToteZombiesProChunk[chunkId] = 0;
                            }
                            KillCounter.ToteZombiesProChunk[chunkId]++;
                            newlyModified++;

                            // Netzwerk-Sync: Wir informieren alle Clients, dass dieser Chunk jetzt neu ausgerottet ist
                            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(new EinmaligerSpawn.Network.NetPackageChunkSync(chunkId));
                        }
                    }
                }
            }

            string actionText = isReset ? "reaktiviert (Reset)" : "neu ausgerottet (Clear)";
            string modeText = isReset ? "RESET" : "CLEAR";

            Log.Out($"=== Cheat Clear ({radiusMeter}m) - Modus: {modeText} ===");
            Log.Warning($"[ES Spawner] Ich habe {totalChecked} Chunks geprüft und {newlyModified} {actionText}.");
        }

        // -----------------------------------------------------------------
        // BEFEHL: es limit <Zahl>
        // -----------------------------------------------------------------
        private void CmdLimit(List<string> _params)
        {
            // SICHERHEITS-CHECK: Nur der Server darf globale Spielregeln ändern
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Admin-Befehl kann nur vom Server oder Host ausgeführt werden.");
                return;
            }

            if (_params.Count < 2 || !int.TryParse(_params[1], out int neuesLimit))
            {
                Log.Warning($"Aktuelles Limit: {ModEinstellungen.GlobalesZombieLimit}. Bitte nutze 'es limit <Zahl>', z.B. 'es limit 18'.");
                return;
            }

            neuesLimit = Mathf.Max(1, neuesLimit);
            ModEinstellungen.GlobalesZombieLimit = neuesLimit;
            ModEinstellungen.Speichern();
            Log.Warning($"[EinmaligerSpawn] Globales Autospawn-Limit wurde auf {neuesLimit} gesetzt.");
        }

        // -----------------------------------------------------------------
        // BEFEHL: es localclear / es walkclear <on / off / reason [player]>
        // -----------------------------------------------------------------
        private void CmdLocalClear(List<string> _params)
        {
            // Server only. Client rauswerfen.
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Admin-Befehl kann nur vom Server oder Host ausgeführt werden.");
                return;
            }

            string currentStatus = ModEinstellungen.LokalerChunkClearAktiv ? "ON" : "OFF";

            if (_params.Count < 2)
            {
                Log.Warning($"Aktueller Status (localclear): {currentStatus}. Bitte nutze 'es localclear on', 'off', 'reason' oder 'reason <spielername>'.");
                return;
            }

            string state = _params[1].ToLower();

            // PARAMETER: reason / grund (optional mit Spielername)
            if (state == "reason" || state == "grund")
            {
                EntityPlayer targetPlayer = null;

                // Wurde ein Spielername als 3. Parameter übergeben?
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

                    if (targetPlayer == null)
                    {
                        Log.Warning($"[EinmaligerSpawn] Spieler '{_params[2]}' nicht gefunden. Überprüfe die Schreibweise.");
                        return;
                    }
                }
                else
                {
                    // Kein Name angegeben -> Wir nehmen den Host selbst
                    targetPlayer = GameManager.Instance.World.GetPrimaryPlayer();
                }

                if (targetPlayer != null)
                {
                    Log.Out($"[EinmaligerSpawn] Starte Diagnose für Spieler: {targetPlayer.EntityName}");
                    LokalenChunkSaeubern.Diagnose(targetPlayer);
                }
                return;
            }

            if (state == "on" || state == "true")
            {
                ModEinstellungen.LokalerChunkClearAktiv = true;
                ModEinstellungen.Speichern();
                Log.Warning("[EinmaligerSpawn] Lokaler Chunk-Clear (4s-Präsenz) ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.LokalerChunkClearAktiv = false;
                ModEinstellungen.Speichern();
                Log.Warning("[EinmaligerSpawn] Lokaler Chunk-Clear (4s-Präsenz) ist nun DEAKTIVIERT.");
            }
            else
            {
                Log.Warning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es localclear on', 'off' oder 'reason'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es map <on / off / reload>
        // -----------------------------------------------------------------
        private void CmdMap(List<string> _params)
        {
            // Ein Dedicated Server hat kein eigenes HUD: rauswerfen.
            if (GameManager.IsDedicatedServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Befehl steuert ein lokales UI-Element und ist auf einem reinen Dedicated Server wirkungslos.");
                return;
            }

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

        // -----------------------------------------------------------------
        // BEFEHL: es msg / es message <on / off>
        // -----------------------------------------------------------------
        private void CmdMsg(List<string> _params)
        {
            // Server only. Client rauswerfen.
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Admin-Befehl kann nur vom Server oder Host ausgeführt werden.");
                return;
            }

            string currentStatus = ModEinstellungen.ChatNachrichtenAktiv ? "ON" : "OFF";

            if (_params.Count < 2)
            {
                Log.Warning($"Aktueller Status (msg): {currentStatus}. Bitte nutze 'es msg on' oder 'es msg off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.ChatNachrichtenAktiv = true;
                ModEinstellungen.Speichern();
                Log.Warning("[EinmaligerSpawn] Globale Chat-Nachrichten sind nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.ChatNachrichtenAktiv = false;
                ModEinstellungen.Speichern();
                Log.Warning("[EinmaligerSpawn] Globale Chat-Nachrichten sind nun DEAKTIVIERT.");
            }
            else
            {
                Log.Warning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es msg on' oder 'es msg off'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es progressbuff <on / off / time <sek> / radius <meter>>
        // -----------------------------------------------------------------
        private void CmdProgressBuff(EntityPlayerLocal player, List<string> _params)
        {
            // SICHERHEITS-CHECK: Ein Dedicated Server hat kein lokales HUD
            if (GameManager.IsDedicatedServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Befehl steuert ein lokales UI-Element und ist auf einem Dedicated Server wirkungslos.");
                return;
            }

            string currentStatus = ModEinstellungen.ZeigeLokalenFortschritt ? "ON" : "OFF";
            float currentTimer = ModEinstellungen.BuffUpdateIntervall;
            int currentRadius = ModEinstellungen.ProgressBuffRadius;

            if (_params.Count < 2)
            {
                Log.Warning($"Status: {currentStatus} | Update: {currentTimer}s | Radius: {currentRadius}m. Bitte nutze 'es progressbuff <on/off/time [sek]/radius [meter]>'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.ZeigeLokalenFortschritt = true;
                ModEinstellungen.Speichern();
                Log.Out("[EinmaligerSpawn] Lokaler Fortschritts-Buff ist nun AKTIVIERT.");

                if (player != null && !player.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
                {
                    player.Buffs.AddBuff("buffEinmaligerSpawnProgress");
                }
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.ZeigeLokalenFortschritt = false;
                ModEinstellungen.Speichern();
                Log.Out("[EinmaligerSpawn] Lokaler Fortschritts-Buff ist nun DEAKTIVIERT.");

                if (player != null && player.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
                {
                    player.Buffs.RemoveBuff("buffEinmaligerSpawnProgress");
                }
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
                else
                {
                    Log.Warning("Bitte gib eine gültige Zahl für das Intervall an, z.B. 'es progressbuff time 2.5'.");
                }
            }
            else if (state == "radius")
            {
                if (_params.Count >= 3 && int.TryParse(_params[2], out int neuerRadius))
                {
                    // Begrenzen wir den Radius auf sinnvolle Werte, damit die Engine nicht einfriert
                    neuerRadius = Mathf.Clamp(neuerRadius, 16, 1000);
                    ModEinstellungen.ProgressBuffRadius = neuerRadius;
                    ModEinstellungen.Speichern();
                    Log.Out($"[EinmaligerSpawn] Der Suchradius für den Fortschritts-Buff wurde auf {neuerRadius} Meter gesetzt.");
                }
                else
                {
                    Log.Warning("Bitte gib eine gültige Zahl für den Radius an, z.B. 'es progressbuff radius 150'.");
                }
            }
            else
            {
                // Fallback: Wenn der Nutzer nur eine Zahl eintippt (alte Logik für das Intervall)
                if (float.TryParse(state.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float fallbackTimer))
                {
                    fallbackTimer = Mathf.Clamp(fallbackTimer, 0.1f, 10f);
                    ModEinstellungen.BuffUpdateIntervall = fallbackTimer;
                    ModEinstellungen.Speichern();
                    Log.Out($"[EinmaligerSpawn] Das Update-Intervall für den Fortschritts-Buff wurde auf {fallbackTimer} Sekunden gesetzt.");
                }
                else
                {
                    Log.Warning("Ungültiger Parameter. Bitte nutze 'es progressbuff <on/off/time [sek]/radius [meter]>'.");
                }
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es range [radius] [spielername] ODER es range [radius] [chunkX] [chunkZ]
        // -----------------------------------------------------------------
        private void CmdRange(EntityPlayerLocal localPlayer, List<string> _params)
        {
            if (GameManager.IsDedicatedServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Befehl ist auf einem Dedicated Server wirkungslos.");
                return;
            }

            int radiusMeter = 120;
            EntityPlayer targetPlayer = localPlayer;
            string searchName = null;

            bool useChunkCoords = false;
            int targetChunkX = 0;
            int targetChunkZ = 0;

            // --- Parameter intelligent auswerten ---
            if (_params.Count == 2)
            {
                if (int.TryParse(_params[1], out int parsedRadius))
                    radiusMeter = parsedRadius;
                else
                    searchName = _params[1].ToLower();
            }
            else if (_params.Count == 3)
            {
                bool isParam1Int = int.TryParse(_params[1], out int val1);
                bool isParam2Int = int.TryParse(_params[2], out int val2);

                if (isParam1Int && isParam2Int)
                {
                    useChunkCoords = true;
                    targetChunkX = val1;
                    targetChunkZ = val2;
                }
                else if (isParam1Int && !isParam2Int)
                {
                    radiusMeter = val1;
                    searchName = _params[2].ToLower();
                }
                else
                {
                    Log.Warning("[EinmaligerSpawn] Ungültige Parameter. Nutzung: 'es range [radius] [name]' oder 'es range [chunkX] [chunkZ]'.");
                    return;
                }
            }
            else if (_params.Count >= 4)
            {
                if (int.TryParse(_params[1], out int r) && int.TryParse(_params[2], out int cx) && int.TryParse(_params[3], out int cz))
                {
                    radiusMeter = r;
                    useChunkCoords = true;
                    targetChunkX = cx;
                    targetChunkZ = cz;
                }
                else
                {
                    Log.Warning("[EinmaligerSpawn] Ungültige Parameter. Nutzung: 'es range [radius] [chunkX] [chunkZ]'.");
                    return;
                }
            }

            // --- FALL 1: Direkte Chunk-Koordinaten ---
            if (useChunkCoords)
            {
                var ergebnisChunk = KillCounter.BerechneLokalenFortschritt(targetChunkX, targetChunkZ, radiusMeter);
                Log.Out($"=== Spawn-Radar ({radiusMeter}m) für Chunk [{targetChunkX}, {targetChunkZ}] ===");
                Log.Out($"Status: {ergebnisChunk.gesperrt}/{ergebnisChunk.gesamt} ({ergebnisChunk.prozent}%)");
                return;
            }

            // --- FALL 2: Spieler wird gesucht ---
            if (!string.IsNullOrEmpty(searchName))
            {
                EntityPlayer foundPlayer = null;
                foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
                {
                    if (p.EntityName.ToLower().Contains(searchName))
                    {
                        foundPlayer = p;
                        break;
                    }
                }

                if (foundPlayer != null)
                {
                    targetPlayer = foundPlayer;
                }
                else
                {
                    Log.Warning($"[EinmaligerSpawn] Spieler '{searchName}' befindet sich in einem Chunk, der aktuell für dich nicht geladen ist (oder der Name ist falsch). Nähere dich, damit der Bereich geladen wird.");
                    return;
                }
            }

            // --- Normale Berechnung für den ermittelten Spieler ---
            // Spieler-Koordinaten in Chunk-Koordinaten umrechnen
            Vector3i pos = targetPlayer.GetBlockPosition();
            int pChunkX = pos.x >> 4;
            int pChunkZ = pos.z >> 4;

            var ergebnisSpieler = KillCounter.BerechneLokalenFortschritt(pChunkX, pChunkZ, radiusMeter);

            Log.Out($"=== Spawn-Radar ({radiusMeter}m) für {targetPlayer.EntityName} ===");
            Log.Out($"Status: {ergebnisSpieler.gesperrt}/{ergebnisSpieler.gesamt} ({ergebnisSpieler.prozent}%)");
        }

        // -----------------------------------------------------------------
        // BEFEHL: es tactical / es taktik <on / off>
        // -----------------------------------------------------------------
        private void CmdTactical(List<string> _params)
        {
            // Server only. Client rauswerfen.
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Admin-Befehl kann nur vom Server oder Host ausgeführt werden.");
                return;
            }

            string currentStatus = ModEinstellungen.TaktischerKillAktiv ? "ON" : "OFF";

            if (_params.Count < 2)
            {
                Log.Warning($"Aktueller Status (tactical): {currentStatus}. Bitte nutze 'es tactical on' oder 'es tactical off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.TaktischerKillAktiv = true;
                ModEinstellungen.Speichern();
                Log.Warning("[EinmaligerSpawn] Taktischer Kill (Bonus-Clear) ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.TaktischerKillAktiv = false;
                ModEinstellungen.Speichern();
                Log.Warning("[EinmaligerSpawn] Taktischer Kill (Bonus-Clear) ist nun DEAKTIVIERT.");
            }
            else
            {
                Log.Warning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es tactical on' oder 'es tactical off'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es timer / es time <Sekunden>
        // -----------------------------------------------------------------
        private void CmdTimer(List<string> _params)
        {
            // Server only. Client rauswerfen.
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Admin-Befehl kann nur vom Server oder Host ausgeführt werden.");
                return;
            }

            if (_params.Count < 2 || !float.TryParse(_params[1], out float neuerTimer))
            {
                Log.Warning($"Aktueller Timer: {ModEinstellungen.SpawnCheckIntervall} Sekunden. Bitte nutze 'es timer <Sekunden>', z.B. 'es timer 15'.");
                return;
            }

            neuerTimer = Mathf.Max(1f, neuerTimer);
            ModEinstellungen.SpawnCheckIntervall = neuerTimer;
            ModEinstellungen.Speichern();
            Log.Warning($"[EinmaligerSpawn] Autospawn-Überprüfungsintervall wurde auf {neuerTimer} Sekunden gesetzt.");
        }

        // -----------------------------------------------------------------
        // BEFEHL: es where
        // -----------------------------------------------------------------
        private void CmdWhere(EntityPlayerLocal player)
        {
            // Ein Dedicated Server ist kein player: rauswerfen
            if (GameManager.IsDedicatedServer)
            {
                Log.Warning("[EinmaligerSpawn] Dieser Befehl steuert ein lokales UI-Element und ist auf einem reinen Dedicated Server wirkungslos.");
                return;
            }

            if (player == null)
            {
                Log.Warning("[EinmaligerSpawn] Es hat noch kein Spieler in die Welt geladen.");
                return;
            }

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

                Log.Out($"[ES Spawner] Universal-Radar: Nächster Feind (Typ: {closestEnemy.GetType().Name}, ID: {closestEnemy.entityId}) ist {Mathf.RoundToInt(closestDist)}m entfernt.");
                Log.Out($"[ES Spawner] Marker erfolgreich über Systemklasse '{magicClassName}' gesetzt!");
            }
            else
            {
                Log.Out("[ES Spawner] Universal-Radar: Keine lebenden Feinde in deinem geladenen Umfeld gefunden.");
            }
        }
    }
}