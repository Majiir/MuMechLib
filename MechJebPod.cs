using System;
using System.Collections.Generic;
using UnityEngine;
using MuMech;

class MuMechJebPod : CommandPod {
    private static Transform[] defSeat = null;

    public enum ShellStat {
        CLOSED,
        EJECTING,
        EJECTED
    }

    // special FX
    public ShellStat shellStat = ShellStat.CLOSED;
    public float shellDecay = 0;
    public float brainStress = 0;
    public float brainStressMax = 50.0F;
    public float brainStressMin = 1.0F;
    public float brainStressReliefClosed = 0.01F;
    public float brainStressReliefOpen = 0.5F;

    public GameObject brain = null;
    public bool lightUp = false;
    public Transform eye = null;
    FlightCamera cam = null;

    public MechJebCore core = null;

    protected override void onPartAwake() {
        core = new MechJebCore();
        core.part = this;

        GameObject iS = GameObject.Find("internalSpace");
        if (iS != null) {
            InternalModel m = iS.transform.FindChild("mk1pod_internal").GetComponent<InternalModel>();

            if (m != null) {
                if (defSeat == null) {
                    defSeat = (Transform[])m.seats.Clone();
                }

                m.seats = new Transform[0];
            }
        }

        base.onPartAwake();
    }

    public override void OnDrawStats() {
        GUILayout.TextArea("Max SAS Torque: " + maxTorque + "\nUnmanned pod", GUILayout.ExpandHeight(true));
    }

