using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class MuMechPart : Part
{
    private static int s_creationOrder = 0;
    public int creationOrder = 0;

    public static void traceTrans(string prev, Transform tr)
    {
        print(prev + "." + tr.name);
        for (int i = 0; i < tr.childCount; i++)
        {
            traceTrans(prev + "." + tr.name, tr.GetChild(i));
        }
    }

    public bool isSymmMaster()
    {
        for (int i = 0; i < symmetryCounterparts.Count; i++)
        {
            if (symmetryCounterparts[i].GetComponent<MuMechPart>().creationOrder < creationOrder)
            {
                return false;
            }
        }
        return true;
    }

    protected override void onPartStart()
    {
        base.onPartStart();
        creationOrder = s_creationOrder++;
    }
}
