using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using UnityEngine;


namespace MuMech
{


    class MechJebModuleLandingAutopilot : ComputerModule
    {


        public MechJebModuleLandingAutopilot(MechJebCore core) : base(core) { }

        //programmatic interface to land at a specific target. this should probably
        //only be called if the module has been enabled for a while so that it has had
        //the chance to run at least one landing simulation. I'm not sure what will happen
        //if you enable the module and then immediately call landAt()
        public void landAt(double latitude, double longitude)
        {
            core.controlClaim(this);

            autoLandAtTarget = true;
            deorbitBurnFirstMessage = false;
            gaveUpOnCourseCorrections = false;

            targetLatitude = latitude;
            targetLongitude = longitude;
            initializeDecimalStrings();
            initializeDMSStrings();

            if (targetLatitude == BEACH_LATITUDE && targetLongitude == BEACH_LONGITUDE) dmsInput = false;

            core.landDeactivate(this);
            landStep = LandStep.COURSE_CORRECTIONS;
            FlightInputHandler.SetNeutralControls();
        }

        public override void onModuleDisabled()
        {
            turnOffSteering();
            core.controlRelease(this);
        }

        public override void onControlLost()
        {
            turnOffSteering();
        }

        void turnOffSteering()
        {
            if (autoLandAtTarget)
            {
                autoLandAtTarget = false;
                core.attitudeDeactivate(this);
                FlightInputHandler.SetNeutralControls();
            }
            if (core.trans_land) core.landDeactivate(this);
        }

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

        bool _dmsInput;
        bool dmsInput
        {
            get { return _dmsInput; }
            set
            {
                if (_dmsInput != value) core.settingsChanged = true;
                _dmsInput = value;
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
            targetLongitude = settings["RC_targetLongitude"].valueDecimal(KSC_LONGITUDE);
            dmsInput = settings["RC_dmsInput"].valueBool(false);
            initializeDecimalStrings();
            initializeDMSStrings();

            Vector4 savedHelpWindowPos = settings["RC_helpWindowPos"].valueVector(new Vector4(150, 50));
            helpWindowPos = new Rect(savedHelpWindowPos.x, savedHelpWindowPos.y, 10, 10);

        }

        public override void onSaveGlobalSettings(SettingsManager settings)
        {
            base.onSaveGlobalSettings(settings);

            settings["RC_targetLatitude"].value_decimal = targetLatitude;
            settings["RC_targetLongitude"].value_decimal = targetLongitude;
            settings["RC_dmsInput"].value_bool = dmsInput;
            settings["RC_helpWindowPos"].value_vector = new Vector4(helpWindowPos.x, helpWindowPos.y);
        }

        public enum LandStep { ON_COURSE, COURSE_CORRECTIONS, DECELERATING, KILLING_HORIZONTAL_VELOCITY, FINAL_DESCENT, LANDED };
        String[] landerStateStrings = { "On course for target", "Executing course correction", "Decelerating", "Killing horizontal velocity", "Final descent", "Landed" };
        LandStep landStep;

        //simulation stuff:
        const double simulationTimeStep = 0.2; //seconds
        int simCounter = 0;
        public LandingPrediction prediction = new LandingPrediction();
        long nextSimulationDelayMs = 0;
        Stopwatch nextSimulationTimer = new Stopwatch();

        Vector3d velAUnit;
        Vector3d velBUnit;
        //Vector3d correctedSurfaceVelocity;
        Vector3d velocityCorrection;
        bool velocityCorrectionSet = false;


        //GUI stuff:
        bool showHelpWindow = false;
        const int WINDOW_ID = 7792;
        const int HELP_WINDOW_ID = 7794;

        const double KSC_LATITUDE = -0.103;
        const double KSC_LONGITUDE = -74.575;
        public const double BEACH_LATITUDE = 5.77;
        public const double BEACH_LONGITUDE = -62.12;
        String targetLatitudeString;
        String targetLongitudeString;
        String targetLatitudeDegString;
        String targetLatitudeMinString;
        String targetLatitudeSecString;
        String targetLongitudeDegString;
        String targetLongitudeMinString;
        String targetLongitudeSecString;
        bool dmsNorth = true;
        bool dmsEast = true;



        public bool autoLandAtTarget = false;
        bool gaveUpOnCourseCorrections = false;
        bool tooLittleThrustToLand = false;
        bool deorbitBurnFirstMessage = false;
        double decelerationEndAltitudeASL = 2500.0;



        /////////////////////////////////////////
        // GUI //////////////////////////////////
        /////////////////////////////////////////


        public override void drawGUI(int baseWindowID)
        {
            GUI.skin = HighLogic.Skin;

            windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, "Landing Autopilot", GUILayout.Width(320), GUILayout.Height(100));
            if (showHelpWindow)
            {
                helpWindowPos = GUILayout.Window(HELP_WINDOW_ID, helpWindowPos, HelpWindowGUI, "MechJeb Landing Autopilot Help", GUILayout.Width(400), GUILayout.Height(500));
            }
        }


        protected override void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();


