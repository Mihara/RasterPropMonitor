namespace JSI
{
    public class VariableOrNumber
    {
        private bool warningMade;
        private readonly float? value;
        private readonly string variableName;
        private readonly object owner;
        private readonly RasterPropMonitorComputer comp;
        private System.Func<bool> stateFunction;

        public VariableOrNumber(string input, RasterPropMonitorComputer compInstance, object caller)
        {
            owner = caller;
            comp = compInstance;
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

        public VariableOrNumber(string input, object caller)
        {
            owner = caller;
            comp = null;
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

        public VariableOrNumber(System.Func<bool> stateFunction, object caller)
        {
            this.stateFunction = stateFunction;
            owner = caller;
        }

        public bool Get(out float destination)
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
            destination = comp.ProcessVariable(variableName, -1).MassageToFloat();
            if (float.IsNaN(destination) || float.IsInfinity(destination))
            {
                if (!warningMade)
                {
                    JUtil.LogMessage(owner, "Warning, {0} can fail to produce a usable number.", variableName);
                    warningMade = true;
                    return false;
                }
            }
            return true;
        }

        public bool Get(out float destination, RasterPropMonitorComputer incomp)
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
            destination = incomp.ProcessVariable(variableName, -1).MassageToFloat();
            if (float.IsNaN(destination) || float.IsInfinity(destination))
            {
                if (!warningMade)
                {
                    JUtil.LogMessage(owner, "Warning: {0} can fail to produce a usable number.", variableName);
                    warningMade = true;
                    return false;
                }
            }
            return true;
        }
    }
}
