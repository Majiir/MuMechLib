using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleTablePhase : ComputerModule
    {
        public MechJebModuleTablePhase(MechJebCore core) : base(core) { }

        public override string getName()
        {
            return "Phase Angles - " + part.vessel.mainBody.name;
        }

        public override GUILayoutOption[] windowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(200) };
        }

        protected override void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            if ((part.vessel.mainBody != Planetarium.fetch.Sun) && (part.vessel.mainBody.referenceBody != null) && (part.vessel.mainBody.referenceBody.orbitingBodies.Count > 0))
            {
                foreach (CelestialBody sibling in part.vessel.mainBody.referenceBody.orbitingBodies)
                {
                    if (sibling != part.vessel.mainBody)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(sibling.name, GUILayout.ExpandWidth(true));
                        GUILayout.Label(ARUtils.clampDegrees((sibling.orbit.trueAnomaly + sibling.orbit.argumentOfPeriapsis + sibling.orbit.LAN) - (part.vessel.mainBody.orbit.trueAnomaly + part.vessel.mainBody.orbit.argumentOfPeriapsis + part.vessel.mainBody.orbit.LAN)).ToString("F6") + "°");
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.Label("Click for a calculator\nhttp://ksp.olex.biz/");
            if ((Event.current.type == EventType.repaint) && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && Input.GetMouseButtonDown(0))
            {
                Application.OpenURL("http://ksp.olex.biz/");
            }

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }
    }
}