            double newTargetLatitude;
            if (!dmsInput)
            {
                bool parseError = false;

                if (Double.TryParse(targetLatitudeString, out newTargetLatitude)) targetLatitude = newTargetLatitude;
                else parseError = true;

                double newTargetLongitude;
                if (Double.TryParse(targetLongitudeString, out newTargetLongitude)) targetLongitude = newTargetLongitude;
                else parseError = true;

                if (targetLatitudeString.ToLower().Equals("the") && targetLongitudeString.ToLower().Equals("beach"))
                {
                    parseError = false;
                    targetLatitude = BEACH_LATITUDE;
                    targetLongitude = BEACH_LONGITUDE;
                }

                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                if (parseError) labelStyle.normal.textColor = Color.yellow;

                GUILayout.Label("Target:", labelStyle);
                targetLatitudeString = GUILayout.TextField(targetLatitudeString, GUILayout.Width(80));
                GUILayout.Label("° N, ");
                targetLongitudeString = GUILayout.TextField(targetLongitudeString, GUILayout.Width(80));
                GUILayout.Label("° E");
            }
            else
            {
                bool parseError = false;

                double latDeg, latMin, latSec;
                if (Double.TryParse(targetLatitudeDegString, out latDeg)
                    && Double.TryParse(targetLatitudeMinString, out latMin)
                    && Double.TryParse(targetLatitudeSecString, out latSec))
                {
                    targetLatitude = (dmsNorth ? 1 : -1) * (latDeg + latMin / 60.0 + latSec / 3600.0);
                }
                else
                {
                    parseError = true;
                }

                double lonDeg, lonMin, lonSec;
                if (Double.TryParse(targetLongitudeDegString, out lonDeg)
                    && Double.TryParse(targetLongitudeMinString, out lonMin)
                    && Double.TryParse(targetLongitudeSecString, out lonSec))
                {
                    targetLongitude = (dmsEast ? 1 : -1) * (lonDeg + lonMin / 60.0 + lonSec / 3600.0);
                }
                else
                {
                    parseError = true;
                }


                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                if (parseError) labelStyle.normal.textColor = Color.yellow;

                GUILayout.Label("Target:", labelStyle);
                targetLatitudeDegString = GUILayout.TextField(targetLatitudeDegString, GUILayout.Width(35));
                GUILayout.Label("°");
                targetLatitudeMinString = GUILayout.TextField(targetLatitudeMinString, GUILayout.Width(20));
                GUILayout.Label("'");
                targetLatitudeSecString = GUILayout.TextField(targetLatitudeSecString, GUILayout.Width(20));
                GUILayout.Label("''");
                if (dmsNorth)
                {
                    if (GUILayout.Button("N")) dmsNorth = false;
                }
                else
                {
                    if (GUILayout.Button("S")) dmsNorth = true;
                }

                targetLongitudeDegString = GUILayout.TextField(targetLongitudeDegString, GUILayout.Width(35));
                GUILayout.Label("°");
                targetLongitudeMinString = GUILayout.TextField(targetLongitudeMinString, GUILayout.Width(20));
                GUILayout.Label("'");
                targetLongitudeSecString = GUILayout.TextField(targetLongitudeSecString, GUILayout.Width(20));
                GUILayout.Label("''");
                if (dmsEast)
                {
                    if (GUILayout.Button("E")) dmsEast = false;
                }
                else
                {
                    if (GUILayout.Button("W")) dmsEast = true;
                }
            }



            GUILayout.EndHorizontal();




            switch (prediction.outcome)
            {
                case LandingPrediction.Outcome.LANDED:
                    if (dmsInput) GUILayout.Label("Predicted landing site: " + dmsLocationString(prediction.landingLatitude, prediction.landingLongitude));
                    else GUILayout.Label(String.Format("Predicted landing site: {0:0.000}° N, {1:0.000}° E", prediction.landingLatitude, prediction.landingLongitude));

                    double eastError = eastErrorKm(prediction.landingLongitude, targetLongitude, targetLatitude);
                    double northError = northErrorKm(prediction.landingLatitude, targetLatitude);
                    GUILayout.Label(String.Format("{1:0.0} km " + (northError > 0 ? "north" : "south") +
                        ", {0:0.0} km " + (eastError > 0 ? "east" : "west") + " of target", Math.Abs(eastError), Math.Abs(northError)));

                    double currentLatitude = part.vessel.mainBody.GetLatitude(vesselState.CoM);
                    double currentLongitude = part.vessel.mainBody.GetLongitude(vesselState.CoM);
                    if (dmsInput) GUILayout.Label("Current coordinates: " + dmsLocationString(currentLatitude, currentLongitude));
                    else GUILayout.Label(String.Format("Current coordinates: {0:0.000}° N, {1:0.000}° E", currentLatitude, clampDegrees(currentLongitude)));
                    break;

                case LandingPrediction.Outcome.AEROBRAKED:
                    GUILayout.Label("Predicted orbit after aerobreaking:");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(String.Format("Apoapsis = {0:0} km", prediction.aerobrakeApoapsis / 1000.0));
                    GUILayout.Label(String.Format("Periapsis = {0:0} km", prediction.aerobrakePeriapsis / 1000.0));
                    GUILayout.EndHorizontal();
                    break;

                case LandingPrediction.Outcome.TIMED_OUT:
                    GUILayout.Label("Reentry simulation timed out.");
                    break;

                case LandingPrediction.Outcome.NO_REENTRY:
                    GUILayout.Label("Orbit does not re-enter:");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(String.Format("Periapsis = {0:0} km", part.vessel.orbit.PeA / 1000.0));
                    GUILayout.Label(String.Format("Atmosphere height = {0:0} km", part.vessel.mainBody.maxAtmosphereAltitude / 1000.0));
                    GUILayout.EndHorizontal();
                    break;
            }

            if ((prediction.outcome == LandingPrediction.Outcome.LANDED || prediction.outcome == LandingPrediction.Outcome.AEROBRAKED)
                && part.vessel.mainBody.maxAtmosphereAltitude > 0)
            {
                GUILayout.Label(String.Format("Predicted maximum of {0:0.0} gees", prediction.maxGees));
            }


