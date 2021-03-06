﻿using System;
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
        public List<string> history = new List<string>();
        public int historyPos = 0;

        public LuaTable luaEnv;
        public LuaTable mechjeb;

        static MechJebModuleAutom8()
        {
            A8Console.OnConsoleEvent += A8ConsoleEvent;
        }

        static void A8ConsoleEvent(A8Console.Event e, string txt)
        {
            if (instance != null)
            {
                instance.logPos = new Vector2(0, float.PositiveInfinity);
            }
        }

        public void consoleRun()
        {
            A8Console.WriteLine("> " + line);
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
                A8Console.WriteLine(e.GetType().Name + ": " + e.Message);
                luaEnv.SetNameValue("lastError", ObjectToLua.ToLuaValue(e));
            }
            line = "";
        }

        public void runScript(string script)
        {
            try
            {
                LuaRuntime.GlobalEnvironment = luaEnv;
                LuaRuntime.Run(script, luaEnv);
            }
            catch (Exception e)
            {
                A8Console.WriteLine(e.GetType().Name + ": " + e.Message);
                luaEnv.SetNameValue("lastError", ObjectToLua.ToLuaValue(e));
            }
        }

        public MechJebModuleAutom8(MechJebCore core)
            : base(core)
        {
            instance = this;
            luaEnv = LuaRuntime.CreateGlobalEnviroment();
            mechjeb = new LuaTable();
            core.registerLuaMembers(mechjeb);
            luaEnv.SetNameValue("mechjeb", mechjeb);
            luaEnv.SetNameValue("vessel", ObjectToLua.ToLuaValue(vesselState));

            if (KSP.IO.File.Exists<MuMechJeb>("autorun.lua"))
            {
                try
                {
                    LuaRuntime.GlobalEnvironment = luaEnv;
                    LuaRuntime.RunFile("autorun.lua", luaEnv);
                }
                catch (Exception e)
                {
                    A8Console.WriteLine(e.GetType().Name + ": " + e.Message);
                    luaEnv.SetNameValue("lastError", ObjectToLua.ToLuaValue(e));
                }
            }
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
            GUILayout.TextArea(A8Console.log, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
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
