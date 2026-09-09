🌐 [English](README.md) | 🇩🇪 [Deutsch](README_DE.md)
---

# EinmaligerSpawn (for 7D2D Version 3.x.x)

📌 **[⬇️ Skip directly to the console commands](#console-commands)**

## About this mod
The mod tracks when you have cleared a chunk or a POI of zombies and permanently prevents them from respawning in that chunk. It also features a dynamic radar for buildings, a graphical map overlay for your progress, and a global background scanner for the game world.

## Installation
1. Download the latest version of the mod here: [EinmaligerSpawn Release](../../releases/tag/EinmaligerSpawn)
2. Extract the downloaded ZIP file.
3. Place the extracted `Mods` folder in your mods directory under `%AppData%\7DaysToDie\`.

**Important for Multiplayer:** This mod communicates via its own custom network packages and therefore **must be installed on both the server and all clients**. The mod does not support EAC, which means the server must have EAC disabled.

## The Graphical In-Game Menu
Almost all functions of the mod can now be conveniently controlled via a custom UI menu.
*   **Access:** Open the in-game map and click the "Show ES Menu" button at the top.
*   **Client Section:** Local control over the map overlay, radar, HUD progress buff, and chat messages.
*   **Admin Section:** Direct adjustment of global zombie limits, spawn timers, tactical kills, and execution of cheat-clears for selected players.

## The AutoSpawner & Map Scanner
The vanilla spawn system often acts too slowly. Our AutoSpawner ensures that the world around you remains populated in a targeted manner.
*   **Default Behavior:** By default, the mod checks every 5 seconds if new zombies are needed and maintains a global limit of a maximum of 18 active zombies.
*   **Global Map Scanner:** A resource-friendly background thread scans the entire map once and automatically marks oceans and impassable terrain as "unspawnable".

## Clear Mechanics (How chunks are cleared)
1.  **Point of Origin (The Default Clear):** If you kill a zombie, the chunk from which it originally spawned is cleared.
2.  **Place of Death (Tactical Kill / Kiting):** If you kite a zombie into another chunk and kill it there, that chunk is also cleared. (Enabled by default).
3.  **Passing Through (Local Chunk Clear):** If you stay continuously in a chunk for 4 seconds, it is considered secured. (Enabled by default).

## Dynamic POI Radar & Map Overlay
*   **Map Overlay:** Freshly cleared chunks light up yellow briefly on the 2D map and are displayed darkened afterwards.
*   **POI Radar:** In uncleared POIs, a red 2D dot on the map or an orange 3D marker on the compass shows you the way to the next living enemy.
*   **Quests:** Once cleared, POIs are no longer available as quests. During buried supplies quests, enemy waves will no longer spawn.

## Important Gameplay Notes
*   **Heat Spawns:** Heat spawns (such as Screamers) must strictly be disabled, as they bypass the mod's spawn logic.
*   **Blood Moon:** A Blood Moon makes no sense from a gameplay perspective and should be disabled.
*   **New Player Buff:** The mod takes your level and playtime progress into account and spares you a bit in the beginning (newbie protection).

## Sandbox Settings (`Sandboxeinstellungen.txt`)
For world generation and the mod to function correctly, the sandbox settings must absolutely be set correctly by the user.
*   **Recommended Settings:** `ABBDBGFBHABLABWACHAEXGFCCFFAFKAEPAETK`
*   **Minimum Settings:** `ABWACHA`

<a id="console-commands"></a>
## Console Commands
All local player commands start with the prefix `es`, all server commands with `esa`.

### Client / User Commands
*   `es map <on/off/reload>`: Controls the personal map overlay or reloads markers.
*   `es msg <on/off>`: Enables or disables local chat messages from the mod.
*   `es progressbuff <on/off/time [sec]/radius [m]>`: Controls the HUD progress buff.
*   `es range [radius] [name/chunkX chunkZ]`: Checks the clearance progress in the vicinity.
*   `es where`: Universal radar, marks the nearest active zombie.

### Server / Admin Commands
*   `esa cheat_clear [player] [radius] [reset]`: Sets chunks in a radius to 'cleared' or resets their status.
*   `esa cheat_loud [player/coords] [rooms]`: Forces the nearest POI (max. 80m) to wake sleeping zombies.
*   `esa limit <number>`: Sets the global AutoSpawn limit for zombies.
*   `esa localclear <on/off/reason [name]>`: Controls the 4s-clear or runs diagnostics.
*   `esa range [player] [radius]`: Calculates the cleared area around a player.
*   `esa scanreset [hard]`: Restarts the map scan (the `hard` option deletes empty 0-entries from the database).
*   `esa tactical <on/off>`: Controls the server-side Tactical Kill.
*   `esa timer <seconds>`: Adjusts the AutoSpawn check interval.

---
This mod requires Harmony by Andreas Pardeike. Many thanks for his great work!