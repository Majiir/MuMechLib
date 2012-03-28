using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MuMech {
    class MechJebModuleSmartASS : ComputerModule {
        protected static string[] SASS_texts = { "KILL\nROT", "SURF", "PRO\nGRAD", "RETR\nGRAD", "NML\n+", "NML\n-", "RAD\n+", "RAD\n-" };

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

        private bool mode_changed = false;
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

        public string srf_hdg = "90";
        public string srf_pit = "90";
        public float srf_act_hdg = 90.0F;
        public float srf_act_pit = 90.0F;
        public bool srf_act = false;

        public MechJebModuleSmartASS(MechJebCore core) : base(core) { }

        public override string getName() {
            return "Smart A.S.S.";
        }

        public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
            partDataCollection.Add("sass_mode", new KSPParseable((int)mode, KSPParseable.Type.INT));
            partDataCollection.Add("srf_hdg", new KSPParseable(srf_hdg, KSPParseable.Type.STRING));
            partDataCollection.Add("srf_pit", new KSPParseable(srf_pit, KSPParseable.Type.STRING));
            partDataCollection.Add("srf_act", new KSPParseable(srf_act, KSPParseable.Type.BOOL));
            partDataCollection.Add("srf_act_hdg", new KSPParseable(srf_act_hdg, KSPParseable.Type.FLOAT));
            partDataCollection.Add("srf_act_pit", new KSPParseable(srf_act_pit, KSPParseable.Type.FLOAT));
            base.onFlightStateSave(partDataCollection);
        }

        public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
            if (parsedData.ContainsKey("sass_mode")) mode = (Mode)parsedData["sass_mode"].value_int;
            if (parsedData.ContainsKey("srf_hdg")) srf_hdg = parsedData["srf_hdg"].value;
            if (parsedData.ContainsKey("srf_pit")) srf_pit = parsedData["srf_pit"].value;
            if (parsedData.ContainsKey("srf_act")) srf_act = parsedData["srf_act"].value_bool;
            if (parsedData.ContainsKey("srf_act_hdg")) srf_act_hdg = parsedData["srf_act_hdg"].value_float;
            if (parsedData.ContainsKey("srf_act_pit")) srf_act_pit = parsedData["srf_act_pit"].value_float;
            base.onFlightStateLoad(parsedData);
        }

        public override GUILayoutOption[] windowOptions() {
            return new GUILayoutOption[] { GUILayout.Width(130) };
        }

        protected override void WindowGUI(int windowID) {
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
                    windowPos = new Rect(windowPos.x, windowPos.y, 10, 10);
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
                        mode_changed = true;
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

            base.WindowGUI(windowID);
        }

        public override void onPartUpdate() {
            if (mode_changed) {
                windowPos = new Rect(windowPos.x, windowPos.y, 10, 10);

                if (mode != Mode.SURFACE) {
                    srf_act = false;
                }

                if (((mode != Mode.OFF) && (mode != Mode.STANDBY)) || ((mode == Mode.SURFACE) && (srf_act))) {
                    switch (mode) {
                        case Mode.KILLROT:
                            core.rotateTo(part.vessel.transform.up, MechJebCore.RotationReference.INERTIAL);
                            break;
                        case Mode.SURFACE:
                            Quaternion r = Quaternion.AngleAxis(srf_act_hdg, Vector3.up) * Quaternion.AngleAxis(-srf_act_pit, Vector3.right);
                            core.rotateTo(r, MechJebCore.RotationReference.SURFACE);
                            break;
                        case Mode.PROGRADE:
                            core.rotateTo(Vector3d.forward, MechJebCore.RotationReference.ORBIT);
                            break;
                        case Mode.RETROGRADE:
                            core.rotateTo(Vector3d.back, MechJebCore.RotationReference.ORBIT);
                            break;
                        case Mode.NORMAL_PLUS:
                            core.rotateTo(Vector3d.left, MechJebCore.RotationReference.ORBIT);
                            break;
                        case Mode.NORMAL_MINUS:
                            core.rotateTo(Vector3d.right, MechJebCore.RotationReference.ORBIT);
                            break;
                        case Mode.RADIAL_PLUS:
                            core.rotateTo(Vector3d.up, MechJebCore.RotationReference.ORBIT);
                            break;
                        case Mode.RADIAL_MINUS:
                            core.rotateTo(Vector3d.down, MechJebCore.RotationReference.ORBIT);
                            break;
                    }
                } else {
                    core.rotationActive = false;
                }

                mode_changed = false;
            }

            base.onPartFixedUpdate();
        }
    }
}
