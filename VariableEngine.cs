using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechVariableEngineGroup {
    public MuMechVariableEngine controller = null;
    public Dictionary<string, bool> groups = new Dictionary<string, bool>();
}

class MuMechVariableEngine : LiquidEngine {
    public static Dictionary<Vessel, MuMechVariableEngineGroup> vessels = new Dictionary<Vessel, MuMechVariableEngineGroup>();
    public static Rect winPos;

    public string group = "Main engine";
    public bool start = true;

    public string fuelType = "liquid";
    public string fuelType2 = "";
    public float fuelConsumption2 = 0;

    public bool gamePaused = false;

    protected override void onActiveFixedUpdate() {
        if (vessels[vessel].groups[group]) {
            fuelLookupTargets.Clear();
            fuelLookupTargets.Add(this);
            base.onActiveFixedUpdate();
        } else {
            findFxGroup("active").setActive(false);
        }
    }

    protected override void onActiveUpdate() {
        if ((vessel != null) && !vessels.ContainsKey(vessel)) {
            vessels[vessel] = new MuMechVariableEngineGroup();
        }
        if (vessels[vessel].controller == null) {
            vessels[vessel].controller = this;
            RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));
        }
        if (!vessels[vessel].groups.ContainsKey(group)) {
            vessels[vessel].groups[group] = start;
        }

        base.onActiveUpdate();
    }

    public bool RequestFuelType(string type, float amount, uint reqId) {
        if (type.ToLowerInvariant() == "rcs") {
            Dictionary<int, List<RCSFuelTank>> cand = new Dictionary<int, List<RCSFuelTank>>();
            int maxStage = -1;

            foreach (Part p in vessel.parts) {
                if (p is RCSFuelTank) {
                    if ((p.State == PartStates.ACTIVE) || (p.State == PartStates.IDLE)) {
                        if (!cand.ContainsKey(p.inverseStage)) {
                            cand[p.inverseStage] = new List<RCSFuelTank>();
                        }
                        cand[p.inverseStage].Add((RCSFuelTank)p);
                        if (p.inverseStage > maxStage) {
                            maxStage = p.inverseStage;
                        }
                    }
                }
            }

            if (maxStage == -1) {
                return false;
            }

            float partAmount = amount / cand[maxStage].Count;

            foreach (RCSFuelTank t in cand[maxStage]) {
                t.getFuel(partAmount);
            }

            return true;
        } else if (type.ToLowerInvariant() == "liquid") {
            Dictionary<int, List<FuelTank>> cand = new Dictionary<int, List<FuelTank>>();
            int maxStage = -1;

            foreach (Part p in vessel.parts) {
                if (p is FuelTank) {
                    if ((p.State == PartStates.ACTIVE) || (p.State == PartStates.IDLE)) {
                        if (!cand.ContainsKey(p.inverseStage)) {
                            cand[p.inverseStage] = new List<FuelTank>();
                        }
                        cand[p.inverseStage].Add((FuelTank)p);
                        if (p.inverseStage > maxStage) {
                            maxStage = p.inverseStage;
                        }
                    }
                }
            }

            if (maxStage == -1) {
                return false;
            }

            float partAmount = amount / cand[maxStage].Count;

            foreach (FuelTank t in cand[maxStage]) {
                t.RequestFuel(this, partAmount, reqId);
            }

            return true;
        } else {
            Dictionary<int, List<MuMechVariableTank>> cand = new Dictionary<int, List<MuMechVariableTank>>();
            int maxStage = -1;

            foreach (Part p in vessel.parts) {
                if (p is MuMechVariableTank) {
                    if ((((MuMechVariableTank)p).type == type) && (((MuMechVariableTank)p).fuel > 0)) {
                        if (!cand.ContainsKey(p.inverseStage)) {
                            cand[p.inverseStage] = new List<MuMechVariableTank>();
                        }
                        cand[p.inverseStage].Add((MuMechVariableTank)p);
                        if (p.inverseStage > maxStage) {
                            maxStage = p.inverseStage;
                        }
                    }
                }
            }

            if (maxStage == -1) {
                return false;
            }

            float partAmount = amount / cand[maxStage].Count;

            foreach (MuMechVariableTank t in cand[maxStage]) {
                t.getFuel(type, partAmount);
            }

            return true;
        }
    }

    public override bool RequestFuel(Part source, float amount, uint reqId) {
        if (source != this) {
            return false;
        }

        fuelLookupTargets.Clear();

        if (!RequestFuelType(fuelType, amount, reqId)) {
            return false;
        }

        if ((fuelConsumption2 > 0) && (fuelType2 != "") && !RequestFuelType(fuelType2, amount * (fuelConsumption2 / fuelConsumption), reqId)) {
            return false;
        }

        return true;
    }

    private void WindowGUI(int windowID) {
        GUILayout.BeginVertical();

        Dictionary<string, bool> tmpGrp = new Dictionary<string, bool>(vessels[vessel].groups);

        foreach (KeyValuePair<string, bool> grp in tmpGrp) {
            vessels[vessel].groups[grp.Key] = GUILayout.Toggle(grp.Value, grp.Key);
        }

        GUILayout.EndVertical();

        GUI.DragWindow();
    }

    private void drawGUI() {
        if ((vessel == FlightGlobals.ActiveVessel) && (vessels[vessel].controller == this) && (state == PartStates.ACTIVE) && isControllable && InputLockManager.IsUnlocked(ControlTypes.THROTTLE) && !gamePaused) {
            if (winPos.x == 0 && winPos.y == 0) {
                winPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            }

            GUI.skin = HighLogic.Skin;

            winPos = GUILayout.Window(123, winPos, WindowGUI, "Engine control", GUILayout.MinWidth(150));
        }
    }

    protected override void onPartUpdate() {
        base.onPartUpdate();
    }

    protected override void onPartDeactivate() {
        if ((vessel != null) && vessels.ContainsKey(vessel) && (vessels[vessel].controller == this)) {
            vessels[vessel].controller = null;
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
        }

        base.onPartDeactivate();
    }

    protected override void onDisconnect() {
        if ((vessel != null) && vessels.ContainsKey(vessel) && (vessels[vessel].controller == this)) {
            vessels[vessel].controller = null;
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
        }

        base.onDisconnect();
    }

    protected override void onPartDestroy() {
        if ((vessel != null) && vessels.ContainsKey(vessel) && (vessels[vessel].controller == this)) {
            vessels[vessel].controller = null;
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
        }

        base.onPartDestroy();
    }

    protected override void onFlightStart() {
        if (!vessels.ContainsKey(vessel)) {
            vessels[vessel] = new MuMechVariableEngineGroup();
        }
        if (!vessels[vessel].groups.ContainsKey(group) || start) {
            vessels[vessel].groups[group] = start;
        }

        base.onFlightStart();

        fuelSource = this;
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
