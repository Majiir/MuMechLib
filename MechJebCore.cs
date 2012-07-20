using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using MuMech;
using System.Reflection;
using SharpLua;
using SharpLua.LuaTypes;

namespace MuMech
{
    public class VesselStateKeeper
    {
        public List<MechJebCore> jebs = new List<MechJebCore>();
        public MechJebCore controller = null;
        public VesselState state = new VesselState();
    }

    public class MechJebCore
    {
        public float lastThrottle;

        public enum AttitudeReference
        {
            INERTIAL,          //world coordinate system.
            ORBIT,             //forward = prograde, left = normal plus, up = radial plus
            ORBIT_HORIZONTAL,  //forward = surface projection of orbit velocity, up = surface normal
            SURFACE_NORTH,     //forward = north, left = west, up = surface normal
            SURFACE_VELOCITY,  //forward = surface frame vessel velocity, up = perpendicular component of surface normal
            TARGET,             //forward = toward target, up = perpendicular component of vessel heading
            RELATIVE_VELOCITY, //forward = toward relative velocity direction, up = tbd
            TARGET_ORIENTATION //forwad = direction target is facing, up = target up
        }

        public enum TargetType
        {
            NONE,
            VESSEL,
            BODY
        }

        public enum TMode
        {
            OFF,
            KEEP_ORBITAL,
            KEEP_SURFACE,
            KEEP_VERTICAL,
            DIRECT
        }

        public enum WindowStat
        {
            HIDDEN,
            MINIMIZED,
            NORMAL,
            OPENING,
            CLOSING
        }

        private TMode prev_tmode = TMode.OFF;
        private TMode _tmode = TMode.OFF;
        public TMode tmode
        {
            get
            {
                return _tmode;
            }
            set
            {
                if (_tmode != value)
                {
                    prev_tmode = _tmode;
                    _tmode = value;
                    tmode_changed = true;
                }
            }
        }

        public static Dictionary<Vessel, VesselStateKeeper> allJebs = new Dictionary<Vessel, VesselStateKeeper>();

        public List<ComputerModule> modules = new List<ComputerModule>();

        protected ComputerModule _controlModule = null;
        public ComputerModule controlModule
        {
            get
            {
                return _controlModule;
            }
        }

        public MechJebModuleAutom8 autom8 = null;

        public VesselState vesselState;

        public Part part;

        private static bool calibrationMode = false;
        private static int windowIDbase = 60606;
        private int calibrationTarget = 0;
        private double[] calibrationDeltas = { 0.00001, 0.00005, 0.0001, 0.0005, 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10, 50, 100, 500, 1000, 5000 };
        private int calibrationDelta = 0;

        public static SettingsManager settings = null;
        public bool settingsLoaded = false;
        public bool settingsChanged = false;
        public bool gamePaused = false;
        public bool firstDraw = true;
        public float stress = 0;

        // SAS
        private bool flyByWire = false;
        private bool tmode_changed = false;
        private Vector3d integral = Vector3d.zero;
        private Vector3d prev_err = Vector3d.zero;
        private Vector3 act = Vector3.zero;
        private Vector3d k_integral = Vector3d.zero;
        private Vector3d k_prev_err = Vector3d.zero;
        private double t_integral = 0;
        private double t_prev_err = 0;
        public float Kp = 10000.0F;
        public float Ki = 0.0F;
        public float Kd = 0.0F;
        public float t_Kp = 0.05F;
        public float t_Ki = 0.000001F;
        public float t_Kd = 0.05F;
        public float r_Kp = 0;
        public float r_Ki = 0;
        public float r_Kd = 0;
        public float Damping = 3.0F;

        public float trans_spd_act = 0;
        public float trans_prev_thrust = 0;
        public bool trans_kill_h = false;
        public bool trans_land = false;
        public bool trans_land_gears = false;
        public double trans_land_touchdown_speed = 0.5;

        public bool liftedOff = true;

        public TimeWarp timeWarp;
        double warpIncreaseAttemptTime = 0;

        public bool attitudeKILLROT = false;

        protected bool attitudeChanged = false;
        protected bool _attitudeActive = false;
        public bool attitudeActive
        {
            get
            {
                return _attitudeActive;
            }
        }

        protected AttitudeReference _oldAttitudeReference = AttitudeReference.INERTIAL;
        protected AttitudeReference _attitudeReference = AttitudeReference.INERTIAL;
        public AttitudeReference attitudeReference
        {
            get
            {
                return _attitudeReference;
            }
            set
            {
                if (_attitudeReference != value)
                {
                    _oldAttitudeReference = _attitudeReference;
                    _attitudeReference = value;
                    attitudeChanged = true;
                }
            }
        }

        protected Quaternion _oldAttitudeTarget = Quaternion.identity;
        protected Quaternion _lastAttitudeTarget = Quaternion.identity;
        protected Quaternion _attitudeTarget = Quaternion.identity;
        public Quaternion attitudeTarget
        {
            get
            {
                return _attitudeTarget;
            }
            set
            {
                if (Math.Abs(Vector3d.Angle(_lastAttitudeTarget * Vector3d.forward, value * Vector3d.forward)) > 10)
                {
                    _oldAttitudeTarget = _attitudeTarget;
                    _lastAttitudeTarget = value;
                    attitudeChanged = true;
                }
                _attitudeTarget = value;
            }
        }

        protected bool _attitudeRollMatters = false;
        public bool attitudeRollMatters
        {
            get
            {
                return _attitudeRollMatters;
            }
        }

        public bool targetChanged = false;
        protected TargetType _targetType = TargetType.NONE;
        public TargetType targetType
        {
            get
            {
                return _targetType;
            }
            set
            {
                if (_targetType != value)
                {
                    _targetType = value;
                    targetChanged = true;
                    if (attitudeReference == AttitudeReference.TARGET)
                    {
                        attitudeChanged = true;
                    }
                }
            }
        }

        protected Vessel _targetVessel = null;
        public Vessel targetVessel
        {
            get
            {
                return _targetVessel;
            }
            set
            {
                if (_targetVessel != value)
                {
                    _targetVessel = value;
                    if (targetType == TargetType.VESSEL)
                    {
                        targetChanged = true;
                        if (attitudeReference == AttitudeReference.TARGET)
                        {
                            attitudeChanged = true;
                        }
                    }
                }
            }
        }

        protected CelestialBody _targetBody = null;
        public CelestialBody targetBody
        {
            get
            {
                return _targetBody;
            }
            set
            {
                if (_targetBody != value)
                {
                    _targetBody = value;
                    if (targetType == TargetType.BODY)
                    {
                        targetChanged = true;
                        if (attitudeReference == AttitudeReference.TARGET)
                        {
                            attitudeChanged = true;
                        }
                    }
                }
            }
        }

        // Auto Staging
        protected double lastStageTime = 0;
        public bool autoStage = false;
        public double autoStageDelay = 1.0;
        public int autoStageLimit = 0;

