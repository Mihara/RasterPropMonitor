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
using System.Collections.Generic;
using UnityEngine;
namespace JSI
{
    public class VariableOrNumber
    {
        private readonly string variableName;
        private float value;
        private bool warningMade;

        static private Dictionary<string, VariableOrNumber> vars = new Dictionary<string, VariableOrNumber>();

        /// <summary>
        /// Create a new VariableOrNumber, or return an existing one that
        /// tracks the same value.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static VariableOrNumber Instantiate(string input)
        {
            string varName = input.Trim();
            float floatval;
            if(float.TryParse(varName, out floatval))
            {
                // If it's a numeric value, let's canonicalize it using
                // ToString, so we don't have duplicates that evaluate to the
                // same value (eg, 1.0, 1, 1.00, etc).
                varName = floatval.ToString();
            }

            if(!vars.ContainsKey(varName))
            {
                VariableOrNumber VoN = new VariableOrNumber(varName);
                vars.Add(varName, VoN);
                //JUtil.LogMessage(null, "Adding VoN {0}", varName);
            }
            return vars[varName];
        }

        /// <summary>
        /// Used by RPMVesselComputer to signal that we no longer need the
        /// cache of variables.
        /// </summary>
        internal static void Clear()
        {
            vars.Clear();
        }

        private VariableOrNumber(string input)
        {
            float realValue;
            if (float.TryParse(input, out realValue))
            {
                value = realValue;
            }
            else
            {
                variableName = input.Trim();
            }
        }

        /// <summary>
        /// Evaluate the variable, returning it in destination.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="comp"></param>
        /// <returns></returns>
        public bool Get(out float destination, RPMVesselComputer comp)
        {
            if (!string.IsNullOrEmpty(variableName))
            {
                value = comp.ProcessVariable(variableName).MassageToFloat();
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    if (!warningMade)
                    {
                        JUtil.LogMessage(this, "Warning: {0} can fail to produce a usable number.", variableName);
                        warningMade = true;
                    }
                    destination = value;
                    return false;
                }
            }

            destination = value;
            return true;
        }
    }

    /// <summary>
    /// Encapsulates the code and logic required to track a variable-or-number
    /// that is bounded with a range that is likewise defined as variable-or-
    /// number.
    /// </summary>
    public class VariableOrNumberRange
    {
        VariableOrNumber sourceValue;
        VariableOrNumber lowerBound;
        VariableOrNumber upperBound;

        public VariableOrNumberRange(string sourceVariable, string range1, string range2)
        {
            sourceValue = VariableOrNumber.Instantiate(sourceVariable);
            lowerBound = VariableOrNumber.Instantiate(range1);
            upperBound = VariableOrNumber.Instantiate(range2);
        }

        public bool InverseLerp(RPMVesselComputer comp, out float scaledValue)
        {
            float value;
            float low, high;
            if (!(sourceValue.Get(out value, comp) && lowerBound.Get(out low, comp) && upperBound.Get(out high, comp)))
            {
                scaledValue = 0.0f;
                return false;
            }
            else
            {
                scaledValue = Mathf.InverseLerp(low, high, value);
                return true;
            }
        }

        /// <summary>
        /// Provides a simple boolean true/false for whether the named
        /// variable is in range.
        /// </summary>
        /// <param name="comp"></param>
        /// <param name="persistence"></param>
        /// <returns></returns>
        public bool IsInRange(RPMVesselComputer comp)
        {
            float value;
            float low, high;

            if(!(sourceValue.Get(out value, comp) && lowerBound.Get(out low, comp) && upperBound.Get(out high, comp)))
            {
                return false;
            }
            else if (high < low)
            {
                return (value >= high && value <= low);
            }
            else
            {
                return (value >= low && value <= high);
            }
        }
    }
}
