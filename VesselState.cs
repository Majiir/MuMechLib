using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MuMech;

namespace MuMech {
    class VesselState {
        public Vector3d CoM;
        public Vector3d MoI;
        public Vector3d up;

        public Quaternion rotationSurface;
        public Quaternion rotationVesselSurface;

        public Vector3d velocityMainBodySurface;
        public Vector3d velocityVesselSurface;
        public Vector3d velocityVesselOrbit;
        public Vector3d gravityForce;

        public MovingAverage speedOrbital = new MovingAverage();
        public MovingAverage speedSurface = new MovingAverage();
        public MovingAverage speedVertical = new MovingAverage();
        public MovingAverage speedHorizontal = new MovingAverage();
        public MovingAverage vesselHeading = new MovingAverage();
        public MovingAverage vesselPitch = new MovingAverage();
        public MovingAverage vesselRoll = new MovingAverage();
        public MovingAverage altitudeASL = new MovingAverage();
        public MovingAverage altitudeTrue = new MovingAverage();
        public MovingAverage orbitApA = new MovingAverage();
        public MovingAverage orbitPeA = new MovingAverage();
        public MovingAverage orbitPeriod = new MovingAverage();
        public MovingAverage orbitTimeToAp = new MovingAverage();
        public MovingAverage orbitTimeToPe = new MovingAverage();
        public MovingAverage orbitLAN = new MovingAverage();
        public MovingAverage orbitArgumentOfPeriapsis = new MovingAverage();
        public MovingAverage orbitInclination = new MovingAverage();
        public MovingAverage orbitEccentricity = new MovingAverage();
        public MovingAverage orbitSemiMajorAxis = new MovingAverage();
        public MovingAverage latitude = new MovingAverage();
        public MovingAverage longitude = new MovingAverage();

        public double mass;
        public double thrustAvailable;
        public double massDrag;
        public double atmosphericDensity;

        public void Update(Vessel vessel) {
            CoM = vessel.findWorldCenterOfMass();
            MoI = vessel.findLocalMOI(CoM);
            up = (CoM - vessel.mainBody.position).normalized;

            Vector3d north = Vector3.Exclude(up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - CoM).normalized;
            rotationSurface = Quaternion.LookRotation(north, up);
            rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.transform.rotation) * rotationSurface);

            velocityVesselOrbit = vessel.orbit.GetVel();
            velocityVesselSurface = velocityVesselOrbit  - vessel.mainBody.getRFrmVel(CoM);
            velocityMainBodySurface = rotationSurface * velocityVesselSurface;
            gravityForce = FlightGlobals.getGeeForceAtPosition(CoM);

            speedOrbital.value = velocityVesselOrbit.magnitude;
            speedSurface.value = velocityVesselSurface.magnitude;
            speedVertical.value = Vector3d.Dot(velocityVesselSurface, up);
            speedHorizontal.value = (velocityVesselSurface - (speedVertical * up)).magnitude;

            vesselHeading.value = rotationVesselSurface.eulerAngles.y;
            vesselPitch.value = (rotationVesselSurface.eulerAngles.x > 180) ? (360.0 - rotationVesselSurface.eulerAngles.x) : -rotationVesselSurface.eulerAngles.x;
            vesselRoll.value = (rotationVesselSurface.eulerAngles.z > 180) ? (rotationVesselSurface.eulerAngles.z - 360.0) : rotationVesselSurface.eulerAngles.z;

            altitudeASL.value = vessel.mainBody.GetAltitude(CoM);
            RaycastHit sfc;
            Physics.Raycast(CoM, -up, out sfc, (float)altitudeASL + 1000.0F, 1 << 15);
            altitudeTrue.value = sfc.distance;

            atmosphericDensity = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(altitudeASL, vessel.mainBody));

            orbitApA.value = vessel.orbit.ApA;
            orbitPeA.value = vessel.orbit.PeA;
            orbitPeriod.value = vessel.orbit.period;
            orbitTimeToAp.value = vessel.orbit.timeToAp;
            orbitTimeToPe.value = vessel.orbit.timeToPe;
            orbitLAN.value = vessel.orbit.LAN;
            orbitArgumentOfPeriapsis.value = vessel.orbit.argumentOfPeriapsis;
            orbitInclination.value = vessel.orbit.inclination;
            orbitEccentricity.value = vessel.orbit.eccentricity;
            orbitSemiMajorAxis.value = vessel.orbit.semiMajorAxis;
            latitude.value = vessel.mainBody.GetLatitude(CoM);
            longitude.value = vessel.mainBody.GetLongitude(CoM);

            mass = thrustAvailable = massDrag = 0;
            foreach (Part p in vessel.parts) {
                mass += p.mass;
                massDrag += p.mass * p.maximum_drag;
                if (((p.State == PartStates.ACTIVE) || ((Staging.CurrentStage > Staging.LastStage) && (p.inverseStage == Staging.LastStage))) && ((p is LiquidEngine) || (p is SolidRocket))) {
                    if (p is LiquidEngine) {
                        thrustAvailable += ((LiquidEngine)p).maxThrust;
                    } else if (p is SolidRocket) {
                        thrustAvailable += ((SolidRocket)p).thrust;
                    }
                }
            }
        }

        public double TerminalVelocity() {
            return Math.Sqrt((2 * gravityForce.magnitude * mass) / (0.009785 * Math.Exp(-altitudeASL / 5000.0) * massDrag));
        }
    }
}
