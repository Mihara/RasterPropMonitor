using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSI
{
    public partial class RasterPropMonitorComputer : PartModule
    {
        internal RPMVesselComputer.VariableEvaluator GetEvaluator(string input, RPMVesselComputer comp, out bool cacheable)
        {
            if (input.IndexOf("_", StringComparison.Ordinal) > -1)
            {
                string[] tokens = input.Split('_');
                // Strings stored in module configuration.
                if (tokens.Length == 2 && tokens[0] == "STOREDSTRING")
                {
                    cacheable = true;

                    int storedStringNumber;
                    if (int.TryParse(tokens[1], out storedStringNumber) && storedStringNumber >= 0)
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp) =>
                        {
                            if (rpmComp == null)
                            {
                                return "";
                            }

                            string[] toks = variable.Split('_');
                            int storedNumber;
                            int.TryParse(toks[1], out storedNumber);
                            if (storedNumber < rpmComp.storedStringsArray.Count)
                            {
                                return rpmComp.storedStringsArray[storedNumber];
                            }
                            else
                            {
                                return "";
                            }
                        };
                    }
                    else
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp) =>
                        {
                            if (rpmComp == null)
                            {
                                return "";
                            }

                            string[] toks = variable.Split('_');
                            int stringNumber;
                            if (int.TryParse(toks[1], out stringNumber) && stringNumber >= 0 && stringNumber < rpmComp.storedStringsArray.Count)
                            {
                                return rpmComp.storedStrings[stringNumber];
                            }
                            else
                            {
                                return "";
                            }
                        };
                    }
                }
            }

            return comp.GetEvaluator(input, out cacheable);
        }
    }
}
