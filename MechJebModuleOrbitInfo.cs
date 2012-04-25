using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleOrbitInfo : ComputerModule
    {
        public MechJebModuleOrbitInfo(MechJebCore core) : base(core) { }

        public override string getName()
        {
            return "Orbital Information";
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

            base.WindowGUI(windowID);
        }
    }
}
