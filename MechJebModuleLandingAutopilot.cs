using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using UnityEngine;

/*
 * Todo:
 * 
 * -fix exception when LAND touches down while Translatron window is open
 * 
 * -fix any bugs in LAND
 * 
 * Future:
 * -investigate and fix remaining failures of kepler solver to converge for high ecc orbits (managed to hang game activating land at target after a short hop up from munar surface)
 * -for mun landings, replace "predicted landing location" with "predicted impact site" when LAND-at-target isn't active     
 * -save coordinates by body
 * -fix remaining pole problems
 * -figure out exactly where the bottom of the ship is for more controlled landings
 * -put main simulation in a separate thread
 * -display a target icon showing the landed target in the map view
 * 
 * Changed:
 * 
 * -can specify touchdown speed
 * -smoother, safer landings since surface height is known well in advance
 * -displays coordinates when you mouse over surface in map view
 * -can click to select target location in map view
 * -added automatic deorbit burn
 * -solved some problems with high-ecc orbits
 * -in orbit info panel fixed incorrect time to Pe for hyperbolic orbits with timewarp > 2x
 * -will continue trajectories that go exoatmospheric as long as Pe < 0, to enable targeted suborbital hops
 * 
 */


namespace MuMech
{


    public class MechJebModuleLandingAutopilot : ComputerModule
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
            if (autoLand || autoLandAtTarget) core.controlRelease(this);
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
            }
            if (autoLand)
            {
                autoLand = false;
            }
            if (core.controlModule == this)
            {
                core.attitudeDeactivate(this);
                FlightInputHandler.SetNeutralControls();

                if (core.trans_land) core.landDeactivate(this);
            }
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

        bool _dmsInput = true;
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

        private double _touchdownSpeed = 0.5;
        String touchdownSpeedString;
        public double touchdownSpeed
        {
            get { return _touchdownSpeed; }
            set
            {
                if (value != _touchdownSpeed) core.settingsChanged = true;
                _touchdownSpeed = value;
            }
        }

        public override void onLoadGlobalSettings(SettingsManager settings)
        {
            base.onLoadGlobalSettings(settings);

            targetLatitude = settings["RC_targetLatitude"].valueDecimal(KSC_LATITUDE);
            targetLongitude = settings["RC_targetLongitude"].valueDecimal(KSC_LONGITUDE);
            dmsInput = settings["RC_dmsInput"].valueBool(true);
            initializeDecimalStrings();
            initializeDMSStrings();
            touchdownSpeed = settings["RC_touchdownSpeed"].valueDecimal(0.5);
            touchdownSpeedString = touchdownSpeed.ToString();

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
            settings["RC_touchdownSpeed"].value_decimal = touchdownSpeed;
        }

        public enum LandStep { DEORBIT_BURN, ON_COURSE, COURSE_CORRECTIONS, DECELERATING, KILLING_HORIZONTAL_VELOCITY, FINAL_DESCENT, LANDED };
        LandStep landStep;
        String landStatusString = "";

        //simulation stuff:
        const double simulationTimeStep = 0.2; //seconds
        int simCounter = 0;
        public LandingPrediction prediction = new LandingPrediction();
        long nextSimulationDelayMs = 0;
        Stopwatch nextSimulationTimer = new Stopwatch();

        Vector3d velAUnit;
        Vector3d velBUnit;
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

        bool gettingMapTarget = false;

        public bool autoLandAtTarget = false;
        bool autoLand = false;
        bool deorbiting = false;
        bool gaveUpOnCourseCorrections = false;
        bool tooLittleThrustToLand = false;
        bool deorbitBurnFirstMessage = false;
        double decelerationEndAltitudeASL = 2500.0;

        MechJebModuleOrbitOper orbitOper;

        PlanetariumCamera planetariumCamera;

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

            if (MapView.MapIsEnabled && gettingMapTarget)
            {

                GUI.Label(new Rect(Screen.width * 3 / 8, Screen.height / 10, Screen.width / 4, 5000),
                          "Select the target landing site by clicking anywhere on " + part.vessel.mainBody.name + "'s surface",
                          ARUtils.labelStyle(Color.yellow));

                if (!windowPos.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                {
                    double mouseLatitude;
                    double mouseLongitude;
                    if (getMouseCoordinates(out mouseLatitude, out mouseLongitude))
                    {
                        String mouseCoordString;
                        if (dmsInput) mouseCoordString = dmsLocationString(mouseLatitude, mouseLongitude, true);
                        else mouseCoordString = String.Format("{0:0.000}° N\n {1:0.000}° E", mouseLatitude, mouseLongitude);
                        GUILayout.Label(mouseCoordString);

                        GUI.Label(new Rect(Input.mousePosition.x + 15, Screen.height - Input.mousePosition.y, 500, 500), mouseCoordString, ARUtils.labelStyle(Color.yellow));
                    }
                }
            }


        }

        protected bool getMouseCoordinates(out double mouseLatitude, out double mouseLongitude)
        {
            Ray mouseRay = planetariumCamera.camera.ScreenPointToRay(Input.mousePosition);
            mouseRay.origin = mouseRay.origin / Planetarium.InverseScaleFactor;
            Vector3d relOrigin = mouseRay.origin - part.vessel.mainBody.position;
            Vector3d relSurfacePosition;
            if (PQS.LineSphereIntersection(relOrigin, mouseRay.direction, part.vessel.mainBody.Radius, out relSurfacePosition))
            {
                Vector3d surfacePoint = part.vessel.mainBody.position + relSurfacePosition;
                mouseLatitude = part.vessel.mainBody.GetLatitude(surfacePoint);
                mouseLongitude = ARUtils.clampDegrees(part.vessel.mainBody.GetLongitude(surfacePoint));
                return true;
            }
            else
            {
                mouseLatitude = 0;
                mouseLongitude = 0;
                return false;
            }
        }



        public override void onPartUpdate()
        {
            if (!enabled) return;

            if (MapView.MapIsEnabled && gettingMapTarget && !windowPos.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
            {
                if (Input.GetMouseButtonDown(0))
                {
                    double mouseLatitude;
                    double mouseLongitude;
                    if (getMouseCoordinates(out mouseLatitude, out mouseLongitude))
                    {
                        targetLatitude = mouseLatitude;
                        targetLongitude = mouseLongitude;
                        initializeDecimalStrings();
                        initializeDMSStrings();
                        gettingMapTarget = false;
                    }
                }
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

                GUILayout.Label("Target:", labelStyle, GUILayout.Width(40.0F));
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
                GUIStyle compassToggleStyle = new GUIStyle(GUI.skin.button);
                compassToggleStyle.padding.bottom = compassToggleStyle.padding.top = 3;
                //                compassToggleStyle.padding.left = 3;

                GUILayout.Label("Target:", labelStyle, GUILayout.Width(40));
                targetLatitudeDegString = GUILayout.TextField(targetLatitudeDegString, GUILayout.Width(35));
                GUILayout.Label("°");
                targetLatitudeMinString = GUILayout.TextField(targetLatitudeMinString, GUILayout.Width(20));
                GUILayout.Label("'");
                targetLatitudeSecString = GUILayout.TextField(targetLatitudeSecString, GUILayout.Width(20));
                GUILayout.Label("''");
                if (dmsNorth)
                {
                    if (GUILayout.Button("N", compassToggleStyle, GUILayout.Width(25.0F))) dmsNorth = false;
                }
                else
                {
                    if (GUILayout.Button("S", compassToggleStyle, GUILayout.Width(25.0F))) dmsNorth = true;
                }

                targetLongitudeDegString = GUILayout.TextField(targetLongitudeDegString, GUILayout.Width(35));
                GUILayout.Label("°");
                targetLongitudeMinString = GUILayout.TextField(targetLongitudeMinString, GUILayout.Width(20));
                GUILayout.Label("'");
                targetLongitudeSecString = GUILayout.TextField(targetLongitudeSecString, GUILayout.Width(20));
                GUILayout.Label("''");
                if (dmsEast)
                {
                    if (GUILayout.Button("E", compassToggleStyle, GUILayout.Width(25.0F))) dmsEast = false;
                }
                else
                {
                    if (GUILayout.Button("W", compassToggleStyle, GUILayout.Width(25.0F))) dmsEast = true;
                }
            }


            GUILayout.EndHorizontal();


            GUIStyle normalStyle = new GUIStyle(GUI.skin.button);
            GUIStyle greenStyle = ARUtils.buttonStyle(Color.green);


            GUILayout.BeginHorizontal();

            if (gettingMapTarget && !MapView.MapIsEnabled) gettingMapTarget = false;
            GUI.SetNextControlName("MapTargetToggle");
            gettingMapTarget = GUILayout.Toggle(gettingMapTarget, "Select target on map", (gettingMapTarget ? greenStyle : normalStyle));
            if (gettingMapTarget && !MapView.MapIsEnabled) MapView.EnterMapView();

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

            GUILayout.EndHorizontal();




            switch (prediction.outcome)
            {
                case LandingPrediction.Outcome.LANDED:
                    if (part.vessel.Landed || part.vessel.Splashed
                        || part.vessel.mainBody.maxAtmosphereAltitude > 0 || autoLandAtTarget)
                    {
                        String locationString;
                        if (part.vessel.Landed) locationString = "Landed at ";
                        else if (part.vessel.Splashed) locationString = "Splashed down at ";
                        else locationString = "Predicted landing site: ";

                        if (dmsInput) locationString += dmsLocationString(prediction.landingLatitude, prediction.landingLongitude);
                        else locationString += String.Format("{0:0.000}° N, {1:0.000}° E", prediction.landingLatitude, prediction.landingLongitude);

                        GUILayout.Label(locationString);

                        double eastError = eastErrorKm(prediction.landingLongitude, targetLongitude, targetLatitude);
                        double northError = northErrorKm(prediction.landingLatitude, targetLatitude);
                        GUILayout.Label(String.Format("{1:0.0} km " + (northError > 0 ? "north" : "south") +
                            ", {0:0.0} km " + (eastError > 0 ? "east" : "west") + " of target", Math.Abs(eastError), Math.Abs(northError)));
                    }
                    //double currentLatitude = part.vessel.mainBody.GetLatitude(vesselState.CoM);
                    //double currentLongitude = part.vessel.mainBody.GetLongitude(vesselState.CoM);
                    //if (dmsInput) GUILayout.Label("Current coordinates: " + dmsLocationString(currentLatitude, currentLongitude));
                    //else GUILayout.Label(String.Format("Current coordinates: {0:0.000}° N, {1:0.000}° E", currentLatitude, ARUtils.clampDegrees(currentLongitude)));
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


            bool newAutoLand = GUILayout.Toggle(autoLand, "LAND", (autoLand ? greenStyle : normalStyle));
            if (newAutoLand && !autoLand)
            {
                autoLandAtTarget = false;
                core.controlClaim(this);
                core.landActivate(this, touchdownSpeed);
                FlightInputHandler.SetNeutralControls();
            }
            else if (!newAutoLand && autoLand)
            {
                core.landDeactivate(this);
                core.controlRelease(this);
            }
            autoLand = newAutoLand;

            bool newAutoLandAtTarget = GUILayout.Toggle(autoLandAtTarget, "LAND at target", (autoLandAtTarget ? greenStyle : normalStyle));

            if (newAutoLandAtTarget && !autoLandAtTarget)
            {
                deorbitBurnFirstMessage = false;
                gaveUpOnCourseCorrections = false;
                core.controlClaim(this);
                core.landDeactivate(this);

                if (part.vessel.orbit.PeA > -0.1 * part.vessel.mainBody.Radius) landStep = LandStep.DEORBIT_BURN;
                else landStep = LandStep.COURSE_CORRECTIONS;

                FlightInputHandler.SetNeutralControls();
            }
            else if (!newAutoLandAtTarget && autoLandAtTarget)
            {
                turnOffSteering();
                core.controlRelease(this);
            }
            autoLandAtTarget = newAutoLandAtTarget;


            showHelpWindow = GUILayout.Toggle(showHelpWindow, "?", new GUIStyle(GUI.skin.button));

            GUILayout.EndHorizontal();


            if (autoLandAtTarget)
            {
                GUILayout.Label("Landing step: " + landStatusString);
            }

            GUIStyle yellow = ARUtils.labelStyle(Color.yellow);
            if (gaveUpOnCourseCorrections) GUILayout.Label("Attempted course corrections made the trajectory no longer land. Try a bigger deorbit burn. Or is the target on the wrong side of " + part.vessel.mainBody.name + "?", yellow);
            if (tooLittleThrustToLand) GUILayout.Label("Warning: Too little thrust to land.", yellow);
            if (deorbitBurnFirstMessage) GUILayout.Label("You must do a deorbit burn before activating \"LAND at target\"", yellow);


            GUILayout.BeginHorizontal();

            touchdownSpeed = ARUtils.doGUITextInput("Touchdown speed:", 200.0F, touchdownSpeedString, 50.0F, "m/s", 50.0F, out touchdownSpeedString, touchdownSpeed);
            core.trans_land_touchdown_speed = touchdownSpeed;

            GUILayout.EndHorizontal();


            GUILayout.EndVertical();

            GUI.DragWindow();
        }


        Vector2 scrollPosition;
        private void HelpWindowGUI(int windowID)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("MechJeb's landing autopilot can automatically fly you down to a landing on Kerbin or the Mun. Optionally, you may specify the coordinates to land at. The landing autopilot can also be used to predict the outcome of aerobraking maneuvers.\n\n" +
"----------Untargeted Landings----------\n\n" +
"If you want to do a powered landing, and don't want MechJeb to aim for a specific target, hit LAND. You can activate LAND at any point in your descent. If you are still in orbit, MechJeb will do a deorbit burn. LAND will manage your speed to fly you down to a soft landing. It will try to touch down at the vertical speed specified in the \"Touchdown speed\" field.\n\n" +
"----------Targeted Landings----------\n\n" +
"MechJeb can also land a specified target coodinates. Coordinates  can be specified in two formats, decimal (DEC) and degrees-minutes-seconds (DMS), and you can switch to one or the other by pressing DEC or DMS.\n\n" +
"First you need to set a target. There are several ways of doing this:\n\n" +
"Entering coordinates by hand - You can type coordinates into the target coordinate text fields. For instance, if you want to land next to another vessel that has already landed, you can mouse over the ship in map view and the game will display its coordinates in DMS format. You can then enter these coordinates as your target coordinates, making sure you are in DMS mode.\n\n" +
"Point-and-click - Hit \"Select target on map\" and you'll be taken to the map view. Now you can click any point on the surface of the body you are orbiting. The point you click will become the target landing site. MechJeb will display coordinates as you mouse over the surface, and will automatically paste these into the target coordinate text fields when you click on your desired landing site.\n\n" +
"Target KSC - Hit \"Target KSC\" and MechJeb will automatically paste the coordinates of KSC on Kerbin into the target coordinate text fields.\n\n" +
"Once you have set your landing target, hit \"LAND at target\" and MechJeb will initiate a completely automatic targeted descent to land at the target. The descent proceeds in several stages:\n\n" +
"Deorbit burn - If you are still in orbit, MechJeb will first warp to the appropriate point in the orbit and perform a deorbit burn.\n\n" +
"Course correction - MechJeb will do a correction burn culminating in fine adjustments to the trajectory so that it's aimed precisely at the target.\n\n" +
"On course for target - Once the trajectory is good enough, MechJeb will display the status \"On course for target (safe to warp)\". As indicated, it's now safe to increase time warp as much as you like to speed the descent. MechJeb will automatically unwarp when necessary. In addition, if you are going to stage during the descent it's best to stage now, rather than during deceleration burn, as staging during the deceleration burn will throw off MechJeb's predictions and cause you to over- or under-shoot the target.\n\n" +
"Deceleration - For Munar landings the next step is a deceleration burn. MechJeb will continue to make small corrections to the trajectory during this burn. MechJeb aims for the deceleration burn to end 200m vertically above the target. At the end of the burn, the ship will briefly come to a halt before dropping down on the target. For Kerbin landings, MechJeb simply allows the atmosphere to slow down the craft during this stage.\n\n" +
"Final descent - This is the final vertical powered descent, culminating in touchdown. MechJeb will try to touch down with the vertical speed specified in the \"Touchdown speed\" input.\n\n" +
"----------Manual Targeted Landings on Kerbin----------\n\n" +
"You can use the landing autopilot's landing prediction to do a targeted landing on Kerbin manual instead of under autopilot control. When you are on a landing trajectory, MechJeb will display your predicted landing site, taking into account atmospheric drag. It will also display how far that predicted landing site is from your target. By doing manual burns to bring the predicted landing site to the target landing site you can make sure you land at your target.\n\n" +
"MechJeb will not display a predicted landing site on the Mun unless the \"LAND at target\" autopilot mode is active, so it won't help you do manual targeted landings on the Mun. This is because your landing site is highly dependent on the exact details of your deceleration burn, and MechJeb cannot predict how you will perform the deceleration burn.\n\n" +
"----------Aerobraking predictions----------\n\n" +
"If you are on an aerobraking trajectory--one that passes through the atmosphere but does not land--the landing autopilot will show a prediction of what your orbit (apoapsis and periapsis) will be after you exit the atmosphere. This lets you perform accurate aerobraking maneuvers."
);
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

            targetLongitude = ARUtils.clampDegrees(targetLongitude);
            int targetLongitudeDeg = (int)Math.Floor(Math.Abs(targetLongitude));
            int targetLongitudeMin = (int)Math.Floor(60 * (Math.Abs(targetLongitude) - targetLongitudeDeg));
            int targetLongitudeSec = (int)Math.Floor(3600 * (Math.Abs(targetLongitude) - targetLongitudeDeg - targetLongitudeMin / 60.0));
            targetLongitudeDegString = targetLongitudeDeg.ToString();
            targetLongitudeMinString = targetLongitudeMin.ToString();
            targetLongitudeSecString = targetLongitudeSec.ToString();
            dmsEast = (targetLongitude > 0);

        }

        String dmsLocationString(double lat, double lon, bool newline = false)
        {
            return dmsAngleString(lat) + (lat > 0 ? " N" : " S") + (newline ? "\n" : ", ")
                 + dmsAngleString(ARUtils.clampDegrees(lon)) + (ARUtils.clampDegrees(lon) > 0 ? " E" : " W");
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
            double degreeError = ARUtils.clampDegrees(predLongitude - targLongitude);
            return degreeError * Math.PI / 180 * part.vessel.mainBody.Radius * Math.Cos(targLatitude * Math.PI / 180) / 1000.0;
        }




        ///////////////////////////////////////
        // FLY BY WIRE ////////////////////////
        ///////////////////////////////////////


        public override void drive(FlightCtrlState s)
        {

            if (!autoLandAtTarget) return;

            switch (landStep)
            {
                case LandStep.DEORBIT_BURN:
                    driveDeorbitBurn(s);
                    break;

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

        void driveDeorbitBurn(FlightCtrlState s)
        {
            //compute the desired velocity after deorbiting. we aim for a trajectory that 
            // a) has the same vertical speed as our current trajectory
            // b) has a horizontal speed that will give it a periapsis of -10% of the body's radius
            // c) has a heading that points toward where the target will be at the end of free-fall, accounting for planetary rotation
            double horizontalDV = orbitOper.deltaVToChangePeriapsis(-0.1 * part.vessel.mainBody.Radius);
            horizontalDV *= Math.Sign(-0.1 * part.vessel.mainBody.Radius - part.vessel.orbit.PeA);
            Vector3d currentHorizontal = Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbit).normalized;
            Vector3d forwardDeorbitVelocity = vesselState.velocityVesselOrbit + horizontalDV * currentHorizontal;
            AROrbit forwardDeorbitTrajectory = new AROrbit(vesselState.CoM, forwardDeorbitVelocity, vesselState.time, part.vessel.mainBody);
            double freefallTime = projectFreefallEndTime(forwardDeorbitTrajectory, vesselState.time) - vesselState.time;
            double planetRotationDuringFreefall = 360 * freefallTime / part.vessel.mainBody.rotationPeriod;
            Vector3d currentTargetRadialVector = part.vessel.mainBody.GetRelSurfacePosition(targetLatitude, targetLongitude, 0);
            Quaternion freefallPlanetRotation = Quaternion.AngleAxis((float)planetRotationDuringFreefall, part.vessel.mainBody.angularVelocity);
            Vector3d freefallEndTargetRadialVector = freefallPlanetRotation * currentTargetRadialVector;
            Vector3d currentTargetPosition = part.vessel.mainBody.position + part.vessel.mainBody.Radius * part.vessel.mainBody.GetSurfaceNVector(targetLatitude, targetLongitude);
            Vector3d freefallEndTargetPosition = part.vessel.mainBody.position + freefallEndTargetRadialVector;
            Vector3d freefallEndHorizontalToTarget = Vector3d.Exclude(vesselState.up, freefallEndTargetPosition - vesselState.CoM).normalized;
            double currentHorizontalSpeed = Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbit).magnitude;
            double finalHorizontalSpeed = currentHorizontalSpeed + horizontalDV;
            double currentVerticalSpeed = Vector3d.Dot(vesselState.up, vesselState.velocityVesselOrbit);

            Vector3d currentRadialVector = vesselState.CoM - part.vessel.mainBody.position;
            Vector3d currentOrbitNormal = Vector3d.Cross(currentRadialVector, vesselState.velocityVesselOrbit);
            double targetAngleToOrbitNormal = Math.Abs(Vector3d.Angle(currentOrbitNormal, freefallEndTargetRadialVector));
            targetAngleToOrbitNormal = Math.Min(targetAngleToOrbitNormal, 180 - targetAngleToOrbitNormal);

            double targetAheadAngle = Math.Abs(Vector3d.Angle(currentRadialVector, freefallEndTargetRadialVector));

            double planeChangeAngle = Math.Abs(Vector3d.Angle(currentHorizontal, freefallEndHorizontalToTarget));

            if (!deorbiting)
            {
                if (targetAngleToOrbitNormal < 10
                   || (targetAheadAngle < 90 && targetAheadAngle > 60 && planeChangeAngle < 90)) deorbiting = true;
            }

            if (deorbiting)
            {
                if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) core.warpMinimum(this);

                Vector3d desiredVelocity = finalHorizontalSpeed * freefallEndHorizontalToTarget + currentVerticalSpeed * vesselState.up;
                Vector3d velocityChange = desiredVelocity - vesselState.velocityVesselOrbit;

                if (velocityChange.magnitude < 2.0) landStep = LandStep.COURSE_CORRECTIONS;

                core.attitudeTo(velocityChange.normalized, MechJebCore.AttitudeReference.INERTIAL, this);

                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = 1.0F;
                else s.mainThrottle = 0.0F;

                landStatusString = "Executing deorbit burn";
            }
            else
            {
                //if we don't want to deorbit but we're already on a reentry trajectory, we can't wait until the ideal point 
                //in the orbit to deorbt; we already have deorbited.
                if (part.vessel.orbit.ApA < part.vessel.mainBody.maxAtmosphereAltitude)
                {
                    landStep = LandStep.COURSE_CORRECTIONS;
                }
                else
                {
                    s.mainThrottle = 0.0F;
                    core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.ORBIT, this);
                    core.warpIncrease(this);
                }

                landStatusString = "Moving to deorbit burn point";
            }
        }

        void driveCourseCorrections(FlightCtrlState s)
        {
            landStatusString = "Executing course correction";

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
            landStatusString = "On course for target (safe to warp)";

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
            landStatusString = "Killing horizontal velocity";

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
            double speedCorrectionTimeConstant = 1.0;
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
            landStatusString = "Final descent";

            //hand off to core's landing mode
            if (part.vessel.Landed || part.vessel.Splashed)
            {
                turnOffSteering();
                core.controlRelease(this);
                landStep = LandStep.LANDED;
                return;
            }


            if (!core.trans_land)
            {
                core.landActivate(this, touchdownSpeed);
            }
        }

        void driveDecelerationBurn(FlightCtrlState s)
        {
            //we aren't right next to the ground but our speed is near or above the nominal deceleration speed
            //for the current altitude

            if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) core.warpPhysics(this);

            //for atmosphere landings, let the air decelerate us
            if (part.vessel.mainBody.maxAtmosphereAltitude > 0)
            {
                landStatusString = "Decelerating";

                s.mainThrottle = 0;
                core.attitudeTo(Vector3.back, MechJebCore.AttitudeReference.SURFACE_VELOCITY, this);

                //skip deceleration burn and kill-horizontal-velocity for kerbin landings
                if (vesselState.altitudeASL < decelerationEndAltitudeASL) landStep = LandStep.FINAL_DESCENT;
                return;
            }

            //vacuum landings:

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

            if (s.mainThrottle == 0.0F) landStatusString = "Preparing for braking burn";
            else landStatusString = "Executing braking burn";
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


        public override void onFlightStart()
        {
            foreach (ComputerModule module in core.modules)
            {
                if (module is MechJebModuleOrbitOper) orbitOper = (MechJebModuleOrbitOper)module;
            }

            planetariumCamera = (PlanetariumCamera)GameObject.FindObjectOfType(typeof(PlanetariumCamera));
        }


        public override void onPartFixedUpdate()
        {
            if (!enabled) return;

            if (autoLand && !core.trans_land) //detect when core land turns itself off.
            {
                autoLand = false;
                core.controlRelease(this);
            }

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
                if (prediction != null && prediction.outcome == LandingPrediction.Outcome.LANDED)
                {
                    return 2000 + ARUtils.PQSSurfaceHeight(prediction.landingLatitude, prediction.landingLongitude, part.vessel.mainBody);
                }
                else
                {
                    return 2000 + ARUtils.PQSSurfaceHeight(targetLatitude, targetLongitude, part.vessel.mainBody);
                }
            }
            else
            {
                if (prediction != null && prediction.outcome == LandingPrediction.Outcome.LANDED)
                {
                    return 100 + ARUtils.PQSSurfaceHeight(prediction.landingLatitude, prediction.landingLongitude, part.vessel.mainBody);
                }
                else
                {
                    return 100 + ARUtils.PQSSurfaceHeight(targetLatitude, targetLongitude, part.vessel.mainBody);
                }


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

        protected LandingPrediction simulateReentryRK4(double dt, Vector3d velOffset)
        {
            return simulateReentryRK4(vesselState.CoM, vesselState.velocityVesselOrbit + velOffset, vesselState.time, dt, true);
        }


        //predict the reentry trajectory. uses the fourth order Runge-Kutta numerical integration scheme
        protected LandingPrediction simulateReentryRK4(Vector3d startPos, Vector3d startVel, double startTime, double dt, bool recurse)
        {
            LandingPrediction result = new LandingPrediction();

            //should change this to also account for hyperbolic orbits where we have passed periapsis
            //should also change this to use the orbit giv en by the parameters
            if (part.vessel.orbit.PeA > part.vessel.mainBody.maxAtmosphereAltitude)
            {
                result.outcome = LandingPrediction.Outcome.NO_REENTRY;
                return result;
            }

            //use the known orbit in vacuum to find the position and velocity when we first hit the atmosphere 
            //or start decelerating with thrust:
            AROrbit freefallTrajectory = new AROrbit(startPos, startVel, startTime, part.vessel.mainBody);
            double initialT = projectFreefallEndTime(freefallTrajectory, startTime);
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
                    result.landingLongitude = ARUtils.clampDegrees(result.landingLongitude);
                    result.landingTime = t;
                    return result;
                }

                //detect the end of an aerobraking pass
                if (beenInAtmosphere
                    && part.vessel.mainBody.maxAtmosphereAltitude > 0
                    && altitudeASL > part.vessel.mainBody.maxAtmosphereAltitude + 1000
                    && Vector3d.Dot(vel, pos - part.vessel.mainBody.position) > 0)
                {
                    if (part.vessel.orbit.PeA > 0 || !recurse)
                    {
                        result.outcome = LandingPrediction.Outcome.AEROBRAKED;
                        result.aerobrakeApoapsis = ARUtils.computeApoapsis(pos, vel, part.vessel.mainBody);
                        result.aerobrakePeriapsis = ARUtils.computePeriapsis(pos, vel, part.vessel.mainBody);
                        return result;
                    }
                    else
                    {
                        //continue suborbital trajectories to a landing
                        return simulateReentryRK4(pos, vel, t, dt, false);
                    }
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
        double projectFreefallEndTime(AROrbit offsetOrbit, double startTime)
        {
            Vector3d currentPosition = offsetOrbit.positionAtTime(startTime);
            double currentAltitude = FlightGlobals.getAltitudeAtPos(currentPosition);

            //check if we are already in the atmosphere or below the deceleration burn end altitude
            if (currentAltitude < part.vessel.mainBody.maxAtmosphereAltitude || currentAltitude < decelerationEndAltitudeASL)
            {
                return vesselState.time;
            }

            Vector3d currentOrbitVelocity = offsetOrbit.velocityAtTime(startTime);
            Vector3d currentSurfaceVelocity = currentOrbitVelocity - part.vessel.mainBody.getRFrmVel(currentPosition);

            //check if we already should be decelerating or will be momentarily:
            //that is, if our velocity is downward and our speed is close to the nominal deceleration speed
            if (Vector3d.Dot(currentSurfaceVelocity, currentPosition - part.vessel.mainBody.position) < 0
                && currentSurfaceVelocity.magnitude > 0.9 * decelerationSpeed(currentAltitude, vesselState.maxThrustAccel, part.vessel.mainBody))
            {
                return vesselState.time;
            }

            //check if the orbit reenters at all
            double timeToPe = offsetOrbit.timeToPeriapsis(startTime);
            Vector3d periapsisPosition = offsetOrbit.positionAtTime(startTime + timeToPe);
            if (FlightGlobals.getAltitudeAtPos(periapsisPosition) > part.vessel.mainBody.maxAtmosphereAltitude)
            {
                return startTime + timeToPe; //return the time of periapsis as a next best number
            }

            //determine time & velocity of reentry
            double minReentryTime = startTime;
            double maxReentryTime = startTime + timeToPe;
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

    public class LandingPrediction
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


    public class Matrix2x2
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