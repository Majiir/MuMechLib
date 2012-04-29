using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/*
 * Todo:
 * 
 * Future:
 * 
 * -make small orbit adjustments more precise (redo throttle management?)
 * -make sure inclination is OK in TRANS
 * 
 * Changed:
 * 
 * -TRANS burns that miss the target won't hang the game
 * -PE and AP+PE will reject periapsis below -(planet radius)
 * 
 */

namespace MuMech
{

    public class MechJebModuleOrbitOper : ComputerModule
    {
        public MechJebModuleOrbitOper(MechJebCore core) : base(core) { }

        public override void onControlLost()
        {
            if (currentOperation != Operation.NONE)
            {
                currentOperation = Operation.NONE;
                FlightInputHandler.SetNeutralControls();
            }
        }

        public enum Operation
        {
            PERIAPSIS,
            APOAPSIS,
            ELLIPTICIZE,
            CIRCULARIZE,
            TRANSFER_INJECTION,
            WARP,
            NONE
        }

        Operation currentOperation = Operation.NONE;
        Operation guiTab = Operation.PERIAPSIS;
        public String[] guiTabStrings = new String[] { "PE", "AP", "AP+PE", "CIRC", "TRANS", "WARP" };
        public String[] guiTitleStrings = new String[] { "Change periapsis", "Change apoapsis", "Change both apsides", "Cirularize", "Transfer injection", "Time warp" };
        bool showHelpWindow = false;
        Vector2 helpScrollPosition = new Vector2();

        public enum WarpPoint { PERIAPSIS, APOAPSIS, SOI_CHANGE }
        public String[] warpPointStrings = new String[] { "Periapsis", "Apoapsis", "SoI switch" };
        public String[] warpPointStrings2 = new String[] { "periapsis", "apoapsis", "SoI switch" };
        WarpPoint warpPoint;
        double[] warpLookaheadTimes = new double[] { 0, 1, 10, 20, 25, 50, 500, 5000 };

        bool raisingApsis;

        CelestialBody transferTarget;
        bool startedTransferInjectionBurn = false;
        double postTransferPeR;

        String newPeAString = "100";
        double _newPeA = 100000.0;
        double newPeA
        {
            get { return _newPeA; }
            set
            {
                if (value != _newPeA) core.settingsChanged = true;
                _newPeA = value;
            }
        }

        String newApAString = "100";
        double _newApA = 100000.0;
        double newApA
        {
            get { return _newApA; }
            set
            {
                if (value != _newApA) core.settingsChanged = true;
                _newApA = value;
            }
        }

        String desiredPostTransferPeAString = "100";
        double _desiredPostTransferPeA = 100000.0;
        double desiredPostTransferPeA
        {
            get { return _desiredPostTransferPeA; }
            set
            {
                if (_desiredPostTransferPeA != value) core.settingsChanged = true;
                _desiredPostTransferPeA = value;
            }
        }

        String warpTimeOffsetString = "0";
        double _warpTimeOffset = 0;
        double warpTimeOffset
        {
            get { return _warpTimeOffset; }
            set
            {
                if (value != _warpTimeOffset) core.settingsChanged = true;
                _warpTimeOffset = value;
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

            newPeA = settings["OO_newPeA"].valueDecimal(100000.0);
            newPeAString = (newPeA / 1000.0).ToString();
            newApA = settings["OO_newApA"].valueDecimal(100000.0);
            newApAString = (newApA / 1000.0).ToString();
            desiredPostTransferPeA = settings["OO_desiredPostTransferPeA"].valueDecimal(100000.0);
            desiredPostTransferPeAString = (desiredPostTransferPeA / 1000.0).ToString();
            warpTimeOffset = settings["OO_warpTimeOffset"].valueDecimal(0.0);
            warpTimeOffsetString = warpTimeOffset.ToString();

            Vector4 savedHelpWindowPos = settings["OO_helpWindowPos"].valueVector(new Vector4(150, 50));
            helpWindowPos = new Rect(savedHelpWindowPos.x, savedHelpWindowPos.y, 10, 10);

        }

        public override void onSaveGlobalSettings(SettingsManager settings)
        {
            base.onSaveGlobalSettings(settings);

            settings["OO_newPeA"].value_decimal = newPeA;
            settings["OO_newApA"].value_decimal = newApA;
            settings["OO_desiredPostTransferPeA"].value_decimal = desiredPostTransferPeA;
            settings["OO_warpTimeOffset"].value_decimal = warpTimeOffset;
            settings["OO_helpWindowPos"].value_vector = new Vector4(helpWindowPos.x, helpWindowPos.y);

        }

        public override void drawGUI(int baseWindowID)
        {
            windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, getName() + " - " + guiTitleStrings[(int)guiTab], GUILayout.Width(200.0F), GUILayout.Height(100.0F));
            if (showHelpWindow)
            {
                helpWindowPos = GUILayout.Window(baseWindowID + 1, helpWindowPos, HelpWindowGUI, getName() + " - Help", GUILayout.Width(400), GUILayout.Height(500));
            }
        }

