using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechVariableEngineGroup {
    public MuMechVariableEngine controller = null;
    public Dictionary<string, bool> groups = new Dictionary<string, bool>();
}

class MuMechVariableEngine : LiquidEngine {
    protected static Dictionary<Vessel, MuMechVariableEngineGroup> vessels = new Dictionary<Vessel, MuMechVariableEngineGroup>();
    protected static Rect winPos;

    public string group = "Main engine";
    public bool start = true;

    public string fuelType = "liquid";
    public string fuelType2 = "";
    public float fuelConsumption2 = 0;
    public bool liquidSeekAnywhere = false;

    public float efficiencyASL = 1;
    public float efficiencyVacuum = 1;

    public float nozzleExtension = 0;
    public float nozzleExtensionTime = 5;
    public Vector3 nozzleAxis = Vector3.up;

    public string rotor = "";
    public Vector3 rotorAxis = Vector3.forward;
    public float rotorMin = 0;
    public float rotorMax = 3600;
    public float rotorAirMult = 1;
    public float rotorAirMin = 1;
    public int rotorBlur = 20;
    public string rotor2 = "";
    public Vector3 rotor2Axis = Vector3.forward;
    public float rotor2Min = 0;
    public float rotor2Max = 3600;
    public float rotor2AirMult = 1;
    public float rotor2AirMin = 1;
    public int rotor2Blur = 20;
    public string rotor3 = "";
    public Vector3 rotor3Axis = Vector3.forward;
    public float rotor3Min = 0;
    public float rotor3Max = 3600;
    public float rotor3AirMult = 1;
    public float rotor3AirMin = 1;
    public int rotor3Blur = 20;

    protected bool gamePaused = false;
    protected float minThrustOrig = 0;
    protected float maxThrustOrig = 0;
    protected bool gotThrustOrig = false;
    protected Vector3 origGimbalPos = Vector3.zero;
    protected bool gotGimbalOrig = false;
    protected float nozzleProgress = 0;
    protected Transform nozzle = null;
    protected Transform[] rotorTrans = { null };
    protected Transform[] rotor2Trans = { null };
    protected Transform[] rotor3Trans = { null };

    protected override void onPartStart() {
        nozzle = transform.Find("model/obj_gimbal/nozzle");
        if (nozzle == null) {
            nozzle = transform.Find("model/obj_gimbal");
        }
        if (nozzle != null) {
            origGimbalPos = nozzle.localPosition;
            gotGimbalOrig = true;
            nozzle.localPosition = origGimbalPos + nozzleAxis.normalized * nozzleExtension;
        }
        if (rotor != "") {
            rotorTrans = new Transform[rotorBlur];
            rotorTrans[0] = transform.Find("model").FindChild(rotor);
            if (rotorTrans[0] != null) {
                for (int i = 1; i < rotorBlur; i++) {
                    GameObject neo = (GameObject)GameObject.Instantiate(rotorTrans[0].gameObject, rotorTrans[0].position, rotorTrans[0].rotation);
                    neo.transform.parent = rotorTrans[0].parent;
                    rotorTrans[i] = neo.transform;
                }

            }
        }
        if (rotor2 != "") {
            rotor2Trans = new Transform[rotor2Blur];
            rotor2Trans[0] = transform.Find("model").FindChild(rotor2);
            if (rotor2Trans[0] != null) {
                for (int i = 1; i < rotor2Blur; i++) {
                    GameObject neo = (GameObject)GameObject.Instantiate(rotor2Trans[0].gameObject, rotor2Trans[0].position, rotor2Trans[0].rotation);
                    neo.transform.parent = rotor2Trans[0].parent;
                    rotor2Trans[i] = neo.transform;
                }

            }
        }
        if (rotor3 != "") {
            rotor3Trans = new Transform[rotor3Blur];
            rotor3Trans[0] = transform.Find("model").FindChild(rotor3);
            if (rotor3Trans[0] != null) {
                for (int i = 1; i < rotor3Blur; i++) {
                    GameObject neo = (GameObject)GameObject.Instantiate(rotor3Trans[0].gameObject, rotor3Trans[0].position, rotor3Trans[0].rotation);
                    neo.transform.parent = rotor3Trans[0].parent;
                    rotor3Trans[i] = neo.transform;
                }

            }
        }

        base.onPartStart();
    }

