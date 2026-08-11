using System;
using System.Collections.Generic;
using Audio;
using HarmonyLib;
using EinmaligerSpawn.Config;
using UnityEngine;
using UnityEngine.Scripting;

// =========================================================================
// HILFSKLASSE: Die fehlende String-Combobox für die Engine-XML
// =========================================================================
[Preserve]
public class XUiC_ComboBoxString : XUiC_ComboBoxList<string>
{
}

// =========================================================================
// 1. Controller für das Menü
// =========================================================================
[Preserve]
public class XUiC_GrafischeModEinstellungen : XUiController
{
    // Client Befehle
    private XUiC_SimpleButton btnToggleMap;
    private XUiC_SimpleButton btnReloadMap;
    private bool isMapActive = true;

    private XUiC_SimpleButton btnToggleMsg;

    private XUiC_SimpleButton btnToggleClearing;
    private XUiC_ComboBoxInt cbxClearingRadius;
    private XUiC_SimpleButton btnApplyClearing;

    private XUiC_SimpleButton btnMarkEnemy;

    // Admin Befehle
    private XUiC_ComboBoxInt cbxLimit;
    private XUiC_ComboBoxInt cbxTimer;
    private XUiC_SimpleButton btnApplyLimit;
    private XUiC_SimpleButton btnApplyTimer;

    private XUiC_SimpleButton btnToggleTactical;

    private XUiV_Label lblPlayerName;
    private XUiC_SimpleButton btnPlayerPrev;
    private XUiC_SimpleButton btnPlayerNext;

    private XUiC_ComboBoxInt cbxCheatLoudRooms;
    private XUiC_SimpleButton btnCheatLoud;

    private XUiC_ComboBoxInt cbxClearRadius;
    private XUiC_SimpleButton btnCheatClear;

    private XUiC_SimpleButton btnToggleLocalClear;
    private XUiC_SimpleButton btnLocalClearDiagnose;

    private List<string> playerList = new List<string>();
    private int currentPlayerIndex = -1;

