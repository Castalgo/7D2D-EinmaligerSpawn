🌐 [English](README.md) | 🇩🇪 [Deutsch](README_DE.md)
---

# EinmaligerSpawn (für 7D2D Version 3.x.x)

📌 **[⬇️ Direkt zu den Konsolenbefehlen springen](#konsolenbefehle)**

## Über diese Mod
Die Mod registriert, wenn du einen Chunk oder ein POI von Zombies gesäubert hast, und verhindert dauerhaft, dass sie in diesem Chunk respawnen. 
Aktuell befindet sich das Projekt in der Alpha-Phase (es ist die erste spielbare Alpha-Version) und wurde bisher nur im aktuellen Build getestet.

## Installation
1. Lade dir die aktuellste Version der Mod hier herunter: [EinmaligerSpawn Release](../../releases/tag/EinmaligerSpawn)
2. Entpacke die heruntergeladene ZIP-Datei.
3. Platziere den entpackten Ordner `Mods` in deinem Mod-Verzeichnis unter `%AppData%\7DaysToDie\`.

**Wichtig für Multiplayer:** Diese Mod kommuniziert über eigene Netzwerkpakete und muss daher **sowohl auf dem Server als auch bei allen Clients** installiert sein. Die Mod unterstützt kein EAC, d. h. der Server muss EAC abgeschaltet haben.

Tipp: Die Mod funktioniert prima mit [Advanced Minimap mod](https://www.nexusmods.com/7daystodie/mods/11073) zusammen.

---

## Der AutoSpawner
Die Mod beinhaltet einen komplett eigenen AutoSpawner. Dieser wurde hinzugefügt, da das Vanilla-Spawnsystem des Spiels oft zu langsam und passiv agiert, wenn zu viele Bereiche um einen herum keinen Spawn mehr erlauben. Der Spawner sorgt dafür, dass die Welt um dich herum trotzdem bevölkert ist.
*   **Standard-Verhalten:** Die Mod prüft standardmäßig alle 5 Sekunden, ob neue Zombies benötigt werden, und hält ein globales Limit von maximal 18 aktiven Zombies aufrecht.
*   **Anpassbarkeit:** Du kannst diese Werte jederzeit im Spiel über die Konsolenbefehle `esa timer <Sekunden>` und `esa limit <Zahl>` an deine Vorlieben oder Serverleistung anpassen.

---

## Die Clear-Mechaniken (Wie Chunks gesäubert werden)
Du hast verschiedene Möglichkeiten, wie ein Chunk in der Mod als "ausgerottet" markiert wird. Fast alle dieser Mechaniken können individuell modifiziert oder ganz abgeschaltet werden, da die Einstellungen im Spielstand-Ordner in der `EinmaligerSpawn_Config.json` gespeichert werden.

1.  **Ursprungsort (Der Standard-Clear)**
    Tötest du einen von der Mod gespawnten Zombie, wird sofort der Chunk gesäubert, aus dem dieser Zombie ursprünglich stammte.
2.  **Todesort (Taktischer Kill / Kiting)**
    Zusätzlich zum Ursprungsort belohnt dich die Mod, wenn du taktisch spielst. Ziehst (kitest) du einen Zombie in einen anderen, angrenzenden Chunk und tötest ihn dort, wird auch dieser Todesort-Chunk mitgesäubert. 
    *   *Standard:* Diese Funktion ist standardmäßig aktiviert (`TaktischerKillAktiv = true`). 
    *   *Steuerung:* Ist vom Host jderzeit ein- oder abschaltbar über die Weltkarte.
3.  **Durchlaufen (Lokaler Chunk Clear)**
    Du musst nicht jeden Chunk freikämpfen. Wenn du dich einfach nur 4 Sekunden lang ununterbrochen in einem Chunk aufhältst, gilt dieser ebenfalls als gesichert.
    *   *Standard:* Diese Funktion ist standardmäßig eingeschaltet (`LokalerChunkClearAktiv = true`).
    *   *Steuerung:* Ist vom Host jderzeit ein- oder abschaltbar über die Weltkarte.

---

## Wichtige Hinweise zum Gameplay
*   **Heat-Spawns:** Heat-Spawns (wie z. B. Screamer) müssen zwingend deaktiviert sein, weil sie die Spawnlogik der Mod umgehen.
*   **Quests:** Einmal geclearte POIs sind nicht mehr als Quest verfügbar. Bei Grabequests spawnen keine Gegnerwellen mehr.
*   **Blutmond:** Ein Blutmond ergibt spieltechnisch keinen Sinn und sollte deaktiviert sein, weil er den Grundsatzgedanken der Mod aushebelt und nur in nicht geclearten Chunks spawnen kann.
*   **Buff für neue Spieler:** Die Mod berücksichtigt euren Buff und verschont euch anfangs etwas.

---

## Sandbox-Einstellungen (`Sandboxeinstellungen.txt`)
Für die Weltgenerierung und die korrekte Funktion der Mod müssen die Sandbox-Einstellungen zwingend vom User korrekt gesetzt werden. Trage die Werte entsprechend in die Vorgaben ein.

*   **Empfohlene Einstellungen:** `ABBDBGFBHABLABWACHAEXGFCCFFAFKAEPAETK`
*   **Minimaleinstellungen:** `ABWACHA` (Diese Einstellungen stellen das absolute Minimum für ein funktionierendes Spielerlebnis dar).

---

<a id="konsolenbefehle"></a>
## Konsolenbefehle
Die Mod bringt eine Reihe eigener Befehle für die Ingame-Konsole mit. Alle lokalen Spieler-Befehle beginnen mit dem Präfix `es`, alle Server-Befehle mit `esa`. Nutze `es help` oder `esa help` im Spiel für eine Übersicht.

### Client / User Befehle (Für jeden lokal nutzbar)
*   `es map <on/off/reload>`: Um das persönliche Karten-Overlay zu steuern oder Marker neu zu laden.
*   `es msg <on/off>`: Um deine lokalen Chat-Nachrichten der Mod ein- oder auszuschalten.
*   `es progressbuff <on/off/time [sek]/radius [m]>`: Um den HUD-Fortschritt zu steuern, das Intervall oder den Suchradius anzupassen.
*   `es range [radius] [name]` ODER `es range [radius] [chunkX] [chunkZ]`: Um den Säuberungsfortschritt im Umkreis (Standard 120m) zu prüfen.
*   `es where`: Als Universal-Radar, um den nähesten aktiven Zombie zu markieren.

### Server / Admin Befehle (Nur für Host & Server-Admins)
*   `esa cheat_clear [Spieler] [radius] [reset]`: Um Chunks im Umkreis auf 'gesäubert' zu setzen oder den Status zu löschen (Reset).
*   `esa cheat_loud [Spieler/Coords] [Räume]`: Zwingt das nächste POI (max. 80m), seine schlafenden Zombies zu wecken und auf den Spieler zu hetzen.
*   `esa limit <Zahl>`: Um das globale Autospawn-Limit für Zombies auf dem Server festzulegen.
*   `esa localclear <on/off/reason [name]>`: Für den autom. 4s-Clear (on/off) oder zur Fehlerdiagnose bei einem Spieler (reason).
*   `esa range [Spieler] [radius]`: Um als Admin den geclearten Bereich um einen beliebigen Spieler zu berechnen.
*   `esa tactical <on/off>`: Um den serverseitigen Bonus-Clear (Taktischer Kill) ein- oder auszuschalten.
*   `esa timer <Sekunden>`: Um das serverseitige Autospawn-Überprüfungsintervall anzupassen.

---
This mod requires Harmony by Andreas Pardeike. Many thanks for his great work!