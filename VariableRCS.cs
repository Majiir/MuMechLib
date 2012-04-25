using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class MuMechVariableRCS : RCSModule
{
    public string fuelType = "rcs";
    public string fuelType2 = "";
    public float fuelConsumption2 = 0;

    public bool RequestFuelType(string type, float amount, uint reqId)
    {
        if (type.ToLowerInvariant() == "rcs")
        {
            Dictionary<int, List<RCSFuelTank>> cand = new Dictionary<int, List<RCSFuelTank>>();
            int maxStage = -1;

            foreach (Part p in vessel.parts)
            {
                if (p is RCSFuelTank)
                {
                    if ((p.State == PartStates.ACTIVE) || (p.State == PartStates.IDLE))
                    {
                        if (!cand.ContainsKey(p.inverseStage))
                        {
                            cand[p.inverseStage] = new List<RCSFuelTank>();
                        }
                        cand[p.inverseStage].Add((RCSFuelTank)p);
                        if (p.inverseStage > maxStage)
                        {
                            maxStage = p.inverseStage;
                        }
                    }
                }
            }

            if (maxStage == -1)
            {
                return false;
            }

            float partAmount = amount / cand[maxStage].Count;

            foreach (RCSFuelTank t in cand[maxStage])
            {
                t.getFuel(partAmount);
            }

            return true;
        }
        else if (type.ToLowerInvariant() == "liquid")
        {
            Dictionary<int, List<FuelTank>> cand = new Dictionary<int, List<FuelTank>>();
            int maxStage = -1;

            foreach (Part p in vessel.parts)
            {
                if (p is FuelTank)
                {
                    if ((p.State == PartStates.ACTIVE) || (p.State == PartStates.IDLE))
                    {
                        if (!cand.ContainsKey(p.inverseStage))
                        {
                            cand[p.inverseStage] = new List<FuelTank>();
                        }
                        cand[p.inverseStage].Add((FuelTank)p);
                        if (p.inverseStage > maxStage)
                        {
                            maxStage = p.inverseStage;
                        }
                    }
                }
            }

            if (maxStage == -1)
            {
                return false;
            }

            float partAmount = amount / cand[maxStage].Count;

            foreach (FuelTank t in cand[maxStage])
            {
                t.RequestFuel(this, partAmount, reqId);
            }

            return true;
        }
        else
        {
            Dictionary<int, List<MuMechVariableTank>> cand = new Dictionary<int, List<MuMechVariableTank>>();
            int maxStage = -1;

            foreach (Part p in vessel.parts)
            {
                if (p is MuMechVariableTank)
                {
                    if ((((MuMechVariableTank)p).type == type) && (((MuMechVariableTank)p).fuel > 0))
                    {
                        if (!cand.ContainsKey(p.inverseStage))
                        {
                            cand[p.inverseStage] = new List<MuMechVariableTank>();
                        }
                        cand[p.inverseStage].Add((MuMechVariableTank)p);
                        if (p.inverseStage > maxStage)
                        {
                            maxStage = p.inverseStage;
                        }
                    }
                }
            }

            if (maxStage == -1)
            {
                return false;
            }

            float partAmount = amount / cand[maxStage].Count;

            foreach (MuMechVariableTank t in cand[maxStage])
            {
                t.getFuel(type, partAmount);
            }

            return true;
        }
    }

    public override bool RequestRCS(float amount, int earliestStage)
    {
        if (!RequestFuelType(fuelType, amount, Part.getFuelReqId()))
        {
            return false;
        }

        if ((fuelConsumption2 > 0) && (fuelType2 != "") && !RequestFuelType(fuelType2, amount * (fuelConsumption2 / fuelConsumption), Part.getFuelReqId()))
        {
            return false;
        }

        return true;
    }
}
