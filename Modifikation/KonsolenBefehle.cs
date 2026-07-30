using System;
using System.Collections.Generic;
using EinmaligerSpawn.ChunkDatenbank;
using UnityEngine;
using EinmaligerSpawn.Config;
using EinmaligerSpawn.KartenOverlayManager;
using EinmaligerSpawn.LocalClear;
using EinmaligerSpawn.LootBagMarker;
using EinmaligerSpawn.ZombieSpawner;

namespace EinmaligerSpawn.Commands
{
    public class ConsoleCmdEinmaligerSpawn : ConsoleCmdAbstract
    {
        // -----------------------------------------------------------------
        // Die zentrale Variable für den Hilfetext (Konstante)
        // -----------------------------------------------------------------
        private const string HilfeText =
    "=== User Befehle ===\n" +
    "Nutze 'es map <on/off/reload>' für das Overlay.\n" +
    "Nutze 'es range [x]' um dir anzeigen zu lassen, wie viele Chunks in deiner Umgebung noch spawnen dürfen.\n" +
    "Nutze 'es msg <on/off>' für globale Chat-Nachrichten.\n" +
    "Nutze 'es where' um den nähesten aktiven Zombie zu finden.\n" +
    "Nutze 'es progressbuff <on/off>' um einen Buff anzeigen zu lassen, der deinen lokalen Säuberungs-Fortschritt anzeigt.\n" +
    "Nutze 'es localclear reason' um herauszufinden, warum der Chunk nicht gesäubert ist.\n" +
    "Nutze 'es cheat_lootbagmarker <on/off>' um Radar-Marker auf LootBags setzen zu lassen." +
    "=== Einmaliger Spawn Admin-Befehle ===\n" +
    "Nutze 'es limit <Zahl>' um das max. Autospawn-Limit zu setzen.\n" +
    "Nutze 'es timer <Sekunden>' um das Autospawn-Intervall zu ändern.\n" +
    "Nutze 'es localclear <on/off>' für den autom. 4s-Clear beim Durchlaufen.\n" +
    "Nutze 'es tactical <on/off>' für den Bonus-Clear.\n" +
    "Nutze 'es cheat_clear [radius] [reset]' um Chunks im Umkreis auf gecleart zu setzen oder zu löschen.";

        public override string[] getCommands()
        {
            return new string[] { "es" };
        }

        public override string getDescription()
        {
            return HilfeText;
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null) return;

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
                    CmdCheatClear(player, _params);
                    break;
                case "cheat_lootbagmarker":
                    CmdCheatLootbagMarker(_params);
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

        // =================================================================
        // HELPER METHODEN (Alphabetisch sortiert)
        // =================================================================

