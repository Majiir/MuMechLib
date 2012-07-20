using System;
using System.Text;
using MuMech;

namespace SharpLua
{
    public class A8Console
    {
        public static void Write(string txt)
        {
            MechJebModuleAutom8.instance.log += txt;
        }

        public static void WriteLine(string txt)
        {
            MechJebModuleAutom8.instance.log += txt + "\n";
        }

        public static void Clear()
        {
            MechJebModuleAutom8.instance.log = "";
        }
    }
}
