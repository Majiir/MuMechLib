using System;
using System.Collections.Generic;
using UnityEngine;
using MuMech;

public class MuMechJebPod2 : CommandPod
{
    public enum BlinkStatus
    {
        NONE,
        CLOSING,
        OPENING
    }

    // special FX
    public float brainStress = 0;
    public float brainStressMax = 50.0F;
    public float brainStressMin = 1.0F;
    public float brainStressReliefClosed = 0.01F;
    public float brainStressReliefOpen = 0.5F;

    protected bool craftLoaded = false;

    public GameObject[] brain = { null, null };
    public Transform eye = null;
    public Transform innereye = null;
    public Quaternion innereyeRot;
    public Transform[] eyelid = null;
    public Quaternion[] eyelidRot = null;
    public LandingLeg[] doors = { null, null, null, null };
    FlightCamera cam = null;

    public BlinkStatus blinkStatus = BlinkStatus.NONE;
    public float blinkProgress = 0;
    public double blinkLast = 0;

    public MechJebCore core = null;

    protected override void onPartAwake()
    {
        core = new MechJebCore();
        core.part = this;
        base.onPartAwake();
    }

    public override void OnDrawStats()
    {
        GUILayout.TextArea("Max SAS Torque: " + maxTorque + "\nUnmanned pod", GUILayout.ExpandHeight(true));
    }

    public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
    {
        core.onFlightStateSave(partDataCollection);
        base.onFlightStateSave(partDataCollection);
    }

    public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
    {
        core.onFlightStateLoad(parsedData);
        base.onFlightStateLoad(parsedData);
    }

    protected override void onPartStart()
    {
        stackIcon.SetIcon(DefaultIcons.ADV_SAS);
        core.part = this;
        core.onPartStart();

        base.onPartStart();

        if (transform.Find("model/base/eye") == null)
        {
            return;
        }

        brain[0] = (GameObject)GameObject.Instantiate(UnityEngine.Resources.Load("Effects/fx_exhaustFlame_blue"));
        brain[0].name = "brain_FX";
        brain[0].transform.parent = transform.Find("model");
        brain[0].transform.localPosition = new Vector3(0, -0.2F, 0);
        brain[0].transform.localRotation = Quaternion.AngleAxis(45, Vector3.up);

        brain[0].particleEmitter.emit = false;
        brain[0].particleEmitter.maxEmission = brain[0].particleEmitter.minEmission = 0;
        brain[0].particleEmitter.maxEnergy = 0.75F;
        brain[0].particleEmitter.minEnergy = 0.25F;
        brain[0].particleEmitter.maxSize = brain[0].particleEmitter.minSize = 0.1F;
        brain[0].particleEmitter.localVelocity = Vector3.zero;
        brain[0].particleEmitter.rndVelocity = new Vector3(0.3F, 0.3F, 3.0F);
        brain[0].particleEmitter.useWorldSpace = false;

        brain[1] = (GameObject)GameObject.Instantiate(brain[0]);
        brain[1].transform.parent = transform.Find("model");
        brain[1].transform.localPosition = new Vector3(0, -0.2F, 0);
        brain[1].transform.localRotation = Quaternion.AngleAxis(-45, Vector3.up);
    }

    protected override void onFlightStart()
    {
        core.onFlightStart();
        base.onFlightStart();
    }

