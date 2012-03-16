using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using UnityEngine;
using MuMech;

namespace MuMech {
    class VesselStateKeeper {
        public List<MechJebCore> jebs = new List<MechJebCore>();
        public MechJebCore controller = null;
        public VesselState state = new VesselState();
    }

    class MechJebCore {
        public enum Mode {
            OFF,
            KILLROT,
            SURFACE,
            PROGRADE,
            RETROGRADE,
            NORMAL_PLUS,
            NORMAL_MINUS,
            RADIAL_PLUS,
            RADIAL_MINUS,
            STANDBY,
            INTERNAL
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

        private Mode prev_mode = Mode.OFF;
        private Mode _mode = Mode.OFF;
        public Mode mode {
            get {
                return _mode;
            }
            set {
                if (_mode != value) {
                    prev_mode = _mode;
                    _mode = value;
                    mode_changed = true;
                }
            }
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

        private static bool calibrationMode = false;
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
        private bool mode_changed = false;
        private bool tmode_changed = false;
        private Vector3 integral = Vector3.zero;
        private Vector3 prev_err = Vector3.zero;
        private Vector3 act = Vector3.zero;
        private Vector3 k_integral = Vector3.zero;
        private Vector3 k_prev_err = Vector3.zero;
        private double t_integral = 0;
        private double t_prev_err = 0;
        public float Kp = 20.0F;
        public float Ki = 0.0F;
        public float Kd = 40.0F;
        public float k_Kp = 13.0F;
        public float k_Ki = 0.0F;
        public float k_Kd = 0.0F;
        public float t_Kp = 0.05F;
        public float t_Ki = 0.000001F;
        public float t_Kd = 0.05F;
        public float Damping = 0.0F;

        public string srf_hdg = "90";
        public string srf_pit = "90";
        public float srf_act_hdg = 90.0F;
        public float srf_act_pit = 90.0F;
        public bool srf_act = false;

        public string trans_spd = "0";
        public float trans_spd_act = 0;
        public float trans_prev_thrust = 0;
        public bool trans_kill_h = false;
        public bool trans_land = false;
        public bool trans_land_gears = false;

        public Quaternion internal_target = Quaternion.identity;

        public VesselState vesselState;

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

        // SmartASS window
        protected static string[] SASS_texts = { "KILL\nROT", "SURF", "PRO\nGRAD", "RETR\nGRAD", "NML\n+", "NML\n-", "RAD\n+", "RAD\n-" };

        private Rect _SASS_windowPos;
        protected Rect SASS_windowPos {
            get { return _SASS_windowPos; }
            set {
                if (_SASS_windowPos.x != value.x || _SASS_windowPos.y != value.y) {
                    settingsChanged = true;
                }
                _SASS_windowPos = value;
            }
        }

        private WindowStat _SASS_windowStat;
        protected WindowStat SASS_windowStat {
            get { return _SASS_windowStat; }
            set {
                if (_SASS_windowStat != value) {
                    _SASS_windowStat = value;
                    settingsChanged = true;
                }
            }
        }

        // orbital window
        private Rect _orbital_windowPos;
        protected Rect orbital_windowPos {
            get { return _orbital_windowPos; }
            set {
                if (_orbital_windowPos.x != value.x || _orbital_windowPos.y != value.y) {
                    settingsChanged = true;
                }
                _orbital_windowPos = value;
            }
        }

        private WindowStat _orbital_windowStat;
        protected WindowStat orbital_windowStat {
            get { return _orbital_windowStat; }
            set {
                if (_orbital_windowStat != value) {
                    _orbital_windowStat = value;
                    settingsChanged = true;
                }
            }
        }

        // surface window
        private Rect _surface_windowPos;
        protected Rect surface_windowPos {
            get { return _surface_windowPos; }
            set {
                if (_surface_windowPos.x != value.x || _surface_windowPos.y != value.y) {
                    settingsChanged = true;
                }
                _surface_windowPos = value;
            }
        }

        private WindowStat _surface_windowStat;
        protected WindowStat surface_windowStat {
            get { return _surface_windowStat; }
            set {
                if (_surface_windowStat != value) {
                    _surface_windowStat = value;
                    settingsChanged = true;
                }
            }
        }

        // Translatron window
        protected static string[] trans_texts = { "OFF", "KEEP\nOBT", "KEEP\nSURF", "KEEP\nVERT" };

        private Rect _trans_windowPos;
        protected Rect trans_windowPos {
            get { return _trans_windowPos; }
            set {
                if (_trans_windowPos.x != value.x || _trans_windowPos.y != value.y) {
                    settingsChanged = true;
                }
                _trans_windowPos = value;
            }
        }

        private WindowStat _trans_windowStat;
        protected WindowStat trans_windowStat {
            get { return _trans_windowStat; }
            set {
                if (_trans_windowStat != value) {
                    _trans_windowStat = value;
                    settingsChanged = true;
                }
            }
        }

        public Part part;

        public void saveSettings() {
            if (!settingsLoaded) {
                return;
            }
            settings["main_windowStat"].value_integer = (int)main_windowStat;
            settings["main_windowProgr"].value_decimal = main_windowProgr;
            settings["SASS_windowPos"].value_vector = new Vector4(SASS_windowPos.x, SASS_windowPos.y);
            settings["SASS_windowStat"].value_integer = (int)SASS_windowStat;
            settings["orbital_windowPos"].value_vector = new Vector4(orbital_windowPos.x, orbital_windowPos.y);
            settings["orbital_windowStat"].value_integer = (int)orbital_windowStat;
            settings["surface_windowPos"].value_vector = new Vector4(surface_windowPos.x, surface_windowPos.y);
            settings["surface_windowStat"].value_integer = (int)surface_windowStat;
            settings["trans_windowPos"].value_vector = new Vector4(trans_windowPos.x, trans_windowPos.y);
            settings["trans_windowStat"].value_integer = (int)trans_windowStat;
            settings.save();
            settingsChanged = false;
        }

        public void loadSettings() {
            if (settings == null) {
                settings = new SettingsManager(KSPUtil.ApplicationRootPath + "MuMech/MechJeb.cfg");
            }

            main_windowStat = (WindowStat)settings["main_windowStat"].value_integer;
            main_windowProgr = (float)settings["main_windowProgr"].value_decimal;
            SASS_windowPos = new Rect(settings["SASS_windowPos"].value_vector.x, settings["SASS_windowPos"].value_vector.y, 10, 10);
            SASS_windowStat = (WindowStat)settings["SASS_windowStat"].value_integer;
            orbital_windowPos = new Rect(settings["orbital_windowPos"].value_vector.x, settings["orbital_windowPos"].value_vector.y, 10, 10);
            orbital_windowStat = (WindowStat)settings["orbital_windowStat"].value_integer;
            surface_windowPos = new Rect(settings["surface_windowPos"].value_vector.x, settings["surface_windowPos"].value_vector.y, 10, 10);
            surface_windowStat = (WindowStat)settings["surface_windowStat"].value_integer;
            trans_windowPos = new Rect(settings["trans_windowPos"].value_vector.x, settings["trans_windowPos"].value_vector.y, 10, 10);
            trans_windowStat = (WindowStat)settings["trans_windowStat"].value_integer;
            settingsChanged = false;
            settingsLoaded = true;
        }

        public void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
            saveSettings();

            partDataCollection.Add("mode", new KSPParseable((int)mode, KSPParseable.Type.INT));
            partDataCollection.Add("tmode", new KSPParseable((int)tmode, KSPParseable.Type.INT));
            partDataCollection.Add("srf_act", new KSPParseable(srf_act, KSPParseable.Type.BOOL));
            partDataCollection.Add("srf_act_hdg", new KSPParseable(srf_act_hdg, KSPParseable.Type.FLOAT));
            partDataCollection.Add("srf_act_pit", new KSPParseable(srf_act_pit, KSPParseable.Type.FLOAT));
            partDataCollection.Add("srf_hdg", new KSPParseable(srf_hdg, KSPParseable.Type.STRING));
            partDataCollection.Add("srf_pit", new KSPParseable(srf_pit, KSPParseable.Type.STRING));
            partDataCollection.Add("trans_spd", new KSPParseable(trans_spd, KSPParseable.Type.STRING));
            partDataCollection.Add("trans_spd_act", new KSPParseable(trans_spd_act, KSPParseable.Type.FLOAT));
            partDataCollection.Add("trans_kill_h", new KSPParseable(trans_kill_h, KSPParseable.Type.BOOL));
            partDataCollection.Add("trans_land", new KSPParseable(trans_land, KSPParseable.Type.BOOL));
        }

