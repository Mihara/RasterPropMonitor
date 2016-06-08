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
using System.Collections.Generic;
using System.Text;

namespace JSI
{
    // A SelectVariable defines a user-defined variable that consists of one or
    // more RPM variables.  Separate variables and ranges determine which result
    // is returned when the Select variable is chosen.
    class SelectVariable : IComplexVariable
    {
        public readonly string name;

        private List<VariableOrNumberRange> sourceVariables = new List<VariableOrNumberRange>();
        private List<bool> reverse = new List<bool>();
        private List<VariableOrNumber> result = new List<VariableOrNumber>();

        internal SelectVariable(ConfigNode node, RasterPropMonitorComputer rpmComp)
        {
            name = node.GetValue("name");

            foreach (ConfigNode sourceVarNode in node.GetNodes("VARIABLE_DEFINITION"))
            {
                bool reverseVal;
                VariableOrNumberRange vonr = ProcessSourceNode(sourceVarNode, rpmComp, out reverseVal);

                sourceVariables.Add(vonr);
                reverse.Add(reverseVal);

                VariableOrNumber val = rpmComp.InstantiateVariableOrNumber(sourceVarNode.GetValue("value"));
                result.Add(val);
            }

            if (node.HasValue("defaultValue"))
            {
                VariableOrNumber val = rpmComp.InstantiateVariableOrNumber(node.GetValue("defaultValue"));
                result.Add(val);
            }
            else
            {
                throw new Exception(string.Format("Select variable {0} is missing its defaultValue", name));
            }

            if (sourceVariables.Count == 0)
            {
                throw new ArgumentException("Did not find any VARIABLE_DEFINITION nodes in RPM_SELECT_VARIABLE", name);
            }

        }

        public object Evaluate(RasterPropMonitorComputer rpmComp, RPMVesselComputer comp)
        {
            int i = 0;
            for (; i < sourceVariables.Count; ++i)
            {
                if (sourceVariables[i].IsInRange(rpmComp, comp) ^ reverse[i])
                {
                    break;
                }
            }

            return result[i].Evaluate(rpmComp, comp);
        }

        private static VariableOrNumberRange ProcessSourceNode(ConfigNode node, RasterPropMonitorComputer rpmComp, out bool reverse)
        {
            VariableOrNumberRange range;
            if (node.HasValue("range"))
            {
                string[] tokens = { };
                tokens = node.GetValue("range").Split(',');
                if (tokens.Length != 2)
                {
                    throw new ArgumentException("Found an unparseable value reading custom SOURCE_VARIABLE range");
                }
                range = new VariableOrNumberRange(rpmComp, node.GetValue("name").Trim(), tokens[0].Trim(), tokens[1].Trim());
            }
            else
            {
                range = new VariableOrNumberRange(rpmComp, node.GetValue("name").Trim(), float.MinValue.ToString(), float.MaxValue.ToString());
            }

            if (node.HasValue("reverse"))
            {
                if (!bool.TryParse(node.GetValue("reverse"), out reverse))
                {
                    throw new ArgumentException("So is 'reverse' true or false?");
                }
            }
            else
            {
                reverse = false;
            }

            return range;
        }
    }
}
