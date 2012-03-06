using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechJeb : MuMechPart {
    public enum Mode {
        OFF,
        KILLROT,
        SURFACE,
        PROGRADE,
        RETROGRADE,
        NORMAL_PLUS,
        NORMAL_MINUS,
        RADIAL_PLUS,
        RADIAL_MINUS,
        STANDBY
	}

    public enum DoorStat {
        CLOSED,
        OPENING,
        OPEN,
        CLOSING
    }

    private Mode _mode = Mode.OFF;
    public Mode mode {
        get {
            return _mode;
        }
        set {
            if (_mode != value) {
                _mode = value;
                mode_changed = true;
            }
        }
    }

    private static bool calibrationMode = false;

    private static string[] texts = { "KILL\nROT", "SURF", "PRO\nGRAD", "RETR\nGRAD", "NML\n+", "NML\n-", "RAD\n+", "RAD\n-" };
    protected Rect windowPos;

    private bool flyByWire = false;
    private bool mode_changed = false;
    private Vector3 integral = Vector3.zero;
    private Vector3 prev_err = Vector3.zero;
    private Vector3 act = Vector3.zero;
    private Vector3 k_integral = Vector3.zero;
    private Vector3 k_prev_err = Vector3.zero;
    public float k_Kp = 13.0F;
    public float k_Ki = 0.0F;
    public float k_Kd = 0.0F;
    public float Kp = 20.0F;
    public float Ki = 0.15F;
    public float Kd = 50.0F;
    public float Damping = 1.0F;

    private float resetTime = 0;

    public string srf_hdg = "90";
    public string srf_pth = "90";
    public float srf_act_hdg = 90.0F;
    public float srf_act_pth = 90.0F;
    public bool srf_act = false;

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

    public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
        partDataCollection.Add("window", new KSPParseable(new Vector2(windowPos.x, windowPos.y), KSPParseable.Type.VECTOR2));
        base.onFlightStateSave(partDataCollection);
    }

    public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
        if (parsedData.ContainsKey("window")) {
            windowPos = new Rect(parsedData["window"].value_v2.x, parsedData["window"].value_v2.y, 10, 10);
        }
        base.onFlightStateLoad(parsedData);
    }

    private void drive(FlightCtrlState s) {
        if (isControllable && InputLockManager.IsUnlocked(ControlTypes.PITCH) && (mode != Mode.OFF) && (mode != Mode.STANDBY)) {
            Vector3 MOI = vessel.findLocalMOI(vessel.findWorldCenterOfMass());

            Kp = Mathf.Clamp(MOI.magnitude * 0.45F, 1.0F, 100.0F);
            Ki = Mathf.Clamp(MOI.magnitude * 0.003F, 0.01F, 1.0F);
            Kd = Mathf.Clamp(MOI.magnitude, 1.5F, 100.0F);

            Vector3 err = vessel.transform.InverseTransformDirection(vessel.rigidbody.angularVelocity);
            k_integral += err * TimeWarp.fixedDeltaTime;
            Vector3 deriv = (err - k_prev_err) / TimeWarp.fixedDeltaTime;
            Vector3 k_act = (k_Kp * err) + (k_Ki * k_integral) + (k_Kd * deriv);
            k_prev_err = err;

            if (mode != Mode.KILLROT) {
                k_act *= Damping;
            }

            int userCommanding = ((s.pitch != 0)?1:0) + ((s.roll != 0)?2:0) + ((s.yaw != 0)?4:0);

            if ((userCommanding & 1) == 0) s.pitch = Mathf.Clamp(s.pitch + k_act.x, -1.0F, 1.0F);
            if ((userCommanding & 2) == 0) s.roll = Mathf.Clamp(s.roll + k_act.y, -1.0F, 1.0F);
            if ((userCommanding & 4) == 0) s.yaw = Mathf.Clamp(s.yaw + k_act.z, -1.0F, 1.0F);

            if ((mode == Mode.SURFACE) && (!srf_act)) {
                return;
            }

            if (mode != Mode.KILLROT) {
                s.killRot = false;

                Vector3d up = (vessel.findWorldCenterOfMass() - vessel.mainBody.position).normalized;
                Vector3d prograde = vessel.orbit.GetRelativeVel().normalized;
                Quaternion obt = Quaternion.LookRotation(prograde, up);

                Vector3 tgt_fwd = vessel.transform.forward;
                Vector3 tgt_up = up;

                switch (mode) {
                    case Mode.SURFACE:
                        Quaternion r = Quaternion.LookRotation(up, vessel.mainBody.transform.up) * Quaternion.AngleAxis(srf_act_hdg + 180.0F, Vector3.forward) * Quaternion.AngleAxis(90.0F - srf_act_pth, Vector3.right);
                        
                        tgt_fwd = r * Vector3.forward;
                        if (srf_act_pth == 90.0F) {
                            tgt_up = -vessel.mainBody.transform.up;
                        }
                        break;
                    case Mode.PROGRADE:
                        tgt_fwd = prograde;
                        break;
                    case Mode.RETROGRADE:
                        tgt_fwd = -prograde;
                        break;
                    case Mode.NORMAL_PLUS:
                        tgt_fwd = obt * Vector3.left;
                        break;
                    case Mode.NORMAL_MINUS:
                        tgt_fwd = obt * Vector3.right;
                        break;
                    case Mode.RADIAL_PLUS:
                        tgt_fwd = obt * Vector3.up;
                        tgt_up = prograde;
                        break;
                    case Mode.RADIAL_MINUS:
                        tgt_fwd = obt * Vector3.down;
                        tgt_up = prograde;
                        break;
                }

                err = -vessel.transform.InverseTransformDirection(tgt_fwd);
                integral += err * TimeWarp.fixedDeltaTime;
                deriv = (err - prev_err) / TimeWarp.fixedDeltaTime;
                act = Kp * err + Ki * integral + Kd * deriv;
                prev_err = err;

                if (calibrationMode) {
                    if (Input.GetKeyDown(KeyCode.KeypadMultiply)) {
                        print("Prograde: " + prograde + "\nUp: " + up + "\ntgt_fw: " + (Vector3d)tgt_fwd + "\ntgt_up: " + (Vector3d)tgt_up + "\nerr: " + (Vector3d)err + "\nint: " + (Vector3d)integral + "\nderiv: " + (Vector3d)deriv + "\nact: " + (Vector3d)act + "\nvec_up: " + (Vector3d)Vector3.up + "\nvess_up: " + (Vector3d)vessel.transform.up + "\nvess_fwd: " + (Vector3d)vessel.transform.forward + "\nroll: " + Vector3.Dot(vessel.transform.right, tgt_up));
                        traceTrans("", transform);
                    }
                }

                if (userCommanding != 0) {
                    prev_err = Vector3.zero;
                    integral = Vector3.zero;
                } else {
                    s.pitch = Mathf.Clamp(s.pitch + act.z, -1.0F, 1.0F);
                    s.roll = Mathf.Clamp(s.roll + Vector3.Dot(vessel.transform.right, tgt_up) * 10.0F, -1.0F, 1.0F);
                    s.yaw = Mathf.Clamp(s.yaw - act.x, -1.0F, 1.0F);
                    doorStress += (Mathf.Min(Mathf.Abs(act.z), 1.0F) + Mathf.Min(Mathf.Abs(act.x), 1.0F)) * TimeWarp.fixedDeltaTime;
                }
            }
        }
    }

    private void WindowGUI(int windowID) {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));

        GUIStyle sty = new GUIStyle(GUI.skin.button);
        sty.normal.textColor = sty.focused.textColor = Color.white;
        sty.hover.textColor = sty.active.textColor = Color.yellow;
        sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
        sty.padding = new RectOffset(8, 8, 8, 8);

        GUILayout.BeginVertical();

        if (mode == Mode.OFF) {
            if (GUILayout.Button("ON", sty, GUILayout.ExpandWidth(true))) {
                mode = Mode.STANDBY;
            }
        } else {
            if (GUILayout.Button("OFF", sty, GUILayout.ExpandWidth(true))) {
                mode = Mode.OFF;
                windowPos.width = windowPos.height = 10;
            }

            mode = (Mode)GUILayout.SelectionGrid((int)mode - 1, texts, 2, sty) + 1;

            if (mode == Mode.SURFACE) {
                GUIStyle msty = new GUIStyle(GUI.skin.button);
                msty.padding = new RectOffset();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("HDG", GUILayout.Width(30));
                srf_hdg = GUILayout.TextField(srf_hdg, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("+", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F))) {
                    float tmp = Convert.ToInt16(srf_hdg);
                    tmp += 1;
                    if (tmp >= 360.0F) {
                        tmp -= 360.0F;
                    }
                    srf_hdg = Mathf.RoundToInt(tmp).ToString();
                }
                if (GUILayout.Button("-", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F))) {
                    float tmp = Convert.ToInt16(srf_hdg);
                    tmp -= 1;
                    if (tmp < 0) {
                        tmp += 360.0F;
                    }
                    srf_hdg = Mathf.RoundToInt(tmp).ToString();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("PTH", GUILayout.Width(30));
                srf_pth = GUILayout.TextField(srf_pth, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("+", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F))) {
                    float tmp = Convert.ToInt16(srf_pth);
                    tmp += 1;
                    if (tmp >= 360.0F) {
                        tmp -= 360.0F;
                    }
                    srf_pth = Mathf.RoundToInt(tmp).ToString();
                }
                if (GUILayout.Button("-", msty, GUILayout.Width(18.0F), GUILayout.Height(18.0F))) {
                    float tmp = Convert.ToInt16(srf_pth);
                    tmp -= 1;
                    if (tmp < 0) {
                        tmp += 360.0F;
                    }
                    srf_pth = Mathf.RoundToInt(tmp).ToString();
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("EXECUTE", sty, GUILayout.ExpandWidth(true))) {
                    srf_act_hdg = Convert.ToInt16(srf_hdg);
                    srf_act_pth = Convert.ToInt16(srf_pth);
                    srf_act = true;
                    print("MechJeb activating SURF mode. HDG = " + srf_act_hdg + " - PTH = " + srf_act_pth);
                }
            }
        }

        GUILayout.EndVertical();
    }

    private void drawGUI() {
        if (isControllable) {
            GUI.skin = HighLogic.Skin;
            windowPos = GUILayout.Window(1, windowPos, WindowGUI, "MechJeb", GUILayout.MinWidth(100));
        }
    }

    protected override void onPartStart() {
        stackIcon.SetIcon(DefaultIcons.ADV_SAS);
        if ((windowPos.x == 0) && (windowPos.y == 0)) {
            windowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
        }

        brain = (GameObject)GameObject.Instantiate(Resources.Load("Effects/fx_exhaustFlame_blue"));
        brain.name = "brain_FX";
        brain.transform.parent = transform.Find("model");
        brain.transform.localPosition = new Vector3(0, 0.2F, 0);
        brain.transform.localRotation = Quaternion.identity;

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
        RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));
        FlightInputHandler.OnFlyByWire += new FlightInputHandler.FlightInputCallback(drive);
        flyByWire = true;
    }

    protected override void onPartFixedUpdate() {
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

            transform.Find("model/base/Door01").RotateAroundLocal(Vector3.left, 2.0F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
            transform.Find("model/base/Door02").RotateAroundLocal(Vector3.back, 2.0F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
            transform.Find("model/base/Door03").RotateAroundLocal(Vector3.right, 2.0F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));
            transform.Find("model/base/Door04").RotateAroundLocal(Vector3.forward, 2.0F * (doorProgr - oldProgr) * ((doorStat == DoorStat.OPENING) ? 1.0F : -1.0F));

            if (doorProgr >= 1.0F) {
                doorStat = (doorStat == DoorStat.OPENING) ? DoorStat.OPEN : DoorStat.CLOSED;
            }
        }

        base.onPartFixedUpdate();
    }

    protected override void onPartUpdate() {
        resetTime += Time.deltaTime;
        if ((resetTime >= 5) || mode_changed || InputLockManager.IsLocked(ControlTypes.PITCH)) {
            integral = Vector3.zero;
            prev_err = Vector3.zero;
            act = Vector3.zero;
            k_integral = Vector3.zero;
            k_prev_err = Vector3.zero;
            resetTime = 0;
        }
        if (mode_changed) {
            srf_act = false;
            windowPos.width = windowPos.height = 10;

            mode_changed = false;
        }

        if (calibrationMode) {
            bool printk = false;

            float shift = 0.25F;
            if (Input.GetKey(KeyCode.RightAlt)) {
                shift = 0.05F;
            }

            if (Input.GetKeyDown(KeyCode.Keypad7)) {
                if (Input.GetKey(KeyCode.RightControl)) {
                    Kp += shift;
                } else {
                    k_Kp += shift;
                }
                printk = true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad4)) {
                if (Input.GetKey(KeyCode.RightControl)) {
                    Kp -= shift;
                } else {
                    k_Kp -= shift;
                }
                printk = true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad8)) {
                if (Input.GetKey(KeyCode.RightControl)) {
                    Ki += shift;
                } else {
                    k_Ki += shift;
                }
                printk = true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad5)) {
                if (Input.GetKey(KeyCode.RightControl)) {
                    Ki -= shift;
                } else {
                    k_Ki -= shift;
                }
                printk = true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad9)) {
                if (Input.GetKey(KeyCode.RightControl)) {
                    Kd += shift;
                } else {
                    k_Kd += shift;
                }
                printk = true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad6)) {
                if (Input.GetKey(KeyCode.RightControl)) {
                    Kd -= shift;
                } else {
                    k_Kd -= shift;
                }
                printk = true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad1)) {
                Damping += shift;
                printk = true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad0)) {
                Damping -= shift;
                printk = true;
            }
            if (printk) {
                print("k_Kp = " + k_Kp + " - k_Ki = " + k_Ki + " - k_Kd = " + k_Kd);
                print("Kp = " + Kp + " - Ki = " + Ki + " - Kd = " + Kd + " - Damp = " + Damping);
            }
        }
    }

    protected override void onDisconnect() {
        if (flyByWire) {
            FlightInputHandler.OnFlyByWire -= new FlightInputHandler.FlightInputCallback(drive);
            flyByWire = false;
        }
    }

    protected override void onPartDestroy() {
        if (flyByWire) {
            FlightInputHandler.OnFlyByWire -= new FlightInputHandler.FlightInputCallback(drive);
            flyByWire = false;
        }
        RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
    }
}
