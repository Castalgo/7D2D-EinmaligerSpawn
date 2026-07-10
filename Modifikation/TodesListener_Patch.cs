using HarmonyLib;
using EinmaligerSpawn.Manager;

namespace EinmaligerSpawn.Patches
{
    // ---------------------------------------------------------
    // TEIL 1: Der Zähler (Wenn der Zombie stirbt)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(EntityAlive), "SetDead")]
    public class TodesListener_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityAlive __instance)
        {
            // Prüfen: Ist es ein Zombie UND steht er in unserem Gedächtnis?
            if (__instance is EntityZombie && ChunkDatenbank.ZombieUrsprung.ContainsKey(__instance.entityId))
            {
                // 1. Ursprungs-Chunk aus dem Gedächtnis lesen
                string ursprungsChunk = ChunkDatenbank.ZombieUrsprung[__instance.entityId];

                // 2. Kill diesem Ursprungs-Chunk anrechnen
                ChunkDatenbank.AddToterZombieNachID(ursprungsChunk, DynamischesSpawnLimit.MaxKills);

                // 3. Zombie aus dem Gedächtnis löschen (Er ist ja jetzt tot)
                ChunkDatenbank.ZombieUrsprung.Remove(__instance.entityId);
            }
        }
    }

    // ---------------------------------------------------------
    // TEIL 2: Die Müllabfuhr (Wenn der Zombie despawnt)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(World), "RemoveEntity")]
    public class Despawn_Listener_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int _entityId, EnumRemoveEntityReason _reason)
        {
            // Wenn die Engine eine ID löscht, schauen wir, ob wir sie kennen.
            // Wenn ja: Lösche sie lautlos aus unserem Gedächtnis, um RAM zu sparen.
            if (ChunkDatenbank.ZombieUrsprung.ContainsKey(_entityId))
            {
                ChunkDatenbank.ZombieUrsprung.Remove(_entityId);
            }
        }
    }
}