        public void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
            loadSettings();

            if (parsedData.ContainsKey("mode")) mode = (Mode)parsedData["mode"].value_int;
            if (parsedData.ContainsKey("tmode")) tmode = (TMode)parsedData["tmode"].value_int;
            if (parsedData.ContainsKey("srf_act")) srf_act = parsedData["srf_act"].value_bool;
            if (parsedData.ContainsKey("srf_act_hdg")) srf_act_hdg = parsedData["srf_act_hdg"].value_float;
            if (parsedData.ContainsKey("srf_act_pit")) srf_act_pit = parsedData["srf_act_pit"].value_float;
            if (parsedData.ContainsKey("srf_hdg")) srf_hdg = parsedData["srf_hdg"].value;
            if (parsedData.ContainsKey("srf_pit")) srf_pit = parsedData["srf_pit"].value;
            if (parsedData.ContainsKey("trans_spd")) trans_spd = parsedData["trans_spd"].value;
            if (parsedData.ContainsKey("trans_spd_act")) trans_spd_act = parsedData["trans_spd_act"].value_float;
            if (parsedData.ContainsKey("trans_kill_h")) trans_kill_h = parsedData["trans_kill_h"].value_bool;
            if (parsedData.ContainsKey("trans_land")) trans_land = parsedData["trans_land"].value_bool;
        }

