using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpLua.LuaTypes;
using UnityEngine;

namespace SharpLua
{
    public class LuaUtils
    {
        public static Vector3d valueToVector3d(LuaValue val)
        {
            Vector3d ret = Vector3d.zero;

            LuaTable t = val as LuaTable;
            LuaString s = val as LuaString;

            if (t != null)
            {
                try
                {
                    ret.x = (t.GetValue(1) as LuaNumber).Number;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Invalid vector x value");
                }
                try
                {
                    ret.y = (t.GetValue(2) as LuaNumber).Number;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Invalid vector y value");
                }
                try
                {
                    ret.z = (t.GetValue(3) as LuaNumber).Number;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Invalid vector z value");
                }
            }
            else if (s != null)
            {
                switch (s.Text)
                {
                    case "forward":
                    case "f":
                        ret = Vector3d.forward;
                        break;
                    case "back":
                    case "b":
                        ret = Vector3d.back;
                        break;
                    case "left":
                    case "l":
                        ret = Vector3d.left;
                        break;
                    case "right":
                    case "r":
                        ret = Vector3d.right;
                        break;
                    case "up":
                    case "u":
                        ret = Vector3d.up;
                        break;
                    case "down":
                    case "d":
                        ret = Vector3d.down;
                        break;
                    default:
                        throw new ArgumentException("Invalid vector string");
                }
            }
            else
            {
                throw new ArgumentException("Invalid vector");
            }

            return ret;
        }

        public static LuaTable Vector3dToTable(Vector3d v)
        {
            LuaTable t = new LuaTable();

            t.AddValue(new LuaNumber(v.x));
            t.AddValue(new LuaNumber(v.y));
            t.AddValue(new LuaNumber(v.z));

            return t;
        }

        public static Quaternion valueToQuaternion(LuaValue val)
        {
            Quaternion ret = Quaternion.identity;

            LuaTable t = val as LuaTable;

            if (t != null)
            {
                try
                {
                    ret.x = (float)(t.GetValue(1) as LuaNumber).Number;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Invalid quatertion x value");
                }
                try
                {
                    ret.y = (float)(t.GetValue(2) as LuaNumber).Number;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Invalid quatertion y value");
                }
                try
                {
                    ret.z = (float)(t.GetValue(3) as LuaNumber).Number;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Invalid quatertion z value");
                }
                try
                {
                    ret.w = (float)(t.GetValue(4) as LuaNumber).Number;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Invalid quatertion w value");
                }
            }
            else
            {
                throw new ArgumentException("Invalid quaternion");
            }

            return ret;
        }

        public static LuaTable QuaternionToTable(Quaternion q)
        {
            LuaTable t = new LuaTable();

            t.AddValue(new LuaNumber(q.x));
            t.AddValue(new LuaNumber(q.y));
            t.AddValue(new LuaNumber(q.z));
            t.AddValue(new LuaNumber(q.w));

            return t;
        }
    }
}
