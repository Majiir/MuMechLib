using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech {
    class MechJebModuleSurfaceInfo : ComputerModule {
        public MechJebModuleSurfaceInfo(MechJebCore core) : base(core) { }

        public override string getName() {
            return "Surface Information";
        }

        public override GUILayoutOption[] windowOptions() {
            return new GUILayoutOption[] { GUILayout.Width(200) };
        }

        protected override void WindowGUI(int windowID) {
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

            base.WindowGUI(windowID);
        }
    }
}
