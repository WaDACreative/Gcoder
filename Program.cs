using System;
using System.IO;

namespace WaDAGcode
{
    class MainClass
    {
        public static void Main (string[] args)
        {
            if (args.Length < 1) {
                PrintHelp ();
                return;
            }

            StreamReader sr = File.OpenText (args[0]);

            EstimateWorkingTime (sr);
            sr.Close ();
        }

        private static void PrintHelp()
        {
            Console.WriteLine ("*** WaDAGcoder Utility ***");
        }

        private static void EstimateWorkingTime(StreamReader sr)
        {
            string gcode;
            int pos, next;
            double oldX = 0, oldY = 0;
            double newX = 0, newY = 0;
            double dX, dY, L;
            double totalLength = 0;
            double totalX = 0;
            double totalY = 0;
            double milliSecond = 0;
            double speed = 1800;

            while (!sr.EndOfStream)
            {
                gcode = sr.ReadLine ().Trim ();
                // Pass comment.
                if (gcode.Length < 5 || gcode [0] == ';')
                    continue;

                // Pass non-G1 commands.
                if (gcode.Substring (0, 3) != "G1 ")
                    continue;

                pos = gcode.IndexOf ('X', 3) + 1;
                next = gcode.IndexOf (' ', pos + 1);
                next = next > 0 ? next : gcode.Length;
                if (pos >= 4) {
                    newX = Convert.ToDouble (gcode.Substring (pos, next - pos));
                }

                pos = gcode.IndexOf ('Y', 3) + 1;
                next = gcode.IndexOf (' ', pos + 1);
                next = next > 0 ? next : gcode.Length;
                if (pos >= 4) {
                    newY = Convert.ToDouble (gcode.Substring (pos, next - pos));
                }

                pos = gcode.IndexOf ('F', 3) + 1;
                next = gcode.IndexOf (' ', pos + 1);
                next = next > 0 ? next : gcode.Length;
                if (pos >= 4) {
                    speed = Convert.ToDouble (gcode.Substring (pos, next - pos));
                }

                dX = Math.Abs (newX - oldX);
                dY = Math.Abs (newY - oldY);
                L = Math.Sqrt (dX * dX + dY * dY);

                oldX = newX;
                oldY = newY;

                totalX += dX;
                totalY += dY;
                totalLength += L;

                milliSecond += (L * 1000 * 60 / speed);
            }

            Console.WriteLine ("Total X Moves (mm) : {0}", totalX);
            Console.WriteLine ("Total Y Moves (mm) : {0}", totalY);
            Console.WriteLine ("Total Moves (mm) : {0}", totalLength);
            Console.WriteLine ("MilliSecond : {0}", milliSecond);

            TimeSpan t = TimeSpan.FromMilliseconds (milliSecond);
            Console.WriteLine ("Total Time : {0:dd} day(s) {1:hh\\:mm\\:ss}", t, t);
        }
    }
}