    // Init: Sucht UI-Elemente, setzt Startwerte und verknüpft Klick-Events.
    public override void Init()
    {
        base.Init();

        // Client Events
        btnToggleMap = GetChildById("btnToggleMap") as XUiC_SimpleButton;
        if (btnToggleMap != null) btnToggleMap.OnPressed += BtnToggleMap_OnPressed;

        btnReloadMap = GetChildById("btnReloadMap") as XUiC_SimpleButton;
        if (btnReloadMap != null) btnReloadMap.OnPressed += BtnReloadMap_OnPressed;

        btnToggleMsg = GetChildById("btnToggleMsg") as XUiC_SimpleButton;
        if (btnToggleMsg != null) btnToggleMsg.OnPressed += BtnToggleMsg_OnPressed;

        btnToggleClearing = GetChildById("btnToggleClearing") as XUiC_SimpleButton;
        if (btnToggleClearing != null) btnToggleClearing.OnPressed += BtnToggleClearing_OnPressed;

        cbxClearingRadius = GetChildById("cbxClearingRadius") as XUiC_ComboBoxInt;

        btnApplyClearing = GetChildById("btnApplyClearing") as XUiC_SimpleButton;
        if (btnApplyClearing != null) btnApplyClearing.OnPressed += BtnApplyClearing_OnPressed;

        btnMarkEnemy = GetChildById("btnMarkEnemy") as XUiC_SimpleButton;
        if (btnMarkEnemy != null) btnMarkEnemy.OnPressed += BtnMarkEnemy_OnPressed;


        // Admin Events
        cbxLimit = GetChildById("cbxLimit") as XUiC_ComboBoxInt;
        btnApplyLimit = GetChildById("btnApplyLimit") as XUiC_SimpleButton;
        if (btnApplyLimit != null) btnApplyLimit.OnPressed += BtnApplyLimit_OnPress;

        cbxTimer = GetChildById("cbxTimer") as XUiC_ComboBoxInt;
        btnApplyTimer = GetChildById("btnApplyTimer") as XUiC_SimpleButton;
        if (btnApplyTimer != null) btnApplyTimer.OnPressed += BtnApplyTimer_OnPress;

        btnToggleTactical = GetChildById("btnToggleTactical") as XUiC_SimpleButton;
        if (btnToggleTactical != null) btnToggleTactical.OnPressed += BtnToggleTactical_OnPressed;

        lblPlayerName = GetChildById("lblPlayerName")?.ViewComponent as XUiV_Label;
        btnPlayerPrev = GetChildById("btnPlayerPrev") as XUiC_SimpleButton;
        btnPlayerNext = GetChildById("btnPlayerNext") as XUiC_SimpleButton;

        if (btnPlayerPrev != null) btnPlayerPrev.OnPressed += BtnPlayerPrev_OnPressed;
        if (btnPlayerNext != null) btnPlayerNext.OnPressed += BtnPlayerNext_OnPressed;

        cbxCheatLoudRooms = GetChildById("cbxCheatLoudRooms") as XUiC_ComboBoxInt;
        if (cbxCheatLoudRooms != null && cbxCheatLoudRooms.Value == 0) cbxCheatLoudRooms.Value = 1;

        btnCheatLoud = GetChildById("btnCheatLoud") as XUiC_SimpleButton;
        if (btnCheatLoud != null) btnCheatLoud.OnPressed += BtnCheatLoud_OnPressed;

        cbxClearRadius = GetChildById("cbxClearRadius") as XUiC_ComboBoxInt;
        if (cbxClearRadius != null && cbxClearRadius.Value == 0) cbxClearRadius.Value = 20;

        btnCheatClear = GetChildById("btnCheatClear") as XUiC_SimpleButton;
        if (btnCheatClear != null) btnCheatClear.OnPressed += BtnCheatClear_OnPressed;

        btnToggleLocalClear = GetChildById("btnToggleLocalClear") as XUiC_SimpleButton;
        if (btnToggleLocalClear != null) btnToggleLocalClear.OnPressed += BtnToggleLocalClear_OnPressed;

        btnLocalClearDiagnose = GetChildById("btnLocalClearDiagnose") as XUiC_SimpleButton;
        if (btnLocalClearDiagnose != null) btnLocalClearDiagnose.OnPressed += BtnLocalClearDiagnose_OnPressed;
    }

