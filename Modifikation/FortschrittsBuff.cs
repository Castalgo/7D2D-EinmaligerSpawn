using EinmaligerSpawn.ChunkDatenbank;
using EinmaligerSpawn.Config;
using UnityEngine;

namespace EinmaligerSpawn.HUD
{
    public static class FortschrittsBuff
    {
        private static float updateTimer = 0f;

        public static void OnGameUpdate()
        {
            // 1. Grundlegender Sicherheitscheck
            if (GameManager.Instance == null || GameManager.Instance.World == null)
                return;

            // 2. Abbrechen, wenn in der Config deaktiviert (spart Rechenzeit!)
            if (!ModEinstellungen.ZeigeLokalenFortschritt)
                return;

            // 3. Client only, Server rauswerfen.
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null)
                return;

            // 4. Eigener, lokaler Timer (Drosselung der Berechnungen)
            updateTimer += Time.deltaTime;
            if (updateTimer < ModEinstellungen.BuffUpdateIntervall)
                return;

            updateTimer = 0f;

            // 5. Buff verteilen, falls er fehlt
            if (!player.Buffs.HasBuff("buffEinmaligerSpawnProgress"))
            {
                player.Buffs.AddBuff("buffEinmaligerSpawnProgress");
            }

            // --- NEU: Spieler-Position in Chunk-Koordinaten umwandeln ---
            Vector3i pos = player.GetBlockPosition();
            int playerChunkX = pos.x >> 4;
            int playerChunkZ = pos.z >> 4;

            // 6. Prozentwert berechnen (mit dem flexiblen Radius aus der Config)
            var fortschritt = KillCounter.BerechneLokalenFortschritt(playerChunkX, playerChunkZ, ModEinstellungen.ProgressBuffRadius);

            // 7. Den Wert ins HUD schreiben
            player.Buffs.SetCustomVar("esLocalClearPercent", fortschritt.prozent, true);
        }
    }
}