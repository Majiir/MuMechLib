using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class MuMechRCS2Liquid : Part
{
    public float conversionRate;

    public MuMechRCS2Liquid()
    {
        conversionRate = 1.0F;
    }

    protected override void onPartStart()
    {
        stackIcon.SetIcon(DefaultIcons.FUEL_LINE);
        stackIconGrouping = StackIconGrouping.SAME_MODULE;
        fuelCrossFeed = true;
    }

    public override bool RequestFuel(Part source, float amount, uint reqId)
    {
        return vessel.rootPart.RequestRCS(amount * conversionRate, 0);
    }

    public override void OnDrawStats()
    {
        GUILayout.TextArea("Conversion rate: " + conversionRate, GUILayout.ExpandHeight(true));
    }
}
