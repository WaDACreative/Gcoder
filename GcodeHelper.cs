using System;
using System.IO;

namespace WaDAGcoder
{
	public class GcodeHelper
	{
		public GcodeHelper ()
		{
		}

		#region Main Functions
		public static void ConvertToIncrementalFormat(StreamReader sr, string szOutput)
		{
			StreamWriter sw = File.CreateText (string.Format ("{0}-incremental.gcode", szOutput));

            bool skip = false, ignored = false;
			string gcode;
			string command = "";
			double currentX = 0, currentY = 0, currentZ = 0, currentE = 0;
			double X, Y, Z, E, F;
			double deltaX = 0, deltaY = 0, deltaZ = 0, deltaE = 0;
            bool InG91Mode = false;
            bool RestoreAccel = false;

			while (!sr.EndOfStream) {
				gcode = sr.ReadLine().Trim();
				skip = false;
                ignored = false;

				skip |= (gcode.Length < 5 || gcode [0] == ';');

                ignored |= gcode.Contains ("M190");
                ignored |= gcode.Contains ("M109");
                ignored |= gcode.Contains ("M82");
                ignored |= gcode.Contains ("M204");
                ignored |= gcode.Contains ("M106");
                ignored |= gcode.Contains ("M107");
                ignored |= gcode.Contains ("M104");
                ignored |= gcode.Contains ("M140");
                ignored |= gcode.Contains ("M84");
                ignored |= gcode.Contains ("G28");

                if (InG91Mode)
                    ignored |= gcode.Contains ("G90");
                
                if (ignored)
                    continue;
                
				if (!skip)
					command = gcode.Substring (0, 3);
				
                if (!InG91Mode)
                    InG91Mode = gcode.Contains ("G91");
                
				// Pass non-G1 commands.
                if (!skip && command == "G28") {
                    currentX = 0;
                    currentY = 0;
                    currentZ = 0;
                } else if (!skip && command == "G92") {
					GetGcodeE (gcode, ref currentE);
					// Skip G92.
					gcode = "";
				}
				else if (!skip && command == "G1 ") {
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

                    if (!RestoreAccel) {
                        RestoreAccel = gcode.EndsWith ("; perimeter");
                        if (RestoreAccel)
                            sw.WriteLine ("G5.1 Q1 R1");
                    }

					gcode = "G1";
					if (X != 0) {
						deltaX = X - currentX;
						currentX = X;
						gcode += string.Format (" X{0:F3}", deltaX);
					}
					if (Y != 0) {
						deltaY = Y - currentY;
						currentY = Y;
						gcode += string.Format (" Y{0:F3}", deltaY);
					}
					if (Z != 0) {
						deltaZ = Z - currentZ;
						currentZ = Z;
						gcode += string.Format (" Z{0:F3}", deltaZ);
					}
					if (E != 0) {
						deltaE = E - currentE;
						currentE = E;
						gcode += string.Format (" C{0:F5}", deltaE);
					}
					if (F != 0) {
						gcode += string.Format (" F{0:F3}", F);
					}
				}
				sw.WriteLine (gcode);
			}
			sw.Close ();
		}

		public static void EstimateWorkingTime(StreamReader sr)
		{
			string gcode;
			double oldX = 0, oldY = 0, oldZ = 0, oldE = 0;
			double newX = 0, newY = 0, newZ = 0, newE = 0;
			double dX, dY, dZ, dE, L;
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
				if (gcode.Substring (0, 4) == "G92 ") {
					GetGcodeE (gcode, ref oldE);
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

                    if (GetGcodeZ (gcode, ref newZ))
                        dZ = Math.Abs (newZ - oldZ);
                    else
                        dZ = 0;
                    
                    if (GetGcodeE (gcode, ref newE)) {
                        dE = Math.Abs (newE - oldE);
                        oldE = newE;
                    }
                    else
                        dE = 0;

                    // Update speed if specified.
                    GetGcodeF (gcode, ref speed);

					L = Math.Sqrt (dX * dX + dY * dY);

					oldX = newX;
					oldY = newY;

					totalX += dX;
					totalY += dY;
                    totalE += dE;

					totalLength += L;
                    if (L > 0)
                        milliSecond += (L * 1000 * 60 / speed);
                    else if (dZ > dE)
                        milliSecond += (dZ * 1000 * 60 / speed);
                    else
                        milliSecond += (dE * 1000 * 60 / speed);
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

		public static void CompensateXY(string[] args, StreamReader sr)
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

		#region Helper Methods
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
		#endregion
	}
}

