using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using OrbitExtensions;
using SharpLua.LuaTypes;
/*
 * Todo:
 * 
 * 
 * Changed:
 * 
 * -changed (hopefully improved) throttle management
 * 
 */

namespace MuMech
{

    public class MechJebModuleOrbitOper : ComputerModule
    {

        public MechJebModuleOrbitOper(MechJebCore core) : base(core) { }

        

        public override void registerLuaMembers(LuaTable index)
        {
            index.Register("changePe", proxyChangePe);
            index.Register("changeAp", proxyChangeAp);
            index.Register("changeApAndPe", proxyChangeApAndPe);
            index.Register("circularize", proxyCircularize);
            index.Register("transfer", proxyTransfer);
            index.Register("warpToEvent", proxyWarpToEvent);
        }

        public LuaValue proxyChangePe(LuaValue[] args)
        {
            if (args.Count() != 1) throw new Exception("changePe usage: changePe(periapsis [in meters])");

            try
            {
                newPeA = ((LuaNumber)args[0]).Number;
            }
            catch (Exception)
            {
                throw new Exception("changePe: invalid periapsis");
            }

            newPeAString = (newPeA / 1000.0).ToString();

            this.enabled = true;
            core.controlClaim(this);
            currentOperation = Operation.PERIAPSIS;
            raisingApsis = (part.vessel.orbit.PeA < newPeA);

            guiTab = Operation.PERIAPSIS;

            return LuaNil.Nil;
        }

        public LuaValue proxyChangeAp(LuaValue[] args)
        {
            if (args.Count() != 1) throw new Exception("changeAp usage: changePe(periapsis [in meters])");

            try
            {
                newApA = ((LuaNumber)args[0]).Number;
            }
            catch (Exception)
            {
                throw new Exception("changeAp: invalid apoapsis");
            }

            newApAString = (newApA / 1000.0).ToString();

            this.enabled = true;
            core.controlClaim(this);
            currentOperation = Operation.APOAPSIS;
            raisingApsis = (part.vessel.orbit.ApR > 0 && part.vessel.orbit.ApA < newApA);

            guiTab = Operation.APOAPSIS;

            return LuaNil.Nil;
        }

        public LuaValue proxyChangeApAndPe(LuaValue[] args)
        {
            if (args.Count() != 2) throw new Exception("changeAp usage: changeApAndPe(apoapsis[in meters], periapsis [in meters])");

            try
            {
                newApA = ((LuaNumber)args[0]).Number;
            }
            catch (Exception)
            {
                throw new Exception("changeAp: invalid apoapsis");
            }

            try
            {
                newPeA = ((LuaNumber)args[1]).Number;
            }
            catch (Exception)
            {
                throw new Exception("changeApAndPe: invalid periapsis");
            }

            newApAString = (newApA / 1000.0).ToString();
            newPeAString = (newPeA / 1000.0).ToString();

            this.enabled = true;
            core.controlClaim(this);
            currentOperation = Operation.ELLIPTICIZE;

            guiTab = Operation.ELLIPTICIZE;

            return LuaNil.Nil;
        }

        public LuaValue proxyCircularize(LuaValue[] args)
        {
            this.enabled = true;
            core.controlClaim(this);
            currentOperation = Operation.CIRCULARIZE;

            guiTab = Operation.CIRCULARIZE;

            return LuaNil.Nil;
        }

        public LuaValue proxyTransfer(LuaValue[] args)
        {
            if (args.Count() != 2) throw new Exception("transfer usage: transfer(target body, final periapsis [in meters])");

            try
            {
                foreach (CelestialBody body in part.vessel.mainBody.orbitingBodies)
                {
                    if (body.name.ToLower().Contains(args[0].ToString().ToLower()))
                    {
                        transferTarget = body;
                        break;
                    }
                }
            }
            catch (Exception)
            {
                throw new Exception("transfer: invalid target body");
            }

            try
            {
                desiredPostTransferPeA = ((LuaNumber)args[1]).Number;
            }
            catch (Exception)
            {
                throw new Exception("transfer: invalid final periapsis");
            }

            desiredPostTransferPeAString = (desiredPostTransferPeA / 1000.0).ToString();

            this.enabled = true;
            core.controlClaim(this);
            currentOperation = Operation.TRANSFER_INJECTION;
            transState = TRANSState.WAITING_FOR_INJECTION;

            guiTab = Operation.TRANSFER_INJECTION;

            return LuaNil.Nil;
        }