        // Main window
        private WindowStat _main_windowStat;
        protected WindowStat main_windowStat
        {
            get { return _main_windowStat; }
            set
            {
                if (_main_windowStat != value)
                {
                    _main_windowStat = value;
                    settingsChanged = true;
                }
            }
        }

        private float _main_windowProgr;
        protected float main_windowProgr
        {
            get { return _main_windowProgr; }
            set
            {
                if (_main_windowProgr != value)
                {
                    _main_windowProgr = value;
                    settingsChanged = true;
                }
            }
        }

        public string version = "";

        public void saveSettings()
        {
            if (!settingsLoaded)
            {
                return;
            }

            foreach (ComputerModule module in modules)
            {
                module.onSaveGlobalSettings(settings);
            }
            settings["main_windowStat"].value_integer = (int)main_windowStat;
            settings["main_windowProgr"].value_decimal = main_windowProgr;
            settings.save();
            settingsChanged = false;
        }

        public void loadSettings()
        {
            if (settings == null)
            {
                settings = new SettingsManager(KSPUtil.ApplicationRootPath + "MuMech/MechJeb.cfg");
            }

            foreach (ComputerModule module in modules)
            {
                module.onLoadGlobalSettings(settings);
            }
            main_windowStat = (WindowStat)settings["main_windowStat"].value_integer;
            main_windowProgr = (float)settings["main_windowProgr"].value_decimal;
            settingsChanged = false;
            settingsLoaded = true;
        }

        public void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
        {
            saveSettings();

            partDataCollection.Add("tmode", new KSPParseable((int)tmode, KSPParseable.Type.INT));
            partDataCollection.Add("trans_spd_act", new KSPParseable(trans_spd_act, KSPParseable.Type.FLOAT));
            partDataCollection.Add("trans_kill_h", new KSPParseable(trans_kill_h, KSPParseable.Type.BOOL));
            partDataCollection.Add("trans_land", new KSPParseable(trans_land, KSPParseable.Type.BOOL));

            foreach (ComputerModule module in modules)
            {
                module.onFlightStateSave(partDataCollection);
            }
        }

        public void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
        {
            loadSettings();

            if (parsedData.ContainsKey("tmode")) tmode = (TMode)parsedData["tmode"].value_int;
            if (parsedData.ContainsKey("trans_spd_act")) trans_spd_act = parsedData["trans_spd_act"].value_float;
            if (parsedData.ContainsKey("trans_kill_h")) trans_kill_h = parsedData["trans_kill_h"].value_bool;
            if (parsedData.ContainsKey("trans_land")) trans_land = parsedData["trans_land"].value_bool;

            foreach (ComputerModule module in modules)
            {
                module.onFlightStateLoad(parsedData);
            }
        }

        private void onFlyByWire(FlightCtrlState s)
        {
            if ((part.vessel != FlightGlobals.ActiveVessel) || (allJebs[part.vessel].controller != this))
            {
                return;
            }
            drive(s);
        }

        public bool controlClaim(ComputerModule newController, bool force = false)
        {
            if (controlModule == newController)
            {
                return true;
            }
            if (!controlRelease(newController, force))
            {
                return false;
            }

            _controlModule = newController;

            return true;
        }

        public bool controlRelease(ComputerModule controller, bool force = false)
        {
            if (controlModule != null)
            {
                controlModule.onControlLost();
            }

            _controlModule = null;

            attitudeDeactivate(controller);
            landDeactivate(controller);
            tmode = TMode.OFF;

            return true;
        }

        public Quaternion attitudeGetReferenceRotation(AttitudeReference reference)
        {
            Vector3d fwd;
            Quaternion rotRef = Quaternion.identity;
            switch (reference)
            {
                case AttitudeReference.ORBIT:
                    rotRef = Quaternion.LookRotation(vesselState.velocityVesselOrbitUnit, vesselState.up);
                    break;
                case AttitudeReference.ORBIT_HORIZONTAL:
                    rotRef = Quaternion.LookRotation(Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbitUnit), vesselState.up);
                    break;
                case AttitudeReference.SURFACE_NORTH:
                    rotRef = vesselState.rotationSurface;
                    break;
                case AttitudeReference.SURFACE_VELOCITY:
                    rotRef = Quaternion.LookRotation(vesselState.velocityVesselSurfaceUnit, vesselState.up);
                    break;
                case AttitudeReference.TARGET:
                    fwd = (((targetType == TargetType.VESSEL) ? (Vector3d)targetVessel.transform.position : targetBody.position) - vesselState.CoM).normalized;
                    rotRef = Quaternion.LookRotation(fwd, Vector3d.Cross(fwd, vesselState.leftOrbit));
                    break;
                case AttitudeReference.RELATIVE_VELOCITY:
                    fwd = (vesselState.velocityVesselOrbit - ((targetType == TargetType.VESSEL) ? targetVessel.orbit.GetVel() : targetBody.orbit.GetVel())).normalized;
                    rotRef = Quaternion.LookRotation(fwd, Vector3d.Cross(fwd, vesselState.leftOrbit));
                    break;
                case AttitudeReference.TARGET_ORIENTATION:
                    switch (targetType)
                    {
                        case TargetType.VESSEL:
                            rotRef = Quaternion.LookRotation(targetVessel.transform.up, targetVessel.transform.right);
                            break;
                        case TargetType.BODY:
                            rotRef = vesselState.rotationSurface;
                            break;
                    }
                    break;
            }
            return rotRef;
        }

        public Vector3d attitudeWorldToReference(Vector3d vector, AttitudeReference reference)
        {
            return Quaternion.Inverse(attitudeGetReferenceRotation(reference)) * vector;
        }

        public Vector3d attitudeReferenceToWorld(Vector3d vector, AttitudeReference reference)
        {
            return attitudeGetReferenceRotation(reference) * vector;
        }

