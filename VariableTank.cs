using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechVariableTank : MuMechPart {
    public string type = "Generic";
    public float fuel = 100;
    public float dryMass = 1;
    public float emptyExplosionPotential = 0;
    public float fullExplosionPotential = 1;

    private float fullMass;
    private float fullFuel;
    private VInfoBox fuelBox = null;

    public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
        partDataCollection["fuel"] = new KSPParseable(fuel, KSPParseable.Type.FLOAT);

        base.onFlightStateSave(partDataCollection);
    }

    public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
        if (parsedData.ContainsKey("fuel")) fuel = parsedData["fuel"].value_float;

        base.onFlightStateLoad(parsedData);
    }

    protected override void onPartStart() {
        stackIcon.SetIcon(DefaultIcons.FUEL_TANK);
        stackIconGrouping = StackIconGrouping.SAME_TYPE;

        fullMass = mass;
        fullFuel = fuel;

        base.onPartStart();
    }

    protected override void onFlightStart() {
        stackIcon.SetIconColor((state == PartStates.IDLE) || (state == PartStates.ACTIVE) ?  XKCDColors.BrightTeal : XKCDColors.SlateGrey);

        base.onFlightStart();
    }

    public override void OnDrawStats() {
        GUILayout.TextArea("Fuel type: " + type + "\nCapacity: " + fuel + "\nDry mass: " + dryMass, GUILayout.ExpandHeight(true));
    }

    public bool getFuel(string fuelType, float amount) {
        if ((state == PartStates.DEAD) || (fuelType != type) || (fuel <= 0)) {
            return false;
        }

        if (state != PartStates.ACTIVE) {
            force_activate();
        }

        fuel = Mathf.Clamp(fuel - amount, 0, fullFuel);
        mass = Mathf.Lerp(dryMass, fullMass, fuel / fullFuel);
        explosionPotential = Mathf.Lerp(emptyExplosionPotential, fullExplosionPotential, fuel / fullFuel);

        if (fuelBox == null) {
            fuelBox = stackIcon.DisplayInfo();
            fuelBox.SetLength(2.0F);
            XKCDColors.NextColorAlpha = 0.6F;
            fuelBox.SetMsgBgColor(XKCDColors.DarkLime);
            fuelBox.SetMsgTextColor(XKCDColors.ElectricLime);
            fuelBox.SetMessage(type);
            fuelBox.SetProgressBarBgColor(XKCDColors.DarkLime);
            fuelBox.SetProgressBarColor(XKCDColors.Yellow);
            XKCDColors.NextColorAlpha = 1.0F;
        }

        fuelBox.SetValue(fuel / fullFuel);

        if (fuel <= 0) {
            if (state == PartStates.ACTIVE) {
                deactivate();
            }

            stackIcon.SetIconColor(XKCDColors.SlateGrey);

            if (fuelBox != null) {
                stackIcon.ClearInfoBoxes();
                fuelBox = null;
            }
        }

        return true;
    }
}