        public LuaValue proxyWarpToEvent(LuaValue[] args)
        {
            if (args.Count() == 0 || args.Count() > 2) throw new Exception("warpToEvent usage: warpToEvent(event) or warpToEvent(event, lead time)");

            try
            {
                if (args[0].ToString().ToLower().Contains("pe")) warpPoint = WarpPoint.PERIAPSIS;
                else if (args[0].ToString().ToLower().Contains("ap")) warpPoint = WarpPoint.APOAPSIS;
                else if (args[0].ToString().ToLower().Contains("soi")) warpPoint = WarpPoint.SOI_CHANGE;
                else throw new Exception("warpToEvent: event must be one of \"pe\", \"ap\", \"soi\"");
            }
            catch (Exception)
            {
                throw new Exception("warpToEvent: invalid event");
            }

            if (args.Count() == 2)
            {
                try
                {
                    warpTimeOffset = ((LuaNumber)args[1]).Number;
                }
                catch (Exception)
                {
                    throw new Exception("warpToEvent: invalid lead time");
                }

            }
            else
            {
                warpTimeOffset = 0;
            }

            warpTimeOffsetString = warpTimeOffset.ToString();

            this.enabled = true;
            core.controlClaim(this);
            currentOperation = Operation.WARP;

            guiTab = Operation.WARP;

            return LuaNil.Nil;
        }


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
        public String[] guiTitleStrings = new String[] { "Change periapsis", "Change apoapsis", "Change both apsides", "Circularize", "Transfer injection", "Time warp" };
        bool showHelpWindow = false;
        Vector2 helpScrollPosition = new Vector2();

        public enum WarpPoint { PERIAPSIS, APOAPSIS, SOI_CHANGE }
        public String[] warpPointStrings = new String[] { "Periapsis", "Apoapsis", "SoI switch" };
        public String[] warpPointStrings2 = new String[] { "periapsis", "apoapsis", "SoI switch" };
        WarpPoint warpPoint;
        double[] warpLookaheadTimes = new double[] { 0, 2.5, 5, 25, 50, 500, 10000, 100000 };

        bool raisingApsis;

        CelestialBody transferTarget;

        enum TRANSState { WAITING_FOR_INJECTION, INJECTING, WAITING_FOR_CORRECTION, LOWERING_PERIAPSIS };
        String[] transStateStrings = new String[] { "Moving to injection burn point", "Executing main injection burn", "Moving to mid-course correction", "Lowering final periapsis" };
        TRANSState transState;

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

                        double postTransferPeR = getPredictedPostTransferPeR();
                        if (transferTarget != null && postTransferPeR != -1)
                        {
                            GUILayout.Label(String.Format("Predicted periapsis after transfer to " + transferTarget.name + ": {0:0} km", (postTransferPeR - transferTarget.Radius) / 1000.0));
                        }

