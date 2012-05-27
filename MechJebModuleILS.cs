using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MuMech;
using UnityEngine;

namespace MuMech
{
    class MechJebModuleILS : ComputerModule
    {
        double runwayStartLat = -0.040633;
        double runwayStartLon = -74.6908;
        double runwayStartAlt = 67;
        double runwayEndLat = -0.041774;
        double runwayEndLon = -74.5341702;
        double runwayEndAlt = 67;

        public MechJebModuleILS(MechJebCore core) : base(core) { }

        NavBall navball;

        GameObject waypoint = new GameObject();
        Transform defaultPurpleWaypoint;

        private bool _minimized = false;
        public bool minimized
        {
            get { return _minimized; }
            set
            {
                if(value != _minimized) core.settingsChanged = true;
                _minimized = value;
            }
        }

        public override void onLoadGlobalSettings(SettingsManager settings)
        {
            minimized = settings["ILS_minimized"].valueBool(false);

            base.onLoadGlobalSettings(settings);
        }

        public override void onSaveGlobalSettings(SettingsManager settings)
        {
            settings["ILS_minimized"].value_bool = minimized;

            base.onSaveGlobalSettings(settings);
        }

        protected override void WindowGUI(int windowID)
        {
            //positions of the start and end of the runway
            Vector3d runwayStart = part.vessel.mainBody.GetWorldSurfacePosition(runwayStartLat, runwayStartLon, runwayStartAlt);
            Vector3d runwayEnd = part.vessel.mainBody.GetWorldSurfacePosition(runwayEndLat, runwayEndLon, runwayEndAlt);

            //swap the start and end if we are closer to the end
            if ((vesselState.CoM - runwayEnd).magnitude < (vesselState.CoM - runwayStart).magnitude)
            {
                Vector3d newStart = runwayEnd;
                runwayEnd = runwayStart;
                runwayStart = newStart;
            }

            //a coordinate system oriented to the runway
            Vector3d runwayUpUnit = part.vessel.mainBody.GetSurfaceNVector(runwayStartLat, runwayStartLon).normalized;
            Vector3d runwayHorizontalUnit = Vector3d.Exclude(runwayUpUnit, runwayStart - runwayEnd).normalized;
            Vector3d runwayLeftUnit = -Vector3d.Cross(runwayHorizontalUnit, runwayUpUnit).normalized;

            Vector3d vesselUnit = (vesselState.CoM - runwayStart).normalized;

            double leftSpeed = Vector3d.Dot(vesselState.velocityVesselSurface, runwayLeftUnit);
            double verticalSpeed = vesselState.speedVertical;
            double horizontalSpeed = vesselState.speedHorizontal;
            double flightPathAngle = 180 / Math.PI * Math.Atan2(verticalSpeed, horizontalSpeed);

            double leftDisplacement = Vector3d.Dot(runwayLeftUnit, vesselState.CoM - runwayStart);

            Vector3d vectorToRunway = runwayStart - vesselState.CoM;
            double verticalDistanceToRunway = Vector3d.Dot(vectorToRunway, vesselState.up);
            double horizontalDistanceToRunway = Math.Sqrt(vectorToRunway.sqrMagnitude - verticalDistanceToRunway * verticalDistanceToRunway);
            double flightPathAngleToRunway = 180 / Math.PI * Math.Atan2(verticalDistanceToRunway, horizontalDistanceToRunway);

            //set the purple waypoint to guide the plane into the runway, correcting for lateral offset
            waypoint.transform.position = runwayStart - 3 * leftDisplacement * runwayLeftUnit;


            //highjack the purple navball waypoint
            if (navball == null)
            {
                navball = (NavBall)GameObject.FindObjectOfType(typeof(NavBall));
            }

            if (navball != null)
            {
                if (navball.waypointTarget != waypoint.transform)
                {
                    defaultPurpleWaypoint = navball.waypointTarget; //save the usual waypoint
                    navball.waypointTarget = waypoint.transform;
                }
            }


            if (minimized)
            {
                if (GUILayout.Button("Maximize")) minimized = false;
            }
            else
            {
                GUILayout.BeginVertical();

                if (GUILayout.Button("Minimize")) minimized = true;

                
                GUILayout.Label(String.Format("Lateral speed = {0:0.00} m/s " + (leftSpeed < 0 ? "right" : "left"), Math.Abs(leftSpeed)));
                GUILayout.Label(String.Format("Lateral displacement = {0:0} m " + (leftDisplacement < 0 ? "right" : "left"), Math.Abs(leftDisplacement)));
                GUILayout.Label(String.Format("Flight Path Angle = {0:0.00} deg.", flightPathAngle));
                GUILayout.Label(String.Format("FPA to runway = {0:0.00} deg.", flightPathAngleToRunway));
                GUILayout.Label(String.Format("Distance to runway = {0:0.00} km", horizontalDistanceToRunway / 1000.0));
                GUILayout.Label("The purple circle on the navball points toward the runway. Align your velocity vector with the purple circle and you'll land on the runway, properly aligned with it.");

                GUILayout.EndVertical();

                GUI.DragWindow();
            }
        }

        public override void onModuleDisabled()
        {
            if (navball != null)
            {
                if (navball.waypointTarget == waypoint.transform)
                {
                    navball.waypointTarget = defaultPurpleWaypoint;
                }
            }
        }

        public override GUILayoutOption[] windowOptions()
        {
            if(minimized) return new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(40) };
            else return new GUILayoutOption[] { GUILayout.Width(250) };
        }

        public override String getName()
        {
            return "Instrument Landing System";
        }
    }
}
