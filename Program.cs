using System;
using System.IO;
using System.Collections.Generic;

namespace WaDAGcoder
{
    class MainClass
    {
        public static void Main (string[] args)
		{
			if (args.Length < 2) {
				PrintHelp ();
				return;
			}

			StreamReader sr = File.OpenText (args [args.Length - 1]);

			switch (args [0]) {
			case "incremental":
				string[] strs = args [args.Length - 1].Split ('.');
				if (strs != null) {
					GcodeHelper.ConvertToIncrementalFormat (sr, strs [0]);
				} else {
					Console.WriteLine ("!!! Please assign output file. !!!");
				}
				break;
			case "eval":
				GcodeHelper.EstimateWorkingTime (sr);
				break;
			case "para":
				PrintSlicingParameters (sr);
				break;
			case "offset":
				GcodeHelper.CompensateXY (args, sr);
				break;
			}
			sr.Close ();
		}

        private static void PrintHelp()
        {
            Console.WriteLine ("*** WaDAGcoder Utility ***");
            Console.WriteLine (" eval   => Evaluate working time.");
            Console.WriteLine (" para   => Print out slicing paramters.");
            Console.WriteLine (" offset => Regenerate Gcode to compensate X/Y coordinate.");
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