        public bool attitudeTo(Quaternion attitude, AttitudeReference reference, ComputerModule controller)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller))
            {
                return false;
            }

            attitudeReference = reference;
            attitudeTarget = attitude;
            _attitudeActive = true;
            _attitudeRollMatters = true;

            return true;
        }

        public bool attitudeTo(Vector3d direction, AttitudeReference reference, ComputerModule controller)
        {
            bool ok = false;
            double ang_diff = Math.Abs(Vector3d.Angle(attitudeGetReferenceRotation(attitudeReference) * attitudeTarget * Vector3d.forward, attitudeGetReferenceRotation(reference) * direction));
            if (!attitudeActive || (ang_diff > 45))
            {
                ok = attitudeTo(Quaternion.LookRotation(direction, attitudeWorldToReference(-part.vessel.transform.forward, reference)), reference, controller);
            }
            else
            {
                ok = attitudeTo(Quaternion.LookRotation(direction, attitudeWorldToReference(attitudeReferenceToWorld(attitudeTarget * Vector3d.up, attitudeReference), reference)), reference, controller);
            }
            if (ok)
            {
                _attitudeRollMatters = false;
                return true;
            }
            else
            {
                return false;
            }

        }

        public bool attitudeTo(double heading, double pitch, double roll, ComputerModule controller) {
            Quaternion attitude = Quaternion.AngleAxis((float)heading, Vector3.up) * Quaternion.AngleAxis(-(float)pitch, Vector3.right) * Quaternion.AngleAxis(-(float)roll, Vector3.forward);
            return attitudeTo(attitude, MechJebCore.AttitudeReference.SURFACE_NORTH, controller);
        }

        public bool attitudeDeactivate(ComputerModule controller)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller))
            {
                return false;
            }

            _attitudeActive = false;
            attitudeChanged = true;

            return true;
        }

        //angle in degrees between the vessel's current pointing direction and the attitude target, ignoring roll
        public double attitudeAngleFromTarget()
        {
            return Math.Abs(Vector3d.Angle(attitudeGetReferenceRotation(attitudeReference) * attitudeTarget * Vector3d.forward, vesselState.forward));
        }
        public float distanceFromTarget()
        {
            return Vector3.Distance(((targetType == TargetType.VESSEL) ? (Vector3d)targetVessel.transform.position : targetBody.position), part.vessel.transform.position);
        }
        public Orbit targetOrbit()
        {
            return targetType == TargetType.VESSEL ? targetVessel.orbit : targetBody.orbit;
        }
        public Vector3d relativeVelocityToTarget()
        {
            return (vesselState.velocityVesselOrbit - ((targetType == TargetType.VESSEL) ? targetVessel.orbit.GetVel() : targetBody.orbit.GetVel()));
        }
        public string targetName()
        {
            return targetType == TargetType.VESSEL ? targetVessel.vesselName : targetBody.name;
        }

        public void setTarget(Vessel target)
        {
            targetVessel = target;
            targetType = TargetType.VESSEL;
        }
        public void setTarget()
        {
            targetType = TargetType.NONE;
        }

        public void setTarget(CelestialBody target)
        {
            targetBody = target;
            targetType = TargetType.BODY;
        }

        public bool landActivate(ComputerModule controller, double touchdownSpeed = 0.5)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller))
            {
                return false;
            }

            trans_land_touchdown_speed = touchdownSpeed;

            if (trans_land)
            {
                return true;
            }

            trans_land = true;
            trans_land_gears = false;
            tmode = TMode.KEEP_VERTICAL;

            return true;
        }

        public bool landDeactivate(ComputerModule controller)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller))
            {
                return false;
            }
            if (!trans_land)
            {
                return true;
            }

            trans_land = false;
            tmode = TMode.OFF;

            return true;
        }


        public bool warpIncrease(ComputerModule controller, bool instant = true, double maxRate = 100000.0)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller)) return false;

            //need to use instantaneous altitude and not the time-averaged vesselState.altitudeASL,
            //because the game freaks out really hard if you try to violate the altitude limits
            double instantAltitudeASL = (vesselState.CoM - part.vessel.mainBody.position).magnitude - part.vessel.mainBody.Radius;


            //conditions to increase warp:
            //-we are not already at max warp
            //-the most recent non-instant warp change has completed
            //-the next warp rate is not greater than maxRate
            //-we are out of the atmosphere, or the next warp rate is still a physics rate, or we are landed
            //-increasing the rate is allowed by altitude limits, or we are landed
            //-we did not increase the warp within the last 2 seconds
            if (TimeWarp.CurrentRateIndex + 1 < timeWarp.warpRates.Length
                && (TimeWarp.CurrentRate == 0 || timeWarp.warpRates[TimeWarp.CurrentRateIndex] == TimeWarp.CurrentRate)
                && timeWarp.warpRates[TimeWarp.CurrentRateIndex + 1] <= maxRate
                && (instantAltitudeASL > part.vessel.mainBody.maxAtmosphereAltitude
                    || timeWarp.warpRates[TimeWarp.CurrentRateIndex + 1] <= TimeWarp.MaxPhysicsRate
                    || part.vessel.Landed)
                && (instantAltitudeASL > timeWarp.altitudeLimits[TimeWarp.CurrentRateIndex + 1] * part.vessel.mainBody.Radius
                    || part.vessel.Landed)
                && vesselState.time - warpIncreaseAttemptTime > 2)
            {
                warpIncreaseAttemptTime = vesselState.time;
                TimeWarp.SetRate(TimeWarp.CurrentRateIndex + 1, instant);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void warpDecrease(ComputerModule controller, bool instant = true)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller)) return;

            if (TimeWarp.CurrentRateIndex > 0
                /*&& timeWarp.warpRates[TimeWarp.CurrentRateIndex] == TimeWarp.CurrentRate*/)
            {
                TimeWarp.SetRate(TimeWarp.CurrentRateIndex - 1, instant);
            }
        }

        public void warpMinimum(ComputerModule controller, bool instant = true)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller)) return;

            TimeWarp.SetRate(0, instant);
        }

        public void warpPhysics(ComputerModule controller, bool instant = true)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller)) return;

            if (timeWarp.warpRates[TimeWarp.CurrentRateIndex] <= TimeWarp.MaxPhysicsRate)
            {
                return;
            }
            else
            {
                int newIndex = TimeWarp.CurrentRateIndex;
                while (newIndex > 0 && timeWarp.warpRates[newIndex] > TimeWarp.MaxPhysicsRate) newIndex--;
                TimeWarp.SetRate(newIndex, instant);
            }
        }

        public void warpTo(ComputerModule controller, double timeLeft, double[] lookaheadTimes, double maxRate = 10000.0)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller)) return;

            if (timeLeft < lookaheadTimes[TimeWarp.CurrentRateIndex]
                || timeWarp.warpRates[TimeWarp.CurrentRateIndex] > maxRate)
            {
                warpDecrease(controller, true);
            }
            else if (TimeWarp.CurrentRateIndex < timeWarp.warpRates.Length - 1
                && lookaheadTimes[TimeWarp.CurrentRateIndex + 1] < timeLeft
                && timeWarp.warpRates[TimeWarp.CurrentRateIndex + 1] <= maxRate)
            {
                warpIncrease(controller, false, maxRate);
            }
        }

        public bool autoStageActivate(ComputerModule controller, double stagingDelay = 1.0, int stageLimit = 0)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller))
            {
                return false;
            }

            autoStage = true;
            autoStageDelay = stagingDelay;
            autoStageLimit = stageLimit;

            return true;
        }

        public bool autoStageDeactivate(ComputerModule controller)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller))
            {
                return false;
            }

            autoStage = false;

            return true;
        }

        public bool launch(ComputerModule controller)
        {
            if ((controlModule != null) && (controller != null) && (controlModule != controller))
            {
                return false;
            }

            lastStageTime = vesselState.time;
            if (Staging.CurrentStage == Staging.StageCount)
            {
                Staging.ActivateNextStage();
                return true;
            }

            return false;
        }

        private void drive(FlightCtrlState s)
        {
            stress = 0;

            if (part.State == PartStates.DEAD)
            {
                return;
            }

            if (controlModule != null)
            {
                if (controlModule.enabled)
                {
                    controlModule.drive(s);
                }
                else
                {
                    _controlModule = null;
                }
            }

            if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)
            {
                return;
            }

            driveThrust(s);
            driveAttitude(s);
            driveAutoStaging();

            if (double.IsNaN(s.mainThrottle))
            {
                print("MechJeb: caught throttle = NaN");
                s.mainThrottle = 0;
            }

            lastThrottle = s.mainThrottle;
        }

        private void driveThrust(FlightCtrlState s)
        {
            if ((tmode != TMode.OFF) && (vesselState.thrustAvailable > 0))
            {
                double spd = 0;

                if (trans_land)
                {
                    if (part.vessel.Landed || part.vessel.Splashed)
                    {
                        tmode = TMode.OFF;
                        trans_land = false;
                    }
                    else
                    {
                        tmode = TMode.KEEP_VERTICAL;
                        trans_kill_h = true;
                        double minalt = Math.Min(vesselState.altitudeASL, vesselState.altitudeTrue);
                        if (vesselState.maxThrustAccel < vesselState.gravityForce.magnitude)
                        {
                            trans_spd_act = 0;
                        }
                        else
                        {
                            trans_spd_act = (float)-Math.Sqrt((vesselState.maxThrustAccel - vesselState.gravityForce.magnitude) * 2 * minalt) * 0.50F;
                        }
                        if (!trans_land_gears && (minalt < 1000))
                        {
                            part.vessel.rootPart.SendEvent("LowerLeg");
                            foreach (Part p in part.vessel.parts)
                            {
                                if (p is LandingLeg)
                                {
                                    LandingLeg l = (LandingLeg)p;
                                    if (l.legState == LandingLeg.LegStates.RETRACTED)
                                    {
                                        l.DeployOnActivate = true;
                                        l.force_activate();
                                    }
                                }
                            }
                            trans_land_gears = true;
                        }
                        if (minalt < 200)
                        {
                            minalt = Math.Min(vesselState.altitudeBottom, minalt);
                            trans_spd_act = -Mathf.Lerp(0, (float)Math.Sqrt((vesselState.maxThrustAccel - vesselState.gravityForce.magnitude) * 2 * 200) * 0.50F, (float)minalt / 200);
                            trans_spd_act = (float)Math.Min(-trans_land_touchdown_speed, trans_spd_act);
                            //                            if (minalt < 3.0 * trans_land_touchdown_speed) trans_spd_act = -(float)trans_land_touchdown_speed;
                            /*
                            if ((minalt >= 90) && (vesselState.speedHorizontal > 1)) {
                                trans_spd_act = -Mathf.Lerp(0, (float)Math.Sqrt((vesselState.maxThrustAccel - vesselState.gravityForce.magnitude) * 2 * 200) * 0.90F, (float)(minalt - 100) / 300);
                            } else {
                                trans_spd_act = -Mathf.Lerp(0, (float)Math.Sqrt((vesselState.maxThrustAccel - vesselState.gravityForce.magnitude) * 2 * 200) * 0.90F, (float)minalt / 300);
                            }
                            */
                        }
                    }
                }

                switch (tmode)
                {
                    case TMode.KEEP_ORBITAL:
                        spd = vesselState.speedOrbital;
                        break;
                    case TMode.KEEP_SURFACE:
                        spd = vesselState.speedSurface;
                        break;
                    case TMode.KEEP_VERTICAL:
                        spd = vesselState.speedVertical;
                        Vector3d rot = Vector3d.up;
                        if (trans_kill_h)
                        {
                            Vector3 hsdir = Vector3.Exclude(vesselState.up, part.vessel.orbit.GetVel() - part.vessel.mainBody.getRFrmVel(vesselState.CoM));
                            Vector3 dir = -hsdir + vesselState.up * Math.Max(Math.Abs(spd), 20 * part.vessel.mainBody.GeeASL);
                            if ((Math.Min(vesselState.altitudeASL, vesselState.altitudeTrue) > 5000) && (hsdir.magnitude > Math.Max(Math.Abs(spd), 100 * part.vessel.mainBody.GeeASL) * 2))
                            {
                                tmode = TMode.DIRECT;
                                trans_spd_act = 100;
                                rot = -hsdir;
                            }
                            else
                            {
                                if (spd > 0)
                                {
                                    rot = vesselState.up;
                                }
                                else
                                {
                                    rot = dir.normalized;
                                }
                            }
                            attitudeTo(rot, AttitudeReference.INERTIAL, null);
                        }
                        break;
                }

                double t_err = (trans_spd_act - spd) / vesselState.maxThrustAccel;
                t_integral += t_err * TimeWarp.fixedDeltaTime;
                double t_deriv = (t_err - t_prev_err) / TimeWarp.fixedDeltaTime;
                double t_act = (t_Kp * t_err) + (t_Ki * t_integral) + (t_Kd * t_deriv);
                t_prev_err = t_err;

                double int_error = attitudeActive ? Math.Abs(Vector3d.Angle(attitudeGetReferenceRotation(attitudeReference) * attitudeTarget * Vector3d.forward, vesselState.forward)) : 0;

                if ((tmode != TMode.KEEP_VERTICAL) || !trans_kill_h || (int_error < 2) || ((Math.Min(vesselState.altitudeASL, vesselState.altitudeTrue) < 1000) && (int_error < 90)))
                {
                    if (tmode == TMode.DIRECT)
                    {
                        trans_prev_thrust = s.mainThrottle = trans_spd_act / 100.0F;
                    }
                    else
                    {
                        trans_prev_thrust = s.mainThrottle = Mathf.Clamp(trans_prev_thrust + (float)t_act, 0, 1.0F);
                    }
                }
                else
                {
                    if ((int_error >= 2) && (vesselState.torqueThrustPYAvailable > vesselState.torquePYAvailable * 10))
                    {
                        trans_prev_thrust = s.mainThrottle = 0.1F;
                    }
                    else
                    {
                        trans_prev_thrust = s.mainThrottle = 0;
                    }
                }
            }
        }

        private void driveAttitude(FlightCtrlState s)
        {
            if (attitudeActive)
            {
                int userCommanding = (Mathfx.Approx(s.pitch, 0, 0.1F) ? 0 : 1) + (Mathfx.Approx(s.yaw, 0, 0.1F) ? 0 : 2) + (Mathfx.Approx(s.roll, 0, 0.1F) ? 0 : 4);
                double precision = Math.Max(0.5, Math.Min(10.0, (vesselState.torquePYAvailable + vesselState.torqueThrustPYAvailable * s.mainThrottle) * 20.0 / vesselState.MoI.magnitude));
                double int_error = attitudeActive ? Math.Abs(Vector3d.Angle(attitudeGetReferenceRotation(attitudeReference) * attitudeTarget * Vector3d.forward, vesselState.forward)) : 0;

                Quaternion target = attitudeGetReferenceRotation(attitudeReference) * attitudeTarget;
                Quaternion delta = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(part.vessel.transform.rotation) * target);
                /*
                if (Mathf.Abs(Quaternion.Angle(delta, Quaternion.identity)) > 30) {
                    delta = Quaternion.Slerp(Quaternion.identity, delta, 0.1F);
                }
                */
                Vector3d deltaEuler = new Vector3d((delta.eulerAngles.x > 180) ? (delta.eulerAngles.x - 360.0F) : delta.eulerAngles.x, -((delta.eulerAngles.y > 180) ? (delta.eulerAngles.y - 360.0F) : delta.eulerAngles.y), (delta.eulerAngles.z > 180) ? (delta.eulerAngles.z - 360.0F) : delta.eulerAngles.z);
                Vector3d err = deltaEuler * Math.PI / 180.0F;
                Vector3d torque = new Vector3d(vesselState.torquePYAvailable + vesselState.torqueThrustPYAvailable * s.mainThrottle, vesselState.torqueRAvailable, vesselState.torquePYAvailable + vesselState.torqueThrustPYAvailable * s.mainThrottle);
                if ((int_error < Math.Min(0.1, precision / 10.0)) && (Math.Abs(deltaEuler.y) < Math.Min(0.5, precision / 2.0)))
                {
                    Kp = Ki = Kd = 0;
                }
                else if ((int_error < precision) && (Math.Abs(deltaEuler.y) < precision * 5.0))
                {
                    Kp = 12;
                    Ki = 0.001F;
                    Kd = 30;
                }
                else
                {
                    Kp = 10000;
                    Ki = Kd = 0;
                    Vector3d inertia = Vector3d.Scale(MuUtils.Sign(vesselState.angularMomentum) * 1.1, Vector3d.Scale(Vector3d.Scale(vesselState.angularMomentum, vesselState.angularMomentum), MuUtils.Invert(Vector3d.Scale(torque, vesselState.MoI))));
                    err += MuUtils.Reorder(inertia, 132);
                }
                err.Scale(Vector3d.Scale(vesselState.MoI, MuUtils.Invert(torque)));

                integral += err * TimeWarp.fixedDeltaTime;
                Vector3d deriv = (err - prev_err) / TimeWarp.fixedDeltaTime;
                act = Kp * err + Ki * integral + Kd * deriv;
                prev_err = err;

                if (userCommanding != 0)
                {
                    s.killRot = false;

                    if (attitudeKILLROT)
                    {
                        attitudeTo(Quaternion.LookRotation(part.vessel.transform.up, -part.vessel.transform.forward), AttitudeReference.INERTIAL, null);
                    }
                }
                else
                {
                    s.killRot = (int_error < precision / 10.0) && (Math.Abs(deltaEuler.y) < Math.Min(0.5, precision / 2.0));
                }
                if ((userCommanding & 4) > 0)
                {
                    prev_err = new Vector3d(prev_err.x, prev_err.y, 0);
                    integral = new Vector3d(integral.x, integral.y, 0);
                    if (!attitudeRollMatters)
                    {
                        attitudeTo(Quaternion.LookRotation(attitudeTarget * Vector3d.forward, attitudeWorldToReference(-part.vessel.transform.forward, attitudeReference)), attitudeReference, null);
                        _attitudeRollMatters = false;
                    }
                }
                else
                {
                    if (!double.IsNaN(act.z)) s.roll = Mathf.Clamp(s.roll + act.z, -1.0F, 1.0F);
                }
                if ((userCommanding & 3) > 0)
                {
                    prev_err = new Vector3d(0, 0, prev_err.z);
                    integral = new Vector3d(0, 0, integral.z);
                }
                else
                {
                    if (!double.IsNaN(act.x)) s.pitch = Mathf.Clamp(s.pitch + act.x, -1.0F, 1.0F);
                    if (!double.IsNaN(act.y)) s.yaw = Mathf.Clamp(s.yaw + act.y, -1.0F, 1.0F);
                }
                stress = Mathf.Min(Mathf.Abs(act.x), 1.0F) + Mathf.Min(Mathf.Abs(act.y), 1.0F) + Mathf.Min(Mathf.Abs(act.z), 1.0F);
            }
        }

        private void driveAutoStaging()
        {
            //if autostage enabled, and if we are not waiting on the pad, and if there are stages left,
            //and if we are allowed to continue staging, and if we didn't just fire the previous stage
            if (autoStage && liftedOff && Staging.CurrentStage > 0 && Staging.CurrentStage > autoStageLimit 
                && vesselState.time - lastStageTime > autoStageDelay)
            {
                //don't decouple active or idle engines or tanks
                if (!ARUtils.inverseStageDecouplesActiveOrIdleEngineOrTank(Staging.CurrentStage - 1, part.vessel))
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

        private void main_WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            foreach (ComputerModule module in modules)
            {
                if (!module.hidden)
                {
                    bool on = module.enabled;
                    if (on != GUILayout.Toggle(on, module.getName()))
                    {
                        module.enabled = !on;
                    }
                }
            }

            if (GUILayout.Button("Online Manual"))
            {
                Application.OpenURL("http://wiki.mechjeb.com/index.php?title=Manual");
            }
            GUILayout.EndVertical();
        }

        private void drawGUI()
        {

            if ((part.State != PartStates.DEAD) && (part.vessel == FlightGlobals.ActiveVessel) && (allJebs[part.vessel].controller == this) && !gamePaused)
            {
                GUI.skin = HighLogic.Skin;

                switch (main_windowStat)
                {
                    case WindowStat.OPENING:
                        main_windowProgr += Time.fixedDeltaTime;
                        if (main_windowProgr >= 1)
                        {
                            main_windowProgr = 1;
                            main_windowStat = WindowStat.NORMAL;
                        }
                        break;
                    case WindowStat.CLOSING:
                        main_windowProgr -= Time.fixedDeltaTime;
                        if (main_windowProgr <= 0)
                        {
                            main_windowProgr = 0;
                            main_windowStat = WindowStat.HIDDEN;
                        }
                        break;
                }

                GUI.depth = -100;
                GUI.SetNextControlName("MechJebOpen");
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(new Vector3(0, 0, -90)), Vector3.one);
                if (GUI.Button(new Rect((-Screen.height - 100) / 2, Screen.width - 25 - (200 * main_windowProgr), 100, 25), (main_windowStat == WindowStat.HIDDEN) ? "/\\ MechJeb /\\" : "\\/ MechJeb \\/"))
                {
                    if (main_windowStat == WindowStat.HIDDEN)
                    {
                        main_windowStat = WindowStat.OPENING;
                        main_windowProgr = 0;
                        firstDraw = true;
                    }
                    else if (main_windowStat == WindowStat.NORMAL)
                    {
                        main_windowStat = WindowStat.CLOSING;
                        main_windowProgr = 1;
                    }
                }
                GUI.matrix = Matrix4x4.identity;

                GUI.depth = -99;

                if (main_windowStat != WindowStat.HIDDEN)
                {
                    GUILayout.Window(windowIDbase, new Rect(Screen.width - main_windowProgr * 200, (Screen.height - 200) / 2, 200, 200), main_WindowGUI, "MechJeb " + version, GUILayout.Width(200), GUILayout.Height(200));
                }

                GUI.depth = -98;

                int wid = 1;
                foreach (ComputerModule module in modules)
                {
                    if (module.enabled)
                    {
                        module.drawGUI(windowIDbase + wid);
                    }
                    wid += 7;
                }

                if (firstDraw)
                {
                    GUI.FocusControl("MechJebOpen");
                    firstDraw = false;
                }
            }
        }

        public void onPartStart()
        {
            vesselState = new VesselState();

            Version v = Assembly.GetAssembly(typeof(MechJebCore)).GetName().Version;

            version = v.Major.ToString() + "." + v.Minor.ToString() + "." + v.Build.ToString();

            modules.Add(new MechJebModuleSmartASS(this));
            modules.Add(new MechJebModuleTranslatron(this));
            modules.Add(new MechJebModuleOrbitInfo(this));
            modules.Add(new MechJebModuleSurfaceInfo(this));
            modules.Add(new MechJebModuleVesselInfo(this));
            modules.Add(new MechJebModuleLandingAutopilot(this));
            modules.Add(new MechJebModuleAscentAutopilot(this));
            modules.Add(new MechJebModuleOrbitOper(this));
            //modules.Add(new MechJebModuleRendezvous(this));
            modules.Add(new MechJebModuleILS(this));

            //modules.Add(new MechJebModuleOrbitPlane(this));

            //modules.Add(new MechJebModuleJoke(this));

            //modules.Add(new MechJebModuleAscension(this));

            autom8 = new MechJebModuleAutom8(this);
            modules.Add(autom8);

            foreach (ComputerModule module in modules)
            {
                module.onPartStart();
            }
        }

        public void onFlightStart()
        {
            loadSettings();

            RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));
            firstDraw = true;

            FlightInputHandler.OnFlyByWire += new FlightInputHandler.FlightInputCallback(onFlyByWire);
            flyByWire = true;

            attitudeTo(Quaternion.LookRotation(part.vessel.transform.up, -part.vessel.transform.forward), MechJebCore.AttitudeReference.INERTIAL, null);
            _attitudeActive = false;
            attitudeChanged = false;

            timeWarp = (TimeWarp)GameObject.FindObjectOfType(typeof(TimeWarp));

            foreach (ComputerModule module in modules)
            {
                module.onFlightStart();
            }
        }

        public void onPartFixedUpdate()
        {
            if (settingsChanged)
            {
                saveSettings();
            }

            if ((part == null))
            {
                print("F part == null");
                return;
            }
            if ((part.vessel == null))
            {
                print("F part.vessel == null");
                return;
            }

            if (((part.vessel.rootPart is Decoupler) || (part.vessel.rootPart is DecouplerGUI) || (part.vessel.rootPart is RadialDecoupler)) && part.vessel.rootPart.gameObject.layer != 2)
            {
                print("Disabling collision with decoupler...");
                EditorLogic.SetLayerRecursive(part.vessel.rootPart.gameObject, 2);
            }

            if (part.vessel.orbit.objectType != Orbit.ObjectType.VESSEL)
            {
                part.vessel.orbit.objectType = Orbit.ObjectType.VESSEL;
            }

            if (!part.isConnected)
            {
                List<Part> tmp = new List<Part>(part.vessel.parts);
                foreach (Part p in tmp)
                {
                    if ((p is Decoupler) || (p is DecouplerGUI) || (p is RadialDecoupler))
                    {
                        print("Disabling collision with decoupler...");
                        EditorLogic.SetLayerRecursive(p.gameObject, 2);
                    }
                }

                tmp = new List<Part>(part.vessel.parts);
                foreach (Part p in tmp)
                {
                    p.isConnected = true;
                    if (p.State == PartStates.IDLE)
                    {
                        p.force_activate();
                    }
                }
            }

            if (!allJebs.ContainsKey(part.vessel))
            {
                allJebs[part.vessel] = new VesselStateKeeper();
                allJebs[part.vessel].controller = this;
            }

            if (!allJebs[part.vessel].jebs.Contains(this))
            {
                allJebs[part.vessel].jebs.Add(this);
            }

            if (allJebs[part.vessel].controller == this)
            {
                allJebs[part.vessel].state.Update(part.vessel);
                vesselState = allJebs[part.vessel].state;

                foreach (ComputerModule module in modules)
                {
                    module.vesselState = vesselState;
                    module.onPartFixedUpdate();
                }

                if (!liftedOff && (FlightGlobals.ActiveVessel == part.vessel) && (Staging.CurrentStage <= Staging.lastStage))
                {
                    foreach (ComputerModule module in modules)
                    {
                        module.onLiftOff();
                    }
                    liftedOff = true;
                }

                if (part.vessel == FlightGlobals.ActiveVessel)
                {
                    MechJebModuleAutom8.instance = autom8;
                }
                else
                {
                    FlightCtrlState s = new FlightCtrlState();
                    drive(s);
                    part.vessel.FeedInputFeed(s);
                }
            }

        }

        public void onPartUpdate()
        {
            if ((part == null))
            {
                print("part == null");
                return;
            }
            if ((part.vessel == null))
            {
                print("part.vessel == null");
                return;
            }

            if (!allJebs.ContainsKey(part.vessel))
            {
                allJebs[part.vessel] = new VesselStateKeeper();
                allJebs[part.vessel].controller = this;
            }
            if (!allJebs[part.vessel].jebs.Contains(this))
            {
                allJebs[part.vessel].jebs.Add(this);
            }

            foreach (ComputerModule module in modules)
            {
                module.onPartUpdate();
            }

            if (tmode_changed)
            {
                if (trans_kill_h && (tmode == TMode.OFF))
                {
                    _attitudeActive = false;
                    attitudeChanged = true;
                    trans_land = false;
                }
                t_integral = 0;
                t_prev_err = 0;
                tmode_changed = false;
                FlightInputHandler.SetNeutralControls();
            }

            if (attitudeChanged)
            {
                if (attitudeReference != AttitudeReference.INERTIAL)
                {
                    attitudeKILLROT = false;
                }
                integral = Vector3.zero;
                prev_err = Vector3.zero;
                act = Vector3.zero;

                print("MechJeb resetting PID controller.");
                attitudeChanged = false;

                foreach (ComputerModule module in modules)
                {
                    module.onAttitudeChange(_oldAttitudeReference, _oldAttitudeTarget, attitudeReference, attitudeTarget);
                }
            }

            if (calibrationMode)
            {
                bool printk = false;

                if (Input.GetKeyDown(KeyCode.KeypadDivide))
                {
                    calibrationTarget++;
                    if (calibrationTarget > 2)
                    {
                        calibrationTarget = 0;
                    }
                    switch (calibrationTarget)
                    {
                        case 0:
                            print("Calibrating K");
                            break;
                        case 1:
                            print("Calibrating r_K");
                            break;
                        case 2:
                            print("Calibrating t_K");
                            break;
                    }
                }

                if (Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    calibrationDelta -= 1;
                    if (calibrationDelta < 0)
                    {
                        calibrationDelta = calibrationDeltas.Length - 1;
                    }
                    print("Calibration delta = " + calibrationDeltas[calibrationDelta]);
                }

                if (Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    calibrationDelta += 1;
                    if (calibrationDelta >= calibrationDeltas.Length)
                    {
                        calibrationDelta = 0;
                    }
                    print("Calibration delta = " + calibrationDeltas[calibrationDelta]);
                }

                if (Input.GetKeyDown(KeyCode.Keypad7))
                {
                    switch (calibrationTarget)
                    {
                        case 0:
                            Kp += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Kp += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Kp += (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad4))
                {
                    switch (calibrationTarget)
                    {
                        case 0:
                            Kp -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Kp -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Kp -= (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad8))
                {
                    switch (calibrationTarget)
                    {
                        case 0:
                            Ki += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Ki += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Ki += (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad5))
                {
                    switch (calibrationTarget)
                    {
                        case 0:
                            Ki -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Ki -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Ki -= (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad9))
                {
                    switch (calibrationTarget)
                    {
                        case 0:
                            Kd += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Kd += (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Kd += (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad6))
                {
                    switch (calibrationTarget)
                    {
                        case 0:
                            Kd -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 1:
                            r_Kd -= (float)calibrationDeltas[calibrationDelta];
                            break;
                        case 2:
                            t_Kd -= (float)calibrationDeltas[calibrationDelta];
                            break;
                    }
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad1))
                {
                    Damping += (float)calibrationDeltas[calibrationDelta];
                    printk = true;
                }
                if (Input.GetKeyDown(KeyCode.Keypad0))
                {
                    Damping -= (float)calibrationDeltas[calibrationDelta];
                    printk = true;
                }
                if (printk)
                {
                    switch (calibrationTarget)
                    {
                        case 0:
                            print("Kp = " + Kp + " - Ki = " + Ki + " - Kd = " + Kd + " - Damp = " + Damping);
                            break;
                        case 1:
                            print("r_Kp = " + r_Kp + " - r_Ki = " + r_Ki + " - r_Kd = " + r_Kd);
                            break;
                        case 2:
                            print("t_Kp = " + t_Kp + " - t_Ki = " + t_Ki + " - t_Kd = " + t_Kd);
                            break;
                    }
                }
            }

        }

        public void onDisconnect()
        {
            foreach (ComputerModule module in modules)
            {
                module.onDisconnect();
            }

            if ((part.vessel != null) && allJebs.ContainsKey(part.vessel) && allJebs[part.vessel].jebs.Contains(this))
            {
                if (allJebs[part.vessel].controller == this)
                {
                    allJebs[part.vessel].controller = null;
                }
                allJebs[part.vessel].jebs.Remove(this);
            }
        }

        public void onPartDestroy()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartDestroy();
            }

            if ((part != null) && (part.vessel != null) && (allJebs != null) && allJebs.ContainsKey(part.vessel) && (allJebs[part.vessel].jebs != null) && allJebs[part.vessel].jebs.Contains(this))
            {
                if (allJebs[part.vessel].controller == this)
                {
                    allJebs[part.vessel].controller = null;
                }
                allJebs[part.vessel].jebs.Remove(this);
            }

            if (flyByWire)
            {
                FlightInputHandler.OnFlyByWire -= new FlightInputHandler.FlightInputCallback(onFlyByWire);
                flyByWire = false;
            }
            RenderingManager.RemoveFromPostDrawQueue(0, new Callback(drawGUI));
        }

        public void onGamePause()
        {
            gamePaused = true;
            foreach (ComputerModule module in modules)
            {
                module.onGamePause();
            }
        }

        public void onGameResume()
        {
            gamePaused = false;
            foreach (ComputerModule module in modules)
            {
                module.onGameResume();
            }
        }

        public void onActiveFixedUpdate()
        {
            foreach (ComputerModule module in modules)
            {
                module.onActiveFixedUpdate();
            }
        }

        public void onActiveUpdate()
        {
            foreach (ComputerModule module in modules)
            {
                module.onActiveUpdate();
            }
        }

        public void onBackup()
        {
            foreach (ComputerModule module in modules)
            {
                module.onBackup();
            }
        }

        public void onDecouple(float breakForce)
        {
            foreach (ComputerModule module in modules)
            {
                module.onDecouple(breakForce);
            }
        }

        public void onFlightStartAtLaunchPad()
        {
            liftedOff = false;

            foreach (ComputerModule module in modules)
            {
                module.onFlightStartAtLaunchPad();
            }
        }

        public void onPack()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPack();
            }
        }

        public bool onPartActivate()
        {
            bool ok = false;
            foreach (ComputerModule module in modules)
            {
                ok = module.onPartActivate() || ok;
            }
            return ok;
        }

        public void onPartAwake()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartAwake();
            }
        }

        public void onPartDeactivate()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartDeactivate();
            }
        }

        public void onPartDelete()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartDelete();
            }
        }

        public void onPartExplode()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartExplode();
            }
        }

        public void onPartLiftOff()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartLiftOff();
            }
        }

        public void onPartLoad()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartLoad();
            }
        }

        public void onPartSplashdown()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartSplashdown();
            }
        }

        public void onPartTouchdown()
        {
            foreach (ComputerModule module in modules)
            {
                module.onPartTouchdown();
            }
        }

        public void onUnpack()
        {
            foreach (ComputerModule module in modules)
            {
                module.onUnpack();
            }
        }

        public LuaValue proxyControlClaim(LuaValue[] arg)
        {
            return LuaBoolean.From(controlClaim(autom8, (arg.Count() > 0) ? arg[0].GetBooleanValue() : false));
        }

        public LuaValue proxyControlRelease(LuaValue[] arg)
        {
            return LuaBoolean.From(controlRelease(autom8, (arg.Count() > 0) ? arg[0].GetBooleanValue() : false));
        }

        public LuaValue proxyAttitudeGetReferenceRotation(LuaValue[] arg)
        {
            if (arg.Count() != 1)
            {
                throw new Exception("usage: attitudeGetReferenceRotation(reference)");
            }

            AttitudeReference r;
            try
            {
                r = (AttitudeReference)Enum.Parse(typeof(AttitudeReference), arg[0].ToString(), true);
            }
            catch (Exception)
            {
                throw new Exception("attitudeGetReferenceRotation: invalid reference");
            }

            return LuaUtils.QuaternionToTable(attitudeGetReferenceRotation(r));
        }

        public LuaValue proxyAttitudeWorldToReference(LuaValue[] arg)
        {
            if (arg.Count() != 2)
            {
                throw new Exception("usage: attitudeWorldToReference(vector, reference)");
            }

            Vector3d dir;
            try
            {
                dir = LuaUtils.valueToVector3d(arg[0]);
            }
            catch (Exception e)
            {
                throw new Exception("attitudeWorldToReference: invalid vector - " + e.Message);
            }

            AttitudeReference r;
            try
            {
                r = (AttitudeReference)Enum.Parse(typeof(AttitudeReference), arg[1].ToString(), true);
            }
            catch (Exception)
            {
                throw new Exception("attitudeWorldToReference: invalid reference");
            }

            return LuaUtils.Vector3dToTable(attitudeWorldToReference(dir, r));
        }

        public LuaValue proxyAttitudeReferenceToWorld(LuaValue[] arg)
        {
            if (arg.Count() != 2)
            {
                throw new Exception("usage: attitudeReferenceToWorld(vector, reference)");
            }

            Vector3d dir;
            try
            {
                dir = LuaUtils.valueToVector3d(arg[0]);
            }
            catch (Exception e)
            {
                throw new Exception("attitudeReferenceToWorld: invalid vector - " + e.Message);
            }

            AttitudeReference r;
            try
            {
                r = (AttitudeReference)Enum.Parse(typeof(AttitudeReference), arg[1].ToString(), true);
            }
            catch (Exception)
            {
                throw new Exception("attitudeReferenceToWorld: invalid reference");
            }

            return LuaUtils.Vector3dToTable(attitudeReferenceToWorld(dir, r));
        }

        public LuaValue proxyAttitudeTo(LuaValue[] arg)
        {
            if (arg.Count() != 2)
            {
                throw new Exception("usage: attitudeTo(vector, reference)");
            }

            Vector3d dir;
            try
            {
                dir = LuaUtils.valueToVector3d(arg[0]);
            }
            catch (Exception e)
            {
                throw new Exception("attitudeTo: invalid vector - " + e.Message);
            }

            AttitudeReference r;
            try
            {
                r = (AttitudeReference)Enum.Parse(typeof(AttitudeReference), arg[1].ToString(), true);
            }
            catch (Exception)
            {
                throw new Exception("attitudeTo: invalid reference");
            }

            return LuaBoolean.From(attitudeTo(dir, r, autom8));
        }

        public LuaValue proxyAttitudeDeactivate(LuaValue[] arg)
        {
            return LuaBoolean.From(attitudeDeactivate(autom8));
        }

        public LuaValue proxyAttitudeAngleFromTarget(LuaValue[] arg)
        {
            return new LuaNumber(attitudeAngleFromTarget());
        }

        public LuaValue proxyDistanceFromTarget(LuaValue[] arg)
        {
            return new LuaNumber(distanceFromTarget());
        }

        public LuaValue proxyRelativeVelocityToTarget(LuaValue[] arg)
        {
            return LuaUtils.Vector3dToTable(relativeVelocityToTarget());
        }

        public LuaValue proxyTargetName(LuaValue[] arg)
        {
            return new LuaString(targetName());
        }

        //public LuaValue proxySetTarget(LuaValue[] arg) { }

        public LuaValue proxyLandActivate(LuaValue[] arg)
        {
            return LuaBoolean.From(landActivate(autom8, ((arg.Count() > 0) && (arg[0] is LuaNumber)) ? (arg[0] as LuaNumber).Number : 0.5));
        }

        public LuaValue proxyLandDeactivate(LuaValue[] arg)
        {
            return LuaBoolean.From(landDeactivate(autom8));
        }

        public LuaValue proxyWarpIncrease(LuaValue[] arg)
        {
            return LuaBoolean.From(warpIncrease(autom8, (arg.Count() > 0) ? arg[0].GetBooleanValue() : true, ((arg.Count() > 1) && (arg[1] is LuaNumber)) ? (arg[1] as LuaNumber).Number : 10000));
        }

        public LuaValue proxyWarpDecrease(LuaValue[] arg)
        {
            warpDecrease(autom8, (arg.Count() > 0) ? arg[0].GetBooleanValue() : true);
            return LuaNil.Nil;
        }

        public LuaValue proxyWarpMinimum(LuaValue[] arg)
        {
            warpMinimum(autom8, (arg.Count() > 0) ? arg[0].GetBooleanValue() : true);
            return LuaNil.Nil;
        }

        public LuaValue proxyWarpPhysics(LuaValue[] arg)
        {
            warpPhysics(autom8, (arg.Count() > 0) ? arg[0].GetBooleanValue() : true);
            return LuaNil.Nil;
        }

        public LuaValue proxyAutoStageActivate(LuaValue[] arg)
        {
            return LuaBoolean.From(autoStageActivate(autom8, ((arg.Count() > 0) && (arg[0] is LuaNumber)) ? (arg[0] as LuaNumber).Number : 1.0, ((arg.Count() > 1) && (arg[1] is LuaNumber)) ? (int)(arg[1] as LuaNumber).Number : 0));
        }

        public LuaValue proxyAutoStageDeactivate(LuaValue[] arg)
        {
            return LuaBoolean.From(autoStageDeactivate(autom8));
        }

        public LuaValue proxyLaunch(LuaValue[] arg)
        {
            return LuaBoolean.From(launch(autom8));
        }

        public LuaValue proxyGetAttitudeActive(LuaValue[] arg)
        {
            return LuaBoolean.From(attitudeActive);
        }

        public LuaValue proxyGetControlClaimed(LuaValue[] arg)
        {
            return LuaBoolean.From(controlModule != null);
        }

        public LuaValue proxyBusy(LuaValue[] arg)
        {
            return LuaBoolean.From(attitudeActive || (controlModule != null));
        }

        public LuaValue proxyFree(LuaValue[] arg)
        {
            return LuaBoolean.From(!attitudeActive && (controlModule == null));
        }

        public void registerLuaMembers(LuaTable index)
        {
            index.Register("controlClaim", proxyControlClaim);
            index.Register("controlRelease", proxyControlRelease);
            index.Register("attitudeGetReferenceRotation", proxyAttitudeGetReferenceRotation);
            index.Register("attitudeWorldToReference", proxyAttitudeWorldToReference);
            index.Register("attitudeReferenceToWorld", proxyAttitudeReferenceToWorld);
            index.Register("attitudeTo", proxyAttitudeTo);
            index.Register("attitudeDeactivate", proxyAttitudeDeactivate);
            index.Register("attitudeAngleFromTarget", proxyAttitudeAngleFromTarget);
            index.Register("distanceFromTarget", proxyDistanceFromTarget);
            index.Register("relativeVelocityToTarget", proxyRelativeVelocityToTarget);
            index.Register("targetName", proxyTargetName);
            //index.Register("setTarget", proxySetTarget);
            index.Register("landActivate", proxyLandActivate);
            index.Register("landDeactivate", proxyLandDeactivate);
            index.Register("warpIncrease", proxyWarpIncrease);
            index.Register("warpDecrease", proxyWarpDecrease);
            index.Register("warpMinimum", proxyWarpMinimum);
            index.Register("warpPhysics", proxyWarpPhysics);
            index.Register("autoStageActivate", proxyAutoStageActivate);
            index.Register("autoStageDeactivate", proxyAutoStageDeactivate);
            index.Register("launch", proxyLaunch);

            index.Register("getAttitudeActive", proxyGetAttitudeActive);
            index.Register("getControlClaimed", proxyGetControlClaimed);

            index.Register("busy", proxyBusy);
            index.Register("free", proxyFree);

            foreach (ComputerModule m in modules)
            {
                m.registerLuaMembers(index);
            }
        }

        static void print(String s)
        {
            MonoBehaviour.print(s);
        }
    }
}
