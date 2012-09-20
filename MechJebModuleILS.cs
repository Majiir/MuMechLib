using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MuMech;
using UnityEngine;
using SharpLua.LuaTypes;

namespace MuMech
{
    public class MechJebModuleILS : ComputerModule
    {
        public double targetHeading;
        public String targetHeadingString = "90";
        public double targetAltitude;
        public String targetAltitudeString = "5000";

        public bool holdHeadingAndAlt = false;
        public bool ilsOn = false;
        public bool autoLand = false;
        public bool loweredGear = false;

        public double runwayStartLat = -0.040633;
        public double runwayStartLon = -74.6908;
        public double runwayStartAlt = 67;
        public double runwayEndLat = -0.041774;
        public double runwayEndLon = -74.5341702;
        public double runwayEndAlt = 67;

        public double glideslope = 3;
        public String glideslopeString = "3";

        NavBall navball;

        GameObject waypoint = new GameObject();
        Transform defaultPurpleWaypoint;

        public double leftSpeed;
        public double leftDisplacement;
        public double horizontalDistanceToRunway;
        public double flightPathAngleToRunway;
        public double flightPathAngle;
        public Vector3d runwayStart;
        public Vector3d runwayEnd;
        public Vector3d runwayDir;

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

        public MechJebModuleILS(MechJebCore core) : base(core) { }

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

        public override void registerLuaMembers(LuaTable index)
        {
            index.Register("ilsHold", proxyHold);
            index.Register("ilsLand", proxyLand);
        }

        public LuaValue proxyHold(LuaValue[] args)
        {
            if (args.Count() != 2) throw new Exception("ilsHold usage: ilsHold(altitude [in meters], heading [in degrees])");

            double altitude;
            double heading;

            try
            {
                altitude = ((LuaNumber)args[0]).Number;
            }
            catch (Exception)
            {
                throw new Exception("ilsHold: invalid altitude");
            }

            try
            {
                heading = ((LuaNumber)args[1]).Number;
            }
            catch (Exception)
            {
                throw new Exception("ilsHold: invalid heading");
            }

            hold(altitude, heading);

            return LuaNil.Nil;
        }

        public LuaValue proxyLand(LuaValue[] args)
        {
            land();

            return LuaNil.Nil;
        }

        public void hold(double altitude, double heading)
        {
            this.enabled = true;
            targetHeading = heading;
            targetAltitude = altitude;
            targetHeadingString = targetHeading.ToString();
            targetAltitudeString = targetAltitude.ToString();
            autoLand = false;
            holdHeadingAndAlt = true;
            ilsOn = true;
            core.controlClaim(this);
        }

        public void land()
        {
            this.enabled = true;
            autoLand = true;
            holdHeadingAndAlt = false;
            ilsOn = true;
            loweredGear = false;
            core.controlClaim(this);
        }

