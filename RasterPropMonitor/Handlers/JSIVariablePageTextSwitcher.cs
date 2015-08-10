using UnityEngine;

namespace JSI
{
    public class JSIVariablePageTextSwitcher : InternalModule
    {
        [KSPField]
        public string variableName;
        [KSPField]
        public string scale;
        [KSPField]
        public Vector2 threshold;
        [KSPField]
        public string definitionOut = string.Empty;
        [KSPField]
        public string definitionIn = string.Empty;
        [KSPField]
        public int refreshRate = 10;
        private string textOut, textIn;
        private readonly VariableOrNumber[] scaleEnds = new VariableOrNumber[3];
        private readonly float[] scaleResults = new float[3];
        private bool pageActiveState;
        private bool isInThreshold;
        private int updateCountdown;
        private PersistenceAccessor persistence;
        // Analysis disable UnusedParameter
        public string ShowPage(int width, int height)
        {
            return isInThreshold ? textIn : textOut;
        }

        public void PageActive(bool active, int pageNumber)
        {
            pageActiveState = active;
        }
        // Analysis restore UnusedParameter
        private bool UpdateCheck()
        {
            if (updateCountdown <= 0)
            {
                updateCountdown = refreshRate;
                return true;
            }
            updateCountdown--;
            return false;
        }
        // I don't like this mess of copypaste, but how can I improve it away?...
        public override void OnUpdate()
        {
            if (!pageActiveState || !JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
                return;

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            for (int i = 0; i < 3; i++)
            {
                if (!scaleEnds[i].Get(out scaleResults[i], comp, persistence))
                {
                    return;
                }
            }

            float scaledValue = Mathf.InverseLerp(scaleResults[0], scaleResults[1], scaleResults[2]);

            isInThreshold = (scaledValue >= threshold.x && scaledValue <= threshold.y);
        }

        public void Start()
        {

            string[] tokens = scale.Split(',');

            if (tokens.Length == 2)
            {

                //comp = RasterPropMonitorComputer.Instantiate(internalProp);
                scaleEnds[0] = new VariableOrNumber(tokens[0], this);
                scaleEnds[1] = new VariableOrNumber(tokens[1], this);
                scaleEnds[2] = new VariableOrNumber(variableName, this);

                textIn = JUtil.LoadPageDefinition(definitionIn);
                textOut = JUtil.LoadPageDefinition(definitionOut);

                float min = Mathf.Min(threshold.x, threshold.y);
                float max = Mathf.Max(threshold.x, threshold.y);
                threshold.x = min;
                threshold.y = max;

                persistence = new PersistenceAccessor(internalProp);
            }
            else
            {
                JUtil.LogErrorMessage(this, "Could not parse the 'scale' parameter: {0}", scale);
            }
        }

        public void OnDestroy()
        {
            //JUtil.LogMessage(this, "OnDestroy()");
            persistence = null;
        }
    }
}