                        foreach (CelestialBody body in part.vessel.mainBody.orbitingBodies)
                        {
                            double deltaV = deltaVToChangeApoapsis(body.orbit.PeA);
                            Orbit transferOrbit = ARUtils.computeOrbit(part.vessel, deltaV * vesselState.velocityVesselOrbitUnit, vesselState.time);
                            double arrivalTime = vesselState.time + transferOrbit.timeToAp;
                            Vector3d vesselArrivalPosition = transferOrbit.getAbsolutePositionAtUT(arrivalTime);
                            Orbit targetOrbit = ARUtils.computeOrbit(body.position, (FlightGlobals.RefFrameIsRotating ? -1 : 1) * body.orbit.GetVel(), part.vessel.mainBody, vesselState.time);
                            Vector3d targetArrivalPosition = targetOrbit.getAbsolutePositionAtUT(arrivalTime);
                            Vector3d targetPlaneNormal = Vector3d.Cross(body.position - part.vessel.mainBody.position, body.orbit.GetVel());
                            Vector3d vesselArrivalPositionTargetPlane = part.vessel.mainBody.position + Vector3d.Exclude(targetPlaneNormal, vesselArrivalPosition - part.vessel.mainBody.position);
                            double angleOffset = Math.Abs(Vector3d.Angle(targetArrivalPosition - part.vessel.mainBody.position, vesselArrivalPositionTargetPlane - part.vessel.mainBody.position));

                            if (
                                    Math.Abs(Vector3d.Angle(vesselState.velocityVesselOrbitUnit, targetArrivalPosition - part.vessel.mainBody.position)) <
                                    Math.Abs(Vector3d.Angle(vesselState.velocityVesselOrbitUnit * -1, targetArrivalPosition - part.vessel.mainBody.position))
                                )
                            {
                                angleOffset = 360.0 - angleOffset; //if we have passed the transfer point then give the user the time to loop back around the long way
                            }

                            angleOffset = Math.Max(angleOffset - 1.0, 0.0); //give us a 1 degree window
                            double timeOffset = Math.Abs(angleOffset) / 360 * part.vessel.orbit.period;

                            if (GUILayout.Button(String.Format("Transfer point to " + body.name + " in {1:0} s (Δv ≈ {0:0} m/s)", deltaV, MuUtils.ToSI(timeOffset, 1))))
                            {
                                core.controlClaim(this);
                                currentOperation = Operation.TRANSFER_INJECTION;
                                transState = TRANSState.WAITING_FOR_INJECTION;
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
                    statusString = "Transferring to " + transferTarget.name + "\n" + transStateStrings[(int)transState];
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

        bool isSOISwitchPredicted()
        {
            return part.vessel.orbit.closestEncounterBody != null
                && part.vessel.orbit.closestEncounterBody == transferTarget
                && part.vessel.orbit.ClAppr > 0
                && part.vessel.orbit.ClAppr < part.vessel.orbit.closestEncounterBody.sphereOfInfluence;
        }

        double getPredictedPostTransferPeR()
        {
            if (transferTarget != null
                && transferTarget != part.vessel.mainBody
                && isSOISwitchPredicted())
            {
                return part.vessel.orbit.nextPatch.PeR;
            }
            else
            {
                return -1;
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
                    driveTransfer(s);
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
                if ((TimeWarp.WarpMode == TimeWarp.Modes.HIGH) && (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)) core.warpMinimum(this);

                double dVLeft = deltaVToChangeApoapsis(newApA);
                float throttle = Mathf.Clamp((float)(dVLeft / (0.5 * vesselState.maxThrustAccel)), 0.0F, 1.0F);
                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = throttle;
                else s.mainThrottle = 0.0F;
            }
        }

        void drivePeriapsis(FlightCtrlState s)
        {
            bool endBurn;
            double managedValue = part.vessel.orbit.PeA;
            double targetValue = newPeA;

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
                if ((TimeWarp.WarpMode == TimeWarp.Modes.HIGH) && (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)) core.warpMinimum(this);

                double dVLeft = deltaVToChangePeriapsis(newPeA);
                float throttle = Mathf.Clamp((float)(dVLeft / (0.5 * vesselState.maxThrustAccel)), 0.0F, 1.0F);
                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = throttle;
                else s.mainThrottle = 0.0F;
            }
        }

        void driveCircularize(FlightCtrlState s)
        {
            Vector3d circVelocityCorrection = circularizationVelocityCorrection();
            core.attitudeTo(circVelocityCorrection.normalized, MechJebCore.AttitudeReference.INERTIAL, this);

            double dVLeft = circVelocityCorrection.magnitude;
            if (dVLeft < 0.1)
            {
                endOperation();
            }
            else
            {
                if ((TimeWarp.WarpMode == TimeWarp.Modes.HIGH) && (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)) core.warpMinimum(this);

                float throttle = Mathf.Clamp((float)(dVLeft / (0.5 * vesselState.maxThrustAccel)), 0.0F, 1.0F);
                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = throttle;
                else s.mainThrottle = 0.0F;
            }
        }


        void driveEllipticize(FlightCtrlState s)
        {
            Vector3d ellVelocityCorrection = ellipticizationVelocityCorrection(newPeA, newApA);
            core.attitudeTo(ellVelocityCorrection.normalized, MechJebCore.AttitudeReference.INERTIAL, this);

            double dVLeft = ellVelocityCorrection.magnitude;
            if (dVLeft < 0.1)
            {
                endOperation();
            }
            else
            {
                if ((TimeWarp.WarpMode == TimeWarp.Modes.HIGH) && (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)) core.warpMinimum(this);

                float throttle = Mathf.Clamp((float)(dVLeft / (0.5 * vesselState.maxThrustAccel)), 0.0F, 1.0F);
                if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = throttle;
                else s.mainThrottle = 0.0F;
            }
        }

        void driveTransfer(FlightCtrlState s)
        {
            switch (transState)
            {
                case TRANSState.WAITING_FOR_INJECTION:
                    driveTransferWaitingForInjection(s);
                    break;

                case TRANSState.INJECTING:
                    driveTransferInjection(s);
                    break;

                case TRANSState.WAITING_FOR_CORRECTION:
                    driveTransferWaitingForCorrection(s);
                    break;

                case TRANSState.LOWERING_PERIAPSIS:
                    driveTransferLoweringPeriapsis(s);
                    break;
            }
        }

        void driveTransferWaitingForInjection(FlightCtrlState s)
        {
            if (transferTarget == null || part.vessel.orbit.eccentricity > 1.0)
            {
                endOperation();
                return;
            }

            Orbit transferOrbit = ARUtils.computeOrbit(part.vessel, deltaVToChangeApoapsis(transferTarget.orbit.PeA) * vesselState.velocityVesselOrbitUnit, vesselState.time);

            double arrivalTime = vesselState.time + transferOrbit.timeToAp;
            Vector3d vesselArrivalPosition = transferOrbit.getAbsolutePositionAtUT(arrivalTime);

            //minus sign is there because transferTarget.orbit.GetVel() seems to be mysteriously negated
            //when the ship is in the rotating reference frame of the original body.
            Orbit targetOrbit = ARUtils.computeOrbit(transferTarget.position, (FlightGlobals.RefFrameIsRotating ? -1 : 1) * transferTarget.orbit.GetVel(), part.vessel.mainBody, vesselState.time);

            Vector3d targetArrivalPosition = targetOrbit.getAbsolutePositionAtUT(arrivalTime);

            Vector3d targetPlaneNormal = Vector3d.Cross(transferTarget.position - part.vessel.mainBody.position, transferTarget.orbit.GetVel());
            Vector3d vesselArrivalPositionTargetPlane = part.vessel.mainBody.position + Vector3d.Exclude(targetPlaneNormal, vesselArrivalPosition - part.vessel.mainBody.position);

            double angleOffset = Math.Abs(Vector3d.Angle(targetArrivalPosition - part.vessel.mainBody.position,
                                                         vesselArrivalPositionTargetPlane - part.vessel.mainBody.position));

            if (Math.Abs(angleOffset) < 1)
            {
                transState = TRANSState.INJECTING;
                if ((TimeWarp.WarpMode == TimeWarp.Modes.HIGH) && (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)) core.warpMinimum(this);
            }
            else
            {
                double timeOffset = angleOffset / 360 * part.vessel.orbit.period;
                core.warpTo(this, timeOffset - 30, warpLookaheadTimes);
            }

            core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.ORBIT, this);

            //don't burn:
            s.mainThrottle = 0.0F;
        }

