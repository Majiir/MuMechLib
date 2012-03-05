using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechToggle : MuMechPart {
    public bool toggle_drag = false;
    public bool toggle_break = false;
    public bool toggle_model = false;
    public bool toggle_collision = false;
    public float on_angularDrag = 2.0F;
    public float on_maximum_drag = 0.2F;
    public float on_minimum_drag = 0.2F;
    public float on_crashTolerance = 9.0F;
    public float on_breakingForce = 22.0F;
    public float on_breakingTorque = 22.0F;
    public float off_angularDrag = 2.0F;
    public float off_maximum_drag = 0.2F;
    public float off_minimum_drag = 0.2F;
    public float off_crashTolerance = 9.0F;
    public float off_breakingForce = 22.0F;
    public float off_breakingTorque = 22.0F;
    public string on_model = "on";
    public string off_model = "off";
    public string rotate_model = "on";
    public Vector3 onRotateAxis = Vector3.forward;
    public float onRotateSpeed = 0;
    public string onKey = "p";
    public Vector3 keyRotateAxis = Vector3.forward;
    public float keyRotateSpeed = 0;
    public string rotateKey = "9";
    public string revRotateKey = "0";

    private bool on = false;

    public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
        partDataCollection.Add("on", new KSPParseable(on, KSPParseable.Type.BOOL));
        base.onFlightStateSave(partDataCollection);
    }

    public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
        KSPParseable tmp;
        parsedData.TryGetValue("on", out tmp);
        on = tmp.value_bool;
        updateState();
        base.onFlightStateLoad(parsedData);
    }

    public void updateState() {
        if (on) {
            if (toggle_model) {
                transform.FindChild("model").FindChild(on_model).renderer.enabled = true;
                transform.FindChild("model").FindChild(off_model).renderer.enabled = false;
            }
            if (toggle_drag) {
                angularDrag = on_angularDrag;
                minimum_drag = on_minimum_drag;
                maximum_drag = on_maximum_drag;
            }
            if (toggle_break) {
                crashTolerance = on_crashTolerance;
                breakingForce = on_breakingForce;
                breakingTorque = on_breakingTorque;
            }
        } else {
            if (toggle_model) {
                transform.FindChild("model").FindChild(on_model).renderer.enabled = false;
                transform.FindChild("model").FindChild(off_model).renderer.enabled = true;
            }
            if (toggle_drag) {
                angularDrag = off_angularDrag;
                minimum_drag = off_minimum_drag;
                maximum_drag = off_maximum_drag;
            }
            if (toggle_break) {
                crashTolerance = off_crashTolerance;
                breakingForce = off_breakingForce;
                breakingTorque = off_breakingTorque;
            }
        }
        if (toggle_collision) {
            collider.enabled = on;
            collisionEnhancer.enabled = on;
            terrainCollider.enabled = on;
        }
    }

    protected override void onPartStart() {
        base.onPartStart();
        stackIcon.SetIcon(DefaultIcons.STRUT);
        on = true;
        updateState();
    }

    protected override void onPartAttach(Part parent) {
        on = false;
        updateState();
    }

    protected override void onPartDetach() {
        on = true;
        updateState();
    }

    protected override void onEditorUpdate() {
        base.onEditorUpdate();
    }

    protected override void onFlightStart() {
        on = false;
        updateState();
    }

    protected override void onPartUpdate() {
        if (connected && Input.GetKeyDown(onKey)) {
            on = !on;
            updateState();
        }
    }

    protected override bool onPartActivate() {
        on = true;
        updateState();
        return true;
    }

    protected override void onPartFixedUpdate() {
        if (on && (onRotateSpeed != 0)) {
            transform.FindChild("model").FindChild(rotate_model).RotateAroundLocal(onRotateAxis, TimeWarp.fixedDeltaTime * onRotateSpeed * (isSymmMaster() ? -1 : 1));
        }
        if ((keyRotateSpeed != 0) && Input.GetKey(rotateKey)) {
            transform.FindChild("model").FindChild(rotate_model).RotateAroundLocal(keyRotateAxis, TimeWarp.fixedDeltaTime * keyRotateSpeed * (isSymmMaster() ? -1 : 1));
        }
        if ((keyRotateSpeed != 0) && Input.GetKey(revRotateKey)) {
            transform.FindChild("model").FindChild(rotate_model).RotateAroundLocal(keyRotateAxis, TimeWarp.fixedDeltaTime * keyRotateSpeed * (isSymmMaster() ? 1 : -1));
        }
    }

    protected override void onPartDeactivate() {
        on = false;
        updateState();
    }
}
