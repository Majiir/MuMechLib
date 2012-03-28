using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MuMech;

namespace MuMech {
    class VesselStateKeeper {
        public List<MechJebCore> jebs = new List<MechJebCore>();
        public MechJebCore controller = null;
        public VesselState state = new VesselState();
    }

    class MechJebCore {
        public enum RotationReference {
            INERTIAL,
            ORBIT,
            SURFACE,
            TARGET
        }

        public enum TargetType {
            NONE,
            VESSEL,
            BODY
        }

        public enum TMode {
            OFF,
            KEEP_ORBITAL,
            KEEP_SURFACE,
            KEEP_VERTICAL
        }

        public enum WindowStat {
            HIDDEN,
            MINIMIZED,
            NORMAL,
            OPENING,
            CLOSING
        }

        private TMode prev_tmode = TMode.OFF;
        private TMode _tmode = TMode.OFF;
        public TMode tmode {
            get {
                return _tmode;
            }
            set {
                if (_tmode != value) {
                    prev_tmode = _tmode;
                    _tmode = value;
                    tmode_changed = true;
                }
            }
        }

        public static Dictionary<Vessel, VesselStateKeeper> allJebs = new Dictionary<Vessel, VesselStateKeeper>();

        public List<ComputerModule> modules = new List<ComputerModule>();

        public VesselState vesselState;

        public Part part;

        private static bool calibrationMode = true;
        private static int windowIDbase = 60606;
        private int calibrationTarget = 0;
        private double[] calibrationDeltas = { 0.00001, 0.00005, 0.0001, 0.0005, 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10, 50, 100, 500 };
        private int calibrationDelta = 0;

        public static SettingsManager settings = null;
        public bool settingsLoaded = false;
        public bool settingsChanged = false;
        public bool gamePaused = false;
        public bool firstDraw = true;
        public float stress = 0;

        // SAS
        private bool flyByWire = false;
        private bool tmode_changed = false;
        private Vector3d integral = Vector3d.zero;
        private Vector3d prev_err = Vector3d.zero;
        private Vector3 act = Vector3.zero;
        private Vector3d k_integral = Vector3d.zero;
        private Vector3d k_prev_err = Vector3d.zero;
        private double t_integral = 0;
        private double t_prev_err = 0;
        public float Kp = 12.0F;
        public float Ki = 0.001F;
        public float Kd = 30.0F;
        public float t_Kp = 0.05F;
        public float t_Ki = 0.000001F;
        public float t_Kd = 0.05F;
        public float r_Kp = 0;
        public float r_Ki = 0;
        public float r_Kd = 0;
        public float Damping = 0.0F;

        public float trans_spd_act = 0;
        public float trans_prev_thrust = 0;
        public bool trans_kill_h = false;
        public bool trans_land = false;
        public bool trans_land_gears = false;

        public Quaternion internal_target = Quaternion.identity;

        protected bool rotationChanged = false;
        protected bool _rotationActive = false;
        public bool rotationActive {
            get {
                return _rotationActive;
            }
            set {
                if (_rotationActive != value) {
                    _rotationActive = value;
                    rotationChanged = true;
                }
            }
        }

        protected RotationReference _rotationReference = RotationReference.INERTIAL;
        public RotationReference rotationReference {
            get {
                return _rotationReference;
            }
            set {
                if (_rotationReference != value) {
                    _rotationReference = value;
                    rotationChanged = true;
                }
            }
        }

        protected Quaternion _rotationTarget = Quaternion.identity;
        public Quaternion rotationTarget {
            get {
                return _rotationTarget;
            }
            set {
                if (Mathf.Abs(Quaternion.Angle(_rotationTarget, value)) > 10) {
                    rotationChanged = true;
                }
                _rotationTarget = value;
            }
        }

        protected bool targetChanged = false;
        protected TargetType _targetType = TargetType.NONE;
        public TargetType targetType {
            get {
                return _targetType;
            }
            set {
                if (_targetType != value) {
                    _targetType = value;
                    targetChanged = true;
                    if (rotationReference == RotationReference.TARGET) {
                        rotationChanged = true;
                    }
                }
            }
        }

