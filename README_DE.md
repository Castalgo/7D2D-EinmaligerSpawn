🌐 [English](README.md) | 🇩🇪 [Deutsch](README_DE.md)
---

# EinmaligerSpawn (für 7D2D Version 3.x.x)

📌 **[⬇️ Direkt zu den Konsolenbefehlen springen](#konsolenbefehle)**

## Über diese Mod
Die Mod registriert, wenn du einen Chunk oder ein POI von Zombies gesäubert hast, und verhindert dauerhaft, dass sie in diesem Chunk respawnen. Sie bietet zudem ein dynamisches Radar für Gebäude, ein grafisches Karten-Overlay für deinen Fortschritt und einen globalen Hintergrund-Scanner für die Spielwelt.

## Installation
1. Lade dir die aktuellste Version der Mod hier herunter: [EinmaligerSpawn Release](../../releases/tag/EinmaligerSpawn)
2. Entpacke die heruntergeladene ZIP-Datei.
3. Platziere den entpackten Ordner `Mods` in deinem Mod-Verzeichnis unter `%AppData%\7DaysToDie\`.

**Wichtig für Multiplayer:** Diese Mod kommuniziert über eigene Netzwerkpakete und muss daher **sowohl auf dem Server als auch bei allen Clients** installiert sein. Die Mod unterstützt kein EAC, d. h. der Server muss EAC abgeschaltet haben.

**Empfehlung / Kompatibilität:** Diese Mod funktioniert hervorragend mit der [Advanced Minimap mod](https://www.nexusmods.com/7daystodie/mods/11073) zusammen. Wir empfehlen ausdrücklich, diese zusätzlich zu installieren, damit das dynamische POI-Radar und das Karten-Overlay der gesäuberten Gebiete in Echtzeit direkt auf deiner Minimap angezeigt werden!

## Das Grafische Ingame-Menü (Neu)
Fast alle Funktionen der Mod lassen sich nun bequem über ein eigenes UI-Menü steuern.
*   **Zugriff:** Öffne die Ingame-Karte und klicke oben auf den Button "Show ES Menu".
*   **Client-Bereich:** Lokale Steuerung von Map-Overlay, Radar, HUD-Fortschrittsbuff und Chat-Nachrichten.
*   **Admin-Bereich:** Direkte Anpassung von globalen Zombie-Limits, Spawn-Timern, Taktischen Kills sowie Ausführung von Cheat-Clears für ausgewählte Spieler.

## Der AutoSpawner & Map-Scanner
Das Vanilla-Spawnsystem agiert oft zu langsam. Unser AutoSpawner sorgt dafür, dass die Welt um dich herum gezielt bevölkert bleibt.
*   **Standard-Verhalten:** Die Mod prüft standardmäßig alle 5 Sekunden, ob neue Zombies benötigt werden, und hält ein globales Limit von maximal 18 aktiven Zombies aufrecht.
*   **Globaler Map-Scanner:** Ein ressourcenschonender Hintergrund-Thread analysiert einmalig die gesamte Karte und markiert Ozeane sowie unpassierbares Terrain automatisch als "unspawnbar".

## Die Clear-Mechaniken (Wie Chunks gesäubert werden)
1.  **Ursprungsort (Der Standard-Clear):** Tötest du einen Zombie, wird der Chunk gesäubert, aus dem er stammte.
2.  **Todesort (Taktischer Kill / Kiting):** Ziehst du einen Zombie in einen anderen Chunk und tötest ihn dort, wird dieser ebenfalls gesäubert. (Standardmäßig aktiv).
3.  **Durchlaufen (Lokaler Chunk Clear):** Hältst du dich 4 Sekunden lang ununterbrochen in einem Chunk auf, gilt dieser als gesichert. (Standardmäßig aktiv).

## Dynamisches POI-Radar & Karten-Overlay
*   **Karten-Overlay:** Frisch gesäuberte Chunks leuchten auf der 2D-Karte kurz gelb auf und werden danach abgedunkelt dargestellt. 
*   **POI-Radar:** In noch nicht gesäuberten POIs zeigt dir ein roter 2D-Punkt auf der Karte oder ein oranger 3D-Marker im Kompass den Weg zum nächsten lebenden Feind.
*   **Quests:** Einmal geclearte POIs sind nicht mehr als Quest verfügbar. Bei Grabequests spawnen keine Gegnerwellen mehr.

## Wichtige Hinweise zum Gameplay
*   **Heat-Spawns:** Heat-Spawns (wie z. B. Screamer) müssen zwingend deaktiviert sein, weil sie die Spawnlogik der Mod umgehen.
*   **Blutmond:** Ein Blutmond ergibt spieltechnisch keinen Sinn, weil geblockte Biom-Spawns auch Blutmondzombies am spawnen hindert.
*   **Buff für neue Spieler:** Die Mod berücksichtigt deinen Level- und Spielzeit-Fortschritt und verschont dich anfangs (Anfängerschutz), aber setzt den Lokaler Chunk Clear-Zeitbedarf ebenfalls hoch.

## Sandbox-Einstellungen (`Sandboxeinstellungen.txt`)
Für die Weltgenerierung und die korrekte Funktion der Mod müssen die Sandbox-Einstellungen zwingend vom User korrekt gesetzt werden.
*   **Empfohlene Einstellungen:** `ABBDBGFBHABLABWACHAEXGFCCFFAFKAEPAETK`
*   **Minimaleinstellungen:** `ABWACHA`

<a id="konsolenbefehle"></a>
## Konsolenbefehle
Alle lokalen Spieler-Befehle beginnen mit dem Präfix `es`, alle Server-Befehle mit `esa`.

### Client / User Befehle
*   `es map <on/off/reload>`: Steuert das persönliche Karten-Overlay oder lädt Marker neu.
*   `es msg <on/off>`: Schaltet lokale Chat-Nachrichten der Mod ein oder aus.
*   `es progressbuff <on/off/time [sek]/radius [m]>`: Steuert den HUD-Fortschrittsbuff.
*   `es range [radius] [name/chunkX chunkZ]`: Prüft den Säuberungsfortschritt im Umkreis.
*   `es where`: Universal-Radar, markiert den nähesten aktiven Zombie.

### Server / Admin Befehle
*   `esa cheat_clear [Spieler] [radius] [reset]`: Setzt Chunks im Umkreis auf 'gesäubert' oder löscht den Status.
*   `esa cheat_loud [Spieler/Coords] [Räume]`: Zwingt das nächste POI (max. 80m), schlafende Zombies zu wecken.
*   `esa limit <Zahl>`: Legt das globale Autospawn-Limit für Zombies fest.
*   `esa localclear <on/off/reason [name]>`: Steuert den 4s-Clear oder führt Fehlerdiagnosen durch.
*   `esa range [Spieler] [radius]`: Berechnet den geclearten Bereich um einen Spieler.
*   `esa scanreset [hard]`: Startet den Map-Scan neu (Option `hard` löscht leere 0-Einträge aus der Datenbank).
*   `esa tactical <on/off>`: Steuert den serverseitigen Taktischen Kill.
*   `esa timer <Sekunden>`: Passt das Autospawn-Überprüfungsintervall an.

---
This mod requires Harmony by Andreas Pardeike. Many thanks for his great work!