    public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
        core.onFlightStateSave(partDataCollection);
        base.onFlightStateSave(partDataCollection);
    }

    public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
        core.onFlightStateLoad(parsedData);
        base.onFlightStateLoad(parsedData);
    }

    protected override void onPartStart() {
        stackIcon.SetIcon(DefaultIcons.ADV_SAS);

        GameObject iS = GameObject.Find("internalSpace");
        if (iS != null) {
            InternalModel m = iS.transform.FindChild("mk1pod_internal").GetComponent<InternalModel>();
            if (m.seats.Length == 0) {
                m.seats = defSeat;
            }

            internalModel = new InternalModel();
        }

        core.part = this;
        core.onPartStart();

        base.onPartStart();

        if (transform.Find("model/base/eye") == null) {
            return;
        }

        brain = (GameObject)GameObject.Instantiate(Resources.Load("Effects/fx_exhaustFlame_blue"));
        brain.name = "brain_FX";
        brain.transform.parent = transform.Find("model");
        brain.transform.localPosition = new Vector3(0, 0.2F, 0);
        brain.transform.localRotation = Quaternion.identity;

        brain.AddComponent<Light>();
        brain.light.color = XKCDColors.ElectricBlue;
        brain.light.intensity = 0;
        brain.light.range = 2.0F;

        brain.particleEmitter.emit = false;
        brain.particleEmitter.maxEmission = brain.particleEmitter.minEmission = 0;
        brain.particleEmitter.maxEnergy = 0.75F;
        brain.particleEmitter.minEnergy = 0.25F;
        brain.particleEmitter.maxSize = brain.particleEmitter.minSize = 0.1F;
        brain.particleEmitter.localVelocity = Vector3.zero;
        brain.particleEmitter.rndVelocity = new Vector3(3.0F, 0.5F, 3.0F);
        brain.particleEmitter.useWorldSpace = false;
    }

    protected override void onFlightStart() {
        core.onFlightStart();
        base.onFlightStart();
    }

    protected override void onPartFixedUpdate() {
        core.onPartFixedUpdate();
        base.onPartFixedUpdate();

        if (eye == null) {
            eye = transform.Find("model/base/eye");
            if (eye == null) {
                return;
            }
        }
        if (cam == null) {
            cam = (FlightCamera)GameObject.FindObjectOfType(typeof(FlightCamera));
        }

        if (core.rotationActive) {
            eye.rotation = Quaternion.RotateTowards(eye.rotation, core.rotationGetReferenceRotation(core.rotationReference) * Quaternion.Euler(90, 0, 0), 360 * TimeWarp.fixedDeltaTime);
        } else {
            if (brainStress < brainStressMin) {
                eye.rotation = Quaternion.RotateTowards(eye.rotation, Quaternion.LookRotation((cam.transform.position - eye.position).normalized, cam.transform.up) * Quaternion.Euler(90, 0, 0), 360 * TimeWarp.fixedDeltaTime);
            } else {
                eye.localRotation = Quaternion.RotateTowards(eye.localRotation, Quaternion.identity, 360 * TimeWarp.fixedDeltaTime);
            }
        }

        brainStress += core.stress * TimeWarp.fixedDeltaTime;
        brainStress -= brainStress * ((shellStat != ShellStat.CLOSED) ? brainStressReliefOpen : brainStressReliefClosed) * TimeWarp.fixedDeltaTime;

        if (brainStress < brainStressMin) {
            brain.particleEmitter.emit = false;
            lightUp = false;
            brain.light.intensity = 0;
        } else {
            if (shellStat != ShellStat.CLOSED) {
                brain.particleEmitter.maxEmission = (brainStress - brainStressMin) * 1000.0F;
                brain.particleEmitter.minEmission = brain.particleEmitter.maxEmission / 2.0F;
                brain.particleEmitter.emit = true;
            }

            if (lightUp) {
                brain.light.intensity -= TimeWarp.fixedDeltaTime * Mathf.Sqrt(brainStress);
                if (brain.light.intensity <= 0) {
                    lightUp = false;
                }
            } else {
                brain.light.intensity += TimeWarp.fixedDeltaTime * Mathf.Sqrt(brainStress);
                if (brain.light.intensity >= 1.0F) {
                    lightUp = true;
                }
            }
        }

        if ((shellStat == ShellStat.CLOSED) && ((Input.GetKey(KeyCode.KeypadPeriod)) || (brainStress >= brainStressMax))) {
            shellStat = ShellStat.EJECTING;
            shellDecay = 0;
        }

        if (shellStat == ShellStat.EJECTING) {
            shellDecay += TimeWarp.fixedDeltaTime;

            transform.Find("model/base/shell01").position += (transform.Find("model/base/shell01").position - transform.Find("model/base").position).normalized * 50 * TimeWarp.fixedDeltaTime;
            transform.Find("model/base/shell02").position += (transform.Find("model/base/shell02").position - transform.Find("model/base").position).normalized * 50 * TimeWarp.fixedDeltaTime;
            transform.Find("model/base/shell03").position += (transform.Find("model/base/shell03").position - transform.Find("model/base").position).normalized * 50 * TimeWarp.fixedDeltaTime;

            if (shellDecay >= 5) {
                GameObject.Destroy(transform.Find("model/base/shell01").gameObject);
                GameObject.Destroy(transform.Find("model/base/shell02").gameObject);
                GameObject.Destroy(transform.Find("model/base/shell03").gameObject);
                shellStat = ShellStat.EJECTED;
            }
        }
    }

    protected override void onPartUpdate() {
        core.onPartUpdate();
        base.onPartUpdate();
    }

    protected override void onDisconnect() {
        core.onDisconnect();
        base.onDisconnect();
    }

    protected override void onPartDestroy() {
        core.onPartDestroy();
        base.onPartDestroy();
    }

    protected override void onGamePause() {
        core.onGamePause();
        base.onGamePause();
    }

    protected override void onGameResume() {
        core.onGameResume();
        base.onGameResume();
    }

    protected override void onActiveFixedUpdate() {
        core.onActiveFixedUpdate();
        base.onActiveFixedUpdate();
    }

    protected override void onActiveUpdate() {
        core.onActiveUpdate();
        base.onActiveUpdate();
    }

    public override void onBackup() {
        core.onBackup();
        base.onBackup();
    }

    protected override void onDecouple(float breakForce) {
        core.onDecouple(breakForce);
        base.onDecouple(breakForce);
    }

    protected override void onFlightStartAtLaunchPad() {
        core.onFlightStartAtLaunchPad();
        base.onFlightStartAtLaunchPad();
    }

    protected override void onPack() {
        core.onPack();
        base.onPack();
    }

    protected override bool onPartActivate() {
        core.onPartActivate();
        return base.onPartActivate();
    }

    protected override void onPartDeactivate() {
        core.onPartDeactivate();
        base.onPartDeactivate();
    }

    protected override void onPartDelete() {
        core.onPartDelete();
        base.onPartDelete();
    }

    protected override void onPartExplode() {
        core.onPartExplode();
        base.onPartExplode();
    }

    protected override void onPartLiftOff() {
        core.onPartLiftOff();
        base.onPartLiftOff();
    }

    protected override void onPartLoad() {
        core.onPartLoad();
        base.onPartLoad();
    }

    protected override void onPartSplashdown() {
        core.onPartSplashdown();
        base.onPartSplashdown();
    }

    protected override void onPartTouchdown() {
        core.onPartTouchdown();
        base.onPartTouchdown();
    }

    protected override void onUnpack() {
        core.onUnpack();
        base.onUnpack();
    }
}
