using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using UnityEngine;


namespace MuMech
{

    class ReentryComputer : ComputerModule
    {
        public ReentryComputer(MechJebCore core) : base(core) {  }

        double _targetLatitude;
        double targetLatitude
        {
            get { return _targetLatitude; }
            set
            {
                if (_targetLatitude != value) core.settingsChanged = true;
                _targetLatitude = value;
            }
        }

        double _targetLongitude;
        double targetLongitude
        {
            get { return _targetLongitude; }
            set
            {
                if (_targetLongitude != value) core.settingsChanged = true;
                _targetLongitude = value;
            }
        }

        protected Rect _helpWindowPos;
        Rect helpWindowPos
        {
            get { return _helpWindowPos; }
            set
            {
                if (_helpWindowPos.x != value.x || _helpWindowPos.y != value.y) core.settingsChanged = true;
                _helpWindowPos = value;
            }
        }

        public override void onLoadGlobalSettings(SettingsManager settings)
        {
            base.onLoadGlobalSettings(settings);

            targetLatitude = settings["RC_targetLatitude"].valueDecimal(KSC_LATITUDE);
            targetLatitudeString = targetLatitude.ToString();
            targetLongitude = settings["RC_targetLongitude"].valueDecimal(KSC_LONGITUDE);
            targetLongitudeString = targetLongitude.ToString();

            Vector4 savedHelpWindowPos = settings["RC_helpWindowPos"].valueVector(new Vector4(150, 50));
            helpWindowPos = new Rect(savedHelpWindowPos.x, savedHelpWindowPos.y, 10, 10);

        }

        public override void onSaveGlobalSettings(SettingsManager settings)
        {
            base.onSaveGlobalSettings(settings);

            settings["RC_targetLatitude"].value_decimal = targetLatitude;
            settings["RC_targetLongitude"].value_decimal = targetLongitude;
            settings["RC_helpWindowPos"].value_vector = new Vector4(helpWindowPos.x, helpWindowPos.y);
            
        }

        const int ON_COURSE = 0, COURSE_CORRECTIONS = 1, PREPARING_TO_DECELERATE = 2, DECELERATING = 3, HOVER = 4, FINAL_DESCENT = 5;
        String[] landerStateStrings = { "On course for target", "Correcting trajectory", "Preparing to decelerate", "Decelerating", "Hovering", "Final descent" };
        int landerState;

        //simulation stuff:
        const double simulationTimeStep = 0.2; //seconds
        SimulationResult simulationResult = new SimulationResult();        
        long nextSimulationDelayMs = 0;
        Stopwatch nextSimulationTimer = new Stopwatch();


        //GUI stuff:
        bool showHelpWindow = false;
        const int WINDOW_ID = 7792;
        const int HELP_WINDOW_ID = 7794;

        const double KSC_LATITUDE = -0.103;
        const double KSC_LONGITUDE = -74.575;
        String targetLatitudeString = "-0.103";
        String targetLongitudeString = "-74.575";

        double currentLatitude;
        double currentLongitude;

        bool autoLand = false;
        bool autoLandAtTarget = false;
        bool courseCorrected = false;
        bool gaveUpOnCourseCorrections = false;
        bool tooLittleThrustToLand = false;
        bool deorbitBurnFirstMessage = false;
        bool extendedLandingLegs = false;
        double decelerationEndAltitudeASL = 2500.0;



        //drive code stuff:
        Vector3d lastThrustError = Vector3d.zero;
        const double thrustKP = 3.0;
        const double thrustKD = 15.0;
        double thrustGainAttenuator = 1;





        /////////////////////////////////////////
        // GUI //////////////////////////////////
        /////////////////////////////////////////


        public override void drawGUI(int baseWindowID)
        {
            GUI.skin = HighLogic.Skin;

            windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, "Landing Autopilot", GUILayout.Width(300), GUILayout.Height(100));
            if (showHelpWindow)
            {
                helpWindowPos = GUILayout.Window(HELP_WINDOW_ID, helpWindowPos, HelpWindowGUI, "MechJeb Landing Autopilot Help", GUILayout.Width(400), GUILayout.Height(500));
            }
        }


        protected override void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            bool parseError = false;

            double newTargetLatitude;
            if (Double.TryParse(targetLatitudeString, out newTargetLatitude)) targetLatitude = newTargetLatitude;
            else parseError = true;
            
            double newTargetLongitude;
            if (Double.TryParse(targetLongitudeString, out newTargetLongitude)) targetLongitude = newTargetLongitude;
            else parseError = true;
            
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            if (parseError) labelStyle.normal.textColor = Color.yellow;

            GUILayout.Label("Target:", labelStyle);
            targetLatitudeString = GUILayout.TextField(targetLatitudeString, GUILayout.Width(80));
            GUILayout.Label("° N, ");
            targetLongitudeString = GUILayout.TextField(targetLongitudeString, GUILayout.Width(80));
            GUILayout.Label("° E");

            showHelpWindow = GUILayout.Toggle(showHelpWindow, "?", new GUIStyle(GUI.skin.button));

            GUILayout.EndHorizontal();

