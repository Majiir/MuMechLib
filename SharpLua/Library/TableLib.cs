﻿using System;
using System.Collections.Generic;
using KSP.IO;
using System.Text;

using SharpLua.LuaTypes;

namespace SharpLua.Library
{
    public static class TableLib
    {
        public static void RegisterModule(LuaTable enviroment)
        {
            LuaTable module = new LuaTable();
            RegisterFunctions(module);
            enviroment.SetNameValue("table", module);
        }

        public static void RegisterFunctions(LuaTable module)
        {
            module.Register("concat", Concat);
            module.Register("insert", Insert);
            module.Register("remove", Remove);
            module.Register("removeitem", RemoveItem);
            module.Register("maxn", Maxn);
            module.Register("sort", Sort);
            module.Register("copy", Copy);
            // 3 different ways to call one function...
            module.Register("dump", PrintContents);
            module.Register("print", PrintContents);
            module.Register("printcontents", PrintContents);
            module.Register("find", Find);
        }

        public static LuaValue Concat(LuaValue[] values)
        {
            LuaTable table = values[0] as LuaTable;
            LuaString separator = values.Length > 1 ? values[1] as LuaString : LuaString.Empty;
            LuaNumber startNumber = values.Length > 2 ? values[2] as LuaNumber : null;
            LuaNumber endNumber = values.Length > 3 ? values[3] as LuaNumber : null;

            int start = startNumber == null ? 1 : (int)startNumber.Number;
            int end = endNumber == null ? table.Count : (int)endNumber.Number;

            if (start > end)
            {
                return LuaString.Empty;
            }
            else
            {
                StringBuilder text = new StringBuilder();

                for (int index = start; index < end; index++)
                {
                    text.Append(table.GetValue(index).ToString());
                    text.Append(separator.Text);
                }
                text.Append(table.GetValue(end).ToString());

                return new LuaString(text.ToString());
            }
        }

        public static LuaValue Insert(LuaValue[] values)
        {
            LuaTable table = values[0] as LuaTable;
            if (values.Length == 2)
            {
                LuaValue item = values[1];
                table.AddValue(item);
            }
            else if (values.Length == 3)
            {
                LuaNumber number = values[1] as LuaNumber;
                LuaValue item = values[2];
                int index = (int)number.Number;
                table.InsertValue(index, item);
            }
            return null;
        }

        public static LuaValue Remove(LuaValue[] values)
        {
            LuaTable table = values[0] as LuaTable;
            int index = table.Count;
            if (values.Length == 2)
            {
                LuaNumber number = values[1] as LuaNumber;
                index = (int)number.Number;
            }

            LuaValue item = table.GetValue(index);
            table.RemoveAt(index);
            return item;
        }

        public static LuaValue RemoveItem(LuaValue[] values)
        {
            LuaTable table = values[0] as LuaTable;
            LuaValue item = values[1];

            bool removed = table.Remove(item);
            return LuaBoolean.From(removed);
        }

        public static LuaValue Maxn(LuaValue[] values)
        {
            LuaTable table = values[0] as LuaTable;
            double maxIndex = 0;
            foreach (var key in table.Keys)
            {
                LuaNumber number = key as LuaNumber;
                if (number != null && number.Number > 0)
                {
                    if (number.Number > maxIndex)
                    {
                        maxIndex = number.Number;
                    }
                }
            }
            return new LuaNumber(maxIndex);
        }
        
        public static LuaValue Sort(LuaValue[] values)
        {
            LuaTable table = values[0] as LuaTable;
            if (values.Length == 2)
            {
                LuaFunction comp = values[1] as LuaFunction;
                table.Sort(comp);
            }
            else
            {
                table.Sort();
            }
            return null;
        }
        
        public static LuaValue Copy(LuaValue[] args)
        {
            LuaTable _new = (LuaTable) args[0];
            LuaTable old = (LuaTable) args[1];
            if (_new == null)
                throw new ArgumentNullException("Table to copy to cannot be nil!");
            if (old == null)
                throw new ArgumentNullException("Table to copy from cannot be nil!");
            if (old.Count == 0)
                return _new; // nothing to copy
            Dictionary<LuaValue, LuaValue> oldFields = (Dictionary<LuaValue, LuaValue>)old.KeyValuePairs;
            List<LuaValue> keys = new List<LuaValue>();
            List<LuaValue> values = new List<LuaValue>();
            
            foreach (LuaValue key in oldFields.Keys)
                keys.Add(key);
            foreach (LuaValue val in oldFields.Values)
                values.Add(val);
            
            // add to new table
            for (int i = 0; i < keys.Count; i++)
            {
                try {
                    _new.SetKeyValue(keys[i], values[i]);
                } catch (Exception ex) {
                    throw new Exception("Error copying at index " + i + ": " + ex.Message);
                }
            }
            return _new;
        }
        
        static LuaTable scanned = new LuaTable();
        public static LuaValue PrintContents(LuaValue[] args)
        {
            scanned = new LuaTable();
            PrintTable(args[0] as LuaTable, "");
            return LuaNil.Nil;
        }
        
        private static void PrintTable(LuaTable tbl, string indent, LuaValue _key = null)
        {
            /* sample output:
                    table: 002CCBA8
                    {
                        field = value
                        X = 10
                        y = function: 002CCBA8
                    }
             */
            string i = indent;
            A8Console.Write(i);
            if (_key != null)
                A8Console.Write(_key.ToString() + " = ");
            A8Console.WriteLine(tbl.ToString() + "\n" + i + "{");
            
            foreach (LuaValue key in tbl.Keys)
            {
                LuaValue v = tbl.GetValue(key);
                if (v.GetTypeCode() == "table")
                {
                    // check that its not a reference of itself
                    if (!scanned.ContainsKey(key))
                    {
                        scanned.SetKeyValue(key, v);
                        PrintTable(v as LuaTable, i + " ", key);
                    }
                    else
                    {
                        A8Console.WriteLine(i + " " + key.ToString() + " = " + v.ToString());
                    }
                }
                else
                {
                    scanned.SetKeyValue(key, v);
                    A8Console.WriteLine(i + " " + key.ToString() + " = " + v.ToString());
                }
            }/*
            foreach (LuaValue key in tbl.MetaTable.Keys)
            {
                A8Console.WriteLine(i + "(MetaTable): " + key.ToString() + " = " + tbl.MetaTable.GetValue(key).ToString());
            }*/
            A8Console.WriteLine(i + "}");
        }
        
        public static LuaValue Find(LuaValue[] args)
        {
            LuaValue v = args[0] as LuaValue;
            LuaTable t = args[1] as LuaTable;
            foreach (LuaValue key in t.Keys)
                if (key.Value == v.Value)
                    return key;
            
            return LuaNil.Nil;
        }
    }
}