        void driveTransferInjection(FlightCtrlState s)
        {
            if (part.vessel.orbit.eccentricity > 1)
            {
                endOperation();
                return;
            }

            if (getPredictedPostTransferPeR() != -1 && part.vessel.orbit.relativeInclination(transferTarget.orbit) < 1)
            {
                transState = TRANSState.LOWERING_PERIAPSIS;
                return;
            }

            if(part.vessel.orbit.ApR > transferTarget.orbit.PeR) 
            {
                transState = TRANSState.WAITING_FOR_CORRECTION;
                return;
            }

            if ((TimeWarp.WarpMode == TimeWarp.Modes.HIGH) && (TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)) core.warpMinimum(this);

            core.attitudeTo(Vector3d.forward, MechJebCore.AttitudeReference.ORBIT, this);

            if (core.attitudeAngleFromTarget() < 5) s.mainThrottle = 1.0F;
            else s.mainThrottle = 0.0F;
        }

        void driveTransferWaitingForCorrection(FlightCtrlState s) {
            if(vesselState.radius < 0.5 * (part.vessel.orbit.PeR + part.vessel.orbit.ApR)) 
            {
                core.warpIncrease(this, false);
            }
            else 
            {
                core.warpMinimum(this, false);
                transState = TRANSState.LOWERING_PERIAPSIS;
            }

            s.mainThrottle = 0.0F;
        }

