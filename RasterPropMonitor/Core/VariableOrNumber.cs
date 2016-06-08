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
        internal readonly string variableName;
        internal double numericValue;
        internal string stringValue;
        internal bool isNumeric;
        private readonly RasterPropMonitorComputer rpmComp;
        internal VoNType variableType = VoNType.Invalid;
        internal enum VoNType
        {
            Invalid,
            ConstantNumeric,
            ConstantString,
            VariableValue,
        }

        /// <summary>
        /// Initialize a VariableOrNumber
        /// </summary>
        /// <param name="input">The name of the variable</param>
        /// <param name="cacheable">Whether the variable is cacheable</param>
        /// <param name="rpmComp">The RasterPropMonitorComputer that owns the variable</param>
        internal VariableOrNumber(string input, bool cacheable, RasterPropMonitorComputer rpmComp_)
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
                variableType = VoNType.ConstantNumeric;
            }
            else if (input[0] == '$')
            {
                stringValue = input.Substring(1);
                variableType = VoNType.ConstantString;
            }
            else
            {
                variableName = varName;
                variableType = VoNType.VariableValue;

                if (!cacheable)
                {
                    rpmComp = rpmComp_;
                }
            }
        }

        /// <summary>
        /// Return the value as a float.
        /// </summary>
        /// <returns></returns>
        public float AsFloat()
        {
            if (rpmComp != null)
            {
                return rpmComp.ProcessVariable(variableName, null).MassageToFloat();
            }
            else
            {
                return (float)numericValue;
            }
        }

        /// <summary>
        /// Returns the value as a double.
        /// </summary>
        /// <returns></returns>
        public double AsDouble()
        {
            if (rpmComp != null)
            {
                return rpmComp.ProcessVariable(variableName, null).MassageToDouble();
            }
            else
            {
                return numericValue;
            }
        }

        /// <summary>
        /// Returns the value as an int.
        /// </summary>
        /// <returns></returns>
        public int AsInt()
        {
            if (rpmComp != null)
            {
                return rpmComp.ProcessVariable(variableName, null).MassageToInt();
            }
            else
            {
                return (int)numericValue;
            }
        }

        /// <summary>
        /// Return the value boxed as an object
        /// </summary>
        /// <returns></returns>
        public object Get()
        {
            if (rpmComp != null)
            {
                return rpmComp.ProcessVariable(variableName, null);
            }
            else if (isNumeric)
            {
                return numericValue;
            }
            else
            {
                return stringValue;
            }
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

        public VariableOrNumberRange(RasterPropMonitorComputer rpmComp, string sourceVariable, string range1, string range2, string moduloVariable = null)
        {
            sourceValue = rpmComp.InstantiateVariableOrNumber(sourceVariable);
            lowerBound = rpmComp.InstantiateVariableOrNumber(range1);
            upperBound = rpmComp.InstantiateVariableOrNumber(range2);
            if (!string.IsNullOrEmpty(moduloVariable))
            {
                modulo = rpmComp.InstantiateVariableOrNumber(moduloVariable);
            }
        }

        /// <summary>
        /// Return a value in the range of 0 to 1 representing where the current variable
        /// evaluates within its range.
        /// </summary>
        /// <returns></returns>
        public float InverseLerp()
        {
            float value = sourceValue.AsFloat();
            float low = lowerBound.AsFloat();
            float high = upperBound.AsFloat();

            if (modulo != null)
            {
                float mod = modulo.AsFloat();

                float scaledValue = Mathf.InverseLerp(low, high, value);
                float range = Mathf.Abs(high - low);
                if (range > 0.0f)
                {
                    float modDivRange = mod / range;
                    scaledValue = (scaledValue % (modDivRange)) / modDivRange;
                }
                //value = value % mod;
                return scaledValue;
            }
            else
            {
                return Mathf.InverseLerp(low, high, value);
            }
        }

        /// <summary>
        /// Provides a simple boolean true/false for whether the named
        /// variable is in range.
        /// </summary>
        /// <param name="comp"></param>
        /// <returns></returns>
        public bool IsInRange()
        {
            float value = sourceValue.AsFloat();
            float low = lowerBound.AsFloat();
            float high = upperBound.AsFloat();

            if (high < low)
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
        public bool IsInRange(float value)
        {
            float low = lowerBound.AsFloat();
            float high = upperBound.AsFloat();

            if (high < low)
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