            switch (simulationResult.endCondition)
            {
                case SimulationResult.EndCondition.LANDED:
                    GUILayout.Label(String.Format("Predicted landing site: {0:0.000}° N, {1:0.000}° E", simulationResult.predictedLandingLatitude, simulationResult.predictedLandingLongitude));
                    double eastError = eastErrorKm(simulationResult.predictedLandingLongitude, targetLongitude, simulationResult.predictedLandingLatitude);
                    double northError = northErrorKm(simulationResult.predictedLandingLatitude, targetLatitude);
                    GUILayout.Label(String.Format("{1:0.0} km " + (northError > 0 ? "north" : "south") +
                        ", {0:0.0} km " + (eastError > 0 ? "east" : "west") + " of target", Math.Abs(eastError), Math.Abs(northError)));
                    GUILayout.Label(String.Format("Current coordinates: {0:0.000}° N, {1:0.000}° E", clampDegrees(currentLatitude), clampDegrees(currentLongitude)));
                    break;

                case SimulationResult.EndCondition.AEROBRAKED:
                    GUILayout.Label("Predicted orbit after aerobreaking:");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(String.Format("Apoapsis = {0:0} km", simulationResult.predictedAerobrakeApoapsis / 1000.0));
                    GUILayout.Label(String.Format("Periapsis = {0:0} km", simulationResult.predictedAerobrakePeriapsis / 1000.0));
                    GUILayout.EndHorizontal();
                    break;

                case SimulationResult.EndCondition.TIMED_OUT:
                    GUILayout.Label("Reentry simulation timed out.");
                    break;

                case SimulationResult.EndCondition.NO_REENTRY:
                    GUILayout.Label("Orbit does not re-enter:");
                    GUILayout.Label(String.Format("Periapsis = {0:0} km | Atmosphere height = {1:0} km", part.vessel.orbit.PeA / 1000.0, part.vessel.mainBody.maxAtmosphereAltitude / 1000.0));
                    break;
            }

            if ((simulationResult.endCondition == SimulationResult.EndCondition.LANDED || simulationResult.endCondition == SimulationResult.EndCondition.AEROBRAKED) 
                && part.vessel.mainBody.maxAtmosphereAltitude > 0)
            {
                GUILayout.Label(String.Format("Predicted maximum of {0:0.0} gees", simulationResult.predictedMaxGees));
            }


            GUILayout.BeginHorizontal();

            bool newAutoLand = GUILayout.Toggle(autoLand, "Auto-land", new GUIStyle(GUI.skin.button));
            if (newAutoLand && !autoLand && simulationResult.endCondition != SimulationResult.EndCondition.LANDED) deorbitBurnFirstMessage = true;
            else autoLand = newAutoLand;

            if (autoLand || gaveUpOnCourseCorrections) autoLandAtTarget = false;

            bool newAutoLandAtTarget = GUILayout.Toggle(autoLandAtTarget, "Auto-land at target", new GUIStyle(GUI.skin.button));
            if (newAutoLandAtTarget && !autoLandAtTarget && simulationResult.endCondition != SimulationResult.EndCondition.LANDED) deorbitBurnFirstMessage = true;
            else autoLandAtTarget = newAutoLandAtTarget;
            
            if (autoLandAtTarget)
            {
                autoLand = false;
                gaveUpOnCourseCorrections = false;
            }
            else
            {
                correctedSurfaceVelocitySet = false;
            }

            if (!autoLand && !autoLandAtTarget) extendedLandingLegs = false;

            if(autoLand || autoLandAtTarget) deorbitBurnFirstMessage = false;

            GUILayout.EndHorizontal();

            if (autoLand || autoLandAtTarget)
            {
                String stateString = "Landing step: " + landerStateStrings[landerState];
                GUILayout.Label(stateString);
            }

            GUIStyle yellow = new GUIStyle(GUI.skin.label);
            yellow.normal.textColor = Color.yellow;
            if (gaveUpOnCourseCorrections)
            {
                autoLandAtTarget = false;
                GUILayout.Label("Gave up on landing at target. Is it on the wrong side of " + part.vessel.mainBody.name + "?", yellow);
            }
            if (tooLittleThrustToLand) GUILayout.Label("Warning: Too little thrust to land.", yellow);
            if (deorbitBurnFirstMessage) GUILayout.Label("You must do a deorbit burn before activating auto-land", yellow);


            if (part.vessel.mainBody.name.Equals("Kerbin") && GUILayout.Button("Target KSC"))
            {
                targetLatitudeString = KSC_LATITUDE.ToString();
                targetLongitudeString = KSC_LONGITUDE.ToString();
            }

            GUILayout.BeginHorizontal();
            
            
            GUILayout.EndHorizontal();


            GUILayout.EndVertical();

