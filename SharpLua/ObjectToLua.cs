/*
 * Created by SharpDevelop.
 * User: elijah
 * Date: 1/5/2012
 * Time: 8:46 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using KSP.IO;
using System.Reflection;
using SharpLua.LuaTypes;
using System.Linq;

namespace SharpLua
{
    /// <summary>
    /// Converts the object to the Lua equivalent (Credit to Arjen Douma for the idea of having it static)
    /// </summary>
    public class ObjectToLua
    {

        private static void SetPropertyValue(object obj, object value, PropertyInfo propertyInfo)
        {
            if (propertyInfo.PropertyType.FullName == "System.Int32")
            {
                propertyInfo.SetValue(obj, (int)(double)value, null);
            }
            else if (propertyInfo.PropertyType.FullName == "System.Single")
            {
                propertyInfo.SetValue(obj, Convert.ToSingle(value), null);
            }
            else if (propertyInfo.PropertyType.IsEnum)
            {
                object enumValue = Enum.Parse(propertyInfo.PropertyType, (string)value);
                propertyInfo.SetValue(obj, enumValue, null);
            }
            /*
            else if (propertyInfo.PropertyType.FullName == "System.Drawing.Image")
            {
                LuaTable enviroment = LuaRuntime.GlobalEnvironment.GetValue("_G") as LuaTable;
                LuaString workDir = enviroment.GetValue("_WORKDIR") as LuaString;
                var image = System.Drawing.Image.FromFile(Path.Combine(workDir.Text, (string)value));
                propertyInfo.SetValue(obj, image, null);
            }
            */
            else
            {
                propertyInfo.SetValue(obj, value, null);
            }
        }

        private static void SetFieldValue(object obj, object value, FieldInfo fieldInfo)
        {
            if (fieldInfo.FieldType.FullName == "System.Int32")
            {
                fieldInfo.SetValue(obj, (int)(double)value);
            }
            else if (fieldInfo.FieldType.FullName == "System.Single")
            {
                fieldInfo.SetValue(obj, Convert.ToSingle(value));
            }
            else if (fieldInfo.FieldType.IsEnum)
            {
                object enumValue = Enum.Parse(fieldInfo.FieldType, (string)value);
                fieldInfo.SetValue(obj, enumValue);
            }
            else if (fieldInfo.FieldType.IsArray && (value is LuaTable))
            {
                Type elem = fieldInfo.FieldType.GetElementType();
                if (new string[] { "System.Int32", "System.Single", "System.Double" }.Any(i => i == elem.FullName))
                {
                    double[] tmp = ((LuaTable)value).ToDoubleArray();
                    if (elem.FullName == "System.Int32") {
                        fieldInfo.SetValue(obj, Array.ConvertAll(tmp, i => (int)i));
                    }
                    else if (elem.FullName == "System.Single")
                    {
                        fieldInfo.SetValue(obj, Array.ConvertAll(tmp, i => (float)i));
                    }
                    else if (elem.FullName == "System.Double")
                    {
                        fieldInfo.SetValue(obj, tmp);
                    }
                }
                else if (elem.FullName == "System.String")
                {
                    fieldInfo.SetValue(obj, ((LuaTable)value).ToStringArray());
                }
            }
            else
            {
                fieldInfo.SetValue(obj, value);
            }
        }

        private static void SetMemberValue(object control, Type type, string member, object value)
        {
            UnityEngine.MonoBehaviour.print("Trying to set " + type.FullName + " - " + member + " to (" + value.GetType().FullName + ") " + value.ToString());

            PropertyInfo propertyInfo = type.GetProperty(member);
            if (propertyInfo != null)
            {
                UnityEngine.MonoBehaviour.print("Type: Property - " + propertyInfo.PropertyType.FullName);
                SetPropertyValue(control, value, propertyInfo);
                return;
            }
            FieldInfo fieldInfo = type.GetField(member);
            if (fieldInfo != null)
            {
                UnityEngine.MonoBehaviour.print("Type: Field - " + fieldInfo.FieldType.FullName);
                SetFieldValue(control, value, fieldInfo);
                return;
            }
            EventInfo eventInfo = type.GetEvent(member);
            if (eventInfo != null)
            {
                switch (eventInfo.EventHandlerType.FullName)
                {
                    case "System.EventHandler":
                        eventInfo.AddEventHandler(control, new EventHandler((sender, e) =>
                                                                            {
                                                                                (value as LuaFunc).Invoke(new LuaValue[] { new LuaUserdata(sender), new LuaUserdata(e) });
                                                                            }));
                        break;
                    /*
                    case "System.Windows.Forms.TreeViewEventHandler":
                        eventInfo.AddEventHandler(control, new TreeViewEventHandler((sender, e) =>
                                                                                    {
                                                                                        (value as LuaFunc).Invoke(new LuaValue[] { new LuaUserdata(sender), new LuaUserdata(e) });
                                                                                    }));
                        break;
                    */
                    default:
                        throw new NotImplementedException(eventInfo.EventHandlerType.FullName + " type not implemented.");
                }
            }
        }
        
        private static LuaValue GetMemberValue(object control, Type type, string member)
        {
            PropertyInfo propertyInfo = type.GetProperty(member);
            if (propertyInfo != null)
            {
                object value = propertyInfo.GetValue(control, null);
                return ToLuaValue(value);
            }
            FieldInfo fi = type.GetField(member);
            if (fi != null)
            {
                return ToLuaValue(fi.GetValue(control));
            }
            MethodInfo[] miarr = type.GetMethods();
            MethodInfo mi = null;
            foreach (MethodInfo m in miarr)
                if (m.Name == member)
                    mi = m;
            if (mi != null)
            {
                return new LuaFunction((LuaValue[] args) =>
                                       {
                                           List<object> args2 = new List<object>();
                                           foreach (LuaValue v in args)
                                               args2.Add(v.Value);

                                           mi = null;
                                           foreach (MethodInfo m in miarr)
                                           {
                                               if (m.Name == member)
                                               {
                                                   UnityEngine.MonoBehaviour.print("Found " + m + " count = " + m.GetGenericArguments().Length);
                                                    try
                                                    {
                                                        UnityEngine.MonoBehaviour.print("Trying " + m);
                                                        object result = m.Invoke(control, args2.ToArray());
                                                        return ToLuaValue(result);
                                                    }
                                                    catch (ArgumentException e)
                                                    {
                                                        UnityEngine.MonoBehaviour.print(e);
                                                    }
                                                    catch (TargetParameterCountException e)
                                                    {
                                                        UnityEngine.MonoBehaviour.print(e);
                                                    }
                                               }
                                           }
                                           throw new Exception(string.Format("Cannot get {0}  with {2} arguments from {1}", member, control, args.Length));
                                       });
            }
            
            throw new Exception(string.Format("Cannot get {0} from {1}", member, control));
        }
        
        private static LuaValue GetIndexerValue(object control, Type type, double index)
        {
            MemberInfo[] members = type.GetMember("Item");

            if (members.Length == 0)
            {
                throw new InvalidOperationException(string.Format("Indexer is not defined in {0}", type.FullName));
            }

            foreach (MemberInfo memberInfo in members)
            {
                PropertyInfo propertyInfo = memberInfo as PropertyInfo;
                if (propertyInfo != null)
                {
                    try
                    {
                        object result = propertyInfo.GetValue(control, new object[] { (int)index - 1 });
                        return ToLuaValue(result);
                    }
                    catch (TargetParameterCountException)
                    {
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (MethodAccessException)
                    {
                    }
                    catch (TargetInvocationException)
                    {
                    }
                }
            }

            return LuaNil.Nil;
        }

        public static LuaValue ToLuaValue(object value)
        {
            if ((value as LuaValue) != null)
                return value as LuaValue;
            if (value is int || value is double || value is float)
            {
                return new LuaNumber(Convert.ToDouble(value));
            }
            else if (value is MuMech.MovingAverage)
            {
                return new LuaNumber(((MuMech.MovingAverage)value).value);
            }
            else if (value is string)
            {
                return new LuaString((string)value);
            }
            else if (value is bool)
            {
                return LuaBoolean.From((bool)value);
            }
            else if (value == null)
            {
                return LuaNil.Nil;
            }
            else if (value.GetType().IsEnum)
            {
                return new LuaString(value.ToString());
            }
            else if (value.GetType().IsArray)
            {
                LuaTable table = new LuaTable();
                foreach (var item in (value as Array))
                {
                    table.AddValue(ToLuaValue(item));
                }
                return table;
            }
            else if (value is LuaValue)
            {
                return (LuaValue)value;
            }
            else
            {
                LuaUserdata data = new LuaUserdata(value);
                data.MetaTable = GetControlMetaTable();
                return data;
            }
        }

        static LuaTable controlMetaTable;
        public static LuaTable GetControlMetaTable()
        {
            if (controlMetaTable == null)
            {
                controlMetaTable = new LuaTable();

                controlMetaTable.SetNameValue("__index", new LuaFunction((values) =>
                                                                         {
                                                                             LuaUserdata control = values[0] as LuaUserdata;
                                                                             Type type = control.Value.GetType();

                                                                             LuaString member = values[1] as LuaString;
                                                                             if (member != null)
                                                                             {
                                                                                 return GetMemberValue(control.Value, type, member.Text);
                                                                             }

                                                                             LuaNumber index = values[1] as LuaNumber;
                                                                             if (index != null)
                                                                             {
                                                                                 return GetIndexerValue(control.Value, type, index.Number);
                                                                             }

                                                                             return LuaNil.Nil;
                                                                         }));

                controlMetaTable.SetNameValue("__newindex", new LuaFunction((values) =>
                                                                            {
                                                                                LuaUserdata control = values[0] as LuaUserdata;
                                                                                LuaString member = values[1] as LuaString;
                                                                                LuaValue value = values[2];

                                                                                Type type = control.Value.GetType();
                                                                                SetMemberValue(control.Value, type, member.Text, value.Value);
                                                                                return null;
                                                                            }));
            }

            return controlMetaTable;
        }

        public static LuaTable ToLuaTable(object o)
        {
            LuaTable ret = new LuaTable();
            
            // check if Dictionary...
            System.Collections.IDictionary dict = o as System.Collections.IDictionary;
            if (dict != null)
            {
                foreach (object obj in dict.Keys)
                {
                    ret.SetKeyValue(ToLuaValue(obj), ToLuaValue(dict[obj]));
                }
                return ret;
            }
            
            System.Collections.IEnumerable ie = (o as System.Collections.IEnumerable);
            if (ie != null)
            {
                foreach (object obj in ie)
                {
                    ret.AddValue(ToLuaValue(obj));
                }
                return ret;
            }
            
            // check if <type>...
            // not an array type
            ret.AddValue(ToLuaValue(o));
            return ret;
        }
    }
}
