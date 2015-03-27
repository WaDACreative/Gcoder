using System;
using System.IO;
using System.Collections.Generic;

namespace WaDAGcode
{
    class MainClass
    {
        public static void Main (string[] args)
        {
            if (args.Length < 2) {
                PrintHelp ();
                return;
            }

            StreamReader sr = File.OpenText (args[args.Length - 1]);

            switch (args [0]) {
            case "eval":
                EstimateWorkingTime (sr);
                break;
            case "para":
                PrintSlicingParameters (sr);
                break;
            case "offset":
                CompensateXY (args, sr);
                break;
            }
            sr.Close ();
        }

        #region
        private static bool GetXYZCompensation(string[] args, ref double x, ref double y, ref double z)
        {
            bool Success = false;
            string s;

            foreach (string a in args) {
                s = a.ToUpper ();
                if (s [0] == 'X') {
                    x = Convert.ToSingle (s.Substring (2));
                    Success = true;
                } else if (s [0] == 'Y') {
                    y = Convert.ToSingle (s.Substring (2));
                    Success = true;
                } else if (s [0] == 'Z') {
                    z = Convert.ToSingle (s.Substring (2));
                    Success = true;
                }
            }

            return Success;
        }

        private static void CompensateXY(string[] args, StreamReader sr)
        {
            double OffsetX = 0, OffsetY = 0, OffsetZ = 0;

            if (!GetXYZCompensation(args, ref OffsetX, ref OffsetY, ref OffsetZ)) {
                Console.WriteLine("!!! Please assign X / Y offset !!!");
                return ;
            }

            StreamWriter sw = File.CreateText (string.Format ("{0:yyyyMMdd-hhmmss}.gcode", DateTime.Now));

            bool skip = false;
            string gcode;
            double X, Y, Z, E, F;

            while (!sr.EndOfStream) {
                gcode = sr.ReadLine().Trim();
                skip = false;

                if (gcode.Length < 5 || gcode [0] == ';')
                    skip = true;

                // Pass non-G1 commands.
                if (!skip && gcode.Substring (0, 3) != "G1 ")
                    skip = true;

                if (!skip) {
                    X = 0;
                    Y = 0;
                    Z = 0;
                    E = 0;
                    F = 0;

                    GetGcodeX (gcode, ref X);
                    GetGcodeY (gcode, ref Y);
                    GetGcodeZ (gcode, ref Z);
                    GetGcodeE (gcode, ref E);
                    GetGcodeF (gcode, ref F);

                    gcode = "G1";
                    if (X != 0) {
                        X += OffsetX;
                        gcode += string.Format (" X{0:F3}", X);
                    }
                    if (Y != 0) {
                        Y += OffsetY;
                        gcode += string.Format (" Y{0:F3}", Y);
                    }
                    if (Z != 0) {
                        Z += OffsetZ;
                        gcode += string.Format (" Z{0:F3}", Z);
                    }
                    if (E != 0) {
                        gcode += string.Format (" E{0:F5}", E);
                    }
                    if (F != 0) {
                        gcode += string.Format (" F{0:F3}", F);
                    }

                }
                sw.WriteLine (gcode);
            }
            sw.Close ();
        }
        #endregion

        #region Gcode Helpper
        private static bool GetGcodeX(string gcode, ref double X)
        {
            int pos, next;
            bool GetValue = false;

            pos = gcode.IndexOf ('X', 3) + 1;
            next = gcode.IndexOf (' ', pos + 1);
            next = next > 0 ? next : gcode.Length;
            if (pos >= 4) {
                X = Convert.ToSingle (gcode.Substring (pos, next - pos));
                GetValue = true;
            }

            return GetValue;
        }

        private static bool GetGcodeY(string gcode, ref double Y)
        {
            int pos, next;
            bool GetValue = false;

            pos = gcode.IndexOf ('Y', 3) + 1;
            next = gcode.IndexOf (' ', pos + 1);
            next = next > 0 ? next : gcode.Length;
            if (pos >= 4) {
                Y = Convert.ToSingle (gcode.Substring (pos, next - pos));
                GetValue = true;
            }

            return GetValue;
        }

        private static bool GetGcodeZ(string gcode, ref double Z)
        {
            int pos, next;
            bool GetValue = false;

            pos = gcode.IndexOf ('Z', 3) + 1;
            next = gcode.IndexOf (' ', pos + 1);
            next = next > 0 ? next : gcode.Length;
            if (pos >= 4) {
                Z = Convert.ToSingle (gcode.Substring (pos, next - pos));
                GetValue = true;
            }

            return GetValue;
        }