    protected override void onPartAttach(Part parent) {
        if (gotGimbalOrig) {
            nozzle.localPosition = origGimbalPos + nozzleAxis.normalized * nozzleExtension;
        } else {
            nozzle = transform.Find("model/obj_gimbal/nozzle");
            if (nozzle == null) {
                nozzle = transform.Find("model/obj_gimbal");
            }
            if (nozzle != null) {
                origGimbalPos = nozzle.localPosition;
                gotGimbalOrig = true;
                nozzle.localPosition = origGimbalPos + nozzleAxis.normalized * nozzleExtension;
            }
        }

        base.onPartAttach(parent);
    }

    protected override void onPartDetach() {
        if (gotGimbalOrig) {
            nozzle.localPosition = origGimbalPos;
        } else {
            nozzle = transform.Find("model/obj_gimbal/nozzle");
            if (nozzle == null) {
                nozzle = transform.Find("model/obj_gimbal");
            }
            if (nozzle != null) {
                origGimbalPos = nozzle.localPosition;
                gotGimbalOrig = true;
            }
        }

        base.onPartDetach();
    }

    protected override void onActiveFixedUpdate() {
        if (vessels[vessel].groups[group]) {
            fuelLookupTargets.Clear();
            fuelLookupTargets.Add(this);
            base.onActiveFixedUpdate();
        } else {
            findFxGroup("active").setActive(false);
        }

        if ((nozzleExtension != 0) && (nozzleProgress < nozzleExtensionTime)) {
            nozzleProgress += TimeWarp.fixedDeltaTime;
            nozzle.localPosition = origGimbalPos + nozzleAxis.normalized * nozzleExtension * (1 - nozzleProgress / nozzleExtensionTime);
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

    protected override void onPartFixedUpdate() {
        if (rotorTrans[0] != null) {
            rotorTrans[0].localRotation = rotorTrans[rotorBlur - 1].localRotation;
            if ((state == PartStates.ACTIVE) && vessels[vessel].groups[group]) {
                rotorTrans[rotorBlur - 1].RotateAroundLocal(rotorAxis, Mathf.Sign(rotorMax) * Mathf.Max(Mathf.Abs(rotorMin + rotorMax * this.thrust / this.maxThrust), Mathf.Abs((float)FlightGlobals.ship_srfVelocity.magnitude * rotorAirMult)) * TimeWarp.fixedDeltaTime);
            } else {
                float airRot = Mathf.Abs((float)FlightGlobals.ship_srfVelocity.magnitude * rotorAirMult);
                if (airRot > rotorAirMin) {
                    rotorTrans[rotorBlur - 1].RotateAroundLocal(rotorAxis, Mathf.Sign(rotorMax) * airRot * TimeWarp.fixedDeltaTime);
                }
            }
            for (int i = 1; i < rotorBlur - 1; i++) {
                rotorTrans[i].localRotation = Quaternion.Lerp(rotorTrans[0].localRotation, rotorTrans[rotorBlur - 1].localRotation, (float)i / (float)rotorBlur);
            }
        }
        if (rotor2Trans[0] != null) {
            rotor2Trans[0].localRotation = rotor2Trans[rotor2Blur - 1].localRotation;
            if ((state == PartStates.ACTIVE) && vessels[vessel].groups[group]) {
                rotor2Trans[rotor2Blur - 1].RotateAroundLocal(rotor2Axis, Mathf.Sign(rotor2Max) * Mathf.Max(Mathf.Abs(rotor2Min + rotor2Max * this.thrust / this.maxThrust), Mathf.Abs((float)FlightGlobals.ship_srfVelocity.magnitude * rotor2AirMult)) * TimeWarp.fixedDeltaTime);
            } else {
                float airRot = Mathf.Abs((float)FlightGlobals.ship_srfVelocity.magnitude * rotor2AirMult);
                if (airRot > rotor2AirMin) {
                    rotor2Trans[rotor2Blur - 1].RotateAroundLocal(rotor2Axis, Mathf.Sign(rotor2Max) * airRot * TimeWarp.fixedDeltaTime);
                }
            }
            for (int i = 1; i < rotor2Blur - 1; i++) {
                rotor2Trans[i].localRotation = Quaternion.Lerp(rotor2Trans[0].localRotation, rotor2Trans[rotor2Blur - 1].localRotation, i / (float)rotor2Blur);
            }
        }
        if (rotor3Trans[0] != null) {
            rotor3Trans[0].localRotation = rotor3Trans[rotor3Blur - 1].localRotation;
            if ((state == PartStates.ACTIVE) && vessels[vessel].groups[group]) {
                rotor3Trans[rotor3Blur - 1].RotateAroundLocal(rotor3Axis, Mathf.Sign(rotor3Max) * Mathf.Max(Mathf.Abs(rotor3Min + rotor3Max * this.thrust / this.maxThrust), Mathf.Abs((float)FlightGlobals.ship_srfVelocity.magnitude * rotor3AirMult)) * TimeWarp.fixedDeltaTime);
            } else {
                float airRot = Mathf.Abs((float)FlightGlobals.ship_srfVelocity.magnitude * rotor3AirMult);
                if (airRot > rotor3AirMin) {
                    rotor3Trans[rotor3Blur - 1].RotateAroundLocal(rotor3Axis, Mathf.Sign(rotor3Max) * airRot * TimeWarp.fixedDeltaTime);
                }
            }
            for (int i = 1; i < rotor3Blur - 1; i++) {
                rotor3Trans[i].localRotation = Quaternion.Lerp(rotor3Trans[0].localRotation, rotor3Trans[rotor3Blur - 1].localRotation, i / (float)rotor3Blur);
            }
        }
        base.onPartFixedUpdate();
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
            if (liquidSeekAnywhere) {
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
                if (!base.RequestFuel(this, amount, reqId) && !base.RequestFuel(this, amount, Part.getFuelReqId())) {
                    return false;
                }
                return true;
            }
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
            state = PartStates.DEAD;
            return false;
        }

        if ((fuelConsumption2 > 0) && (fuelType2 != "") && !RequestFuelType(fuelType2, amount * (fuelConsumption2 / fuelConsumption), reqId)) {
            state = PartStates.DEAD;
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

    protected override void onCtrlUpd(FlightCtrlState ctrlState) {
        if (!gotThrustOrig) {
            minThrustOrig = minThrust;
            maxThrustOrig = maxThrust;
            gotThrustOrig = true;
        }

        float maxPress = vessel.mainBody.atmosphere ? (float)FlightGlobals.getStaticPressure(0, vessel.mainBody) : 0;
        float curPress = vessel.mainBody.atmosphere ? (float)FlightGlobals.getStaticPressure(vessel.mainBody.GetAltitude(vessel.transform.position), vessel.mainBody) : 0;

        float eff = Mathf.Clamp01(Mathf.Lerp(efficiencyVacuum, efficiencyASL, (maxPress > 0) ? (curPress / maxPress) : 0));
        maxThrust = 0.0001F + maxThrustOrig * eff;
        minThrust = minThrustOrig * eff;

        base.onCtrlUpd(ctrlState);
    }

    protected override void onFlightStart() {
        if (!vessels.ContainsKey(vessel)) {
            vessels[vessel] = new MuMechVariableEngineGroup();
        }
        if (!vessels[vessel].groups.ContainsKey(group) || start) {
            vessels[vessel].groups[group] = start;
        }

        if (!gotGimbalOrig) {
            nozzle = transform.Find("model/obj_gimbal/nozzle");
            if (nozzle == null) {
                nozzle = transform.Find("model/obj_gimbal");
            }
            if (nozzle != null) {
                origGimbalPos = nozzle.localPosition;
                gotGimbalOrig = true;
            }
        }
        if (gotGimbalOrig) {
            if (state == PartStates.ACTIVE) {
                nozzle.localPosition = origGimbalPos;
                nozzleProgress = nozzleExtensionTime;
            } else {
                nozzle.localPosition = origGimbalPos + nozzleAxis.normalized * nozzleExtension;
                nozzleProgress = 0;
            }
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
