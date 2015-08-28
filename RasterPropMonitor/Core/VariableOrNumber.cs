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
namespace JSI
{
    public class VariableOrNumber
    {
        public readonly string variableName;
        private float value;
        private bool warningMade;

        public VariableOrNumber(string input)
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
        private readonly string sourceValueName;
        private readonly string lowerBoundName;
        private readonly string upperBoundName;

        private readonly float sourceValue;
        private readonly float lowerBound;
        private readonly float upperBound;

        private bool warningMade;

        public VariableOrNumberRange(string sourceVariable, string range1, string range2)
        {
            float realValue;
            if (float.TryParse(sourceVariable, out realValue))
            {
                sourceValue = realValue;
            }
            else
            {
                sourceValueName = sourceVariable.Trim();
            }

            if (float.TryParse(range1, out realValue))
            {
                lowerBound = realValue;
            }
            else
            {
                lowerBoundName = range1.Trim();
            }

            if (float.TryParse(range2, out realValue))
            {
                upperBound = realValue;
            }
            else
            {
                upperBoundName = range2.Trim();
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

            if (!string.IsNullOrEmpty(sourceValueName))
            {
                value = comp.ProcessVariable(sourceValueName).MassageToFloat();
            }
            else
            {
                value = sourceValue;
            }
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                if (!warningMade)
                {
                    JUtil.LogMessage(this, "Warning: {0} can fail to produce a usable number.", sourceValueName);
                    warningMade = true;
                }

                return false;
            }

            if (!string.IsNullOrEmpty(lowerBoundName))
            {
                low = comp.ProcessVariable(lowerBoundName).MassageToFloat();
                if (float.IsNaN(low) || float.IsInfinity(low))
                {
                    if (!warningMade)
                    {
                        JUtil.LogMessage(this, "Warning: {0} can fail to produce a usable number.", lowerBoundName);
                        warningMade = true;
                    }

                    return false;
                }
            }
            else
            {
                low = lowerBound;
            }

            if (!string.IsNullOrEmpty(upperBoundName))
            {
                high = comp.ProcessVariable(upperBoundName).MassageToFloat();
                if (float.IsNaN(high) || float.IsInfinity(high))
                {
                    if (!warningMade)
                    {
                        JUtil.LogMessage(this, "Warning: {0} can fail to produce a usable number.", upperBoundName);
                        warningMade = true;
                    }

                    return false;
                }
            }
            else
            {
                high = upperBound;
            }

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
