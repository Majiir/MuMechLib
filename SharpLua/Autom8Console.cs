using System;
using System.Text;
using MuMech;

namespace SharpLua
{
    public class A8Console
    {
        public enum Event
	    {
            WRITE,
            WRITELINE,
            CLEAR
	    }

        public delegate void A8ConsoleEventDelegate(Event e, string txt);

        public static string log = "";
        public static event A8ConsoleEventDelegate OnConsoleEvent;

        public static void Write(string txt)
        {
            lock (typeof(A8Console))
            {
                log += txt;
            }
            if (OnConsoleEvent != null)
            {
                OnConsoleEvent(Event.WRITE, txt);
            }
        }

        public static void WriteLine(string txt)
        {
            lock (typeof(A8Console))
            {
                log += txt + "\n";
            }
            if (OnConsoleEvent != null)
            {
                OnConsoleEvent(Event.WRITELINE, txt);
            }
        }

        public static void Clear()
        {
            lock (typeof(A8Console))
            {
                log = "";
            }
            if (OnConsoleEvent != null)
            {
                OnConsoleEvent(Event.CLEAR, "");
            }
        }
    }
}
