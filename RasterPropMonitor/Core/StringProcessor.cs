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
    public static class StringProcessor
    {
        private static readonly SIFormatProvider fp = new SIFormatProvider();

        public static string ProcessString(string input, RPMVesselComputer comp, int propID = -1)
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
                        string[] vars = tokens[1].Split(JUtil.VariableSeparator, StringSplitOptions.RemoveEmptyEntries);

                        var variables = new object[vars.Length];
                        for (int i = 0; i < vars.Length; i++)
                        {
                            variables[i] = comp.ProcessVariable(vars[i], propID);
                        }
                        string output = string.Format(fp, tokens[0], variables);
                        return output.TrimEnd();
                    }
                }
            }
            catch (Exception e)
            {
                JUtil.LogMessage(comp, "Bad format on string {0}", input);
                throw e;
            }
            return input.TrimEnd();
        }
    }
}
