/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System;

namespace JSI
{
    public class StringProcessorFormatter
    {
        // The formatString or plain text (if usesComp is false).
        public readonly string formatString;
        // An array of source variables
        public readonly VariableOrNumber[] sourceVariables;
        // An array holding evaluants
        public readonly object[] sourceValues;

        // Indicates that the SPF uses RPMVesselComputer to process variables
        public readonly bool usesComp;

        // TODO: Add support for multi-line processed support.
        public StringProcessorFormatter(string input, RasterPropMonitorComputer rpmComp)
        {
            if(string.IsNullOrEmpty(input))
            {
                formatString = "";
                usesComp = false;
            }
            else if (input.IndexOf(JUtil.VariableListSeparator[0], StringComparison.Ordinal) >= 0)
            {
                string[] tokens = input.Split(JUtil.VariableListSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2)
                {
                    throw new ArgumentException(string.Format("Invalid format string: {0}", input));
                }
                else
                {
                    string[] sourceVarStrings = tokens[1].Split(JUtil.VariableSeparator, StringSplitOptions.RemoveEmptyEntries);
                    sourceVariables = new VariableOrNumber[sourceVarStrings.Length];
                    for (int i = 0; i < sourceVarStrings.Length; ++i )
                    {
                        sourceVariables[i] = rpmComp.InstantiateVariableOrNumber(sourceVarStrings[i]);
                    }
                    sourceValues = new object[sourceVariables.Length];
                    formatString = tokens[0].TrimEnd();

                    usesComp = true;
                }
            }
            else
            {
                formatString = input.TrimEnd();
                usesComp = false;
            }
        }
    }

    public static class StringProcessor
    {
        private static readonly SIFormatProvider fp = new SIFormatProvider();

        public static string ProcessString(StringProcessorFormatter formatter, RasterPropMonitorComputer rpmComp)
        {
            if (formatter.usesComp)
            {
                try
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    for (int i = 0; i < formatter.sourceVariables.Length; ++i)
                    {
                        formatter.sourceValues[i] = formatter.sourceVariables[i].Get();
                    }

                    return string.Format(fp, formatter.formatString, formatter.sourceValues);
                }
                catch(Exception e)
                {
                    JUtil.LogErrorMessage(formatter, "Exception trapped in ProcessString for {1}: {0}", e, formatter.formatString);
                }
            }

            return formatter.formatString;
        }

        public static string ProcessString(string input, RasterPropMonitorComputer rpmComp)
        {
            try
            {
                if (input.IndexOf(JUtil.VariableListSeparator[0], StringComparison.Ordinal) >= 0)
                {
                    string[] tokens = input.Split(JUtil.VariableListSeparator, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length != 2)
                    {
                        return "FORMAT ERROR";
                    }
                    else
                    {
                        RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                        string[] vars = tokens[1].Split(JUtil.VariableSeparator, StringSplitOptions.RemoveEmptyEntries);

                        var variables = new object[vars.Length];
                        for (int i = 0; i < vars.Length; i++)
                        {
                            variables[i] = rpmComp.ProcessVariable(vars[i], comp);
                        }
                        string output = string.Format(fp, tokens[0], variables);
                        return output.TrimEnd();
                    }
                }

            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(rpmComp, "Bad format on string {0}: {1}", input, e);
            }

            return input.TrimEnd();
        }
    }
}