            GUILayout.BeginHorizontal();

            GUIStyle normalStyle = new GUIStyle(GUI.skin.button);
            GUIStyle greenStyle = new GUIStyle(GUI.skin.button);
            greenStyle.onNormal.textColor = greenStyle.onFocused.textColor = greenStyle.onHover.textColor = greenStyle.onActive.textColor = Color.green;

            bool oldAutoLand = core.trans_land && !autoLandAtTarget;
            //print("core.trans_land = " + core.trans_land);
            //print("oldAutoLand = " + oldAutoLand);
            bool newAutoLand = GUILayout.Toggle(oldAutoLand, "LAND", (oldAutoLand ? greenStyle : normalStyle));
            //print("newAutoLand = " + newAutoLand);
            if (newAutoLand && !oldAutoLand)
            {
                autoLandAtTarget = false;
                core.controlClaim(this);
                core.landActivate(this);
                //print("called core.landActivate");
                FlightInputHandler.SetNeutralControls();
            }
            else if (!newAutoLand && oldAutoLand)
            {
                core.landDeactivate(this);
                //print("called core.landDeactivate");
            }


            bool newAutoLandAtTarget = GUILayout.Toggle(autoLandAtTarget, "LAND at target", (autoLandAtTarget ? greenStyle : normalStyle));

            if (newAutoLandAtTarget && !autoLandAtTarget)
            {
                if (prediction.outcome != LandingPrediction.Outcome.LANDED)
                {
                    deorbitBurnFirstMessage = true;
                    newAutoLandAtTarget = false;
                }
                else
                {
                    deorbitBurnFirstMessage = false;
                    gaveUpOnCourseCorrections = false;
                    core.controlClaim(this);
                    core.landDeactivate(this);
                    landStep = LandStep.COURSE_CORRECTIONS;
                    FlightInputHandler.SetNeutralControls();
                }
            }
            else if (!newAutoLandAtTarget && autoLandAtTarget)
            {
                turnOffSteering();
                core.controlRelease(this);
            }
            autoLandAtTarget = newAutoLandAtTarget;


            if (part.vessel.mainBody.name.Equals("Kerbin") && GUILayout.Button("Target KSC"))
            {
                targetLatitude = KSC_LATITUDE;
                targetLongitude = KSC_LONGITUDE;
                initializeDecimalStrings();
                initializeDMSStrings();
            }

            if (dmsInput)
            {
                if (GUILayout.Button("DEC"))
                {
                    dmsInput = false;
                    initializeDecimalStrings();
                }
            }
            else
            {
                if (GUILayout.Button("DMS"))
                {
                    dmsInput = true;
                    initializeDMSStrings();
                }
            }

            showHelpWindow = GUILayout.Toggle(showHelpWindow, "?", new GUIStyle(GUI.skin.button));



            GUILayout.EndHorizontal();

            if (autoLandAtTarget)
            {
                String stateString = "Landing step: " + landerStateStrings[(int)landStep];
                GUILayout.Label(stateString);
            }

            GUIStyle yellow = new GUIStyle(GUI.skin.label);
            yellow.normal.textColor = Color.yellow;
            if (gaveUpOnCourseCorrections) GUILayout.Label("Attempted course corrections made the trajectory no longer land. Try a bigger deorbit burn. Or is the target on the wrong side of " + part.vessel.mainBody.name + "?", yellow);
            if (tooLittleThrustToLand) GUILayout.Label("Warning: Too little thrust to land.", yellow);
            if (deorbitBurnFirstMessage) GUILayout.Label("You must do a deorbit burn before activating \"LAND at target\"", yellow);

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


        void initializeDecimalStrings()
        {
            targetLatitudeString = String.Format("{0:0.000}", targetLatitude);
            targetLongitudeString = String.Format("{0:0.000}", targetLongitude);
            if (targetLatitude == BEACH_LATITUDE && targetLongitude == BEACH_LONGITUDE)
            {
                targetLatitudeString = "the";
                targetLongitudeString = "beach";
            }
        }

        void initializeDMSStrings()
        {
            int targetLatitudeDeg = (int)Math.Floor(Math.Abs(targetLatitude));
            int targetLatitudeMin = (int)Math.Floor(60 * (Math.Abs(targetLatitude) - targetLatitudeDeg));
            int targetLatitudeSec = (int)Math.Floor(3600 * (Math.Abs(targetLatitude) - targetLatitudeDeg - targetLatitudeMin / 60.0));
            targetLatitudeDegString = targetLatitudeDeg.ToString();
            targetLatitudeMinString = targetLatitudeMin.ToString();
            targetLatitudeSecString = targetLatitudeSec.ToString();
            dmsNorth = (targetLatitude > 0);

            targetLongitude = clampDegrees(targetLongitude);
            int targetLongitudeDeg = (int)Math.Floor(Math.Abs(targetLongitude));
            int targetLongitudeMin = (int)Math.Floor(60 * (Math.Abs(targetLongitude) - targetLongitudeDeg));
            int targetLongitudeSec = (int)Math.Floor(3600 * (Math.Abs(targetLongitude) - targetLongitudeDeg - targetLongitudeMin / 60.0));
            targetLongitudeDegString = targetLongitudeDeg.ToString();
            targetLongitudeMinString = targetLongitudeMin.ToString();
            targetLongitudeSecString = targetLongitudeSec.ToString();
            dmsEast = (targetLongitude > 0);

        }

        String dmsLocationString(double lat, double lon)
        {
            return dmsAngleString(lat) + (lat > 0 ? " N, " : " S, ") + dmsAngleString(clampDegrees(lon)) + (clampDegrees(lon) > 0 ? " E" : " W");
        }

