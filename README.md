🌐 [English](README.md) | 🇩🇪 [Deutsch](README_DE.md)
---

# EinmaligerSpawn (for 7D2D Version 3.x.x)

## About this mod
The mod tracks when you have cleared a chunk or a POI of zombies and permanently prevents them from respawning in that chunk. 
Currently, the project is in its alpha phase (this is the first playable alpha version) and has only been tested on the current build.

## Installation
1. Download the latest version of the mod here: [EinmaligerSpawn Release](../../releases/tag/EinmaligerSpawn)
2. Extract the downloaded ZIP file.
3. Place the extracted `Mods` folder in your mods directory under `%AppData%\7DaysToDie\`.

The mod is purely client-side and does not need to be installed on the server. This mod does not support EAC, which means the server must have EAC disabled.

---

## The AutoSpawner
The mod includes its own custom AutoSpawner. This was added because the vanilla spawn system often acts too slowly and passively when too many areas around you no longer allow spawns. The spawner ensures that the world around you remains populated.
*   **Default Behavior:** By default, the mod checks every 5 seconds if new zombies are needed and maintains a global limit of a maximum of 18 active zombies.
*   **Customizability:** You can adjust these values at any time in-game using the console commands `es timer <seconds>` and `es limit <number>` to suit your preferences or server performance.

---

## Clear Mechanics (How chunks are cleared)
You have several ways to mark a chunk as "cleared" in the mod. Almost all of these mechanics can be individually modified or completely disabled, as the settings are saved in the `EinmaligerSpawn_Config.json` within your savegame folder.

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
The mod comes with several custom commands for the in-game console. All commands start with the prefix `es`. Use `es help` in-game for an overview.

### Client / User Commands (Available to everyone)
*   `es map <on/off/reload>`: Controls the personal map overlay or reloads markers.
*   `es progressbuff <on/off/time [sec]/radius [m]>`: Controls the HUD progress buff, updates the interval, or changes the search radius.
*   `es range [radius] [name]` OR `es range [radius] [chunkX] [chunkZ]`: Checks the clearance progress in the vicinity (default 120m).
*   `es where`: Universal radar to mark the nearest active zombie on the compass.

### Server / Admin Commands (Host & Server Admins only)
*   `es cheat_clear [radius] [reset]`: Sets chunks within a radius to 'cleared' or resets their status.
*   `es limit <number>`: Sets the global AutoSpawn limit for zombies on the server.
*   `es localclear <on/off/reason [name]>`: Toggles the automatic 4s-clear (on/off) or runs diagnostics for a specific player (reason).
*   `es msg <on/off>`: Enables or disables the global chat messages of the mod for everyone.
*   `es tactical <on/off>`: Enables or disables the server-side bonus clear (Place of Death).
*   `es timer <seconds>`: Adjusts the server-side AutoSpawn check interval.

---
This mod uses Harmony by Andreas Pardeike, licensed under the MIT License. Many thanks for his work.