    protected override void onPartFixedUpdate()
    {
        core.onPartFixedUpdate();
        base.onPartFixedUpdate();

        if (eye == null)
        {
            eye = transform.Find("model/base/eye");
            if (eye == null)
            {
                return;
            }
        }
        if (cam == null)
        {
            cam = (FlightCamera)GameObject.FindObjectOfType(typeof(FlightCamera));
        }

        if (innereye == null)
        {
            eyelid = new Transform[2];
            eyelidRot = new Quaternion[2];

            eyelid[0] = eye.Find("eyelid1");
            eyelid[1] = eye.Find("eyelid2");
            if (eyelid[0] != null)
            {
                eyelidRot[0] = eyelid[0].localRotation;
            }
            if (eyelid[1] != null)
            {
                eyelidRot[1] = eyelid[1].localRotation;
            }

            innereye = eye.Find("innereye");
            if (innereye != null)
            {
                innereyeRot = innereye.localRotation;
            }

            doors = new LandingLeg[] { null, null, null, null };
            if (vessel != null)
            {
                foreach (Part p in vessel.parts)
                {
                    if (p.name == "MultiMech.JebPod.Leg")
                    {
                        doors[0] = (LandingLeg)p;
                        for (int i = 0; i < Math.Min(3, p.symmetryCounterparts.Count); i++)
                        {
                            doors[i + 1] = (LandingLeg)p.symmetryCounterparts[i];
                        }
                    }
                }
            }
        }

        brainStress += core.stress * TimeWarp.fixedDeltaTime;
        brainStress -= brainStress * (((doors[0] == null) || (doors[0].legState == LandingLeg.LegStates.DEPLOYED)) ? brainStressReliefOpen : brainStressReliefClosed) * TimeWarp.fixedDeltaTime;

        if ((brainStress > brainStressMin) && ((doors[0] == null) || (doors[0].legState == LandingLeg.LegStates.DEPLOYED)))
        {
            brain[0].particleEmitter.maxEmission = brain[1].particleEmitter.maxEmission = (brainStress - brainStressMin) * 1000.0F;
            brain[0].particleEmitter.minEmission = brain[1].particleEmitter.minEmission = brain[0].particleEmitter.maxEmission / 2.0F;
            brain[0].particleEmitter.emit = brain[1].particleEmitter.emit = true;
        }
        else
        {
            brain[0].particleEmitter.emit = brain[1].particleEmitter.emit = false;
        }

        if (brainStress >= brainStressMax)
        {
            foreach (LandingLeg door in doors)
            {
                if ((door != null) && (door.legState == LandingLeg.LegStates.RETRACTED))
                {
                    door.DeployOnActivate = true;
                    door.force_activate();
                }
            }
        }

        if (vessel.Landed || vessel.Splashed)
        {
            eye.localRotation = Quaternion.RotateTowards(eye.localRotation, Quaternion.identity, 360 * TimeWarp.fixedDeltaTime);

            if ((eyelid[0] != null) && (eyelid[1] != null))
            {
                eyelid[0].localRotation = eyelidRot[0];
                eyelid[1].localRotation = eyelidRot[1];
            }
        }
        else
        {
            if (core.attitudeActive)
            {
                eye.rotation = Quaternion.RotateTowards(eye.rotation, core.attitudeGetReferenceRotation(core.attitudeReference) * core.attitudeTarget * Quaternion.Euler(90, 0, 0), 360 * TimeWarp.fixedDeltaTime);
            }
            else
            {
                if (brainStress < brainStressMin)
                {
                    eye.rotation = Quaternion.RotateTowards(eye.rotation, Quaternion.LookRotation((cam.transform.position - eye.position).normalized, cam.transform.up) * Quaternion.Euler(90, 0, 0), 360 * TimeWarp.fixedDeltaTime);
                }
                else
                {
                    eye.localRotation = Quaternion.RotateTowards(eye.localRotation, Quaternion.identity, 360 * TimeWarp.fixedDeltaTime);
                }
            }

            if ((eyelid[0] == null) || (eyelid[1] == null) || (innereye == null))
            {
                return;
            }

            double eyeStressAngle = Math.Max(5, 27 - brainStress / 2);

            if ((blinkStatus == BlinkStatus.NONE) && (UnityEngine.Random.Range(0, 10.0F / TimeWarp.fixedDeltaTime) < (Time.time - blinkLast)))
            {
                blinkStatus = BlinkStatus.CLOSING;
                blinkProgress = 0;
            }

            switch (blinkStatus)
            {
                case BlinkStatus.CLOSING:
                    eyeStressAngle *= 1 - blinkProgress;
                    blinkProgress = Mathf.Clamp01(blinkProgress + TimeWarp.fixedDeltaTime * 10);
                    if (blinkProgress >= 1)
                    {
                        blinkStatus = BlinkStatus.OPENING;
                        blinkProgress = 0;
                    }
                    break;
                case BlinkStatus.OPENING:
                    eyeStressAngle *= blinkProgress;
                    blinkProgress = Mathf.Clamp01(blinkProgress + TimeWarp.fixedDeltaTime * 10);
                    if (blinkProgress >= 1)
                    {
                        blinkStatus = BlinkStatus.NONE;
                        blinkLast = Time.time;
                    }
                    break;
            }

            eyelid[0].localRotation = eyelidRot[0] * Quaternion.AngleAxis((float)eyeStressAngle, -Vector3.right);
            eyelid[1].localRotation = eyelidRot[1] * Quaternion.AngleAxis((float)eyeStressAngle, Vector3.right);

            innereye.localRotation = innereyeRot * Quaternion.AngleAxis((Mathf.PingPong(Time.time, 0.05f) * 40 - 1) * Mathf.Sqrt(brainStress) / 10, Vector3.forward);
        }
    }