        String dmsAngleString(double angle)
        {
            int degrees = (int)Math.Floor(Math.Abs(angle));
            int minutes = (int)Math.Floor(60 * (Math.Abs(angle) - degrees));
            int seconds = (int)Math.Floor(3600 * (Math.Abs(angle) - degrees - minutes / 60.0));

            return String.Format("{0:0}° {1:0}' {2:0}\"", degrees, minutes, seconds);
        }


        double northErrorKm(double predLatitude, double targLatitude)
        {
            return (predLatitude - targLatitude) * Math.PI / 180 * part.vessel.mainBody.Radius / 1000.0;
        }

        double eastErrorKm(double predLongitude, double targLongitude, double targLatitude)
        {
            double degreeError = clampDegrees(predLongitude - targLongitude);
            return degreeError * Math.PI / 180 * part.vessel.mainBody.Radius * Math.Cos(targLatitude * Math.PI / 180) / 1000.0;
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

            if (!autoLandAtTarget) return;

            switch (landStep)
            {
                case LandStep.ON_COURSE:
                    driveOnCourse(s);
                    break;

                case LandStep.COURSE_CORRECTIONS:
                    driveCourseCorrections(s);
                    break;

                case LandStep.DECELERATING:
                    driveDecelerationBurn(s);
                    break;

                case LandStep.KILLING_HORIZONTAL_VELOCITY:
                    driveKillHorizontalVelocity(s);
                    break;

                case LandStep.FINAL_DESCENT:
                    driveFinalDescent(s);
                    break;
            }
        }

        void driveCourseCorrections(FlightCtrlState s)
        {
            //if we are too low to make course corrections, start the deceleration burn
            if (!courseCorrectionsAllowed())
            {
                landStep = LandStep.DECELERATING;
                s.mainThrottle = 0;
                core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.SURFACE_VELOCITY, this);
                return;
            }

            //            double errorKm = Math.Sqrt(Math.Pow(eastErrorKm(prediction.landingLongitude, targetLongitude, targetLatitude), 2)
            //                              + Math.Pow(northErrorKm(prediction.landingLatitude, targetLatitude), 2));

            //if our predicted landing site is close enough, stop trying to correct it
            double errorKm = part.vessel.mainBody.Radius / 1000.0 * latLonSeparation(prediction.landingLatitude, prediction.landingLongitude,
                                                                            targetLatitude, targetLongitude).magnitude;
            if (errorKm < courseCorrectionsAccuracyTargetKm())
            {
                //course correction complete
                landStep = LandStep.ON_COURSE;
                s.mainThrottle = 0.0F;
                core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.SURFACE_VELOCITY, this);
                return;
            }

            //if we haven't calculated the desired correction, just point retrograde
            if (!velocityCorrectionSet)
            {
                s.mainThrottle = 0;
                core.attitudeTo(Vector3.back, MechJebCore.AttitudeReference.SURFACE_VELOCITY, this);
                return;
            }


            //do course corrections to try and land at our target:

            //project out of correctedSurfaceVelocity - surfaceVelocity any components normal to velA and velB
            Vector3d uncorrectedDirection = Vector3d.Cross(velAUnit, velBUnit);
            velocityCorrection = Vector3d.Exclude(uncorrectedDirection, velocityCorrection);

            Vector3d desiredThrustVector = velocityCorrection.normalized;
            core.attitudeTo(desiredThrustVector, MechJebCore.AttitudeReference.INERTIAL, this);

            double speedCorrectionTimeConstant = 10.0;

            if (Vector3d.Dot(vesselState.forward, desiredThrustVector) > 0.9)
            {
                if (vesselState.maxThrustAccel > 0) s.mainThrottle = Mathf.Clamp((float)(velocityCorrection.magnitude / (speedCorrectionTimeConstant * vesselState.maxThrustAccel)), 0.0F, 1.0F);
                else s.mainThrottle = 0;
            }
            else
            {
                s.mainThrottle = 0;
            }

