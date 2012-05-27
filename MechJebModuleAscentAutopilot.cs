using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


/*
 * Todo:
 * 
 * -Enable launch-to-plane (auto-time and auto set inclination)
 * 
 * Future:
 * 
 * -add a roll input (may require improved attitude controller)
 * 
 */


namespace MuMech
{

    public class MechJebModuleAscentAutopilot : ComputerModule
    {
        public MechJebModuleAscentAutopilot(MechJebCore core) : base(core) { }

        public override void onModuleDisabled()
        {
            turnOffSteering();
        }

        public override void onControlLost()
        {
            turnOffSteering();
        }

        void turnOffSteering()
        {
            if (mode != AscentMode.DISENGAGED)
            {
                if (mode != AscentMode.ON_PAD)
                {
                    FlightInputHandler.SetNeutralControls(); //makes sure we leave throttle at zero
                }
                mode = AscentMode.DISENGAGED;
                core.attitudeDeactivate(this);
            }
        }

        public const double BEACH_ALTITUDE = 100001.6;
        public const double BEACH_INCLINATION = 24.0;

        //programmatic interface to the module. 
        public void launchTo(double orbitAltitude, double orbitInclination)
        {
            this.enabled = true;
            core.controlClaim(this);
            desiredOrbitAltitude = orbitAltitude;
            desiredInclination = orbitInclination;

            //lift off if we haven't yet:
            if (Staging.CurrentStage == Staging.StageCount)
            {
                Staging.ActivateNextStage();
            }

            mode = AscentMode.VERTICAL_ASCENT;
        }

        ////////////////////////////////////
        // GLOBAL DATA /////////////////////
        ////////////////////////////////////

        //autopilot modes
        public enum AscentMode { ON_PAD, VERTICAL_ASCENT, GRAVITY_TURN, COAST_TO_APOAPSIS, CIRCULARIZE, DISENGAGED };

        String[] modeStrings = new String[] { "Awaiting liftoff", "Vertical ascent", "Gravity turn", "Coasting to apoapsis", "Circularizing", "Disengaged" };
        public AscentMode mode = AscentMode.DISENGAGED;

        //control errors
        Vector3d lastThrustError = Vector3d.zero;

        //control gains
        const double velocityKP = 5.0;
        const double thrustKP = 3.0;
        const double thrustKD = 15.0;
        //double thrustGainAttenuator = 1;


        //GUI stuff
        bool minimized = false;
        bool showHelpWindow = false;
        Texture2D pathTexture = new Texture2D(400, 100);
        Vector2 helpScrollPosition;


        //things the drive code needs to remember between frames
        //double lastAccelerationTime = 0;
        //int lastAttemptedWarpIndex = 0;
        double lastStageTime = 0;

        double totalDVExpended;
        double gravityLosses;
        double dragLosses;
        double steeringLosses;
        double launchTime;
        double mecoTime;
        double launchLongitude;
        double launchMass;
        double launchPhaseAngle;

        bool timeIgnitionForRendezvous = false;
        bool choosingRendezvousTarget = false;
        double predictedLaunchPhaseAngle;
        String predictedLaunchPhaseAngleString = "";
        Vessel rendezvousTarget;
        Vector2 rendezvousTargetScrollPos = new Vector2();
        double rendezvousIgnitionCountdown;
        bool rendezvousIgnitionCountdownHolding;

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

