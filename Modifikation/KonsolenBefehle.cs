using System;
using System.Collections.Generic;
using EinmaligerSpawn.Manager;
using UnityEngine;

namespace EinmaligerSpawn.Commands
{
    public class ConsoleCmdEinmaligerSpawn : ConsoleCmdAbstract
    {
        // -----------------------------------------------------------------
        // Die zentrale Variable für den Hilfetext (Konstante)
        // -----------------------------------------------------------------
        private const string HilfeText =
    "=== User Befehle ===\n" +
    "Nutze 'es map <on/off/reload>' für das Overlay,\n" +
    "Nutze 'es range [x]' um dir anzeigen zu lassen, wie viele Chunks in deiner Umgebung noch spawnen dürfen,\n" +
    "Nutze 'es msg <on/off>' für globale Chat-Nachrichten,\n" +
    "Nutze 'es where' um den nähesten aktiven Zombie zu finden,\n" +
    "=== Einmaliger Spawn Admin-Befehle ===\n" +
    "Nutze 'es localclear <on/off>' für den autom. 4s-Clear beim Durchlaufen,\n" +
    "Nutze 'es tactical <on/off>' für den Bonus-Clear,\n" +
    "Nutze 'es limit <Zahl>' um das max. Autospawn-Limit zu setzen,\n" +
    "Nutze 'es timer <Sekunden>' um das Autospawn-Intervall zu ändern,\n" +
    "Nutze 'es cheat_spawn [x]' oder 'es cheat_clear [x]' zum spawnen von Zombies oder Chunks im Umkreis auf gecleart zu setzen.";

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
                case "cheat_spawn":
                    CmdCheatSpawn(player, _params, _senderInfo);
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
            UnityEngine.Debug.Log(HilfeText);
        }

        // =================================================================
        // HELPER METHODEN (Alphabetisch sortiert)
        // =================================================================

