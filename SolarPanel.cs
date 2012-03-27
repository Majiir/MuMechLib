using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechSolarPanel : MuMechToggle {
    public string fuelType = "Energy";
    public float power = 0.1F;

    protected override void onPartFixedUpdate() {
        if (on) {
            float amount = -power * TimeWarp.fixedDeltaTime;

            Dictionary<int, List<MuMechVariableTank>> cand = new Dictionary<int, List<MuMechVariableTank>>();
            int maxStage = -1;

            foreach (Part p in vessel.parts) {
                if (p is MuMechVariableTank) {
                    if ((((MuMechVariableTank)p).type == fuelType) && (((MuMechVariableTank)p).fuel < ((MuMechVariableTank)p).fullFuel)) {
                        if (!cand.ContainsKey(p.inverseStage)) {
                            cand[p.inverseStage] = new List<MuMechVariableTank>();
                        }
                        cand[p.inverseStage].Add((MuMechVariableTank)p);
                        if (p.inverseStage > maxStage) {
                            maxStage = p.inverseStage;
                        }
                    }
                }
            }

            if (maxStage != -1) {
                float partAmount = amount / cand[maxStage].Count;

                foreach (MuMechVariableTank t in cand[maxStage]) {
                    t.getFuel(fuelType, partAmount);
                }
            }
        }

        base.onPartFixedUpdate();
    }
}