        private void onFlyByWire(FlightCtrlState s) {
            if ((part.vessel != FlightGlobals.ActiveVessel) || (allJebs[part.vessel].controller != this)) {
                return;
            }
            drive(s);
        }

        private void drive(FlightCtrlState s) {
            stress = 0;

            if (part.State == PartStates.DEAD) {
                return;
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
                        trans_spd_act = (float)-Math.Sqrt((vesselState.thrustAvailable / vesselState.mass - vesselState.gravityForce.magnitude) * 2 * minalt) * 0.90F;
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
                            trans_spd_act = -Mathf.Lerp(0, (float)Math.Sqrt((vesselState.thrustAvailable / vesselState.mass - vesselState.gravityForce.magnitude) * 2 * 200) * 0.90F, (float)minalt / 200);
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
                        mode = Mode.INTERNAL;
                        if (trans_kill_h) {
                            Vector3 hsdir = Vector3.Exclude(vesselState.up, part.vessel.orbit.GetVel() - part.vessel.mainBody.getRFrmVel(vesselState.CoM));
                            Vector3 dir = -hsdir + vesselState.up * Math.Max(Math.Abs(spd), 100 * part.vessel.mainBody.GeeASL);
                            if (hsdir.magnitude > Math.Max(Math.Abs(spd), 100 * part.vessel.mainBody.GeeASL) * 2) {
                                fullBlast = true;
                                internal_target = Quaternion.LookRotation(-hsdir, -part.vessel.transform.forward);
                            } else {
                                internal_target = Quaternion.LookRotation(dir.normalized, part.vessel.mainBody.transform.up);
                            }
                        } else {
                            internal_target = Quaternion.LookRotation(vesselState.up, part.vessel.mainBody.transform.up);
                        }
                        break;
                }

                double t_err = (trans_spd_act - spd) / (vesselState.thrustAvailable / vesselState.mass);
                t_integral += t_err * TimeWarp.fixedDeltaTime;
                double t_deriv = (t_err - t_prev_err) / TimeWarp.fixedDeltaTime;
                double t_act = (t_Kp * t_err) + (t_Ki * t_integral) + (t_Kd * t_deriv);
                t_prev_err = t_err;

                if ((tmode != TMode.KEEP_VERTICAL) || (Mathf.Abs(Quaternion.Angle(internal_target, part.vessel.transform.rotation) - 90) < 2) || (Math.Min(vesselState.altitudeASL, vesselState.altitudeTrue) < 1000)) {
                    if (fullBlast) {
                        trans_prev_thrust = s.mainThrottle = 1;
                    } else {
                        trans_prev_thrust = s.mainThrottle = Mathf.Clamp(trans_prev_thrust + (float)t_act, 0, 1.0F);
                    }
                }
            }

