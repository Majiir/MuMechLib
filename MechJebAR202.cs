using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using MuMech;

public class MuMechJebAR202 : MuMechPart
{
    public MechJebCore core = null;

    //light stuff
    public enum LightColor { NEITHER, GREEN, RED };
    LightColor litLight = 0;
    Shader originalLensShader;
    Color originalLensColor = new Color(0, 0, 0, 0);
    Light greenLight;
    Light redLight;
    Transform greenLightTransform;
    Transform redLightTransform;

    void handleLights()
    {
        bool somethingEnabled = false;
        foreach (ComputerModule module in core.modules)
        {
            if (module.enabled) somethingEnabled = true;
        }

        if (MapView.MapIsEnabled) litLight = LightColor.NEITHER;
        else litLight = (somethingEnabled ? LightColor.GREEN : LightColor.RED);

        switch (litLight)
        {
            case LightColor.GREEN:
                if (!greenLight.enabled) turnOnLight(LightColor.GREEN);
                if (redLight.enabled) turnOffLight(LightColor.RED);
                break;

            case LightColor.RED:
                if (greenLight.enabled) turnOffLight(LightColor.GREEN);
                if (!redLight.enabled) turnOnLight(LightColor.RED);
                break;

            case LightColor.NEITHER:
                if (greenLight.enabled) turnOffLight(LightColor.GREEN);
                if (redLight.enabled) turnOffLight(LightColor.RED);
                break;
        }

    }

    void initializeLights()
    {
        greenLightTransform = null;
        redLightTransform = null;

        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t.name.Equals("light_green")) greenLightTransform = t;
            if (t.name.Equals("light_red")) redLightTransform = t;
        }

        if (greenLightTransform != null)
        {
            originalLensShader = greenLightTransform.renderer.material.shader;
            greenLight = greenLightTransform.gameObject.AddComponent<Light>();
            greenLight.transform.parent = greenLightTransform;
            greenLight.type = LightType.Point;
            greenLight.renderMode = LightRenderMode.ForcePixel;
            greenLight.shadows = LightShadows.None;
            greenLight.enabled = false;
            greenLight.color = Color.green;
            greenLight.range = 1.5F;
        }
        if (redLightTransform != null)
        {
            originalLensShader = redLightTransform.renderer.material.shader;
            redLight = redLightTransform.gameObject.AddComponent<Light>();
            redLight.transform.parent = redLightTransform;
            redLight.type = LightType.Point;
            redLight.renderMode = LightRenderMode.ForcePixel;
            redLight.shadows = LightShadows.None;
            redLight.enabled = false;
            redLight.color = Color.red;
            redLight.range = 1.5F;
        }
    }

    void turnOnLight(LightColor which)
    {
        switch (which)
        {
            case LightColor.GREEN:
                if (greenLightTransform != null)
                {
                    greenLightTransform.renderer.material.shader = Shader.Find("Self-Illumin/Specular");
                    greenLightTransform.renderer.material.color = Color.green;
                    greenLight.enabled = true;
                }
                break;

            case LightColor.RED:
                if (redLightTransform != null)
                {
                    redLightTransform.renderer.material.shader = Shader.Find("Self-Illumin/Specular");
                    redLightTransform.renderer.material.color = Color.red;
                    redLight.enabled = true;
                }
                break;
        }
    }

    void turnOffLight(LightColor which)
    {
        switch (which)
        {
            case LightColor.GREEN:
                if (greenLightTransform != null)
                {
                    greenLightTransform.renderer.material.shader = originalLensShader;
                    greenLightTransform.renderer.material.color = originalLensColor;
                    greenLight.enabled = false;
                }
                break;

            case LightColor.RED:
                if (redLightTransform != null)
                {
                    redLightTransform.renderer.material.shader = originalLensShader;
                    redLightTransform.renderer.material.color = originalLensColor;
                    redLight.enabled = false;
                }
                break;
        }
    }


    protected override void onPartAwake()
    {
        core = new MechJebCore();
        core.part = this;
        base.onPartAwake();
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
        initializeLights();

        core.part = this;
        core.onPartStart();
    }

    protected override void onFlightStart()
    {
        core.onFlightStart();
        base.onFlightStart();
    }

    protected override void onPartFixedUpdate()
    {
        handleLights();

        core.onPartFixedUpdate();
        base.onPartFixedUpdate();
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
}


