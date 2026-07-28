🌐 [English](README.md) | 🇩🇪 [Deutsch](README_DE.md)
---

# EinmaligerSpawn (für 7D2D Version 3.x.x)

## Über diese Mod
Die Mod registriert, wenn du einen Chunk oder ein POI von Zombies gesäubert hast, und verhindert dauerhaft, dass sie in diesem Chunk respawnen. 
Aktuell befindet sich das Projekt in der Alpha-Phase (es ist die erste spielbare Alpha-Version) und wurde bisher nur im aktuellen Build getestet.

## Installation
Platziere den Ordner `Mods/EinmaligerSpawn` einfach in deinem Mod-Verzeichnis unter `C:\Users\yourCurrentUserprofilename\AppData\Roaming\7DaysToDie\Mods\`.

---

## Der AutoSpawner
Die Mod beinhaltet einen komplett eigenen AutoSpawner. Dieser wurde hinzugefügt, da das Vanilla-Spawnsystem des Spiels oft zu langsam und passiv agiert, wenn zu viele Bereiche um einen herum keinen Spawn mehr erlauben. Der Spawner sorgt dafür, dass die Welt um dich herum trotzdem bevölkert ist.
*   **Standard-Verhalten:** Die Mod prüft standardmäßig alle 5 Sekunden, ob neue Zombies benötigt werden, und hält ein globales Limit von maximal 18 aktiven Zombies aufrecht.
*   **Anpassbarkeit:** Du kannst diese Werte jederzeit im Spiel über die Konsolenbefehle `es timer <Sekunden>` und `es limit <Zahl>` an deine Vorlieben oder Serverleistung anpassen.

---

## Die Clear-Mechaniken (Wie Chunks gesäubert werden)
Du hast verschiedene Möglichkeiten, wie ein Chunk in der Mod als "ausgerottet" markiert wird. Fast alle dieser Mechaniken können individuell modifiziert oder ganz abgeschaltet werden, da die Einstellungen im Spielstand-Ordner in der `ModConfig.json` gespeichert werden.

1.  **Ursprungsort (Der Standard-Clear)**
    Tötest du einen von der Mod gespawnten Zombie, wird sofort der Chunk gesäubert, aus dem dieser Zombie ursprünglich stammte.
2.  **Todesort (Taktischer Kill / Kiting)**
    Zusätzlich zum Ursprungsort belohnt dich die Mod, wenn du taktisch spielst. Ziehst (kitest) du einen Zombie in einen anderen, angrenzenden Chunk und tötest ihn dort, wird auch dieser Todesort-Chunk mitgesäubert. 
    *   *Standard:* Diese Funktion ist standardmäßig aktiviert (`TaktischerKillAktiv = true`). 
    *   *Steuerung:* Du kannst den Taktik-Clear über die Konsole mit `es tactical <on/off>` jederzeit umschalten.
3.  **Durchlaufen (Lokaler Chunk Clear)**
    Du musst nicht jeden Chunk freikämpfen. Wenn du dich einfach nur 4 Sekunden lang ununterbrochen in einem Chunk aufhältst, gilt dieser ebenfalls als gesichert.
    *   *Standard:* Diese Funktion ist standardmäßig eingeschaltet (`LokalerChunkClearAktiv = true`).
    *   *Steuerung:* Möchtest du das Spiel härter machen, deaktiviere diese Mechanik über die Konsole mit `es localclear <on/off>`.

---

## Wichtige Hinweise zum Gameplay
*   **Heat-Spawns:** Heat-Spawns (wie z. B. Screamer) müssen zwingend deaktiviert sein, weil sie die Spawnlogik der Mod umgehen.
*   **Quests:** Einmal geclearte POIs sind nicht mehr als Quest verfügbar. Bei Grabequests spawnen keine Gegnerwellen mehr.
*   **Blutmond:** Ein Blutmond ergibt spieltechnisch keinen Sinn und sollte deaktiviert sein, weil er den Grundsatzgedanken der Mod aushebelt.
*   **Buff für neue Spieler:** Die Mod berücksichtigt euren Buff und verschont euch anfangs etwas.

---

## Sandbox-Einstellungen (`Sandboxeinstellungen.txt`)
Für die Weltgenerierung und die korrekte Funktion der Mod müssen die Sandbox-Einstellungen zwingend vom User korrekt gesetzt werden. Trage die Werte entsprechend in die Vorgaben ein.

*   **Empfohlene Einstellungen:** `ABBDBGFBHABLABWACHAEXGFCCFFAFKAEPAETK`
*   **Minimaleinstellungen:** `ABWACHA` (Diese Einstellungen stellen das absolute Minimum für ein funktionierendes Spielerlebnis dar).

---

## Konsolenbefehle
Die Mod bringt eine Reihe eigener Befehle für die Ingame-Konsole mit. Alle Befehle beginnen mit dem Präfix `es`.

### User-Befehle
*   `es map <on/off/reload>`: Nutze diesen Befehl für das grüne Karten-Overlay.
*   `es range [x]`: Um dir anzeigen zu lassen, wie viele Chunks in deiner Umgebung noch spawnen dürfen.
*   `es msg <on/off>`: Aktiviert oder deaktiviert die globalen Chat-Nachrichten (standardmäßig aktiviert)[cite: 6].
*   `es where`: Um den nähesten aktiven Zombie zu finden.
*   `es localclear reason`: Um herauszufinden, warum der Chunk nicht gesäubert ist.
*   `es cheat_lootbagmarker <on/off>`: Um Radar-Marker auf LootBags setzen zu lassen.

### Admin-Befehle
*   `es limit <Zahl>`: Um das maximale Autospawn-Limit zu setzen.
*   `es timer <Sekunden>`: Um das Autospawn-Intervall zu ändern.
*   `es localclear <on/off>`: Schaltet den automatischen 4s-Clear beim Durchlaufen um.
*   `es tactical <on/off>`: Aktiviert oder deaktiviert den Bonus-Clear (Todesort).
*   `es cheat_clear [x]`: Um Chunks im Umkreis auf "gecleart" zu setzen.