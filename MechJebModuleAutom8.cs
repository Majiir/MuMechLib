using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SharpLua;
using SharpLua.LuaTypes;
using System.Threading;

namespace MuMech
{
    public class MechJebModuleAutom8 : ComputerModule
    {
        public static MechJebModuleAutom8 instance = null;

        public string line = "";
        private string _log = "";
        public string log
        {
            get
            {
                return _log;
            }
            set
            {
                _log = value;
                logPos = new Vector2(0, float.PositiveInfinity);
            }
        }
        public List<string> history = new List<string>();
        public int historyPos = 0;

        public LuaTable luaEnv;
        public LuaTable mechjeb;

        public void consoleRun()
        {
            log += "> " + line + "\n";
            if (history.LastOrDefault() != line)
            {
                history.Add(line);
            }
            historyPos = 0;
            try
            {
                LuaRuntime.GlobalEnvironment = luaEnv;
                LuaRuntime.Run(line, luaEnv);
            }
            catch (Exception e)
            {
                log += e.GetType().Name + ": " + e.Message + "\n";
            }
            line = "";
        }

        public MechJebModuleAutom8(MechJebCore core)
            : base(core)
        {
            instance = this;
            luaEnv = LuaRuntime.CreateGlobalEnviroment();
            mechjeb = new LuaTable();
            core.registerLuaMembers(mechjeb);
            luaEnv.SetNameValue("mechjeb", mechjeb);
            luaEnv.SetNameValue("mj", ObjectToLua.ToLuaValue(core));
            luaEnv.SetNameValue("vessel", ObjectToLua.ToLuaValue(vesselState));
        }

        public override void onPartFixedUpdate()
        {
            luaEnv.SetNameValue("vessel", ObjectToLua.ToLuaValue(vesselState));
            base.onPartFixedUpdate();
        }

        public override string getName()
        {
            return "Autom8";
        }

        public override GUILayoutOption[] windowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(600), GUILayout.Height(200) };
        }

        Vector2 logPos = Vector2.zero;

        protected override void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            logPos = GUILayout.BeginScrollView(logPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.TextArea(log, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.KeyDown) {
                switch (Event.current.keyCode)
                {
                    case KeyCode.Return:
                        consoleRun();
                        Event.current.Use();
                        break;
                    case KeyCode.UpArrow:
                        if (historyPos < history.Count)
                        {
                            historyPos++;
                            line = history[history.Count - historyPos];
                        }
                        Event.current.Use();
                        break;
                    case KeyCode.DownArrow:
                        if (historyPos > 0)
                        {
                            historyPos--;
                            if (historyPos <= 0)
                            {
                                line = "";
                            }
                            else
                            {
                                line = history[history.Count - historyPos];
                            }
                        }
                        Event.current.Use();
                        break;
                }
            }
            line = GUILayout.TextField(line, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Execute", GUILayout.Width(75)))
            {
                consoleRun();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }
    }
}
