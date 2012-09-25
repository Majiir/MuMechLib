using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleJoke : ComputerModule
    {
        protected AudioSource sorry;
        protected AudioSource glitch;
        protected const int NUM_GLITCH_SOUNDS = 8;
        AudioClip[] glitchClips;
        protected MechJebModuleAscentAutopilot ascent = null;
        protected MechJebModuleLandingAutopilot landing = null;
        protected MechJebModuleTranslatron translatron = null;
        protected SpeechBubble bubble = null;

        protected FlightCamera cam = null;
        protected bool seekCamera = false;
        protected bool randomExplosions = false;
        protected bool goToBeach = false;

        protected bool beachThrottledDown = false;
        protected bool beachDecoupled = false;

        public MechJebModuleJoke(MechJebCore core)
            : base(core)
        {
            hidden = true;
            enabled = true;

            sorry = core.part.gameObject.AddComponent<AudioSource>();
            WWW www = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "Parts/mumech_MechJebPod/snd1.wav");
            if ((sorry != null) && (www != null))
            {
                sorry.clip = www.GetAudioClip(false);
                sorry.volume = 0;
                sorry.Stop();
            }

            glitch = core.part.gameObject.AddComponent<AudioSource>();
            glitchClips = new AudioClip[NUM_GLITCH_SOUNDS];
            for (int i = 0; i < NUM_GLITCH_SOUNDS; i++)
            {
                www = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "Parts/mumech_MechJebPod/glitch" + i + ".wav");
                if (www != null)
                {
                    glitchClips[i] = www.GetAudioClip(false);
                }
            }

            foreach (ComputerModule module in core.modules)
            {
                if (module is MechJebModuleAscentAutopilot)
                {
                    ascent = (MechJebModuleAscentAutopilot)module;
                }
                if (module is MechJebModuleLandingAutopilot)
                {
                    landing = (MechJebModuleLandingAutopilot)module;
                }
                if (module is MechJebModuleTranslatron)
                {
                    translatron = (MechJebModuleTranslatron)module;
                }
            }
        }

        public override string getName()
        {
            return "April's First Module";
        }

        public override void drive(FlightCtrlState s)
        {
            if (cam == null)
            {
                cam = (FlightCamera)GameObject.FindObjectOfType(typeof(FlightCamera));
            }
            if (seekCamera)
            {
                core.attitudeTo((cam.transform.position - vesselState.CoM).normalized, MechJebCore.AttitudeReference.INERTIAL, this);
                core.tmode = MechJebCore.TMode.DIRECT;
                core.trans_spd_act = 100;
            }
            if (goToBeach)
            {
                if (landing.prediction.landingLongitude > MechJebModuleLandingAutopilot.BEACH_LONGITUDE
                    && !beachDecoupled)
                {
                    if (beachThrottledDown)
                    {
                        translatron.recursiveDecouple();
                        beachDecoupled = true;
                    }
                    else
                    {
                        s.mainThrottle = 0;
                        beachThrottledDown = true;
                    }
                }
            }

            base.drive(s);
        }

        public override void onLiftOff()
        {
            if (UnityEngine.Random.Range(0, 2) == 0)
            {
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        seekCamera = true;
                        core.controlClaim(this, true);
                        break;
                    case 1:
                        randomExplosions = true;
                        break;
                    case 2:
                        goToBeach = true;
                        beachDecoupled = false;
                        beachThrottledDown = false;
                        ascent.launchTo(MechJebModuleAscentAutopilot.BEACH_ALTITUDE, MechJebModuleAscentAutopilot.BEACH_INCLINATION);
                        landing.enabled = true;
                        break;
                }
            }

            base.onPartLiftOff();
        }

        public override void onAttitudeChange(MechJebCore.AttitudeReference oldReference, Quaternion oldTarget, MechJebCore.AttitudeReference newReference, Quaternion newTarget)
        {
            if (core.attitudeActive && !goToBeach && !seekCamera && (core.controlModule != this) && (oldReference != newReference))
            {
                if (UnityEngine.Random.Range(0, 4) == 0)
                {
                    sorry.volume = 0.2F;
                    sorry.Play();
                    core.attitudeDeactivate(this);
                }
            }

            base.onAttitudeChange(oldReference, oldTarget, newReference, newTarget);
        }

        public override void onLoadGlobalSettings(SettingsManager settings)
        {
        }

        public override void onSaveGlobalSettings(SettingsManager settings)
        {
        }

        public override void drawGUI(int baseWindowID)
        {
            if (goToBeach && (part.vessel.rootPart.protoModuleCrew.Count > 0))
            {
                if (bubble == null)
                {
                    GUIStyle txt = new GUIStyle(GUI.skin.label);
                    txt.normal.textColor = Color.black;
                    txt.alignment = TextAnchor.MiddleCenter;
                    bubble = new SpeechBubble(txt);
                }
                bubble.drawBubble(new Vector2(part.vessel.rootPart.protoModuleCrew[0].KerbalRef.screenPos.x + (part.vessel.rootPart.protoModuleCrew[0].KerbalRef.avatarSize / 2), part.vessel.rootPart.protoModuleCrew[0].KerbalRef.screenPos.y), "Let's go to the beach!", Color.white);
            }
        }

        public override void onPartFixedUpdate()
        {
            if (randomExplosions)
            {
                if (((TimeWarp.WarpMode == TimeWarp.Modes.LOW) || (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate)) && UnityEngine.Random.Range(0, (int)(15.0 / vesselState.deltaT)) == 0)
                {
                    Part victim = part.vessel.parts[UnityEngine.Random.Range(0, part.vessel.parts.Count)];
                    if (victim != this.part && !(victim is CommandPod))
                    {
                        victim.explode();

                        int glitchNumber = UnityEngine.Random.Range(0, NUM_GLITCH_SOUNDS);
                        if ((glitch != null) && (glitchClips[glitchNumber] != null))
                        {
                            glitch.clip = glitchClips[glitchNumber];
                            glitch.volume = 0.3F;
                            glitch.Play();
                        }
                    }
                }
            }

            if (goToBeach)
            {
                if ((part.vessel.Landed || part.vessel.Splashed)
                    && Math.Abs(part.vessel.mainBody.GetLatitude(vesselState.CoM) - MechJebModuleLandingAutopilot.BEACH_LATITUDE) < 1.0
                    && Math.Abs(part.vessel.mainBody.GetLongitude(vesselState.CoM) - MechJebModuleLandingAutopilot.BEACH_LONGITUDE) < 1.0)
                {

                    //turn off the joke once we at the beach.
                    goToBeach = false;
                    ascent.enabled = false;
                    landing.enabled = false;
                }
                else
                {
                    //if the landing AP is disable, reenable it
                    if (!landing.enabled)
                    {
                        landing.enabled = true;
                    }

                    if (landing.prediction.landingLongitude > MechJebModuleLandingAutopilot.BEACH_LONGITUDE)
                    {
                        //disable ascent computer once we are going to land near the target
                        if (ascent.enabled)
                        {
                            ascent.enabled = false;
                        }


                        if (beachDecoupled)
                        {
                            //if we've decoupled the last stage, activate landing autopilot
                            if (!landing.autoLandAtTarget)
                            {
                                landing.landAt(MechJebModuleLandingAutopilot.BEACH_LATITUDE, MechJebModuleLandingAutopilot.BEACH_LONGITUDE);
                            }
                        }
                        else
                        {
                            //if we haven't decouple the last stage claim control so we can run the drive() code that decouples the lander
                            core.controlClaim(this);
                        }
                    }
                    else if (landing.prediction.landingLongitude < MechJebModuleLandingAutopilot.BEACH_LONGITUDE - 1.0)
                    {
                        //if our predicted landing point is still far from the beach, reenable the ascent AP if it gets turned off:
                        if (!ascent.enabled || ascent.mode == MechJebModuleAscentAutopilot.AscentMode.DISENGAGED)
                        {
                            ascent.enabled = true;
                            ascent.launchTo(MechJebModuleAscentAutopilot.BEACH_ALTITUDE, MechJebModuleAscentAutopilot.BEACH_INCLINATION);
                        }
                    }
                }
            }
        }
    }
}