            if (velocityCorrectionSet)
            {
                velocityCorrection -= vesselState.deltaT * vesselState.thrustAccel(s.mainThrottle) * vesselState.forward;
            }
        }

        void driveOnCourse(FlightCtrlState s)
        {
            s.mainThrottle = 0.0F;
            core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.SURFACE_VELOCITY, this);

            //if we are too low for course corrections, start the deceleration burn
            if (!courseCorrectionsAllowed()) landStep = LandStep.DECELERATING;

            //check if another course correction is needed:
            double errorKm = part.vessel.mainBody.Radius / 1000.0 * latLonSeparation(prediction.landingLatitude, prediction.landingLongitude,
                                                                            targetLatitude, targetLongitude).magnitude;
            if (errorKm > 2 * courseCorrectionsAccuracyTargetKm()) landStep = LandStep.COURSE_CORRECTIONS;
        }

        void driveKillHorizontalVelocity(FlightCtrlState s)
        {
            if (Vector3d.Dot(Vector3d.Exclude(vesselState.up, vesselState.forward), vesselState.velocityVesselSurface) > 0)
            {
                s.mainThrottle = 0;
                core.attitudeTo(Vector3.up, MechJebCore.AttitudeReference.SURFACE_NORTH, this);
                landStep = LandStep.FINAL_DESCENT;
                return;
            }

            //control thrust to control vertical speed:
            double desiredSpeed = 0; //hover until horizontal velocity is killed
            double controlledSpeed = Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.up);
            double speedError = desiredSpeed - controlledSpeed;
            double speedCorrectionTimeConstant = 0.3;
            double desiredAccel = speedError / speedCorrectionTimeConstant;
            double minAccel = -vesselState.localg;
            double maxAccel = -vesselState.localg + Vector3d.Dot(vesselState.forward, vesselState.up) * vesselState.maxThrustAccel;
            if (maxAccel - minAccel > 0)
            {
                s.mainThrottle = Mathf.Clamp((float)((desiredAccel - minAccel) / (maxAccel - minAccel)), 0.0F, 1.0F);
            }
            else
            {
                s.mainThrottle = 0;
            }

            //angle up and slightly away from vertical:
            Vector3d desiredThrustVector = (vesselState.up + 0.2 * Vector3d.Exclude(vesselState.up, vesselState.forward).normalized).normalized;
            core.attitudeTo(desiredThrustVector, MechJebCore.AttitudeReference.INERTIAL, this);
        }

        void driveFinalDescent(FlightCtrlState s)
        {
            //hand off to core's landing mode
            if (part.Landed || part.Splashed)
            {
                turnOffSteering();
                core.controlRelease(this);
                landStep = LandStep.LANDED;
                return;
            }


            if (!core.trans_land)
            {
                core.landActivate(this);
            }
        }

        void driveDecelerationBurn(FlightCtrlState s)
        {
            //we aren't right next to the ground but our speed is near or above the nominal deceleration speed
            //for the current altitude

            if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) TimeWarp.SetRate(1);

            //for atmosphere landings, let the air decelerate us
            if (part.vessel.mainBody.maxAtmosphereAltitude > 0)
            {
                s.mainThrottle = 0;
                core.attitudeTo(Vector3.back, MechJebCore.AttitudeReference.SURFACE_VELOCITY, this);

                //skip deceleration burn and kill-horizontal-velocity for kerbin landings
                if (vesselState.altitudeTrue < 2000) landStep = LandStep.FINAL_DESCENT;
                return;
            }

            if (vesselState.altitudeASL < decelerationEndAltitudeASL)
            {
                landStep = LandStep.KILLING_HORIZONTAL_VELOCITY;
                s.mainThrottle = 0;
                core.attitudeTo(Vector3.up, MechJebCore.AttitudeReference.SURFACE_NORTH, this);
                return;
            }

            Vector3d desiredThrustVector;
            if (Vector3d.Dot(vesselState.velocityVesselSurfaceUnit, vesselState.up) > 0.5)
            {
                //velocity is close to directly upward. point up
                desiredThrustVector = vesselState.up;
            }
            else if (Vector3d.Dot(vesselState.velocityVesselSurfaceUnit, vesselState.up) > 0)
            {
                //velocity is upward but not directly upward. point at 45 degrees, leaning retrograde
                desiredThrustVector = (vesselState.up + Vector3d.Exclude(vesselState.up, -vesselState.velocityVesselSurfaceUnit).normalized).normalized;
            }
            else
            {
                //velocity is downward
                desiredThrustVector = -vesselState.velocityVesselSurfaceUnit;
            }


            if (velocityCorrectionSet)
            {
                double velACorrection = Vector3d.Dot(velAUnit, velocityCorrection);
                double velACorrectionAngle = velACorrection / (vesselState.maxThrustAccel * 5.0); //1 second time constant at max thrust
                velACorrectionAngle = Mathf.Clamp((float)velACorrectionAngle, -0.10F, 0.10F);
                double velBCorrection = Vector3d.Dot(velBUnit, velocityCorrection);
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
                double controlledSpeed = vesselState.speedSurface * Math.Sign(Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.up)); //positive if we are ascending, negative if descending
                double desiredSpeed = -decelerationSpeed(vesselState.altitudeASL, vesselState.maxThrustAccel, part.vessel.mainBody);
                double desiredSpeedAfterDt = -decelerationSpeed(vesselState.altitudeASL + vesselState.deltaT * Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.up), vesselState.maxThrustAccel, part.vessel.mainBody);
                double minAccel = -vesselState.localg * Math.Abs(Vector3d.Dot(vesselState.velocityVesselSurfaceUnit, vesselState.up));
                double maxAccel = vesselState.maxThrustAccel * Vector3d.Dot(vesselState.forward, -vesselState.velocityVesselSurfaceUnit) - vesselState.localg * Math.Abs(Vector3d.Dot(vesselState.velocityVesselSurfaceUnit, vesselState.up));
                double speedCorrectionTimeConstant = 0.3;
                double speedError = desiredSpeed - controlledSpeed;
                double desiredAccel = speedError / speedCorrectionTimeConstant + (desiredSpeedAfterDt - desiredSpeed) / vesselState.deltaT;
                if (maxAccel - minAccel > 0) s.mainThrottle = Mathf.Clamp((float)((desiredAccel - minAccel) / (maxAccel - minAccel)), 0.0F, 1.0F);
                else s.mainThrottle = 0;
            }

            core.attitudeTo(desiredThrustVector, MechJebCore.AttitudeReference.INERTIAL, this);

            if (velocityCorrectionSet)
            {
                Vector3d velocityNormalThrustAccel = Vector3d.Exclude(vesselState.velocityVesselSurface, vesselState.thrustAccel(s.mainThrottle) * vesselState.forward);
                velocityCorrection -= vesselState.deltaT * velocityNormalThrustAccel;
            }
        }

        bool courseCorrectionsAllowed()
        {
            return (vesselState.speedSurface < 0.8 * decelerationSpeed(vesselState.altitudeASL, vesselState.maxThrustAccel, part.vessel.mainBody)
                    && vesselState.altitudeASL > 10000);
        }

        double courseCorrectionsAccuracyTargetKm()
        {
            if (part.vessel.mainBody.maxAtmosphereAltitude > 0)
            {
                //kerbin landings
                if (vesselState.altitudeASL > part.vessel.mainBody.maxAtmosphereAltitude) return 1.0;
                else return 0.3;
            }
            else
            {
                //munar landings
                return 0.3;
            }
        }

        //////////////////////////////
        // VESSEL HISTORY UPDATES ////
        //////////////////////////////



        public override void onPartFixedUpdate()
        {

            if (!enabled) return;

            decelerationEndAltitudeASL = chooseDecelerationEndAltitudeASL();

            runSimulations();

        }


        void runSimulations()
        {
            if (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate &&
                    (nextSimulationDelayMs == 0 || nextSimulationTimer.ElapsedMilliseconds > nextSimulationDelayMs))
            {
                Stopwatch s = Stopwatch.StartNew();
                prediction = simulateReentryRK4(simulationTimeStep, Vector3d.zero);
                s.Stop();
                nextSimulationDelayMs = 10 * s.ElapsedMilliseconds;
                nextSimulationTimer.Reset();
                nextSimulationTimer.Start();

                if (autoLandAtTarget &&
                    (landStep == LandStep.COURSE_CORRECTIONS || landStep == LandStep.ON_COURSE || landStep == LandStep.DECELERATING))
                {
                    if (!velocityCorrectionSet) simCounter = 0;

                    if ((simCounter % 25) == 0)
                    {
                        if (courseCorrectionsAllowed())
                        {
                            velAUnit = chooseDownrangeVelocityVector();
                        }
                        else
                        {
                            velAUnit = vesselState.upNormalToVelSurface;
                        }
                        velBUnit = vesselState.leftSurface;
                    }
                    if ((simCounter % 5) == 0) computeVelocityCorrection();
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




        double chooseDecelerationEndAltitudeASL()
        {
            if (part.vessel.mainBody.maxAtmosphereAltitude > 0)
            {
                double groundAltitude = vesselState.altitudeASL - Math.Min(vesselState.altitudeTrue, vesselState.altitudeASL);
                return groundAltitude + 500;
            }
            else
            {
                return 5000.0;
            }
        }

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
                return 999999999.9;
            }
            else
            {
                tooLittleThrustToLand = false;
            }

            if (altitudeASL < decelerationEndAltitudeASL) return 999999999.9; //this shouldn't happen

            return 0.8 * Math.Sqrt(2 * (thrustAcc - surfaceGravity) * (altitudeASL - (decelerationEndAltitudeASL - 5)));
        }



        ///////////////////////////////////////
        // REENTRY SIMULATION /////////////////
        ///////////////////////////////////////





        //figure out what we wish our velocity were right now if we want to land at the user's target
        //we do this by first doing a pair of hypothetical simulations, which we have extra velocity in
        //two different orthogonal directions. By observing how this affects the landing site we compute the change in our velocity
        //necessary to produce the desired change in our predicted landing location
        void computeVelocityCorrection()
        {
            double offsetMagnitude = -5.0;

            //simulate a trajectory where we have extra velocity in the A direction
            LandingPrediction resultA = simulateReentryRK4(simulationTimeStep * 10.0, offsetMagnitude * velAUnit);
            //if simulation doesn't hit the ground, we should burn retrograde until it does. set a goal that will cause us to deorbit
            if (resultA.outcome != LandingPrediction.Outcome.LANDED)
            {
                if (landStep == LandStep.COURSE_CORRECTIONS && resultA.outcome == LandingPrediction.Outcome.NO_REENTRY)
                {
                    gaveUpOnCourseCorrections = true;
                    turnOffSteering();
                    core.controlRelease(this);
                    print("A: gaveup, end condition = " + resultA.outcome);
                }
                velocityCorrection = -10 * vesselState.velocityVesselSurfaceUnit;
                return;
            }

            //simulate a trajectory where we have extra velocity in the B direction
            LandingPrediction resultB = simulateReentryRK4(simulationTimeStep * 10.0, offsetMagnitude * velBUnit);
            if (resultB.outcome != LandingPrediction.Outcome.LANDED)
            {
                if (landStep == LandStep.COURSE_CORRECTIONS && resultB.outcome == LandingPrediction.Outcome.NO_REENTRY)
                {
                    gaveUpOnCourseCorrections = true;
                    turnOffSteering();
                    core.controlRelease(this);
                    print("B: gaveup, end condition = " + resultB.outcome);
                }
                velocityCorrection = -10 * vesselState.velocityVesselSurfaceUnit;
                return;
            }

            //simulate a trajectory with our current velocity
            LandingPrediction resultCurrent = simulateReentryRK4(simulationTimeStep * 10.0, Vector3d.zero);
            if (resultCurrent.outcome != LandingPrediction.Outcome.LANDED)
            {
                if (landStep == LandStep.COURSE_CORRECTIONS && resultCurrent.outcome == LandingPrediction.Outcome.NO_REENTRY)
                {
                    gaveUpOnCourseCorrections = true;
                    turnOffSteering();
                    core.controlRelease(this);
                    print("C: gaveup, end condition = " + resultCurrent.outcome);
                }
                velocityCorrection = -10 * vesselState.velocityVesselSurfaceUnit;
                return;
            }

            Vector2d sepA = latLonSeparation(resultCurrent.landingLatitude, resultCurrent.landingLongitude,
                                             resultA.landingLatitude, resultA.landingLongitude) / offsetMagnitude;

            Vector2d sepB = latLonSeparation(resultCurrent.landingLatitude, resultCurrent.landingLongitude,
                                 resultB.landingLatitude, resultB.landingLongitude) / offsetMagnitude;

            //construct a matrix that, when multiplied by a velocity offset vector2, gives the resulting offset in the landing position
            Matrix2x2 partialsMatrix = new Matrix2x2(sepA.x, sepB.x, sepA.y, sepB.y);

            //invert this to get a matrix that tells how to change our velocity to produce a 
            //give change in our landing site:
            Matrix2x2 inversePartialsMatrix = partialsMatrix.inverse();

            Vector2d sepDesired = latLonSeparation(prediction.landingLatitude, prediction.landingLongitude, targetLatitude, targetLongitude);

            Vector2d correctionMagnitudes = inversePartialsMatrix.times(sepDesired);

            velocityCorrection = correctionMagnitudes.x * velAUnit + correctionMagnitudes.y * velBUnit;
            velocityCorrectionSet = true;
        }


        //When doing course corrections, if we thrust left or right we will shift our landing site left or right.
        //However burn prograde and up will both shift our landing site away from us, and retrograde and down
        //will both shift our landing site toward us. Which of prograde and up is more efficient depends on the situation
        //This function tries to pick a thrust vector in the prograde-up plane that will be most effective in shifting the 
        //landing site toward or away from us.
        Vector3d chooseDownrangeVelocityVector()
        {
            double offsetMagnitude = -5.0;

            LandingPrediction resultPrograde = simulateReentryRK4(simulationTimeStep * 10.0, offsetMagnitude * vesselState.velocityVesselSurfaceUnit);
            //if the simulated trajectory doesn't land, retrograde is probably the best choice
            if (resultPrograde.outcome != LandingPrediction.Outcome.LANDED)
            {
                if (landStep == LandStep.COURSE_CORRECTIONS && resultPrograde.outcome == LandingPrediction.Outcome.NO_REENTRY)
                {
                    gaveUpOnCourseCorrections = true;
                    turnOffSteering();
                    core.controlRelease(this);
                    print("D: gaveup, end condition = " + resultPrograde.outcome);
                }
                return vesselState.velocityVesselSurfaceUnit;
            }

            LandingPrediction resultUp = simulateReentryRK4(simulationTimeStep * 10.0, offsetMagnitude * vesselState.upNormalToVelSurface);
            //if the simulated trajectory doesn't land, retrograde is probably the best choice
            if (resultUp.outcome != LandingPrediction.Outcome.LANDED)
            {
                if (landStep == LandStep.COURSE_CORRECTIONS && resultUp.outcome == LandingPrediction.Outcome.NO_REENTRY)
                {
                    gaveUpOnCourseCorrections = true;
                    turnOffSteering();
                    core.controlRelease(this);
                    print("E: gaveup, end condition = " + resultUp.outcome);
                }
                return vesselState.velocityVesselSurfaceUnit;
            }

            LandingPrediction resultCurrent = simulateReentryRK4(simulationTimeStep * 10.0, Vector3d.zero);
            if (resultCurrent.outcome != LandingPrediction.Outcome.LANDED)
            {
                if (landStep == LandStep.COURSE_CORRECTIONS && resultCurrent.outcome == LandingPrediction.Outcome.NO_REENTRY)
                {
                    gaveUpOnCourseCorrections = true;
                    turnOffSteering();
                    core.controlRelease(this);
                    print("F: gaveup, end condition = " + resultCurrent.outcome);
                }
                return vesselState.velocityVesselSurfaceUnit;
            }

            Vector2d sepUp = latLonSeparation(resultCurrent.landingLatitude, resultCurrent.landingLongitude,
                                              resultUp.landingLatitude, resultUp.landingLongitude) / offsetMagnitude;
            Vector2d sepPrograde = latLonSeparation(resultCurrent.landingLatitude, resultCurrent.landingLongitude,
                                                    resultPrograde.landingLatitude, resultPrograde.landingLongitude) / offsetMagnitude;


            //now we get the most effective thrust vector by taking a linear combination of these two possibilities weighted
            //by the magnitude of their effect on the landing location
            return (sepPrograde.magnitude * vesselState.velocityVesselSurfaceUnit + sepUp.magnitude * vesselState.upNormalToVelSurface).normalized;
        }


        //Turns the given latitude and longitude into points on the unit sphere,
        //then rotates the unit sphere so the lat1, lon1 is at the north pole,
        //then projects the point given by lat2, lon2 onto the equatorial plane.
        //This gives a Vector2 that represents the separation between the two points
        //that is well-behaved even when one or both points are near the poles.
        Vector2d latLonSeparation(double lat1, double lon1, double lat2, double lon2)
        {
            Vector3d unit2 = QuaternionD.AngleAxis(lon2, Vector3d.up) * QuaternionD.AngleAxis(90 - lat2, Vector3d.forward) * Vector3d.up;
            Vector3d rotUnit2 = QuaternionD.AngleAxis(-(90 - lat1), Vector3d.forward) * QuaternionD.AngleAxis(-lon1, Vector3d.up) * unit2;
            return new Vector2d(rotUnit2.x, rotUnit2.z);
        }




        //predict the reentry trajectory. uses the fourth order Runge-Kutta numerical integration scheme
        protected LandingPrediction simulateReentryRK4(double dt, Vector3d velOffset)
        {
            LandingPrediction result = new LandingPrediction();

            //should change this to also account for hyperbolic orbits where we have passed periapsis
            if (part.vessel.orbit.PeA > part.vessel.mainBody.maxAtmosphereAltitude)
            {
                result.outcome = LandingPrediction.Outcome.NO_REENTRY;
                return result;
            }

            //double groundAltitude = vesselState.altitudeASL - measureAltitudeAboveSurface(vesselState.CoM, part.vessel.mainBody);

            //use the known orbit in vacuum to find the position and velocity when we first hit the atmosphere 
            //or start decelerating with thrust:
            AROrbit freefallTrajectory = new AROrbit(vesselState.CoM, vesselState.velocityVesselOrbit + velOffset, vesselState.time, part.vessel.mainBody);
            double initialT = projectFreefallEndTime(freefallTrajectory);

            //a hack to detect improperly initialized stuff and not try to do the simulation
            if (double.IsNaN(initialT))
            {
                result.outcome = LandingPrediction.Outcome.NO_REENTRY;
                return result;
            }

            Vector3d pos = freefallTrajectory.positionAtTime(initialT);
            Vector3d vel = freefallTrajectory.velocityAtTime(initialT);
            double initialAltitude = FlightGlobals.getAltitudeAtPos(pos);
            Vector3d initialPos = new Vector3d(pos.x, pos.y, pos.z);
            Vector3d initialVel = new Vector3d(vel.x, vel.y, vel.z); ;

            double dragCoeffOverMass = vesselState.massDrag / vesselState.mass;

            //now integrate the equations of motion until we hit the surface or we exit the atmosphere again:
            double t = initialT;
            result.maxGees = 0;
            bool beenInAtmosphere = false;
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

                if (altitudeASL < part.vessel.mainBody.maxAtmosphereAltitude) beenInAtmosphere = true;

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
                result.maxGees = Math.Max(dragAccel / 9.81, result.maxGees);


                //detect landing
                if (altitudeASL < decelerationEndAltitudeASL)
                {
                    result.outcome = LandingPrediction.Outcome.LANDED;
                    result.landingLatitude = part.vessel.mainBody.GetLatitude(pos);
                    result.landingLongitude = part.vessel.mainBody.GetLongitude(pos);
                    result.landingLongitude -= (t - vesselState.time) * (360.0 / (part.vessel.mainBody.rotationPeriod)); //correct for planet rotation (360 degrees every 6 hours)
                    result.landingLongitude = clampDegrees(result.landingLongitude);
                    result.landingTime = t;
                    return result;
                }

                //detect the end of an aerobraking pass
                if (beenInAtmosphere
                    && part.vessel.mainBody.maxAtmosphereAltitude > 0
                    && altitudeASL > part.vessel.mainBody.maxAtmosphereAltitude + 1000
                    && Vector3d.Dot(vel, pos - part.vessel.mainBody.position) > 0)
                {
                    result.outcome = LandingPrediction.Outcome.AEROBRAKED;
                    result.aerobrakeApoapsis = ARUtils.computeApoapsis(pos, vel, part.vessel.mainBody);
                    result.aerobrakePeriapsis = ARUtils.computePeriapsis(pos, vel, part.vessel.mainBody);
                    return result;
                }

                //Don't tie up the CPU by running forever. Some real reentry trajectories will time out, but that's
                //just too bad. It's possible to spend an arbitarily long time in the atmosphere before landing.
                //For example, a circular orbit just inside the edge of the atmosphere will decay
                //extremely slowly. So there's no reasonable timeout setting here that will simulate all valid reentry trajectories
                //to their conclusion.
                if (t > initialT + 1000)
                {
                    result.outcome = LandingPrediction.Outcome.TIMED_OUT;
                    print("MechJeb landing simulation timed out:");
                    print("current ship altitude ASL = " + vesselState.altitudeASL);
                    print("freefall end altitude ASL = " + initialAltitude);
                    print("freefall end position = " + initialPos);
                    print("freefall end vel = " + initialVel);
                    print("timeout position = " + pos);
                    print("timeout altitude ASL = " + altitudeASL);
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

            //check if we are already in the atmosphere or below the deceleration burn end altitude
            if (currentAltitude < part.vessel.mainBody.maxAtmosphereAltitude || currentAltitude < decelerationEndAltitudeASL)
            {
                return vesselState.time;
            }

            Vector3d currentOrbitVelocity = offsetOrbit.velocityAtTime(vesselState.time);
            Vector3d currentSurfaceVelocity = currentOrbitVelocity - part.vessel.mainBody.getRFrmVel(currentPosition);

            //check if we already should be decelerating or will be momentarily:
            //that is, if our velocity is downward and our speed is close to the nominal deceleration speed
            if (Vector3d.Dot(currentSurfaceVelocity, currentPosition - part.vessel.mainBody.position) < 0
                && currentSurfaceVelocity.magnitude > 0.9 * decelerationSpeed(currentAltitude, vesselState.maxThrustAccel, part.vessel.mainBody))
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

                if (Vector3d.Dot(testSurfaceVelocity, testPosition - part.vessel.mainBody.position) < 0 &&
                    (testAltitude < part.vessel.mainBody.maxAtmosphereAltitude
                     || testAltitude < decelerationEndAltitudeASL
                     || testSpeed > 0.9 * decelerationSpeed(testAltitude, vesselState.maxThrustAccel, part.vessel.mainBody)))
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

    }

    class LandingPrediction
    {
        public enum Outcome { LANDED, AEROBRAKED, TIMED_OUT, NO_REENTRY }
        public Outcome outcome;
        public double landingLatitude = 0;
        public double landingLongitude = 0;
        public double landingTime = 0;
        public double aerobrakeApoapsis;
        public double aerobrakePeriapsis;
        public double maxGees = 0;
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