            GUI.DragWindow();
        }


        Vector2 scrollPosition;
        private void HelpWindowGUI(int windowID)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("MechJeb's landing autopilot can automatically fly you down to a landing on Kerbin or the Mun. Optionally, you may specify the coordinates to land at. The landing autopilot can also be used to predict the outcome of aerobraking maneuvers.\n\n" +
"While in orbit, the landing autopilot will display your current periapsis. The landing autopilot will not predict your landing location until you are on a trajectory that intersects Kerbin's atmosphere or the Mun's surface. Once you are on a landing trajectory, the landing autopilot will display the predicted landing site.\n\n" +
"Note that if you are on a trajectory through Kerbin's atmosphere that does not land but instead exits the atmosphere again, the landing autopilot will display the orbit you will have after exiting the atmosphere. This can be used to accurately judge aerobraking maneuvers. If you want to land instead of aerobrake, then you need to burn retrograde until the landing autopilot starts to show a predicted landing site.\n\n" +
"In the text fields, you can enter a target landing site. When you are on a landing trajectory, the landing autopilot will display your predicted landing site, calculated by a simulation of the descent. It will also display how far from the current target this landing site is. The landing autopilot will also display your current coordinates.\n\n" +
"The \"Auto-land\" and \"Auto-land at target\" buttons let the landing autopilot take control of the ship to do a powered landing. \"Auto-land\" will disregard the target coordinates and put the ship down wherever it is currently falling toward, much like the Translatron LAND feature. \"Auto-land at target\" will land the ship at the target coordinates you have entered in the text fields. \n\n" +
"Using \"Auto-land at target\":\n\n" +
"\"Auto-land at target\" is best activated immediately after your deorbit burn. You don't have to make a very precise deorbit burn: the autopilot will automatically correct the trajectory to point toward the desired landing site. However, you do need to make sure that you do your deorbit burn on the correct side of the planet or Mun. A decent rule of thumb is to do your deorbit burn when your target is 90 degrees ahead of you along your orbit. Once the landing autopilot has corrected the trajectory, it will display \"On course for target.\" At this point it's safe to activate time warp if you want to speed up the landing.\n\n" +
"The landing autopilot will automatically deactivate time warp as it prepares to make the deceleration burn. During Mun landings, the autopilot will continue to make subtle corrections to the landing trajectory during the deceleration burn to aim more precisely at your target.\n\n" +
"For Mun landings, the deceleration burn ends directly above the target at an altitude of 2500m. The autopilot will kill all horizontal speed and descend vertically to the target. For Kerbin landings, the deceleration burn occurs in the last 2000m of the descent.\n\n" +
"Note that if you enable RCS during the final vertical descent, the autopilot will automatically use it to kill horizontal velocity and help land vertically.");
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }


        double northErrorKm(double predLatitude, double targLatitude)
        {
            return (predLatitude - targLatitude) * Math.PI / 180 * part.vessel.mainBody.Radius / 1000.0;
        }

        double eastErrorKm(double predLongitude, double targLongitude, double predLatitude)
        {
            double degreeError = clampDegrees(predLongitude - targLongitude);
            return degreeError * Math.PI / 180 * part.vessel.mainBody.Radius * Math.Cos(predLatitude * Math.PI / 180) / 1000.0;
        }

        //keeps angles in the range -180 to 180
        double clampDegrees(double angle)
        {
            angle = angle + ((int)(2 + Math.Abs(angle) / 360)) * 360.0; //should be positive
            angle = angle % 360.0;
            if (angle > 180.0) return angle - 360.0;
            else return angle;
        }


        ///////////////////////////////////////
        // FLY BY WIRE ////////////////////////
        ///////////////////////////////////////


        public override void drive(FlightCtrlState s)
        {
            //if we are far above the surface and steering for a specific target, then we do small burns to 
            //move our predicted landing site toward the target
            if (autoLandAtTarget && correctedSurfaceVelocitySet && courseCorrectionsAllowed())
            {
                double errorKm = Math.Sqrt(Math.Pow(eastErrorKm(simulationResult.predictedLandingLongitude, targetLongitude, simulationResult.predictedLandingLatitude), 2)
                                          + Math.Pow(northErrorKm(simulationResult.predictedLandingLatitude, targetLatitude), 2));
                if (courseCorrected)
                {
                    landerState = ON_COURSE;

                    if (errorKm < 0.3)
                    {
                        //do nothing, we're on course
                        s.mainThrottle = 0.0F;
                        steerTowardThrustVector(s, -vesselState.velocityVesselSurfaceUnit);
                    }
                    else
                    {
                        //we're too far off: we need to do another course correction:
                        courseCorrected = false;
                        s.mainThrottle = 0.0F;
                        steerTowardThrustVector(s, -vesselState.velocityVesselSurfaceUnit);
                    }
                }
                else
                {
                    landerState = COURSE_CORRECTIONS;

                    if (errorKm < 0.1)
                    {
                        //course correction complete
                        s.mainThrottle = 0.0F;
                        courseCorrected = true;
                        steerTowardThrustVector(s, -vesselState.velocityVesselSurfaceUnit);
                    }
                    else
                    {
                        //velAUnit is chosen periodically in onPartFixedUpdate
                        velBUnit = vesselState.leftSurface;

                        //then we have time to do course corrections to try and land at our target:
                        Vector3d velocityCorrection = correctedSurfaceVelocity - vesselState.velocityVesselSurface;

                        //project out of correctedSurfaceVelocity - surfaceVelocity any components normal to velA and velB
                        Vector3d uncorrectedDirection = Vector3d.Cross(velAUnit, velBUnit);
                        velocityCorrection = Vector3d.Exclude(uncorrectedDirection, velocityCorrection);

                        Vector3d desiredThrustVector = velocityCorrection.normalized;
                        steerTowardThrustVector(s, desiredThrustVector);

                        double speedCorrectionTimeConstant = 10.0;
                        
                        if (Vector3d.Dot(vesselState.forward, desiredThrustVector) > 0.75)
                        {
                            s.mainThrottle = Mathf.Clamp((float)(velocityCorrection.magnitude / (speedCorrectionTimeConstant * vesselState.maxThrustAccel)), 0.0F, 1.0F);
                        }
                    }
                }
            }
            else if (autoLand || autoLandAtTarget) //if we are landing and too close to the surface for course corrections:
            {
                if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)
                {
                    TimeWarp.SetRate(0);
                }

                //if we're done:
                if (part.vessel.Landed || part.vessel.Splashed)
                {
                    autoLand = false;
                    autoLandAtTarget = false;
                    FlightInputHandler.SetNeutralControls();
                    return;
                }

                if (vesselState.altitudeTrue < 300 && !extendedLandingLegs)
                {
                    extendLandingLegs();
                    extendedLandingLegs = true;
                }

                //if we are very close to the surface, we first hover while we kill horizontal speed, and then descend to touchdown
                if (vesselState.altitudeASL < decelerationEndAltitudeASL + 5)
                {
                    //thrust up, angled slightly to kill horizontal velocity
                    Vector3d velocitySurfaceComponent = Vector3d.Exclude(vesselState.up, vesselState.velocityVesselSurface);
                    Vector3d desiredThrustVector = vesselState.up;
                    desiredThrustVector -= Math.Min(0.05, velocitySurfaceComponent.magnitude) * velocitySurfaceComponent.normalized;
                    steerTowardThrustVector(s, desiredThrustVector.normalized);

                    //control thrust to control vertical speed:
                    double minAccel = -vesselState.localg;
                    double maxAccel = -vesselState.localg + Vector3d.Dot(vesselState.forward, vesselState.up) * vesselState.maxThrustAccel;
                    double controlledSpeed = Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.up);
                    double speedCorrectionTimeConstant = 0.3;

                    double descentSpeed;
                    if (vesselState.altitudeTrue > 200) descentSpeed = -0.5 * Math.Sqrt(2 * maxAccel * vesselState.altitudeTrue);
                    else descentSpeed = -0.5 * Math.Sqrt(2 * maxAccel * 200) * Math.Min(vesselState.altitudeTrue, vesselState.altitudeASL) / 200.0; //desired descent speed

                    //the "HOVER" state is a brief burst of thrust that kills all ground velocity parallel to the thrust vector,
                    //so we can drop straight down. Stop thrusting when there is a positive component of velocity along
                    //the ground projection of the thrust vector
                    if (landerState != FINAL_DESCENT 
                        && Vector3d.Dot(Vector3d.Exclude(vesselState.up, vesselState.forward), vesselState.velocityVesselSurface) < 0)
                    {
                        landerState = HOVER;
                    }
                    else
                    {
                        landerState = FINAL_DESCENT;
                    }

                    double desiredSpeed;
                    if (landerState == FINAL_DESCENT)
                    {
                        desiredSpeed = descentSpeed;
                    }
                    else //landerState == HOVER
                    {
                        desiredSpeed = 0; //hover until horizontal velocity is killed
                    }

                    //set throttle appropriately:
                    double speedError = desiredSpeed - controlledSpeed;
                    double desiredAccel = speedError / speedCorrectionTimeConstant;
                    s.mainThrottle = Mathf.Clamp((float)((desiredAccel - minAccel) / (maxAccel - minAccel)), 0.0F, 1.0F);

                    //use RCS to help kill horizontal velocity
                    Vector3d surfaceVelocityVesselFrame = part.vessel.transform.InverseTransformDirection(vesselState.velocityVesselSurface);

                    s.X = (float)(5 * surfaceVelocityVesselFrame.x);
                    if (Math.Abs(s.X) < 0.1) s.X = 0.0F;
                    s.X = Mathf.Clamp(s.X, -1.0F, +1.0F);

                    s.Y = (float)(5 * surfaceVelocityVesselFrame.z);
                    if (Math.Abs(s.Y) < 0.1) s.Y = 0.0F;
                    s.Y = Mathf.Clamp(s.Y, -1.0F, +1.0F);
                }
                else
                {
                    //we aren't right next to the ground but our speed is approaching the limit:
                    Vector3d desiredThrustVector = -vesselState.velocityVesselSurfaceUnit;
                    if (Vector3d.Dot(desiredThrustVector, vesselState.up) < -0.5)
                    {
                        desiredThrustVector = vesselState.up;
                    }
                    else if (Vector3d.Dot(desiredThrustVector, vesselState.up) < 0)
                    {
                        desiredThrustVector = (vesselState.up + Vector3d.Exclude(vesselState.up, desiredThrustVector).normalized).normalized;
                    }

                    double controlledSpeed = vesselState.speedSurface * Math.Sign(Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.up)); //positive if we are ascending, negative if descending
                    double desiredSpeed = -decelerationSpeed(vesselState.altitudeASL, vesselState.maxThrustAccel, part.vessel.mainBody);
                    double desiredSpeedAfterDt = -decelerationSpeed(vesselState.altitudeASL + vesselState.deltaT * Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.up), vesselState.maxThrustAccel, part.vessel.mainBody);
                    double minAccel = -vesselState.localg * Math.Abs(Vector3d.Dot(vesselState.velocityVesselSurfaceUnit, vesselState.up));
                    double maxAccel = vesselState.maxThrustAccel * Vector3d.Dot(vesselState.forward, -vesselState.velocityVesselSurfaceUnit) - vesselState.localg * Math.Abs(Vector3d.Dot(vesselState.velocityVesselSurfaceUnit, vesselState.up));

                    if (autoLandAtTarget && correctedSurfaceVelocitySet)
                    {
                        velAUnit = vesselState.upNormalToVelSurface;
                        velBUnit = vesselState.leftSurface;

                        double velACorrection = Vector3d.Dot(velAUnit, correctedSurfaceVelocity - vesselState.velocityVesselSurface);
                        double velACorrectionAngle = velACorrection / (vesselState.maxThrustAccel * 5.0); //1 second time constant at max thrust
                        velACorrectionAngle = Mathf.Clamp((float)velACorrectionAngle, -0.10F, 0.10F);
                        double velBCorrection = Vector3d.Dot(velBUnit, correctedSurfaceVelocity - vesselState.velocityVesselSurface);
                        double velBCorrectionAngle = velBCorrection / (vesselState.maxThrustAccel * 5.0);
                        velBCorrectionAngle = Mathf.Clamp((float)velBCorrectionAngle, -0.10F, 0.10F);

                        desiredThrustVector = (desiredThrustVector + velACorrectionAngle * velAUnit + velBCorrectionAngle * velBUnit).normalized;
                    }

                    if (Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.up) > 0 
                             || Vector3d.Dot(vesselState.forward, desiredThrustVector) < 0.75)
                    {
                        s.mainThrottle = 0;
                    }
                    else
                    {
                        double speedCorrectionTimeConstant = 0.3;
                        double speedError = desiredSpeed - controlledSpeed;
                        double desiredAccel = speedError / speedCorrectionTimeConstant + (desiredSpeedAfterDt - desiredSpeed) / vesselState.deltaT;
                        s.mainThrottle = Mathf.Clamp((float)((desiredAccel - minAccel) / (maxAccel - minAccel)), 0.0F, 1.0F);
                    }

                    if (s.mainThrottle == 0.0F) landerState = PREPARING_TO_DECELERATE;
                    else landerState = DECELERATING;

                    steerTowardThrustVector(s, desiredThrustVector);
                }
            }

        }

        bool courseCorrectionsAllowed()
        {
            return (vesselState.speedSurface < 0.8 * decelerationSpeed(vesselState.altitudeASL, vesselState.maxThrustAccel, part.vessel.mainBody) 
                    && vesselState.altitudeASL > 5000);
        }

        void extendLandingLegs()
        {
            foreach (Part p in part.vessel.parts)
            {
                if (p is LandingLeg && ((LandingLeg)p).legState == LandingLeg.LegStates.RETRACTED)
                {
                    ((LandingLeg)p).DeployOnActivate = true;
                    p.force_activate();
                }
            }
        }

        //slews the ship toward desiredThrustVector
        void steerTowardThrustVector(FlightCtrlState s, Vector3d desiredThrustVector)
        {
            if (Vector3d.Dot(vesselState.forward, desiredThrustVector) < 0)
            {
                //if our current heading is way off the desired one, establish an intermediate goal that is less than 90 degrees away:
                desiredThrustVector = ((vesselState.forward + desiredThrustVector) / 2).normalized;
            }
            Vector3d thrustError = (Vector3d)part.vessel.transform.InverseTransformDirection((Vector3)desiredThrustVector);

            Vector3d derivativeThrustError = (thrustError - lastThrustError) / vesselState.deltaT;
            lastThrustError = thrustError;

            Vector3d MOI = part.vessel.findLocalMOI(vesselState.CoM);
            Vector3d angularVelocity = part.vessel.transform.InverseTransformDirection(part.vessel.rigidbody.angularVelocity);

            Vector3d command = thrustKP * thrustError + thrustKD * derivativeThrustError;
            command *= (MOI.magnitude / 60.0); //need to use more force on the controls for heavier ships

            //if we find we're constantly trying to give commands greater than 1, reduce all the gains
            //until our commands fall in a more reasonable range. this seems to give better control on big rockets
            command *= thrustGainAttenuator;
            if (Math.Abs(command.x) > 1 || Math.Abs(command.z) > 1)
            {
                thrustGainAttenuator *= 0.99;
            }
            else if (thrustGainAttenuator < 1)
            {
                thrustGainAttenuator *= 1.005;
            }

            s.yaw += (float)command.x;
            s.pitch -= (float)command.z;

            s.yaw = Mathf.Clamp(s.yaw, -1.0F, +1.0F);
            s.pitch = Mathf.Clamp(s.pitch, -1.0F, +1.0F);

            //just damp out any roll:
            s.roll += (float)(angularVelocity.y * MOI.y);
            s.roll = Mathf.Clamp(s.roll, -1.0F, +1.0F);
        }


        //////////////////////////////
        // VESSEL HISTORY UPDATES ////
        //////////////////////////////



        int counter = 0;
        public override void onPartFixedUpdate()
        {
            if (!enabled) return;

            if (part.vessel.mainBody.maxAtmosphereAltitude > 0)
            {
                double groundAltitude = vesselState.altitudeASL - vesselState.altitudeTrue;
                decelerationEndAltitudeASL = groundAltitude + 15;
            }
            else
            {
                decelerationEndAltitudeASL = 2500.0;
            }
            

            currentLatitude = part.vessel.mainBody.GetLatitude(vesselState.CoM);
            currentLongitude = part.vessel.mainBody.GetLongitude(vesselState.CoM);

            if (autoLandAtTarget && correctedSurfaceVelocitySet)
            {
                //propagate corrected velocity to next frame. this is nontrivial since it's a rotating frame quantity
                correctedSurfaceVelocity += part.vessel.mainBody.getRFrmVel(vesselState.CoM); //convert to inertial frame velocity
                correctedSurfaceVelocity += vesselState.deltaT * FlightGlobals.getGeeForceAtPosition(vesselState.CoM); //apply gravity
                correctedSurfaceVelocity -= part.vessel.mainBody.getRFrmVel(vesselState.CoM + vesselState.deltaT * vesselState.velocityVesselOrbit); //convert to rotating frame velocity in new position

                double maxSurfaceVel = decelerationSpeed(vesselState.altitudeASL /*measureAltitudeAboveSurface(vesselState.CoM, part.vessel.mainBody)*/, vesselState.maxThrustAccel, part.vessel.mainBody);
                if (correctedSurfaceVelocity.magnitude > maxSurfaceVel)
                {
                    correctedSurfaceVelocity = maxSurfaceVel * correctedSurfaceVelocity.normalized;
                }
            }



            if (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate && 
                (nextSimulationDelayMs == 0 || nextSimulationTimer.ElapsedMilliseconds > nextSimulationDelayMs))
            {
                Stopwatch s = Stopwatch.StartNew();
                simulationResult = simulateReentryRK4(simulationTimeStep, Vector3d.zero);
                s.Stop();
                nextSimulationDelayMs = 10 * s.ElapsedMilliseconds;
                nextSimulationTimer.Reset();
                nextSimulationTimer.Start();

                if (autoLandAtTarget)
                {
                    if (!correctedSurfaceVelocitySet) counter = 0;

                    if ((counter % 25) == 0)
                    {
                        if (courseCorrectionsAllowed())
                        {
                            velAUnit = chooseDownrangeVelocityVector();
                        }
                        else
                        {
                            velAUnit = vesselState.upNormalToVelSurface;
                        }
                    }
                    if ((counter % 5) == 0) computeCorrectedSurfaceVelocity();
                }
            }

        }
        

        public override String getName()
        {
            return "Landing autopilot";
        }

        ///////////////////////////////////////
        // POWERED LANDINGS ///////////////////
        ///////////////////////////////////////


        //the deceleration phase gradually decelerates to come to a hover at 2000m ASL
        //this function gives the speed of the vessel as a function of altitude for the deceleration phase
        double decelerationSpeed(double altitudeASL, double thrustAcc, CelestialBody body)
        {
            //I think it should never be necessary to decelerate above 2000m when landing on kerbin
            if (body.maxAtmosphereAltitude > 0 && altitudeASL > 2000) return Double.MaxValue;

            double surfaceGravity = ARUtils.G * body.Mass / Math.Pow(body.Radius, 2);
            if (thrustAcc < surfaceGravity)
            {
                tooLittleThrustToLand = true;
                return Double.MaxValue;
            }
            else
            {
                tooLittleThrustToLand = false;
            }

            return 0.8 * Math.Sqrt(2 * (thrustAcc - surfaceGravity) * (altitudeASL - decelerationEndAltitudeASL));
        }

        ///////////////////////////////////////
        // REENTRY SIMULATION /////////////////
        ///////////////////////////////////////

        

        //inverse matrix:

        Vector3d velAUnit;
        Vector3d velBUnit;
        Vector3d correctedSurfaceVelocity;
        bool correctedSurfaceVelocitySet = false;

        //figure out what we wish our velocity were right now if we want to land at the user's target
        //we do this by first doing a pair of hypothetical simulations, which we have extra velocity in
        //two different orthogonal directions. By observing how this affects the landing site we compute the change in our velocity
        //necessary to produce the desired change in our predicted landing location
        void computeCorrectedSurfaceVelocity()
        {
            double offsetMagnitude = -5.0;

            //simulate a trajectory where we have extra velocity in the A direction
            SimulationResult resultA = simulateReentryRK4(simulationTimeStep * 10.0, offsetMagnitude * velAUnit);
            double velALatitude = resultA.predictedLandingLatitude;
            double velALongitude = resultA.predictedLandingLongitude;

            //if simulation doesn't hit the ground, we should burn retrograde until it does. set a goal that will cause us to deorbit
            if (resultA.endCondition != SimulationResult.EndCondition.LANDED)
            {
                gaveUpOnCourseCorrections = true;
                correctedSurfaceVelocity = vesselState.velocityVesselSurface - 10 * vesselState.velocityVesselSurface.normalized;
                return;
            }

            //simulate a trajectory where we have extra velocity in the B direction
            SimulationResult resultB = simulateReentryRK4(simulationTimeStep*10.0, offsetMagnitude * velBUnit);
            double velBLatitude = resultB.predictedLandingLatitude;
            double velBLongitude = resultB.predictedLandingLongitude;

            if (resultB.endCondition != SimulationResult.EndCondition.LANDED)
            {
                gaveUpOnCourseCorrections = true;
                correctedSurfaceVelocity = vesselState.velocityVesselSurface - 10 * vesselState.velocityVesselSurface.normalized;
                return;
            }

            //simulate a trajectory with our current velocity
            SimulationResult resultCurrent = simulateReentryRK4(simulationTimeStep * 10.0, Vector3d.zero);

            if (resultCurrent.endCondition != SimulationResult.EndCondition.LANDED)
            {
                gaveUpOnCourseCorrections = true;
                correctedSurfaceVelocity = vesselState.velocityVesselSurface - 10 * vesselState.velocityVesselSurface.normalized;
                return;
            }

            //look at how the different velocities changed our landing site:
            double partialLatitudePartialVelA = (velALatitude - resultCurrent.predictedLandingLatitude) / offsetMagnitude;
            double partialLongitudePartialVelA = clampDegrees(velALongitude - resultCurrent.predictedLandingLongitude) / offsetMagnitude;
            double partialLatitudePartialVelB = (velBLatitude - resultCurrent.predictedLandingLatitude) / offsetMagnitude;
            double partialLongitudePartialVelB = clampDegrees(velBLongitude - resultCurrent.predictedLandingLongitude) / offsetMagnitude;

            Matrix2x2 partialsMatrix = new Matrix2x2(partialLatitudePartialVelA, partialLatitudePartialVelB,
                                                     partialLongitudePartialVelA, partialLongitudePartialVelB);

            //invert this to get a matrix that tells how to change our velocity to produce a 
            //give change in our landing site:
            Matrix2x2 inversePartialsMatrix = partialsMatrix.inverse();

            Vector2d correctionMagnitudes = inversePartialsMatrix.times(new Vector2d(targetLatitude - simulationResult.predictedLandingLatitude,
                                                                                     clampDegrees(targetLongitude - simulationResult.predictedLandingLongitude)));

            Vector3d velocityCorrection = correctionMagnitudes.x * velAUnit + correctionMagnitudes.y * velBUnit;
            
            correctedSurfaceVelocity = vesselState.velocityVesselSurface + velocityCorrection;
            correctedSurfaceVelocitySet = true;
        }


        //When doing course corrections, if we thrust left or right we will shift our landing site left or right.
        //However burn prograde and up will both shift our landing site away from us, and retrograde and down
        //will both shift our landing site toward us. Which of prograde and up is more efficient depends on the situation
        //This function tries to pick a thrust vector in the prograde-up plane that will be most effective in shifting the 
        //landing site toward or away from us.
        Vector3d chooseDownrangeVelocityVector()
        {
            double offsetMagnitude = -5.0;

            SimulationResult resultPrograde = simulateReentryRK4(simulationTimeStep * 10.0, offsetMagnitude * vesselState.velocityVesselSurfaceUnit);

            //if the simulated trajectory doesn't land, retrograde is probably the best choice
            if (resultPrograde.endCondition != SimulationResult.EndCondition.LANDED)
            {
                gaveUpOnCourseCorrections = true;
                return vesselState.velocityVesselSurfaceUnit;
            }
            double progradeLandingLatitude = resultPrograde.predictedLandingLatitude;
            double progradeLandingLongitude = resultPrograde.predictedLandingLongitude;


            SimulationResult resultUp = simulateReentryRK4(simulationTimeStep * 10.0, offsetMagnitude * vesselState.upNormalToVelSurface);

            //if the simulated trajectory doesn't land, retrograde is probably the best choice
            if (resultUp.endCondition != SimulationResult.EndCondition.LANDED)
            {
                gaveUpOnCourseCorrections = true;
                return vesselState.velocityVesselSurfaceUnit;
            }

            double upLandingLatitude = resultUp.predictedLandingLatitude;
            double upLandingLongitude = resultUp.predictedLandingLongitude;


            SimulationResult resultCurrent = simulateReentryRK4(simulationTimeStep * 10.0, Vector3d.zero);

            if (resultCurrent.endCondition != SimulationResult.EndCondition.LANDED)
            {
                gaveUpOnCourseCorrections = true;
                return vesselState.velocityVesselSurfaceUnit;
            }

            double progradeShiftKm = Math.Sqrt(Math.Pow(eastErrorKm(progradeLandingLongitude, resultCurrent.predictedLandingLongitude, resultCurrent.predictedLandingLatitude), 2)
                                             + Math.Pow(northErrorKm(progradeLandingLatitude, resultCurrent.predictedLandingLatitude), 2));

            double upShiftKm = Math.Sqrt(Math.Pow(eastErrorKm(upLandingLongitude, resultCurrent.predictedLandingLongitude, resultCurrent.predictedLandingLatitude), 2)
                                       + Math.Pow(northErrorKm(upLandingLatitude, resultCurrent.predictedLandingLatitude), 2));

            //now we get the most effective thrust vector by taking a linear combination of these two possibilities weighted
            //by the magnitude of their effect on the landing location
            return (progradeShiftKm * vesselState.velocityVesselSurfaceUnit + upShiftKm * vesselState.upNormalToVelSurface).normalized;

        }





        //predict the reentry trajectory. uses the fourth order Runge-Kutta numerical integration scheme
        protected SimulationResult simulateReentryRK4(double dt, Vector3d velOffset)
        {
            SimulationResult result = new SimulationResult();

            //should change this to also account for hyperbolic orbits where we have passed periapsis
            if (part.vessel.orbit.PeA > part.vessel.mainBody.maxAtmosphereAltitude)
            {
                result.endCondition = SimulationResult.EndCondition.NO_REENTRY;
                return result;
            }

            //double groundAltitude = vesselState.altitudeASL - measureAltitudeAboveSurface(vesselState.CoM, part.vessel.mainBody);

            //use the known orbit in vacuum to find the position and velocity when we first hit the atmosphere 
            //or start decelerating with thrust:
            AROrbit freefallTrajectory = new AROrbit(vesselState.CoM, vesselState.velocityVesselOrbit + velOffset, vesselState.time, part.vessel.mainBody);
            double initialT = projectFreefallEndTime(freefallTrajectory/*, groundAltitude*/);
            //a hack to detect improperly initialized stuff and not try to do the simulation
            if (double.IsNaN(initialT))
            {
                result.endCondition = SimulationResult.EndCondition.NO_REENTRY;
                return result;
            }
            
            Vector3d pos = freefallTrajectory.positionAtTime(initialT);
            Vector3d vel = freefallTrajectory.velocityAtTime(initialT);

            double dragCoeffOverMass = vesselState.massDrag / vesselState.mass;

            //now integrate the equations of motion until we hit the surface or we exit the atmosphere again:
            double t = initialT;
            result.predictedMaxGees = 0;
            while (true)
            {
                //one time step of RK4:
                Vector3d dv1 = dt * ARUtils.computeTotalAccel(pos, vel, dragCoeffOverMass, part.vessel.mainBody);
                Vector3d dx1 = dt * vel;

                Vector3d dv2 = dt * ARUtils.computeTotalAccel(pos + 0.5 * dx1, vel + 0.5 * dv1, dragCoeffOverMass, part.vessel.mainBody);
                Vector3d dx2 = dt * (vel + 0.5 * dv1);

                Vector3d dv3 = dt * ARUtils.computeTotalAccel(pos + 0.5 * dx2, vel + 0.5 * dv2, dragCoeffOverMass, part.vessel.mainBody);
                Vector3d dx3 = dt * (vel + 0.5 * dv2);

                Vector3d dv4 = dt * ARUtils.computeTotalAccel(pos + dx3, vel + dv3, dragCoeffOverMass, part.vessel.mainBody);
                Vector3d dx4 = dt * (vel + dv3);

                Vector3d dx = (dx1 + 2 * dx2 + 2 * dx3 + dx4) / 6.0;
                Vector3d dv = (dv1 + 2 * dv2 + 2 * dv3 + dv4) / 6.0;

                pos += dx;
                vel += dv;
                t += dt;



                double altitudeASL = FlightGlobals.getAltitudeAtPos(pos);


                //We will thrust to reduce our speed if it is too high. However we have to 
                //be aware that it's the *surface* frame speed that is controlled.
                Vector3d surfaceVel = vel - part.vessel.mainBody.getRFrmVel(pos);
                double maxSurfaceVel = decelerationSpeed(altitudeASL, vesselState.maxThrustAccel, part.vessel.mainBody);
                if (altitudeASL > decelerationEndAltitudeASL && surfaceVel.magnitude > maxSurfaceVel)
                {
                    surfaceVel = maxSurfaceVel * surfaceVel.normalized;
                    vel = surfaceVel + part.vessel.mainBody.getRFrmVel(pos);
                }


                //during reentry the gees you feel come from drag:
                double dragAccel = ARUtils.computeDragAccel(pos, vel, dragCoeffOverMass, part.vessel.mainBody).magnitude;
                result.predictedMaxGees = Math.Max(dragAccel / 9.81, result.predictedMaxGees);


                //detect landing
                if (altitudeASL < decelerationEndAltitudeASL)
                {
                    result.endCondition = SimulationResult.EndCondition.LANDED;
                    result.predictedLandingLatitude = part.vessel.mainBody.GetLatitude(pos);
                    result.predictedLandingLongitude = part.vessel.mainBody.GetLongitude(pos);
                    result.predictedLandingLongitude -= (t - vesselState.time) * (360.0 / (part.vessel.mainBody.rotationPeriod)); //correct for planet rotation (360 degrees every 6 hours)
                    result.predictedLandingLongitude = clampDegrees(result.predictedLandingLongitude);
                    result.predictedLandingTime = t;
                    return result;
                }

                //detect the end of an aerobraking pass
                if (altitudeASL > part.vessel.mainBody.maxAtmosphereAltitude + 1000 && Vector3d.Dot(vel, pos - part.vessel.mainBody.position) > 0)
                {
                    result.endCondition = SimulationResult.EndCondition.AEROBRAKED;
                    result.predictedAerobrakeApoapsis = ARUtils.computeApoapsis(pos, vel, part.vessel.mainBody);
                    result.predictedAerobrakePeriapsis = ARUtils.computePeriapsis(pos, vel, part.vessel.mainBody);
                    return result;
                }

                //Don't tie up the CPU by running forever. Some real reentry trajectories will time out, but that's
                //just too bad. It's possible to spend an arbitarily long time in the atmosphere before landing.
                //For example, a circular orbit just inside the edge of the atmosphere will decay
                //extremely slowly. So there's no reasonable timeout setting here that will simulate all valid reentry trajectories
                //to their conclusion.
                if (t > initialT + 1000)
                {
                    result.endCondition = SimulationResult.EndCondition.TIMED_OUT;
                    return result;
                }
            }
        }




        //In a landing, we are going to free fall some way and then either hit atmosphere or start
        //decelerating with thrust. Until that happens we can project the orbit forward along a conic section.
        //Given an orbit, this function figures out the time at which the free-fall phase will end
        double projectFreefallEndTime(AROrbit offsetOrbit)
        {
            Vector3d currentPosition = offsetOrbit.positionAtTime(vesselState.time);
            double currentAltitude = FlightGlobals.getAltitudeAtPos(currentPosition);

            //check if we are already in the atmosphere
            if (currentAltitude < part.vessel.mainBody.maxAtmosphereAltitude)
            {
                return vesselState.time;
            }

            Vector3d currentOrbitVelocity = offsetOrbit.velocityAtTime(vesselState.time);
            Vector3d currentSurfaceVelocity = currentOrbitVelocity - part.vessel.mainBody.getRFrmVel(currentPosition);

            //check if we already should be decelerating or will be momentarily:
            if (currentSurfaceVelocity.magnitude > 0.9 * decelerationSpeed(currentAltitude , vesselState.maxThrustAccel, part.vessel.mainBody))
            {
                return vesselState.time;
            }

            //check if the orbit reenters at all
            double timeToPe = offsetOrbit.timeToPeriapsis(vesselState.time);
            Vector3d periapsisPosition = offsetOrbit.positionAtTime(vesselState.time + timeToPe);
            if (FlightGlobals.getAltitudeAtPos(periapsisPosition) > part.vessel.mainBody.maxAtmosphereAltitude)
            {
                return vesselState.time + timeToPe; //return the time of periapsis as a next best number
            }

            //determine time & velocity of reentry
            double minReentryTime = vesselState.time;
            double maxReentryTime = vesselState.time + timeToPe;
            while (maxReentryTime - minReentryTime > 1.0)
            {
                double test = (maxReentryTime + minReentryTime) / 2.0;
                Vector3d testPosition = offsetOrbit.positionAtTime(test);
                double testAltitude = FlightGlobals.getAltitudeAtPos(testPosition);
                Vector3d testOrbitVelocity = offsetOrbit.velocityAtTime(test);
                Vector3d testSurfaceVelocity = testOrbitVelocity - part.vessel.mainBody.getRFrmVel(testPosition);
                double testSpeed = testSurfaceVelocity.magnitude;

                if (testAltitude < part.vessel.mainBody.maxAtmosphereAltitude
                    || testAltitude < decelerationEndAltitudeASL 
                    || testSpeed > 0.9 * decelerationSpeed(testAltitude, vesselState.maxThrustAccel, part.vessel.mainBody))
                {
                    maxReentryTime = test;
                }
                else
                {
                    minReentryTime = test;
                }
            }

            return (maxReentryTime + minReentryTime) / 2;
        }



        void print(String s)
        {
            MonoBehaviour.print(s);
        }


    }

    class SimulationResult
    {
        public enum EndCondition { LANDED, AEROBRAKED, TIMED_OUT, NO_REENTRY }
        public EndCondition endCondition;
        public double predictedLandingLatitude = 0;
        public double predictedLandingLongitude = 0;
        public double predictedLandingTime = 0;
        public double predictedAerobrakeApoapsis;
        public double predictedAerobrakePeriapsis;
        public double predictedMaxGees = 0;
    }


    class Matrix2x2
    {
        double a, b, c, d;

        //  [a    b]
        //  [      ]
        //  [c    d]

        public Matrix2x2(double a, double b, double c, double d)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
        }

        public Matrix2x2 inverse()
        {
            //           1  [d   -c]
            //inverse = --- [      ]
            //          det [-b   a]

            double det = a * d - b * c;
            return new Matrix2x2(d / det, -b / det, -c / det, a / det);
        }

        public Vector2d times(Vector2d vec)
        {
            return new Vector2d(a * vec.x + b * vec.y, c * vec.x + d * vec.y);
        }
    }
}