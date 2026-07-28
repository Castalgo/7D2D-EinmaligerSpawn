🌐 [English](README.md) | 🇩🇪 [Deutsch](README_DE.md)
---

# EinmaligerSpawn (for 7D2D Version 3.x.x)

## About this mod
The mod tracks when you have cleared a chunk or a POI of zombies and permanently prevents them from respawning in that chunk. 
Currently, the project is in its alpha phase (this is the first playable alpha version) and has only been tested on the current build.

## Installation
Simply place the `Mods/EinmaligerSpawn` folder into your mod directory at `C:\Users\yourCurrentUserprofilename\AppData\Roaming\7DaysToDie\Mods\`.

---

## The AutoSpawner
The mod includes its own custom AutoSpawner. This was added because the vanilla spawn system often acts too slowly and passively when too many areas around you no longer allow spawns. The spawner ensures that the world around you remains populated.
*   **Default Behavior:** By default, the mod checks every 5 seconds if new zombies are needed and maintains a global limit of a maximum of 18 active zombies.
*   **Customizability:** You can adjust these values at any time in-game using the console commands `es timer <seconds>` and `es limit <number>` to suit your preferences or server performance.

---

## Clear Mechanics (How chunks are cleared)
You have several ways to mark a chunk as "cleared" in the mod. Almost all of these mechanics can be individually modified or completely disabled, as the settings are saved in the `ModConfig.json` within your savegame folder.

1.  **Point of Origin (The Default Clear)**
    If you kill a zombie spawned by the mod, the chunk from which this zombie originally spawned is immediately cleared.
2.  **Place of Death (Tactical Kill / Kiting)**
    In addition to the point of origin, the mod rewards you for playing tactically. If you kite a zombie into another adjacent chunk and kill it there, this death-location chunk is also cleared. 
    *   *Default:* This feature is enabled by default (`TaktischerKillAktiv = true`). 
    *   *Control:* You can toggle the tactical clear at any time via the console using `es tactical <on/off>`.
3.  **Passing Through (Local Chunk Clear)**
    You don't have to fight for every chunk. If you stay continuously in a chunk for just 4 seconds, it is also considered secured.
    *   *Default:* This feature is enabled by default (`LokalerChunkClearAktiv = true`).
    *   *Control:* If you want to make the game harder, disable this mechanic via the console using `es localclear <on/off>`.

---

## Important Gameplay Notes
*   **Heat Spawns:** Heat spawns (such as Screamers) must strictly be disabled, as they bypass the mod's spawn logic.
*   **Quests:** Once cleared, POIs are no longer available as quests. During buried supplies quests, enemy waves will no longer spawn.
*   **Blood Moon:** A Blood Moon makes no sense from a gameplay perspective and should be disabled because it undermines the core concept of the mod.
*   **New Player Buff:** The mod takes your starting buff into account and spares you a bit in the beginning.

---

## Sandbox Settings (`Sandboxeinstellungen.txt`)
For world generation and the mod to function correctly, the sandbox settings must absolutely be set correctly by the user. Apply the values according to the guidelines.

*   **Recommended Settings:** `ABBDBGFBHABLABWACHAEXGFCCFFAFKAEPAETK`
*   **Minimum Settings:** `ABWACHA` (These settings represent the absolute minimum for a functional gameplay experience).

---

## Console Commands
The mod comes with several custom commands for the in-game console. All commands start with the prefix `es`.

### User Commands
*   `es map <on/off/reload>`: Use this command for the green map overlay.
*   `es range [x]`: Shows how many chunks in your vicinity are still allowed to spawn zombies.
*   `es msg <on/off>`: Enables or disables global chat messages (enabled by default).
*   `es where`: Finds the nearest active zombie.
*   `es localclear reason`: Explains why the current chunk hasn't been cleared.
*   `es cheat_lootbagmarker <on/off>`: Places radar markers on LootBags.

### Admin Commands
*   `es limit <number>`: Sets the maximum AutoSpawn limit.
*   `es timer <seconds>`: Changes the AutoSpawn interval.
*   `es localclear <on/off>`: Toggles the automatic 4-second clear when passing through.
*   `es tactical <on/off>`: Enables or disables the bonus clear (Place of Death).
*   `es cheat_clear [x]`: Sets chunks within a radius to "cleared".