using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class MuMechLiquidRCSModule : RCSModule
{
    public override bool RequestRCS(float amount, int earliestStage)
    {
        return RequestFuel(this, amount, Part.getFuelReqId());
    }
}
