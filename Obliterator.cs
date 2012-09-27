using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

class MuMechObliteratorGhost : MonoBehaviour
{
    public string planetName;
    public string parentName;
    public List<string> targetNames;
    public MuMechObliterator part;

    void Start()
    {
        planetName = part.planetName;
        parentName = part.parentName;
        targetNames = part.targetNames.Split("/".ToCharArray()).ToList<string>();
    }

    void Update()
    {
        if (FlightGlobals.ready)
        {
            foreach (OrbitDriver o in Planetarium.Orbits)
            {
                if (o.name == planetName)
                {
                    GameObject planet = o.gameObject;
                    Transform parentObject = o.transform.Find(parentName);

                    if (parentObject != null)
                    {
                        for (int i = 0; i < parentObject.childCount; i++)
                        {
                            foreach (string n in targetNames)
                            {
                                if (parentObject.GetChild(i).name.StartsWith(n))
                                {
                                    GameObject target = parentObject.GetChild(i).gameObject;
                                    print("Obliterator - Destroying " + target.name);
                                    target.transform.parent = null;
                                    GameObject.Destroy(target);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

class MuMechObliterator : Part
{
    public string planetName = "";
    public string parentName = "";
    public string targetNames = "";

    protected override void onPartLoad()
    {
        GameObject ghost = new GameObject("obliteratorGhost_" + name);
        MuMechObliteratorGhost og = ghost.AddComponent<MuMechObliteratorGhost>();
        og.part = this;
        GameObject.DontDestroyOnLoad(ghost);
        base.onPartLoad();
    }

    protected override void onPartStart()
    {
        if (GameObject.Find("interior_vehicleassembly") != null)
        {
            GameObject.Destroy(GameObject.Find("interior_vehicleassembly"));
            Camera.mainCamera.GetComponent<VABCamera>().maxHeight *= 100;
            Camera.mainCamera.GetComponent<VABCamera>().maxDistance *= 100;
        }
        if (GameObject.Find("xport_sph3") != null)
        {
            GameObject.Destroy(GameObject.Find("xport_sph3"));
            Camera.mainCamera.GetComponent<SPHCamera>().maxHeight *= 100;
            Camera.mainCamera.GetComponent<SPHCamera>().maxDistance *= 100;
        }
    }
}