        private static bool GetGcodeE(string gcode, ref double E)
        {
            int pos, next;
            bool GetValue = false;

            pos = gcode.IndexOf ('E', 3) + 1;
            next = gcode.IndexOf (' ', pos + 1);
            next = next > 0 ? next : gcode.Length;
            if (pos >= 4) {
                E = Convert.ToSingle (gcode.Substring (pos, next - pos));
                GetValue = true;
            }

            return GetValue;
        }

        private static bool GetGcodeF(string gcode, ref double F)
        {
            int pos, next;
            bool GetValue = false;

            pos = gcode.IndexOf ('F', 3) + 1;
            next = gcode.IndexOf (' ', pos + 1);
            next = next > 0 ? next : gcode.Length;
            if (pos >= 4) {
                F = Convert.ToSingle (gcode.Substring (pos, next - pos));
                GetValue = true;
            }

            return GetValue;
        }
        #endregion

        private static void PrintHelp()
        {
            Console.WriteLine ("*** WaDAGcoder Utility ***");
            Console.WriteLine (" eval   => Evaluate working time.");
            Console.WriteLine (" para   => Print out slicing paramters.");
            Console.WriteLine (" offset => Regenerate Gcode to compensate X/Y coordinate.");
        }

        private static void EstimateWorkingTime(StreamReader sr)
        {
            string gcode;
            double oldX = 0, oldY = 0, oldE = 0;
            double newX = 0, newY = 0, newE = 0;
            double dX, dY, L;
            double totalLength = 0;
            double totalX = 0;
            double totalY = 0;
            double totalE = 0;
            double milliSecond = 0;
            double speed = 1800;

            while (!sr.EndOfStream) {
                gcode = sr.ReadLine ().Trim ();
                // Pass comment.
                if (gcode.Length < 5 || gcode [0] == ';')
                    continue;

                // G92 command.
                if (gcode.Substring (0, 4) != "G92 ") {
                    GetGcodeE (gcode, ref oldE);
                    Console.WriteLine ("G92 occurred ! {0}", oldE);
                }
                // G1 command.
                else if (gcode.Substring (0, 3) == "G1 ") {

                    if (GetGcodeX (gcode, ref newX))
                        dX = Math.Abs (newX - oldX);
                    else
                        dX = 0;

                    if (GetGcodeY (gcode, ref newY))
                        dY = Math.Abs (newY - oldY);
                    else
                        dY = 0;

                    if (GetGcodeE (gcode, ref newE)) {
                        totalE += (newE - oldE);
                        oldE = newE;
                    }

                    L = Math.Sqrt (dX * dX + dY * dY);

                    oldX = newX;
                    oldY = newY;

                    totalX += dX;
                    totalY += dY;
                    totalLength += L;

                    milliSecond += (L * 1000 * 60 / speed);
                }
            }

            Console.WriteLine ("Total X Moves (mm) : {0}", totalX);
            Console.WriteLine ("Total Y Moves (mm) : {0}", totalY);
            Console.WriteLine ("Total E Length (mm) : {0}", totalE);
            Console.WriteLine ("Total Distance (mm) : {0}", totalLength);
            Console.WriteLine ("MilliSecond : {0}", milliSecond);

            TimeSpan t = TimeSpan.FromMilliseconds (milliSecond);
            Console.WriteLine ("Total Time : {0:dd} day(s) {1:hh\\:mm\\:ss}", t, t);
        }

        private static Dictionary<string, string> parametersMap;

        private static void PrintSlicingParameters(StreamReader sr)
        {
            string line, key, value;

            parametersMap = LoadParametersConfig();

            while (!sr.EndOfStream) {
                line = sr.ReadLine ().Trim ();

                // Skip all Gcode command.
                if (line.Length == 0 || line [0] != ';')
                    continue;
                
                // Extract parameter.
                string[] param = line.Substring (2).Split ('=');

                // Skip empty line or abnormal-formatted line.
                if (param == null || param.Length < 2)
                    continue;
                
                key = param [0].Trim ();
                value = param [1].Trim ();

                if (parametersMap.ContainsKey (key)) {
                    Console.WriteLine (parametersMap [key] + " => " + value);
                }
            }
        }

        private static Dictionary<string, string> LoadParametersConfig()
        {
            StreamReader sr = File.OpenText ("parameters.cfg");
            Dictionary<string, string> dict = new Dictionary<string, string> ();

            string line;

            while (!sr.EndOfStream) {
                line = sr.ReadLine ();

                string[] param = line.Split ('=');

                // Skip empty line or abnormal-formatted line.
                if (param == null || param.Length < 2)
                    continue;
                
                dict.Add (param [0], param [1]);    
            }

            sr.Close ();

            return dict;
        }
    }
}