        protected override void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();


            GUIStyle normal = new GUIStyle(GUI.skin.label);
            GUIStyle yellow = new GUIStyle(GUI.skin.label);
            yellow.normal.textColor = Color.yellow;

            if (part.vessel.orbit.eccentricity < 1.0)
            {
                GUILayout.Label(String.Format("Current orbit: {0:0}km x {1:0}km, inclined {2:0.0}°", part.vessel.orbit.PeA / 1000.0, part.vessel.orbit.ApA / 1000.0, part.vessel.orbit.inclination));
            }
            else
            {
                GUILayout.Label(String.Format("Current orbit: hyperbolic, Pe = {0:0}km, inclined {1:0.0}°", part.vessel.orbit.PeA / 1000.0, part.vessel.orbit.inclination));
            }

            guiTab = (Operation)GUILayout.Toolbar((int)guiTab, guiTabStrings);

            switch (guiTab)
            {
                case Operation.PERIAPSIS:
                    newPeA = ARUtils.doGUITextInput("New periapsis: ", 250.0F, newPeAString, 50.0F, "km", 30.0F,
                                                    out newPeAString, newPeA, 1000.0);

                    if (newPeA > vesselState.altitudeASL)
                    {
                        GUILayout.Label("Periapsis cannot be above current altitude.", yellow);
                    }
                    else if (GUILayout.Button(String.Format("Burn (Δv = {0:0} m/s)", deltaVToChangePeriapsis(newPeA))))
                    {
                        core.controlClaim(this);
                        currentOperation = Operation.PERIAPSIS;
                        raisingApsis = (part.vessel.orbit.PeA < newPeA);
                    }
                    break;

                case Operation.APOAPSIS:
                    newApA = ARUtils.doGUITextInput("New apoapsis: ", 250.0F, newApAString, 50.0F, "km", 30.0F,
                                                          out newApAString, newApA, 1000.0);

                    if (newApA < vesselState.altitudeASL)
                    {
                        GUILayout.Label("Apoapsis cannot be below current altitude.", yellow);
                    }
                    else if (GUILayout.Button(String.Format("Burn (Δv = {0:0} m/s)", deltaVToChangeApoapsis(newApA))))
                    {
                        core.controlClaim(this);
                        currentOperation = Operation.APOAPSIS;
                        raisingApsis = (part.vessel.orbit.ApR > 0 && part.vessel.orbit.ApA < newApA);
                    }
                    break;



                case Operation.ELLIPTICIZE:
                    newPeA = ARUtils.doGUITextInput("New periapsis: ", 250.0F, newPeAString, 50.0F, "km", 30.0F,
                                                          out newPeAString, newPeA, 1000.0);

                    newApA = ARUtils.doGUITextInput("New apoapsis: ", 250.0F, newApAString, 50.0F, "km", 30.0F,
                                                          out newApAString, newApA, 1000.0);

                    if (newPeA > vesselState.altitudeASL)
                    {
                        GUILayout.Label("Periapsis cannot be above current altitude.", yellow);
                    }
                    else if (newApA < vesselState.altitudeASL)
                    {
                        GUILayout.Label("Apoapsis cannot be below current altitude.", yellow);
                    }
                    else if (GUILayout.Button(String.Format("Burn (Δv = {0:0} m/s)", ellipticizationVelocityCorrection(newPeA, newApA).magnitude)))
                    {
                        core.controlClaim(this);
                        currentOperation = Operation.ELLIPTICIZE;
                    }
                    break;

                case Operation.CIRCULARIZE:
                    if (GUILayout.Button(String.Format("Circularize at {0:0} km (Δv = {1:0} m/s)", vesselState.altitudeASL / 1000.0, circularizationVelocityCorrection().magnitude)))
                    {
                        core.controlClaim(this);
                        currentOperation = Operation.CIRCULARIZE;
                    }
                    break;

                case Operation.TRANSFER_INJECTION:
                    if (part.vessel.mainBody.orbitingBodies.Count > 0)
                    {
                        desiredPostTransferPeA = ARUtils.doGUITextInput("Desired final periapsis:", 250.0F, desiredPostTransferPeAString, 50.0F, "km", 30.0F,
                                                                        out desiredPostTransferPeAString, desiredPostTransferPeA, 1000.0);

                        if (transferTarget != null && postTransferPeR != -1)
                        {
                            GUILayout.Label(String.Format("Predicted periapsis after transfer to " + transferTarget.name + ": {0:0} km", (postTransferPeR - transferTarget.Radius) / 1000.0));
                        }

                        foreach (CelestialBody body in part.vessel.mainBody.orbitingBodies)
                        {
                            if (GUILayout.Button(String.Format("Transfer to " + body.name + " (Δv ≈ {0:0} m/s)", deltaVToChangeApoapsis(body.orbit.PeA))))
                            {
                                core.controlClaim(this);
                                currentOperation = Operation.TRANSFER_INJECTION;
                                startedTransferInjectionBurn = false;
                                transferTarget = body;
                            }
                        }

                        if (part.vessel.orbit.eccentricity > 1.0)
                        {
                            GUILayout.Label("Transfer injection failed. Did you start from a circular equatorial orbit?", ARUtils.labelStyle(Color.yellow));
                        }
                    }
                    else
                    {
                        GUILayout.Label("No bodies orbit " + part.vessel.mainBody.name);
                    }
                    break;


                case Operation.WARP:
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Warp to:");
                    warpPoint = (WarpPoint)GUILayout.Toolbar((int)warpPoint, warpPointStrings);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (warpPoint == WarpPoint.APOAPSIS || warpPoint == WarpPoint.PERIAPSIS)
                    {
                        warpTimeOffset = ARUtils.doGUITextInput("Lead time:", 150.0F, warpTimeOffsetString, 50.0F, "s", 30.0F,
                                                               out warpTimeOffsetString, warpTimeOffset);
                    }

                    if (GUILayout.Button("Warp"))
                    {
                        core.controlClaim(this);
                        FlightInputHandler.SetNeutralControls();
                        currentOperation = Operation.WARP;
                    }
                    GUILayout.EndHorizontal();
                    break;
            }

