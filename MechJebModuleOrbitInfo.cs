using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleOrbitInfo : ComputerModule
    {
    protected int windowWidth = 200;
        public MechJebModuleOrbitInfo(MechJebCore core) : base(core) { }

        public override string getName()
        {
            return "Orbital Information";
        }
		
        public override GUILayoutOption[] windowOptions()
        {
        if (core.targetType != MechJebCore.TargetType.NONE)
        {
        windowWidth=300;
        }
        else 
        {
        windowWidth =200;
        }
            return new GUILayoutOption[] { GUILayout.Width(windowWidth) };
        }

        protected override void WindowGUI(int windowID)
        {
 
          GUILayout.BeginHorizontal();
        	  GUILayout.BeginVertical();
          
         		GUILayout.Label ("Name", GUILayout.ExpandWidth (true));
         		GUILayout.Label ("Orbiting", GUILayout.ExpandWidth (true));
         		GUILayout.Label ("Orbital Speed", GUILayout.ExpandWidth (true));
         		GUILayout.Label ("Apoapsis", GUILayout.ExpandWidth (true));
         		GUILayout.Label ("Periapsis", GUILayout.ExpandWidth (true));
         		GUILayout.Label ("Period", GUILayout.ExpandWidth (true));
       			GUILayout.Label ("Time to Apoapsis", GUILayout.ExpandWidth (true));
        		GUILayout.Label ("Time to Periapsis", GUILayout.ExpandWidth (true));
      			GUILayout.Label ("LAN", GUILayout.ExpandWidth (true));
       			GUILayout.Label ("LPe", GUILayout.ExpandWidth (true));
       		 	GUILayout.Label ("Inclination", GUILayout.ExpandWidth (true));
        		GUILayout.Label ("Eccentricity", GUILayout.ExpandWidth (true));
       			GUILayout.Label ("Semimajor Axis", GUILayout.ExpandWidth (true));
   			GUILayout.EndVertical();
   			GUILayout.BeginVertical ();
   				if (part.vessel.vesselName.Length>10)
					GUILayout.Label (part.vessel.vesselName.Remove (10), GUILayout.ExpandWidth (true));
				else
					GUILayout.Label (part.vessel.vesselName, GUILayout.ExpandWidth (true));
				GUILayout.Label (part.vessel.orbit.referenceBody.name);
				GUILayout.Label (MuUtils.ToSI (vesselState.speedOrbital) + "m/s");
				GUILayout.Label (MuUtils.ToSI (vesselState.orbitApA) + "m");
				GUILayout.Label (MuUtils.ToSI (vesselState.orbitPeA) + "m");
				GUILayout.Label (MuUtils.ToSI (vesselState.orbitPeriod) + "s");
				GUILayout.Label (MuUtils.ToSI (vesselState.orbitTimeToAp) + "s");
				GUILayout.Label (MuUtils.ToSI (vesselState.orbitTimeToPe) + "s");
				GUILayout.Label (vesselState.orbitLAN.ToString ("F6") + "°");
				GUILayout.Label (((vesselState.orbitLAN + vesselState.orbitArgumentOfPeriapsis) % 360.0).ToString ("F6") + "°");
				GUILayout.Label (vesselState.orbitInclination.ToString ("F6") + "°");
				GUILayout.Label (vesselState.orbitEccentricity.ToString ("F6"));
				GUILayout.Label (MuUtils.ToSI (vesselState.orbitSemiMajorAxis) + "m");
			GUILayout.EndVertical ();
			
			if (core.targetType != MechJebCore.TargetType.NONE)
			{
			GUILayout.BeginVertical ();
				if (core.targetName().Length>10)
						GUILayout.Label (core.targetName ().Remove (10), GUILayout.ExpandWidth (true));
					else
						GUILayout.Label (core.targetName (), GUILayout.ExpandWidth (true));
				
					GUILayout.Label (core.targetOrbit().referenceBody.name);
					GUILayout.Label (MuUtils.ToSI (core.targetOrbit ().GetVel().magnitude) + "m/s");
					GUILayout.Label (MuUtils.ToSI (core.targetOrbit ().ApA) + "m");
					GUILayout.Label (MuUtils.ToSI (core.targetOrbit ().PeA) + "m");
					GUILayout.Label (MuUtils.ToSI (core.targetOrbit ().period) + "s");
					GUILayout.Label (MuUtils.ToSI (core.targetOrbit ().timeToAp) + "s");
					GUILayout.Label (MuUtils.ToSI (core.targetOrbit ().timeToPe) + "s");
					GUILayout.Label (core.targetOrbit ().LAN.ToString ("F6") + "°");
					GUILayout.Label (((core.targetOrbit ().LAN + core.targetVessel.orbit.argumentOfPeriapsis) % 360.0).ToString ("F6") + "°");
					GUILayout.Label (core.targetOrbit ().inclination.ToString ("F6") + "°");
					GUILayout.Label (core.targetOrbit ().eccentricity.ToString ("F6"));
					GUILayout.Label (MuUtils.ToSI (core.targetOrbit ().semiMajorAxis) + "m");
				GUILayout.EndVertical ();
				
			}
           
		GUILayout.EndHorizontal();
         
        base.WindowGUI(windowID);
        }
    }
}
