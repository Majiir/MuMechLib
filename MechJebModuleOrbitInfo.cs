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

        public bool showingRetrograde = false;

        public override string getName()
        {
            return "Orbital Information";
        }

        public override GUILayoutOption[] windowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width((core.targetType != MechJebCore.TargetType.NONE)?310:210) };
        }

        protected override void WindowGUI(int windowID)
        {

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label("Name", GUILayout.ExpandWidth(true));
            GUILayout.Label("Orbiting", GUILayout.ExpandWidth(true));
            GUILayout.Label("Orbital Speed", GUILayout.ExpandWidth(true));
            GUILayout.Label("Apoapsis", GUILayout.ExpandWidth(true));
            GUILayout.Label("Periapsis", GUILayout.ExpandWidth(true));
            GUILayout.Label("Period", GUILayout.ExpandWidth(true));
            GUILayout.Label("Time to Apoapsis", GUILayout.ExpandWidth(true));
            GUILayout.Label("Time to Periapsis", GUILayout.ExpandWidth(true));
            GUILayout.Label("LAN", GUILayout.ExpandWidth(true));
            GUILayout.Label("LPe", GUILayout.ExpandWidth(true));
            GUILayout.Label("Inclination", GUILayout.ExpandWidth(true));
            GUILayout.Label("Eccentricity", GUILayout.ExpandWidth(true));
            GUILayout.Label("Semimajor Axis", GUILayout.ExpandWidth(true));
            GUILayout.Label(showingRetrograde ? "Angle to Retrograde" : "Angle to Prograde", GUILayout.ExpandWidth(true));
            if ((Event.current.type == EventType.repaint) && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && Input.GetMouseButtonDown(0))
            {
                showingRetrograde = !showingRetrograde;
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (part.vessel.vesselName.Length > 10)
            {
                GUILayout.Label(part.vessel.vesselName.Remove(10), GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.Label(part.vessel.vesselName, GUILayout.ExpandWidth(true));
            }
            GUILayout.Label(part.vessel.orbit.referenceBody.name);
            GUILayout.Label(MuUtils.ToSI(vesselState.speedOrbital) + "m/s");
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitApA) + "m");
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitPeA) + "m");
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitPeriod) + "s");
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitTimeToAp) + "s");
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitTimeToPe) + "s");
            GUILayout.Label(vesselState.orbitLAN.ToString("F6") + "°");
            GUILayout.Label(((vesselState.orbitLAN + vesselState.orbitArgumentOfPeriapsis) % 360.0).ToString("F6") + "°");
            GUILayout.Label(vesselState.orbitInclination.ToString("F6") + "°");
            GUILayout.Label(vesselState.orbitEccentricity.ToString("F6"));
            GUILayout.Label(MuUtils.ToSI(vesselState.orbitSemiMajorAxis) + "m");
            GUILayout.Label(ARUtils.clampDegrees360(vesselState.angleToPrograde + (showingRetrograde?180:0)).ToString("F3") + "°");
            GUILayout.EndVertical();

            if (core.targetType != MechJebCore.TargetType.NONE)
            {
                GUILayout.BeginVertical();
                if (core.targetName().Length > 10)
                {
                    GUILayout.Label(core.targetName().Remove(10), GUILayout.ExpandWidth(true));
                }
                else
                {
                    GUILayout.Label(core.targetName(), GUILayout.ExpandWidth(true));
                }
                GUILayout.Label(core.targetOrbit().referenceBody.name);
                GUILayout.Label(MuUtils.ToSI(core.targetOrbit().GetVel().magnitude) + "m/s");
                GUILayout.Label(MuUtils.ToSI(core.targetOrbit().ApA) + "m");
                GUILayout.Label(MuUtils.ToSI(core.targetOrbit().PeA) + "m");
                GUILayout.Label(MuUtils.ToSI(core.targetOrbit().period) + "s");
                GUILayout.Label(MuUtils.ToSI(core.targetOrbit().timeToAp) + "s");
                GUILayout.Label(MuUtils.ToSI(core.targetOrbit().timeToPe) + "s");
                GUILayout.Label(core.targetOrbit().LAN.ToString("F6") + "°");
                GUILayout.Label(((core.targetOrbit().LAN + core.targetOrbit().argumentOfPeriapsis) % 360.0).ToString("F6") + "°");
                GUILayout.Label(core.targetOrbit().inclination.ToString("F6") + "°");
                GUILayout.Label(core.targetOrbit().eccentricity.ToString("F6"));
                GUILayout.Label(MuUtils.ToSI(core.targetOrbit().semiMajorAxis) + "m");
                GUILayout.Label("");
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            base.WindowGUI(windowID);
        }
    }
}
