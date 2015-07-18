using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Globalization;

namespace JSI
{
    public class JSIActionGroupSwitch : InternalModule
    {
        [KSPField]
        public string animationName = string.Empty;
        [KSPField]
        public bool animateExterior;
        [KSPField]
        public string switchTransform = string.Empty;
        [KSPField]
        public string actionName = "lights";
        [KSPField]
        public string perPodPersistenceName = string.Empty;
        [KSPField]
        public string perPodMasterSwitchName = string.Empty;
        [KSPField]
        public string masterVariableName = string.Empty;
        private VariableOrNumber masterVariable = null;
        [KSPField]
        public string masterVariableRange = string.Empty;
        private VariableOrNumber[] masterRange = new VariableOrNumber[2];
        [KSPField]
        public bool reverse;
        [KSPField]
        public float customSpeed = 1f;
        [KSPField]
        public string internalLightName;
        [KSPField]
        public string needsElectricCharge;
        private bool needsElectricChargeValue;
        [KSPField]
        public string switchSound = "Squad/Sounds/sound_click_flick";
        [KSPField]
        public float switchSoundVolume = 0.5f;
        [KSPField]
        public string coloredObject = string.Empty;
        [KSPField]
        public string colorName = "_EmissiveColor";
        [KSPField]
        public string consumeOnToggle = string.Empty;
        [KSPField]
        public string consumeWhileActive = string.Empty;
        [KSPField]
        public string disabledColor = string.Empty;
        private Color disabledColorValue;
        [KSPField]
        public string enabledColor = string.Empty;
        private Color enabledColorValue;
        [KSPField]
        public bool initialState = false;
        [KSPField]
        public int switchGroupIdentifier = -1;
        [KSPField]
        public int refreshRate = 60;
        // Neater.
        private static readonly Dictionary<string, KSPActionGroup> groupList = new Dictionary<string, KSPActionGroup> { 
			{ "gear",KSPActionGroup.Gear },
			{ "brakes",KSPActionGroup.Brakes },
			{ "lights",KSPActionGroup.Light },
			{ "rcs",KSPActionGroup.RCS },
			{ "sas",KSPActionGroup.SAS },
			{ "abort",KSPActionGroup.Abort },
			{ "stage",KSPActionGroup.Stage },
			{ "custom01",KSPActionGroup.Custom01 },
			{ "custom02",KSPActionGroup.Custom02 },
			{ "custom03",KSPActionGroup.Custom03 },
			{ "custom04",KSPActionGroup.Custom04 },
			{ "custom05",KSPActionGroup.Custom05 },
			{ "custom06",KSPActionGroup.Custom06 },
			{ "custom07",KSPActionGroup.Custom07 },
			{ "custom08",KSPActionGroup.Custom08 },
			{ "custom09",KSPActionGroup.Custom09 },
			{ "custom10",KSPActionGroup.Custom10 }
		};
        private readonly Dictionary<string, bool> customGroupList = new Dictionary<string, bool> {
			{ "intlight",false },
			{ "dummy",false },
			{ "plugin",false },
		};
        private Animation anim;
        private bool currentState;
        private bool isCustomAction;
        private string persistentVarName;
        private Light[] lightObjects;
        private FXGroup audioOutput;
        private int lightCheckCountdown;
        private RasterPropMonitorComputer comp;
        private bool startupComplete;
        private Renderer colorShiftRenderer;
        private Func<bool> stateHandler;
        private Action<bool> actionHandler;
        private bool isPluginAction;

        // Consume-on-toggle and consume-while-active
        private bool consumingOnToggleUp, consumingOnToggleDown;
        private string consumeOnToggleName = string.Empty;
        private float consumeOnToggleAmount;
        private bool consumingWhileActive;
        private string consumeWhileActiveName = string.Empty;
        private float consumeWhileActiveAmount;
        private bool forcedShutdown;

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;

