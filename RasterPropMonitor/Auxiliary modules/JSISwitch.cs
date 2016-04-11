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
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace JSI
{
    /// <summary>
    /// Replacement for JSIActionGroupSwitch that supports multiple actions as a consequence of a "click".
    /// </summary>
    class JSISwitch : InternalModule
    {

        /// <summary>
        /// Name of the transform that triggers this object.  Required.
        /// </summary>
        [KSPField]
        public string switchTransform = string.Empty;
        /// <summary>
        /// Whether the switch is momentary (generates a Click on press and on release) or not
        /// </summary>
        [KSPField]
        public bool momentarySwitch = false;

        /// <summary>
        /// Name of the animation to play in response to button clicks.
        /// </summary>
        [KSPField]
        public string animationName = string.Empty;
        private Animation anim;
        /// <summary>
        /// Should the animation be reversed?
        /// </summary>
        [KSPField]
        public bool reverse = false;
        /// <summary>
        /// How quickly should the animation play?
        /// </summary>
        [KSPField]
        public float customSpeed = 1f;

        /// <summary>
        /// URI to the sound that's played when the switch is pressed.  Optional.
        /// </summary>
        [KSPField]
        public string switchSound = string.Empty;
        private FXGroup switchAudio;
        /// <summary>
        /// Volume of the sound that's played.  Optional.
        /// </summary>
        [KSPField]
        public float switchSoundVolume = 0.5f;

        [KSPField]
        public string loopingSound = string.Empty;
        private FXGroup loopingAudio;
        [KSPField]
        public float loopingSoundVolume = 0.0f;

        /// <summary>
        /// Name of a variable that can be used to disable the switch.
        /// </summary>
        [KSPField]
        public string masterVariableName = string.Empty;
        private VariableOrNumberRange masterVariable = null;
        /// <summary>
        /// Range of values where the masterVariable allows changes.
        /// </summary>
        [KSPField]
        public string masterVariableRange = string.Empty;

        [KSPField]
        public string consumeOnToggle = string.Empty;
        [KSPField]
        public string consumeWhileActive = string.Empty;
        private bool consumingOnToggleUp, consumingOnToggleDown;
        private string consumeOnToggleName = string.Empty;
        private float consumeOnToggleAmount;
        private bool consumingWhileActive;
        private string consumeWhileActiveName = string.Empty;
        private float consumeWhileActiveAmount;

        /// <summary>
        /// Initial state of the switch.  Optional.  Only for props using a persistent value, though.
        /// </summary>
        [KSPField]
        public bool initialState = false;
        /// <summary>
        /// Refresh rate for the vessel object.  Optional & irrelevant if we don't query RPMVesselComp.
        /// </summary>
        [KSPField]
        public int refreshRate = 60;

        private bool startupComplete = false;
        private bool currentState = false;
        private bool forcedShutdown = false;
        private List<IJSIAction> action = new List<IJSIAction>();
        private int masterActionIndex = -1;

        /// <summary>
        /// Configure the switch.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            if (string.IsNullOrEmpty(switchTransform))
            {
                throw new Exception("JSISwitch failed to configure: no switchTransform specified in prop " + internalProp.propName);
            }

            if (momentarySwitch)
            {
                SmarterButton.CreateButton(internalProp, switchTransform, Click, Click);
            }
            else
            {
                SmarterButton.CreateButton(internalProp, switchTransform, Click);
            }

            if (!string.IsNullOrEmpty(switchSound))
            {
                switchAudio = JUtil.SetupIVASound(internalProp, switchSound, switchSoundVolume, false);
            }

            if (!string.IsNullOrEmpty(animationName))
            {
                // Set up the animation
                Animation[] animators = internalProp.FindModelAnimators(animationName);
                //Animation[] animators = animateExterior ? part.FindModelAnimators(animationName) : internalProp.FindModelAnimators(animationName);
                if (animators.Length > 0)
                {
                    anim = animators[0];
                }
                else
                {
                    JUtil.LogErrorMessage(this, "Could not find animation \"{0}\" on {2} \"{1}\"",
                        animationName, internalProp.name, "prop");
                    //JUtil.LogErrorMessage(this, "Could not find animation \"{0}\" on {2} \"{1}\"",
                    //    animationName, animateExterior ? part.name : internalProp.name, animateExterior ? "part" : "prop");
                    return;
                }
                anim[animationName].wrapMode = WrapMode.Once;

                if (currentState ^ reverse)
                {
                    anim[animationName].speed = float.MaxValue;
                    anim[animationName].normalizedTime = 0.0f;

                }
                else
                {
                    anim[animationName].speed = float.MinValue;
                    anim[animationName].normalizedTime = 1.0f;
                }
                anim.Play(animationName);
            }

            // Now parse consumeOnToggle and consumeWhileActive...
            if (!string.IsNullOrEmpty(consumeOnToggle))
            {
                string[] tokens = consumeOnToggle.Split(',');
                if (tokens.Length == 3)
                {
                    consumeOnToggleName = tokens[0].Trim();
                    if (!(PartResourceLibrary.Instance.GetDefinition(consumeOnToggleName) != null &&
                       float.TryParse(tokens[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture,
                           out consumeOnToggleAmount)))
                    {
                        JUtil.LogErrorMessage(this, "Could not parse \"{0}\"", consumeOnToggle);
                    }
                    switch (tokens[2].Trim().ToLower())
                    {
                        case "on":
                            consumingOnToggleUp = true;
                            break;
                        case "off":
                            consumingOnToggleDown = true;
                            break;
                        case "both":
                            consumingOnToggleUp = true;
                            consumingOnToggleDown = true;
                            break;
                        default:
                            JUtil.LogErrorMessage(this, "So should I consume resources when turning on, turning off, or both in \"{0}\"?", consumeOnToggle);
                            break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(consumeWhileActive))
            {
                string[] tokens = consumeWhileActive.Split(',');
                if (tokens.Length == 2)
                {
                    consumeWhileActiveName = tokens[0].Trim();
                    if (!(PartResourceLibrary.Instance.GetDefinition(consumeWhileActiveName) != null &&
                       float.TryParse(tokens[1].Trim(),
                           NumberStyles.Any, CultureInfo.InvariantCulture,
                           out consumeWhileActiveAmount)))
                    {
                        JUtil.LogErrorMessage(this, "Could not parse \"{0}\"", consumeWhileActive);
                    }
                    else
                    {
                        consumingWhileActive = true;
                        JUtil.LogMessage(this, "Switch in prop {0} prop id {1} will consume {2} while active at a rate of {3}", internalProp.propName,
                            internalProp.propID, consumeWhileActiveName, consumeWhileActiveAmount);
                    }
                }
            }

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            comp.UpdateDataRefreshRate(refreshRate);

            if (!string.IsNullOrEmpty(masterVariableName))
            {
                string[] range = masterVariableRange.Split(',');
                if (range.Length == 2)
                {
                    masterVariable = new VariableOrNumberRange(masterVariableName, range[0], range[1]);
                }
                else
                {
                    masterVariable = null;
                }
            }

            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("PROP");
            for (int i = 0; i < nodes.Length; ++i)
            {
                if (nodes[i].GetValue("name") == internalProp.propName)
                {
                    ConfigNode[] pluginNodes = nodes[i].GetNodes("MODULE")[moduleID].GetNodes("ACTION");
                    for (int j = 0; j < pluginNodes.Length; ++j)
                    {
                        try
                        {
                            IJSIAction newAction = AddAction(pluginNodes[j]);
                            action.Add(newAction);
                            if (newAction.IsMasterAction && masterActionIndex == -1)
                            {
                                masterActionIndex = action.Count - 1;
                            }
                        }
                        catch (Exception e)
                        {
                            JUtil.LogErrorMessage(this, "Failed to create JSIAction: {0}", e);
                        }
                    }
                }
            }

            if (action.Count == 0)
            {
                JUtil.LogErrorMessage(this, "No actions were created for JSIAction in prop {0}", internalProp.propName);
                return;
            }

            if (masterActionIndex == -1)
            {
                masterActionIndex = 0;
            }
            JUtil.LogMessage(this, "Selected action {0} as the master action", masterActionIndex);

            startupComplete = true;
        }

        /// <summary>
        /// Tear everything down.
        /// </summary>
        public void OnDestroy()
        {
            for (int i = 0; i < action.Count; ++i)
            {
                action[i].TearDown();
            }

            // TODO: Do any of these need explicitly released?
            switchAudio = null;
            loopingAudio = null;
            anim = null;
        }

        /// <summary>
        /// Callback for button presses.
        /// </summary>
        public void Click()
        {
            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            bool switchEnabled = true;
            if (!forcedShutdown)
            {
                if (masterVariable != null)
                {
                    switchEnabled = masterVariable.IsInRange(comp);
                }
            }
            if (!switchEnabled)
            {
                // If the master switch is 'off' and we're not here because
                // of a forced shutdown, don't allow this switch to work.
                // early return
                return;
            }

            for (int i = 0; i < action.Count; ++i)
            {
                action[i].Click(vessel, null, comp);
            }
        }

        /// <summary>
        /// Update.  Mainly for objects that consume resources.
        /// </summary>
        public override void OnUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            if (!startupComplete)
            {
                return;
            }

            if (!JUtil.IsActiveVessel(vessel))
            {
                if (loopingAudio != null && currentState == true)
                {
                    loopingAudio.audio.volume = 0.0f;
                }
                return;
            }
            else if (loopingAudio != null && currentState == true && loopingAudio.Active)
            {
                loopingAudio.audio.volume = loopingSoundVolume * GameSettings.SHIP_VOLUME;
            }

            if (consumingWhileActive && currentState && !forcedShutdown)
            {
                float requesting = (consumeWhileActiveAmount * TimeWarp.deltaTime);
                float extracted = part.RequestResource(consumeWhileActiveName, requesting);
                if (Mathf.Abs(extracted - requesting) > Mathf.Abs(requesting * 0.5f))
                {
                    // We don't have enough of the resource or can't produce more negative resource, so we should shut down...
                    forcedShutdown = true;
                    //JUtil.LogMessage(this, "Could not consume {0}, asked for {1}, got {2} shutting switch down.", consumeWhileActiveName, requesting, extracted);
                }
            }

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            bool newState = action[masterActionIndex].CurrentState(vessel, comp);

            if (masterVariable != null)
            {
                if (!masterVariable.IsInRange(comp))
                {
                    forcedShutdown = true;
                }
            }

            if (forcedShutdown)
            {
                if (currentState == true)
                {
                    for (int i = 0; i < action.Count; ++i)
                    {
                        if (action[i].CurrentState(vessel, comp))
                        {
                            action[i].Click(false, vessel, comp);
                        }
                    }
                }

                newState = false;
                forcedShutdown = false;
            }

            //for (int i = 0; i < action.Count; ++i)
            //{
            //    action[i].Update();
            //}

            if (newState != currentState)
            {
                // If we're consuming resources on toggle, do that now.
                if ((consumingOnToggleUp && newState) || (consumingOnToggleDown && !newState))
                {
                    float extracted = part.RequestResource(consumeOnToggleName, consumeOnToggleAmount);
                    if (Math.Abs(extracted - consumeOnToggleAmount) > Math.Abs(consumeOnToggleAmount / 2))
                    {
                        // We don't have enough of the resource, so we force a shutdown on the next loop.
                        // This ensures the animations will play at least once.
                        forcedShutdown = true;
                    }
                }

                if (switchAudio != null && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
                    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
                {
                    switchAudio.audio.Play();
                }

                if (loopingAudio != null && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
                    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
                {
                    if (newState)
                    {
                        loopingAudio.audio.Play();
                    }
                    else
                    {
                        loopingAudio.audio.Stop();
                    }
                }

                if (anim != null)
                {
                    if (newState ^ reverse)
                    {
                        anim[animationName].normalizedTime = 0.0f;
                        anim[animationName].speed = customSpeed;
                        anim.Play(animationName);
                    }
                    else
                    {
                        anim[animationName].normalizedTime = 1.0f;
                        anim[animationName].speed = -customSpeed;
                        anim.Play(animationName);
                    }
                }
                //else if (colorShiftMaterial != null)
                //{
                //    colorShiftMaterial.SetColor(colorName, (newState ^ reverse ? enabledColorValue : disabledColorValue));
                //}
                currentState = newState;
            }

        }

        private IJSIAction AddAction(ConfigNode node)
        {
            if (!node.HasValue("name"))
            {
                throw new Exception("JSIAction ACTION node is missing a name");
            }

            string name = node.GetValue("name");
            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);

            if (JSIActionGroupSwitch.groupList.ContainsKey(name))
            {
                return new JSIKSPAction(name, node);
            }
            else if (name == "intlight")
            {
                return new JSIIntLight(node, comp, internalModel);
            }
            else if (name == "dummy")
            {
                return new JSIDummy(node, comp);
            }
            else if (name == "plugin")
            {
                return new JSIPlugin(node);
            }

            throw new NotImplementedException("No IJSIAction for " + name);
        }

        /// <summary>
        /// Interface for actions that a JSISwitch can trigger
        /// </summary>
        abstract internal class IJSIAction
        {
            private readonly bool masterAction;
            internal bool IsMasterAction
            {
                get { return masterAction; }
            }

            internal readonly VariableOrNumberRange enableVariable;

            internal IJSIAction(ConfigNode node)
            {
                if (!node.HasValue("masterAction") || !bool.TryParse(node.GetValue("masterAction"), out masterAction))
                {
                    masterAction = false;
                }
                enableVariable = null;
            }

            /// <summary>
            /// Do something - the switch was clicked.
            /// </summary>
            /// <param name="newState">State that switch is transitioning to.</param>
            /// <param name="vessel">active vessel</param>
            /// <param name="comp">active vessel's vessel computer</param>
            abstract internal void Click(bool newState, Vessel vessel, RPMVesselComputer comp);
            /// <summary>
            /// What is the current state of the switch?
            /// </summary>
            /// <param name="vessel">activeVessel</param>
            /// <param name="comp">comp</param>
            /// <returns>Current state</returns>
            abstract internal bool CurrentState(Vessel vessel, RPMVesselComputer comp);
            /// <summary>
            /// We're done with the switch.  Release resources.
            /// </summary>
            abstract internal void TearDown();
        }

        /// <summary>
        /// Class that toggles a persistent variable
        /// </summary>
        private class JSIDummy : IJSIAction
        {
            private readonly string perPodPersistenceName;
            private bool currentState;

            internal JSIDummy(ConfigNode node, RPMVesselComputer comp)
                : base(node)
            {
                if (!node.HasValue("perPodPersistenceName"))
                {
                    throw new Exception("Trying to create Dummy JSIAction with no perPodPersistenceName");
                }

                perPodPersistenceName = node.GetValue("perPodPersistenceName");
                if (string.IsNullOrEmpty(perPodPersistenceName))
                {
                    throw new Exception("Invalid perPodPersistenceName supplied for Dummy JSIAction");
                }

                if (node.HasValue("initialState"))
                {
                    if (!bool.TryParse(node.GetValue("initialState"), out currentState))
                    {
                        currentState = false;
                    }
                }
                else
                {
                    currentState = false;
                }

                currentState = comp.GetPersistentVariable(perPodPersistenceName, currentState);
                comp.SetPersistentVariable(perPodPersistenceName, currentState);
            }

            override internal void Click(bool newState, Vessel vessel, RPMVesselComputer comp)
            {
                currentState = !currentState;
                comp.SetPersistentVariable(perPodPersistenceName, currentState);
            }

            override internal bool CurrentState(Vessel vessel, RPMVesselComputer comp)
            {
                currentState = comp.GetPersistentVariable(perPodPersistenceName, currentState);
                if (enableVariable != null)
                {
                    currentState = currentState & enableVariable.IsInRange(comp);
                }
                return currentState;
            }

            override internal void TearDown()
            {
                // No-op
            }
        }

        /// <summary>
        /// Class that manages stock KSP actions
        /// </summary>
        private class JSIKSPAction : IJSIAction
        {
            private readonly KSPActionGroup kspAction;

            internal JSIKSPAction(string name, ConfigNode node)
                : base(node)
            {
                kspAction = JSIActionGroupSwitch.groupList[name];
            }

            override internal void Click(bool newState, Vessel vessel, RPMVesselComputer comp)
            {
                if (kspAction == KSPActionGroup.Stage)
                {
                    if (InputLockManager.IsUnlocked(ControlTypes.STAGING))
                    {
                        vessel.ActionGroups.ToggleGroup(kspAction);
                        StageManager.ActivateNextStage();
                    }
                }
                else
                {
                    vessel.ActionGroups.ToggleGroup(kspAction);
                }
            }

            override internal bool CurrentState(Vessel vessel, RPMVesselComputer comp)
            {
                return vessel.ActionGroups[kspAction];
            }

            override internal void TearDown()
            {
                // No-op
            }
        }

        /// <summary>
        /// IJSIAction for internal lights
        /// </summary>
        private class JSIIntLight : IJSIAction
        {
            private readonly string persistentVarName;
            private Light[] lightObjects;
            private readonly bool needsElectricCharge;
            private bool currentState;

            internal JSIIntLight(ConfigNode node, RPMVesselComputer comp, InternalModel internalModel)
                : base(node)
            {
                if (!node.HasValue("internalLightName"))
                {
                    throw new Exception("Unable to configure IJSIAction intlight without 'internalLightName'");
                }

                string internalLightName = node.GetValue("internalLightName");
                if (node.HasValue("persistentVarName"))
                {
                    persistentVarName = node.GetValue("persistentVarName");
                    if (string.IsNullOrEmpty(persistentVarName))
                    {
                        throw new Exception("Invalid persistentVarName supplied for internal light " + internalLightName);
                    }
                }
                else
                {
                    persistentVarName = internalLightName;
                }

                Light[] availableLights = internalModel.FindModelComponents<Light>();
                if (availableLights != null && availableLights.Length > 0)
                {
                    List<Light> lights = new List<Light>(availableLights);
                    for (int i = lights.Count - 1; i >= 0; --i)
                    {
                        if (lights[i].name != internalLightName)
                        {
                            lights.RemoveAt(i);
                        }
                    }
                    if (lights.Count > 0)
                    {
                        lightObjects = lights.ToArray();
                    }
                }

                if (lightObjects == null)
                {
                    throw new Exception("No lights with name " + internalLightName + " were found for JSISwitch intlight");
                }

                if (node.HasValue("needsElectricCharge"))
                {
                    bool.TryParse(node.GetValue("needsElectricCharge"), out needsElectricCharge);
                }

                bool initialState = false;
                if (node.HasValue("initialState"))
                {
                    bool.TryParse(node.GetValue("initialState"), out initialState);
                }

                comp.GetPersistentVariable(persistentVarName, initialState);
                currentState = initialState;
                comp.SetPersistentVariable(persistentVarName, currentState);

                SetInternalLights(currentState);
            }

            override internal void Click(bool newState, Vessel vessel, RPMVesselComputer comp)
            {
                currentState = !currentState;

                comp.SetPersistentVariable(persistentVarName, currentState);

                SetInternalLights(currentState);
            }

            override internal bool CurrentState(Vessel vessel, RPMVesselComputer comp)
            {
                currentState = comp.GetPersistentVariable(persistentVarName, currentState);
                return currentState;
            }

            override internal void TearDown()
            {
                lightObjects = null;
            }

            private void SetInternalLights(bool value)
            {
                for (int i = 0; i < lightObjects.Length; ++i)
                {
                    lightObjects[i].enabled = value;
                }
            }
        }

        private class JSIPlugin : IJSIAction
        {
            /*		PLUGINACTION
                    {
                        name = JSIMechJeb
                        actionMethod = ButtonEnableLandingPrediction
                        stateMethod = ButtonEnableLandingPredictionState
                        stateVariable = bob
                    }
            */
            internal JSIPlugin(ConfigNode node)
                : base(node)
            {

            }

            override internal void Click(bool newState, Vessel vessel, RPMVesselComputer comp)
            {
            }

            override internal bool CurrentState(Vessel vessel, RPMVesselComputer comp)
            {
                return false;
            }

            override internal void TearDown()
            {

            }
        }
    }
}
