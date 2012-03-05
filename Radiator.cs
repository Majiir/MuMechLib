using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechRadiator : Part {
    private VInfoBox tempIndicator;

    protected override void onPartStart() {
        stackIcon.SetIcon(DefaultIcons.STRUT);
        stackIconGrouping = StackIconGrouping.SAME_MODULE;
        fuelCrossFeed = true;
    }

    protected override void onPartFixedUpdate() {
        if (tempIndicator == null) {
            tempIndicator = stackIcon.DisplayInfo();
            if (tempIndicator != null) {
                XKCDColors.NextColorAlpha = 0.6F;
                tempIndicator.SetMsgBgColor(XKCDColors.DarkRed);
                tempIndicator.SetMsgTextColor(XKCDColors.OrangeYellow);
                tempIndicator.SetMessage("Heat");
                tempIndicator.SetProgressBarBgColor(XKCDColors.DarkRed);
                tempIndicator.SetProgressBarColor(XKCDColors.OrangeYellow);
                XKCDColors.NextColorAlpha = 1.0F;
            }
        }
        if (tempIndicator != null) {
            tempIndicator.SetValue(temperature / maxTemp, 0.0F, 1.0F);
        }
    }

    public override void OnDrawStats() {
        GUILayout.TextArea("Dissipation rate: " + heatDissipation*100 + "%/s\nConductivity: "+ heatConductivity*100 +"%" , GUILayout.ExpandHeight(true));
    }
}
