using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;



namespace MuMech
{

    class AscentAutopilot : ComputerModule
    {
        public AscentAutopilot(MechJebCore core) : base(core) { }

        public override void onModuleDisabled() {
             mode = DISENGAGED;
             FlightInputHandler.SetNeutralControls(); //makes sure we leave throttle at zero
        }


        ////////////////////////////////////
        // GLOBAL DATA /////////////////////
        ////////////////////////////////////

        //autopilot modes
        const int ON_PAD = 0, VERTICAL_ASCENT = 1, GRAVITY_TURN = 2, COAST_TO_APOAPSIS = 3, CIRCULARIZE = 4, DISENGAGED = 5;
        String[] modeStrings = new String[] { "Awaiting liftoff", "Vertical ascent", "Gravity turn", "Coasting to apoapsis", "Circularizing", "Disengaged" };
        int mode = DISENGAGED;

        //control errors
        Vector3d lastThrustError = Vector3d.zero;

        //control gains
        const double velocityKP = 5.0;
        const double thrustKP = 3.0;
        const double thrustKD = 15.0;
        double thrustGainAttenuator = 1;


        //GUI stuff
        bool minimized = false;
        bool showHelpWindow = false;
        Texture2D pathTexture = new Texture2D(400, 100);
        const int PATH_WINDOW_ID = 7790;
        const int HELP_WINDOW_ID = 7791;


        //things the drive code needs to remember between frames
        double lastAccelerationTime = 0;
        int lastAttemptedWarpIndex = 0;
        double lastStageTime = 0;


        //user inputs
        double _gravityTurnStartAltitude = 10000.0;
        double gravityTurnStartAltitude
        {
            get { return _gravityTurnStartAltitude; }
            set
            {
                if (_gravityTurnStartAltitude != value) core.settingsChanged = true;
                _gravityTurnStartAltitude = value;
            }
        }
        String gravityTurnStartAltitudeKmString = "10";

        double _gravityTurnEndAltitude = 70000.0;
        double gravityTurnEndAltitude
        {
            get { return _gravityTurnEndAltitude; }
            set
            {
                if (_gravityTurnEndAltitude != value) core.settingsChanged = true;
                _gravityTurnEndAltitude = value;
            }
        }
        String gravityTurnEndAltitudeKmString = "70";

        double _gravityTurnEndPitch = 0.0;
        double gravityTurnEndPitch
        {
            get { return _gravityTurnEndPitch; }
            set
            {
                if (_gravityTurnEndPitch != value) core.settingsChanged = true;
                _gravityTurnEndPitch = value;
            }
        }
        String gravityTurnEndPitchString = "0";

        double _gravityTurnShapeExponent = 0.4;
        double gravityTurnShapeExponent
        {
            get { return _gravityTurnShapeExponent; }
            set
            {
                if (_gravityTurnShapeExponent != value) core.settingsChanged = true;
                _gravityTurnShapeExponent = value;
            }
        }

        double _desiredOrbitRadius = 700000.0;
        double desiredOrbitRadius
        {
            get { return _desiredOrbitRadius; }
            set
            {
                if (_desiredOrbitRadius != value) core.settingsChanged = true;
                _desiredOrbitRadius = value;
            }
        }
        String desiredOrbitAltitudeKmString = "100";

        double _desiredInclination = 0.0;
        double desiredInclination
        {
            get { return _desiredInclination; }
            set
            {
                if (_desiredInclination != value) core.settingsChanged = true;
                _desiredInclination = value;
            }
        }
        String desiredInclinationString = "0";

        bool _autoStage = true;
        bool autoStage
        {
            get { return _autoStage; }
            set
            {
                if (_autoStage != value) core.settingsChanged = true;
                _autoStage = value;
            }
        }

        double _autoStageDelay = 1.0;
        double autoStageDelay
        {
            get { return _autoStageDelay; }
            set
            {
                if (_autoStageDelay != value) core.settingsChanged = true;
                _autoStageDelay = value;
            }
        }
        String autoStageDelayString = "1.0";

        int _autoStageLimit = 0;
        int autoStageLimit
        {
            get { return _autoStageLimit; }
            set
            {
                if (_autoStageLimit != value) core.settingsChanged = true;
                _autoStageLimit = value;
            }
        }
        String autoStageLimitString = "0";

        bool _autoWarpToApoapsis = true;
        bool autoWarpToApoapsis
        {
            get { return _autoWarpToApoapsis; }
            set
            {
                if (_autoWarpToApoapsis != value) core.settingsChanged = true;
                _autoWarpToApoapsis = value;
            }
        }
        
        bool _seizeThrottle = true;
        bool seizeThrottle
        {
            get { return _seizeThrottle; }
            set
            {
                if (_seizeThrottle != value) core.settingsChanged = true;
                _seizeThrottle = value;
            }
        }