        double _desiredOrbitAltitude = 100000.0;
        double desiredOrbitAltitude
        {
            get { return _desiredOrbitAltitude; }
            set
            {
                if (_desiredOrbitAltitude != value) core.settingsChanged = true;
                _desiredOrbitAltitude = value;
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

        double _launchHeading = 90.0;
        double launchHeading
        {
            get { return _launchHeading; }
            set
            {
                if (_launchHeading != value) core.settingsChanged = true;
                _launchHeading = value;
            }
        }
        String launchHeadingString = "90";

        bool _headingNotInc = false;
        bool headingNotInc
        {
            get { return _headingNotInc; }
            set
            {
                if (_headingNotInc != value) core.settingsChanged = true;
                _headingNotInc = value;
            }
        }

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

        protected Rect _statsWindowPos;
        Rect statsWindowPos
        {
            get { return _statsWindowPos; }
            set
            {
                if (_statsWindowPos.x != value.x || _statsWindowPos.y != value.y) core.settingsChanged = true;
                _statsWindowPos = value;
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

        bool _showStats = false;
        bool showStats
        {
            get { return _showStats; }
            set
            {
                if (_showStats != value) core.settingsChanged = true;
                _showStats = value;
            }
        }

        public override void onLoadGlobalSettings(SettingsManager settings)
        {
            base.onLoadGlobalSettings(settings);

            gravityTurnStartAltitude = settings["AA_gravityTurnStartAltitude"].valueDecimal(10000.0);
            gravityTurnStartAltitudeKmString = (gravityTurnStartAltitude / 1000.0).ToString();
            gravityTurnEndAltitude = settings["AA_gravityTurnEndAltitude"].valueDecimal(70000.0);
            gravityTurnEndAltitudeKmString = (gravityTurnEndAltitude / 1000.0).ToString();
            gravityTurnEndPitch = settings["AA_gravityTurnEndPitch"].valueDecimal(0.0);
            gravityTurnEndPitchString = gravityTurnEndPitch.ToString();
            gravityTurnShapeExponent = settings["AA_gravityTurnShapeExponent"].valueDecimal(0.4);
            desiredOrbitAltitude = settings["AA_desiredOrbitAltitude"].valueDecimal(100000.0);
            desiredOrbitAltitudeKmString = (desiredOrbitAltitude / 1000.0).ToString();
            desiredInclination = settings["AA_desiredInclination"].valueDecimal(0.0);
            desiredInclinationString = desiredInclination.ToString();
            launchHeading = settings["AA_launchHeading"].valueDecimal(90.0);
            launchHeadingString = launchHeading.ToString();
            headingNotInc = settings["AA_headingNotInc"].valueBool(false);
            autoStage = settings["AA_autoStage"].valueBool(true);
            autoStageDelay = settings["AA_autoStageDelay"].valueDecimal(1.0);
            autoStageLimit = settings["AA_autoStageLimit"].valueInteger(0);
            autoStageDelayString = String.Format("{0:0.0}", autoStageDelay);
            autoStageLimitString = autoStageLimit.ToString();
            autoWarpToApoapsis = settings["AA_autoWarpToApoapsis"].valueBool(true);
            seizeThrottle = settings["AA_seizeThrottle"].valueBool(true);
            predictedLaunchPhaseAngle = settings["AA_predictedLaunchPhaseAngle"].valueDecimal(0.0);
            predictedLaunchPhaseAngleString = String.Format("{0:0.00}", predictedLaunchPhaseAngle);

            Vector4 savedPathWindowPos = settings["AA_pathWindowPos"].valueVector(new Vector4(Screen.width / 2, Screen.height / 2));
            pathWindowPos = new Rect(savedPathWindowPos.x, savedPathWindowPos.y, 10, 10);

            Vector4 savedHelpWindowPos = settings["AA_helpWindowPos"].valueVector(new Vector4(150, 50));
            helpWindowPos = new Rect(savedHelpWindowPos.x, savedHelpWindowPos.y, 10, 10);

            Vector4 savedStatsWindowPos = settings["AA_statsWindowPos"].valueVector(new Vector4(150, 50));
            statsWindowPos = new Rect(savedStatsWindowPos.x, savedStatsWindowPos.y, 10, 10);

            showPathWindow = settings["AA_showPathWindow"].valueBool(false);
            showStats = settings["AA_showStatsWindow"].valueBool(false);
        }

        public override void onSaveGlobalSettings(SettingsManager settings)
        {
            base.onSaveGlobalSettings(settings);

            settings["AA_gravityTurnStartAltitude"].value_decimal = gravityTurnStartAltitude;
            settings["AA_gravityTurnEndAltitude"].value_decimal = gravityTurnEndAltitude;
            settings["AA_gravityTurnEndPitch"].value_decimal = gravityTurnEndPitch;
            settings["AA_gravityTurnShapeExponent"].value_decimal = gravityTurnShapeExponent;
            settings["AA_desiredOrbitAltitude"].value_decimal = desiredOrbitAltitude;
            settings["AA_desiredInclination"].value_decimal = desiredInclination;
            settings["AA_launchHeading"].value_decimal = launchHeading;
            settings["AA_headingNotInc"].value_bool = headingNotInc;
            settings["AA_autoStage"].value_bool = autoStage;
            settings["AA_autoStageDelay"].value_decimal = autoStageDelay;
            settings["AA_autoStageLimit"].value_integer = autoStageLimit;
            settings["AA_autoWarpToApoapsis"].value_bool = autoWarpToApoapsis;
            settings["AA_seizeThrottle"].value_bool = seizeThrottle;
            settings["AA_predictedLaunchPhaseAngle"].value_decimal = predictedLaunchPhaseAngle;
            settings["AA_pathWindowPos"].value_vector = new Vector4(pathWindowPos.x, pathWindowPos.y);
            settings["AA_helpWindowPos"].value_vector = new Vector4(helpWindowPos.x, helpWindowPos.y);
            settings["AA_statsWindowPos"].value_vector = new Vector4(statsWindowPos.x, statsWindowPos.y);
            settings["AA_showPathWindow"].value_bool = showPathWindow;
            settings["AA_showStatsWindow"].value_bool = showStats;
        }





        ////////////////////////////////////////
        // ASCENT PATH /////////////////////////
        ////////////////////////////////////////

        //controls the ascent path. by "pitch" we mean the pitch of the velocity vector in the rotating frame
        private double unsmoothedDesiredPitch(double altitude)
        {
            if (altitude < gravityTurnStartAltitude) return 90.0;

            if (altitude > gravityTurnEndAltitude) return gravityTurnEndPitch;

            return Mathf.Clamp((float)(90.0 - Math.Pow((altitude - gravityTurnStartAltitude) / (gravityTurnEndAltitude - gravityTurnStartAltitude), gravityTurnShapeExponent) * (90.0 - gravityTurnEndPitch)), 0.01F, 89.99F);
        }

        private double desiredPitch(double altitude)
        {
            return unsmoothedDesiredPitch(altitude);
        }

        //some 3d geometry relates the bearing of our ground velocity with the inclination and the latitude:
        private double desiredSurfaceAngle(double latitudeRadians, double referenceFrameBlend)
        {
            double cosDesiredSurfaceAngle = Math.Cos(desiredInclination * Math.PI / 180) / Math.Cos(latitudeRadians);
            double desiredSurfaceAngle;
            if (Math.Abs(cosDesiredSurfaceAngle) > 1.0)
            {
                if (Math.Abs(desiredInclination) < 90) desiredSurfaceAngle = 0;
                else desiredSurfaceAngle = Math.PI;
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
            if (mode == AscentMode.DISENGAGED) return;

            if (mode == AscentMode.ON_PAD && Staging.CurrentStage <= Staging.lastStage) mode = AscentMode.VERTICAL_ASCENT;

            driveAutoStaging();

            switch (mode)
            {
                case AscentMode.VERTICAL_ASCENT:
                    driveVerticalAscent(s);
                    break;

                case AscentMode.GRAVITY_TURN:
                    driveGravityTurn(s);
                    break;

                case AscentMode.COAST_TO_APOAPSIS:
                    driveCoastToApoapsis(s);
                    break;

                case AscentMode.CIRCULARIZE:
                    driveCircularizationBurn(s);
                    break;
            }


            driveLimitOverheat(s);

            driveRendezvousStuff(s);

            driveUpdateStats(s);
        }


        //auto-stage when safe
        private void driveAutoStaging()
        {
            //if autostage enabled, and if we are not waiting on the pad, and if we didn't immediately just fire the previous stage
            if (autoStage && mode != AscentMode.ON_PAD && Staging.CurrentStage > 0 && Staging.CurrentStage > autoStageLimit
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

        void driveLimitOverheat(FlightCtrlState s)
        {
            //limit the throttle if something is close to overheating
            double maxTempRatio = ARUtils.maxTemperatureRatio(part.vessel);
            double tempSafetyMargin = 0.05;
            if (maxTempRatio > (1 - tempSafetyMargin) && seizeThrottle)
            {
                s.mainThrottle = Mathf.Clamp(s.mainThrottle, 0.0F, (float)((1 - maxTempRatio) / tempSafetyMargin));
            }
        }

        void driveUpdateStats(FlightCtrlState s)
        {
            if (mode != AscentMode.DISENGAGED && mode != AscentMode.ON_PAD)
            {
                totalDVExpended += vesselState.deltaT * vesselState.thrustAccel(s.mainThrottle);
                gravityLosses += vesselState.deltaT * Vector3d.Dot(-vesselState.velocityVesselSurfaceUnit, vesselState.gravityForce);
                gravityLosses -= vesselState.deltaT * Vector3d.Dot(vesselState.velocityVesselSurfaceUnit, vesselState.up * vesselState.radius * Math.Pow(1 / part.vessel.mainBody.rotationPeriod, 2));
                steeringLosses += vesselState.deltaT * vesselState.thrustAccel(s.mainThrottle) * (1 - Vector3.Dot(vesselState.velocityVesselSurfaceUnit, vesselState.forward));
                dragLosses += vesselState.deltaT * ARUtils.computeDragAccel(vesselState.CoM, vesselState.velocityVesselOrbit, vesselState.massDrag / vesselState.mass, part.vessel.mainBody).magnitude;

                double netVelocity = totalDVExpended - gravityLosses - steeringLosses - dragLosses;

                double targetCircularPeriod = 2 * Math.PI * Math.Sqrt(Math.Pow(part.vessel.mainBody.Radius + desiredOrbitAltitude, 3) / part.vessel.mainBody.gravParameter);
                double longitudeTraversed = (vesselState.longitude - launchLongitude) + 360 * (vesselState.time - launchTime) / part.vessel.mainBody.rotationPeriod;
                launchPhaseAngle = ARUtils.clampDegrees(360 * (vesselState.time - launchTime) / targetCircularPeriod - longitudeTraversed);
            }
        }

        void driveRendezvousStuff(FlightCtrlState s)
        {
            if (mode == AscentMode.ON_PAD && timeIgnitionForRendezvous && rendezvousTarget != null)
            {
                double phaseAngle = ARUtils.clampDegrees(vesselState.longitude - part.vessel.mainBody.GetLongitude(rendezvousTarget.transform.position));
                double[] warpLookaheadTimes = new double[] { 10, 15, 20, 25, 50, 100, 1000, 10000 };

                double angleDifference = ARUtils.clampDegrees(phaseAngle - predictedLaunchPhaseAngle);
                double angleRate = 360.0 / rendezvousTarget.orbit.period - 360 / part.vessel.mainBody.rotationPeriod;
                rendezvousIgnitionCountdown = angleDifference / angleRate;


                if (rendezvousIgnitionCountdown < 0.0 && rendezvousIgnitionCountdown > -1.0)
                {
                    mode = AscentMode.VERTICAL_ASCENT;
                    Staging.ActivateNextStage();
                }
                else
                {
                    if (rendezvousIgnitionCountdown < 0)
                    {
                        rendezvousIgnitionCountdown = rendezvousTarget.orbit.period / 2;
                        rendezvousIgnitionCountdownHolding = true;
                    }
                    else
                    {
                        rendezvousIgnitionCountdownHolding = false;
                    }
                    core.warpTo(this, rendezvousIgnitionCountdown, warpLookaheadTimes);
                }
            }

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

        void driveVerticalAscent(FlightCtrlState s)
        {
            //during the vertical ascent we just thrust straight up at max throttle
            if (seizeThrottle) s.mainThrottle = efficientThrottle();
            if (vesselState.altitudeASL > gravityTurnStartAltitude) mode = AscentMode.GRAVITY_TURN;
            if (seizeThrottle && part.vessel.orbit.ApA > desiredOrbitAltitude)
            {
                //lastAccelerationTime = vesselState.time;
                mode = AscentMode.COAST_TO_APOAPSIS;
            }

            core.attitudeTo(Vector3d.up, MechJebCore.AttitudeReference.SURFACE_NORTH, this);
        }



        void driveGravityTurn(FlightCtrlState s)
        {
            //stop the gravity turn when our apoapsis reaches the desired altitude
            if (seizeThrottle && part.vessel.orbit.ApA > desiredOrbitAltitude)
            {
                mode = AscentMode.COAST_TO_APOAPSIS;
                //lastAccelerationTime = vesselState.time;
                core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.ORBIT, this);
                return;
            }

            if (vesselState.altitudeASL < gravityTurnStartAltitude)
            {
                mode = AscentMode.VERTICAL_ASCENT;
                core.attitudeTo(Vector3d.up, MechJebCore.AttitudeReference.SURFACE_NORTH, this);
                return;
            }

            if (seizeThrottle)
            {
                float gentleThrottle = throttleToRaiseApoapsis(part.vessel.orbit.ApR, desiredOrbitAltitude + part.vessel.mainBody.Radius);
                if (gentleThrottle < 1.0F)
                {
                    //when we are bringing down the throttle to make the apoapsis accurate, we're liable to point in weird
                    //directions because thrust goes down and so "difficulty" goes up. so just burn prograde
                    s.mainThrottle = gentleThrottle;
                    core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.ORBIT, this);
                    return;
                }
                else
                {
                    s.mainThrottle = efficientThrottle();
                }
            }


            //transition gradually from the rotating to the non-rotating reference frame. this calculation ensures that
            //when our maximum possible apoapsis, given our orbital energy, is desiredOrbitalRadius, then we are
            //fully in the non-rotating reference frame and thus doing the correct calculations to get the right inclination
            double GM = vesselState.localg * vesselState.radius * vesselState.radius;
            double potentialDifferenceWithApoapsis = GM / vesselState.radius - GM / (part.vessel.mainBody.Radius + desiredOrbitAltitude);
            double verticalSpeedForDesiredApoapsis = Math.Sqrt(2 * potentialDifferenceWithApoapsis);
            //double referenceFrameBlend = Mathf.Clamp((float)(1.5 * vesselState.speedOrbital / verticalSpeedForDesiredApoapsis - 0.5), 0.0F, 1.0F);
            double referenceFrameBlend = Mathf.Clamp((float)(vesselState.speedOrbital / verticalSpeedForDesiredApoapsis), 0.0F, 1.0F);

            Vector3d actualVelocityUnit = ((1 - referenceFrameBlend) * vesselState.velocityVesselSurfaceUnit
                                               + referenceFrameBlend * vesselState.velocityVesselOrbitUnit).normalized;

            //minus sign is there because somewhere a sign got flipped in integrating with MechJeb
            double surfaceAngle = -desiredSurfaceAngle(vesselState.latitude * Math.PI / 180.0, referenceFrameBlend);
            Vector3d desiredSurfaceHeading = Math.Cos(surfaceAngle) * vesselState.east + Math.Sin(surfaceAngle) * vesselState.north;
            double desPitch = desiredPitch(vesselState.altitudeASL);
            //double desPitch = 90;
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
            if (desPitch > 80) maxOffset = (90 - desPitch) * Math.PI / 180;
            if (steerOffset.magnitude > maxOffset) steerOffset = maxOffset * steerOffset.normalized;
            desiredThrustVector += steerOffset;
            desiredThrustVector = desiredThrustVector.normalized;

            core.attitudeTo(desiredThrustVector, MechJebCore.AttitudeReference.INERTIAL, this);
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

        void driveCoastToApoapsis(FlightCtrlState s)
        {
            s.mainThrottle = 0.0F;

            if (Vector3d.Dot(vesselState.velocityVesselOrbit, vesselState.up) < 0) //if we have started to descend, i.e. reached apoapsis, circularize
            {
                mode = AscentMode.CIRCULARIZE;
                if (TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(0, false);
                return;
            }

            if (part.vessel.orbit.ApA < desiredOrbitAltitude - 1000.0)
            {
                mode = AscentMode.GRAVITY_TURN;
                core.attitudeTo(Vector3.forward, MechJebCore.AttitudeReference.ORBIT, this);
                return;
            }


            //if we are running physics, we can do burns if the apoapsis falls, and we can maintain our orientation in preparation for circularization
            //if our Ap is too low and we are pointing reasonably along our velocity
            double[] lookaheadTimes = new double[] { 0, 5, 90, 90, 120, 200, 500, 5000 };

            if (part.vessel.orbit.ApA < desiredOrbitAltitude - 10)
            {
                if (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate)
                {
                    //lastAccelerationTime = vesselState.time;
                    core.attitudeTo(Vector3.forward, MechJebCore.AttitudeReference.ORBIT, this);

                    if (Vector3d.Dot(vesselState.forward, vesselState.velocityVesselOrbitUnit) > 0.75)
                    {
                        s.mainThrottle = throttleToRaiseApoapsis(part.vessel.orbit.ApR, desiredOrbitAltitude + part.vessel.mainBody.Radius);
                    }
                    else
                    {
                        s.mainThrottle = 0.0F;
                    }
                    return;
                }
                else
                {
                    core.warpPhysics(this);
                }
            }
            else if (autoWarpToApoapsis)
            {
                //if we're allowed to autowarp, do so
                core.warpTo(this, part.vessel.orbit.timeToAp, lookaheadTimes);
            }

            //if we are out of the atmosphere and have a high enough apoapsis and aren't near apoapsis, play dead to 
            //prevent acceleration from interfering with time warp. otherwise point in the direction that we will
            //want to point at the start of the circularization burn
            if (autoWarpToApoapsis
                && vesselState.altitudeASL > part.vessel.mainBody.maxAtmosphereAltitude
                && part.vessel.orbit.timeToAp > lookaheadTimes[2]
                && part.vessel.orbit.ApA > desiredOrbitAltitude - 10)
            {
                //don't steer
                core.attitudeDeactivate(this);
            }
            else
            {
                double expectedApoapsisThrottle = circularizationBurnThrottle(expectedSpeedAtApoapsis(), circularSpeedAtRadius(part.vessel.orbit.ApR));
                double desiredThrustPitch = thrustPitchForZeroVerticalAcc(part.vessel.orbit.ApR, expectedSpeedAtApoapsis(),
                                                                          vesselState.thrustAccel(expectedApoapsisThrottle), 0);
                Vector3d groundHeading = Vector3d.Exclude(vesselState.up, part.vessel.orbit.GetVel()).normalized;
                Vector3d desiredThrustVector = (Math.Cos(desiredThrustPitch) * groundHeading + Math.Sin(desiredThrustPitch) * vesselState.up);
                core.attitudeTo(desiredThrustVector, MechJebCore.AttitudeReference.INERTIAL, this);
                return;
            }
        }



        void driveCircularizationBurn(FlightCtrlState s)
        {
            if (part.vessel.orbit.ApA < desiredOrbitAltitude - 1000.0)
            {
                mode = AscentMode.GRAVITY_TURN;
                core.attitudeTo(Vector3.forward, MechJebCore.AttitudeReference.ORBIT, this);
                return;
            }

            double orbitalRadius = (vesselState.CoM - part.vessel.mainBody.position).magnitude;
            double circularSpeed = Math.Sqrt(orbitalRadius * vesselState.localg);

            //we start the circularization burn at apoapsis, and the orbit is about as circular as it is going to get when the 
            //periapsis has moved closer to us than the apoapsis
            if (Math.Min(part.vessel.orbit.timeToPe, part.vessel.orbit.period - part.vessel.orbit.timeToPe) <
                Math.Min(part.vessel.orbit.timeToAp, part.vessel.orbit.period - part.vessel.orbit.timeToAp)
                || vesselState.speedOrbital > circularSpeed + 1.0) //makes sure we don't keep burning forever if we are somehow way off course
            {
                predictedLaunchPhaseAngle = launchPhaseAngle;
                predictedLaunchPhaseAngleString = String.Format("{0:00}", predictedLaunchPhaseAngle);
                mecoTime = vesselState.time;
                s.mainThrottle = 0.0F;
                turnOffSteering();
                core.controlRelease(this);
                if (!showStats) enabled = false;
                return;
            }

            //During circularization we think in the *non-rotating* frame, but allow ourselves to discuss centrifugal acceleration
            //as the tendency for the altitude to rise because of our large horizontal velocity.
            //To get a circular orbit we need to reach orbital horizontal speed while simultaneously getting zero vertical speed.
            //We compute the thrust vector needed to get zero vertical acceleration, taking into account gravity and centrifugal
            //acceleration. If we currently have a nonzero vertical velocity we then adjust the thrust vector to bring that to zero

            s.mainThrottle = (float)circularizationBurnThrottle(vesselState.speedOrbital, circularSpeed);

            double thrustAcceleration = vesselState.thrustAccel(s.mainThrottle);

            Vector3d groundHeading = Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbit).normalized;
            double horizontalSpeed = Vector3d.Dot(groundHeading, vesselState.velocityVesselOrbit);
            double verticalSpeed = Vector3d.Dot(vesselState.up, vesselState.velocityVesselOrbit);

            //test the following modification: pitch = 0 if throttle < 1
            double desiredThrustPitch = thrustPitchForZeroVerticalAcc(orbitalRadius, horizontalSpeed, thrustAcceleration, verticalSpeed);



            //Vector3d desiredThrustVector = Math.Cos(desiredThrustPitch) * groundHeading + Math.Sin(desiredThrustPitch) * vesselState.up;
            Vector3d desiredThrustVector = Math.Cos(desiredThrustPitch) * Vector3d.forward + Math.Sin(desiredThrustPitch) * Vector3d.up;
            core.attitudeTo(desiredThrustVector, MechJebCore.AttitudeReference.ORBIT_HORIZONTAL, this);
        }





        ///////////////////////////////////////////////
        // GUI ////////// /////////////////////////////
        ///////////////////////////////////////////////    


        public override void drawGUI(int baseWindowID)
        {
            GUI.skin = HighLogic.Skin;
            if (minimized)
            {
                windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, "Ascent", GUILayout.Width(100), GUILayout.Height((choosingRendezvousTarget ? 600 : 30)));
            }
            else
            {
                windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, "Ascent Autopilot", GUILayout.Width(200), GUILayout.Height(100));
                if (showPathWindow)
                {
                    pathWindowPos = GUILayout.Window(baseWindowID + 1, pathWindowPos, PathWindowGUI, "Ascent Path", GUILayout.Width(300), GUILayout.Height(100));
                }
                if (showStats)
                {
                    statsWindowPos = GUILayout.Window(baseWindowID + 2, statsWindowPos, StatsWindowGUI, "Ascent Stats", GUILayout.Width(225), GUILayout.Height(250));
                }
            }
            if (showHelpWindow)
            {
                helpWindowPos = GUILayout.Window(baseWindowID + 3, helpWindowPos, HelpWindowGUI, "Ascent Autopilot Help", GUILayout.Width(400), GUILayout.Height(500));
            }

        }

        protected override void WindowGUI(int windowID)
        {

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            GUIStyle s = new GUIStyle(GUI.skin.button);
            if (mode == AscentMode.DISENGAGED)
            {
                s.normal.textColor = Color.green;
                if (GUILayout.Button("Engage", s))
                {
                    initializeInternals();
                    core.controlClaim(this);
                    mode = AscentMode.ON_PAD;
                }
            }
            else
            {
                s.normal.textColor = Color.red;
                if (GUILayout.Button("Disengage", s))
                {
                    core.controlRelease(this);
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
                    desiredOrbitAltitude = ARUtils.doGUITextInput("Orbit altitude: ", 100.0F, desiredOrbitAltitudeKmString, 30.0F, "km", 30.0F, out desiredOrbitAltitudeKmString, desiredOrbitAltitude, 1000.0);
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
                        mode = AscentMode.COAST_TO_APOAPSIS;
                        desiredOrbitAltitude = part.vessel.orbit.ApA;
                    }
                }

                GUIStyle angleStyle = new GUIStyle(GUI.skin.button);
                angleStyle.padding.top = angleStyle.padding.bottom = 3;

                if (headingNotInc)
                {
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Heading:", angleStyle, GUILayout.Width(100.0F)))
                    {
                        headingNotInc = false;
                    }

                    double parsedHeading;
                    if (Double.TryParse(launchHeadingString, out parsedHeading))
                    {
                        if (parsedHeading != launchHeading)
                        {
                            launchHeading = parsedHeading;
                            double launchSurfaceAngle = ARUtils.clampDegrees(90 - launchHeading);
                            desiredInclination = 180 / Math.PI * Math.Sign(launchSurfaceAngle) *
                                  Math.Acos(Math.Cos(launchSurfaceAngle * Math.PI / 180) * Math.Cos(vesselState.latitude * Math.PI / 180));
                            desiredInclinationString = String.Format("{0:0}", desiredInclination);
                        }
                    }
                    launchHeadingString = GUILayout.TextField(launchHeadingString, GUILayout.MinWidth(30.0F));
                    GUILayout.Label("°", GUILayout.Width(30.0F));
                    GUILayout.EndHorizontal();

                }
                else
                {
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                    double parsedInc;
                    if (Double.TryParse(desiredInclinationString, out parsedInc)) desiredInclination = parsedInc;

                    if (GUILayout.Button("Inclination:", angleStyle, GUILayout.Width(100.0F)))
                    {
                        headingNotInc = true;
                        double cosSurfaceAngle = Math.Cos(desiredInclination * Math.PI / 180) / Math.Cos(vesselState.latitude * Math.PI / 180);
                        cosSurfaceAngle = Mathf.Clamp((float)cosSurfaceAngle, -1.0F, 1.0F);
                        double surfaceAngle = 180 / Math.PI * Math.Sign(desiredInclination) * Math.Acos(cosSurfaceAngle);
                        launchHeading = (360 + 90 - surfaceAngle) % 360;
                        launchHeadingString = String.Format("{0:0}", launchHeading);
                    }

                    desiredInclinationString = GUILayout.TextField(desiredInclinationString, GUILayout.MinWidth(30.0F));
                    GUILayout.Label("°", GUILayout.Width(30.0F));
                    GUILayout.EndHorizontal();

                    //desiredInclination = ARUtils.doGUITextInput("Orbit inclination: ", 100.0F, desiredInclinationString, 30.0F, "°", 30.0F,
                    //                                            out desiredInclinationString, desiredInclination);
                }
                seizeThrottle = GUILayout.Toggle(seizeThrottle, "Auto-throttle?");

                autoStage = GUILayout.Toggle(autoStage, "Auto-stage?");

                if (autoStage)
                {
                    autoStageDelay = ARUtils.doGUITextInput("Stage delay: ", 100.0F, autoStageDelayString, 30.0F, "s", 30.0F, out autoStageDelayString, autoStageDelay);

                    autoStageLimit = ARUtils.doGUITextInput("Stop at stage #", 100.0F, autoStageLimitString, 30.0F, "", 30.0F, out autoStageLimitString, autoStageLimit);
                }

                bool newAutoWarp = GUILayout.Toggle(autoWarpToApoapsis, "Auto-warp toward apoapsis?", new GUIStyle(GUI.skin.toggle));
                if (autoWarpToApoapsis && !newAutoWarp && TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(0, true);
                autoWarpToApoapsis = newAutoWarp;


                GUILayout.BeginHorizontal();
                GUIStyle stateStyle = new GUIStyle(GUI.skin.label);
                stateStyle.normal.textColor = Color.green;
                if (mode == AscentMode.DISENGAGED) stateStyle.normal.textColor = Color.red;
                GUILayout.Label("State: ", GUILayout.Width(60));
                GUILayout.Label(modeStrings[(int)mode], stateStyle);

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();

                showPathWindow = GUILayout.Toggle(showPathWindow, "Edit path", new GUIStyle(GUI.skin.button));

                showStats = GUILayout.Toggle(showStats, "Stats", new GUIStyle(GUI.skin.button));

                GUILayout.EndHorizontal();

                if (part.vessel.Landed)
                {
                    timeIgnitionForRendezvous = GUILayout.Toggle(timeIgnitionForRendezvous, "Time launch to rendezvous?");
                    if (timeIgnitionForRendezvous)
                    {
                        predictedLaunchPhaseAngle = ARUtils.doGUITextInput("Known LPA:", 100.0F, predictedLaunchPhaseAngleString, 30.0F, "°", 30.0F, out predictedLaunchPhaseAngleString, predictedLaunchPhaseAngle);

                        GUILayout.Label("Target vessel: " + (rendezvousTarget == null ? "none" : rendezvousTarget.vesselName));

                        choosingRendezvousTarget = GUILayout.Toggle(choosingRendezvousTarget, "Choose target", new GUIStyle(GUI.skin.button));

                        if (choosingRendezvousTarget)
                        {
                            rendezvousTargetScrollPos = GUILayout.BeginScrollView(rendezvousTargetScrollPos, GUILayout.Height(200));

                            GUILayout.BeginVertical();
                            foreach (Vessel v in FlightGlobals.Vessels)
                            {
                                if (v != part.vessel && !v.Landed && !v.Splashed && v.mainBody == part.vessel.mainBody)
                                {
                                    if (GUILayout.Button(v.vesselName + String.Format(" @ {0:0}km", part.vessel.mainBody.GetAltitude(v.transform.position) / 1000.0)))
                                    {
                                        rendezvousTarget = v;
                                        choosingRendezvousTarget = false;
                                    }
                                }
                            }
                            GUILayout.EndVertical();

                            GUILayout.EndScrollView();
                        }

                        if (rendezvousTarget != null)
                        {
                            if (mode == AscentMode.DISENGAGED)
                            {
                                GUILayout.Label("Engage to begin countdown");
                            }
                            else if (mode == AscentMode.ON_PAD)
                            {
                                GUILayout.Label(String.Format("T-{0:0} seconds and " + (rendezvousIgnitionCountdownHolding ? "holding" : "counting"), Math.Ceiling(rendezvousIgnitionCountdown)));
                            }
                        }
                    }
                }


            }


            GUILayout.EndVertical();

            GUI.DragWindow(); //make window draggable

        }


        private void StatsWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            String leftTimeString;
            String rightTimeString;
            if (part.vessel.Landed)
            {
                leftTimeString = "Time:";
                rightTimeString = "---";
            }
            else if (mode == AscentMode.DISENGAGED)
            {
                leftTimeString = "MECO at";
                rightTimeString = String.Format("T+{0:0} s", mecoTime - launchTime);
            }
            else
            {
                leftTimeString = "Time:";
                rightTimeString = String.Format("T+{0:0} s", vesselState.time - launchTime);
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(leftTimeString, GUILayout.ExpandWidth(true));
            GUILayout.Label(rightTimeString);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Total Δv expended:", GUILayout.ExpandWidth(true));
            GUILayout.Label(String.Format("{0:0} m/s", totalDVExpended));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("Gravity losses - {0:0.0}%", (totalDVExpended == 0 ? 0 : 100 * gravityLosses / totalDVExpended)), GUILayout.ExpandWidth(true));
            GUILayout.Label(String.Format("{0:0} m/s", gravityLosses));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("Drag losses - {0:0.0}%", (totalDVExpended == 0 ? 0 : 100 * dragLosses / totalDVExpended)), GUILayout.ExpandWidth(true));
            GUILayout.Label(String.Format("{0:0} m/s", dragLosses));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("Steering losses - {0:0.0}%", (totalDVExpended == 0 ? 0 : 100 * steeringLosses / totalDVExpended)), GUILayout.ExpandWidth(true));
            GUILayout.Label(String.Format("{0:0} m/s", steeringLosses));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("Speed gained - {0:0.0}%", (totalDVExpended == 0 ? 0 : 100 * vesselState.velocityVesselSurface.magnitude / totalDVExpended)), GUILayout.ExpandWidth(true));
            GUILayout.Label(String.Format("{0:0} m/s", vesselState.velocityVesselSurface.magnitude));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Launch mass:", GUILayout.ExpandWidth(true));
            GUILayout.Label(String.Format("{0:0.0} tons", launchMass));
            GUILayout.EndHorizontal();


            /*            GUILayout.Label(String.Format("Total Δv expended: {0:0} m/s", totalDVExpended));
                        GUILayout.Label(String.Format("Gravity losses: {0:0} m/s ({1:0.0}%)", gravityLosses, (totalDVExpended == 0 ? 0 : gravityLosses / totalDVExpended * 100)));
                        GUILayout.Label(String.Format("Drag losses: {0:0} m/s ({1:0.0}%)", dragLosses, (totalDVExpended == 0 ? 0 : dragLosses / totalDVExpended * 100)));
                        GUILayout.Label(String.Format("Steering losses: {0:0} m/s ({1:0.0}%)", steeringLosses, (totalDVExpended == 0 ? 0 : steeringLosses / totalDVExpended * 100)));
                        GUILayout.Label(String.Format("Speed gained: {0:0} m/s ({1:0.0}%)", vesselState.velocityVesselSurface.magnitude, (totalDVExpended == 0 ? 0 : vesselState.speedSurface / totalDVExpended * 100)));
                        GUILayout.Label(String.Format("Launch mass: {0:0.0} tons", launchMass));
              */
            if (mode == AscentMode.DISENGAGED && !part.vessel.Landed)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Mass to orbit:", GUILayout.ExpandWidth(true));
                GUILayout.Label(String.Format("{0:0.0} tons", vesselState.mass));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Launch phase angle:", GUILayout.ExpandWidth(true));
                GUILayout.Label(String.Format("{0:0.00}°", launchPhaseAngle));
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Current mass:", GUILayout.ExpandWidth(true));
                GUILayout.Label(String.Format("{0:0.0} tons", vesselState.mass));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Launch phase angle:", GUILayout.ExpandWidth(true));
                GUILayout.Label("...");
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUI.DragWindow();
        }


        private void PathWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            double oldGravityTurnStartAltitude = gravityTurnStartAltitude;
            double oldGravityTurnEndAltitude = gravityTurnEndAltitude;
            double oldGravityTurnShapeExponent = gravityTurnShapeExponent;
            double oldGravityTurnEndPitch = gravityTurnEndPitch;

            gravityTurnStartAltitude = ARUtils.doGUITextInput("Turn start altitude: ", 300.0F, gravityTurnStartAltitudeKmString, 30.0F, "km", 30.0F,
                                                              out gravityTurnStartAltitudeKmString, gravityTurnStartAltitude, 1000.0);

            gravityTurnEndAltitude = ARUtils.doGUITextInput("Turn end altitude: ", 300.0F, gravityTurnEndAltitudeKmString, 30.0F, "km", 30.0F,
                                                            out gravityTurnEndAltitudeKmString, gravityTurnEndAltitude, 1000.0);

            gravityTurnEndPitch = ARUtils.doGUITextInput("Final flight path angle: ", 300.0F, gravityTurnEndPitchString, 30.0F, "°", 30.0F,
                                                         out gravityTurnEndPitchString, gravityTurnEndPitch);

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


        private void HelpWindowGUI(int windowID)
        {
            helpScrollPosition = GUILayout.BeginScrollView(helpScrollPosition);
            GUILayout.Label("MechJeb's ascent autopilot will fly a ship completely automatically from the launchpad to any desired circular orbit around Kerbin. Engage the autopilot on the launchpad to place it in the \"Awaiting liftoff\" state. Launch your ship as usual and the ascent autopilot will fly it to orbit and then automatically disengage. Disengage the autopilot at any time to regain manual control of the ship.\n\n" +
"----------Flight modes----------\n\n" +
"During flight, the autopilot operates in several sequential modes. They are:\n\n" +
"Vertical Ascent - The rocket first flies straight up until it reaches a certain altitude.\n\n" +
"Gravity Turn - The rocket then gradually pitches over and starts accelerating horizontally. This phase ends when the apoapsis reaches the desired orbit altitude.\n\n" +
"Coast To Apoapsis - Then the engines turn off while the rocket and the rocket coasts until it reaches apoapsis, at the desired orbit altitude.\n\n" +
"Circularization Burn - When the apoapsis is reached, the ship executes one final burn to make the orbit circular. The ascent autopilot will automatically disengage after the circularization burn is complete. \n\n" +
"----------Inputs----------\n\n" +
"Orbit Altitude - The altitude of the circular orbit into which the ascent autopilot will insert the rocket.\n\n" +
"Inclination - The inclination of the orbit into which the autopilot will insert the rocket. Zero inclination gives a normal eastward equatorial orbit. +90 or -90 degrees inclination give a polar orbit starting out north or south, respectively. 180 degrees gives a westward equatorial orbit. Clicking \"Inclination\" will turn this input into a Heading input.\n\n" +
"Heading - The heading of the desired orbit. When launching from the equator (where KSC is located), 90 heading is an eastward equatorial orbit, 0 is a northward polar orbit, 180  southward polar orbit, and 270 a westward equatorial orbit. Clicking \"Heading\" will turn this input into an Inclination input.\n\n" +
"Auto-Stage? - By default the autopilot will automatically fire the next stage of the rocket when the current one burns out. This can be turned off by toggling \"Auto-stage?\". \n\n" +
"Auto-Warp Toward Apoapsis? - By default the autopilot will use time warp to speed the coast to apoapsis phase. This can be turned off by toggling \"Auto-warp toward apoapsis?\".\n\n" +
"Auto-Throttle? - By default the autopilot will seize exclusive control of the throttle while it is engaged. If you want to control the throttle during the vertcal ascent and gravity turn you can turn off \"Auto-throttle?\". This will replace the orbit altitude input with a display of your current apoapsis and periapsis, and a \"Circularize\" button. When your apoapsis is at the desired height, you can press \"Circularize\" and the autopilot will again take control of the throttle for the coast to apoapsis and circularization burn at the apoapsis. \n\n" +
"----------Time Launch To Rendezvous----------\n\n" +
"The ascent autopilot can time your launch to approximately rendezvous with a target vessel. Properly used, you can end up about 1km from the target immediately after the circularization burn. This will only work if the target vessel is in a circular, equatorial (0 inclination/90 heading) orbit. First, you will need to do a \"practice launch\" so that the ascent autopilot can learn the timing of the ascent for you particular rocket and the particular descired altitude. For the practice launch, don't select \"Time launch to rendezvous?\". Just enter the altitude of the target as the orbit altitude and launch as usual. At the end of each ascent (after the circularization burn), the autopilot calculates and remembers a \"launch phase angle\" (LPA). This phase angle lets the autopilot time future ascents of the same rocket to the same altitude. Note that changing the rocket, the orbit altitude or the ascent path will change the launch phase angle and you will have to do another practice launch.\n\n" +
"After the practice launch, restart the flight or start a new one with the same rocket. Leave the ascent autopilot disengaged. Select \"Time Launch To Rendezvous?\". The \"Known LPA\" field will already be filled in with the launch phase angle calculated on the previous launch. Click \"Choose target\" and then select the vessel you are trying to rendezvous with. Again, this vessel must be in a circular equatorial orbit or the rendezvous won't work well.  Now hit \"Engage\" and the ascent autopilot will begin a countdown to launch. At T-0 seconds the autopilot will automatically ignite the first stage and begin the ascent. If all goes well, the circularization burn will end near the target vessel.\n\n" +
"Note: To speed the countdown, the autopilot will use time warp. Many rockets respond poorly to time warping on the pad, and will fall apart when coming out of warp. If this is happening to your rocket, there's little you can do but try to build a sturdier rocket.\n\n" +
"----------Customizing the ascent path----------\n\n" +
"The \"Edit path\" button brings up a window that lets you customize the path the ascent will take through the atmosphere. The ascent path window includes a graph of the planned path, which will change to reflect changes in any of the parameters below.\n\n" +
"Turn Start Altitude - the height to which the rocket will ascend vertically before starting to turn over. \n\n" +
"Turn End Altitude - the height at which the rocket stops pitching over, above which the rocket will maintain the \"Final flight path angle\"\n\n" +
"Final Flight Path Angle - The flight path angle is the angle the velocity vector makes with the horizon. The Final Flight Path Angle is the flight path angle the rocket will maintain above the Turn End Altitude.\n\n" +
"Turn Shape - This slider adjusts a continuous parameter describing the shape of the pitchover trajectory. Try varying it to see its effect on the path.\n\n" +
"----------Ascent stats----------\n\n" +
"The \"Stats\" button brings up a window with some statistics about the ascent. \n\n" +
"Time - this counts up from launch and will stop counting at MECO (main engine cut-off) at the end of the circularization burn, and so can be used to time the launch.\n\n" +
"Total Δv Expended - a measure of the fuel cost of the launch. For a given rocket ascending to a given orbit, a more efficient ascent is one that expends less Δv. \"Gravity losses\", \"drag losses\", \"steering losses\", and \"speed gained\" break down what the Δv was expended on.\n\n" +
"Gravity Losses - a measure of how much thrust was spent fighting against gravity instead of accelerating the rocket. Gravity losses are high when the rocket is travelling straight up and zero when the rocket is travelling horizontally.\n\n" +
"Drag Losses - a measure of how much thrust was spent fighting against atmospheric drag instead of accelerating the rocket. Drag losses are high when the rocket is travelling fast in the dense lower atmosphere and low when the rocket is travelling slowly or is above the dense lower air. \n\n" +
"Steering Losses - a measure of how much thrust was spent turning the rocket instead of accelerating it. Steering losses are high when the rocket is thrusting at a significant angle to its current velocity, and zero when it is thrusting parallel to its current velocity.\n\n" +
"Speed Gained - the net acceleration of the rocket during the launch. Note that this is computed in the rotating reference frame of the planet.\n\n" +
"Launch Mass - the mass the rocket had at launch.\n\n" +
"Current Mass/Mass to Orbit - the current mass of the rocket. Mass to orbit is another measure of the ascent's efficiency: a more efficient ascent of the same rocket will bring a greater mass to orbit, since less fuel will have been burned. \n\n" +
"Launch phase angle - If the rocket had been launched this angle ahead of a another vessel that was already in the target orbit, the two ships would meet at the end of the circularization burn. This angle is used in timing launches to achieve orbital rendezvous.\n\n" +
"----------Ascent from the Mun----------\n\n" +
"The ascent autopilot can be used for ascent from the Mun, as follows. With the autopilot disengaged, first enter the desired orbit parameters and edit the ascent path to one reasonable for launching in a vacuum. As a suggestion, try a turn start altitude of 0km, a turn end altitude of 5km, and a final flight path angle of 0 degrees. This launch profile turns horizontal to thrust for orbit almost immediately, since there is no atmosphere to climb above.  When you are ready, engage the autopilot and it will immediately begin the ascent to munar orbit."
);
            GUILayout.EndScrollView();
            GUI.DragWindow();
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



        private void initializeInternals()
        {
            mode = AscentMode.DISENGAGED;
            //lastAccelerationTime = 0;
            //lastAttemptedWarpIndex = 0;
            lastStageTime = 0;
        }


        ///////////////////////////////////////
        // VESSEL HISTORY UPDATES ///////////// 
        ///////////////////////////////////////


        public override void onLiftOff()
        {
            launchTime = vesselState.time;
            launchLongitude = vesselState.longitude;
            launchMass = vesselState.mass;
        }


        public override void onPartStart()
        {
            initializeInternals();
            minimized = true; //will be set to false if we start on the pad
        }

        public override void onFlightStart()
        {
            updatePathTexture();
            part.vessel.orbitDriver.OnReferenceBodyChange += new OrbitDriver.CelestialBodyDelegate(this.handleReferenceBodyChange);
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
    }





}