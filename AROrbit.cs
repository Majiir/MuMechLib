using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/*
 * I couldn't figure out how to use HarvesteR's Orbit class to solve for arbitrary orbits,
 * so here is a class that lets you solve for an orbit given arbitrary initial
 * conditions and then ask for the position and velocity at later times.
 * 
 */


namespace MuMech
{

    public class AROrbit
    {
        CelestialBody referenceBody;
        double GM;
        Vector3d angularMomentum;
        double eccentricity;
        double energy;
        Vector3d unitTowardPeriapsis;
        Vector3d unitNormalToPeriapsis;
        double semiMajorAxis; //negative for hyperbolic orbits
        double meanMotion; //rate at which the mean anomaly increases

        double referenceTime;
        double meanAnomalyAtReferenceTime;

        public bool hyperbolic;

        public double period;

        static bool suppressDebug = false;
        static int debugPrintCount = 0;

        public double periapsis()
        {
            return semiMajorAxis * (1 - eccentricity);
        }

        public double apoapsis()
        {
            return semiMajorAxis * (1 + eccentricity);
        }

        public Vector3d positionAtTime(double time)
        {
            double meanAnomaly = meanAnomalyAtTime(time);
            double eccentricAnomaly = eccentricAnomalyFromMeanAnomaly(meanAnomaly);
            double trueAnomaly = trueAnomalyFromEccentricAnomaly(eccentricAnomaly, meanAnomaly);
            Vector3d ret = positionAtTrueAnomaly(trueAnomaly);
            return ret;
        }

        public Vector3d velocityAtTime(double time)
        {
            double meanAnomaly = meanAnomalyAtTime(time);
            double eccentricAnomaly = eccentricAnomalyFromMeanAnomaly(meanAnomaly);
            double trueAnomaly = trueAnomalyFromEccentricAnomaly(eccentricAnomaly, meanAnomaly);
            Vector3d pos = positionAtTrueAnomaly(trueAnomaly);

            double kineticEnergy = energy + GM / (pos - referenceBody.position).magnitude;
            double speed = Math.Sqrt(2 * Math.Abs(kineticEnergy)); //abs in case precision errors make this negative

            double angleFromHorizontal = Math.Atan(eccentricity * Math.Sin(trueAnomaly) / (1 + eccentricity * Math.Cos(trueAnomaly)));

            Vector3d upUnit = (pos - referenceBody.position).normalized;
            Vector3d horizontalUnit = Vector3d.Cross(angularMomentum, upUnit).normalized;

            Vector3d vel = speed * (Math.Cos(angleFromHorizontal) * horizontalUnit + Math.Sin(angleFromHorizontal) * upUnit);

            if (double.IsNaN(vel.x))
            {
                debug("AROrbit: velocityAtTime returning NaN!!!");
            }

            return vel;
        }

        public Vector3d positionAtTrueAnomaly(double trueAnomaly)
        {
            double r = radiusAtTrueAnomaly(trueAnomaly);
            return referenceBody.position + r * Math.Cos(trueAnomaly) * unitTowardPeriapsis + r * Math.Sin(trueAnomaly) * unitNormalToPeriapsis;
        }

        public double radiusAtTrueAnomaly(double trueAnomaly)
        {
            double invR = GM / angularMomentum.sqrMagnitude * (1 + eccentricity * Math.Cos(trueAnomaly));
            return 1 / invR;
        }

