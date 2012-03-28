using System;
using System.Collections.Generic;
using System.Text;

namespace MuMech {
    class MuUtils {
        public static string ToSI(double d, int digits = 3) {
            double exponent = Math.Log10(Math.Abs(d));
            if (Math.Abs(d) >= 1) {
                switch ((int)Math.Floor(exponent)) {
                    case 0:
                    case 1:
                    case 2:
                        return d.ToString("F"+digits);
                    case 3:
                    case 4:
                    case 5:
                        return (d / 1e3).ToString("F" + digits) + "k";
                    case 6:
                    case 7:
                    case 8:
                        return (d / 1e6).ToString("F" + digits) + "M";
                    case 9:
                    case 10:
                    case 11:
                        return (d / 1e9).ToString("F" + digits) + "G";
                    case 12:
                    case 13:
                    case 14:
                        return (d / 1e12).ToString("F" + digits) + "T";
                    case 15:
                    case 16:
                    case 17:
                        return (d / 1e15).ToString("F" + digits) + "P";
                    case 18:
                    case 19:
                    case 20:
                        return (d / 1e18).ToString("F" + digits) + "E";
                    case 21:
                    case 22:
                    case 23:
                        return (d / 1e21).ToString("F" + digits) + "Z";
                    default:
                        return (d / 1e24).ToString("F" + digits) + "Y";
                }
            } else if (Math.Abs(d) > 0) {
                switch ((int)Math.Floor(exponent)) {
                    case -1:
                    case -2:
                    case -3:
                        return (d * 1e3).ToString("F" + digits) + "m";
                    case -4:
                    case -5:
                    case -6:
                        return (d * 1e6).ToString("F" + digits) + "μ";
                    case -7:
                    case -8:
                    case -9:
                        return (d * 1e9).ToString("F" + digits) + "n";
                    case -10:
                    case -11:
                    case -12:
                        return (d * 1e12).ToString("F" + digits) + "p";
                    case -13:
                    case -14:
                    case -15:
                        return (d * 1e15).ToString("F" + digits) + "f";
                    case -16:
                    case -17:
                    case -18:
                        return (d * 1e18).ToString("F" + digits) + "a";
                    case -19:
                    case -20:
                    case -21:
                        return (d * 1e21).ToString("F" + digits) + "z";
                    default:
                        return (d * 1e24).ToString("F" + digits) + "y";
                }
            } else {
                return "0";
            }
        }
    }

    public class MovingAverage {
        private double[] store;
        private int storeSize;
        private int nextIndex = 0;

        public double value {
            get {
                double tmp = 0;
                foreach (double i in store) {
                    tmp += i;
                }
                return tmp / storeSize;
            }
            set {
                store[nextIndex] = value;
                nextIndex = (nextIndex + 1) % storeSize;
            }
        }

        public MovingAverage(int size = 10, double startingValue = 0) {
            storeSize = size;
            store = new double[size];
            force(startingValue);
        }

        public void force(double newValue) {
            for (int i = 0; i < storeSize; i++) {
                store[i] = newValue;
            }
        }

        public static implicit operator double(MovingAverage v) {
            return v.value;
        }

        public override string ToString() {
            return value.ToString();
        }

        public string ToString(string format) {
            return value.ToString(format);
        }
    }
}