        protected override void WindowGUI(int windowID)
        {
            if (minimized)
            {
                if (GUILayout.Button("Maximize")) minimized = false;
            }
            else
            {
                GUILayout.BeginVertical();

                if (GUILayout.Button("Minimize")) minimized = true;

                targetHeading = ARUtils.doGUITextInput("Heading:", 70.0F, targetHeadingString, 70.0F, "d", 40.0F, out targetHeadingString, targetHeading);
                targetAltitude = ARUtils.doGUITextInput("Altitude:", 70.0F, targetAltitudeString, 70.0F, "m", 40.0F, out targetAltitudeString, targetAltitude);

                bool oldHold = holdHeadingAndAlt;
                holdHeadingAndAlt = GUILayout.Toggle(holdHeadingAndAlt, "Hold Heading + Alt");
                if (holdHeadingAndAlt && !oldHold)
                {
                    autoLand = false;
                    core.controlClaim(this);
                }
                if (!holdHeadingAndAlt && oldHold)
                {
                    core.attitudeDeactivate(this);
                    core.controlRelease(this);
                }

                ilsOn = GUILayout.Toggle(ilsOn, "ILS", new GUIStyle(GUI.skin.button));

                if (ilsOn)
                {
                    GUILayout.Label(String.Format("Lateral speed = {0:0.00} m/s " + (leftSpeed < 0 ? "right" : "left"), Math.Abs(leftSpeed)));
                    GUILayout.Label(String.Format("Lateral displacement = {0:0} m " + (leftDisplacement < 0 ? "right" : "left"), Math.Abs(leftDisplacement)));
                    GUILayout.Label(String.Format("Flight Path Angle = {0:0.00}°", flightPathAngle));
                    GUILayout.Label(String.Format("FPA to runway = {0:0.00}°", flightPathAngleToRunway));
                    GUILayout.Label(String.Format("Distance to runway = {0:0.00} km", horizontalDistanceToRunway / 1000.0));
                    GUILayout.Label("The purple circle on the navball points toward the runway. Align your velocity vector with the purple circle and you'll land on the runway, properly aligned with it.");

                    bool oldAutoLand = autoLand;
                    autoLand = GUILayout.Toggle(autoLand, "Auto Land");
                    if (autoLand && !oldAutoLand)
                    {
                        holdHeadingAndAlt = false;
                        core.controlClaim(this);
                        loweredGear = false;
                    }
                    if (!autoLand && oldAutoLand)
                    {
                        core.attitudeDeactivate(this);
                        core.controlRelease(this);
                    }
                    if (autoLand) glideslope = ARUtils.doGUITextInput("Glideslope:", 70.0F, glideslopeString, 70.0F, "d", 40.0F, out glideslopeString, glideslope);
                }
                else
                {
                    autoLand = false;
                }
                
                GUILayout.EndVertical();

            }

            GUI.DragWindow();
        }

        public override void onPartFixedUpdate()
        {
            if (!this.enabled) return;

            //positions of the start and end of the runway
            runwayStart = part.vessel.mainBody.GetWorldSurfacePosition(runwayStartLat, runwayStartLon, runwayStartAlt);
            runwayEnd = part.vessel.mainBody.GetWorldSurfacePosition(runwayEndLat, runwayEndLon, runwayEndAlt);

            //swap the start and end if we are closer to the end
            if ((vesselState.CoM - runwayEnd).magnitude < (vesselState.CoM - runwayStart).magnitude)
            {
                Vector3d newStart = runwayEnd;
                runwayEnd = runwayStart;
                runwayStart = newStart;
            }
            runwayDir = (runwayEnd - runwayStart).normalized;

            //a coordinate system oriented to the runway
            Vector3d runwayUpUnit = part.vessel.mainBody.GetSurfaceNVector(runwayStartLat, runwayStartLon).normalized;
            Vector3d runwayHorizontalUnit = Vector3d.Exclude(runwayUpUnit, runwayStart - runwayEnd).normalized;
            Vector3d runwayLeftUnit = -Vector3d.Cross(runwayHorizontalUnit, runwayUpUnit).normalized;

            Vector3d vesselUnit = (vesselState.CoM - runwayStart).normalized;

            leftSpeed = Vector3d.Dot(vesselState.velocityVesselSurface, runwayLeftUnit);
            double verticalSpeed = vesselState.speedVertical;
            double horizontalSpeed = vesselState.speedHorizontal;
            flightPathAngle = 180 / Math.PI * Math.Atan2(verticalSpeed, horizontalSpeed);

            leftDisplacement = Vector3d.Dot(runwayLeftUnit, vesselState.CoM - runwayStart);

            Vector3d vectorToRunway = runwayStart - vesselState.CoM;
            double verticalDistanceToRunway = Vector3d.Dot(vectorToRunway, vesselState.up);
            horizontalDistanceToRunway = Math.Sqrt(vectorToRunway.sqrMagnitude - verticalDistanceToRunway * verticalDistanceToRunway);
            flightPathAngleToRunway = 180 / Math.PI * Math.Atan2(verticalDistanceToRunway, horizontalDistanceToRunway);

            //set the purple waypoint to guide the plane into the runway, correcting for lateral offset
            if ((vesselState.CoM - runwayStart).magnitude > 200.0)
            {
                waypoint.transform.position = runwayStart - 3 * leftDisplacement * runwayLeftUnit;
            }
            else
            {
                waypoint.transform.position = runwayEnd;
            }

            if (ilsOn) highjackPurpleWaypoint();
            else unhighjackPurpleWaypoint();
        }