        double trueAnomalyFromEccentricAnomaly(double eccAnomaly, double meanAnomaly)
        {
            if (!hyperbolic)
            {
                double cosTrueAnomaly = (Math.Cos(eccAnomaly) - eccentricity) / (1 - eccentricity * Math.Cos(eccAnomaly));
                if (cosTrueAnomaly > 1) cosTrueAnomaly = 1;
                if (cosTrueAnomaly < -1) cosTrueAnomaly = -1;
                double trueAnomaly = Math.Acos(cosTrueAnomaly);
                if (double.IsNaN(trueAnomaly))
                {
                    debug("AROrbit.trueAnomalyFromEccentricAnomaly: calculated NaN elliptic true anomaly");
                    debug("Acos argument = " + (Math.Cos(eccAnomaly) - eccentricity) / (1 - eccentricity * Math.Cos(eccAnomaly)));
                    debug("eccAnomaly = " + eccAnomaly);
                    debug("eccentricity = " + eccentricity);
                }
                if ((meanAnomaly + 2 * Math.PI) % (2 * Math.PI) < Math.PI)
                {
                    return trueAnomaly;
                }
                else
                {
                    return 2 * Math.PI - trueAnomaly;
                }
            }
            else
            {
                double cosTrueAnomaly = (Math.Cosh(eccAnomaly) - eccentricity) / (1 - eccentricity * Math.Cosh(eccAnomaly));
                if (cosTrueAnomaly > 1) cosTrueAnomaly = 1;
                if (cosTrueAnomaly < -1) cosTrueAnomaly = -1;
                double trueAnomaly = Math.Acos(cosTrueAnomaly);
                if (double.IsNaN(trueAnomaly))
                {
                    debug("AROrbit.trueAnomalyFromEccentricAnomaly: calculated NaN hyperbolic true anomaly");
                }
                if (meanAnomaly < 0) trueAnomaly = -trueAnomaly;
                return trueAnomaly;
            }
        }




        //code from http://www.projectpluto.com/kepler.htm
        double eccentricAnomalyFromMeanAnomaly(double meanAnomaly)
        {
            if (double.IsNaN(meanAnomaly)) debug("AROrbit: eccentricAnomalyFromMeanAnomaly given meanAnomaly = NaN");

            /*if (eccentricity < 0.3)
            {
                double curr = Math.Atan2(Math.Sin(meanAnomaly), Math.Cos(meanAnomaly) - eccentricity);
                double error = curr - eccentricity * Math.Sin(curr) - meanAnomaly;
                curr -= error / (1.0 - eccentricity * Math.Cos(curr));
                debug("ecc < 0.3, eccAnomaly = " + curr);
                return curr;
            }*/

            //if (meanAnomaly > Math.PI) meanAnomaly = meanAnomaly - 2 * Math.PI;
            //make sure meanAnomaly is in the range -pi to pi:
            if (meanAnomaly < 0) meanAnomaly += (1 + (int)(Math.Abs(meanAnomaly) / (2 * Math.PI))) * (2 * Math.PI);
            meanAnomaly %= (2 * Math.PI);

            bool negative = (meanAnomaly < 0);
            meanAnomaly = Math.Abs(meanAnomaly);

            //            double threshold = 1.0e-8 * Math.Abs(1 - eccentricity);
            double threshold = 1.0e-10 * Math.Abs(1 - eccentricity);

            double current = meanAnomaly;
            if ((eccentricity > 0.8 && meanAnomaly < Math.PI / 3) || eccentricity > 1.0)
            {
                double trial = meanAnomaly / Math.Abs(1 - eccentricity);
                if (trial * trial > 6.0 * Math.Abs(1 - eccentricity))
                {
                    if (meanAnomaly < Math.PI)
                    {
                        trial = Math.Pow(6.0 * meanAnomaly, 1 / 3.0);
                    }
                    else
                    {
                        trial = Asinh(meanAnomaly / eccentricity);
                    }
                }
                current = trial;
            }


            double numIters = 0;

            if (eccentricity < 1.0)
            {
                double error = current - eccentricity * Math.Sin(current) - meanAnomaly;
                while (Math.Abs(error) > threshold)
                {
                    numIters += 1;
                    current -= error / (1 - eccentricity * Math.Cos(current));
                    error = current - eccentricity * Math.Sin(current) - meanAnomaly;
                    if (numIters > 100000)
                    {
                        debug("AROrbit: kepler solver (A) failed to converge!! (meanAnomaly = " + meanAnomaly + ")");
                        break;
                    }
                }
            }
            else
            {
                double error = eccentricity * Math.Sinh(current) - current - meanAnomaly;
                while (Math.Abs(error) > threshold)
                {
                    numIters += 1;
                    current -= error / (eccentricity * Math.Cosh(current) - 1.0);
                    error = eccentricity * Math.Sinh(current) - current - meanAnomaly;

                    if (numIters > 100000)
                    {
                        debug("AROrbit: kepler solver (B) failed to converge!! (meanAnomaly = " + meanAnomaly + ")");
                        break;
                    }
                }
            }

            return (negative ? -current : current);
        }


        double meanAnomalyAtTime(double time)
        {
            return meanAnomalyAtReferenceTime + meanMotion * (time - referenceTime);
        }

