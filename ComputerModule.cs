using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech {
    class ComputerModule {
        public Part part = null;
        public MechJebCore core = null;
        public VesselState vesselState = null;

        protected Rect _windowPos;
        public Rect windowPos {
            get {
                return _windowPos;
            }
            set {
                if (_windowPos.x != value.x || _windowPos.y != value.y) {
                    core.settingsChanged = true;
                }
                _windowPos = value;
            }
        }

        protected bool _enabled = false;
        public bool enabled {
            get {
                return _enabled;
            }
            set {
                if (value != _enabled) {
                    core.settingsChanged = true;
                    _enabled = value;
                    if (_enabled) {
                        onModuleEnabled();
                    } else {
                        onModuleDisabled();
                    }
                }
            }
        }

        public bool hidden = false;

        public ComputerModule(MechJebCore core) {
            this.core = core;
            part = core.part;
            vesselState = core.vesselState;
        }

        public virtual void onModuleEnabled() {
        }

        public virtual void onModuleDisabled() {
        }

        public virtual void onControlLost() {
        }

        public virtual void onAttitudeChange(MechJebCore.AttitudeReference oldReference, Quaternion oldTarget, MechJebCore.AttitudeReference newReference, Quaternion newTarget) {
        }

        public virtual void onLiftOff() {
        }

        public virtual void onActiveFixedUpdate() {
        }

        public virtual void onActiveUpdate() {
        }

        public virtual void onBackup() {
        }

        public virtual void drive(FlightCtrlState s) {
        }

        public virtual void onDecouple(float breakForce) {
        }

        public virtual void onDisconnect() {
        }

        public virtual void onFlightStart() {
            if ((windowPos.x == 0) && (windowPos.y == 0)) windowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
        }

        public virtual void onFlightStartAtLaunchPad() {
        }

        public virtual void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
        }

        public virtual void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
        }

        public virtual void onLoadGlobalSettings(SettingsManager settings) {
            windowPos = new Rect(settings["windowPos_" + GetType().Name].value_vector.x, settings["windowPos_" + GetType().Name].value_vector.y, 10, 10);
            enabled = settings["windowStat_" + GetType().Name].value_bool;
        }

        public virtual void onSaveGlobalSettings(SettingsManager settings) {
            settings["windowPos_" + GetType().Name].value_vector = new Vector4(windowPos.x, windowPos.y);
            settings["windowStat_" + GetType().Name].value_bool = enabled;
        }

        public virtual void onGamePause() {
        }

        public virtual void onGameResume() {
        }

        public virtual void onPack() {
        }

        public virtual bool onPartActivate() {
            return false;
        }

        public virtual void onPartAwake() {
        }

        public virtual void onPartDeactivate() {
        }

        public virtual void onPartDelete() {
        }

        public virtual void onPartDestroy() {
        }

        public virtual void onPartExplode() {
        }

        public virtual void onPartFixedUpdate() {
        }

        public virtual void onPartLiftOff() {
        }

        public virtual void onPartLoad() {
        }

        public virtual void onPartSplashdown() {
        }

        public virtual void onPartStart() {
        }

        public virtual void onPartTouchdown() {
        }

        public virtual void onPartUpdate() {
        }

        public virtual void onUnpack() {
        }

        protected virtual void WindowGUI(int windowID) {
            GUI.DragWindow();
        }

        public virtual void drawGUI(int baseWindowID) {
            windowPos = GUILayout.Window(baseWindowID, windowPos, WindowGUI, getName(), windowOptions());
        }

        public virtual GUILayoutOption[] windowOptions() {
            return new GUILayoutOption[0];
        }

        public virtual String getName() {
            return "Computer Module";
        }

        protected void print(String s) {
            MonoBehaviour.print(s);
        }
    }
}