            GUILayout.BeginHorizontal();
            String statusString = "Idle";
            switch (currentOperation)
            {
                case Operation.PERIAPSIS:
                    statusString = String.Format((raisingApsis ? "Raising" : "Lowering") + " periapsis to {0:0} km", newPeA / 1000.0);
                    break;

                case Operation.APOAPSIS:
                    statusString = String.Format((raisingApsis ? "Raising" : "Lowering") + " apoapsis to {0:0} km", newApA / 1000.0);
                    break;

                case Operation.ELLIPTICIZE:
                    statusString = String.Format("Changing orbit to {0:0}km x {1:0}km", newPeA / 1000.0, newApA / 1000.0);
                    break;

                case Operation.CIRCULARIZE:
                    statusString = String.Format("Circularizing at {0:0} km", vesselState.altitudeASL / 1000.0);
                    break;

                case Operation.TRANSFER_INJECTION:
                    if (startedTransferInjectionBurn) statusString = "Injecting into transfer orbit to " + transferTarget.name;
                    else statusString = "Waiting for injection burn point for transfer to " + transferTarget.name;
                    break;

                case Operation.WARP:
                    String offsetString = "";
                    if (warpPoint != WarpPoint.SOI_CHANGE)
                    {
                        if (warpTimeOffset > 0) offsetString = "" + warpTimeOffset + "s before ";
                        else if (warpTimeOffset < 0) offsetString = "" + Math.Abs(warpTimeOffset) + "s after ";
                    }
                    statusString = "Warping to " + offsetString + warpPointStrings2[(int)warpPoint];
                    break;
            }
            GUILayout.Label("Status: " + statusString, GUILayout.Width(200.0F));

            GUIStyle abortStyle = (currentOperation == Operation.NONE ? ARUtils.buttonStyle(Color.gray) : ARUtils.buttonStyle(Color.red));
            if (GUILayout.Button("Abort", abortStyle))
            {
                endOperation();
            }