        Rect _pathWindowPos;
        Rect pathWindowPos
        {
            get { return _pathWindowPos; }
            set
            {
                if (_pathWindowPos.x != value.x || _pathWindowPos.y != value.y) core.settingsChanged = true;
                _pathWindowPos = value;
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

        bool _showPathWindow = false;
        bool showPathWindow
        {
            get { return _showPathWindow; }
            set
            {
                if (_showPathWindow != value) core.settingsChanged = true;
                _showPathWindow = value;
            }
        }

        public override void onLoadGlobalSettings(SettingsManager settings)
        {
            base.onLoadGlobalSettings(settings);

            gravityTurnStartAltitude = settings["AA_gravityTurnStartAltitude"].valueDecimal(10000.0);
            gravityTurnStartAltitudeKmString = (gravityTurnStartAltitude/1000.0).ToString();
            gravityTurnEndAltitude = settings["AA_gravityTurnEndAltitude"].valueDecimal(70000.0);
            gravityTurnEndAltitudeKmString = (gravityTurnEndAltitude / 1000.0).ToString();
            gravityTurnEndPitch = settings["AA_gravityTurnEndPitch"].valueDecimal(0.0);
            gravityTurnEndPitchString = gravityTurnEndPitch.ToString();
            gravityTurnShapeExponent = settings["AA_gravityTurnShapeExponent"].valueDecimal(0.4);
            desiredOrbitRadius = settings["AA_desiredOrbitRadius"].valueDecimal(700000.0);
            desiredOrbitAltitudeKmString = ((desiredOrbitRadius - part.vessel.mainBody.Radius) / 1000.0).ToString();
            desiredInclination = settings["AA_desiredInclination"].valueDecimal(0.0);
            desiredInclinationString = desiredInclination.ToString();
            autoStage = settings["AA_autoStage"].valueBool(true);
            autoStageDelay = settings["AA_autoStageDelay"].valueDecimal(1.0);
            autoStageLimit = settings["AA_autoStageLimit"].valueInteger(0);
            autoStageDelayString = String.Format("{0:0.0}", autoStageDelay);
            autoStageLimitString = autoStageLimit.ToString();
            autoWarpToApoapsis = settings["AA_autoWarpToApoapsis"].valueBool(true);
            seizeThrottle = settings["AA_seizeThrottle"].valueBool(true);

            Vector4 savedPathWindowPos = settings["AA_pathWindowPos"].valueVector(new Vector4(Screen.width/2, Screen.height/2));
            pathWindowPos = new Rect(savedPathWindowPos.x, savedPathWindowPos.y, 10, 10);

            Vector4 savedHelpWindowPos = settings["AA_helpWindowPos"].valueVector(new Vector4(150, 50));
            helpWindowPos = new Rect(savedHelpWindowPos.x, savedHelpWindowPos.y, 10, 10);

            showPathWindow = settings["AA_showPathWindow"].valueBool(false);
        }

        public override void onSaveGlobalSettings(SettingsManager settings)
        {
            base.onSaveGlobalSettings(settings);

            settings["AA_gravityTurnStartAltitude"].value_decimal = gravityTurnStartAltitude;
            settings["AA_gravityTurnEndAltitude"].value_decimal = gravityTurnEndAltitude;
            settings["AA_gravityTurnEndPitch"].value_decimal = gravityTurnEndPitch;
            settings["AA_gravityTurnShapeExponent"].value_decimal = gravityTurnShapeExponent;
            settings["AA_desiredOrbitRadius"].value_decimal = desiredOrbitRadius;
            settings["AA_desiredInclination"].value_decimal = desiredInclination;
            settings["AA_autoStage"].value_bool = autoStage;
            settings["AA_autoStageDelay"].value_decimal = autoStageDelay;
            settings["AA_autoStageLimit"].value_integer = autoStageLimit;
            settings["AA_autoWarpToApoapsis"].value_bool = autoWarpToApoapsis;
            settings["AA_seizeThrottle"].value_bool = seizeThrottle;
            settings["AA_pathWindowPos"].value_vector = new Vector4(pathWindowPos.x, pathWindowPos.y);
            settings["AA_helpWindowPos"].value_vector = new Vector4(helpWindowPos.x, helpWindowPos.y);
            settings["AA_showPathWindow"].value_bool = showPathWindow;

        }





        ////////////////////////////////////////
        // ASCENT PATH /////////////////////////
        ////////////////////////////////////////

        //controls the ascent path. by "pitch" we mean the pitch of the velocity vector in the rotating frame
        private double desiredPitch(double altitude)
        {
            if (altitude < gravityTurnStartAltitude) return 90.0;

            if (altitude > gravityTurnEndAltitude) return gravityTurnEndPitch;

            return Mathf.Clamp((float)(90.0 - Math.Pow((altitude - gravityTurnStartAltitude) / (gravityTurnEndAltitude - gravityTurnStartAltitude), gravityTurnShapeExponent) * (90.0 - gravityTurnEndPitch)), 0.01F, 89.99F);
        }

        //some 3d geometry relates the bearing of our ground velocity with the inclination and the latitude:
        private double desiredSurfaceAngle(double latitudeRadians)
        {
            double cosDesiredSurfaceAngle = Math.Cos(desiredInclination * Math.PI / 180) / Math.Cos(latitudeRadians);
            double desiredSurfaceAngle;
            if (Math.Abs(cosDesiredSurfaceAngle) > 1.0)
            {
                if (Math.Abs(desiredInclination) < 90) desiredSurfaceAngle = 0;
                else desiredSurfaceAngle = Math.PI;
                if (Math.Abs(desiredInclination) < 90) desiredSurfaceAngle = latitudeRadians;
                else desiredSurfaceAngle = Math.PI - latitudeRadians;
            }
            else
            {
                desiredSurfaceAngle = Math.Acos(cosDesiredSurfaceAngle);
                if (desiredInclination > 0) desiredSurfaceAngle = -desiredSurfaceAngle;
            }
            return desiredSurfaceAngle;
        }


        ///////////////////////////////////////
        // FLYING THE ROCKET //////////////////
        ///////////////////////////////////////

        //called every frame or so; this is where we manipulate the flight controls
        public override void drive(FlightCtrlState s)
        {
            if (mode == DISENGAGED) return;

            if (mode == ON_PAD &&
                (Staging.CurrentStage != Staging.StageCount))
            {
                mode = VERTICAL_ASCENT;
            }

            handleAutoStaging();


            //given the mode, determine what direction the rocket should be pointing
            Vector3d desiredThrustVector = Vector3d.zero;
            switch (mode)
            {
                case VERTICAL_ASCENT:
                    desiredThrustVector = driveVerticalAscent(s);
                    break;

                case GRAVITY_TURN:
                    desiredThrustVector = driveGravityTurn(s);
                    break;

                case COAST_TO_APOAPSIS:
                    desiredThrustVector = driveCoastToApoapsis(s);
                    break;

                case CIRCULARIZE:
                    desiredThrustVector = driveCircularizationBurn(s);
                    break;
            }


            //limit the throttle if something is close to overheating
            double maxTempRatio = ARUtils.maxTemperatureRatio(part.vessel);
            if (maxTempRatio > 0.95 && seizeThrottle)
            {
                s.mainThrottle = Mathf.Clamp(s.mainThrottle, 0.0F, (float)((1 - maxTempRatio) / 0.95));
            }


            steerTowardThrustVector(s, desiredThrustVector);

        }


        //auto-stage when safe
        private void handleAutoStaging()
        {
            //if autostage enabled, and if we are not waiting on the pad, and if we didn't immediately just fire the previous stage
            if (autoStage && mode != ON_PAD && Staging.CurrentStage > 0 && Staging.CurrentStage > autoStageLimit 
                && vesselState.time - lastStageTime > autoStageDelay)
            {
                //don't decouple active or idle engines or tanks
                if (!ARUtils.inverseStageDecouplesActiveOrIdleEngineOrTank(Staging.CurrentStage - 1, part.vessel))  //if staging is safe:
                {
                    //only fire decouplers to drop deactivated engines or tanks
                    if (!ARUtils.inverseStageFiresDecoupler(Staging.CurrentStage - 1, part.vessel)
                        || ARUtils.inverseStageDecouplesDeactivatedEngineOrTank(Staging.CurrentStage - 1, part.vessel))
                    {

                        if (ARUtils.inverseStageFiresDecoupler(Staging.CurrentStage - 1, part.vessel))
                        {
                            //if we decouple things, delay the next stage a bit to avoid exploding the debris
                            lastStageTime = vesselState.time;
                        }

                        Staging.ActivateNextStage();
                    }
                }
            }
        }

        //slews the ship toward desiredThrustVector
        void steerTowardThrustVector(FlightCtrlState s, Vector3d desiredThrustVector)
        {
            //hack
            if (desiredThrustVector == Vector3d.zero) return;

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

        ///////////////////////////////////////
        // VERTICAL ASCENT AND GRAVITY TURN ///
        ///////////////////////////////////////

        //gives a throttle setting that will not waste too much fuel blasting through dense atmosphere
        //the algorithm keeps the velocity just slightly above terminal velocity
        //thanks to jebbe on the forums for suggesting this efficiency improvement
        float efficientThrottle()
        {
            if (vesselState.altitudeASL > part.vessel.mainBody.maxAtmosphereAltitude) return 1.0F;

            double ratioTarget = 1.0;
            double falloff = 15.0;

            double velocityRatio = Vector3d.Dot(vesselState.velocityVesselSurface, vesselState.up) / ARUtils.terminalVelocity(part.vessel, vesselState.CoM);

            if (velocityRatio < ratioTarget) return 1.0F;

            return Mathf.Clamp((float)(1.0 - falloff * (velocityRatio - ratioTarget)), 0.0F, 1.0F);
        }



        //gives a throttle setting that reduces as we approach the desired apoapsis
        //so that we can precisely match the desired apoapsis instead of overshooting it
        float throttleToRaiseApoapsis(double currentApoapsisRadius, double finalApoapsisRadius)
        {
            if (currentApoapsisRadius > finalApoapsisRadius + 5.0) return 0.0F;
            else if (part.vessel.orbit.ApA < part.vessel.mainBody.maxAtmosphereAltitude) return 1.0F; //throttle hard to escape atmosphere

            double GM = vesselState.localg * vesselState.radius * vesselState.radius;

            double potentialDifference = GM / currentApoapsisRadius - GM / finalApoapsisRadius;
            double finalKineticEnergy = 0.5 * vesselState.speedOrbital * vesselState.speedOrbital + potentialDifference;
            double speedDifference = Math.Sqrt(2 * finalKineticEnergy) - vesselState.speedOrbital;

            if (speedDifference > 100) return 1.0F;

            return Mathf.Clamp((float)(speedDifference / (1.0 * vesselState.maxThrustAccel)), 0.05F, 1.0F); //1 second time constant for throttle reduction

            //return Mathf.Clamp((float)(speedDifference / 100.0), 0.05F, 1.0F);
        }

        Vector3d driveVerticalAscent(FlightCtrlState s)
        {
            //during the vertical ascent we just thrust straight up at max throttle
            if (seizeThrottle) s.mainThrottle = efficientThrottle();
            if (vesselState.altitudeASL > gravityTurnStartAltitude) mode = GRAVITY_TURN;
            if (seizeThrottle && part.vessel.orbit.ApR > desiredOrbitRadius)
            {
                lastAccelerationTime = vesselState.time;
                mode = COAST_TO_APOAPSIS;
            }
            return vesselState.up;
        }



        Vector3d driveGravityTurn(FlightCtrlState s)
        {
            //stop the gravity turn when our apoapsis reaches the desired altitude
            if (seizeThrottle && part.vessel.orbit.ApR > desiredOrbitRadius)
            {
                mode = COAST_TO_APOAPSIS;
                lastAccelerationTime = vesselState.time;
                return vesselState.velocityVesselOrbitUnit;
            }

            if (vesselState.altitudeASL < gravityTurnStartAltitude)
            {
                mode = VERTICAL_ASCENT;
                return vesselState.up;
            }

            if (seizeThrottle)
            {
                float gentleThrottle = throttleToRaiseApoapsis(part.vessel.orbit.ApR, desiredOrbitRadius);
                if (gentleThrottle < 1.0F)
                {
                    //when we are bringing down the throttle to make the apoapsis accurate, we're liable to point in weird
                    //directions because thrust goes down and so "difficulty" goes up. so just burn prograde
                    s.mainThrottle = gentleThrottle;
                    return vesselState.velocityVesselOrbitUnit;
                }

                s.mainThrottle = efficientThrottle();
            }


            //transition gradually from the rotating to the non-rotating reference frame. this calculation ensures that
            //when our maximum possible apoapsis, given our orbital energy, is desiredOrbitalRadius, then we are
            //fully in the non-rotating reference frame and thus doing the correct calculations to get the right inclination
            double GM = vesselState.localg * vesselState.radius * vesselState.radius;
            double potentialDifferenceWithApoapsis = GM / vesselState.radius - GM / desiredOrbitRadius;
            double verticalSpeedForDesiredApoapsis = Math.Sqrt(2 * potentialDifferenceWithApoapsis);
            double referenceFrameBlend = vesselState.speedOrbital / verticalSpeedForDesiredApoapsis;
            referenceFrameBlend = Mathf.Clamp((float)referenceFrameBlend, 0.0F, 1.0F);

            Vector3d actualVelocityUnit = ((1 - referenceFrameBlend) * vesselState.velocityVesselSurfaceUnit 
                                               + referenceFrameBlend * vesselState.velocityVesselOrbitUnit).normalized;

            //minus sign is there because somewhere a sign got flipped in integrating with MechJeb
            double surfaceAngle = -desiredSurfaceAngle(vesselState.latitude * Math.PI / 180.0);
            Vector3d desiredSurfaceHeading = Math.Cos(surfaceAngle) * vesselState.east + Math.Sin(surfaceAngle) * vesselState.north;
            double desPitch = desiredPitch(vesselState.altitudeASL);
            Vector3d desiredVelocityUnit = Math.Cos(desPitch * Math.PI / 180) * desiredSurfaceHeading
                                                + Math.Sin(desPitch * Math.PI / 180) * vesselState.up;

            Vector3d velocityError = (desiredVelocityUnit - actualVelocityUnit);

            //"difficulty" accounts for the difficulty of changing a large velocity vector given our current thrust
            double difficulty = vesselState.velocityVesselSurface.magnitude / (50 + 10 * vesselState.thrustAccel(s.mainThrottle));
            if (difficulty > 5) difficulty = 5;

            if (vesselState.maxThrustAccel == 0) difficulty = 1.0; //so we don't freak out over having no thrust between stages

            Vector3d desiredThrustVector;
            desiredThrustVector = desiredVelocityUnit;
            Vector3d steerOffset = velocityKP * difficulty * velocityError;
            double maxOffset = 10 * Math.PI / 180;
            if (steerOffset.magnitude > maxOffset) steerOffset = maxOffset * steerOffset.normalized;
            desiredThrustVector += steerOffset;
            desiredThrustVector = desiredThrustVector.normalized;

            return desiredThrustVector;
        }

        ///////////////////////////////////////
        // COAST AND CIRCULARIZATION //////////
        ///////////////////////////////////////

        double thrustPitchForZeroVerticalAcc(double orbitRadius, double currentHorizontalSpeed, double thrustAcceleration, double currentVerticalSpeed)
        {
            double centrifugalAcceleration = currentHorizontalSpeed * currentHorizontalSpeed / orbitRadius;
            double gravityAcceleration = gAtRadius(orbitRadius);
            double downwardAcceleration = gravityAcceleration - centrifugalAcceleration;
            downwardAcceleration -= currentVerticalSpeed / 1.0; //1.0 = 1 second time constant for killing existing vertical speed
            if (Math.Abs(downwardAcceleration) > thrustAcceleration) return 0; //we're screwed, just burn horizontal
            return Math.Asin(downwardAcceleration / thrustAcceleration);
        }

        double expectedSpeedAtApoapsis()
        {
            // (1/2)(orbitSpeed)^2 - g(r)*r = constant
            double currentTotalEnergy = 0.5 * vesselState.speedOrbital * vesselState.speedOrbital - vesselState.localg * vesselState.radius;
            double gAtApoapsis = gAtRadius(part.vessel.orbit.ApR);
            double potentialEnergyAtApoapsis = -gAtApoapsis * part.vessel.orbit.ApR;
            double kineticEnergyAtApoapsis = currentTotalEnergy - potentialEnergyAtApoapsis;
            return Math.Sqrt(2 * kineticEnergyAtApoapsis);
        }

        double circularizationBurnThrottle(double currentSpeed, double finalSpeed)
        {
            if (vesselState.maxThrustAccel == 0)
            {
                //then we are powerless
                return 0;
            }
            double desiredThrustAccel = (finalSpeed - currentSpeed) / 1.0; //5 second time constant for throttling down
            float throttleCommand = (float)((desiredThrustAccel - vesselState.minThrustAccel) / (vesselState.maxThrustAccel - vesselState.minThrustAccel));
            return Mathf.Clamp(throttleCommand, 0.01F, 1.0F);
        }

        double gAtRadius(double radius)
        {
            return FlightGlobals.getGeeForceAtPosition(part.vessel.mainBody.position + radius * vesselState.up).magnitude;
        }

        double circularSpeedAtRadius(double radius)
        {
            return Math.Sqrt(gAtRadius(radius) * radius);
        }

        Vector3d driveCoastToApoapsis(FlightCtrlState s)
        {
            s.mainThrottle = 0.0F;

            if (Vector3d.Dot(vesselState.velocityVesselOrbit, vesselState.up) < 0) //if we have started to descend, i.e. reached apoapsis, circularize
            {
                mode = CIRCULARIZE;
                if (TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(0);
            }

            if (part.vessel.orbit.ApR < desiredOrbitRadius - 1000.0)
            {
                mode = GRAVITY_TURN;
                return vesselState.velocityVesselOrbitUnit;
            }

            //handle time warp
            double[] lookaheadTimes = new double[] { 0, 5, 90, 90, 120, 200, 500, 5000 };
            //reduce time warp if we are close to apoapsis, or just past apoapsis, 
            //or if our apoapsis is too low
            if (part.vessel.orbit.timeToAp < lookaheadTimes[TimeWarp.CurrentRateIndex]
                || part.vessel.orbit.period - part.vessel.orbit.timeToAp < 10
                || (seizeThrottle && part.vessel.orbit.ApR < desiredOrbitRadius - 10.0))
            {
                if (TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(TimeWarp.CurrentRateIndex - 1);
            }
            else
            {
                //if we just tried to increase warp rate but find ourselves at a lower warp, probably the 
                //game switched us back down because it thought there was acceleration. start a cooldown before trying
                //again (or else the game starts lagging hard, switching in and out of physics)
                if (TimeWarp.CurrentRateIndex < lastAttemptedWarpIndex)
                {
                    lastAccelerationTime = vesselState.time;
                    lastAttemptedWarpIndex = TimeWarp.CurrentRateIndex;
                }

                //if we are allowed to warp higher, do so
                double[] altLimits = new double[] { 0, 0, part.vessel.mainBody.maxAtmosphereAltitude, part.vessel.mainBody.maxAtmosphereAltitude, part.vessel.mainBody.Radius * 0.25, part.vessel.mainBody.Radius * 0.5, part.vessel.mainBody.Radius * 1.0, part.vessel.mainBody.Radius * 2.0 };
                if (autoWarpToApoapsis
                    && TimeWarp.CurrentRateIndex < 7
                    && altLimits[TimeWarp.CurrentRateIndex + 1] < vesselState.altitudeASL
                    && part.vessel.orbit.timeToAp > lookaheadTimes[TimeWarp.CurrentRateIndex + 1]
                    && vesselState.time - lastAccelerationTime > 5.0)
                {
                    lastAttemptedWarpIndex = TimeWarp.CurrentRateIndex + 1;
                    TimeWarp.SetRate(TimeWarp.CurrentRateIndex + 1);
                    lastAccelerationTime = vesselState.time;
                }
            }

            //if we are running physics, we can do burns if the apoapsis falls, and we can maintain our orientation in preparation for circularization
            //if our Ap is too low and we are pointing reasonably along our velocity
            if (TimeWarp.CurrentRateIndex <= 1
                && part.vessel.orbit.ApR < desiredOrbitRadius - 10
                && Vector3d.Dot(vesselState.forward, vesselState.velocityVesselOrbitUnit) > 0.5)
            {
                s.mainThrottle = throttleToRaiseApoapsis(part.vessel.orbit.ApR, desiredOrbitRadius);
                lastAccelerationTime = vesselState.time;
                return vesselState.velocityVesselOrbitUnit;
            }

            //if we are out of the atmosphere and have a high enough apoapsis and aren't near apoapsis, play dead to 
            //prevent acceleration from interfering with time warp. otherwise point in the direction that we will
            //want to point at the start of the circularization burn
            if (autoWarpToApoapsis
                && vesselState.altitudeASL > part.vessel.mainBody.maxAtmosphereAltitude
                && part.vessel.orbit.timeToAp > lookaheadTimes[2]
                && part.vessel.orbit.ApR > desiredOrbitRadius - 10)
            {
                return Vector3d.zero;
            }
            else
            {
                double expectedApoapsisThrottle = circularizationBurnThrottle(expectedSpeedAtApoapsis(), circularSpeedAtRadius(part.vessel.orbit.ApR));
                double desiredThrustPitch = thrustPitchForZeroVerticalAcc(part.vessel.orbit.ApR, expectedSpeedAtApoapsis(),
                                                                          vesselState.thrustAccel(expectedApoapsisThrottle), 0);
                Vector3d groundHeading = Vector3d.Exclude(vesselState.up, part.vessel.orbit.GetVel()).normalized;
                return (Math.Cos(desiredThrustPitch) * groundHeading + Math.Sin(desiredThrustPitch) * vesselState.up);
            }
        }



        Vector3d driveCircularizationBurn(FlightCtrlState s)
        {
            if (part.vessel.orbit.ApR < desiredOrbitRadius - 1000.0)
            {
                mode = GRAVITY_TURN;
                return vesselState.velocityVesselOrbitUnit;
            }

            //During circularization we think in the *non-rotating* frame, but allow ourselves to discuss centrifugal acceleration
            //as the tendency for the altitude to rise because of our large horizontal velocity.
            //To get a circular orbit we need to reach orbital horizontal speed while simultaneously getting zero vertical speed.
            //We compute the thrust vector needed to get zero vertical acceleration, taking into account gravity and centrifugal
            //acceleration. If we currently have a nonzero vertical velocity we then adjust the thrust vector to bring that to zero

            double orbitalRadius = (vesselState.CoM - part.vessel.mainBody.position).magnitude;
            double circularSpeed = Math.Sqrt(orbitalRadius * vesselState.localg);

            s.mainThrottle = (float)circularizationBurnThrottle(vesselState.speedOrbital, circularSpeed);

            double thrustAcceleration = vesselState.thrustAccel(s.mainThrottle);

            Vector3d groundHeading = Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbit).normalized;
            double horizontalSpeed = Vector3d.Dot(groundHeading, vesselState.velocityVesselOrbit);
            double verticalSpeed = Vector3d.Dot(vesselState.up, vesselState.velocityVesselOrbit);

            double desiredThrustPitch = thrustPitchForZeroVerticalAcc(orbitalRadius, horizontalSpeed, thrustAcceleration, verticalSpeed);

            //we start the circularization burn at apoapsis, and the orbit is about as circular as it is going to get when the 
            //periapsis has moved closer to us than the apoapsis
            if (Math.Min(part.vessel.orbit.timeToPe, part.vessel.orbit.period - part.vessel.orbit.timeToPe) <
                Math.Min(part.vessel.orbit.timeToAp, part.vessel.orbit.period - part.vessel.orbit.timeToAp)
                || vesselState.speedOrbital > circularSpeed + 1.0) //makes sure we don't keep burning forever if we are somehow way off course
            {
                s.mainThrottle = 0.0F;
                enabled = false;
            }

            return (Math.Cos(desiredThrustPitch) * groundHeading + Math.Sin(desiredThrustPitch) * vesselState.up);
        }





        ///////////////////////////////////////////////
        // GUI ////////// /////////////////////////////
        ///////////////////////////////////////////////    


        public override void drawGUI(int baseWindowID)
        {
            GUI.skin = HighLogic.Skin;
            if (minimized)
            {
                windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, "Ascent", GUILayout.Width(100), GUILayout.Height(30));
            }
            else
            {
                windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, "Ascent Autopilot", GUILayout.Width(200), GUILayout.Height(100));
                if (showPathWindow)
                {
                    pathWindowPos = GUILayout.Window(PATH_WINDOW_ID, pathWindowPos, PathWindowGUI, "Ascent Path", GUILayout.Width(300), GUILayout.Height(100));
                }
            }
            if (showHelpWindow)
            {
                helpWindowPos = GUILayout.Window(HELP_WINDOW_ID, helpWindowPos, HelpWindowGUI, "Ascent Autopilot Help", GUILayout.Width(400), GUILayout.Height(500));
            }

        }

        protected override void WindowGUI(int windowID)
        {

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            GUIStyle s = new GUIStyle(GUI.skin.button);
            if (mode == DISENGAGED)
            {
                s.normal.textColor = Color.green;
                if (GUILayout.Button("Engage", s))
                {
                    initializeInternals();
                    mode = ON_PAD;
                }
            }
            else
            {
                s.normal.textColor = Color.red;
                if (GUILayout.Button("Disengage", s))
                {
                    enabled = false;
                }
            }

            if (minimized)
            {
                if (GUILayout.Button("Maximize"))
                {
                    minimized = false;
                }
            }
            else
            {
                if (GUILayout.Button("Minimize"))
                {
                    minimized = true;
                }
            }

            showHelpWindow = GUILayout.Toggle(showHelpWindow, "?", new GUIStyle(GUI.skin.button));

            GUILayout.EndHorizontal();

            if (!minimized)
            {

                if (seizeThrottle)
                {
                    double orbitAltKm = (desiredOrbitRadius - part.vessel.mainBody.Radius)/1000.0;
                    orbitAltKm = doGUITextInput("Orbit altitude: ", 100.0F, desiredOrbitAltitudeKmString, 30.0F, "km", 30.0F, out desiredOrbitAltitudeKmString, orbitAltKm);
                    desiredOrbitRadius = part.vessel.mainBody.Radius + orbitAltKm * 1000.0;
                }
                else
                {
                    GUILayout.Label(String.Format("Apoapsis: {0:0.0} km", part.vessel.orbit.ApA / 1000.0));
                    if (part.vessel.orbit.PeA > 0)
                    {
                        GUILayout.Label(String.Format("Periapsis: {0:0.0} km", part.vessel.orbit.PeA / 1000.0));
                    }
                    else
                    {
                        GUILayout.Label("Periapsis: <0 km");
                    }

                    if (GUILayout.Button("Circularize"))
                    {
                        mode = COAST_TO_APOAPSIS;
                    }
                }

                desiredInclination = doGUITextInput("Orbit inclination: ", 100.0F, desiredInclinationString, 30.0F, "°", 30.0F, out desiredInclinationString, desiredInclination);

                seizeThrottle = GUILayout.Toggle(seizeThrottle, "Auto-throttle?");

                autoStage = GUILayout.Toggle(autoStage, "Auto-stage?");

                if (autoStage)
                {
                    autoStageDelay = doGUITextInput("Stage delay: ", 100.0F, autoStageDelayString, 30.0F, "s", 30.0F, out autoStageDelayString, autoStageDelay);

                    autoStageLimit = doGUITextInput("Stop at stage #", 100.0F, autoStageLimitString, 30.0F, "", 30.0F, out autoStageLimitString, autoStageLimit);
                }

                bool newAutoWarp = GUILayout.Toggle(autoWarpToApoapsis, "Auto-warp toward apoapsis?", new GUIStyle(GUI.skin.toggle));
                if (autoWarpToApoapsis && !newAutoWarp && TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(0);
                autoWarpToApoapsis = newAutoWarp;

                
                GUILayout.BeginHorizontal();
                GUIStyle stateStyle = new GUIStyle(GUI.skin.label);
                stateStyle.normal.textColor = Color.green;
                if (mode == DISENGAGED) stateStyle.normal.textColor = Color.red;
                GUILayout.Label("State: ", GUILayout.Width(60));
                GUILayout.Label(modeStrings[mode], stateStyle);

                GUILayout.EndHorizontal();

                showPathWindow = GUILayout.Toggle(showPathWindow, "Edit ascent path", new GUIStyle(GUI.skin.button));


            }

            GUILayout.EndVertical();

            GUI.DragWindow(); //make window draggable

        }



        private void PathWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            double oldGravityTurnStartAltitude = gravityTurnStartAltitude;
            double oldGravityTurnEndAltitude = gravityTurnEndAltitude;
            double oldGravityTurnShapeExponent = gravityTurnShapeExponent;
            double oldGravityTurnEndPitch = gravityTurnEndPitch;

            double turnStartAltKm = gravityTurnStartAltitude / 1000.0;
            turnStartAltKm = doGUITextInput("Turn start altitude: ", 300.0F, gravityTurnStartAltitudeKmString, 30.0F, "km", 30.0F, out gravityTurnStartAltitudeKmString, turnStartAltKm);
            gravityTurnStartAltitude = turnStartAltKm * 1000.0;

            double turnEndAltKm = gravityTurnEndAltitude / 1000.0;
            turnEndAltKm = doGUITextInput("Turn end altitude: ", 300.0F, gravityTurnEndAltitudeKmString, 30.0F, "km", 30.0F, out gravityTurnEndAltitudeKmString, turnEndAltKm);
            gravityTurnEndAltitude = turnEndAltKm * 1000.0;
            gravityTurnEndPitch = doGUITextInput("Final pitch: ", 300.0F, gravityTurnEndPitchString, 30.0F, "°", 30.0F, out gravityTurnEndPitchString, gravityTurnEndPitch);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Turn shape: ");
            gravityTurnShapeExponent = GUILayout.HorizontalSlider((float)gravityTurnShapeExponent, 0.0F, 1.0F);
            GUILayout.EndHorizontal();

            GUILayout.Box(pathTexture);

            GUILayout.EndVertical();

            if (gravityTurnStartAltitude != oldGravityTurnStartAltitude ||
                gravityTurnEndAltitude != oldGravityTurnEndAltitude ||
                gravityTurnShapeExponent != oldGravityTurnShapeExponent ||
                gravityTurnEndPitch != oldGravityTurnEndPitch)
            {
                updatePathTexture();
            }

            GUI.DragWindow();
        }


        Vector2 scrollPosition;
        private void HelpWindowGUI(int windowID)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("MechJeb's ascent autopilot will fly a ship completely automatically from the launchpad to any desired circular orbit around Kerbin. Engage the autopilot on the launchpad to place it in the \"Awaiting liftoff\" state. Launch your ship as usual and the ascent autopilot will fly it to orbit and then automatically disengage. Disengage the autopilot at any time to regain manual control of the ship.\n\n" +
    "During flight, the autopilot operates in several sequential modes. During the \"vertical ascent\" it flies directly upwards until it reaches a certain altitude. Then, during the \"gravity turn,\" the ship pitches over and starts accelerating horizontally. When the apoapsis of the ship's orbit reaches the desired altitude, the engines turn off for the \"coast to apoapsis.\" When the apoapsis is reached, the ship executes one final \"circularizing\" burn to make the orbit circular. The ascent autopilot will automatically disengage after the circularization burn is complete. \n\n" +
    "Before launch, you can set the desired final altitude and inclination of the orbit in the appropriate text fields. Zero inclination gives a normal eastward equatorial orbit. +90 or -90 degrees inclination give a polar orbit starting out north or south, respectively. 180 degrees gives a westward equatorial orbit.\n\n" +
    "By default the autopilot will automatically fire the next stage of the rocket when the current one burns out. This can be turned off by toggling \"Auto-stage?\". \n\n" +
    "By default the autopilot will use time warp to speed the coast to apoapsis phase. This can be turned off by toggling \"Auto-warp toward apoapsis?\".\n\n" +
    "By default the autopilot will seize exclusive control of the throttle while it is engaged. If you want to control the throttle during the vertcal ascent and gravity turn you can turn off \"Auto-throttle?\". This will replace the orbit altitude input with a display of your current apoapsis and periapsis, and a \"Circularize\" button. When your apoapsis is at the desired height, you can press \"Circularize\" and the autopilot will again take control of the throttle for the coast to apoapsis and circularization burn at the apoapsis. \n\n" +
    "The \"Edit Ascent Path\" button brings up a window that lets you customize the path the ascent will take through the atmosphere. \"Turn start altitude\" is the height to which the rocket will ascend vertically before starting to turn over. The turn stops once the ship rises above the \"Turn end alitude,\" after which the autopilot will attempt to keep the pitch at the angle given in the next text box. Here \"pitch\" means the angle the velocity vector (yellow circle on the navball) makes with the horizon. In between, the graph shows the path of the ascent.The exact shape of the path can be varied with the \"Turn shape\" slider.\n\n" +
    "The ascent autopilot can be used for ascent from the Mun, as follows. With the autopilot disengaged, first enter the desired orbit parameters and edit the ascent path to one reasonable for launching in a vacuum. As a suggestion, try a turn start altitude of 0km, a turn end altitude of 5km, and a final pitch of 0 degrees. This launch profile turns horizontal to thrust for orbit almost immediately, since there is no atmosphere to climb above.  When you are ready, engage the autopilot and it will immediately begin the ascent to munar orbit.\n\n");
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }


        //utility function that displays a horizontal row of label-textbox-label and parses the number in the textbox
        double doGUITextInput(String leftText, float leftWidth, String currentText, float textWidth, String rightText, float rightWidth, out String newText, double defaultValue)
        {
            GUIStyle leftLabelStyle = new GUIStyle(GUI.skin.label);

            double value;
            if (Double.TryParse(currentText, out value))
            {
                leftLabelStyle.normal.textColor = Color.white;
            }
            else
            {
                leftLabelStyle.normal.textColor = Color.yellow;
                value = defaultValue;
            }
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(leftText, leftLabelStyle, GUILayout.Width(leftWidth));
            newText = GUILayout.TextField(currentText, GUILayout.MinWidth(textWidth));
            GUILayout.Label(rightText, GUILayout.Width(rightWidth));
            GUILayout.EndHorizontal();

            return value;
        }

        //utility function that displays a horizontal row of label-textbox-label and parses the number in the textbox
        int doGUITextInput(String leftText, float leftWidth, String currentText, float textWidth, String rightText, float rightWidth, out String newText, int defaultValue)
        {
            GUIStyle leftLabelStyle = new GUIStyle(GUI.skin.label);

            int value;
            if (int.TryParse(currentText, out value))
            {
                leftLabelStyle.normal.textColor = Color.white;
            }
            else
            {
                leftLabelStyle.normal.textColor = Color.yellow;
                value = defaultValue;
            }
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(leftText, leftLabelStyle, GUILayout.Width(leftWidth));
            newText = GUILayout.TextField(currentText, GUILayout.MinWidth(textWidth));
            GUILayout.Label(rightText, GUILayout.Width(rightWidth));
            GUILayout.EndHorizontal();

            return value;
        }

        //redraw the picture of the planned flight path
        private void updatePathTexture()
        {
            double scale = gravityTurnEndAltitude / pathTexture.height; //meters per pixel


            for (int y = 0; y < pathTexture.height; y++)
            {
                Color c = new Color(0.0F, 0.0F, (float)Math.Max(0.0, 1.0 - y * scale / part.vessel.mainBody.maxAtmosphereAltitude));

                for (int x = 0; x < pathTexture.width; x++)
                {
                    pathTexture.SetPixel(x, y, c);
                }
            }

            double alt = 0;
            double downrange = 0;
            while (alt < gravityTurnEndAltitude && downrange < pathTexture.width * scale)
            {
                alt += scale * Math.Sin(desiredPitch(alt) * Math.PI / 180);
                downrange += scale * Math.Cos(desiredPitch(alt) * Math.PI / 180);
                for (int x = (int)(downrange / scale); x <= (downrange / scale) + 2 && x < pathTexture.width; x++)
                {
                    for (int y = (int)(alt / scale) - 1; y <= (int)(alt / scale) + 1 && y < pathTexture.height; y++)
                    {
                        pathTexture.SetPixel(x, y, Color.red);
                    }
                }
            }

            pathTexture.Apply();
        }




        ///////////////////////////////////////////////
        // INITIALIZATION /////////////////////////////
        ///////////////////////////////////////////////


 /*       private void initializeSettings()
        {
            gravityTurnStartAltitude = 10000.0;
            gravityTurnStartAltitudeKmString = "10";
            gravityTurnEndAltitude = 70000.0;
            gravityTurnEndAltitudeKmString = "70";
            gravityTurnEndPitch = 0;
            gravityTurnEndPitchString = "0";
            desiredOrbitRadius = 600000.0 + 100000.0;
            desiredOrbitAltitudeKmString = "100";
            desiredInclination = 0.0;
            desiredInclinationString = "0";

            autoWarpToApoapsis = true;
            autoStage = true;
            minimized = false;
            seizeThrottle = true;
        }*/

        private void initializeInternals()
        {
            mode = DISENGAGED;
            lastAccelerationTime = 0;
            lastAttemptedWarpIndex = 0;
            lastStageTime = 0;
        }







        ///////////////////////////////////////
        // VESSEL HISTORY UPDATES ///////////// 
        ///////////////////////////////////////


        public override void onPartStart()
        {
            initializeInternals();
            minimized = true; //will be set to false if we start on the pad
        }

        public override void onFlightStart()
        {
            updatePathTexture();

            part.vessel.orbit.OnReferenceBodyChange += new Orbit.CelestialBodyDelegate(this.handleReferenceBodyChange);
        }


        void handleReferenceBodyChange(CelestialBody body)
        {
            if (Staging.CurrentStage != Staging.StageCount) enabled = false; //if statement is needed or else this executes on restarting
        }

        public override void onFlightStartAtLaunchPad() //Called when vessel is placed on the launchpad
        {
            if (part.vessel == FlightGlobals.ActiveVessel)
            {
                //mode = ON_PAD;
                minimized = false;
            }
        }


        public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
        {
            //load settings
        }

        public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
        {
            //save settings

        }



        public override String getName()
        {
            return "Ascent autopilot";
        }


        void print(String s)
        {
            MonoBehaviour.print(s);
        }
    }





}