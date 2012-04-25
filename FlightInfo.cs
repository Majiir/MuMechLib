using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/*
public class FlightInfo
{
    public double time;                   //current value of Planetarium.GetUniversalTime()
    public double deltaT;                 //time since last fly-by-wire frame
    public Vector3d position;             //ship center of mass position in world coordinates
    public Vector3d eastUnit;             //unit vector pointing east, parallel to surface
    public Vector3d upUnit;               //unit vector normal to surface
    public Vector3d northUnit;            //unit vector pointing north, parallel to surface
    public Vector3d forwardUnit;          //unit vector along vertical axis of ship
    public double altitude;               //measured from planet surface
    public double radius;                 //measured from planet center
    public Vector3d surfaceVelocity;      //velocity in the rotating reference frame of the surface
    public Vector3d surfaceVelocityUnit;  //unit vector parallel to above
    public double surfaceSpeed;           //speed in the rotating reference frame of the surface
    public Vector3d orbitVelocity;        //velocity in the non-rotating reference frame external to the planet
    public Vector3d orbitVelocityUnit;    //unit vector parallel to above
    public double orbitSpeed;             //speed in the non-rotating reference frame external to the planet
    public Vector3d upUnitNormalToSurfaceVel;  //unit vector in plane of upUnit and surfaceVelocity and normal to surfaceVelocity
    public Vector3d surfaceLeftUnit;           //unit vector normal to upUnit and surfaceVelocity
    public Vector3d upUnitNormalToOrbitVel;    //unit vector in planet of upUnit and orbitVelocity and normal to orbitVelocity
    public Vector3d orbitLeftUnit;             //unit vector normal to upUnit and orbitVelocity
    public Vector3d geeAcceleration;      //acceleration vector due to gravity
    public double localg;                 //magnitude of above
    public double latitude;               //measured in radians
    public double planetRadius;           //radius of vessel.mainBody in meters
    public double maxAtmoHeight;          //atmosphere height of vessel.mainBody in meters
    public double dragCoefficient;        //total drag coefficient of the vessel
    public double mass;                   //total mass of the vessel
    public double maxThrustAccel;         //maximum thrust / mass
    public double minThrustAccel;         //minimum thrust / mass

//    public Vector3d COMvelocity;

    public void update(Vessel vessel)
    {
        time = Planetarium.GetUniversalTime();
        deltaT = TimeWarp.deltaTime;

        position = vessel.findWorldCenterOfMass();

        eastUnit = vessel.mainBody.getRFrmVel(position).normalized;
        upUnit = (position - vessel.mainBody.position).normalized;
        northUnit = Vector3d.Cross(upUnit, eastUnit);
        forwardUnit = vessel.transform.up;

        altitude = vessel.mainBody.GetAltitude(position);
        radius = (position - vessel.mainBody.position).magnitude;

        orbitVelocity = vessel.orbit.GetVel();
        orbitVelocityUnit = orbitVelocity.normalized;
        orbitSpeed = orbitVelocity.magnitude;
        surfaceVelocity = (orbitVelocity - vessel.mainBody.getRFrmVel(position));
        surfaceVelocityUnit = surfaceVelocity.normalized;
        surfaceSpeed = surfaceVelocity.magnitude;

        upUnitNormalToSurfaceVel = ARUtils.Exclude(surfaceVelocityUnit, upUnit).normalized;
        surfaceLeftUnit = -Vector3d.Cross(upUnitNormalToSurfaceVel, surfaceVelocityUnit);

        upUnitNormalToOrbitVel = ARUtils.Exclude(orbitVelocityUnit, upUnit).normalized;
        orbitLeftUnit = -Vector3d.Cross(upUnitNormalToOrbitVel, orbitVelocityUnit);

        geeAcceleration = FlightGlobals.getGeeForceAtPosition(position);
        localg = geeAcceleration.magnitude;

        latitude = vessel.mainBody.GetLatitude(position) * Math.PI / 180;

        planetRadius = vessel.mainBody.Radius;
        maxAtmoHeight = vessel.mainBody.maxAtmosphereAltitude;

        dragCoefficient = ARUtils.totalDragCoefficient(vessel);
        mass = ARUtils.totalMass(vessel);

        maxThrustAccel = ARUtils.thrustAcceleration(vessel, 1.0);
        minThrustAccel = ARUtils.thrustAcceleration(vessel, 0.0);

    }

    public double thrustAcceleration(double throttle)
    {
        return (1.0 - throttle) * minThrustAccel + throttle * maxThrustAccel;
    }
}

*/