            if ((mode != Mode.OFF) && (mode != Mode.STANDBY)) {
                Vector3 k_err = part.vessel.transform.InverseTransformDirection(part.vessel.rigidbody.angularVelocity);
                k_integral += k_err * TimeWarp.fixedDeltaTime;
                Vector3 k_deriv = (k_err - k_prev_err) / TimeWarp.fixedDeltaTime;
                Vector3 k_act = (k_Kp * k_err) + (k_Ki * k_integral) + (k_Kd * k_deriv);
                k_prev_err = k_err;

                if (mode != Mode.KILLROT) {
                    k_act *= Damping;
                }

                int userCommanding = (Mathfx.Approx(s.pitch, 0, 0.1F) ? 0 : 1) + (Mathfx.Approx(s.roll, 0, 0.1F) ? 0 : 2) + (Mathfx.Approx(s.yaw, 0, 0.1F) ? 0 : 4);

                if ((userCommanding & 1) == 0) s.pitch = Mathf.Clamp(s.pitch + k_act.x, -1.0F, 1.0F);
                if ((userCommanding & 2) == 0) s.roll = Mathf.Clamp(s.roll + k_act.y, -1.0F, 1.0F);
                if ((userCommanding & 4) == 0) s.yaw = Mathf.Clamp(s.yaw + k_act.z, -1.0F, 1.0F);

                if ((mode != Mode.KILLROT) && ((mode != Mode.SURFACE) || srf_act)) {
                    s.killRot = false;

                    Vector3d prograde = vesselState.velocityVesselOrbit.normalized;
                    Quaternion obt = Quaternion.LookRotation(prograde, vesselState.up);

                    Vector3 tgt_fwd = part.vessel.transform.forward;
                    Vector3 tgt_up = vesselState.up;

                    switch (mode) {
                        case Mode.SURFACE:
                            Quaternion r = Quaternion.LookRotation(vesselState.up, part.vessel.mainBody.transform.up) * Quaternion.AngleAxis(srf_act_hdg + 180.0F, Vector3.forward) * Quaternion.AngleAxis(90.0F - srf_act_pit, Vector3.right);

                            tgt_fwd = r * Vector3.forward;
                            if (srf_act_pit == 90.0F) {
                                tgt_up = -part.vessel.transform.forward;
                            }
                            break;
                        case Mode.PROGRADE:
                            tgt_fwd = prograde;
                            break;
                        case Mode.RETROGRADE:
                            tgt_fwd = -prograde;
                            break;
                        case Mode.NORMAL_PLUS:
                            tgt_fwd = obt * Vector3.left;
                            break;
                        case Mode.NORMAL_MINUS:
                            tgt_fwd = obt * Vector3.right;
                            break;
                        case Mode.RADIAL_PLUS:
                            tgt_fwd = obt * Vector3.up;
                            tgt_up = prograde;
                            break;
                        case Mode.RADIAL_MINUS:
                            tgt_fwd = obt * Vector3.down;
                            tgt_up = prograde;
                            break;
                    }

                    Quaternion tgt = Quaternion.LookRotation(tgt_fwd, tgt_up);
                    if (mode == Mode.INTERNAL) {
                        tgt = internal_target;
                    } else {
                        internal_target = tgt;
                    }

                    Quaternion delta = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(part.vessel.transform.rotation) * tgt);

                    Vector3 err = new Vector3((delta.eulerAngles.x > 180) ? (delta.eulerAngles.x - 360.0F) : delta.eulerAngles.x, (delta.eulerAngles.y > 180) ? (delta.eulerAngles.y - 360.0F) : delta.eulerAngles.y, (delta.eulerAngles.z > 180) ? (delta.eulerAngles.z - 360.0F) : delta.eulerAngles.z) / 180.0F;
                    integral += err * TimeWarp.fixedDeltaTime;
                    Vector3 deriv = (err - prev_err) / TimeWarp.fixedDeltaTime;
                    act = Kp * err + Ki * integral + Kd * deriv;
                    prev_err = err;

                    if (userCommanding != 0) {
                        prev_err = Vector3.zero;
                        integral = Vector3.zero;
                    } else {
                        s.roll = Mathf.Clamp(s.roll + act.z, -1.0F, 1.0F);
                        if (Mathf.Abs(err.z) < 45.0 / 180.0) {
                            s.pitch = Mathf.Clamp(s.pitch + act.x, -1.0F, 1.0F);
                            s.yaw = Mathf.Clamp(s.yaw - act.y, -1.0F, 1.0F);
                        } else {
                            k_act = (k_Kp * k_err) + (k_Ki * k_integral) + (k_Kd * k_deriv);
                            s.pitch = Mathf.Clamp(s.pitch + k_act.x, -1.0F, 1.0F);
                            s.yaw = Mathf.Clamp(s.yaw + k_act.z, -1.0F, 1.0F);
                        }
                        stress =  Mathf.Min(Mathf.Abs(act.x), 1.0F) + Mathf.Min(Mathf.Abs(act.y), 1.0F) + Mathf.Min(Mathf.Abs(act.z), 1.0F);
                    }
                }
            }
        }

        private void main_WindowGUI(int windowID) {
            GUILayout.BeginVertical();

            bool SASS = SASS_windowStat != WindowStat.HIDDEN;
            if (SASS != GUILayout.Toggle(SASS, "Smart A. S. S.")) {
                SASS_windowStat = (SASS_windowStat == WindowStat.HIDDEN) ? WindowStat.NORMAL : WindowStat.HIDDEN;
            }

            bool trans = trans_windowStat != WindowStat.HIDDEN;
            if (trans != GUILayout.Toggle(trans, "Translatron")) {
                trans_windowStat = (trans_windowStat == WindowStat.HIDDEN) ? WindowStat.NORMAL : WindowStat.HIDDEN;
            }

            bool orbital = orbital_windowStat != WindowStat.HIDDEN;
            if (orbital != GUILayout.Toggle(orbital, "Orbital Information")) {
                orbital_windowStat = (orbital_windowStat == WindowStat.HIDDEN) ? WindowStat.NORMAL : WindowStat.HIDDEN;
            }

            bool surface = surface_windowStat != WindowStat.HIDDEN;
            if (surface != GUILayout.Toggle(surface, "Surface Information")) {
                surface_windowStat = (surface_windowStat == WindowStat.HIDDEN) ? WindowStat.NORMAL : WindowStat.HIDDEN;
            }

            GUILayout.EndVertical();
        }

        private void SASS_WindowGUI(int windowID) {
            GUIStyle sty = new GUIStyle(GUI.skin.button);
            sty.normal.textColor = sty.focused.textColor = Color.white;
            sty.hover.textColor = sty.active.textColor = Color.yellow;
            sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            sty.padding = new RectOffset(8, 8, 8, 8);

            GUILayout.BeginVertical();

            if (mode == Mode.OFF) {
                if (GUILayout.Button("ON", sty, GUILayout.ExpandWidth(true))) {
                    mode = Mode.STANDBY;
                }
            } else if (mode == Mode.INTERNAL) {
                sty.normal.textColor = Color.red;
                sty.onActive = sty.onFocused = sty.onHover = sty.onNormal = sty.active = sty.focused = sty.hover = sty.normal;
                GUILayout.Button("AUTO", sty, GUILayout.ExpandWidth(true));
            } else {
                if (GUILayout.Button("OFF", sty, GUILayout.ExpandWidth(true))) {
                    mode = Mode.OFF;
                    SASS_windowPos = new Rect(SASS_windowPos.x, SASS_windowPos.y, 10, 10);
                }

                mode = (Mode)GUILayout.SelectionGrid((int)mode - 1, SASS_texts, 2, sty) + 1;

                if (mode == Mode.SURFACE) {
                    GUIStyle msty = new GUIStyle(GUI.skin.button);
                    msty.padding = new RectOffset();

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("HDG", GUILayout.Width(30));
                    srf_hdg = GUILayout.TextField(srf_hdg, GUILayout.ExpandWidth(true));
                    srf_hdg = Regex.Replace(srf_hdg, @"[^\d+-]", "");
                    if (GUILayout.Button("+", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F))) {
                        float tmp = Convert.ToInt16(srf_hdg);
                        tmp += 1;
                        if (tmp >= 360.0F) {
                            tmp -= 360.0F;
                        }
                        srf_hdg = Mathf.RoundToInt(tmp).ToString();
                        GUIUtility.keyboardControl = 0;
                    }
                    if (GUILayout.Button("-", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F))) {
                        float tmp = Convert.ToInt16(srf_hdg);
                        tmp -= 1;
                        if (tmp < 0) {
                            tmp += 360.0F;
                        }
                        srf_hdg = Mathf.RoundToInt(tmp).ToString();
                        GUIUtility.keyboardControl = 0;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("PIT", GUILayout.Width(30));
                    srf_pit = GUILayout.TextField(srf_pit, GUILayout.ExpandWidth(true));
                    srf_pit = Regex.Replace(srf_pit, @"[^\d+-]", "");
                    if (GUILayout.Button("+", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F))) {
                        float tmp = Convert.ToInt16(srf_pit);
                        tmp += 1;
                        if (tmp >= 360.0F) {
                            tmp -= 360.0F;
                        }
                        srf_pit = Mathf.RoundToInt(tmp).ToString();
                        GUIUtility.keyboardControl = 0;
                    }
                    if (GUILayout.Button("-", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F))) {
                        float tmp = Convert.ToInt16(srf_pit);
                        tmp -= 1;
                        if (tmp < 0) {
                            tmp += 360.0F;
                        }
                        srf_pit = Mathf.RoundToInt(tmp).ToString();
                        GUIUtility.keyboardControl = 0;
                    }
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("EXECUTE", sty, GUILayout.ExpandWidth(true))) {
                        srf_act_hdg = Convert.ToInt16(srf_hdg);
                        srf_act_pit = Convert.ToInt16(srf_pit);
                        srf_act = true;
                        MonoBehaviour.print("MechJeb activating SURF mode. HDG = " + srf_act_hdg + " - PIT = " + srf_act_pit);
                        GUIUtility.keyboardControl = 0;
                    }

                    if (srf_act) {
                        GUILayout.Label("Active HDG: " + srf_act_hdg.ToString("F0"), GUILayout.ExpandWidth(true));
                        GUILayout.Label("Active PIT: " + srf_act_pit.ToString("F0"), GUILayout.ExpandWidth(true));
                    }
                }
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void orbital_WindowGUI(int windowID) {
            GUIStyle txtR = new GUIStyle(GUI.skin.label);
            txtR.alignment = TextAnchor.UpperRight;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Orbiting", GUILayout.ExpandWidth(true));
            GUILayout.Label(part.vessel.orbit.referenceBody.name, txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Orbital Speed", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.speedOrbital) + "m/s", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Apoapsis", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitApA) + "m", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Periapsis", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitPeA) + "m", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Period", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitPeriod) + "s", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Time to Apoapsis", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitTimeToAp) + "s", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Time to Periapsis", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitTimeToPe) + "s", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("LAN", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.orbitLAN.ToString("F6") + "°", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("LPe", GUILayout.ExpandWidth(true));
            GUILayout.Label(((vesselState.orbitLAN + vesselState.orbitArgumentOfPeriapsis) % 360.0).ToString("F6") + "°", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Inclination", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.orbitInclination.ToString("F6") + "°", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Eccentricity", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.orbitEccentricity.ToString("F6"), txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Semimajor Axis", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitSemiMajorAxis) + "m", txtR);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void surface_WindowGUI(int windowID) {
            GUIStyle txtR = new GUIStyle(GUI.skin.label);
            txtR.alignment = TextAnchor.UpperRight;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Altitude (ASL)", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.altitudeASL) + "m", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Altitude (true)", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.altitudeTrue) + "m", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Pitch", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.vesselPitch.ToString("F3") + "°", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Heading", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.vesselHeading.ToString("F3") + "°", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Roll", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.vesselRoll.ToString("F3") + "°", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Ground Speed", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.speedSurface) + "m/s", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Vertical Speed", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.speedVertical) + "m/s", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Horizontal Speed", GUILayout.ExpandWidth(true));
            GUILayout.Label(MuUtils.ToSI(vesselState.speedHorizontal) + "m/s", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Latitude", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.latitude.ToString("F6") + "°", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Longitude", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.longitude.ToString("F6") + "°", txtR);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void trans_WindowGUI(int windowID) {
            GUIStyle sty = new GUIStyle(GUI.skin.button);
            sty.normal.textColor = sty.focused.textColor = Color.white;
            sty.hover.textColor = sty.active.textColor = Color.yellow;
            sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            sty.padding = new RectOffset(8, 8, 8, 8);

            GUILayout.BeginVertical();

            tmode = (TMode)GUILayout.SelectionGrid((int)tmode, trans_texts, 2, sty);

            trans_kill_h = GUILayout.Toggle(trans_kill_h, "Kill H/S", GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Speed");
            trans_spd = GUILayout.TextField(trans_spd, GUILayout.ExpandWidth(true));
            trans_spd = Regex.Replace(trans_spd, @"[^\d+-]", "");
            GUILayout.EndHorizontal();

            if (GUILayout.Button("EXECUTE", sty, GUILayout.ExpandWidth(true))) {
                trans_spd_act = Convert.ToInt16(trans_spd);
                GUIUtility.keyboardControl = 0;
            }

            if (tmode != TMode.OFF) {
                GUILayout.Label("Active speed: " + MuMech.MuUtils.ToSI(trans_spd_act) + "m/s", GUILayout.ExpandWidth(true));
            }

            GUILayout.FlexibleSpace();

            GUIStyle tsty = new GUIStyle(GUI.skin.label);
            tsty.alignment = TextAnchor.UpperCenter;
            GUILayout.Label("Automation", tsty, GUILayout.ExpandWidth(true));

            if (trans_land) {
                sty.normal.textColor = sty.focused.textColor = sty.hover.textColor = sty.active.textColor = sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            }

            if (GUILayout.Button("LAND", sty, GUILayout.ExpandWidth(true))) {
                trans_land = !trans_land;
                if (trans_land) {
                    trans_land_gears = false;
                    tmode = TMode.KEEP_VERTICAL;
                }
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
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

                if (SASS_windowStat != WindowStat.HIDDEN) {
                    SASS_windowPos = GUILayout.Window(windowIDbase + 1, SASS_windowPos, SASS_WindowGUI, "Smart A. S. S.", GUILayout.Width(130));
                }

                if (orbital_windowStat != WindowStat.HIDDEN) {
                    orbital_windowPos = GUILayout.Window(windowIDbase + 2, orbital_windowPos, orbital_WindowGUI, "Orbital Information", GUILayout.Width(200));
                }

                if (surface_windowStat != WindowStat.HIDDEN) {
                    surface_windowPos = GUILayout.Window(windowIDbase + 3, surface_windowPos, surface_WindowGUI, "Surface Information", GUILayout.Width(200));
                }

                if (trans_windowStat != WindowStat.HIDDEN) {
                    trans_windowPos = GUILayout.Window(windowIDbase + 4, trans_windowPos, trans_WindowGUI, "Translatron", GUILayout.Width(130));
                }

                if (firstDraw) {
                    GUI.FocusControl("MechJebOpen");
                    firstDraw = false;
                }
            }
        }

        public void onPartStart() {
        }

        public void onFlightStart() {
            loadSettings();
            if ((SASS_windowPos.x == 0) && (SASS_windowPos.y == 0)) SASS_windowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            if ((orbital_windowPos.x == 0) && (orbital_windowPos.y == 0)) orbital_windowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            if ((surface_windowPos.x == 0) && (surface_windowPos.y == 0)) surface_windowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            if ((trans_windowPos.x == 0) && (trans_windowPos.y == 0)) trans_windowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);

            RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));
            firstDraw = true;

            FlightInputHandler.OnFlyByWire += new FlightInputHandler.FlightInputCallback(onFlyByWire);
            flyByWire = true;
        }

        public void onPartFixedUpdate() {
            if (settingsChanged) {
                saveSettings();
            }

            if (((part.vessel.rootPart is Decoupler) || (part.vessel.rootPart is RadialDecoupler)) && part.vessel.rootPart.gameObject.layer != 2) {
                MonoBehaviour.print("Disabling collision with decoupler...");
                EditorLogic.setPartLayer(part.vessel.rootPart.gameObject, 2);
            }

            if (part.vessel.orbit.objectType != Orbit.ObjectType.VESSEL) {
                part.vessel.orbit.objectType = Orbit.ObjectType.VESSEL;
            }

            if (!part.isConnected) {
                List<Part> tmp = new List<Part>(part.vessel.parts);
                foreach (Part p in tmp) {
                    if ((p is Decoupler) || (p is RadialDecoupler)) {
                        MonoBehaviour.print("Disabling collision with decoupler...");
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
            }

            if ((part.vessel != FlightGlobals.ActiveVessel) && (allJebs[part.vessel].controller == this)) {
                FlightCtrlState s = new FlightCtrlState();
                drive(s);
                part.vessel.FeedInputFeed(s);
            }
        }

        public void onPartUpdate() {
            if (!allJebs.ContainsKey(part.vessel)) {
                allJebs[part.vessel] = new VesselStateKeeper();
                allJebs[part.vessel].controller = this;
            }
            if (!allJebs[part.vessel].jebs.Contains(this)) {
                allJebs[part.vessel].jebs.Add(this);
            }

            if (tmode_changed) {
                if ((prev_tmode == TMode.KEEP_VERTICAL) && (mode == Mode.INTERNAL)) {
                    mode = Mode.OFF;
                    trans_land = false;
                }
                t_integral = 0;
                t_prev_err = 0;
                trans_spd_act = Convert.ToInt16(trans_spd);
                tmode_changed = false;
                trans_windowPos = new Rect(trans_windowPos.x, trans_windowPos.y, 10, 10);
                GUIUtility.keyboardControl = 0;
                FlightInputHandler.SetNeutralControls();
            }

            if ((integral.magnitude > 10) || (prev_err.magnitude > 10) || mode_changed) {
                integral = Vector3.zero;
                prev_err = Vector3.zero;
                act = Vector3.zero;
                k_integral = Vector3.zero;
                k_prev_err = Vector3.zero;
                MonoBehaviour.print("MechJeb resetting PID controller.");
            }
            if (mode_changed) {
                srf_act = false;
                SASS_windowPos = new Rect(SASS_windowPos.x, SASS_windowPos.y, 10, 10);

                mode_changed = false;
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
                            MonoBehaviour.print("Calibrating K");
                            break;
                        case 1:
                            MonoBehaviour.print("Calibrating k_K");
                            break;
                        case 2:
                            MonoBehaviour.print("Calibrating t_K");
                            break;
                    }
                }

                if (Input.GetKeyDown(KeyCode.KeypadMinus)) {
                    calibrationDelta -= 1;
                    if (calibrationDelta < 0) {
                        calibrationDelta = calibrationDeltas.Length - 1;
                    }
                    MonoBehaviour.print("Calibration delta = " + calibrationDeltas[calibrationDelta]);
                }

                if (Input.GetKeyDown(KeyCode.KeypadPlus)) {
                    calibrationDelta += 1;
                    if (calibrationDelta >= calibrationDeltas.Length) {
                        calibrationDelta = 0;
                    }
                    MonoBehaviour.print("Calibration delta = " + calibrationDeltas[calibrationDelta]);
                }

                if (Input.GetKeyDown(KeyCode.Keypad7)) {
                    switch (calibrationTarget) {
                        case 0:
                            Kp += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            k_Kp += (float)calibrationDeltas[calibrationDelta];
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
                            k_Kp -= (float)calibrationDeltas[calibrationDelta];
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
                            k_Ki += (float)calibrationDeltas[calibrationDelta];
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
                            k_Ki -= (float)calibrationDeltas[calibrationDelta];
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
                            k_Kd += (float)calibrationDeltas[calibrationDelta];
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
                            k_Kd -= (float)calibrationDeltas[calibrationDelta];
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
                            MonoBehaviour.print("Kp = " + Kp + " - Ki = " + Ki + " - Kd = " + Kd + " - Damp = " + Damping);
                            break;
                        case 1:
                            MonoBehaviour.print("k_Kp = " + k_Kp + " - k_Ki = " + k_Ki + " - k_Kd = " + k_Kd);
                            break;
                        case 2:
                            MonoBehaviour.print("t_Kp = " + t_Kp + " - t_Ki = " + t_Ki + " - t_Kd = " + t_Kd);
                            break;
                    }
                }
            }
        }

        public void onDisconnect() {
            if ((part.vessel != null) && allJebs.ContainsKey(part.vessel) && allJebs[part.vessel].jebs.Contains(this)) {
                if (allJebs[part.vessel].controller == this) {
                    allJebs[part.vessel].controller = null;
                }
                allJebs[part.vessel].jebs.Remove(this);
            }

            if (flyByWire) {
                FlightInputHandler.OnFlyByWire -= new FlightInputHandler.FlightInputCallback(onFlyByWire);
                flyByWire = false;
            }
        }

        public void onPartDestroy() {
            if ((part.vessel != null) && allJebs.ContainsKey(part.vessel) && allJebs[part.vessel].jebs.Contains(this)) {
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
        }

        public void onGameResume() {
            gamePaused = false;
        }
    }
}