            showHelpWindow = GUILayout.Toggle(showHelpWindow, "?", new GUIStyle(GUI.skin.button));

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void HelpWindowGUI(int windowID)
        {
            helpScrollPosition = GUILayout.BeginScrollView(helpScrollPosition);
            GUILayout.Label("Orbit summary:\n\n" +
"At the top of the window is displayed a summary of your current orbit, showing its periapsis, apoapsis, and inclination.\n\n" +
"PE - Change periapsis:\n\n" +
"Specify a new periapsis for your orbit and hit \"Burn\", and MechJeb will automatically execute a burn to lower or raise your periapsis to the specified altitude.\n\n" +
"AP - Change apoapsis:\n\n" +
"Specify a new apoapsis for your orbit and hit \"Burn\" and MechJeb will automatically execute a burn to lower or raise your apoapsis to the speciified altitude.\n\n" +
"AP+PE - Change both apsides:\n\n" +
"This lets you set both your periapsis and apoapsis at once. Specify a new periapsis and a new apoapsis and hit \"Burn\" and MechJeb will automatically execute a burn to achieve both simultaneously.\n\n" +
"CIRC - Circularize:\n\n" +
"Hit \"Circularize\" and MechJeb will execute a burn to circularize your orbit at its current altitude. Note that if you are rising or falling rapidly you might be at a different altitude by the time MechJeb actually manages to circularize the orbit.\n\n" +
"TRANS - Trans-munar injection:\n\n" +
"While in Kerbin orbit, specify a desired munar periapsis. Hit \"Transfer to the Mun\" and MechJeb will warp to the appropriate point in your orbit, then execute a trans-munar injection (TMI) burn. The burn will be calculated so that when you enter the Mun's sphere of influence, your periapsis will be the one you specified. Once it has made the TMI burn, MechJeb will also display your predicted Munar periapsis, enabling you to make manual course corrections while you are still in Kerbin's sphere of influence.\n\n" +
"Note: automatic trans-munar injection is likely to go wrong if you are in an orbit with an inclination greater than around 1 or 2 degrees, or a highly elliptical orbit. MechJeb assumes that you are starting from a zero-inclination, circular orbit around Kerbin.\n\n" +
"WARP - Time warp:\n\n" +
"This lets you time warp to apoapsis, periapsis, or the next sphere of influence (SoI) switch. Select one of these three and hit \"Warp\", and MechJeb will automatically ramp the time warp rate up and down to move you quickly to the specified point. When warping to periapsis or apoapsis you can specify a lead time. This lets you warp to, e.g. 15 seconds before periapsis. Note that when warping to the next SoI switch, MechJeb will not go above 1,000x warp, as often ramping down from the higher 10,000x warp carries you farther than you would like.\n\n" +
"Abort:\n\n" +
"To immediately end any automatic operation, hit \"Abort\".\n\n" +
"Status:\n\n" +
"This will report what operation is currently being executed, or \"Idle\" if the orbital operations module is not currently doing anything.");
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }


        public override void onPartFixedUpdate()
        {
            if (!part.vessel.isActiveVessel || !enabled) return;

            if (transferTarget != null && transferTarget != part.vessel.mainBody && TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate)
            {
                postTransferPeR = predictPostTransferPeriapsis(vesselState.CoM, vesselState.velocityVesselOrbit, vesselState.time, transferTarget);
            }
        }

        public override void drive(FlightCtrlState s)
        {
            if (!part.vessel.isActiveVessel) return;

            switch (currentOperation)
            {
                case Operation.APOAPSIS:
                    driveApoapsis(s);
                    break;

                case Operation.PERIAPSIS:
                    drivePeriapsis(s);
                    break;

                case Operation.CIRCULARIZE:
                    driveCircularize(s);
                    break;

                case Operation.ELLIPTICIZE:
                    driveEllipticize(s);
                    break;

                case Operation.TRANSFER_INJECTION:
                    driveTransferInjection(s);
                    break;

                case Operation.WARP:
                    driveWarp(s);
                    break;
            }

        }


        void driveApoapsis(FlightCtrlState s)
        {
            bool endBurn;
            double managedValue = part.vessel.orbit.ApA;
            double targetValue = newApA;
            float minThrottle = 0.01F;

            if (deltaVToChangeApoapsis(newApA) > vesselState.maxThrustAccel) minThrottle = 1.0F;
            if (part.vessel.orbit.ApR < 0) targetValue = -1.0e30; //hyperbolic orbits have "negative apoapsis"

            if (raisingApsis)
            {
                core.attitudeTo(chooseThrustDirectionToRaiseApoapsis(), MechJebCore.AttitudeReference.INERTIAL, this);
                endBurn = (managedValue > targetValue || managedValue < 0);
            }
            else
            {
                core.attitudeTo(-chooseThrustDirectionToRaiseApoapsis(), MechJebCore.AttitudeReference.INERTIAL, this);
                endBurn = (part.vessel.orbit.ApR > 0 && (managedValue < targetValue || targetValue < vesselState.altitudeASL));
            }

            if (endBurn)
            {
                endOperation();
            }
            else
            {
                if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) core.warpMinimum(this);

                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = manageThrottle(managedValue, targetValue, minThrottle);
                else s.mainThrottle = 0.0F;
            }
        }

