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
        private Action<float> del;
        private StringProcessorFormatter spf;
        private RasterPropMonitorComputer rpmComp;
        /// <summary>
        /// The Guid of the vessel we belonged to at Start.  When undocking,
        /// KSP will change the vessel member variable before calling OnDestroy,
        /// which prevents us from getting the RPMVesselComputer we registered
        /// with.  So we have to store the Guid separately.
        /// </summary>
        private Guid registeredVessel = Guid.Empty;

        public void Start()
        {
            try
            {
                rpmComp = RasterPropMonitorComputer.Instantiate(internalProp, true);

                Transform textObjTransform = internalProp.FindModelTransform(transformName);
                textObj = InternalComponents.Instance.CreateText("Arial", fontSize, textObjTransform, string.Empty);
                // Force oneshot if there's no variables:
                oneshot |= !labelText.Contains("$&$");
                string sourceString = labelText.UnMangleConfigText();

                if (!string.IsNullOrEmpty(sourceString) && sourceString.Length > 1)
                {
                    // Alow a " character to escape leading whitespace
                    if (sourceString[0] == '"')
                    {
                        sourceString = sourceString.Substring(1);
                    }
                }
                spf = new StringProcessorFormatter(sourceString, rpmComp);

                if (!oneshot)
                {
                    rpmComp.UpdateDataRefreshRate(refreshRate);
                }

                if (!(string.IsNullOrEmpty(variableName) || string.IsNullOrEmpty(positiveColor) || string.IsNullOrEmpty(negativeColor) || string.IsNullOrEmpty(zeroColor)))
                {
                    positiveColorValue = JUtil.ParseColor32(positiveColor, part, ref rpmComp);
                    negativeColorValue = JUtil.ParseColor32(negativeColor, part, ref rpmComp);
                    zeroColorValue = JUtil.ParseColor32(zeroColor, part, ref rpmComp);
                    del = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), this, "OnCallback");
                    rpmComp.RegisterVariableCallback(variableName, del);
                    registeredVessel = vessel.id;

                    // Initialize the text color. Actually, callback registration took care of that
                }
            }
            catch(Exception e)
            {
                JUtil.LogErrorMessage(this, "Start failed with exception {0}", e);
                spf = new StringProcessorFormatter("x", rpmComp);
            }
        }

        public void OnDestroy()
        {
            if (del != null)
            {
                try
                {
                    rpmComp.UnregisterVariableCallback(variableName, del);
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

        private void OnCallback(float value)
        {
            // Sanity checks:
            if (vessel == null)
            {
                // We're not attached to a ship?
                rpmComp.UnregisterVariableCallback(variableName, del);
                JUtil.LogErrorMessage(this, "Received an unexpected OnCallback()");
                return;
            }

            if (textObj == null)
            {
                // I don't know what is going on here.  This callback is
                // getting called when textObj is null - did the callback
                // fail to unregister on destruction?  It can't get called
                // before textObj is created.
                if (del != null && !string.IsNullOrEmpty(variableName))
                {
                    rpmComp.UnregisterVariableCallback(variableName, del);
                }
                JUtil.LogErrorMessage(this, "Received an unexpected OnCallback() when textObj was null");
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
            if (textObj == null)
            {
                // Shouldn't happen ... but it does, thanks to the quirks of
                // docking and undocking.
                return;
            }

            if (oneshotComplete && oneshot)
            {
                return;
            }

            if (JUtil.RasterPropMonitorShouldUpdate(vessel) && UpdateCheck())
            {
                textObj.text.Text = StringProcessor.ProcessString(spf, rpmComp);
                oneshotComplete = true;
            }
        }
    }
}
