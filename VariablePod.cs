using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class MuMechVariablePod : CommandPod
{
    public int crewCapacity = 3;

    private static Transform[] defSeat = null;

    public override void OnDrawStats()
    {
        GUILayout.TextArea("Max SAS Torque: " + maxTorque + "\nCrew Capacity: " + crewCapacity, GUILayout.ExpandHeight(true));
    }

    protected override void onPartAwake()
    {
        GameObject iS = GameObject.Find("internalSpace");
        if (iS != null)
        {
            InternalModel m = iS.transform.FindChild("mk1pod_internal").GetComponent<InternalModel>();

            if (m != null)
            {
                if (defSeat == null)
                {
                    defSeat = m.seats;
                }

                m.seats = new Transform[crewCapacity];
                for (int i = 0; i < crewCapacity; i++)
                {
                    m.seats[i] = defSeat[i % 3];
                }
            }
        }

        base.onPartAwake();
    }

    protected override void onPartStart()
    {
        GameObject iS = GameObject.Find("internalSpace");
        if ((defSeat != null) && (iS != null))
        {
            internalModel = new InternalModel();
            internalModel.seats = new Transform[crewCapacity];
            for (int i = 0; i < crewCapacity; i++)
            {
                internalModel.seats[i] = defSeat[i % 3];
            }

            InternalModel m = iS.transform.FindChild("mk1pod_internal").GetComponent<InternalModel>();
            m.seats = defSeat;
        }

        base.onPartStart();
    }
}
