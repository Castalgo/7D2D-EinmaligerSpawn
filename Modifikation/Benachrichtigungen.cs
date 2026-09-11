using System;
using EinmaligerSpawn.Config;

namespace EinmaligerSpawn.Benachrichtigungen
{
    public static class NotificationManager
    {
        // ==========================================
        // 1. ZENTRALE HILFSMETHODEN
        // ==========================================

        private static string GetZeitString()
        {
            ValueTuple<int, int, int> time = GameUtils.WorldTimeToElements(GameManager.Instance.World.worldTime);
            return $"Tag {time.Item1}, {time.Item2:00}:{time.Item3:00}";
        }

        private static void SendeLokaleNachricht(string nachricht)
        {
            // Dedicated Server haben kein lokales UI. Externe Clients empfangen ihre Nachrichten über die NetPackages.
            if (GameManager.IsDedicatedServer) return;

            GameManager.Instance.ChatMessageClient(
                EChatType.Global,
                -1,
                nachricht,
                null,
                EMessageSender.Server,
                GeneratedTextManager.BbCodeSupportMode.Supported
            );
        }

        // ==========================================
        // 2. ÖFFENTLICHE AUFRUFE
        // ==========================================

        // NEU: Sendet eine Nachricht zwingend an den gesamten Server (unabhängig vom Chat-Modus)
        public static void SendeGlobaleNachricht(string nachricht)
        {
            // Nur der Server darf / kann globale Server-Nachrichten verschicken
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            GameManager.Instance.ChatMessageServer(
                null,
                EChatType.Global,
                -1,
                nachricht,
                null,
                EMessageSender.Server,
                GeneratedTextManager.BbCodeSupportMode.Supported
            );
        }

        public static void SendePoiClear(string poiName)
        {
            // Modus 1 = Nur POIs, Modus 3 = Alles
            if (ModEinstellungen.ChatNachrichtenModus == 1 || ModEinstellungen.ChatNachrichtenModus == 3)
            {
                string msg = $"[00FF00][{GetZeitString()}] POI '{poiName}' wurde restlos gesäubert![-]";
                SendeLokaleNachricht(msg);
            }
        }

        public static void SendeChunkClear(string chunkId)
        {
            // Modus 2 = Nur Chunks, Modus 3 = Alles
            if (ModEinstellungen.ChatNachrichtenModus == 2 || ModEinstellungen.ChatNachrichtenModus == 3)
            {
                string msg = $"[00FF00][{GetZeitString()}] Gebiet {chunkId} wurde dauerhaft gesäubert![-]";
                SendeLokaleNachricht(msg);
            }
        }

        public static void SendeTaktischenKill(string chunkId, bool istNachbar)
        {
            // Modus 2 = Nur Chunks, Modus 3 = Alles
            if (ModEinstellungen.ChatNachrichtenModus == 2 || ModEinstellungen.ChatNachrichtenModus == 3)
            {
                string chatOrtsangabe = istNachbar ? $"Nachbar {chunkId} wurde zusätzlich gesichert!" : $"Todes-Ort {chunkId} wurde gesäubert!";
                string msg = $"[00FF00][{GetZeitString()}] Taktischer Clear: {chatOrtsangabe}[-]";
                SendeLokaleNachricht(msg);
            }
        }
    }
}