using System;
using System.Collections.Generic;
using UnityEngine;

namespace EinmaligerSpawn.Manager
{
    public static class LokalenChunkSaeubern
    {
        private class TrackingDaten
        {
            public string ChunkId = "";
            public float ZeitImChunk = 0f;
        }

        private static Dictionary<int, TrackingDaten> spielerTracking = new Dictionary<int, TrackingDaten>();

        private static float checkTimer = 0f; // Timer zum Drosseln der Update-Frequenz

        public static void OnGameUpdate()
        {
            // Abbruch, wenn das Feature über die Config deaktiviert wurde
            if (!ModEinstellungen.LokalerChunkClearAktiv)
                return;

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

                // HIER GEÄNDERT: Statt Time.deltaTime rechnen wir den angesammelten Block von z.B. 0.51 Sekunden drauf
                daten.ZeitImChunk += vergangeneZeit;

                if (daten.ZeitImChunk >= 4f)
                {
                    bool erfolgreich = PruefeUndSaeubere(aktuellerChunk, player);
                    // Den Timer nach einer Überprüfung zurücksetzen, auch wenn sie fehlschlug
                    daten.ZeitImChunk = 0f;
                }
            }
        }

        private static bool PruefeUndSaeubere(string chunkId, EntityPlayer player)
        {
            if (ChunkDatenbank.ToteZombiesProChunk.ContainsKey(chunkId) && ChunkDatenbank.ToteZombiesProChunk[chunkId] >= 1)
                return false;

            if (ChunkDatenbank.ZombieUrsprung.ContainsValue(chunkId))
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

            ChunkDatenbank.ToteZombiesProChunk[chunkId] = 1;

            UnityEngine.Debug.Log($"[EinmaligerSpawn] Walkthrough-Clear: Chunk {chunkId} wurde durch 4s friedliche Präsenz von '{player.EntityName}' gesäubert.");
            

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
                KartenOverlayManager.ZeichneMarker(chunkId);
            }

            return true;
        }

        public static void Reset()
        {
            spielerTracking.Clear();
        }
    }
}