        // -----------------------------------------------------------------
        // BEFEHL: es cheat_clear
        // -----------------------------------------------------------------
        private void CmdCheatClear(EntityPlayerLocal player, List<string> _params)
        {
            int radiusMeter = 20;

            if (_params.Count > 1)
            {
                if (int.TryParse(_params[1], out int parsedRadius))
                {
                    radiusMeter = Mathf.Clamp(parsedRadius, 1, 256);
                }
            }

            Vector3i playerPos = player.GetBlockPosition();
            int px = playerPos.x;
            int pz = playerPos.z;

            int playerChunkX = px >> 4;
            int playerChunkZ = pz >> 4;

            int chunkSuchRadius = Mathf.CeilToInt((float)radiusMeter / 16f);
            int maxDistSq = radiusMeter * radiusMeter;

            int newlyCleared = 0;
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

                        if (!ChunkDatenbank.ToteZombiesProChunk.ContainsKey(chunkId))
                        {
                            ChunkDatenbank.ToteZombiesProChunk[chunkId] = 0;
                            newlyCleared++;
                        }

                        ChunkDatenbank.ToteZombiesProChunk[chunkId]++;
                    }
                }
            }

            UnityEngine.Debug.Log($"=== Cheat Clear ({radiusMeter}m) ===");
            UnityEngine.Debug.LogWarning($"[ES Spawner] Ich habe {totalChecked} Chunks geprüft und {newlyCleared} neu ausgerottet.");
        }

        // -----------------------------------------------------------------
        // BEFEHL: es cheat_spawn
        // -----------------------------------------------------------------
        private void CmdCheatSpawn(EntityPlayerLocal player, List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_senderInfo.RemoteClientInfo != null)
            {
                UnityEngine.Debug.LogWarning("[EinmaligerSpawn] ES Spawner kann nur vom Host/Lokal ausgeführt werden.");
                return;
            }

            int requestedZombies = 1;
            if (_params.Count > 1)
            {
                if (int.TryParse(_params[1], out int parsedCount))
                {
                    requestedZombies = Mathf.Clamp(parsedCount, 1, 16);
                }
            }

            AutoSpawner.FuehreSpawnAus(player, requestedZombies, true);
        }

        // -----------------------------------------------------------------
        // BEFEHL: es limit <Zahl>
        // -----------------------------------------------------------------
        private void CmdLimit(List<string> _params)
        {
            if (_params.Count < 2 || !int.TryParse(_params[1], out int neuesLimit))
            {
                UnityEngine.Debug.LogWarning($"Aktuelles Limit: {ModEinstellungen.GlobalesZombieLimit}. Bitte nutze 'es limit <Zahl>', z.B. 'es limit 18'.");
                return;
            }

            neuesLimit = Mathf.Max(1, neuesLimit);
            ModEinstellungen.GlobalesZombieLimit = neuesLimit;
            ModEinstellungen.Speichern();
            UnityEngine.Debug.LogWarning($"[EinmaligerSpawn] Globales Autospawn-Limit wurde auf {neuesLimit} gesetzt.");
        }

        // -----------------------------------------------------------------
        // BEFEHL: es localclear / es walkclear <on / off>
        // -----------------------------------------------------------------
        private void CmdLocalClear(List<string> _params)
        {
            string currentStatus = ModEinstellungen.LokalerChunkClearAktiv ? "ON" : "OFF";

            if (_params.Count < 2)
            {
                UnityEngine.Debug.LogWarning($"Aktueller Status (localclear): {currentStatus}. Bitte nutze 'es localclear on' oder 'es localclear off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.LokalerChunkClearAktiv = true;
                ModEinstellungen.Speichern();
                UnityEngine.Debug.LogWarning("[EinmaligerSpawn] Lokaler Chunk-Clear (4s-Präsenz) ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.LokalerChunkClearAktiv = false;
                ModEinstellungen.Speichern();
                UnityEngine.Debug.LogWarning("[EinmaligerSpawn] Lokaler Chunk-Clear (4s-Präsenz) ist nun DEAKTIVIERT.");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es localclear on' oder 'es localclear off'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es map (on / off / reload)
        // -----------------------------------------------------------------
        private void CmdMap(List<string> _params)
        {
            if (_params.Count < 2)
            {
                UnityEngine.Debug.Log("Bitte nutze 'es map on', 'es map off' oder 'es map reload'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                KartenOverlayManager.SetzeModus(true);
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1,
                    $"[EinmaligerSpawn] Eroberungs-Karte (Overlay) ist nun [00FF00]AKTIVIERT[-].",
                    null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
            }
            else if (state == "off" || state == "false")
            {
                KartenOverlayManager.SetzeModus(false);
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1,
                    $"[EinmaligerSpawn] Eroberungs-Karte (Overlay) ist nun [FF0000]DEAKTIVIERT[-].",
                    null, EMessageSender.Server, GeneratedTextManager.BbCodeSupportMode.Supported);
            }
            else if (state == "reload")
            {
                KartenOverlayManager.Reload();
                UnityEngine.Debug.Log("[EinmaligerSpawn] Karte (Marker) wurde erfolgreich neu geladen.");
            }
            else
            {
                UnityEngine.Debug.Log("Ungültiger Parameter. Bitte nutze 'es map on', 'es map off' oder 'es map reload'.");
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
                UnityEngine.Debug.LogWarning($"Aktueller Status (msg): {currentStatus}. Bitte nutze 'es msg on' oder 'es msg off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.ChatNachrichtenAktiv = true;
                ModEinstellungen.Speichern();
                UnityEngine.Debug.Log("[EinmaligerSpawn] Globale Chat-Nachrichten sind nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.ChatNachrichtenAktiv = false;
                ModEinstellungen.Speichern();
                UnityEngine.Debug.Log("[EinmaligerSpawn] Globale Chat-Nachrichten sind nun DEAKTIVIERT.");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es msg on' oder 'es msg off'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es range
        // -----------------------------------------------------------------
        private void CmdRange(EntityPlayerLocal player, List<string> _params)
        {
            int radiusMeter = 80;

            if (_params.Count > 1)
            {
                int.TryParse(_params[1], out radiusMeter);
            }

            Vector3i playerPos = player.GetBlockPosition();
            int px = playerPos.x;
            int pz = playerPos.z;

            int playerChunkX = px >> 4;
            int playerChunkZ = pz >> 4;

            int chunkSuchRadius = Mathf.CeilToInt((float)radiusMeter / 16f);
            int maxDistSq = radiusMeter * radiusMeter;

            int x_Gesperrt = 0;
            int y_Gesamt = 0;

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
                        y_Gesamt++;
                        string chunkId = $"{cx}_{cz}";

                        if (ChunkDatenbank.ToteZombiesProChunk.ContainsKey(chunkId) &&
                            ChunkDatenbank.ToteZombiesProChunk[chunkId] >= 1)
                        {
                            x_Gesperrt++;
                        }
                    }
                }
            }

            float prozentFloat = y_Gesamt > 0 ? ((float)x_Gesperrt / y_Gesamt) * 100f : 0f;
            int prozent = Mathf.RoundToInt(prozentFloat);

            UnityEngine.Debug.Log($"=== Spawn-Radar ({radiusMeter}m) ===");
            UnityEngine.Debug.Log($"Status: {x_Gesperrt}/{y_Gesamt} ({prozent}%)");
        }

        // -----------------------------------------------------------------
        // BEFEHL: es tactical / es taktik <on / off>
        // -----------------------------------------------------------------
        private void CmdTactical(List<string> _params)
        {
            string currentStatus = ModEinstellungen.TaktischerKillAktiv ? "ON" : "OFF";

            if (_params.Count < 2)
            {
                UnityEngine.Debug.LogWarning($"Aktueller Status (tactical): {currentStatus}. Bitte nutze 'es tactical on' oder 'es tactical off'.");
                return;
            }

            string state = _params[1].ToLower();

            if (state == "on" || state == "true")
            {
                ModEinstellungen.TaktischerKillAktiv = true;
                ModEinstellungen.Speichern();
                UnityEngine.Debug.LogWarning("[EinmaligerSpawn] Taktischer Kill (Bonus-Clear) ist nun AKTIVIERT.");
            }
            else if (state == "off" || state == "false")
            {
                ModEinstellungen.TaktischerKillAktiv = false;
                ModEinstellungen.Speichern();
                UnityEngine.Debug.LogWarning("[EinmaligerSpawn] Taktischer Kill (Bonus-Clear) ist nun DEAKTIVIERT.");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Ungültiger Parameter. Aktueller Status: {currentStatus}. Bitte nutze 'es tactical on' oder 'es tactical off'.");
            }
        }

        // -----------------------------------------------------------------
        // BEFEHL: es timer / es time <Sekunden>
        // -----------------------------------------------------------------
        private void CmdTimer(List<string> _params)
        {
            if (_params.Count < 2 || !float.TryParse(_params[1], out float neuerTimer))
            {
                UnityEngine.Debug.LogWarning($"Aktueller Timer: {ModEinstellungen.SpawnCheckIntervall} Sekunden. Bitte nutze 'es timer <Sekunden>', z.B. 'es timer 15'.");
                return;
            }

            neuerTimer = Mathf.Max(1f, neuerTimer);
            ModEinstellungen.SpawnCheckIntervall = neuerTimer;
            ModEinstellungen.Speichern();
            UnityEngine.Debug.LogWarning($"[EinmaligerSpawn] Autospawn-Überprüfungsintervall wurde auf {neuerTimer} Sekunden gesetzt.");
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

                UnityEngine.Debug.Log($"[ES Spawner] Universal-Radar: Nächster Feind (Typ: {closestEnemy.GetType().Name}, ID: {closestEnemy.entityId}) ist {Mathf.RoundToInt(closestDist)}m entfernt.");
                UnityEngine.Debug.Log($"[ES Spawner] Marker erfolgreich über Systemklasse '{magicClassName}' gesetzt!");
            }
            else
            {
                UnityEngine.Debug.Log("[ES Spawner] Universal-Radar: Keine lebenden Feinde in deinem geladenen Umfeld gefunden.");
            }
        }
    }
}