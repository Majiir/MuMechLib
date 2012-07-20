using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

class MuMechKerbalAccessorizerGhost : MonoBehaviour
{
    GameObject glasses;

    void Start()
    {
        glasses = PartReader.Read(KSPUtil.ApplicationRootPath + "Parts/mumech_accessorizer/", "model", ".mu");
        glasses.name = "glasses";
        glasses.transform.position = new Vector3(1e10f, 1e10f, 1e10f);
        GameObject.DontDestroyOnLoad(glasses);
    }

    void Update()
    {
        if (FlightGlobals.ready)
        {
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.GetComponent<KerbalEVA>() != null)
                {
                    if (v.transform.Find("globalMove01/joints01/bn_spA01/bn_spB01/bn_spc01/bn_spD01/be_spE01/bn_neck01/be_neck01/bn_headPivot_a01/bn_headPivot_b01/glasses") == null)
                    {
                        Transform mesh = v.transform.Find("model01/head02/headMesh01");
                        GameObject newGlasses = (GameObject)GameObject.Instantiate(glasses);
                        newGlasses.name = "glasses";
                        newGlasses.transform.parent = v.transform.Find("globalMove01/joints01/bn_spA01/bn_spB01/bn_spc01/bn_spD01/be_spE01/bn_neck01/be_neck01/bn_headPivot_a01/bn_headPivot_b01");
                        newGlasses.transform.position = mesh.position + mesh.up * -0.15f + mesh.forward * -0.02f;
                        newGlasses.transform.rotation = mesh.rotation;
                    }
                }
            }
        }
    }
}

class MuMechKerbalAccessorizer : Part
{
    protected override void onPartLoad()
    {
        GameObject ghost = new GameObject("accessorizerGhost");
        ghost.AddComponent<MuMechKerbalAccessorizerGhost>();
        GameObject.DontDestroyOnLoad(ghost);
        base.onPartLoad();
    }
}