        void drivePeriapsis(FlightCtrlState s)
        {
            bool endBurn;
            float minThrottle = 0.01F;
            double managedValue = part.vessel.orbit.PeA;
            double targetValue = newPeA;

            if (deltaVToChangePeriapsis(newPeA + part.vessel.mainBody.Radius) > vesselState.maxThrustAccel) minThrottle = 1.0F;

            if (raisingApsis)
            {
                core.attitudeTo(chooseThrustDirectionToRaisePeriapsis(), MechJebCore.AttitudeReference.INERTIAL, this);
                endBurn = (managedValue > targetValue || vesselState.altitudeASL < targetValue);
            }
            else
            {
                core.attitudeTo(-chooseThrustDirectionToRaisePeriapsis(), MechJebCore.AttitudeReference.INERTIAL, this);
                endBurn = (managedValue < targetValue || targetValue < -part.vessel.mainBody.Radius);
            }

            if (endBurn)
            {
                endOperation();
            }
            else
            {
                if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) core.warpMinimum(this);

                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = manageThrottle(managedValue, targetValue, minThrottle);
                else s.mainThrottle = 0.0F;
            }
        }

        void driveCircularize(FlightCtrlState s)
        {
            Vector3d circVelocityCorrection = circularizationVelocityCorrection();
            core.attitudeTo(circVelocityCorrection.normalized, MechJebCore.AttitudeReference.INERTIAL, this);

            double managedValue = circVelocityCorrection.magnitude;
            double targetValue = 0.0;

            float minThrottle = 0.01F;
            if (managedValue > vesselState.maxThrustAccel) minThrottle = 1.0F;

            if (managedValue < 0.1)
            {
                endOperation();
            }
            else
            {
                if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) core.warpMinimum(this);

                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = manageThrottle(managedValue, targetValue, minThrottle);
                else s.mainThrottle = 0.0F;
            }
        }


        void driveEllipticize(FlightCtrlState s)
        {
            Vector3d ellVelocityCorrection = ellipticizationVelocityCorrection(newPeA, newApA);
            core.attitudeTo(ellVelocityCorrection.normalized, MechJebCore.AttitudeReference.INERTIAL, this);

            double managedValue = ellVelocityCorrection.magnitude;
            double targetValue = 0.0;

            float minThrottle = 0.01F;
            if (managedValue > vesselState.maxThrustAccel) minThrottle = 1.0F;

            if (managedValue < 0.1)
            {
                endOperation();
            }
            else
            {
                if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) core.warpMinimum(this);

                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = manageThrottle(managedValue, targetValue, minThrottle);
                else s.mainThrottle = 0.0F;
            }
        }


        void driveTransferInjection(FlightCtrlState s)
        {
            if (transferTarget == null || part.vessel.orbit.eccentricity > 1.0)
            {
                endOperation();
                return;
            }

            if (!startedTransferInjectionBurn)
            {
                double transferSemiMajorAxis = (vesselState.radius + transferTarget.orbit.PeR) / 2.0;
                double transferHalfPeriod = 0.5 * 2 * Math.PI / Math.Sqrt(ARUtils.G * part.vessel.mainBody.Mass) * Math.Pow(transferSemiMajorAxis, 1.5);
                double targetTimeSincePe = transferTarget.orbit.period - transferTarget.orbit.timeToPe;
                double targetArrivalTimeSincePe = targetTimeSincePe + transferHalfPeriod;
                Vector3d targetArrivalPosition = transferTarget.orbit.getPositionAt(targetArrivalTimeSincePe);
                Vector3d vesselArrivalPosition = part.vessel.mainBody.position - transferTarget.orbit.PeR * vesselState.up;
                Vector3d targetPlaneNormal = Vector3d.Cross(transferTarget.position - part.vessel.mainBody.position, transferTarget.orbit.GetVel());
                Vector3d vesselArrivalPositionTargetPlane = part.vessel.mainBody.position + Vector3d.Exclude(targetPlaneNormal - part.vessel.mainBody.position, vesselArrivalPosition);

                double angleOffset = Math.Abs(Vector3d.Angle(targetArrivalPosition - part.vessel.mainBody.position,
                                                             vesselArrivalPositionTargetPlane - part.vessel.mainBody.position));
                double timeOffset = angleOffset / 360 * part.vessel.orbit.period;
                if (Math.Abs(angleOffset) < 1)
                {
                    startedTransferInjectionBurn = true;
                    if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) core.warpMinimum(this);
                }
                else
                {
                    core.warpTo(this, timeOffset - 30, warpLookaheadTimes);
                }


                core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.ORBIT, this);

                //don't burn:
                s.mainThrottle = 0.0F;
                return;
            }
            else
            {
                core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.ORBIT, this);

                double managedValue = postTransferPeR;
                double targetValue = desiredPostTransferPeA + transferTarget.Radius;

                if (managedValue > 0 && managedValue < targetValue)
                {
                    endOperation();
                }
                else
                {
                    if (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate) core.warpMinimum(this);

                    if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = manageThrottle(managedValue, targetValue, 0.01F);
                    else s.mainThrottle = 0.0F;
                }
            }


        }

        void driveWarp(FlightCtrlState s)
        {
            s.mainThrottle = 0.0F;

            if (warpPoint == WarpPoint.SOI_CHANGE)
            {
                //ending warp-to-soi-change is done in the handleReferenceBodyChange callback
                core.warpIncrease(this, false, 1000.0);
            }
            else
            {
                //warp to periapsis or apoapsis

                double timeToTarget;
                double timeSinceTarget;
                if (warpPoint == WarpPoint.APOAPSIS)
                {
                    if (part.vessel.orbit.eccentricity > 1.0)
                    {
                        endOperation();
                        return;
                    }

                    timeToTarget = (part.vessel.orbit.timeToAp - warpTimeOffset + part.vessel.orbit.period) % part.vessel.orbit.period;
                    timeSinceTarget = (2 * part.vessel.orbit.period - part.vessel.orbit.timeToAp + warpTimeOffset) % part.vessel.orbit.period;
                }
                else
                {
                    double timeToPe = part.vessel.orbit.timeToPe;
                    if (part.vessel.orbit.eccentricity > 1)
                    {
                        timeToPe = -part.vessel.orbit.meanAnomaly / (2 * Math.PI / part.vessel.orbit.period);
                    }
                    timeToTarget = (timeToPe - warpTimeOffset + part.vessel.orbit.period) % part.vessel.orbit.period;
                    timeSinceTarget = (2 * part.vessel.orbit.period - timeToPe + warpTimeOffset) % part.vessel.orbit.period;
                    if (part.vessel.orbit.eccentricity > 1) timeSinceTarget = 1.0e30;
                }

                if (timeToTarget < 1.0 || timeSinceTarget < 10.0) endOperation();
                else core.warpTo(this, timeToTarget, warpLookaheadTimes);

            }

        }

        void endOperation()
        {
            if (currentOperation == Operation.WARP && TimeWarp.CurrentRateIndex > 0) core.warpMinimum(this, false);
            currentOperation = Operation.NONE;
            FlightInputHandler.SetNeutralControls();
            core.controlRelease(this);
        }


        double lastManagedValue;
        float lastThrottle = 1.0F;
        MovingAverage managedDerivative = new MovingAverage();
        float manageThrottle(double managedValue, double targetValue, float minThrottle)
        {
            if (lastThrottle != 0) managedDerivative.value = (managedValue - lastManagedValue) / (lastThrottle);

            float throttle;
            if (managedDerivative == 0) throttle = 1.0f;
            else throttle = Mathf.Clamp((float)(0.1 * (targetValue - managedValue) / managedDerivative), minThrottle, 1.0F);

            lastThrottle = throttle;
            lastManagedValue = managedValue;
            return throttle;
        }

        public override void onFlightStart()
        {
            part.vessel.orbit.OnReferenceBodyChange += new Orbit.CelestialBodyDelegate(this.handleReferenceBodyChange);
        }

        public void handleReferenceBodyChange(CelestialBody newBody)
        {
            endOperation();
        }



        Vector3d circularizationVelocityCorrection()
        {
            double circularSpeed = Math.Sqrt(vesselState.localg * vesselState.radius);
            Vector3d circularDirection = Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbit).normalized;
            return circularSpeed * circularDirection - vesselState.velocityVesselOrbit;
        }

        Vector3d ellipticizationVelocityCorrection(double newPeA, double newApA)
        {
            double newPeR = newPeA + part.vessel.mainBody.Radius;
            double newApR = newApA + part.vessel.mainBody.Radius;
            if (vesselState.radius < newPeR || vesselState.radius > newApR || newPeR < 0) return Vector3d.zero;
            double GM = ARUtils.G * part.vessel.mainBody.Mass;
            double E = -GM / (newPeR + newApR);
            double L = Math.Sqrt(Math.Abs((Math.Pow(E * (newApR - newPeR), 2) - GM * GM) / (2 * E)));
            double kineticE = E + GM / vesselState.radius;
            double horizontalV = L / vesselState.radius;
            double verticalV = Math.Sqrt(Math.Abs(2 * kineticE - horizontalV * horizontalV));

            Vector3d horizontalUnit = Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbit).normalized;
            return (horizontalV * horizontalUnit + verticalV * vesselState.up - vesselState.velocityVesselOrbit);
        }

        Vector3d chooseThrustDirectionToRaisePeriapsis()
        {
            return Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbit).normalized;
        }

        Vector3d chooseThrustDirectionToRaiseApoapsis()
        {
            return vesselState.velocityVesselOrbitUnit;
        }


        public double deltaVToChangePeriapsis(double newPeA)
        {
            double newPeR = newPeA + part.vessel.mainBody.Radius;

            //don't bother with impossible maneuvers:
            if (newPeR > vesselState.radius || newPeR < 0) return 0.0;

            //are we raising or lowering the periapsis?
            bool raising = (newPeR > new AROrbit(part.vessel).periapsis());

            Vector3d burnDirection = (raising ? 1 : -1) * chooseThrustDirectionToRaisePeriapsis();

            double minDeltaV = 0;
            double maxDeltaV;
            if (raising)
            {
                //put an upper bound on the required deltaV:
                maxDeltaV = 0.25;
                while (new AROrbit(vesselState.CoM, vesselState.velocityVesselOrbit + maxDeltaV * burnDirection,
                    vesselState.time, part.vessel.mainBody).periapsis() < newPeR)
                {
                    maxDeltaV *= 2;
                    if (maxDeltaV > 100000) break; //a safety precaution
                }
            }
            else
            {
                //when lowering periapsis, we burn horizontally, and max possible deltaV is the deltaV required to kill all horizontal velocity
                double horizontalV = Vector3d.Exclude(vesselState.up, vesselState.velocityVesselOrbit).magnitude;
                maxDeltaV = horizontalV;
            }

            //now do a binary search to find the needed delta-v
            while (maxDeltaV - minDeltaV > 1.0)
            {
                double testDeltaV = (maxDeltaV + minDeltaV) / 2.0;
                double testPeriapsis = new AROrbit(vesselState.CoM, vesselState.velocityVesselOrbit + testDeltaV * burnDirection,
                                                   vesselState.time, part.vessel.mainBody).periapsis();
                if ((testPeriapsis > newPeR && raising) || (testPeriapsis < newPeR && !raising))
                {
                    maxDeltaV = testDeltaV;
                }
                else
                {
                    minDeltaV = testDeltaV;
                }
            }
            return (maxDeltaV + minDeltaV) / 2;
        }


        double deltaVToChangeApoapsis(double newApA)
        {
            double newApR = newApA + part.vessel.mainBody.Radius;

            //don't bother with impossible maneuvers:
            if (newApR < vesselState.radius) return 0.0;

            //are we raising or lowering the periapsis?
            double initialAp = new AROrbit(part.vessel).apoapsis();
            if (initialAp < 0) initialAp = double.MaxValue;
            bool raising = (newApR > initialAp);

            Vector3d burnDirection = (raising ? 1 : -1) * chooseThrustDirectionToRaiseApoapsis();

            double minDeltaV = 0;
            double maxDeltaV;
            if (raising)
            {
                //put an upper bound on the required deltaV:
                maxDeltaV = 0.25;

                double ap = initialAp;
                if (ap < 0) ap = double.MaxValue; //ap < 0 for hyperbolic orbits
                while (ap < newApR)
                {
                    maxDeltaV *= 2;
                    ap = new AROrbit(vesselState.CoM, vesselState.velocityVesselOrbit + maxDeltaV * burnDirection,
                                        vesselState.time, part.vessel.mainBody).apoapsis();
                    if (ap < 0) ap = double.MaxValue;
                    if (maxDeltaV > 100000) break; //a safety precaution
                }
            }
            else
            {
                //when lowering apoapsis, we burn retrograde, and max possible deltaV is total velocity
                maxDeltaV = vesselState.speedOrbital;
            }

            //now do a binary search to find the needed delta-v
            while (maxDeltaV - minDeltaV > 1.0)
            {
                double testDeltaV = (maxDeltaV + minDeltaV) / 2.0;
                double testApoapsis = new AROrbit(vesselState.CoM, vesselState.velocityVesselOrbit + testDeltaV * burnDirection,
                                                   vesselState.time, part.vessel.mainBody).apoapsis();
                if (testApoapsis < 0) testApoapsis = double.MaxValue;
                if ((testApoapsis > newApR && raising) || (testApoapsis < newApR && !raising))
                {
                    maxDeltaV = testDeltaV;
                }
                else
                {
                    minDeltaV = testDeltaV;
                }
            }
            return (maxDeltaV + minDeltaV) / 2;
        }



        double predictPostTransferPeriapsis(Vector3d pos, Vector3d vel, double startTime, CelestialBody target)
        {
            AROrbit transferOrbit = new AROrbit(pos, vel, startTime, part.vessel.mainBody);
            AROrbit targetOrbit = new AROrbit(target.position, target.orbit.GetVel(), vesselState.time, target.orbit.referenceBody);

            double soiSwitchTime = predictSOISwitchTime(transferOrbit, target, startTime);
            if (soiSwitchTime == -1) return -1;

            Vector3d transferPos = transferOrbit.positionAtTime(soiSwitchTime);
            Vector3d targetPos = targetOrbit.positionAtTime(soiSwitchTime);
            Vector3d relativePos = transferPos - targetPos;
            Vector3d transferVel = transferOrbit.velocityAtTime(soiSwitchTime);
            Vector3d targetVel = targetOrbit.velocityAtTime(soiSwitchTime);
            Vector3d relativeVel = transferVel - targetVel;
            double pe = computePeriapsis(relativePos, relativeVel, target);

            return pe;
        }

        double predictSOISwitchTime(AROrbit transferOrbit, CelestialBody target, double startTime)
        {
            if (transferOrbit.hyperbolic) return -1; //hyperbolic orbits not supported

            AROrbit targetOrbit = new AROrbit(target.position, target.orbit.GetVel(), vesselState.time, target.orbit.referenceBody);
            double minSwitchTime = startTime;
            double maxSwitchTime = 0;

            double t = startTime;

            while(true)
            {
                Vector3d transferPos = transferOrbit.positionAtTime(t);
                Vector3d targetPos = targetOrbit.positionAtTime(t);
                if ((transferPos - targetPos).magnitude < target.sphereOfInfluence)
                {
                    maxSwitchTime = t;
                    break;
                }
                else
                {
                    minSwitchTime = t;
                }

                //advance time by a safe amount (so that we don't completely skip the SOI intersection)
                Vector3d transferVel = transferOrbit.velocityAtTime(t);
                Vector3d targetVel = targetOrbit.velocityAtTime(t);
                Vector3d relativeVel = transferVel - targetVel;
                t += 0.1 * target.sphereOfInfluence / relativeVel.magnitude;

                if(t > startTime + transferOrbit.period) break;

                //guard against taking infinitely long on hyperbolic or very elliptical orbits:
                double maxTargetSOIRadius = (target.position - part.vessel.mainBody.position).magnitude + target.sphereOfInfluence;
                if((transferPos - part.vessel.mainBody.position).magnitude > 2 * maxTargetSOIRadius) break;
            }

            if (maxSwitchTime == 0)
            {
                return -1; //didn't intersect target's SOI
            }

            //do a binary search for the SOI entrance time:
            while (maxSwitchTime - minSwitchTime > 1.0)
            {
                double testTime = (minSwitchTime + maxSwitchTime) / 2.0;
                Vector3d transferPos = transferOrbit.positionAtTime(testTime);
                Vector3d targetPos = targetOrbit.positionAtTime(testTime);

                if ((transferPos - targetPos).magnitude < target.sphereOfInfluence) maxSwitchTime = testTime;
                else minSwitchTime = testTime;
            }

            return (maxSwitchTime + minSwitchTime) / 2.0;
        }


        double computePeriapsis(Vector3d relativePos, Vector3d relativeVel, CelestialBody body)
        {
            double radius = relativePos.magnitude;
            double horizontalSpeed = Vector3d.Exclude(relativePos, relativeVel).magnitude;
            double GM = ARUtils.G * body.Mass;
            double E = 0.5 * relativeVel.sqrMagnitude - GM / radius;
            double L = radius * horizontalSpeed;

            double pe = (-GM + Math.Sqrt(Math.Abs(GM * GM + 2 * E * L * L))) / (2 * E);
            return pe;
        }


        public override String getName()
        {
            return "Orbital operations";
        }
    }



}