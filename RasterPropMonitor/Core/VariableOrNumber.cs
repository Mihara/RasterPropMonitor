namespace JSI
{
    public class VariableOrNumber
    {
        private bool warningMade;
        private readonly float? value;
        private readonly string variableName;
        private System.Func<bool> stateFunction;

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

        //public VariableOrNumber(float input)
        //{
        //    value = input;
        //}

        public VariableOrNumber(System.Func<bool> stateFunction)
        {
            this.stateFunction = stateFunction;
        }

        public bool Get(out float destination, RPMVesselComputer comp, PersistenceAccessor persistence)
        {
            if (stateFunction != null)
            {
                bool state = stateFunction();
                destination = state.GetHashCode();
                return true;
            }

            if (value != null)
            {
                destination = value.Value;
                return true;
            }

            destination = comp.ProcessVariable(variableName, persistence).MassageToFloat();
            if (float.IsNaN(destination) || float.IsInfinity(destination))
            {
                if (!warningMade)
                {
                    JUtil.LogMessage(this, "Warning: {0} can fail to produce a usable number.", variableName);
                    warningMade = true;
                }
                return false;
            }
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
        private bool warningMade;

        private readonly float sourceValue;
        private readonly float lowerBound;
        private readonly float upperBound;

        private readonly string sourceValueName;
        private readonly string lowerBoundName;
        private readonly string upperBoundName;

        private System.Func<bool> stateFunction;

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

        public VariableOrNumberRange(System.Func<bool> stateFunction, string range1, string range2)
        {
            this.stateFunction = stateFunction;
            float realValue;

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
        public bool IsInRange(RPMVesselComputer comp, PersistenceAccessor persistence)
        {
            float value;
            float low, high;

            if (stateFunction != null)
            {
                bool state = stateFunction();
                value = state.GetHashCode();
            }
            else if (!string.IsNullOrEmpty(sourceValueName))
            {
                value = comp.ProcessVariable(sourceValueName, persistence).MassageToFloat();
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
                low = comp.ProcessVariable(lowerBoundName, persistence).MassageToFloat();
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
                high = comp.ProcessVariable(upperBoundName, persistence).MassageToFloat();
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