        protected Vessel _targetVessel = null;
        public Vessel targetVessel {
            get {
                return _targetVessel;
            }
            set {
                if (_targetVessel != value) {
                    _targetVessel = value;
                    if (targetType == TargetType.VESSEL) {
                        targetChanged = true;
                        if (rotationReference == RotationReference.TARGET) {
                            rotationChanged = true;
                        }
                    }
                }
            }
        }

        protected CelestialBody _targetBody = null;
        public CelestialBody targetBody {
            get {
                return _targetBody;
            }
            set {
                if (_targetBody != value) {
                    _targetBody = value;
                    if (targetType == TargetType.BODY) {
                        targetChanged = true;
                        if (rotationReference == RotationReference.TARGET) {
                            rotationChanged = true;
                        }
                    }
                }
            }
        }

        // Main window
        private WindowStat _main_windowStat;
        protected WindowStat main_windowStat {
            get { return _main_windowStat; }
            set {
                if (_main_windowStat != value) {
                    _main_windowStat = value;
                    settingsChanged = true;
                }
            }
        }

        private float _main_windowProgr;
        protected float main_windowProgr {
            get { return _main_windowProgr; }
            set {
                if (_main_windowProgr != value) {
                    _main_windowProgr = value;
                    settingsChanged = true;
                }
            }
        }

        public void saveSettings() {
            if (!settingsLoaded) {
                return;
            }

            foreach (ComputerModule module in modules) {
                module.onSaveGlobalSettings(settings);
            }
            settings["main_windowStat"].value_integer = (int)main_windowStat;
            settings["main_windowProgr"].value_decimal = main_windowProgr;
            settings.save();
            settingsChanged = false;
        }

        public void loadSettings() {
            if (settings == null) {
                settings = new SettingsManager(KSPUtil.ApplicationRootPath + "MuMech/MechJeb.cfg");
            }

            foreach (ComputerModule module in modules) {
                module.onLoadGlobalSettings(settings);
            }
            main_windowStat = (WindowStat)settings["main_windowStat"].value_integer;
            main_windowProgr = (float)settings["main_windowProgr"].value_decimal;
            settingsChanged = false;
            settingsLoaded = true;
        }

        public void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
            saveSettings();

            partDataCollection.Add("tmode", new KSPParseable((int)tmode, KSPParseable.Type.INT));
            partDataCollection.Add("trans_spd_act", new KSPParseable(trans_spd_act, KSPParseable.Type.FLOAT));
            partDataCollection.Add("trans_kill_h", new KSPParseable(trans_kill_h, KSPParseable.Type.BOOL));
            partDataCollection.Add("trans_land", new KSPParseable(trans_land, KSPParseable.Type.BOOL));

            foreach (ComputerModule module in modules) {
                module.onFlightStateSave(partDataCollection);
            }
        }

        public void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
            loadSettings();

            if (parsedData.ContainsKey("tmode")) tmode = (TMode)parsedData["tmode"].value_int;
            if (parsedData.ContainsKey("trans_spd_act")) trans_spd_act = parsedData["trans_spd_act"].value_float;
            if (parsedData.ContainsKey("trans_kill_h")) trans_kill_h = parsedData["trans_kill_h"].value_bool;
            if (parsedData.ContainsKey("trans_land")) trans_land = parsedData["trans_land"].value_bool;

