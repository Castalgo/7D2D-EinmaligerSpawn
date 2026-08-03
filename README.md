🌐 [English](README.md) | 🇩🇪 [Deutsch](README_DE.md)
---

# EinmaligerSpawn (for 7D2D Version 3.x.x)

📌 **[⬇️ Skip directly to the console commands](#console-commands)**

## About this mod
The mod tracks when you have cleared a chunk or a POI of zombies and permanently prevents them from respawning in that chunk.[cite: 9]
Currently, the project is in its alpha phase (this is the first playable alpha version) and has only been tested on the current build.[cite: 9]

## Installation
1. Download the latest version of the mod here: [EinmaligerSpawn Release](../../releases/tag/EinmaligerSpawn)[cite: 9]
2. Extract the downloaded ZIP file.[cite: 9]
3. Place the extracted `Mods` folder in your mods directory under `%AppData%\7DaysToDie\`.[cite: 9]

**Important for Multiplayer:** This mod communicates via its own custom network packages and therefore **must be installed on both the server and all clients**. The mod does not support EAC, which means the server must have EAC disabled.[cite: 9]

---

## The AutoSpawner
The mod includes its own custom AutoSpawner.[cite: 9] This was added because the vanilla spawn system often acts too slowly and passively when too many areas around you no longer allow spawns.[cite: 9] The spawner ensures that the world around you remains populated.[cite: 9]
*   **Default Behavior:** By default, the mod checks every 5 seconds if new zombies are needed and maintains a global limit of a maximum of 18 active zombies.[cite: 9]
*   **Customizability:** You can adjust these values at any time in-game using the console commands `esa timer <seconds>` and `esa limit <number>` to suit your preferences or server performance.

---

## Clear Mechanics (How chunks are cleared)
You have several ways to mark a chunk as "cleared" in the mod.[cite: 9] Almost all of these mechanics can be individually modified or completely disabled, as the settings are saved in the `EinmaligerSpawn_Config.json` within your savegame folder.[cite: 9]

1.  **Point of Origin (The Default Clear)**
    If you kill a zombie spawned by the mod, the chunk from which this zombie originally spawned is immediately cleared.[cite: 9]
2.  **Place of Death (Tactical Kill / Kiting)**
    In addition to the point of origin, the mod rewards you for playing tactically.[cite: 9] If you kite a zombie into another adjacent chunk and kill it there, this death-location chunk is also cleared.[cite: 9]
    *   *Default:* This feature is enabled by default (`TaktischerKillAktiv = true`).[cite: 9]
    *   *Control:* You can toggle the tactical clear at any time via the console using `esa tactical <on/off>`.
3.  **Passing Through (Local Chunk Clear)**
    You don't have to fight for every chunk.[cite: 9] If you stay continuously in a chunk for just 4 seconds, it is also considered secured.[cite: 9]
    *   *Default:* This feature is enabled by default (`LokalerChunkClearAktiv = true`).[cite: 9]
    *   *Control:* If you want to make the game harder, disable this mechanic via the console using `esa localclear <on/off>`.

---

## Important Gameplay Notes
*   **Heat Spawns:** Heat spawns (such as Screamers) must strictly be disabled, as they bypass the mod's spawn logic.[cite: 9]
*   **Quests:** Once cleared, POIs are no longer available as quests.[cite: 9] During buried supplies quests, enemy waves will no longer spawn.[cite: 9]
*   **Blood Moon:** A Blood Moon makes no sense from a gameplay perspective and should be disabled because it undermines the core concept of the mod.[cite: 9]
*   **New Player Buff:** The mod takes your starting buff into account and spares you a bit in the beginning.[cite: 9]

---

## Sandbox Settings (`Sandboxeinstellungen.txt`)
For world generation and the mod to function correctly, the sandbox settings must absolutely be set correctly by the user.[cite: 9] Apply the values according to the guidelines.[cite: 9]

*   **Recommended Settings:** `ABBDBGFBHABLABWACHAEXGFCCFFAFKAEPAETK`[cite: 9]
*   **Minimum Settings:** `ABWACHA` (These settings represent the absolute minimum for a functional gameplay experience).[cite: 9]

---

<a id="console-commands"></a>
## Console Commands
The mod comes with several custom commands for the in-game console.[cite: 9] All local player commands start with the prefix `es`, all server commands with `esa`. Use `es help` or `esa help` in-game for an overview.

### Client / User Commands (Available locally to everyone)
*   `es map <on/off/reload>`: Controls the personal map overlay or reloads markers.[cite: 9]
*   `es msg <on/off>`: Enables or disables your local chat messages from the mod.
*   `es progressbuff <on/off/time [sec]/radius [m]>`: Controls the HUD progress buff, updates the interval, or changes the search radius.[cite: 9]
*   `es range [radius] [name]` OR `es range [radius] [chunkX] [chunkZ]`: Checks the clearance progress in the vicinity (default 120m).[cite: 9]
*   `es where`: Universal radar to mark the nearest active zombie on the compass.[cite: 9]

### Server / Admin Commands (Host & Server Admins only)
*   `esa cheat_clear [player] [radius] [reset]`: Sets chunks within a radius to 'cleared' or resets their status.
*   `esa cheat_loud [player/coords] [rooms]`: Forces the nearest POI (max. 80m) to wake its sleeping zombies and sick them on the player.
*   `esa limit <number>`: Sets the global AutoSpawn limit for zombies on the server.[cite: 9]
*   `esa localclear <on/off/reason [name]>`: Toggles the automatic 4s-clear (on/off) or runs diagnostics for a specific player (reason).[cite: 9]
*   `esa range [player] [radius]`: Calculates the cleared area around any player as an admin.
*   `esa tactical <on/off>`: Enables or disables the server-side bonus clear (Tactical Kill).
*   `esa timer <seconds>`: Adjusts the server-side AutoSpawn check interval.[cite: 9]

---
This mod requires Harmony by Andreas Pardeike. Many thanks for his great work!