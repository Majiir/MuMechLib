using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechRCSLiquidEngineGroup {
    public MuMechRCSLiquidEngine controller = null;
    public Dictionary<string, bool> groups = new Dictionary<string,bool>();
}

class MuMechRCSLiquidEngine : LiquidEngine {
    public static Dictionary<Vessel, MuMechRCSLiquidEngineGroup> vessels = new Dictionary<Vessel,MuMechRCSLiquidEngineGroup>();
    public static Rect winPos;

    public string group = "Main engine";
    public bool start = true;
    public bool gamePaused = false;

    protected override void onActiveFixedUpdate() {
        if (vessels[vessel].groups[group]) {
            fuelLookupTargets.Add(this);
            base.onActiveFixedUpdate();
        } else {
            findFxGroup("active").setActive(false);
        }
    }

    protected override void onActiveUpdate() {
        if (!vessels.ContainsKey(vessel)) {
            vessels[vessel] = new MuMechRCSLiquidEngineGroup();
        }
        if (vessels[vessel].controller == null) {
            vessels[vessel].controller = this;
            RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));
        }

        base.onActiveUpdate();
    }

    public override bool RequestFuel(Part source, float amount, uint reqId) {
        if (source == this) {
            fuelLookupTargets.Remove(this);
            return vessel.rootPart.RequestRCS(amount, 0);
        }
        return false;
    }

    private void WindowGUI(int windowID) {
        GUI.DragWindow(new Rect(0, 0, 10000, 25));

        GUILayout.BeginVertical();

        Dictionary<string, bool> tmpGrp = new Dictionary<string, bool>(vessels[vessel].groups);

        foreach (KeyValuePair<string, bool> grp in tmpGrp) {
            vessels[vessel].groups[grp.Key] = GUILayout.Toggle(grp.Value, grp.Key);
        }
        
        GUILayout.EndVertical();
    }

    private void drawGUI() {
        if ((state == PartStates.ACTIVE) && isControllable && InputLockManager.IsUnlocked(ControlTypes.THROTTLE) && !gamePaused) {
            if (winPos.x == 0 && winPos.y == 0) {
                winPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            }

            GUI.skin = HighLogic.Skin;

            winPos = GUILayout.Window(12345, winPos, WindowGUI, "Engine control", GUILayout.MinWidth(150));
        }
    }

    protected override void onPartUpdate() {
        base.onPartUpdate();
    }

    protected override void onDisconnect() {
        if (vessels.ContainsKey(vessel) && (vessels[vessel].controller == this)) {
            vessels[vessel].controller = null;
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
        }

        base.onDisconnect();
    }

    protected override void onPartDestroy() {
        if (vessels.ContainsKey(vessel) && (vessels[vessel].controller == this)) {
            vessels[vessel].controller = null;
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
        }

        base.onPartDestroy();
    }

    protected override void  onFlightStart() {
        if (!vessels.ContainsKey(vessel)) {
            vessels[vessel] = new MuMechRCSLiquidEngineGroup();
        }
        if (vessels[vessel].controller == null) {
            vessels[vessel].controller = this;
            RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));
        }
        if (!vessels[vessel].groups.ContainsKey(group) || start) {
            vessels[vessel].groups[group] = start;
        }

        base.onFlightStart();
    }

    protected override void onGamePause() {
        gamePaused = true;
        base.onGamePause();
    }

    protected override void onGameResume() {
        gamePaused = false;
        base.onGameResume();
    }
}