        // -----------------------------------------------------------------
        // BEFEHL: es cheat_clear [radius] [reset]
        // -----------------------------------------------------------------
        private void CmdCheatClear(EntityPlayerLocal player, List<string> _params)
        {
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

            Vector3i playerPos = player.GetBlockPosition();
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
                            // Chunk aus dem KillCounter löschen
                            if (KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                            {
                                KillCounter.ToteZombiesProChunk.Remove(chunkId);
                                newlyModified++;
                            }

                            // Chunk aus dem AutoSpawner-Cache werfen, damit er neu gescannt werden kann
                            AutoSpawner.RemoveChunkFromCache(chunkId);
                        }
                        else
                        {
                            // --- CLEAR LOGIK ---
                            if (!KillCounter.ToteZombiesProChunk.ContainsKey(chunkId))
                            {
                                KillCounter.ToteZombiesProChunk[chunkId] = 0;
                                newlyModified++;
                            }
                            KillCounter.ToteZombiesProChunk[chunkId]++;
                        }
                    }
                }
            }

            // Konsolen-Feedback dynamisch anpassen
            string actionText = isReset ? "reaktiviert (Reset)" : "neu ausgerottet (Clear)";
            string modeText = isReset ? "RESET" : "CLEAR";

            Log.Out($"=== Cheat Clear ({radiusMeter}m) - Modus: {modeText} ===");
            Log.Warning($"[ES Spawner] Ich habe {totalChecked} Chunks geprüft und {newlyModified} {actionText}.");

            // Erzwingt ein Neuzeichnen der Overlay-Karte, falls sie aktiv ist
            if (ModEinstellungen.KartenOverlayAktiv)
            {
                KartenOverlay.ErzwingeRedraw();
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es cheat_lootbagmarker <on / off>
        // -----------------------------------------------------------------
        private void CmdCheatLootbagMarker(List<string> _params)
        {
            // Da LootbagMarkerManager im neuen Namespace liegt, sprechen wir ihn direkt voll qualifiziert an 
            // (oder du fügst oben "using EinmaligerSpawn.LootBagMarker;" hinzu)
            string currentStatus = EinmaligerSpawn.LootBagMarker.LootbagMarkerManager.IstAktiv ? "ON" : "OFF";

            if (_params.Count < 2)
            {
                Log.Warning($"Aktueller Status (Lootbag-Marker): {currentStatus}. Bitte nutze 'es cheat_lootbagmarker on' oder 'es cheat_lootbagmarker off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                EinmaligerSpawn.LootBagMarker.LootbagMarkerManager.SetzeModus(true);
            }
            else if (state == "off" || state == "false")
            {
                EinmaligerSpawn.LootBagMarker.LootbagMarkerManager.SetzeModus(false);
            }
            else
            {
                Log.Warning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es cheat_lootbagmarker on' oder 'es cheat_lootbagmarker off'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es limit <Zahl>
        // -----------------------------------------------------------------
        private void CmdLimit(List<string> _params)
        {
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
        // BEFEHL: es localclear / es walkclear <on / off / reason>
        // -----------------------------------------------------------------
        private void CmdLocalClear(List<string> _params)
        {
            string currentStatus = ModEinstellungen.LokalerChunkClearAktiv ? "ON" : "OFF";

            if (_params.Count < 2)
            {
               Log.Warning($"Aktueller Status (localclear): {currentStatus}. Bitte nutze 'es localclear on', 'off' oder 'reason'.");
                return;
            }

            string state = _params[1].ToLower();

            // NEUER PARAMETER: reason / grund
            if (state == "reason" || state == "grund")
            {
                EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player != null)
                {
                    LokalenChunkSaeubern.Diagnose(player);
                }
                return;
            }

            // Bestehende ON / OFF Logik
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
        // BEFEHL: es map (on / off / reload)
        // -----------------------------------------------------------------
        private void CmdMap(List<string> _params)
        {
            if (_params.Count < 2)
            {
                Log.Out("Bitte nutze 'es map on', 'es map off' oder 'es map reload'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                KartenOverlay.SetzeModus(true);
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1,
                    $"[EinmaligerSpawn] Eroberungs-Karte (Overlay) ist nun [00FF00]AKTIVIERT[-].",
                    null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
            }
            else if (state == "off" || state == "false")
            {
                KartenOverlay.SetzeModus(false);
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1,
                    $"[EinmaligerSpawn] Eroberungs-Karte (Overlay) ist nun [FF0000]DEAKTIVIERT[-].",
                    null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
            }
            else if (state == "reload")
            {
                KartenOverlay.Reload();
                Log.Out("[EinmaligerSpawn] Karte (Marker) wurde erfolgreich neu geladen.");
            }
            else
            {
                Log.Out("Ungültiger Parameter. Bitte nutze 'es map on', 'es map off' oder 'es map reload'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es msg / es message [on / off]
        // -----------------------------------------------------------------
        private void CmdMsg(List<string> _params)
        {
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
                Log.Out("[EinmaligerSpawn] Globale Chat-Nachrichten sind nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.ChatNachrichtenAktiv = false;
                ModEinstellungen.Speichern();
                Log.Out("[EinmaligerSpawn] Globale Chat-Nachrichten sind nun DEAKTIVIERT.");
            }
            else
            {
                Log.Warning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es msg on' oder 'es msg off'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es progressbuff <on / off>
        // -----------------------------------------------------------------
        private void CmdProgressBuff(EntityPlayerLocal player, List<string> _params)
        {
            string currentStatus = ModEinstellungen.ZeigeLokalenFortschritt ? "ON" : "OFF";

            if (_params.Count < 2)
            {
                Log.Warning($"Aktueller Status (progressbuff): {currentStatus}. Bitte nutze 'es progressbuff on' oder 'es progressbuff off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.ZeigeLokalenFortschritt = true;
                ModEinstellungen.Speichern();
                Log.Out("[EinmaligerSpawn] Lokaler Fortschritts-Buff ist nun AKTIVIERT.");

                // Sofortiges Feedback im HUD erzwingen
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

                // Sofortiges Entfernen aus dem HUD erzwingen
                if (player != null && player.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
                {
                    player.Buffs.RemoveBuff("buffEinmaligerSpawnProgress");
                }
            }
            else
            {
                Log.Warning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es progressbuff on' oder 'es progressbuff off'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es range
        // -----------------------------------------------------------------
        private void CmdRange(EntityPlayerLocal player, List<string> _params)
        {
            int radiusMeter = 120;

            if (_params.Count > 1)
            {
                int.TryParse(_params[1], out radiusMeter);
            }

            // Wir holen uns die Werte sauber aus der neuen zentralen Methode
            var ergebnis = KillCounter.BerechneLokalenFortschritt(player, radiusMeter);

            Log.Out($"=== Spawn-Radar ({radiusMeter}m) ===");
            Log.Out($"Status: {ergebnis.gesperrt}/{ergebnis.gesamt} ({ergebnis.prozent}%)");
        }

        // -----------------------------------------------------------------
        // BEFEHL: es tactical / es taktik <on / off>
        // -----------------------------------------------------------------
        private void CmdTactical(List<string> _params)
        {
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