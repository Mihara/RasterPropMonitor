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
        internal double numericValue;
        internal string stringValue;
        internal bool isNumeric;
        internal readonly bool isCacheable;
        private bool warningMade;
        internal readonly VoNType type = VoNType.Invalid;
        internal enum VoNType
        {
            Invalid,
            ConstantNumeric,
            ConstantString,
            VariableValue,
        }

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
            if (float.TryParse(varName, out floatval))
            {
                // If it's a numeric value, let's canonicalize it using
                // ToString, so we don't have duplicates that evaluate to the
                // same value (eg, 1.0, 1, 1.00, etc).
                varName = floatval.ToString();
            }

            if (varName == "MetersToFeet")
            {
                varName = RPMGlobals.MetersToFeet.ToString();
            }
            else if (varName == "MetersPerSecondToKnots")
            {
                varName = RPMGlobals.MetersPerSecondToKnots.ToString();
            }
            else if (varName == "MetersPerSecondToFeetPerMinute")
            {
                varName = RPMGlobals.MetersPerSecondToFeetPerMinute.ToString();
            }

            if (!vars.ContainsKey(varName))
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

        internal VariableOrNumber(string input, bool cacheable, RasterPropMonitorComputer rpmComp)
        {
            string varName = input.Trim();
            if (varName == "MetersToFeet")
            {
                varName = RPMGlobals.MetersToFeet.ToString();
            }
            else if (varName == "MetersPerSecondToKnots")
            {
                varName = RPMGlobals.MetersPerSecondToKnots.ToString();
            }
            else if (varName == "MetersPerSecondToFeetPerMinute")
            {
                varName = RPMGlobals.MetersPerSecondToFeetPerMinute.ToString();
            }

            float realValue;
            if (float.TryParse(varName, out realValue))
            {
                // If it's a numeric value, let's canonicalize it using
                // ToString, so we don't have duplicates that evaluate to the
                // same value (eg, 1.0, 1, 1.00, etc).
                varName = realValue.ToString();
                numericValue = realValue;
                type = VoNType.ConstantNumeric;
                isCacheable = true;
            }
            else if (input[0] == '$')
            {
                stringValue = input.Substring(1);
                type = VoNType.ConstantString;
                isCacheable = true;
            }
            else
            {
                variableName = varName;
                type = VoNType.VariableValue;
                isCacheable = cacheable;
            }
        }

        private VariableOrNumber(string input)
        {
            float realValue;
            if (float.TryParse(input, out realValue))
            {
                numericValue = realValue;
                type = VoNType.ConstantNumeric;
            }
            else if (input[0] == '$')
            {
                stringValue = input.Substring(1);
                type = VoNType.ConstantString;
            }
            else
            {
                variableName = input.Trim();
                type = VoNType.VariableValue;
            }
        }

        public object Evaluate(RasterPropMonitorComputer rpmComp, RPMVesselComputer comp)
        {
            if (type == VoNType.ConstantNumeric)
            {
                return numericValue;
            }
            else if (type == VoNType.ConstantString)
            {
                return stringValue;
            }
            else if (type == VoNType.VariableValue)
            {
                return rpmComp.ProcessVariable(variableName, comp);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Evaluate the variable, returning it in destination.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="comp"></param>
        /// <returns></returns>
        public bool Get(out float destination, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp)
        {
            if (type == VoNType.ConstantString)
            {
                destination = 0.0f;
                return false;
            }
            else if (type == VoNType.VariableValue)
            {
                numericValue = rpmComp.ProcessVariable(variableName, comp).MassageToDouble();
                if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
                {
                    if (!warningMade)
                    {
                        JUtil.LogMessage(this, "Warning: {0} can fail to produce a usable number.", variableName);
                        warningMade = true;
                    }
                    destination = (float)numericValue;
                    return false;
                }
            }

            destination = (float)numericValue;
            return true;
        }

        /// <summary>
        /// Evaluate the variable, returning it in destination.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="comp"></param>
        /// <returns></returns>
        public bool Get(out double destination, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp)
        {
            if (type == VoNType.ConstantString)
            {
                destination = 0.0;
                return false;
            }
            else if (type == VoNType.VariableValue)
            {
                numericValue = rpmComp.ProcessVariable(variableName, comp).MassageToDouble();
                if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
                {
                    if (!warningMade)
                    {
                        JUtil.LogMessage(this, "Warning: {0} can fail to produce a usable number.", variableName);
                        warningMade = true;
                    }
                    destination = numericValue;
                    return false;
                }
            }

            destination = numericValue;
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
        VariableOrNumber modulo;

        public VariableOrNumberRange(string sourceVariable, string range1, string range2, string moduloVariable = null)
        {
            sourceValue = VariableOrNumber.Instantiate(sourceVariable);
            lowerBound = VariableOrNumber.Instantiate(range1);
            upperBound = VariableOrNumber.Instantiate(range2);
            if (!string.IsNullOrEmpty(moduloVariable))
            {
                modulo = VariableOrNumber.Instantiate(moduloVariable);
            }
        }

        public bool InverseLerp(RasterPropMonitorComputer rpmComp, RPMVesselComputer comp, out float scaledValue)
        {
            float value;
            float low, high;
            if (!(sourceValue.Get(out value, rpmComp, comp) && lowerBound.Get(out low, rpmComp, comp) && upperBound.Get(out high, rpmComp, comp)))
            {
                scaledValue = 0.0f;
                return false;
            }
            else
            {
                if (modulo != null)
                {
                    float mod;
                    if (!modulo.Get(out mod, rpmComp, comp) || mod <= 0.0f)
                    {
                        scaledValue = 0.0f;
                        return false;
                    }
                    else
                    {
                        scaledValue = Mathf.InverseLerp(low, high, value);
                        float range = Mathf.Abs(high - low);
                        if (range > 0.0f)
                        {
                            float modDivRange = mod / range;
                            scaledValue = (scaledValue % (modDivRange)) / modDivRange;
                        }
                        //value = value % mod;
                        return true;
                    }
                }
                else
                {
                    scaledValue = Mathf.InverseLerp(low, high, value);
                    return true;
                }
            }
        }

        /// <summary>
        /// Provides a simple boolean true/false for whether the named
        /// variable is in range.
        /// </summary>
        /// <param name="comp"></param>
        /// <returns></returns>
        public bool IsInRange(RasterPropMonitorComputer rpmComp, RPMVesselComputer comp)
        {
            float value;
            float low, high;

            if (!(sourceValue.Get(out value, rpmComp, comp) && lowerBound.Get(out low, rpmComp, comp) && upperBound.Get(out high, rpmComp, comp)))
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

        /// <summary>
        /// Provides a simple boolean true/false for whether the named
        /// variable is in range.
        /// </summary>
        /// <param name="comp"></param>
        /// <param name="value">The value to test (provided externally)</param>
        /// <returns></returns>
        public bool IsInRange(RasterPropMonitorComputer rpmComp, RPMVesselComputer comp, float value)
        {
            float low, high;

            if (!(lowerBound.Get(out low, rpmComp, comp) && upperBound.Get(out high, rpmComp, comp)))
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