    // OnOpen: Setzt Buttons aktiv und prüft Admin-Rechte für den Server-Teil.
    public override void OnOpen()
    {
        base.OnOpen();

        if (this.viewComponent != null)
        {
            this.viewComponent.IsVisible = true;
        }

        // ====== CLIENT BEREICH (Immer an) ======
        if (btnToggleMap != null)
        {
            btnToggleMap.Enabled = true;
            btnToggleMap.Text = isMapActive ? "ACTIVE" : "DISABLED";
        }
        if (btnReloadMap != null) btnReloadMap.Enabled = true;

        if (btnToggleMsg != null)
        {
            btnToggleMsg.Enabled = true;
            btnToggleMsg.Text = ModEinstellungen.ChatNachrichtenAktiv ? "ACTIVE" : "DISABLED";
        }

        if (btnToggleClearing != null)
        {
            btnToggleClearing.Enabled = true;
            btnToggleClearing.Text = ModEinstellungen.ZeigeLokalenFortschritt ? "ACTIVE" : "DISABLED";
        }

        if (cbxClearingRadius != null)
        {
            cbxClearingRadius.Enabled = true;
            cbxClearingRadius.Value = ModEinstellungen.ProgressBuffRadius;
        }

        if (btnApplyClearing != null) btnApplyClearing.Enabled = true;

        if (btnMarkEnemy != null) btnMarkEnemy.Enabled = true;


        // ====== ADMIN BEREICH (Rechteabhängig) ======
        bool isAdmin = SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;

        if (cbxLimit != null) { cbxLimit.Value = ModEinstellungen.GlobalesZombieLimit; cbxLimit.Enabled = isAdmin; }
        if (btnApplyLimit != null) btnApplyLimit.Enabled = isAdmin;

        if (cbxTimer != null) { cbxTimer.Value = (long)ModEinstellungen.SpawnCheckIntervall; cbxTimer.Enabled = isAdmin; }
        if (btnApplyTimer != null) btnApplyTimer.Enabled = isAdmin;

        if (btnToggleTactical != null)
        {
            btnToggleTactical.Enabled = isAdmin;
            btnToggleTactical.Text = ModEinstellungen.TaktischerKillAktiv ? "ACTIVE" : "DISABLED";
        }

        playerList.Clear();
        if (GameManager.Instance.World != null && GameManager.Instance.World.Players != null)
        {
            foreach (EntityPlayer p in GameManager.Instance.World.Players.list)
            {
                playerList.Add(p.EntityName);
            }
        }

        if (playerList.Count > 0)
        {
            currentPlayerIndex = 0;
            if (lblPlayerName != null) lblPlayerName.Text = playerList[0];
        }
        else
        {
            currentPlayerIndex = -1;
            if (lblPlayerName != null) lblPlayerName.Text = "---";
        }

        if (btnPlayerPrev != null) btnPlayerPrev.Enabled = isAdmin;
        if (btnPlayerNext != null) btnPlayerNext.Enabled = isAdmin;

        if (cbxCheatLoudRooms != null) cbxCheatLoudRooms.Enabled = isAdmin;
        if (btnCheatLoud != null) btnCheatLoud.Enabled = isAdmin;

        if (cbxClearRadius != null) cbxClearRadius.Enabled = isAdmin;
        if (btnCheatClear != null) btnCheatClear.Enabled = isAdmin;

        if (btnToggleLocalClear != null)
        {
            btnToggleLocalClear.Enabled = isAdmin;
            btnToggleLocalClear.Text = ModEinstellungen.LokalerChunkClearAktiv ? "ACTIVE" : "DISABLED";
        }
        if (btnLocalClearDiagnose != null) btnLocalClearDiagnose.Enabled = isAdmin;
    }

    // ====== CLIENT KLICK EVENTS ======