        void driveTransferLoweringPeriapsis(FlightCtrlState s)
        {
            double postTransferPeR = getPredictedPostTransferPeR();
            if (postTransferPeR != -1 && postTransferPeR < desiredPostTransferPeA + transferTarget.Radius)
            {
                endOperation();
                return;
            }

            Orbit targetOrbit = ARUtils.computeOrbit(transferTarget.position, transferTarget.orbit.GetVel(), part.vessel.mainBody, vesselState.time);

            double orbitIntersectionTime;
            if (part.vessel.orbit.PeR < transferTarget.orbit.PeR)
            {
                orbitIntersectionTime = vesselState.time + part.vessel.orbit.timeToAp;
            }
            else
            {
                orbitIntersectionTime = part.vessel.orbit.GetUTforTrueAnomaly(part.vessel.orbit.TrueAnomalyAtRadius(transferTarget.orbit.PeR), 0);
            }

            Vector3d vesselIntersectionPosition = ARUtils.computeOrbit(part.vessel, Vector3d.zero, vesselState.time).getAbsolutePositionAtUT(orbitIntersectionTime);
            Vector3d targetIntersectionPosition = targetOrbit.getAbsolutePositionAtUT(orbitIntersectionTime);
            Vector3d intersectionSeparation = targetIntersectionPosition - vesselIntersectionPosition;

            Vector3d[] burnDirections = new Vector3d[] { vesselState.velocityVesselOrbitUnit, vesselState.leftOrbit, vesselState.upNormalToVelOrbit };
            double[] separationReductions = new double[3];

            double perturbationDeltaV = 0.5;
            for (int i = 0; i < 3; i++)
            {
                Orbit perturbedOrbit = ARUtils.computeOrbit(part.vessel, perturbationDeltaV * burnDirections[i], vesselState.time);

                double perturbedIntersectionTime;
                if (perturbedOrbit.PeR < transferTarget.orbit.PeR)
                {
                    perturbedIntersectionTime = vesselState.time + perturbedOrbit.timeToAp;
                }
                else
                {
                    perturbedIntersectionTime = perturbedOrbit.GetUTforTrueAnomaly(perturbedOrbit.TrueAnomalyAtRadius(transferTarget.orbit.PeR), 0);
                }
                Vector3d perturbedVesselIntersectionPosition = perturbedOrbit.getAbsolutePositionAtUT(perturbedIntersectionTime);
                Vector3d perturbedTargetIntersectionPosition = targetOrbit.getAbsolutePositionAtUT(perturbedIntersectionTime);
                Vector3d perturbedIntersectionSeparation = perturbedTargetIntersectionPosition - perturbedVesselIntersectionPosition;

                separationReductions[i] = (intersectionSeparation.magnitude - perturbedIntersectionSeparation.magnitude) / perturbationDeltaV;
            }


            double gradientMagnitude = Math.Sqrt(Math.Pow(separationReductions[0], 2) + Math.Pow(separationReductions[1], 2) + Math.Pow(separationReductions[2], 2));

            Vector3d desiredBurnDirection = (separationReductions[0] * burnDirections[0] 
                + separationReductions[1] * burnDirections[1] 
                + separationReductions[2] * burnDirections[2]).normalized; 

            core.attitudeTo(desiredBurnDirection, MechJebCore.AttitudeReference.INERTIAL, this);

            if (core.attitudeAngleFromTarget() < 5)
            {
                //this is a crude guess of how much we need to burn:
                double dVLeft;
                if (postTransferPeR == -1) dVLeft = intersectionSeparation.magnitude / gradientMagnitude;
                else dVLeft = (postTransferPeR - (desiredPostTransferPeA + transferTarget.Radius)) / gradientMagnitude;

                float throttle = Mathf.Clamp((float)(dVLeft / (2.0 * vesselState.maxThrustAccel)), 0.02F, 1.0F);
                s.mainThrottle = throttle;
            }
            else
            {
                s.mainThrottle = 0.0F;
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

                    if (part.vessel.orbit.eccentricity < 1)
                    {
                        timeToTarget = (timeToPe - warpTimeOffset + part.vessel.orbit.period) % part.vessel.orbit.period;
                        timeSinceTarget = (2 * part.vessel.orbit.period - timeToPe + warpTimeOffset) % part.vessel.orbit.period;
                    }
                    else
                    {
                        timeToTarget = timeToPe - warpTimeOffset;
                        timeSinceTarget = 1.0e30;
                    }
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


        public override void onFlightStart()
        {
            part.vessel.orbitDriver.OnReferenceBodyChange += new OrbitDriver.CelestialBodyDelegate(this.handleReferenceBodyChange);
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
            bool raising = (newPeR > part.vessel.orbit.PeR);

            Vector3d burnDirection = (raising ? 1 : -1) * chooseThrustDirectionToRaisePeriapsis();

            double minDeltaV = 0;
            double maxDeltaV;
            if (raising)
            {
                //put an upper bound on the required deltaV:
                maxDeltaV = 0.25;
                while (ARUtils.computeOrbit(part.vessel, maxDeltaV * burnDirection, vesselState.time).PeR < newPeR)
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
                double testPeriapsis = ARUtils.computeOrbit(part.vessel, testDeltaV * burnDirection, vesselState.time).PeR;

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
            double initialAp = part.vessel.orbit.ApR;
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
                    ap = ARUtils.computeOrbit(part.vessel, maxDeltaV * burnDirection, vesselState.time).ApR;
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
                double testApoapsis = ARUtils.computeOrbit(part.vessel, testDeltaV * burnDirection, vesselState.time).ApR;

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

        public override String getName()
        {
            return "Orbital operations";
        }
    }


    public class Matrix3x3
    {
        double[, ] m = new double[3, 3];

        //  [a    b]
        //  [      ]
        //  [c    d]

        public Matrix3x3() {}

        public Matrix3x3(Vector3d v1, Vector3d v2, Vector3d v3)
        {
            m[0, 0] = v1.x;
            m[1, 0] = v1.y;
            m[2, 0] = v1.z;
            m[0, 1] = v2.x;
            m[1, 1] = v2.y;
            m[2, 1] = v2.z;
            m[0, 2] = v3.x;
            m[1, 2] = v3.y;
            m[2, 2] = v3.z;
        }

        public Matrix3x3 inverse()
        {
            double det = m[0, 0] * m[1, 1] * m[2, 2] + m[0, 1] * m[1, 2] * m[2, 0] + m[0, 2] * m[1, 0] * m[2, 1]
                       - m[0, 0] * m[1, 2] * m[2, 1] - m[0, 1] * m[1, 0] * m[2, 2] - m[0, 2] * m[1, 1] * m[2, 0];

            Matrix3x3 ret = new Matrix3x3();

            for(int r = 0; r < 3; r++) {
                for(int c = 0; c < 3; c++) {
                    int mr1 = (r == 0 ? 1 : 0);
                    int mr2 = (r == 2 ? 1 : 2);
                    int mc1 = (c == 0 ? 1 : 0);
                    int mc2 = (c == 2 ? 1 : 2);

                    ret.m[r, c] = 1/det * (m[mr1, mc1] * m[mr2, mc2] - m[mr2, mc1] * m[mr1, mc2]);
                }
            }

            return ret;
        }

        public Vector3d times(Vector3d v)
        {
            return new Vector3d(m[0, 0] * v.x + m[0, 1] * v.y + m[0, 2] * v.z,
                m[1, 0] * v.x + m[1, 1] * v.y + m[1, 2] * v.z,
                m[2, 0] * v.x + m[2, 1] * v.y + m[2, 2] * v.z);
        }
    }

}