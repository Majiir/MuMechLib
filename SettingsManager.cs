using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;

namespace MuMech
{
    public class Setting
    {
        public enum Type
        {
            NONE,
            STRING,
            INTEGER,
            DECIMAL,
            VECTOR,
            BOOL
        }

        public Type type = Type.NONE;
        public string raw = "";

        public Setting()
        {
        }

        public Setting(string data)
        {
            load(data);
        }

        public Setting(object obj, Type obj_type)
        {
            switch (obj_type)
            {
                case Type.STRING:
                    value_string = (string)obj;
                    break;
                case Type.INTEGER:
                    value_integer = (int)obj;
                    break;
                case Type.DECIMAL:
                    value_decimal = (double)obj;
                    break;
                case Type.VECTOR:
                    value_vector = (Vector4)obj;
                    break;
                case Type.BOOL:
                    value_bool = (bool)obj;
                    break;
            }
        }

        public string value_string
        {
            get
            {
                return raw.Replace("\\n", "\n");
            }
            set
            {
                raw = value.Replace("\n", "\\n");
                type = Type.STRING;
            }
        }

 
        public int value_integer
        {
            get
            {
                try
                {
                    return Convert.ToInt32(raw);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            set
            {
                raw = value.ToString();
                type = Type.INTEGER;
            }
        }


        public double value_decimal
        {
            get
            {
                try
                {
                    return Convert.ToDouble(raw);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            set
            {
                raw = value.ToString();
                type = Type.DECIMAL;
            }
        }

        public Vector4 value_vector
        {
            get
            {
                string[] pcs = raw.Split(",".ToCharArray());
                if (pcs.Length < 4)
                {
                    return Vector4.zero;
                }
                try
                {
                    return new Vector4(Convert.ToSingle(pcs[0]), Convert.ToSingle(pcs[1]), Convert.ToSingle(pcs[2]), Convert.ToSingle(pcs[3]));
                }
                catch (Exception)
                {
                    return Vector4.zero;
                }
            }
            set
            {
                raw = value.x + "," + value.y + "," + value.z + "," + value.w;
                type = Type.VECTOR;
            }
        }

        public bool value_bool
        {
            get
            {
                try
                {
                    return Convert.ToInt32(raw) == 1;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            set
            {
                raw = (value) ? "1" : "0";
                type = Type.BOOL;
            }
        }

        //Some methods that allow you to provide default values:

        public string valueString(string defaultString)
        {
            if (type != Type.STRING) value_string = defaultString;
            return value_string;
        }

        public int valueInteger(int defaultInt)
        {
            if (type != Type.INTEGER) value_integer = defaultInt;
            return value_integer;
        }

        public double valueDecimal(double defaultDouble)
        {
            if (type != Type.DECIMAL) value_decimal = defaultDouble;
            return value_decimal;
        }

        public bool valueBool(bool defaultBool)
        {
            if (type != Type.BOOL) value_bool = defaultBool;
            return value_bool;
        }

        public Vector4 valueVector(Vector4 defaultVector)
        {
            if (type != Type.VECTOR) value_vector = defaultVector;
            return value_vector;
        }




        public string save()
        {
            return (int)type + "," + raw;
        }

        public void load(string data)
        {
            string[] pcs = data.Split(",".ToCharArray());
            if (pcs.Length >= 2)
            {
                try
                {
                    type = (Type)Convert.ToInt32(pcs[0]);
                }
                catch (Exception)
                {
                    type = Type.NONE;
                    raw = "";
                    return;
                }
                raw = "";
                for (int i = 1; i < pcs.Length; i++)
                {
                    raw += ((i > 1) ? "," : "") + pcs[i];
                }
            }
            else
            {
                type = Type.NONE;
                raw = "";
            }
        }
    }

    public class SettingsManager
    {
        public string saveFile;
        public Dictionary<string, Setting> settings;

        public SettingsManager(string file)
        {
            settings = new Dictionary<string, Setting>();
            saveFile = file;
            load();
        }

        public Setting this[string index]
        {
            get
            {
                if (!settings.ContainsKey(index))
                {
                    settings[index] = new Setting();
                }
                return settings[index];
            }

            set
            {
                settings[index] = value;
            }
        }


        public void load()
        {
            settings.Clear();
            if (File.Exists(saveFile))
            {
                TextReader t = new StreamReader(saveFile);
                string[] ls = t.ReadToEnd().Split("\n".ToCharArray());
                t.Close();

                foreach (string l in ls)
                {
                    string[] pcs = l.Split("=".ToCharArray());
                    if (pcs.Length >= 2)
                    {
                        string tmp = "";
                        for (int i = 1; i < pcs.Length; i++)
                        {
                            tmp += ((i > 1) ? "=" : "") + pcs[i];
                        }
                        settings[pcs[0]] = new Setting(tmp);
                    }
                }
            }
        }

        public void save()
        {
            if (saveFile.Length > 0)
            {
                if (!Directory.Exists(Path.GetDirectoryName(saveFile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(saveFile));
                }
                TextWriter t = new StreamWriter(saveFile);
                foreach (KeyValuePair<string, Setting> s in settings)
                {
                    t.Write(s.Key + "=" + s.Value.save() + "\n");
                }
                t.Close();
            }
        }
    }
}
