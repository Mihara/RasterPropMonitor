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
    public class JSIVariablePageTextSwitcher : InternalModule
    {
        private struct PageDefinition
        {
            internal readonly string variableName;
            internal readonly string range;
            internal readonly string page;
            internal PageDefinition(string variableName, string range, string page)
            {
                this.variableName = variableName;
                this.range = range;
                this.page = page;
            }
        };

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
        private int activePage;
        private PageDefinition[] definitions = null;
        private List<string> text = new List<string>();
        private List<VariableOrNumberRange> range = new List<VariableOrNumberRange>();
        private VariableOrNumberRange legacyRange;
        private bool pageActiveState;
        private bool initialized = false;
        private int updateCountdown;
        // Analysis disable UnusedParameter
        public string ShowPage(int width, int height)
        {
            return text[activePage];
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
            if (!pageActiveState || !initialized || !JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
            {
                return;
            }

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            if (legacyRange != null)
            {
                float scaledValue;
                if (!legacyRange.InverseLerp(comp, out scaledValue))
                {
                    activePage = 1;
                    return;
                }

                activePage = (scaledValue >= threshold.x && scaledValue <= threshold.y) ? 0 : 1;
            }
            else
            {

                activePage = 0;
                for (activePage = 0; activePage < range.Count; ++activePage)
                {
                    if (range[activePage].IsInRange(comp))
                    {
                        break;
                    }
                }
            }
        }

        public void Configure(ConfigNode node)
        {
            ConfigNode[] pages = node.GetNodes("PAGE_DEFINITION");

            if (pages != null && pages.Length > 0)
            {
                definitions = new PageDefinition[pages.Length];

                for (int i = 0; i < pages.Length; ++i)
                {
                    string variableName = pages[i].GetValue("variableName");
                    string range = pages[i].GetValue("range");
                    string page = pages[i].GetValue("page");
                    if (string.IsNullOrEmpty(variableName) || string.IsNullOrEmpty(range) || string.IsNullOrEmpty(page))
                    {
                        JUtil.LogErrorMessage(this, "Incorrect page definition for page {0}", i);
                        definitions = null;
                        if (string.IsNullOrEmpty(definitionIn))
                        {
                            // Make sure we aren't crashing later.
                            definitionIn = definitionOut;
                        }
                        return;
                    }
                    definitions[i] = new PageDefinition(variableName, range, page);
                }
            }
        }

        public void Start()
        {
            if (string.IsNullOrEmpty(definitionIn) && definitions != null)
            {
                for (int i = 0; i < definitions.Length; ++i)
                {
                    string[] varrange = definitions[i].range.Split(',');
                    range.Add(new VariableOrNumberRange(definitions[i].variableName, varrange[0], varrange[1]));
                    text.Add(JUtil.LoadPageDefinition(definitions[i].page));
                }
                definitions = null;
                initialized = true;
            }
            else
            {
                string[] tokens = scale.Split(',');

                if (tokens.Length == 2)
                {
                    legacyRange = new VariableOrNumberRange(variableName, tokens[0], tokens[1]);

                    float min = Mathf.Min(threshold.x, threshold.y);
                    float max = Mathf.Max(threshold.x, threshold.y);
                    threshold.x = min;
                    threshold.y = max;

                    text.Add(JUtil.LoadPageDefinition(definitionIn));

                    initialized = true;
                }
                else
                {
                    JUtil.LogErrorMessage(this, "Could not parse the 'scale' parameter: {0}", scale);
                }
            }

            text.Add(JUtil.LoadPageDefinition(definitionOut));
        }

        public void OnDestroy()
        {
            //JUtil.LogMessage(this, "OnDestroy()");
        }
    }
}
