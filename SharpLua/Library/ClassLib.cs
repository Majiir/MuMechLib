﻿/*
 * Created by SharpDevelop.
 * User: elijah
 * Date: 12/23/2011
 * Time: 12:33 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using SharpLua.LuaTypes;

namespace SharpLua.Library
{
    /// <summary>
    /// lClass in C# with a new module name
    /// </summary>
    public class ClassLib
    {
        public ClassLib()
        {
        }
        
        public static void RegisterModule(LuaTable env)
        {
            LuaTable module = new LuaTable();
            RegisterFunctions(module);
            env.SetNameValue("class", module);
        }
        
        public static void RegisterFunctions(LuaTable mod)
        {
            mod.Register("CreateInstance", CreateInstance);
            mod.Register("FindMethod", FindMethod);
            mod.Register("IsClass", IsClass);
            mod.Register("IsObject", IsObject);
            mod.Register("IsObjectOf", IsObjectOf);
            mod.Register("SetMembers", SetMembers);
            mod.Register("CreateFinalClass", CreateFinalClass);
            mod.Register("CreateStaticClass", CreateStaticClass);
            mod.Register("CreateClass", CreateClass);
            mod.Register("IterateChildClasses", IterateChildClasses);
            mod.Register("IterateParentClasses", IterateParentClasses);
            LuaTable mt = new LuaTable();
            mt.Register("__call", new LuaFunc((LuaValue[] args) =>
                                              {
                                                  return CreateClass(args);
                                              }));
            mod.MetaTable = mt;
        }
        
        public static LuaValue CreateInstance(LuaValue[] args)
        {
            // copy args[0] to a new LuaClass and return it
            LuaClass c = args[0] as LuaClass;
            LuaClass n = new LuaClass(c.Name, c.Final, c.Static);
            n.CallFunction = c.CallFunction;
            n.ChildClasses = c.ChildClasses;
            n.IndexFunction = c.IndexFunction;
            n.MetaTable = c.MetaTable;
            n.NewIndexFunction = c.NewIndexFunction;
            n.ParentClasses = c.ParentClasses;
            n.Constructor = c.Constructor;
            n.Destructor = c.Destructor;
            TableLib.Copy(new LuaValue[] { n.Self, c.Self });
            n.ToStringFunction = c.ToStringFunction;
            
            return n;
        }
        
        public static LuaValue FindMethod(LuaValue[] args)
        {
            //(method, ...)
            string method = (args[0] as LuaString).Text;
            
            for (int i = 1; i < args.Length; i++)
            {
                LuaValue k = args[i];
                LuaValue m = (k as LuaClass).Self.GetValue(method);
                if (m != null && m.GetTypeCode() == "function")
                    return m;
            }
            return LuaNil.Nil;
        }

        public static LuaValue IsClass(LuaValue[] args)
        {
            if ((args[0] as LuaClass) != null)
                return LuaBoolean.True;
            else
                return LuaBoolean.False;
        }
        
        public static LuaValue IsObject(LuaValue[] args)
        {
            if ((args[0] as LuaClass) != null)
                return LuaBoolean.False; // its a class
            else
                return LuaBoolean.True;
        }
        
        public static LuaValue IsObjectOf(LuaValue[] args)
        {
            LuaClass c = args[0] as LuaClass;
            LuaClass _class = args[1] as LuaClass;
            return LuaBoolean.From(c == _class);
        }
        
        public static LuaValue IsMemberOf(LuaValue[] args)
        {
            LuaClass _class = args[1] as LuaClass;
            LuaValue obj = args[0];
            
            return LuaBoolean.From(_class.Self.GetValue(obj) != LuaNil.Nil);
        }
        
        public static LuaValue SetMembers(LuaValue[] args)
        {
            LuaClass _class = args[0] as LuaClass;
            for (int i = 1; i < args.Length; i++)
                SharpLua.Library.TableLib.Copy(new LuaValue[] {_class.Self, args[i]});
            return _class;
        }
        
        #region ITERATORS
        public static LuaValue IterateChildClasses(LuaValue[] args)
        {
            LuaClass _class = args[0] as LuaClass;
            LuaFunction f = new LuaFunction(NextClassIterator);
            return new LuaMultiValue(new LuaValue[] { f, _class.GetChildClasses(new LuaValue[] {}), LuaNil.Nil });
        }
        
        public static LuaValue IterateParentClasses(LuaValue[] args)
        {
            LuaClass _class = args[0] as LuaClass;
            LuaFunction f = new LuaFunction(NextClassIterator);
            return new LuaMultiValue(new LuaValue[] { f, _class.GetParentClasses(new LuaValue[] {}), LuaNil.Nil });
        }
        
        public static LuaValue NextClassIterator(LuaValue[] args)
        {
            LuaMultiValue multi = args[0] as LuaMultiValue;
            LuaValue loopVar = args[1];
            LuaValue result = LuaNil.Nil;

            int idx = (loopVar == LuaNil.Nil ? 0 : Convert.ToInt32(loopVar.MetaTable.GetValue("__class_iterator_index").Value));

            if (idx < multi.Values.Length)
            {
                result = multi.Values[idx];
                result.MetaTable.SetNameValue("__class_iterator_index", new LuaNumber(idx + 1));
            }
            return result;
        }
        #endregion
        
        public static LuaValue CreateFinalClass(LuaValue[] args)
        {
            LuaClass c = CreateClass(args) as LuaClass;
            c.Final = true;
            return c;
        }
        
        public static LuaValue CreateStaticClass(LuaValue[] args)
        {
            LuaClass c = CreateClass(args) as LuaClass;
            c.Static = true;
            return c;
        }
        
        private static int classCount = 0;
        public static LuaValue CreateClass(LuaValue[] args)
        {
            LuaTable from = new LuaTable();
            if (args.Length > 0)
                if (args[0].GetTypeCode() == "table" && ((IsClass(new LuaValue[] {args[0]}) as LuaBoolean).BoolValue == false))
                    from = args[0] as LuaTable;
            LuaClass nClass = new LuaClass("CLASS_" + classCount++, false, false);
            List<LuaClass> Parents = new List<LuaClass>();
            for (int i = 0; i < args.Length; i++)
            {
                LuaClass c = args[i] as LuaClass;
                if (c == null)
                    continue;
                if (c.Final)
                    throw new Exception("Cannot inherit from a final class");
                else
                {
                    Parents.Add(c);
                    c.ChildClasses.Add(nClass);
                }
            }
            nClass.ParentClasses = Parents;
            TableLib.Copy(new LuaValue[] {nClass.Self, from});
            return nClass;
        }
    }
}
