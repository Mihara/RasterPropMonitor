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
using UnityEngine;

namespace JSI
{
    public class JSIVariableLabel : InternalModule
    {
        [KSPField]
        public string labelText = "uninitialized";
        [KSPField]
        public string transformName;
        [KSPField]
        public float fontSize = 0.008f;
        [KSPField]
        public int refreshRate = 10;
        [KSPField]
        public bool oneshot;
        private bool oneshotComplete;
        [KSPField]
        public string variableName = string.Empty;
        [KSPField]
        public string positiveColor = string.Empty;
        private Color positiveColorValue = XKCDColors.White;
        [KSPField]
        public string negativeColor = string.Empty;
        private Color negativeColorValue = XKCDColors.White;
        [KSPField]
        public string zeroColor = string.Empty;
        private Color zeroColorValue = XKCDColors.White;

        private InternalText textObj;
        private int updateCountdown;
        private Action<RPMVesselComputer, float> del;
        private StringProcessorFormatter spf;

        // Annoying as it is, that is the only font actually available to InternalComponents for some bizarre reason,
        // even though I'm pretty sure there are quite a few other fonts in there.
        private const string fontName = "Arial";

        public void Start()
        {
            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);

            Transform textObjTransform = internalProp.FindModelTransform(transformName);
            textObj = InternalComponents.Instance.CreateText(fontName, fontSize, textObjTransform, string.Empty);
            // Force oneshot if there's no variables:
            oneshot |= !labelText.Contains("$&$");
            string sourceString = labelText.UnMangleConfigText();

            // Alow a " character to escape leading whitespace
            if (sourceString[0] == '"')
            {
                sourceString = sourceString.Substring(1);
            }
            spf = new StringProcessorFormatter(sourceString);

            if (!oneshot)
            {
                comp.UpdateDataRefreshRate(refreshRate);
            }

            if (!(string.IsNullOrEmpty(variableName) || string.IsNullOrEmpty(positiveColor) || string.IsNullOrEmpty(negativeColor) || string.IsNullOrEmpty(zeroColor)))
            {
                positiveColorValue = ConfigNode.ParseColor32(positiveColor);
                negativeColorValue = ConfigNode.ParseColor32(negativeColor);
                zeroColorValue = ConfigNode.ParseColor32(zeroColor);
                del = (Action<RPMVesselComputer, float>)Delegate.CreateDelegate(typeof(Action<RPMVesselComputer, float>), this, "OnCallback");
                comp.RegisterCallback(variableName, del);

                // Initialize the text color.
                float value = comp.ProcessVariable(variableName).MassageToFloat();
                if (value < 0.0f)
                {
                    textObj.text.Color = negativeColorValue;
                }
                else if (value > 0.0f)
                {
                    textObj.text.Color = positiveColorValue;
                }
                else
                {
                    textObj.text.Color = zeroColorValue;
                }
            }
        }

        public void OnDestroy()
        {
            if (del != null)
            {
                try
                {
                    RPMVesselComputer comp = null;
                    if (RPMVesselComputer.TryGetInstance(vessel, ref comp))
                    {
                        comp.UnregisterCallback(variableName, del);
                    }
                }
                catch
                {
                    //JUtil.LogMessage(this, "Trapped exception unregistering JSIVariableLabel (you can ignore this)");
                }
            }
            //JUtil.LogMessage(this, "OnDestroy()");
            Destroy(textObj);
            textObj = null;
        }

        private void OnCallback(RPMVesselComputer comp, float value)
        {
            // Sanity checks:
            if (vessel == null || vessel.id != comp.id)
            {
                // We're not attached to a ship?
                comp.UnregisterCallback(variableName, del);
                return;
            }

            if (value < 0.0f)
            {
                textObj.text.Color = negativeColorValue;
            }
            else if (value > 0.0f)
            {
                textObj.text.Color = positiveColorValue;
            }
            else
            {
                textObj.text.Color = zeroColorValue;
            }
        }

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

        public override void OnUpdate()
        {
            if (oneshotComplete && oneshot)
            {
                return;
            }

            if (JUtil.RasterPropMonitorShouldUpdate(vessel) && UpdateCheck())
            {
                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                textObj.text.Text = StringProcessor.ProcessString(spf, comp);
                oneshotComplete = true;
            }
        }
    }
}
