using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

class MuMechMuonDetector : Part {
    static double MIN_PING_DIST = 15000;
    static double MIN_PING_TIME = 0.2;
    static double MAX_PING_TIME = 5;

    protected AudioSource ping;
    protected double lastPing = 0;
    protected double lastDist = double.PositiveInfinity;
    protected Light pingLight = null;
    Shader originalLensShader;
    protected Transform led = null;
    protected Transform disk = null;
    protected static Rect winPos;

    public bool detectorActive = false;

    protected override void onFlightStart() {
        ping = gameObject.AddComponent<AudioSource>();
        WWW www = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "Parts/mumech_MuonDetector/ping.wav");
        if ((ping != null) && (www != null)) {
            ping.clip = www.GetAudioClip(false);
            ping.volume = 0;
            ping.Stop();
        }

        disk = transform.Find("model/disk");
        if (disk != null) {
            MIN_PING_DIST = 150000;
            MIN_PING_TIME = 0.2;
            MAX_PING_TIME = 15;

            led = transform.Find("model/led");
            originalLensShader = led.renderer.material.shader;
            pingLight = led.gameObject.AddComponent<Light>();
            pingLight.type = LightType.Point;
            pingLight.renderMode = LightRenderMode.ForcePixel;
            pingLight.shadows = LightShadows.None;
            pingLight.range = 1;
            pingLight.enabled = false;
        }

        RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));
    }

    public override void OnDrawStats() {
        GUILayout.TextArea("Range: " + ((transform.Find("model/disk") == null)?"15":"150") + "km", GUILayout.ExpandHeight(true));
        base.OnDrawStats();
    }

    protected override void onPartDestroy() {
        RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI));
        base.onPartDestroy();
    }

    protected override void onPartFixedUpdate() {
        if ((pingLight != null) && (Planetarium.GetUniversalTime() - lastPing > 1)) {
            led.renderer.material.color = Color.black;
            led.renderer.material.shader = originalLensShader;
            pingLight.enabled = false;
        }
        if (detectorActive) {
            Animation[] muns = vessel.mainBody.GetComponentsInChildren<Animation>();
            double nearest = double.PositiveInfinity;
            foreach (Animation mun in muns) {
                double dist = (mun.transform.position - vessel.transform.position).magnitude;
                if (dist < nearest) {
                    nearest = dist;
                }
            }
            if (nearest < MIN_PING_DIST) {
                double pingTime = MIN_PING_TIME + (MAX_PING_TIME - MIN_PING_TIME) * nearest / MIN_PING_DIST;
                if (Planetarium.GetUniversalTime() - lastPing >= pingTime) {
                    lastPing = Planetarium.GetUniversalTime();
                    if (ping.isPlaying) {
                        ping.Stop();
                    }
                    ping.volume = 0.2F;
                    ping.Play();

                    if (pingLight != null) {
                        pingLight.color = (lastDist > nearest) ? Color.green : Color.red;
                        led.renderer.material.color = pingLight.color;
                        led.renderer.material.shader = Shader.Find("Self-Illumin/Specular");
                        pingLight.enabled = true;
                    }
                    lastDist = nearest;
                }
            }
            if (disk != null) {
                disk.RotateAroundLocal(Vector3d.up, 1 * Mathf.Deg2Rad * TimeWarp.fixedDeltaTime);
            }
        }
        base.onPartFixedUpdate();
    }

    private void WindowGUI(int windowID) {
        GUILayout.BeginVertical();

        if (GUILayout.Button(detectorActive ? "Deactivate" : "Activate")) {
            detectorActive = !detectorActive;
        }

        GUILayout.EndVertical();

        GUI.DragWindow();
    }

    private void drawGUI() {
        if ((vessel == FlightGlobals.ActiveVessel) && isControllable && InputLockManager.IsUnlocked(ControlTypes.THROTTLE)) {
            if (winPos.x == 0 && winPos.y == 0) {
                winPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            }

            GUI.skin = HighLogic.Skin;

            winPos = GUILayout.Window(891, winPos, WindowGUI, "Muon Detector", GUILayout.MinWidth(120));
        }
    }

}
