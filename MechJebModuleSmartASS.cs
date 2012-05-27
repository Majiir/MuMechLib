using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleSmartASS : ComputerModule
    {
        protected static string[] SASS_texts = { "KILL\nROT", "SURF", "PRO\nGRAD", "RETR\nGRAD", "NML\n+", "NML\n-", "RAD\n+", "RAD\n-" };
        protected static string[] SASS_tgtvessel_texts = { "KILL\nROT", "SURF", "PRO\nGRAD", "RETR\nGRAD", "NML\n+", "NML\n-","RAD\n+",
		 "RAD\n-","RVEL\n+","RVEL\n-","TGT\n+","TGT\n-","PAR\n+","PAR\n-"};
        public enum Mode
        {
            OFF,
            KILLROT,
            SURFACE,
            PROGRADE,
            RETROGRADE,
            NORMAL_PLUS,
            NORMAL_MINUS,
            RADIAL_PLUS,
            RADIAL_MINUS,
            RELATIVE_PLUS,
            RELATIVE_MINUS,
            TARGET_PLUS,
            TARGET_MINUS,
            PARALLEL_PLUS,
            PARALLEL_MINUS,
            AUTO
        }

        private bool showHelpWindow = false;
        private Vector2 helpScrollPosition;
        private bool mode_changed = false;
        private Mode prev_mode = Mode.OFF;
        private Mode _mode = Mode.OFF;
        public Mode mode
        {
            get
            {
                return _mode;
            }
            set
            {
                if (_mode != value)
                {
                    prev_mode = _mode;
                    _mode = value;
                    mode_changed = true;
                }
            }
        }
        protected Rect _helpWindowPos;

        Rect helpWindowPos
        {
            get { return _helpWindowPos; }
            set
            {
                if (_helpWindowPos.x != value.x || _helpWindowPos.y != value.y)
                    core.settingsChanged = true;
                _helpWindowPos = value;
            }
        }

        public string srf_hdg = "90";
        public string srf_pit = "90";
        public float srf_act_hdg = 90.0F;
        public float srf_act_pit = 90.0F;
        public bool srf_act = false;

        public MechJebModuleSmartASS(MechJebCore core) : base(core) { }

        public override string getName()
        {
            return "Smart A.S.S.";
        }

        public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
        {
            partDataCollection.Add("sass_mode", new KSPParseable((int)mode, KSPParseable.Type.INT));
            partDataCollection.Add("srf_hdg", new KSPParseable(srf_hdg, KSPParseable.Type.STRING));
            partDataCollection.Add("srf_pit", new KSPParseable(srf_pit, KSPParseable.Type.STRING));
            partDataCollection.Add("srf_act", new KSPParseable(srf_act, KSPParseable.Type.BOOL));
            partDataCollection.Add("srf_act_hdg", new KSPParseable(srf_act_hdg, KSPParseable.Type.FLOAT));
            partDataCollection.Add("srf_act_pit", new KSPParseable(srf_act_pit, KSPParseable.Type.FLOAT));
            base.onFlightStateSave(partDataCollection);
        }

        public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
        {
            if (parsedData.ContainsKey("sass_mode")) mode = (Mode)parsedData["sass_mode"].value_int;
            if (parsedData.ContainsKey("srf_hdg")) srf_hdg = parsedData["srf_hdg"].value;
            if (parsedData.ContainsKey("srf_pit")) srf_pit = parsedData["srf_pit"].value;
            if (parsedData.ContainsKey("srf_act")) srf_act = parsedData["srf_act"].value_bool;
            if (parsedData.ContainsKey("srf_act_hdg")) srf_act_hdg = parsedData["srf_act_hdg"].value_float;
            if (parsedData.ContainsKey("srf_act_pit")) srf_act_pit = parsedData["srf_act_pit"].value_float;
            base.onFlightStateLoad(parsedData);
        }

        public override void drawGUI(int baseWindowID)
        {
            windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, getName(), GUILayout.Width(130));
            if (showHelpWindow)
            {
                helpWindowPos = GUILayout.Window(baseWindowID + 1, helpWindowPos, HelpWindowGUI, getName() + " - Help", GUILayout.Width(400), GUILayout.Height(500));
            }
        }

        protected override void WindowGUI(int windowID)
        {
            GUIStyle sty = new GUIStyle(GUI.skin.button);
            sty.normal.textColor = sty.focused.textColor = Color.white;
            sty.hover.textColor = sty.active.textColor = Color.yellow;
            sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            sty.padding = new RectOffset(8, 8, 8, 8);

            GUIStyle help_sty = new GUIStyle(sty);
            help_sty.padding = new RectOffset(2, 2, 2, 2);

            GUILayout.BeginVertical();

            if ((core.controlModule != null) || core.trans_land || ((core.tmode == MechJebCore.TMode.KEEP_VERTICAL) && core.trans_kill_h))
            {
                mode = Mode.AUTO;
                sty.normal.textColor = Color.red;
                sty.onActive = sty.onFocused = sty.onHover = sty.onNormal = sty.active = sty.focused = sty.hover = sty.normal;
                GUILayout.Button("AUTO", sty, GUILayout.ExpandWidth(true));
            }
            else
            {
                if (mode == Mode.AUTO)
                {
                    mode = Mode.OFF;
                }

                sty.padding = new RectOffset(8, 8, 2, 2);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("OFF", sty, GUILayout.ExpandWidth(true)))
                {
                    mode = Mode.OFF;
                    windowPos = new Rect(windowPos.x, windowPos.y, 10, 10);
                }
                showHelpWindow = GUILayout.Toggle(showHelpWindow, "?", help_sty, GUILayout.Width(35));
                GUILayout.EndHorizontal();

                mode = (Mode)GUILayout.SelectionGrid((int)mode - 1, (core.targetType == MechJebCore.TargetType.NONE) ? SASS_texts : SASS_tgtvessel_texts, 2, sty) + 1;

                if (mode == Mode.SURFACE)
                {
                    GUIStyle msty = new GUIStyle(GUI.skin.button);
                    msty.padding = new RectOffset();

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("HDG", GUILayout.Width(30));
                    srf_hdg = GUILayout.TextField(srf_hdg, GUILayout.ExpandWidth(true));
                    srf_hdg = Regex.Replace(srf_hdg, @"[^\d+-.]", "");
                    if (GUILayout.Button("+", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F)))
                    {
                        float tmp = Convert.ToSingle(srf_hdg);
                        tmp += 1;
                        if (tmp >= 360.0F)
                        {
                            tmp -= 360.0F;
                        }
                        srf_hdg = Mathf.RoundToInt(tmp).ToString();
                        GUIUtility.keyboardControl = 0;
                    }
                    if (GUILayout.Button("-", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F)))
                    {
                        float tmp = Convert.ToSingle(srf_hdg);
                        tmp -= 1;
                        if (tmp < 0)
                        {
                            tmp += 360.0F;
                        }
                        srf_hdg = Mathf.RoundToInt(tmp).ToString();
                        GUIUtility.keyboardControl = 0;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("PIT", GUILayout.Width(30));
                    srf_pit = GUILayout.TextField(srf_pit, GUILayout.ExpandWidth(true));
                    srf_pit = Regex.Replace(srf_pit, @"[^\d+-.]", "");
                    if (GUILayout.Button("+", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F)))
                    {
                        float tmp = Convert.ToSingle(srf_pit);
                        tmp += 1;
                        if (tmp >= 360.0F)
                        {
                            tmp -= 360.0F;
                        }
                        srf_pit = Mathf.RoundToInt(tmp).ToString();
                        GUIUtility.keyboardControl = 0;
                    }
                    if (GUILayout.Button("-", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F)))
                    {
                        float tmp = Convert.ToSingle(srf_pit);
                        tmp -= 1;
                        if (tmp < 0)
                        {
                            tmp += 360.0F;
                        }
                        srf_pit = Mathf.RoundToInt(tmp).ToString();
                        GUIUtility.keyboardControl = 0;
                    }
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("EXECUTE", sty, GUILayout.ExpandWidth(true)))
                    {
                        srf_act_hdg = Convert.ToSingle(srf_hdg);
                        srf_act_pit = Convert.ToSingle(srf_pit);
                        srf_act = true;
                        mode_changed = true;
                        MonoBehaviour.print("MechJeb activating SURF mode. HDG = " + srf_act_hdg + " - PIT = " + srf_act_pit);
                        GUIUtility.keyboardControl = 0;
                    }

                    if (srf_act)
                    {
                        GUILayout.Label("Active HDG: " + srf_act_hdg.ToString("F1"), GUILayout.ExpandWidth(true));
                        GUILayout.Label("Active PIT: " + srf_act_pit.ToString("F1"), GUILayout.ExpandWidth(true));

                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }

        private void HelpWindowGUI(int windowID)
        {
            helpScrollPosition = GUILayout.BeginScrollView(helpScrollPosition);
            GUILayout.Label(
                "Smart A.S.S. will automatically hold a chosen heading\n\n" +
                "----------Standard Modes----------\n\n" +
                "KILL ROT : Stops Vessel Rotation\n" +
                "SURF : Holds orientation at a specific heading and pitch\n" +
                "PRO GRAD : Holds orientation at prograde (the direction the vessel is moving)\n" +
                "RETR GRAD : Holds orientation at retrorograde (opposite of prograde)\n" +
                "NML + : Holds orientation at orbit normal\n" +
                "NML - : Holds orientation at orbit anti-normal\n" +
                "RAD + : Holds orientation facing up from the orbited body\n" +
                "RAD - : Holds orientation facing down toward the orbited body\n\n" +
                "----------Target Modes----------\n\n" +
                "Target Modes will only be displayed when a target vessel is selected\n" +
                "RVEl + : Points in the direction to zero your velocity relative to the target vessel\n" +
                "RVEL - : Points in the direction to zero your velocity relative to the target vessel\n" +
                "TGT + : Points directly at the target vessel\n" +
                "TGT - : Points directly away from the target vessel\n" +
                "PAR + : Points parallel to the target vessel\n" +
                "PAR - : Points parallel to the target vesssel (opposite direction)\n"
            );
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }
        public override void onAttitudeChange(MechJebCore.AttitudeReference oldReference, Quaternion oldTarget, MechJebCore.AttitudeReference newReference, Quaternion newTarget)
        {
            if (!core.attitudeActive && ((mode != Mode.SURFACE) || srf_act))
            {
                mode = Mode.OFF;
            }

            base.onAttitudeChange(oldReference, oldTarget, newReference, newTarget);
        }

        public override void onPartUpdate()
        {
            if (mode_changed || core.targetChanged)
            {
                windowPos = new Rect(windowPos.x, windowPos.y, 10, 10);

                if (mode != Mode.SURFACE)
                {
                    srf_act = false;
                }

                if (((mode != Mode.OFF) && (mode != Mode.SURFACE)) || ((mode == Mode.SURFACE) && (srf_act)))
                {
                    switch (mode)
                    {
                        case Mode.KILLROT:
                            core.attitudeKILLROT = true;
                            core.attitudeTo(Quaternion.LookRotation(part.vessel.transform.up, -part.vessel.transform.forward), MechJebCore.AttitudeReference.INERTIAL, this);
                            break;
                        case Mode.SURFACE:
                            Quaternion r = Quaternion.AngleAxis(srf_act_hdg, Vector3.up) * Quaternion.AngleAxis(-srf_act_pit, Vector3.right);
                            core.attitudeTo(r * Vector3d.forward, MechJebCore.AttitudeReference.SURFACE_NORTH, this);
                            break;
                        case Mode.PROGRADE:
                            core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.ORBIT, this);
                            break;
                        case Mode.RETROGRADE:
                            core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.ORBIT, this);
                            break;
                        case Mode.NORMAL_PLUS:
                            core.attitudeTo(Vector3d.left, MechJebCore.AttitudeReference.ORBIT, this);
                            break;
                        case Mode.NORMAL_MINUS:
                            core.attitudeTo(Vector3d.right, MechJebCore.AttitudeReference.ORBIT, this);
                            break;
                        case Mode.RADIAL_PLUS:
                            core.attitudeTo(Vector3d.up, MechJebCore.AttitudeReference.ORBIT, this);
                            break;
                        case Mode.RADIAL_MINUS:
                            core.attitudeTo(Vector3d.down, MechJebCore.AttitudeReference.ORBIT, this);
                            break;
                        case Mode.RELATIVE_PLUS:
                            core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.RELATIVE_VELOCITY, this);
                            break;
                        case Mode.RELATIVE_MINUS:
                            core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.RELATIVE_VELOCITY, this);
                            break;
                        case Mode.TARGET_PLUS:
                            core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.TARGET, this);
                            break;
                        case Mode.TARGET_MINUS:
                            core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.TARGET, this);
                            break;
                        case Mode.PARALLEL_PLUS:
                            core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.TARGET_ORIENTATION, this);
                            break;
                        case Mode.PARALLEL_MINUS:
                            core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.TARGET_ORIENTATION, this);
                            break;

                    }
                }
                else
                {
                    core.attitudeDeactivate(this);
                }

                mode_changed = false;
            }

            base.onPartFixedUpdate();
        }
    }
}
