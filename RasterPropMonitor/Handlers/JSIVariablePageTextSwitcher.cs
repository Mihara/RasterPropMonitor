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
                if (!scaleEnds[i].Get(out scaleResults[i], comp))
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
                scaleEnds[0] = VariableOrNumber.Instantiate(tokens[0]);
                scaleEnds[1] = VariableOrNumber.Instantiate(tokens[1]);
                scaleEnds[2] = VariableOrNumber.Instantiate(variableName);

                textIn = JUtil.LoadPageDefinition(definitionIn);
                textOut = JUtil.LoadPageDefinition(definitionOut);

                float min = Mathf.Min(threshold.x, threshold.y);
                float max = Mathf.Max(threshold.x, threshold.y);
                threshold.x = min;
                threshold.y = max;
            }
            else
            {
                JUtil.LogErrorMessage(this, "Could not parse the 'scale' parameter: {0}", scale);
            }
        }

        public void OnDestroy()
        {
            //JUtil.LogMessage(this, "OnDestroy()");
        }
    }
}