            try
            {

                if (!groupList.ContainsKey(actionName) && !customGroupList.ContainsKey(actionName))
                {
                    JUtil.LogErrorMessage(this, "Action \"{0}\" is not supported.", actionName);
                    return;
                }

                // Parse the needs-electric-charge here.
                if (!string.IsNullOrEmpty(needsElectricCharge))
                {
                    switch (needsElectricCharge.ToLowerInvariant().Trim())
                    {
                        case "true":
                        case "yes":
                        case "1":
                            needsElectricChargeValue = true;
                            break;
                        case "false":
                        case "no":
                        case "0":
                            needsElectricChargeValue = false;
                            break;
                    }
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

                if (groupList.ContainsKey(actionName))
                {
                    currentState = vessel.ActionGroups[groupList[actionName]];
                    // action group switches may not belong to a radio group
                    switchGroupIdentifier = -1;
                }
                else
                {
                    isCustomAction = true;
                    switch (actionName)
                    {
                        case "intlight":
                            persistentVarName = internalLightName;
                            lightObjects = internalModel.FindModelComponents<Light>();
                            needsElectricChargeValue |= string.IsNullOrEmpty(needsElectricCharge) || needsElectricChargeValue;
                            break;
                        case "plugin":
                            persistentVarName = string.Empty;
                            comp = RasterPropMonitorComputer.Instantiate(internalProp);
                            comp.UpdateRefreshRates(refreshRate, refreshRate);

                            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("PROP"))
                            {
                                if (node.GetValue("name") == internalProp.propName)
                                {
                                    foreach (ConfigNode pluginConfig in node.GetNodes("MODULE")[moduleID].GetNodes("PLUGINACTION"))
                                    {
                                        if (pluginConfig.HasValue("name") && pluginConfig.HasValue("actionMethod"))
                                        {
                                            string action = pluginConfig.GetValue("name").Trim() + ":" + pluginConfig.GetValue("actionMethod").Trim();
                                            actionHandler = (Action<bool>)comp.GetMethod(action, internalProp, typeof(Action<bool>));

                                            if (actionHandler == null)
                                            {
                                                JUtil.LogErrorMessage(this, "Failed to instantiate action handler {0}", pluginConfig.GetValue("name"));
                                            }
                                            else
                                            {
                                                if (pluginConfig.HasValue("stateMethod"))
                                                {
                                                    string state = pluginConfig.GetValue("name").Trim() + ":" + pluginConfig.GetValue("stateMethod").Trim();
                                                    stateHandler = (Func<bool>)comp.GetMethod(state, internalProp, typeof(Func<bool>));
                                                }
                                                isPluginAction = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            if (actionHandler == null)
                            {
                                actionName = "dummy";
                                JUtil.LogMessage(this, "Plugin handlers did not start, reverting to dummy mode.");
                            }
                            break;
                        default:
                            persistentVarName = "switch" + internalProp.propID + "_" + moduleID;
                            break;
                    }
                    if (!string.IsNullOrEmpty(perPodPersistenceName))
                    {
                        persistentVarName = perPodPersistenceName;
                    }
                    else
                    {
                        // If there's no persistence name, there's no valid group id for this switch
                        switchGroupIdentifier = -1;
                    }
                }

                if (needsElectricChargeValue || !string.IsNullOrEmpty(persistentVarName) || !string.IsNullOrEmpty(perPodMasterSwitchName) || !string.IsNullOrEmpty(masterVariableName))
                {
                    if (comp == null)
                    {
                        comp = RasterPropMonitorComputer.Instantiate(internalProp);
                        comp.UpdateRefreshRates(refreshRate, refreshRate);
                    }

                    if (!string.IsNullOrEmpty(masterVariableName))
                    {
                        masterVariable = new VariableOrNumber(masterVariableName, this);
                        string[] range = masterVariableRange.Split(',');
                        if(range.Length == 2)
                        {
                            masterRange[0] = new VariableOrNumber(range[0], this);
                            masterRange[1] = new VariableOrNumber(range[1], this);
                        }
                        else
                        {
                            masterVariable = null;
                        }
                    }
                }

                // set up the toggle switch
                if (!string.IsNullOrEmpty(switchTransform))
                {
                    SmarterButton.CreateButton(internalProp, switchTransform, Click);
                }

                if (isCustomAction)
                {
                    if (isPluginAction && stateHandler != null)
                    {
                        currentState = stateHandler();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(persistentVarName))
                        {
                            if (switchGroupIdentifier >= 0)
                            {
                                int activeSwitch = comp.Persistence.GetVar(persistentVarName, 0);

                                currentState = customGroupList[actionName] = (switchGroupIdentifier == activeSwitch);
                            }
                            else
                            {
                                currentState = customGroupList[actionName] = comp.Persistence.GetBool(persistentVarName, initialState);
                            }

                            if (actionName == "intlight")
                            {
                                // We have to restore lighting after reading the
                                // persistent variable.
                                SetInternalLights(customGroupList[actionName]);
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(persistentVarName) && !comp.Persistence.HasVar(persistentVarName))
                {
                    if (switchGroupIdentifier >= 0)
                    {
                        if (currentState)
                        {
                            comp.Persistence.SetVar(persistentVarName, switchGroupIdentifier);
                        }
                    }
                    else
                    {
                        comp.Persistence.SetVar(persistentVarName, currentState);
                    }
                }

                if (!string.IsNullOrEmpty(animationName))
                {
                    // Set up the animation
                    Animation[] animators = animateExterior ? part.FindModelAnimators(animationName) : internalProp.FindModelAnimators(animationName);
                    if (animators.Length > 0)
                    {
                        anim = animators[0];
                    }
                    else
                    {
                        JUtil.LogErrorMessage(this, "Could not find animation \"{0}\" on {2} \"{1}\"",
                            animationName, animateExterior ? part.name : internalProp.name, animateExterior ? "part" : "prop");
                        return;
                    }
                    anim[animationName].wrapMode = WrapMode.Once;

                    if (currentState ^ reverse)
                    {
                        anim[animationName].speed = float.MaxValue;
                        anim[animationName].normalizedTime = 0;

                    }
                    else
                    {
                        anim[animationName].speed = float.MinValue;
                        anim[animationName].normalizedTime = 1;
                    }
                    anim.Play(animationName);
                }
                else if (!string.IsNullOrEmpty(coloredObject))
                {
                    // Set up the color shift.
                    colorShiftRenderer = internalProp.FindModelComponent<Renderer>(coloredObject);
                    disabledColorValue = ConfigNode.ParseColor32(disabledColor);
                    enabledColorValue = ConfigNode.ParseColor32(enabledColor);
                    colorShiftRenderer.material.SetColor(colorName, (currentState ^ reverse ? enabledColorValue : disabledColorValue));
                }
                else
                {
                    JUtil.LogMessage(this, "Warning, neither color nor animation are defined.");
                }

                audioOutput = JUtil.SetupIVASound(internalProp, switchSound, switchSoundVolume, false);

                startupComplete = true;
            }
            catch
            {
                JUtil.AnnoyUser(this);
                enabled = false;
                throw;
            }
        }

        private void SetInternalLights(bool value)
        {
            foreach (Light lightobject in lightObjects)
            {
                // I probably shouldn't filter them every time, but I am getting
                // serously confused by this hierarchy.
                if (lightobject.name == internalLightName)
                {
                    lightobject.enabled = value;
                }
            }
        }

        public void Click()
        {
            bool switchEnabled = true;
            if (!forcedShutdown)
            {
                if (!string.IsNullOrEmpty(perPodMasterSwitchName))
                {
                    switchEnabled = comp.Persistence.GetBool(perPodMasterSwitchName, false);
                }
                if (masterVariable != null)
                {
                    float value, range1, range2;
                    if (masterVariable.Get(out value, comp) && masterRange[0].Get(out range1, comp) && masterRange[1].Get(out range2, comp))
                    {
                        float minR = Mathf.Min(range1, range2);
                        float maxR = Mathf.Max(range1, range2);
                        if (value < minR || value > maxR)
                        {
                            // If the master variable is out of spec, disable the switch.
                            switchEnabled = false;
                        }
                    }
                }
            }
            if (!switchEnabled)
            {
                // If the master switch is 'off' and we're not here because
                // of a forced shutdown, don't allow this switch to work.
                // early return
                return;
            }

            if (isCustomAction)
            {
                if (switchGroupIdentifier >= 0)
                {
                    if (!forcedShutdown && !customGroupList[actionName])
                    {
                        customGroupList[actionName] = true;
                        if (!string.IsNullOrEmpty(persistentVarName))
                        {
                            comp.Persistence.SetVar(persistentVarName, switchGroupIdentifier);
                        }
                    }
                    // else: can't turn off a radio group switch.
                }
                else
                {
                    customGroupList[actionName] = !customGroupList[actionName];
                    if (!string.IsNullOrEmpty(persistentVarName))
                    {
                        comp.Persistence.SetVar(persistentVarName, customGroupList[actionName]);
                    }
                }
            }
            else
            {
                vessel.ActionGroups.ToggleGroup(groupList[actionName]);
            }
            // Now we do extra things that with regular actions can't happen.
            switch (actionName)
            {
                case "intlight":
                    SetInternalLights(customGroupList[actionName]);
                    break;
                case "plugin":
                    actionHandler((stateHandler != null) ? !stateHandler() : customGroupList[actionName]);
                    break;
                case "stage":
                    if (InputLockManager.IsUnlocked(ControlTypes.STAGING))
                    {
                        Staging.ActivateNextStage();
                    }
                    break;
            }
        }

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

            if (consumingWhileActive && currentState && !forcedShutdown)
            {
                float requesting = (consumeWhileActiveAmount * TimeWarp.deltaTime);
                float extracted = part.RequestResource(consumeWhileActiveName, requesting);
                if (Math.Abs(extracted - requesting) > Math.Abs(requesting / 2))
                {
                    // We don't have enough of the resource or can't produce more negative resource, so we should shut down...
                    forcedShutdown = true;
                    JUtil.LogMessage(this, "Could not consume {0}, asked for {1}, got {2} shutting switch down.", consumeWhileActiveName, requesting, extracted);
                }
            }

            if (!JUtil.IsActiveVessel(vessel))
            {
                return;
            }

            // Bizarre, but looks like I need to animate things offscreen if I want them in the right condition when camera comes back.
            // So there's no check for internal cameras.

            bool newState;
            if (isPluginAction && stateHandler != null)
            {
                newState = stateHandler();
            }
            else if (isCustomAction)
            {
                if (string.IsNullOrEmpty(switchTransform) && !string.IsNullOrEmpty(perPodPersistenceName))
                {
                    if (switchGroupIdentifier >= 0)
                    {
                        int activeGroupId = comp.Persistence.GetVar(persistentVarName, 0);
                        newState = (switchGroupIdentifier == activeGroupId);
                        customGroupList[actionName] = newState;
                    }
                    else
                    {
                        // If the switch transform is not given, and the global comp.Persistence value is, this means this is a slave module.
                        newState = comp.Persistence.GetBool(persistentVarName, false);
                    }
                }
                else
                {
                    // Otherwise it's a master module. But it still might have to follow the clicks on other copies of the same prop...
                    if (!string.IsNullOrEmpty(perPodPersistenceName))
                    {
                        if (switchGroupIdentifier >= 0)
                        {
                            int activeGroupId = comp.Persistence.GetVar(persistentVarName, 0);
                            newState = (switchGroupIdentifier == activeGroupId);
                            customGroupList[actionName] = newState;
                        }
                        else
                        {
                            newState = comp.Persistence.GetBool(persistentVarName, customGroupList[actionName]);
                        }
                    }
                    else
                    {
                        newState = customGroupList[actionName];
                    }
                }
            }
            else
            {
                newState = vessel.ActionGroups[groupList[actionName]];
            }

            // If needsElectricCharge is true and there is no charge, the state value is overridden to false and the click action is reexecuted.
            if (needsElectricChargeValue)
            {
                lightCheckCountdown--;
                if (lightCheckCountdown <= 0)
                {
                    lightCheckCountdown = refreshRate;
                    forcedShutdown |= currentState && comp.ProcessVariable("SYSR_ELECTRICCHARGE", -1).MassageToDouble() < 0.01d;
                }
            }

            if (!string.IsNullOrEmpty(perPodMasterSwitchName))
            {
                bool switchEnabled = comp.Persistence.GetBool(perPodMasterSwitchName, false);
                if (!switchEnabled)
                {
                    // If the master switch is 'off', this switch needs to turn off
                    newState = false;
                    forcedShutdown = true;
                }
            }

            if (masterVariable != null)
            {
                float value, range1, range2;
                if (masterVariable.Get(out value, comp) && masterRange[0].Get(out range1, comp) && masterRange[1].Get(out range2, comp))
                {
                    float minR = Mathf.Min(range1, range2);
                    float maxR = Mathf.Max(range1, range2);
                    if(value < minR || value > maxR)
                    {
                        // If the master variable is out of spec, disable the switch.
                        newState = false;
                        forcedShutdown = true;
                    }
                }
            }

            if (forcedShutdown)
            {
                if (currentState)
                {
                    Click();
                }
                newState = false;
                forcedShutdown = false;
            }

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
                if (audioOutput != null && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
                    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
                {
                    audioOutput.audio.Play();
                }
                if (anim != null)
                {
                    if (newState ^ reverse)
                    {
                        anim[animationName].normalizedTime = 0;
                        anim[animationName].speed = 1f * customSpeed;
                        anim.Play(animationName);
                    }
                    else
                    {
                        anim[animationName].normalizedTime = 1;
                        anim[animationName].speed = -1f * customSpeed;
                        anim.Play(animationName);
                    }
                }
                else if (colorShiftRenderer != null)
                {
                    colorShiftRenderer.material.SetColor(colorName, (newState ^ reverse ? enabledColorValue : disabledColorValue));
                }
                currentState = newState;
            }
        }

    }
}

