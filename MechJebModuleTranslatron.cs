using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MuMech {
    class MechJebModuleTranslatron : ComputerModule {
        protected static string[] trans_texts = { "OFF", "KEEP\nOBT", "KEEP\nSURF", "KEEP\nVERT" };

        public string trans_spd = "0";

        public MechJebModuleTranslatron(MechJebCore core) : base(core) { }

        public override string getName() {
            return "Translatron";
        }

        public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection) {
            partDataCollection.Add("trans_spd", new KSPParseable(trans_spd, KSPParseable.Type.STRING));
            base.onFlightStateSave(partDataCollection);
        }

        public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData) {
            if (parsedData.ContainsKey("trans_spd")) trans_spd = parsedData["trans_spd"].value;
            base.onFlightStateLoad(parsedData);
        }

        public override GUILayoutOption[] windowOptions() {
            return new GUILayoutOption[] { GUILayout.Width(130) };
        }

        protected override void WindowGUI(int windowID) {
            GUIStyle sty = new GUIStyle(GUI.skin.button);
            sty.normal.textColor = sty.focused.textColor = Color.white;
            sty.hover.textColor = sty.active.textColor = Color.yellow;
            sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            sty.padding = new RectOffset(8, 8, 8, 8);

            GUILayout.BeginVertical();

            core.tmode = (MechJebCore.TMode)GUILayout.SelectionGrid((int)core.tmode, trans_texts, 2, sty);

            core.trans_kill_h = GUILayout.Toggle(core.trans_kill_h, "Kill H/S", GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Speed");
            trans_spd = GUILayout.TextField(trans_spd, GUILayout.ExpandWidth(true));
            trans_spd = Regex.Replace(trans_spd, @"[^\d.+-]", "");
            GUILayout.EndHorizontal();

            if (GUILayout.Button("EXECUTE", sty, GUILayout.ExpandWidth(true))) {
                core.trans_spd_act = Convert.ToInt16(trans_spd);
                GUIUtility.keyboardControl = 0;
            }

            if (core.tmode != MechJebCore.TMode.OFF) {
                GUILayout.Label("Active speed: " + MuMech.MuUtils.ToSI(core.trans_spd_act) + "m/s", GUILayout.ExpandWidth(true));
            }

            GUILayout.FlexibleSpace();

            GUIStyle tsty = new GUIStyle(GUI.skin.label);
            tsty.alignment = TextAnchor.UpperCenter;
            GUILayout.Label("Automation", tsty, GUILayout.ExpandWidth(true));

            if (core.trans_land) {
                sty.normal.textColor = sty.focused.textColor = sty.hover.textColor = sty.active.textColor = sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            }

            if (GUILayout.Button("LAND", sty, GUILayout.ExpandWidth(true))) {
                core.trans_land = !core.trans_land;
                if (core.trans_land) {
                    core.trans_land_gears = false;
                    core.tmode = MechJebCore.TMode.KEEP_VERTICAL;
                }
            }

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }
    }
}