            foreach (ComputerModule module in modules) {
                module.onFlightStateLoad(parsedData);
            }
        }

        private void onFlyByWire(FlightCtrlState s) {
            if ((part.vessel != FlightGlobals.ActiveVessel) || (allJebs[part.vessel].controller != this)) {
                return;
            }
            drive(s);
        }

        public Quaternion rotationGetReferenceRotation(RotationReference reference) {
            Quaternion rotRef = Quaternion.identity;
            switch (reference) {
                case RotationReference.ORBIT:
                    rotRef = Quaternion.LookRotation(vesselState.velocityVesselOrbitUnit, vesselState.up);
                    break;
                case RotationReference.SURFACE:
                    rotRef = vesselState.rotationSurface;
                    break;
                case RotationReference.TARGET:
                    switch (targetType) {
                        case TargetType.VESSEL:
                            rotRef = Quaternion.LookRotation((targetVessel.findWorldCenterOfMass() - vesselState.CoM).normalized, -part.vessel.transform.forward);
                            break;
                        case TargetType.BODY:
                            rotRef = Quaternion.LookRotation((targetBody.position - vesselState.CoM).normalized, -part.vessel.transform.forward);
                            break;
                    }
                    break;
            }
            return rotRef;
        }

        public Vector3d rotationWorldToReference(Vector3d vector, RotationReference reference) {
            return Quaternion.Inverse(rotationGetReferenceRotation(reference)) * vector;
        }

        public Vector3d rotationReferenceToWorld(Vector3d vector, RotationReference reference) {
            return rotationGetReferenceRotation(reference) * vector;
        }

        public void rotateTo(Quaternion rotation, RotationReference reference) {
            rotationReference = reference;
            rotationTarget = rotation;
            rotationActive = true;
        }

        public void rotateTo(Vector3d direction, RotationReference reference) {
            rotateTo(Quaternion.LookRotation(direction, rotationWorldToReference(-part.vessel.transform.forward, reference)), reference);
        }

        public void setTarget(Vessel target) {
            targetVessel = target;
            targetType = TargetType.VESSEL;
        }

        public void setTarget(CelestialBody target) {
            targetBody = target;
            targetType = TargetType.BODY;
        }

        private void drive(FlightCtrlState s) {
            stress = 0;

            if (part.State == PartStates.DEAD) {
                return;
            }

            foreach (ComputerModule module in modules) {
                if (module.enabled) {
                    module.drive(s);
                }
            }

            if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) {
                return;
            }

            if (tmode != TMode.OFF) {
                double spd = 0;
                bool fullBlast = false;

                if (trans_land) {
                    if (part.vessel.Landed || part.vessel.Splashed) {
                        tmode = TMode.OFF;
                        trans_land = false;
                    } else {
                        tmode = TMode.KEEP_VERTICAL;
                        trans_kill_h = true;
                        double minalt = Math.Min(vesselState.altitudeASL, vesselState.altitudeTrue);
                        trans_spd_act = (float)-Math.Sqrt((vesselState.thrustAvailable / vesselState.mass - vesselState.gravityForce.magnitude) * 2 * minalt) * 0.50F;
                        if (!trans_land_gears && (minalt < 1000)) {
                            foreach (Part p in part.vessel.parts) {
                                if (p is LandingLeg) {
                                    LandingLeg l = (LandingLeg)p;
                                    if (l.legState == LandingLeg.LegStates.RETRACTED) {
                                        l.DeployOnActivate = true;
                                        l.force_activate();
                                    }
                                }
                            }
                            trans_land_gears = true;
                        }
                        if (minalt < 200) {
                            trans_spd_act = -Mathf.Lerp(0, (float)Math.Sqrt((vesselState.thrustAvailable / vesselState.mass - vesselState.gravityForce.magnitude) * 2 * 200) * 0.50F, (float)minalt / 200);
                            /*
                            if ((minalt >= 90) && (vesselState.speedHorizontal > 1)) {
                                trans_spd_act = -Mathf.Lerp(0, (float)Math.Sqrt((vesselState.thrustAvailable / vesselState.mass - vesselState.gravityForce.magnitude) * 2 * 200) * 0.90F, (float)(minalt - 100) / 300);
                            } else {
                                trans_spd_act = -Mathf.Lerp(0, (float)Math.Sqrt((vesselState.thrustAvailable / vesselState.mass - vesselState.gravityForce.magnitude) * 2 * 200) * 0.90F, (float)minalt / 300);
                            }
                            */
                        }
                    }
                }

                switch (tmode) {
                    case TMode.KEEP_ORBITAL:
                        spd = vesselState.speedOrbital;
                        break;
                    case TMode.KEEP_SURFACE:
                        spd = vesselState.speedSurface;
                        break;
                    case TMode.KEEP_VERTICAL:
                        spd = vesselState.speedVertical;
                        Vector3d rot = Vector3d.up;
                        if (trans_kill_h) {
                            Vector3 hsdir = Vector3.Exclude(vesselState.up, part.vessel.orbit.GetVel() - part.vessel.mainBody.getRFrmVel(vesselState.CoM));
                            Vector3 dir = -hsdir + vesselState.up * Math.Max(Math.Abs(spd), 20 * part.vessel.mainBody.GeeASL);
                            if (hsdir.magnitude > Math.Max(Math.Abs(spd), 100 * part.vessel.mainBody.GeeASL) * 2) {
                                fullBlast = true;
                                rot = -hsdir;
                            } else {
                                rot = dir.normalized;
                            }
                        } else {
                            rot = vesselState.up;
                        }
                        rotateTo(rot, RotationReference.INERTIAL);
                        break;
                }

                double t_err = (trans_spd_act - spd) / (vesselState.thrustAvailable / vesselState.mass);
                t_integral += t_err * TimeWarp.fixedDeltaTime;
                double t_deriv = (t_err - t_prev_err) / TimeWarp.fixedDeltaTime;
                double t_act = (t_Kp * t_err) + (t_Ki * t_integral) + (t_Kd * t_deriv);
                t_prev_err = t_err;

                double int_error = Mathf.Abs(Quaternion.Angle(rotationGetReferenceRotation(rotationReference) * rotationTarget, part.vessel.transform.rotation * Quaternion.Euler(-90, 0, 0)));
                if ((tmode != TMode.KEEP_VERTICAL) || (int_error < 2) || ((Math.Min(vesselState.altitudeASL, vesselState.altitudeTrue) < 1000) && (int_error < 45))) {
                    if (fullBlast) {
                        trans_prev_thrust = s.mainThrottle = 1;
                    } else {
                        trans_prev_thrust = s.mainThrottle = Mathf.Clamp(trans_prev_thrust + (float)t_act, 0, 1.0F);
                    }
                } else {
                    if ((int_error >= 2) && (vesselState.torqueThrustPYAvailable > vesselState.torquePYAvailable * 10)) {
                        trans_prev_thrust = s.mainThrottle = 0.1F;
                    } else {
                        trans_prev_thrust = s.mainThrottle = 0;
                    }
                }
            }

            if (rotationActive) {
                int userCommanding = (Mathfx.Approx(s.pitch, 0, 0.1F) ? 0 : 1) + (Mathfx.Approx(s.roll, 0, 0.1F) ? 0 : 2) + (Mathfx.Approx(s.yaw, 0, 0.1F) ? 0 : 4);

                s.killRot = false;

                Quaternion target = rotationGetReferenceRotation(rotationReference) * rotationTarget;
                Quaternion delta = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(part.vessel.transform.rotation) * target);
                if (Mathf.Abs(Quaternion.Angle(delta, Quaternion.identity)) > 30) {
                    delta = Quaternion.Slerp(Quaternion.identity, delta, 0.1F);
                }
                Vector3d deltaEuler = delta.eulerAngles;

                Vector3d err = new Vector3d((deltaEuler.x > 180) ? (deltaEuler.x - 360.0F) : deltaEuler.x, (deltaEuler.y > 180) ? (deltaEuler.y - 360.0F) : deltaEuler.y, (deltaEuler.z > 180) ? (deltaEuler.z - 360.0F) : deltaEuler.z) * Math.PI / 180.0F;
                err.Scale(new Vector3d(vesselState.MoI.x / (vesselState.torquePYAvailable + vesselState.torqueThrustPYAvailable * s.mainThrottle), vesselState.MoI.z / (vesselState.torquePYAvailable + vesselState.torqueThrustPYAvailable * s.mainThrottle), vesselState.MoI.y / vesselState.torqueRAvailable));
                integral += err * TimeWarp.fixedDeltaTime;
                Vector3d deriv = (err - prev_err) / TimeWarp.fixedDeltaTime;
                act = Kp * err + Ki * integral + Kd * deriv;
                prev_err = err;

                if (userCommanding != 0) {
                    prev_err = Vector3d.zero;
                    integral = Vector3d.zero;
                } else {
                    if (!double.IsNaN(act.z)) s.roll = Mathf.Clamp(s.roll + act.z, -1.0F, 1.0F);
                    if (!double.IsNaN(act.x)) s.pitch = Mathf.Clamp(s.pitch + act.x, -1.0F, 1.0F);
                    if (!double.IsNaN(act.y)) s.yaw = Mathf.Clamp(s.yaw - act.y, -1.0F, 1.0F);
                    stress = Mathf.Min(Mathf.Abs(act.x), 1.0F) + Mathf.Min(Mathf.Abs(act.y), 1.0F) + Mathf.Min(Mathf.Abs(act.z), 1.0F);
                }
            }
        }

        private void main_WindowGUI(int windowID) {
            GUILayout.BeginVertical();

            foreach (ComputerModule module in modules) {
                bool on = module.enabled;
                if (on != GUILayout.Toggle(on, module.getName())) {
                    module.enabled = !on;
                }
            }

            GUILayout.EndVertical();
        }

        private void drawGUI() {
            if ((part.State != PartStates.DEAD) && (part.vessel == FlightGlobals.ActiveVessel) && (allJebs[part.vessel].controller == this) && !gamePaused) {
                GUI.skin = HighLogic.Skin;

                switch (main_windowStat) {
                    case WindowStat.OPENING:
                        main_windowProgr += Time.fixedDeltaTime;
                        if (main_windowProgr >= 1) {
                            main_windowProgr = 1;
                            main_windowStat = WindowStat.NORMAL;
                        }
                        break;
                    case WindowStat.CLOSING:
                        main_windowProgr -= Time.fixedDeltaTime;
                        if (main_windowProgr <= 0) {
                            main_windowProgr = 0;
                            main_windowStat = WindowStat.HIDDEN;
                        }
                        break;
                }

                GUI.depth = -100;
                GUI.SetNextControlName("MechJebOpen");
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(new Vector3(0, 0, -90)), Vector3.one);
                if (GUI.Button(new Rect((-Screen.height - 100) / 2, Screen.width - 25 - (200 * main_windowProgr), 100, 25), (main_windowStat == WindowStat.HIDDEN) ? "/\\ MechJeb /\\" : "\\/ MechJeb \\/")) {
                    if (main_windowStat == WindowStat.HIDDEN) {
                        main_windowStat = WindowStat.OPENING;
                        main_windowProgr = 0;
                        firstDraw = true;
                    } else if (main_windowStat == WindowStat.NORMAL) {
                        main_windowStat = WindowStat.CLOSING;
                        main_windowProgr = 1;
                    }
                }
                GUI.matrix = Matrix4x4.identity;

                GUI.depth = -99;

                if (main_windowStat != WindowStat.HIDDEN) {
                    GUILayout.Window(windowIDbase, new Rect(Screen.width - main_windowProgr * 200, (Screen.height - 200) / 2, 200, 200), main_WindowGUI, "MechJeb", GUILayout.Width(200), GUILayout.Height(200));
                }

                GUI.depth = -98;

                int wid = 1;
                foreach (ComputerModule module in modules) {
                    if (module.enabled) {
                        module.drawGUI(windowIDbase + wid);
                    }
                    wid += 7;
                }

                if (firstDraw) {
                    GUI.FocusControl("MechJebOpen");
                    firstDraw = false;
                }
            }
        }

        public void onPartStart() {
            vesselState = new VesselState();

            modules.Add(new MechJebModuleSmartASS(this));
            modules.Add(new MechJebModuleTranslatron(this));
            modules.Add(new MechJebModuleOrbitInfo(this));
            modules.Add(new MechJebModuleSurfaceInfo(this));
            modules.Add(new ReentryComputer(this));
            modules.Add(new AscentAutopilot(this));

            //modules.Add(new MechJebModuleAscension(this));
            //modules.Add(new MechJebModuleOrbitOper(this));

            foreach (ComputerModule module in modules) {
                module.onPartStart();
            }
        }

        public void onFlightStart() {
            loadSettings();

            RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));
            firstDraw = true;

            FlightInputHandler.OnFlyByWire += new FlightInputHandler.FlightInputCallback(onFlyByWire);
            flyByWire = true;

            foreach (ComputerModule module in modules) {
                module.onFlightStart();
            }
        }

        public void onPartFixedUpdate() {
            if (settingsChanged) {
                saveSettings();
            }

            if ((part == null)) {
                print("F part == null");
                return;
            }
            if ((part.vessel == null)) {
                print("F part.vessel == null");
                return;
            }

            if (((part.vessel.rootPart is Decoupler) || (part.vessel.rootPart is RadialDecoupler)) && part.vessel.rootPart.gameObject.layer != 2) {
                print("Disabling collision with decoupler...");
                EditorLogic.setPartLayer(part.vessel.rootPart.gameObject, 2);
            }

            if (part.vessel.orbit.objectType != Orbit.ObjectType.VESSEL) {
                part.vessel.orbit.objectType = Orbit.ObjectType.VESSEL;
            }

            if (!part.isConnected) {
                List<Part> tmp = new List<Part>(part.vessel.parts);
                foreach (Part p in tmp) {
                    if ((p is Decoupler) || (p is RadialDecoupler)) {
                        print("Disabling collision with decoupler...");
                        EditorLogic.setPartLayer(p.gameObject, 2);
                    }
                }

                tmp = new List<Part>(part.vessel.parts);
                foreach (Part p in tmp) {
                    p.isConnected = true;
                    if (p.State == PartStates.IDLE) {
                        p.force_activate();
                    }
                }
            }

            if (!allJebs.ContainsKey(part.vessel)) {
                allJebs[part.vessel] = new VesselStateKeeper();
                allJebs[part.vessel].controller = this;
            }

            if (!allJebs[part.vessel].jebs.Contains(this)) {
                allJebs[part.vessel].jebs.Add(this);
            }

            if (allJebs[part.vessel].controller == this) {
                allJebs[part.vessel].state.Update(part.vessel);
                vesselState = allJebs[part.vessel].state;

                foreach (ComputerModule module in modules) {
                    module.vesselState = vesselState;
                    module.onPartFixedUpdate();
                }

                if (part.vessel != FlightGlobals.ActiveVessel) {
                    FlightCtrlState s = new FlightCtrlState();
                    drive(s);
                    part.vessel.FeedInputFeed(s);
                }
            }

        }

        public void onPartUpdate() {
            if ((part == null)) {
                print("part == null");
                return;
            }
            if ((part.vessel == null)) {
                print("part.vessel == null");
                return;
            }

            if (!allJebs.ContainsKey(part.vessel)) {
                allJebs[part.vessel] = new VesselStateKeeper();
                allJebs[part.vessel].controller = this;
            }
            if (!allJebs[part.vessel].jebs.Contains(this)) {
                allJebs[part.vessel].jebs.Add(this);
            }

            foreach (ComputerModule module in modules) {
                module.onPartUpdate();
            }

            if (tmode_changed) {
                if (prev_tmode == TMode.KEEP_VERTICAL) {
                    rotationActive = false;
                    trans_land = false;
                }
                t_integral = 0;
                t_prev_err = 0;
                tmode_changed = false;
                FlightInputHandler.SetNeutralControls();
            }

            if (rotationChanged) {
                integral = Vector3.zero;
                prev_err = Vector3.zero;
                act = Vector3.zero;
                print("MechJeb resetting PID controller.");
                rotationChanged = false;
            }

            if (calibrationMode) {
                bool printk = false;

                if (Input.GetKeyDown(KeyCode.KeypadDivide)) {
                    calibrationTarget++;
                    if (calibrationTarget > 2) {
                        calibrationTarget = 0;
                    }
                    switch (calibrationTarget) {
                        case 0:
                            print("Calibrating K");
                            break;
                        case 1:
                            print("Calibrating r_K");
                            break;
                        case 2:
                            print("Calibrating t_K");
                            break;
                    }
                }

                if (Input.GetKeyDown(KeyCode.KeypadMinus)) {
                    calibrationDelta -= 1;
                    if (calibrationDelta < 0) {
                        calibrationDelta = calibrationDeltas.Length - 1;
                    }
                    print("Calibration delta = " + calibrationDeltas[calibrationDelta]);
                }

                if (Input.GetKeyDown(KeyCode.KeypadPlus)) {
                    calibrationDelta += 1;
                    if (calibrationDelta >= calibrationDeltas.Length) {
                        calibrationDelta = 0;
                    }
                    print("Calibration delta = " + calibrationDeltas[calibrationDelta]);
                }

                if (Input.GetKeyDown(KeyCode.Keypad7)) {
                    switch (calibrationTarget) {
                        case 0:
                            Kp += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Kp += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Kp += (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad4)) {
                    switch (calibrationTarget) {
                        case 0:
                            Kp -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Kp -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Kp -= (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad8)) {
                    switch (calibrationTarget) {
                        case 0:
                            Ki += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Ki += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Ki += (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad5)) {
                    switch (calibrationTarget) {
                        case 0:
                            Ki -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Ki -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Ki -= (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad9)) {
                    switch (calibrationTarget) {
                        case 0:
                            Kd += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Kd += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Kd += (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad6)) {
                    switch (calibrationTarget) {
                        case 0:
                            Kd -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Kd -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Kd -= (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad1)) {
                    Damping += (float)calibrationDeltas[calibrationDelta];
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad0)) {
                    Damping -= (float)calibrationDeltas[calibrationDelta];
                    printk = true;
                }
                if (printk) {
                    switch (calibrationTarget) {
                        case 0:
                            print("Kp = " + Kp + " - Ki = " + Ki + " - Kd = " + Kd + " - Damp = " + Damping);
                            break;
                        case 1:
                            print("r_Kp = " + r_Kp + " - r_Ki = " + r_Ki + " - r_Kd = " + r_Kd);
                            break;
                        case 2:
                            print("t_Kp = " + t_Kp + " - t_Ki = " + t_Ki + " - t_Kd = " + t_Kd);
                            break;
                    }
                }
            }

        }

        public void onDisconnect() {
            foreach (ComputerModule module in modules) {
                module.onDisconnect();
            }

            if ((part.vessel != null) && allJebs.ContainsKey(part.vessel) && allJebs[part.vessel].jebs.Contains(this)) {
                if (allJebs[part.vessel].controller == this) {
                    allJebs[part.vessel].controller = null;
                }
                allJebs[part.vessel].jebs.Remove(this);
            }
        }

        public void onPartDestroy() {
            foreach (ComputerModule module in modules) {
                module.onPartDestroy();
            }

            if ((part != null) && (part.vessel != null) && (allJebs != null) && allJebs.ContainsKey(part.vessel) && (allJebs[part.vessel].jebs != null) && allJebs[part.vessel].jebs.Contains(this)) {
                if (allJebs[part.vessel].controller == this) {
                    allJebs[part.vessel].controller = null;
                }
                allJebs[part.vessel].jebs.Remove(this);
            }

            if (flyByWire) {
                FlightInputHandler.OnFlyByWire -= new FlightInputHandler.FlightInputCallback(onFlyByWire);
                flyByWire = false;
            }
            RenderingManager.RemoveFromPostDrawQueue(0, new Callback(drawGUI));
        }

        public void onGamePause() {
            gamePaused = true;
            foreach (ComputerModule module in modules) {
                module.onGamePause();
            }
        }

        public void onGameResume() {
            gamePaused = false;
            foreach (ComputerModule module in modules) {
                module.onGameResume();
            }
        }

        public void onActiveFixedUpdate() {
            foreach (ComputerModule module in modules) {
                module.onActiveFixedUpdate();
            }
        }

        public void onActiveUpdate() {
            foreach (ComputerModule module in modules) {
                module.onActiveUpdate();
            }
        }

        public void onBackup() {
            foreach (ComputerModule module in modules) {
                module.onBackup();
            }
        }

        public void onDecouple(float breakForce) {
            foreach (ComputerModule module in modules) {
                module.onDecouple(breakForce);
            }
        }

        public void onFlightStartAtLaunchPad() {
            foreach (ComputerModule module in modules) {
                module.onFlightStartAtLaunchPad();
            }
        }

        public void onPack() {
            foreach (ComputerModule module in modules) {
                module.onPack();
            }
        }

        public bool onPartActivate() {
            bool ok = false;
            foreach (ComputerModule module in modules) {
                ok = module.onPartActivate() || ok;
            }
            return ok;
        }

        public void onPartAwake() {
            foreach (ComputerModule module in modules) {
                module.onPartAwake();
            }
        }

        public void onPartDeactivate() {
            foreach (ComputerModule module in modules) {
                module.onPartDeactivate();
            }
        }

        public void onPartDelete() {
            foreach (ComputerModule module in modules) {
                module.onPartDelete();
            }
        }

        public void onPartExplode() {
            foreach (ComputerModule module in modules) {
                module.onPartExplode();
            }
        }

        public void onPartLiftOff() {
            foreach (ComputerModule module in modules) {
                module.onPartLiftOff();
            }
        }

        public void onPartLoad() {
            foreach (ComputerModule module in modules) {
                module.onPartLoad();
            }
        }

        public void onPartSplashdown() {
            foreach (ComputerModule module in modules) {
                module.onPartSplashdown();
            }
        }

        public void onPartTouchdown() {
            foreach (ComputerModule module in modules) {
                module.onPartTouchdown();
            }
        }

        public void onUnpack() {
            foreach (ComputerModule module in modules) {
                module.onUnpack();
            }
        }

        void print(String s) {
            MonoBehaviour.print(s);
        }
    }
}
