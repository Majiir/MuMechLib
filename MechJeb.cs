using System;
using System.Collections.Generic;
using UnityEngine;
using MuMech;

class MuMechJeb : MuMechPart {
    public enum DoorStat {
        CLOSED,
        OPENING,
        OPEN,
        CLOSING
    }

    // special FX
    public DoorStat doorStat = DoorStat.CLOSED;
    public float doorProgr = 0;
    public float doorSpeed = 1.0F;
    public float doorChance = 0.2F;
    public float doorStress = 0;
    public float doorStressMax = 10.0F;
    public float doorStressMin = 1.0F;
    public float doorStressReliefClosed = 0.01F;
    public float doorStressReliefOpen = 0.5F;

    public GameObject brain = null;
    public bool lightUp = false;

    public MechJebCore core = null;

    protected override void onPartAwake() {
        core = new MechJebCore();
        core.part = this;
        base.onPartAwake();
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

        core.part = this;
        core.onPartStart();

        if (transform.Find("model/base/Door01") == null) {
            return;
        }

        brain = (GameObject)GameObject.Instantiate(Resources.Load("Effects/fx_exhaustFlame_blue"));
        brain.name = "brain_FX";
        brain.transform.parent = transform.Find("model");
        if (transform.Find("model/base/Door03") == null) {
            brain.transform.localPosition = new Vector3(0, 0.05F, -0.2F);
            brain.transform.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
        } else {
            brain.transform.localPosition = new Vector3(0, 0.2F, 0);
            brain.transform.localRotation = Quaternion.identity;
        }

        brain.AddComponent<Light>();
        brain.light.color = XKCDColors.ElectricBlue;
        brain.light.intensity = 0;
        brain.light.range = 1.0F;

        brain.particleEmitter.emit = false;
        brain.particleEmitter.maxEmission = brain.particleEmitter.minEmission = 0;
        brain.particleEmitter.maxEnergy = 0.75F;
        brain.particleEmitter.minEnergy = 0.25F;
        brain.particleEmitter.maxSize = brain.particleEmitter.minSize = 0.1F;
        brain.particleEmitter.localVelocity = Vector3.zero;
        brain.particleEmitter.rndVelocity = new Vector3(2.0F, 0.5F, 2.0F);
        brain.particleEmitter.useWorldSpace = false;
    }

    protected override void onFlightStart() {
        core.onFlightStart();
        base.onFlightStart();
    }

    protected override void onPartFixedUpdate() {
        core.onPartFixedUpdate();
        base.onPartFixedUpdate();

        if (transform.Find("model/base/Door01") == null) {
            return;
        }

        doorStress += core.stress * TimeWarp.fixedDeltaTime;
        doorStress -= doorStress * ((doorStat == DoorStat.OPEN) ? doorStressReliefOpen : doorStressReliefClosed) * TimeWarp.fixedDeltaTime;

        if (doorStress < doorStressMin) {
            brain.particleEmitter.emit = false;
            lightUp = false;
            brain.light.intensity = 0;
        } else {
            if (doorStat == DoorStat.OPEN) {
                brain.particleEmitter.maxEmission = (doorStress - doorStressMin) * 1000.0F;
                brain.particleEmitter.minEmission = brain.particleEmitter.maxEmission / 2.0F;
                brain.particleEmitter.emit = true;
            }

            if (lightUp) {
                brain.light.intensity -= TimeWarp.fixedDeltaTime * Mathf.Sqrt(doorStress);
                if (brain.light.intensity <= 0) {
                    lightUp = false;
                }
            } else {
                brain.light.intensity += TimeWarp.fixedDeltaTime * Mathf.Sqrt(doorStress);
                if (brain.light.intensity >= 1.0F) {
                    lightUp = true;
                }
            }
        }

        if ((doorStat == DoorStat.CLOSED) && (doorStress >= doorStressMax)) {
            doorStat = DoorStat.OPENING;
            doorProgr = 0;
        }

        if ((doorStat == DoorStat.OPEN) && (doorStress <= doorStressMin)) {
            doorStat = DoorStat.CLOSING;
            doorProgr = 0;
        }

        if ((doorStat == DoorStat.OPENING) || (doorStat == DoorStat.CLOSING)) {
            float oldProgr = doorProgr;
            doorProgr = Mathf.Clamp(doorProgr + doorSpeed * TimeWarp.fixedDeltaTime, 0, 1.0F);

            if (transform.Find("model/base/Door03") == null) {
                transform.Find("model/base/Door01").RotateAroundLocal(new Vector3(0.590526F, 0.807018F, 0.0F), 2.5F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
                transform.Find("model/base/Door02").RotateAroundLocal(new Vector3(0.566785F, -0.823866F, 0.0F), 2.5F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
            } else {
                transform.Find("model/base/Door01").RotateAroundLocal(Vector3.left, 2.5F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
                transform.Find("model/base/Door02").RotateAroundLocal(Vector3.back, 2.5F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
                transform.Find("model/base/Door03").RotateAroundLocal(Vector3.right, 2.5F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
                transform.Find("model/base/Door04").RotateAroundLocal(Vector3.forward, 2.5F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
            }

            if (doorProgr >= 1.0F) {
                doorStat = (doorStat == DoorStat.OPENING) ? DoorStat.OPEN : DoorStat.CLOSED;
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
}
