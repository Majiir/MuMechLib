using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleVesselInfo : ComputerModule
    {
        FuelFlowAnalyzer ffa;

        public MechJebModuleVesselInfo(MechJebCore core)
            : base(core)
        {
            ffa = new FuelFlowAnalyzer(part.vessel);
        }


/*        public override void onPartUpdate()
        {
            if (!part.vessel.isActiveVessel) return;

            float[] timePerStage;
            float[] deltaVPerStage;
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                ffa.analyze(out timePerStage, out deltaVPerStage);
            }
        }*/

        public override string getName()
        {
            return "Vessel Information";
        }


        public override GUILayoutOption[] windowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(200) };
        }

        protected override void WindowGUI(int windowID)
        {
            GUIStyle txtR = new GUIStyle(GUI.skin.label);
            txtR.alignment = TextAnchor.UpperRight;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Total mass", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.mass.ToString("F1") + " tons", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Total thrust", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.thrustAvailable.ToString("F0") + " kN", txtR);
            GUILayout.EndHorizontal();

            double TWR = vesselState.thrustAvailable / (vesselState.mass * part.vessel.mainBody.gravParameter / Math.Pow(part.vessel.mainBody.Radius, 2));
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("TWR", GUILayout.ExpandWidth(true));
            GUILayout.Label(TWR.ToString("F2"), txtR);
            GUILayout.EndHorizontal();

            /*            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("MoI X", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.MoI.x.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("MoI Z", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.MoI.z.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("MoI Y", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.MoI.y.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("aV X", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.angularVelocity.x.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("aV Z", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.angularVelocity.z.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("aV Y", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.angularVelocity.y.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("aM X", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.angularMomentum.x.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("aM Z", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.angularMomentum.z.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("aM Y", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.angularMomentum.y.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("torqueThrustPYAvailable", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.torqueThrustPYAvailable.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("torquePYAvailable", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.torquePYAvailable.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                        GUILayout.Label("torqueRAvailable", GUILayout.ExpandWidth(true));
                        GUILayout.Label(vesselState.torqueRAvailable.ToString("F6"), txtR);
                        GUILayout.EndHorizontal();*/

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }
    }
}
