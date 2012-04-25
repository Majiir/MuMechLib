using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleTranslatron : ComputerModule
    {
        protected static string[] trans_texts = { "OFF", "KEEP\nOBT", "KEEP\nSURF", "KEEP\nVERT" };

        public enum AbortStage
        {
            OFF,
            THRUSTOFF,
            DECOUPLE,
            BURNUP,
            LAND
        }

        protected AbortStage abort = AbortStage.OFF;
        protected double burnUpTime = 0;

        protected bool autoMode = false;

        public string trans_spd = "0";

        public MechJebModuleTranslatron(MechJebCore core) : base(core) { }

        public override string getName()
        {
            return "Translatron";
        }

        public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
        {
            partDataCollection.Add("trans_spd", new KSPParseable(trans_spd, KSPParseable.Type.STRING));
            base.onFlightStateSave(partDataCollection);
        }

        public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
        {
            if (parsedData.ContainsKey("trans_spd")) trans_spd = parsedData["trans_spd"].value;
            base.onFlightStateLoad(parsedData);
        }

        public override GUILayoutOption[] windowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(130) };
        }

        protected override void WindowGUI(int windowID)
        {

            GUIStyle sty = new GUIStyle(GUI.skin.button);
            sty.normal.textColor = sty.focused.textColor = Color.white;
            sty.hover.textColor = sty.active.textColor = Color.yellow;
            sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            sty.padding = new RectOffset(8, 8, 8, 8);

            GUILayout.BeginVertical();

            if ((core.controlModule != null) || core.trans_land)
            {
                if (!autoMode)
                {
                    windowPos = new Rect(windowPos.x, windowPos.y, 10, 10);
                    autoMode = true;
                }

                sty.normal.textColor = Color.red;
                sty.onActive = sty.onFocused = sty.onHover = sty.onNormal = sty.active = sty.focused = sty.hover = sty.normal;
                GUILayout.Button("AUTO", sty, GUILayout.ExpandWidth(true));
            }
            else
            {
                if (autoMode)
                {
                    windowPos = new Rect(windowPos.x, windowPos.y, 10, 10);
                    autoMode = false;
                }

                MechJebCore.TMode oldMode = core.tmode;
                core.tmode = (MechJebCore.TMode)GUILayout.SelectionGrid((int)core.tmode, trans_texts, 2, sty);
                if (core.tmode != oldMode)
                {
                    core.trans_spd_act = Convert.ToInt16(trans_spd);
                    windowPos = new Rect(windowPos.x, windowPos.y, 10, 10);
                }

                core.trans_kill_h = GUILayout.Toggle(core.trans_kill_h, "Kill H/S", GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Speed");
                trans_spd = GUILayout.TextField(trans_spd, GUILayout.ExpandWidth(true));
                trans_spd = Regex.Replace(trans_spd, @"[^\d.+-]", "");
                GUILayout.EndHorizontal();

                if (GUILayout.Button("EXECUTE", sty, GUILayout.ExpandWidth(true)))
                {
                    core.trans_spd_act = Convert.ToInt16(trans_spd);
                    GUIUtility.keyboardControl = 0;
                }
            }

            if (core.tmode != MechJebCore.TMode.OFF)
            {
                GUILayout.Label("Active speed: " + MuMech.MuUtils.ToSI(core.trans_spd_act) + "m/s", GUILayout.ExpandWidth(true));
            }

            GUILayout.FlexibleSpace();

            GUIStyle tsty = new GUIStyle(GUI.skin.label);
            tsty.alignment = TextAnchor.UpperCenter;
            GUILayout.Label("Automation", tsty, GUILayout.ExpandWidth(true));

            sty.normal.textColor = sty.focused.textColor = sty.hover.textColor = sty.active.textColor = sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = (abort != AbortStage.OFF) ? Color.red : Color.green;

            if (GUILayout.Button((abort != AbortStage.OFF) ? "DON'T PANIC!" : "PANIC!!!", sty, GUILayout.ExpandWidth(true)))
            {
                abort = (abort == AbortStage.OFF) ? AbortStage.THRUSTOFF : AbortStage.OFF;
                if (abort == AbortStage.OFF)
                {
                    core.controlRelease(this);
                }
                else
                {
                    core.controlClaim(this, true);
                }
            }

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }

        public void recursiveDecouple()
        {
            int minStage = Staging.LastStage;
            foreach (Part child in part.vessel.parts)
            {
                if ((child is LiquidEngine) || (child is SolidRocket))
                {
                    if (child.inverseStage < minStage)
                    {
                        minStage = child.inverseStage;
                    }
                }
            }
            List<Part> decouplers = new List<Part>();
            foreach (Part child in part.vessel.parts)
            {
                if ((child.inverseStage > minStage) && ((child is Decoupler) || (child is RadialDecoupler)))
                {
                    decouplers.Add(child);
                }
            }
            foreach (Part decoupler in decouplers)
            {
                decoupler.force_activate();
            }
            if (part.vessel == FlightGlobals.ActiveVessel)
            {
                Staging.ActivateStage(minStage);
            }
        }

        public override void onControlLost()
        {
            if (abort != AbortStage.OFF)
            {
                abort = AbortStage.OFF;
            }

            base.onControlLost();
        }

        public override void drive(FlightCtrlState s)
        {
            if (abort != AbortStage.OFF)
            {
                switch (abort)
                {
                    case AbortStage.THRUSTOFF:
                        FlightInputHandler.SetNeutralControls();
                        s.mainThrottle = 0;
                        abort = AbortStage.DECOUPLE;
                        break;
                    case AbortStage.DECOUPLE:
                        recursiveDecouple();
                        abort = AbortStage.BURNUP;
                        burnUpTime = Planetarium.GetUniversalTime();
                        break;
                    case AbortStage.BURNUP:
                        if ((Planetarium.GetUniversalTime() - burnUpTime < 2) || (vesselState.speedVertical < 10))
                        {
                            core.tmode = MechJebCore.TMode.DIRECT;
                            core.attitudeTo(Vector3d.up, MechJebCore.AttitudeReference.SURFACE_NORTH, this);
                            double int_error = Math.Abs(Vector3d.Angle(vesselState.up, vesselState.forward));
                            core.trans_spd_act = (int_error < 90) ? 100 : 0;
                        }
                        else
                        {
                            abort = AbortStage.LAND;
                        }
                        break;
                    case AbortStage.LAND:
                        core.controlRelease(this);
                        core.landActivate(this);
                        abort = AbortStage.OFF;
                        break;
                }
            }
            base.drive(s);
        }
    }
}
