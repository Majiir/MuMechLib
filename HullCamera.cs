using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class MuMechModuleHullCamera : PartModule
{
    private const bool adjustMode = false;

    public Vector3 cameraPosition = Vector3.zero;
    public Vector3 cameraForward = Vector3.forward;
    public Vector3 cameraUp = Vector3.up;
    public float cameraFoV = 60;
    public float cameraClip = 0.01f;
    public bool camActive = false;
    public bool camEnabled = true;

    [KSPField]
    public string cameraKeyPrev = "f7";
    [KSPField]
    public string cameraKeyNext = "f8";
    [KSPField]
    public string cameraName = "Hull";

    public static List<MuMechModuleHullCamera> cameras = new List<MuMechModuleHullCamera>();
    public static MuMechModuleHullCamera currentCamera = null;
    public static MuMechModuleHullCamera currentHandler = null;

    protected static FlightCamera cam = null;
    protected static Transform origParent = null;
    protected static float origFoV;
    protected static float origClip;

    public void toMainCamera()
    {
        if ((cam != null) && (cam.transform != null))
        {
            cam.transform.parent = origParent;
            Camera.mainCamera.nearClipPlane = origClip;
            foreach (Camera c in cam.GetComponentsInChildren<Camera>())
            {
                c.fov = origFoV;
            }
            cam.setTarget(FlightGlobals.ActiveVessel.transform);

            if (currentCamera != null)
            {
                currentCamera.camActive = false;
            }
            currentCamera = null;
            MapView.EnterMapView();
            MapView.ExitMapView();
        }
    }

    [KSPEvent(guiActive = true, guiName = "Activate Camera")]
    public void ActivateCamera()
    {
        if (part.State == PartStates.DEAD)
        {
            return;
        }

        camActive = !camActive;

        if (!camActive && (cam != null))
        {
            toMainCamera();
        }
        else
        {
            if ((currentCamera != null) && (currentCamera != this))
            {
                currentCamera.camActive = false;
            }
            currentCamera = this;
        }
    }

    [KSPEvent(guiActive = true, guiName = "Disable Camera")]
    public void EnableCamera()
    {
        if (part.State == PartStates.DEAD)
        {
            return;
        }

        camEnabled = !camEnabled;

        if (camEnabled)
        {
            if (!cameras.Contains(this))
            {
                cameras.Add(this);
            }
        }
        else
        {
            if (cameras.Contains(this))
            {
                cameras.Remove(this);
            }
        }

        if (!camEnabled && camActive)
        {
            toMainCamera();
        }
    }

    public override void OnLoad(ConfigNode node)
    {
        base.OnLoad(node);
        if (node.HasValue("cameraPosition")) cameraPosition = KSPUtil.ParseVector3(node.GetValue("cameraPosition"));
        if (node.HasValue("cameraForward")) cameraForward = KSPUtil.ParseVector3(node.GetValue("cameraForward"));
        if (node.HasValue("cameraUp")) cameraUp = KSPUtil.ParseVector3(node.GetValue("cameraUp"));
        if (node.HasValue("camEnabled")) camEnabled = bool.Parse(node.GetValue("camEnabled"));
        if (node.HasValue("cameraFoV")) cameraFoV = float.Parse(node.GetValue("cameraFoV"));
        if (node.HasValue("cameraClip")) cameraClip = float.Parse(node.GetValue("cameraClip"));

        camActive = false;
    }

    public override void OnSave(ConfigNode node)
    {
        node.AddValue("cameraPosition", KSPUtil.WriteVector(cameraPosition));
        node.AddValue("cameraForward", KSPUtil.WriteVector(cameraForward));
        node.AddValue("cameraUp", KSPUtil.WriteVector(cameraUp));
        node.AddValue("camEnabled", camEnabled.ToString());
        node.AddValue("cameraFoV", cameraFoV.ToString());
        node.AddValue("cameraClip", cameraClip.ToString());

        base.OnSave(node);
    }

    public void Update()
    {
        if (vessel == null)
        {
            return;
        }

        Events["ActivateCamera"].guiName = camActive ? "Deactivate Camera" : "Activate Camera";
        Events["EnableCamera"].guiName = camEnabled ? "Disable Camera" : "Enable Camera";

        if (currentHandler == null)
        {
            currentHandler = this;
        }

        if (currentHandler == this)
        {
            cameras.RemoveAll(item => item == null);
            
            if (Input.GetKeyUp(cameraKeyNext))
            {
                if (currentCamera != null)
                {
                    int curCam = cameras.IndexOf(currentCamera);
                    if (curCam + 1 >= cameras.Count)
                    {
                        toMainCamera();
                    }
                    else
                    {
                        cameras[curCam + 1].ActivateCamera();
                    }
                }
                else
                {
                    cameras.First().ActivateCamera();
                }
            }

            if (Input.GetKeyUp(cameraKeyPrev))
            {
                if (currentCamera != null)
                {
                    int curCam = cameras.IndexOf(currentCamera);
                    if (curCam < 1)
                    {
                        toMainCamera();
                    }
                    else
                    {
                        cameras[curCam - 1].ActivateCamera();
                    }
                }
                else
                {
                    cameras.Last().ActivateCamera();
                }
            }
        }
        
        if (adjustMode && (currentCamera == this)) {
            if (Input.GetKeyUp(KeyCode.Keypad8))
            {
                cameraPosition += cameraUp * 0.1f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad2))
            {
                cameraPosition -= cameraUp * 0.1f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad6))
            {
                cameraPosition += cameraForward * 0.1f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad4))
            {
                cameraPosition -= cameraForward * 0.1f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad7))
            {
                cameraClip += 0.05f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad1))
            {
                cameraClip -= 0.05f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad9))
            {
                cameraFoV += 5;
            }

            if (Input.GetKeyUp(KeyCode.Keypad3))
            {
                cameraFoV -= 5;
            }

            if (Input.GetKeyUp(KeyCode.KeypadMinus))
            {
                print("Position: " + cameraPosition + " - Clip = " + cameraClip + " - FoV = " + cameraFoV);
            }
        }
    }

    public void FixedUpdate()
    {
        if (vessel == null)
        {
            return;
        }

        if (cam == null)
        {
            cam = (FlightCamera)GameObject.FindObjectOfType(typeof(FlightCamera));
        }

        if ((cam != null) && (origParent == null))
        {
            origParent = cam.transform.parent;
            origClip = Camera.mainCamera.nearClipPlane;
            origFoV = Camera.mainCamera.fov;
        }

        if (camActive && (part.State == PartStates.DEAD))
        {
            CleanUp();
        }

        if (part.State == PartStates.DEAD)
        {
            camEnabled = false;
        }

        if ((part.State == PartStates.DEAD) && cameras.Contains(this))
        {
            CleanUp();
        }

        if (!cameras.Contains(this) && (part.State != PartStates.DEAD))
        {
            cameras.Add(this);
        }

        if ((origParent != null) && (cam != null) && camActive)
        {
            cam.setTarget(null);
            cam.transform.parent = part.transform;
            cam.transform.localPosition = cameraPosition;
            cam.transform.localRotation = Quaternion.LookRotation(cameraForward, cameraUp);
            foreach (Camera c in cam.GetComponentsInChildren<Camera>())
            {
                c.fov = cameraFoV;
            }
            Camera.mainCamera.nearClipPlane = cameraClip;
        }

        base.OnFixedUpdate();
    }

    public override void OnStart(StartState state)
    {
        //part.name = cameraName + " Camera";

        if (camEnabled && (state != StartState.None) && (state != StartState.Editor))
        {
            if (!cameras.Contains(this))
            {
                cameras.Add(this);
            }
            vessel.OnJustAboutToBeDestroyed += CleanUp;
        }
        part.OnJustAboutToBeDestroyed += CleanUp;
        part.OnEditorDestroy += CleanUp;

        base.OnStart(state);
    }

    public void CleanUp()
    {
        if (camActive)
        {
            toMainCamera();
        }

        if (currentCamera == this)
        {
            currentCamera = null;
        }

        if (currentHandler == this)
        {
            currentHandler = null;
        }

        if (cameras.Contains(this))
        {
            cameras.Remove(this);
        }
    }

    public void OnDestroy()
    {
        CleanUp();
    }
}