        public override void drive(FlightCtrlState s)
        {
            if (autoLand)
            {
                if (!part.vessel.Landed)
                {
                    if (!loweredGear && (vesselState.CoM - runwayStart).magnitude < 1000.0)
                    {
                        part.vessel.rootPart.SendEvent("LowerLandingGear");
                        loweredGear = true;
                    }

                    Vector3d vectorToWaypoint = waypoint.transform.position - vesselState.CoM;
                    double headingToWaypoint = 180 / Math.PI * Math.Atan2(Vector3d.Dot(vectorToWaypoint, vesselState.east), Vector3d.Dot(vectorToWaypoint, vesselState.north));
                    
                    double desiredFPA = Mathf.Clamp((float)(flightPathAngleToRunway + 3 * (flightPathAngleToRunway + glideslope)), -20.0F, 0.0F);

                    aimVelocityVector(desiredFPA, headingToWaypoint);
                }
                else
                {
                    //keep the plane aligned with the runway:
                    Vector3d target = runwayEnd;
                    if (Vector3d.Dot(target - vesselState.CoM, vesselState.forward) < 0) target = runwayStart;
                    core.attitudeTo((target- vesselState.CoM).normalized, MechJebCore.AttitudeReference.INERTIAL, this);
                }
            }
            else if (holdHeadingAndAlt)
            {
                double targetClimbRate = (targetAltitude - vesselState.altitudeASL) / 30.0;
                double targetFlightPathAngle = 180 / Math.PI * Math.Asin(Mathf.Clamp((float)(targetClimbRate / vesselState.speedSurface), (float)Math.Sin(-Math.PI / 9), (float)Math.Sin(Math.PI / 9)));
                aimVelocityVector(targetFlightPathAngle, targetHeading);
            }
        }

        public double stableAoA = 0; //we average AoA over time to get an estimate of what pitch will produce what FPA
        public double pitchCorrection = 0; //we average (commanded pitch - actual pitch) over time in order to fix this offset in our commands
        public float maxYaw = 10.0F;
        public float maxRoll = 10.0F;
        public float maxPitchCorrection = 5.0F;
        public double AoAtimeConstant = 2.0;
        public double pitchCorrectionTimeConstant = 15.0;
        void aimVelocityVector(double desiredFpa, double desiredHeading)
        {
            //horizontal control
            double velocityHeading = 180 / Math.PI * Math.Atan2(Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.east),
                                                                Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.north));
            double headingTurn = Mathf.Clamp((float)ARUtils.clampDegrees(desiredHeading - velocityHeading), -maxYaw, maxYaw);
            double noseHeading = velocityHeading + headingTurn;
            double noseRoll = (maxRoll / maxYaw) * headingTurn;
            
            //vertical control
            double nosePitch = desiredFpa + stableAoA + pitchCorrection;
            
            core.attitudeTo(noseHeading, nosePitch, noseRoll, this);

            double flightPathAngle = 180 / Math.PI * Math.Atan2(vesselState.speedVertical, vesselState.speedHorizontal);
            double AoA = vesselState.vesselPitch - flightPathAngle;
            stableAoA = (AoAtimeConstant * stableAoA + vesselState.deltaT * AoA) / (AoAtimeConstant + vesselState.deltaT); //a sort of integral error

            pitchCorrection = (pitchCorrectionTimeConstant * pitchCorrection + vesselState.deltaT * (nosePitch - vesselState.vesselPitch)) / (pitchCorrectionTimeConstant + vesselState.deltaT);
            pitchCorrection = Mathf.Clamp((float)pitchCorrection, -maxPitchCorrection, maxPitchCorrection);
        }


        public override void onModuleDisabled()
        {
            unhighjackPurpleWaypoint();
        }

        void highjackPurpleWaypoint()
        {
            //highjack the purple navball waypoint
            if (navball == null) navball = (NavBall)GameObject.FindObjectOfType(typeof(NavBall));
            if (navball != null)
            {
                if (navball.target != waypoint.transform)
                {
                    defaultPurpleWaypoint = navball.target; //save the usual waypoint
                    navball.target = waypoint.transform;
                }
            }
        }

        void unhighjackPurpleWaypoint()
        {
            if (navball != null)
            {
                if (navball.target == waypoint.transform)
                {
                    navball.target = defaultPurpleWaypoint;
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
