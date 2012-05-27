using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{

    public class ARUtils
    {
        public const double G = 6.674E-11; //this seems to be the value the game uses

        ///////////////////////////////////////////////////
        //ROCKET CHARACTERISTICS///////////////////////////
        ///////////////////////////////////////////////////

        //sums the thrust of ACTIVE engines under root, given hypothetical throttle setting
        //note: won't properly handle engines that don't subclass LiquidEngine or SolidRocket
        public static double totalThrustOfActiveEngines(Vessel v, double throttle)
        {
            double thrust = 0;

            foreach (Part p in v.parts)
            {
                if (p.State == PartStates.ACTIVE)
                {
                    if (p is LiquidEngine)
                    {
                        thrust += (1.0 - throttle) * ((LiquidEngine)p).minThrust + throttle * ((LiquidEngine)p).maxThrust;
                    }
                    if (p is SolidRocket)
                    {
                        thrust += ((SolidRocket)p).thrust;
                    }
                }
            }

            return thrust;
        }

        //sum of mass of parts under root
        public static double totalMass(Vessel v)
        {
            double ret = 0;
            foreach (Part p in v.parts)
            {
                ret += p.mass;
            }
            return ret;
        }

        //acceleration due to thrust of the rocket given a hypothetical throttle setting,
        //not accounting for external forces like drag and gravity
        public static double thrustAcceleration(Vessel v, double throttle)
        {
            return totalThrustOfActiveEngines(v, throttle) / totalMass(v);
        }

        public static bool engineHasFuel(Part p)
        {
            //I don't really know the details of how you're supposed to use RequestFuel, but this seems to work to
            //test whether something can get fuel.
            return p.RequestFuel(p, 0, Part.getFuelReqId());
        }

        public static bool hasIdleEngineDescendant(Part p)
        {
            if ((p.State == PartStates.IDLE) && (p is SolidRocket || p is LiquidEngine)) return true;
            foreach (Part child in p.children)
            {
                if (hasIdleEngineDescendant(child)) return true;
            }
            return false;
        }

        //detect if a part is above an active or idle engine in the part tree
        public static bool hasActiveOrIdleEngineOrTankDescendant(Part p)
        {
            if ((p.State == PartStates.ACTIVE || p.State == PartStates.IDLE) 
                && (p is SolidRocket || (p is LiquidEngine && engineHasFuel(p)))) return true;
            if ((p is FuelTank) && ((FuelTank)p).fuel > 0) return true;
            foreach (Part child in p.children)
            {
                if (hasActiveOrIdleEngineOrTankDescendant(child)) return true;
            }
            return false;
        }

        //detect if a part is above a deactivated engine or fuel tank
        public static bool hasDeactivatedEngineOrTankDescendant(Part p)
        {
            if ((p.State == PartStates.DEACTIVATED) && (p is SolidRocket || p is LiquidEngine || p is FuelTank)) return true;
            if (p is LiquidEngine && !engineHasFuel(p)) return true;
            foreach (Part child in p.children)
            {
                if (hasDeactivatedEngineOrTankDescendant(child)) return true;
            }
            return false;
        }

        //determine whether it's safe to activate inverseStage
        public static bool inverseStageDecouplesActiveOrIdleEngineOrTank(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && (p is Decoupler || p is RadialDecoupler) && hasActiveOrIdleEngineOrTankDescendant(p))
                {
                    return true;
                }
            }
            return false;
        }

        //determine whether inverseStage sheds a dead engine
        public static bool inverseStageDecouplesDeactivatedEngineOrTank(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && (p is Decoupler || p is RadialDecoupler) && hasDeactivatedEngineOrTankDescendant(p)) return true;
            }
            return false;
        }

        //determine whether activating inverseStage will fire any sort of decoupler. This
        //is used to tell whether we should delay activating the next stage after activating inverseStage
        public static bool inverseStageFiresDecoupler(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && (p is Decoupler || p is RadialDecoupler)) return true;
            }
            return false;
        }

        //determine the maximum value of temperature/maxTemperature for any part in the ship
        //as a way of determining how close we are to blowing something up from overheating
        public static double maxTemperatureRatio(Vessel v)
        {
            double maxTempRatio = 0; //rootPart.temperature / rootPart.maxTemp;
            foreach (Part p in v.parts)
            {
                double tempRatio = p.temperature / p.maxTemp;
                if (tempRatio > maxTempRatio)
                {
                    maxTempRatio = tempRatio;
                }
            }
            return maxTempRatio;
        }

        //used in calculating the drag force on the ship:
        public static double totalDragCoefficient(Vessel v)
        {
            double ret = 0;
            foreach (Part p in v.parts)
            {
                ret += p.maximum_drag * p.mass;
            }
            return ret;
        }


        //formula for drag seems to be drag force = (1/2) * (air density / 125) * (mass * max_drag) * (airspeed)^2
        //so drag acceleration is (1/2) * (air density / 125) * (average max_drag) * (airspeed)^2
        //where the max_drag average over parts is weighted by the part weight
        public static Vector3d computeDragAccel(Vector3d pos, Vector3d orbitVel, double dragCoeffOverMass, CelestialBody body)
        {
            double airDensity = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(pos, body)) / 125.0;
            Vector3d airVel = orbitVel - body.getRFrmVel(pos);
            return -0.5 * airDensity * dragCoeffOverMass * airVel.magnitude * airVel;
        }

        public static double terminalVelocity(Vessel v, Vector3d position)
        {
            double alt = FlightGlobals.getAltitudeAtPos(position);
            double localg = FlightGlobals.getGeeForceAtPosition(position).magnitude;

            if (alt > v.mainBody.maxAtmosphereAltitude) return Double.MaxValue;

            double airDensity = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(position, v.mainBody)) / 125.0;
            return Math.Sqrt(2 * localg * totalMass(v) / (totalDragCoefficient(v) * airDensity));
        }


        public static Vector3d computeTotalAccel(Vector3d pos, Vector3d orbitVel, double dragCoeffOverMass, CelestialBody body)
        {
            return FlightGlobals.getGeeForceAtPosition(pos) + computeDragAccel(pos, orbitVel, dragCoeffOverMass, body);
        }



        public static double computeApoapsis(Vector3d pos, Vector3d vel, CelestialBody body)
        {
            double r = (pos - body.position).magnitude;
            double v = vel.magnitude;
            double GM = FlightGlobals.getGeeForceAtPosition(pos).magnitude * r * r;
            double E = -GM / r + 0.5 * v * v;
            double L = Vector3d.Cross(pos - body.position, vel).magnitude;
            double apoapsis = (-GM - Math.Sqrt(GM * GM + 2 * E * L * L)) / (2 * E);
            return apoapsis - body.Radius;
        }

        public static double computePeriapsis(Vector3d pos, Vector3d vel, CelestialBody body)
        {
            double r = (pos - body.position).magnitude;
            double v = vel.magnitude;
            double GM = FlightGlobals.getGeeForceAtPosition(pos).magnitude * r * r;
            double E = -GM / r + 0.5 * v * v;
            double L = Vector3d.Cross(pos - body.position, vel).magnitude;
            double periapsis = (-GM + Math.Sqrt(GM * GM + 2 * E * L * L)) / (2 * E);
            return periapsis - body.Radius;
        }


        //utility function that displays a horizontal row of label-textbox-label and parses the number in the textbox
        public static double doGUITextInput(String leftText, float leftWidth, String currentText, float textWidth,
                                            String rightText, float rightWidth, out String newText, double defaultValue, double multiplier = 1.0)
        {
            GUIStyle leftLabelStyle = new GUIStyle(GUI.skin.label);

            double value;
            if (Double.TryParse(currentText, out value))
            {
                leftLabelStyle.normal.textColor = Color.white;
                value *= multiplier;
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
        public static int doGUITextInput(String leftText, float leftWidth, String currentText, float textWidth, String rightText, float rightWidth, out String newText, int defaultValue)
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

        public static double PQSSurfaceHeight(double latitude, double longitude, CelestialBody body)
        {
            Vector3d pqsRadialVector = QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right;
            return body.pqsController.GetSurfaceHeight(pqsRadialVector) - body.pqsController.radius;
        }

        public static double PQSAltitude(Vector3d pos, CelestialBody body)
        {
            return (pos - body.position).magnitude - body.Radius - PQSSurfaceHeight(body.GetLatitude(pos), body.GetLongitude(pos), body);
        }


        public static GUIStyle buttonStyle(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.onNormal.textColor = style.onFocused.textColor = style.onHover.textColor = style.onActive.textColor = color;
            style.normal.textColor = color;
            return style;
        }

        public static GUIStyle labelStyle(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = color;
            return style;
        }

        //keeps angles in the range -180 to 180
        public static double clampDegrees(double angle)
        {
            angle = angle + ((int)(2 + Math.Abs(angle) / 360)) * 360.0; //should be positive
            angle = angle % 360.0;
            if (angle > 180.0) return angle - 360.0;
            else return angle;
        }

        public static void debugPartStates(Part root, int level)
        {
            String space = "";
            for (int i = 0; i < level; i++) space += " ";
            MonoBehaviour.print(space + root + " - " + root.State);
            foreach (Part child in root.children)
            {
                debugPartStates(child, level + 2);
            }
        }


        public static Orbit computeOrbit(Vessel vessel, Vector3d deltaV, double UT)
        {
            Orbit ret = new Orbit();
            ret.UpdateFromStateVectors(vessel.findWorldCenterOfMass()-vessel.mainBody.position, vessel.orbit.GetVel() + deltaV, vessel.mainBody, UT);
            return ret;
        }

        public static Orbit computeOrbit(Vector3d pos, Vector3d vel, CelestialBody body, double UT)
        {
            Orbit ret = new Orbit();
            ret.UpdateFromStateVectors(pos - body.position, vel, body, UT);
            return ret;
        }

        public static void print(String s)
        {
            MonoBehaviour.print(s);
        }
    }


}