    // Klick (Client): Schaltet das Map-Overlay um (ON/OFF).
    private void BtnToggleMap_OnPressed(XUiController _sender, int _mouseButton)
    {
        isMapActive = !isMapActive;
        string cmd = isMapActive ? "es map on" : "es map off";
        SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync(cmd, null);

        if (btnToggleMap != null)
        {
            btnToggleMap.Text = isMapActive ? "ACTIVE" : "DISABLED";
        }
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // Klick (Client): Lädt die Marker des Map-Overlays neu.
    private void BtnReloadMap_OnPressed(XUiController _sender, int _mouseButton)
    {
        SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync("es map reload", null);
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // Klick (Client): Schaltet die Benachrichtigungen im Chat um (ON/OFF).
    private void BtnToggleMsg_OnPressed(XUiController _sender, int _mouseButton)
    {
        ModEinstellungen.ChatNachrichtenAktiv = !ModEinstellungen.ChatNachrichtenAktiv;
        ModEinstellungen.Speichern();

        if (btnToggleMsg != null)
        {
            btnToggleMsg.Text = ModEinstellungen.ChatNachrichtenAktiv ? "ACTIVE" : "DISABLED";
        }
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // Klick (Client): Schaltet den HUD Fortschritts-Buff um (ON/OFF).
    private void BtnToggleClearing_OnPressed(XUiController _sender, int _mouseButton)
    {
        bool newState = !ModEinstellungen.ZeigeLokalenFortschritt;
        string cmd = newState ? "es progressbuff on" : "es progressbuff off";
        SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync(cmd, null);

        if (btnToggleClearing != null)
        {
            btnToggleClearing.Text = newState ? "ACTIVE" : "DISABLED";
        }
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // Klick (Client): Wendet den neuen Suchradius für den HUD Fortschritts-Buff an.
    private void BtnApplyClearing_OnPressed(XUiController _sender, int _mouseButton)
    {
        if (cbxClearingRadius == null) return;
        int radius = (int)cbxClearingRadius.Value;

        string cmd = $"es progressbuff radius {radius}";
        SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync(cmd, null);

        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // Klick (Client): Führt 'es where' als Universal-Radar aus.
    private void BtnMarkEnemy_OnPressed(XUiController _sender, int _mouseButton)
    {
        SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync("es where", null);
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }


    // ====== ADMIN KLICK EVENTS ======

    // 1. Klick (Zurück): Wählt den vorherigen Spieler in der Liste.
    private void BtnPlayerPrev_OnPressed(XUiController _sender, int _mouseButton)
    {
        if (playerList.Count == 0) return;

        currentPlayerIndex--;
        if (currentPlayerIndex < 0) currentPlayerIndex = playerList.Count - 1;

        if (lblPlayerName != null) lblPlayerName.Text = playerList[currentPlayerIndex];
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // 2. Klick (Vor): Wählt den nächsten Spieler in der Liste.
    private void BtnPlayerNext_OnPressed(XUiController _sender, int _mouseButton)
    {
        if (playerList.Count == 0) return;

        currentPlayerIndex++;
        if (currentPlayerIndex >= playerList.Count) currentPlayerIndex = 0;

        if (lblPlayerName != null) lblPlayerName.Text = playerList[currentPlayerIndex];
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // 3. Klick: Führt 'esa cheat_clear' für den Target Player im gewählten Radius aus.
    private void BtnCheatClear_OnPressed(XUiController _sender, int _mouseButton)
    {
        if (currentPlayerIndex < 0 || currentPlayerIndex >= playerList.Count || cbxClearRadius == null) return;

        string selectedPlayer = playerList[currentPlayerIndex];
        int radius = (int)cbxClearRadius.Value;

        string cmd = $"esa cheat_clear \"{selectedPlayer}\" {radius} clear";
        SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync(cmd, null);

        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // 4. Klick: Führt 'esa cheat_loud' für den Target Player (gewählte Räume) aus.
    private void BtnCheatLoud_OnPressed(XUiController _sender, int _mouseButton)
    {
        if (currentPlayerIndex < 0 || currentPlayerIndex >= playerList.Count || cbxCheatLoudRooms == null) return;

        string selectedPlayer = playerList[currentPlayerIndex];
        int rooms = (int)cbxCheatLoudRooms.Value;

        string cmd = $"esa cheat_loud \"{selectedPlayer}\" {rooms}";
        SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync(cmd, null);

        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // 5. Klick: Speichert das Zombie-Limit und spielt Sound ab.
    private void BtnApplyLimit_OnPress(XUiController _sender, int _mouseButton)
    {
        if (cbxLimit == null) return;
        ModEinstellungen.GlobalesZombieLimit = (int)cbxLimit.Value;
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // 6. Klick: Schaltet den lokalen Chunk-Clear um (ON/OFF) und speichert direkt.
    private void BtnToggleLocalClear_OnPressed(XUiController _sender, int _mouseButton)
    {
        ModEinstellungen.LokalerChunkClearAktiv = !ModEinstellungen.LokalerChunkClearAktiv;
        ModEinstellungen.Speichern();

        if (btnToggleLocalClear != null)
        {
            btnToggleLocalClear.Text = ModEinstellungen.LokalerChunkClearAktiv ? "ACTIVE" : "DISABLED";
        }
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // 7. Klick: Führt 'esa localclear reason' für den Target Player aus (mit 'ui' für globalen Chat).
    private void BtnLocalClearDiagnose_OnPressed(XUiController _sender, int _mouseButton)
    {
        if (currentPlayerIndex < 0 || currentPlayerIndex >= playerList.Count) return;

        string selectedPlayer = playerList[currentPlayerIndex];

        string cmd = $"esa localclear reason \"{selectedPlayer}\" ui";
        SingletonMonoBehaviour<SdtdConsole>.Instance.ExecuteSync(cmd, null);

        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // 8. Klick: Schaltet den Tactical Kill um (ON/OFF) und speichert.
    private void BtnToggleTactical_OnPressed(XUiController _sender, int _mouseButton)
    {
        ModEinstellungen.TaktischerKillAktiv = !ModEinstellungen.TaktischerKillAktiv;
        ModEinstellungen.Speichern();

        if (btnToggleTactical != null)
        {
            btnToggleTactical.Text = ModEinstellungen.TaktischerKillAktiv ? "ACTIVE" : "DISABLED";
        }
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }

    // 9. Klick: Speichert den Spawn-Timer und spielt Sound ab.
    private void BtnApplyTimer_OnPress(XUiController _sender, int _mouseButton)
    {
        if (cbxTimer == null) return;
        ModEinstellungen.SpawnCheckIntervall = (float)cbxTimer.Value;
        Manager.PlayInsidePlayerHead("craft_complete_item", -1, 0f, false, false);
    }
}

// =========================================================================
// 2. Harmony Patch: Verknüpft den Text-Button bei jedem Öffnen der Karte
// =========================================================================
[HarmonyPatch(typeof(XUiC_MapArea), "OnOpen")]
public class MapArea_OnOpen_Patch
{
    // Postfix: Verknüpft den 'Hide/Show ES Menu'-Button beim Öffnen der Ingame-Karte.
    public static void Postfix(XUiC_MapArea __instance)
    {
        Log.Out("[ES_DEBUG] MapArea OnOpen - Suche Buttons...");

        XUiController btnText = __instance.GetChildById("btnESAdminToggleText");
        if (btnText != null)
        {
            if (btnText is XUiC_SimpleButton simpleBtnText)
            {
                simpleBtnText.Text = "Hide ES Menu";
                simpleBtnText.OnPressed -= OnMenuToggleClicked;
                simpleBtnText.OnPressed += OnMenuToggleClicked;
                Log.Out("[ES_DEBUG] Text-Button (SimpleButton) gefunden und verknuepft.");
            }
            else
            {
                btnText.OnPress -= OnMenuToggleClicked;
                btnText.OnPress += OnMenuToggleClicked;
                Log.Out("[ES_DEBUG] Text-Button (Standard) gefunden und verknuepft.");
            }
        }
        else
        {
            Log.Out("[ES_DEBUG] FEHLER: Text-Button NICHT gefunden!");
        }
    }

    // Klick: Schaltet die Sichtbarkeit des Admin-Menüs um und passt den Button-Text an.
    private static void OnMenuToggleClicked(XUiController _sender, int _mouseButton)
    {
        Log.Out("[ES_DEBUG] KLICK ERKANNT von: " + _sender.ViewComponent.ID);

        XUiV_Window adminWin = _sender.xui.GetWindow("windowEinmaligerSpawnAdmin");
        if (adminWin != null)
        {
            adminWin.IsVisible = !adminWin.IsVisible;
            _sender.xui.calculateWindowGroupLayout(_sender.xui.GetWindowGroupById("map"));

            XUiV_Window mapWin = _sender.xui.GetWindow("mapArea");
            if (mapWin != null && mapWin.Controller != null)
            {
                XUiController btnText = mapWin.Controller.GetChildById("btnESAdminToggleText");
                if (btnText is XUiC_SimpleButton simpleBtn)
                {
                    simpleBtn.Text = adminWin.IsVisible ? "Hide ES Menu" : "Show ES Menu";
                }
            }
        }
        else
        {
            Log.Out("[ES_DEBUG] FEHLER: windowEinmaligerSpawnAdmin nicht gefunden!");
        }
    }
}