        public double timeToPeriapsis(double time)
        {
            double currentMeanAnomaly = meanAnomalyAtTime(time);

            if (hyperbolic)
            {
                return -currentMeanAnomaly / meanMotion;
            }
            else
            {
                if (currentMeanAnomaly < 0)
                {
                    currentMeanAnomaly += 2 * Math.PI * (1 + (int)(Math.Abs(currentMeanAnomaly) / (2 * Math.PI)));
                }
                currentMeanAnomaly %= 2 * Math.PI;
                return (2 * Math.PI - currentMeanAnomaly) / meanMotion;
            }
        }

        public AROrbit(Vessel v)
            : this(v.findWorldCenterOfMass(), v.orbit.GetVel(), Planetarium.GetUniversalTime(), v.mainBody)
        {
        }

        public AROrbit(Vector3d pos, Vector3d vel, double time, CelestialBody body)
        {
            referenceBody = body;
            Vector3d radialVector = pos - body.position;      //radial vector from planet center to pos
            angularMomentum = Vector3d.Cross(radialVector, vel);   //angular momentum per unit mass
            GM = ARUtils.G * body.Mass;
            energy = 0.5 * vel.sqrMagnitude - GM / radialVector.magnitude; //energy per unit mass
            eccentricity = Math.Sqrt(Math.Abs(1 + 2 * energy * angularMomentum.sqrMagnitude / (GM * GM))); //abs in case precision errors make the argument negative
            hyperbolic = (eccentricity > 1.0);

            Vector3d rungeLenz = Vector3d.Cross(vel, angularMomentum) - GM * radialVector.normalized;
            unitTowardPeriapsis = rungeLenz.normalized;
            unitNormalToPeriapsis = Vector3d.Cross(angularMomentum, unitTowardPeriapsis).normalized;

            double trueAnomaly = Math.Atan2(Vector3d.Dot(radialVector, unitNormalToPeriapsis),
                                            Vector3d.Dot(radialVector, unitTowardPeriapsis));
            if (!hyperbolic)
            {
                double eccentricAnomaly = Math.Acos((eccentricity + Math.Cos(trueAnomaly)) / (1 + eccentricity * Math.Cos(trueAnomaly)));
                if (double.IsNaN(eccentricAnomaly))
                {
                    //debug("AROrbit constructor: elliptic eccentric anomaly is NaN!");
                    eccentricAnomaly = 0;
                }
                if (Vector3d.Dot(vel, radialVector) < 0) eccentricAnomaly = 2 * Math.PI - eccentricAnomaly;
                meanAnomalyAtReferenceTime = eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly);
            }
            else
            {
                double eccentricAnomaly = Acosh((eccentricity + Math.Cos(trueAnomaly)) / (1 + eccentricity * Math.Cos(trueAnomaly)));
                if (double.IsNaN(eccentricAnomaly))
                {
                    //debug("AROrbit constructor: hyperbolic eccentric anomaly is NaN!");
                    eccentricAnomaly = 0;
                }
                if (Vector3d.Dot(vel, radialVector) < 0) eccentricAnomaly = -eccentricAnomaly;
                meanAnomalyAtReferenceTime = eccentricity * Math.Sinh(eccentricAnomaly) - eccentricAnomaly;
            }

            referenceTime = time;

            semiMajorAxis = -0.5 * GM / energy;

            meanMotion = Math.Sqrt(GM / Math.Pow(Math.Abs(semiMajorAxis), 3));
            if (double.IsNaN(meanMotion))
            {
                debug("AROrbit constructor: mean motion is NaN!");
            }

            period = 2 * Math.PI / meanMotion;
        }

        static double Asinh(double x)
        {
            return (Math.Log(x + Math.Sqrt(x * x + 1.0)));
        }


        double Acosh(double x)
        {
            return (Math.Log(x + Math.Sqrt((x * x) - 1.0)));
        }

        void debug(String s)
        {
            if (suppressDebug)
            {
                //print("(suppressed)");
                return;
            }
            print(s);
            debugPrintCount += 1;

            if (debugPrintCount > 200)
            {
                print("AROrbit: too many debugging messages. Suppressing further debugging output.");
                suppressDebug = true;
            }
        }

        void print(String s)
        {
            MonoBehaviour.print(s);
        }
    }

}