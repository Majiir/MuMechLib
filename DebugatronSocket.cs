using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpLua;
using SharpLua.LuaTypes;
using UnityEngine;
using JSONSharp;

namespace MuMech
{

    public class SocketStateObject {
        public Socket socket = null;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();  
    }

    class DebugatronSocketGhost : MonoBehaviour
    {
        public int port;
        public MuMechDebugatronSocket part;

        TcpListener server = null;
        List<SocketStateObject> connections = new List<SocketStateObject>();
        StringBuilder sendCache = new StringBuilder();
        LuaTable luaEnv;

        void Start()
        {
            port = part.port;

            luaEnv = LuaRuntime.CreateGlobalEnviroment();
            LuaTable dbg = new LuaTable();
            dbg.Register("send", dbgSend);
            dbg.Register("tojson", dbgToJSON);
            luaEnv.SetNameValue("debugatron", dbg);

            A8Console.OnConsoleEvent += A8ConsoleEvent;

            server = new TcpListener(IPAddress.Any, port);
            server.Start();
        }

        void A8ConsoleEvent(A8Console.Event e, string txt)
        {
            if (e != A8Console.Event.CLEAR)
            {
                lock (this)
                {
                    sendCache.Append(txt + ((e == A8Console.Event.WRITELINE) ? "\n" : ""));
                }
            }
        }

        LuaValue dbgSend(LuaValue[] arg)
        {
            if (arg.Length > 0)
            {
                lock (this)
                {
                    sendCache.Append(arg[0].ToString());
                }
            }
            return LuaNil.Nil;
        }

        LuaValue dbgToJSON(LuaValue[] arg)
        {
            if (arg.Length > 0)
            {
                return new LuaString(JSONReflector.ObjectToJSON(arg[0].Value));
            }
            return LuaNil.Nil;
        }

        void Update()
        {
            while (server.Pending())
            {
                SocketStateObject newConn = new SocketStateObject();
                newConn.socket = server.AcceptSocket();
                connections.Add(newConn);
            }

            List<SocketStateObject> cleanup = new List<SocketStateObject>();

            byte[] sendData;
            lock (this)
            {
                sendData = Encoding.UTF8.GetBytes(sendCache.ToString());
                sendCache = new StringBuilder();
            }

            LuaRuntime.GlobalEnvironment = luaEnv;
            foreach (SocketStateObject client in connections)
            {
                if (!client.socket.Connected)
                {
                    cleanup.Add(client);
                    continue;
                }

                if (client.socket.Available > 0)
	            {
                    int br = client.socket.Receive(client.buffer, SocketStateObject.BufferSize, SocketFlags.Partial);
                    client.sb.Append(Encoding.UTF8.GetString(client.buffer, 0, br));
                    string tmp = client.sb.ToString();
                    if (tmp.Contains("\n"))
                    {
                        string[] pcs = tmp.Replace("\r", "").Split(char.Parse("\n"));
                        foreach (string line in pcs.Take(pcs.Length - 1))
                        {
                            try
                            {
                                LuaRuntime.Run(line, luaEnv);
                            }
                            catch (Exception e)
                            {
                                A8Console.WriteLine(e.GetType().Name + ": " + e.Message);
                                luaEnv.SetNameValue("lastError", ObjectToLua.ToLuaValue(e));
                            }
                        }
                        client.sb = new StringBuilder(pcs.Last());
                    }
	            }

                if (sendData.Length > 0)
                {
                    client.socket.Send(sendData);
                }
            }

            connections.RemoveAll(i => cleanup.Contains(i));
        }
    }

    public class MuMechDebugatronSocket : PartModule
    {
        public int port = 33284;

        public MuMechDebugatronSocket()
        {
            GameObject ghost = new GameObject("DebugatronSocketGhost_" + name);
            DebugatronSocketGhost g = ghost.AddComponent<DebugatronSocketGhost>();
            g.part = this;
            GameObject.DontDestroyOnLoad(ghost);
        }
    }
}
