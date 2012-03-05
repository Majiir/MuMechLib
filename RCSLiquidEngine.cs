using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechRCSLiquidEngine : LiquidEngine {
    protected override void onActiveFixedUpdate() {
        fuelLookupTargets.Add(this);
        base.onActiveFixedUpdate();
    }
    public override bool RequestFuel(Part source, float amount, uint reqId) {
        fuelLookupTargets.Remove(this);
        return vessel.rootPart.RequestRCS(amount, 0);
    }
}