    protected override void onPartUpdate()
    {
        core.onPartUpdate();
        base.onPartUpdate();
    }

    protected override void onDisconnect()
    {
        core.onDisconnect();
        base.onDisconnect();
    }

    protected override void onPartDestroy()
    {
        core.onPartDestroy();
        base.onPartDestroy();
    }

    protected override void onGamePause()
    {
        core.onGamePause();
        base.onGamePause();
    }

    protected override void onGameResume()
    {
        core.onGameResume();
        base.onGameResume();
    }

    protected override void onActiveFixedUpdate()
    {
        core.onActiveFixedUpdate();
        base.onActiveFixedUpdate();
    }

    protected override void onActiveUpdate()
    {
        core.onActiveUpdate();
        base.onActiveUpdate();
    }

    public override void onBackup()
    {
        /*
        if (!craftLoaded)
        {
            if ((this.children.Count == 0))
            {
                try
                {
                    KSP.IO.File.WriteAllText<MuMechJebPod2>(KSP.IO.File.ReadAllText<MuMechJebPod2>(KSPUtil.ApplicationRootPath + "Parts/mumech_MechJebPod2/default.craft"), KSPUtil.ApplicationRootPath + "Ships/__mechjebpod_tmp.craft");
                    //System.IO.File.Copy(KSPUtil.ApplicationRootPath + "Parts/mumech_MechJebPod2/default.craft", KSPUtil.ApplicationRootPath + "Ships/__mechjebpod_tmp.craft", true);
                }
                catch (Exception)
                {
                }
                EditorLogic.LoadShipFromFile("__mechjebpod_tmp");
                return;
            }
            else
            {
                try
                {
                    KSP.IO.File.Delete<MuMechJebPod2>(KSPUtil.ApplicationRootPath + "Ships/__mechjebpod_tmp.craft");
                    //System.IO.File.Delete(KSPUtil.ApplicationRootPath + "Ships/__mechjebpod_tmp.craft");
                }
                catch (Exception)
                {
                }
            }
            craftLoaded = true;
        }
        */
        core.onBackup();
        base.onBackup();
    }

    protected override void onDecouple(float breakForce)
    {
        core.onDecouple(breakForce);
        base.onDecouple(breakForce);
    }

    protected override void onFlightStartAtLaunchPad()
    {
        core.onFlightStartAtLaunchPad();
        base.onFlightStartAtLaunchPad();
    }

    protected override void onPack()
    {
        core.onPack();
        base.onPack();
    }

    protected override bool onPartActivate()
    {
        core.onPartActivate();
        return base.onPartActivate();
    }

    protected override void onPartDeactivate()
    {
        core.onPartDeactivate();
        base.onPartDeactivate();
    }

    protected override void onPartDelete()
    {
        core.onPartDelete();
        base.onPartDelete();
    }

    protected override void onPartExplode()
    {
        core.onPartExplode();
        base.onPartExplode();
    }

    protected override void onPartLiftOff()
    {
        core.onPartLiftOff();
        base.onPartLiftOff();
    }

    protected override void onPartLoad()
    {
        core.onPartLoad();
        base.onPartLoad();
    }

    protected override void onPartSplashdown()
    {
        core.onPartSplashdown();
        base.onPartSplashdown();
    }

    protected override void onPartTouchdown()
    {
        core.onPartTouchdown();
        base.onPartTouchdown();
    }

    protected override void onUnpack()
    {
        core.onUnpack();
        base.onUnpack();
    }

    protected override void onPartAttach(Part parent)
    {
        core.onPartAttach(parent);
        base.onPartAttach(parent);
    }

    protected override void onPartDetach()
    {
        core.onPartDetach();
        base.onPartDetach();
    }

}
