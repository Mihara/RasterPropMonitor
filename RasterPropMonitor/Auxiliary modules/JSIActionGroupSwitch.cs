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
using System.Reflection;
using UnityEngine;

namespace JSI
{
    internal class JSIActionGroupSwitch : InternalModule
    {
        [KSPField]
        public string animationName = string.Empty;
        [KSPField]
        public bool animateExterior = false;
        [KSPField]
        public string switchTransform = string.Empty;
        [KSPField]
        public string actionName = "lights";
        [KSPField]
        public string perPodPersistenceName = string.Empty;
        private bool perPodPersistenceValid = false;
        [KSPField]
        public string perPodMasterSwitchName = string.Empty;
        private bool perPodMasterSwitchValid = false;
        [KSPField]
        public string masterVariableName = string.Empty;
        private VariableOrNumberRange masterVariable = null;
        [KSPField]
        public string masterVariableRange = string.Empty;
        [KSPField]
        public bool momentarySwitch = false;
        [KSPField]
        public bool reverse = false;
        [KSPField]
        public float customSpeed = 1f;
        [KSPField]
        public string internalLightName = string.Empty;
        [KSPField]
        public string needsElectricCharge = string.Empty;
        private bool needsElectricChargeValue;
        [KSPField]
        public string resourceName = "SYSR_ELECTRICCHARGE";
        private bool resourceDepleted = false; // Managed by rpmComp callback
        private Action<bool> del;

        [KSPField]
        public string switchSound = "Squad/Sounds/sound_click_flick";
        [KSPField]
        public float switchSoundVolume = 0.5f;
        [KSPField]
        public string loopingSound = string.Empty;
        [KSPField]
        public float loopingSoundVolume = 0.0f;
        [KSPField]
        public string coloredObject = string.Empty;
        [KSPField]
        public string colorName = "_EmissiveColor";
        private int colorNameId = -1;
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
        internal static readonly Dictionary<string, KSPActionGroup> groupList = new Dictionary<string, KSPActionGroup> { 
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
        internal enum CustomActions
        {
            None,
            IntLight,
            Dummy,
            Plugin,
            Stage,
            Transfer,
            TransferToPersistent,
            TransferFromPersistent,
            TransferFromVariable
        };
        private bool customGroupState = false;
        internal static readonly Dictionary<string, CustomActions> customGroupList = new Dictionary<string, CustomActions> {
            { "---none---", CustomActions.None},
            { "intlight", CustomActions.IntLight },
            { "dummy",CustomActions.Dummy },
            { "plugin",CustomActions.Plugin },
            { "transfer", CustomActions.Transfer},
            { "stage",CustomActions.Stage }
        };
        private KSPActionGroup kspAction = KSPActionGroup.None;
        private CustomActions customAction = CustomActions.None;
        private Animation anim;
        private bool currentState;
        private bool isCustomAction;
        private string persistentVarName;
        private bool persistentVarValid = false;
        private Light[] lightObjects;
        private FXGroup audioOutput;
        private FXGroup loopingOutput;
        private bool startupComplete;
        private Material colorShiftMaterial;
        private VariableOrNumber stateVariable;
        private Action<bool> actionHandler;
        private bool isPluginAction;
        private RasterPropMonitorComputer rpmComp;

        private VariableOrNumber transferGetter;
        private Action<double> transferSetter;
        private string transferPersistentName;

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
            {
                return;
            }

            try
            {
                rpmComp = RasterPropMonitorComputer.Instantiate(internalProp, true);

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
                    kspAction = groupList[actionName];
                    currentState = vessel.ActionGroups[kspAction];
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
                            if (!string.IsNullOrEmpty(internalLightName))
                            {
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
                                        needsElectricChargeValue |= string.IsNullOrEmpty(needsElectricCharge) || needsElectricChargeValue;
                                    }
                                    else
                                    {
                                        actionName = "dummy";
                                    }
                                }
                            }
                            else
                            {
                                actionName = "dummy";
                            }
                            break;
                        case "plugin":
                            persistentVarName = string.Empty;
                            rpmComp.UpdateDataRefreshRate(refreshRate);

                            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("PROP"))
                            {
                                if (node.GetValue("name") == internalProp.propName)
                                {
                                    foreach (ConfigNode pluginConfig in node.GetNodes("MODULE")[moduleID].GetNodes("PLUGINACTION"))
                                    {
                                        if (pluginConfig.HasValue("name") && pluginConfig.HasValue("actionMethod"))
                                        {
                                            string action = pluginConfig.GetValue("name").Trim() + ":" + pluginConfig.GetValue("actionMethod").Trim();
                                            actionHandler = (Action<bool>)rpmComp.GetMethod(action, internalProp, typeof(Action<bool>));

                                            if (actionHandler == null)
                                            {
                                                JUtil.LogErrorMessage(this, "Failed to instantiate action handler {0}", action);
                                            }
                                            else
                                            {
                                                if (pluginConfig.HasValue("stateMethod"))
                                                {
                                                    string state = pluginConfig.GetValue("name").Trim() + ":" + pluginConfig.GetValue("stateMethod").Trim();
                                                    stateVariable = rpmComp.InstantiateVariableOrNumber("PLUGIN_" + state);
                                                }
                                                else if (pluginConfig.HasValue("stateVariable"))
                                                {
                                                    stateVariable = rpmComp.InstantiateVariableOrNumber(pluginConfig.GetValue("stateVariable").Trim());
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
                        case "transfer":
                            persistentVarName = string.Empty;
                            rpmComp.UpdateDataRefreshRate(refreshRate);

                            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("PROP"))
                            {
                                if (node.GetValue("name") == internalProp.propName)
                                {
                                    foreach (ConfigNode pluginConfig in node.GetNodes("MODULE")[moduleID].GetNodes("TRANSFERACTION"))
                                    {
                                        if (pluginConfig.HasValue("name") || pluginConfig.HasValue("getVariable"))
                                        {
                                            if (pluginConfig.HasValue("stateMethod"))
                                            {
                                                string state = pluginConfig.GetValue("name").Trim() + ":" + pluginConfig.GetValue("stateMethod").Trim();
                                                stateVariable = rpmComp.InstantiateVariableOrNumber("PLUGIN_" + state);
                                            }
                                            else if (pluginConfig.HasValue("stateVariable"))
                                            {
                                                stateVariable = rpmComp.InstantiateVariableOrNumber(pluginConfig.GetValue("stateVariable").Trim());
                                            }
                                            if (pluginConfig.HasValue("setMethod"))
                                            {
                                                string action = pluginConfig.GetValue("name").Trim() + ":" + pluginConfig.GetValue("setMethod").Trim();
                                                transferSetter = (Action<double>)rpmComp.GetMethod(action, internalProp, typeof(Action<double>));

                                                if (transferSetter == null)
                                                {
                                                    JUtil.LogErrorMessage(this, "Failed to instantiate transfer handler {0}", pluginConfig.GetValue("name"));
                                                }
                                                else if (pluginConfig.HasValue("perPodPersistenceName"))
                                                {
                                                    transferPersistentName = pluginConfig.GetValue("perPodPersistenceName").Trim();
                                                    actionName = "transferFromPersistent";
                                                    customAction = CustomActions.TransferFromPersistent;
                                                }
                                                else if (pluginConfig.HasValue("getVariable"))
                                                {
                                                    transferGetter = rpmComp.InstantiateVariableOrNumber(pluginConfig.GetValue("getVariable").Trim());
                                                    actionName = "transferFromVariable";
                                                    customAction = CustomActions.TransferFromVariable;
                                                }
                                                else
                                                {
                                                    JUtil.LogErrorMessage(this, "Unable to configure transfer setter method in {0} - no perPodPersistenceName or getVariable", internalProp.name);
                                                    transferSetter = null;
                                                    //JUtil.LogMessage(this, "Got setter {0}", action);
                                                }
                                            }
                                            else if (pluginConfig.HasValue("getMethod"))
                                            {
                                                if (pluginConfig.HasValue("perPodPersistenceName"))
                                                {
                                                    string action = pluginConfig.GetValue("name").Trim() + ":" + pluginConfig.GetValue("getMethod").Trim();
                                                    var getter = (Func<double>)rpmComp.GetMethod(action, internalProp, typeof(Func<double>));

                                                    if (getter == null)
                                                    {
                                                        JUtil.LogErrorMessage(this, "Failed to instantiate transfer handler {0} in {1}", pluginConfig.GetValue("name"), internalProp.name);
                                                    }
                                                    else
                                                    {
                                                        transferGetter = rpmComp.InstantiateVariableOrNumber("PLUGIN_" + action);
                                                        transferPersistentName = pluginConfig.GetValue("perPodPersistenceName").Trim();
                                                        actionName = "transferToPersistent";
                                                        customAction = CustomActions.TransferToPersistent;
                                                        //JUtil.LogMessage(this, "Got getter {0}", action);
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    JUtil.LogErrorMessage(this, "Transfer handler in {0} configured with 'getVariable', but no 'perPodPeristenceName'", internalProp.name);
                                                }
                                            }
                                            else if (pluginConfig.HasValue("getVariable"))
                                            {
                                                if (pluginConfig.HasValue("perPodPersistenceName"))
                                                {
                                                    transferGetter = rpmComp.InstantiateVariableOrNumber(pluginConfig.GetValue("getVariable").Trim());
                                                    transferPersistentName = pluginConfig.GetValue("perPodPersistenceName").Trim();
                                                    actionName = "transferToPersistent";
                                                    customAction = CustomActions.TransferToPersistent;
                                                }
                                                else
                                                {
                                                    JUtil.LogErrorMessage(this, "Transfer handler in {0} configured with 'getVariable', but no 'perPodPeristenceName'", internalProp.name);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (transferGetter == null && transferSetter == null)
                            {
                                actionName = "dummy";
                                stateVariable = null;
                                JUtil.LogMessage(this, "Transfer handlers did not start, reverting to dummy mode.");
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

                    persistentVarValid = !string.IsNullOrEmpty(persistentVarName);
                }

                perPodPersistenceValid = !string.IsNullOrEmpty(perPodPersistenceName);

                if (customGroupList.ContainsKey(actionName))
                {
                    customAction = customGroupList[actionName];
                }

                if (needsElectricChargeValue || persistentVarValid || !string.IsNullOrEmpty(perPodMasterSwitchName) || !string.IsNullOrEmpty(masterVariableName) ||
                    transferGetter != null || transferSetter != null)
                {
                    rpmComp.UpdateDataRefreshRate(refreshRate);

                    if (!string.IsNullOrEmpty(masterVariableName))
                    {
                        string[] range = masterVariableRange.Split(',');
                        if (range.Length == 2)
                        {
                            masterVariable = new VariableOrNumberRange(rpmComp, masterVariableName, range[0], range[1]);
                        }
                        else
                        {
                            masterVariable = null;
                        }
                    }
                }

                if (needsElectricChargeValue)
                {
                    del = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), this, "ResourceDepletedCallback");
                    rpmComp.RegisterResourceCallback(resourceName, del);
                }

                // set up the toggle switch
                if (!string.IsNullOrEmpty(switchTransform))
                {
                    if (momentarySwitch)
                    {
                        SmarterButton.CreateButton(internalProp, switchTransform, Click, Click);
                    }
                    else
                    {
                        SmarterButton.CreateButton(internalProp, switchTransform, Click);
                    }
                }

                if (isCustomAction)
                {
                    if (isPluginAction && stateVariable != null)
                    {
                        currentState = stateVariable.AsInt() > 0;
                    }
                    else
                    {
                        if (persistentVarValid)
                        {
                            if (switchGroupIdentifier >= 0)
                            {
                                int activeSwitch = rpmComp.GetPersistentVariable(persistentVarName, 0).MassageToInt();

                                currentState = customGroupState = (switchGroupIdentifier == activeSwitch);
                            }
                            else
                            {
                                currentState = customGroupState = rpmComp.GetPersistentVariable(persistentVarName, initialState);
                            }

                            if (customAction == CustomActions.IntLight)
                            {
                                // We have to restore lighting after reading the
                                // persistent variable.
                                SetInternalLights(customGroupState);
                            }
                        }
                    }
                }

                if (persistentVarValid && !rpmComp.HasPersistentVariable(persistentVarName))
                {
                    if (switchGroupIdentifier >= 0)
                    {
                        if (currentState)
                        {
                            rpmComp.SetPersistentVariable(persistentVarName, switchGroupIdentifier);
                        }
                    }
                    else
                    {
                        rpmComp.SetPersistentVariable(persistentVarName, currentState);
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
                    Renderer colorShiftRenderer = internalProp.FindModelComponent<Renderer>(coloredObject);
                    disabledColorValue = JUtil.ParseColor32(disabledColor, part, ref rpmComp);
                    enabledColorValue = JUtil.ParseColor32(enabledColor, part, ref rpmComp);
                    colorShiftMaterial = colorShiftRenderer.material;
                    colorNameId = Shader.PropertyToID(colorName);
                    colorShiftMaterial.SetColor(colorNameId, (currentState ^ reverse ? enabledColorValue : disabledColorValue));
                }
                else
                {
                    JUtil.LogMessage(this, "Warning, neither color nor animation are defined in prop {0} #{1} (this may be okay).", internalProp.propName, internalProp.propID);
                }

                audioOutput = JUtil.SetupIVASound(internalProp, switchSound, switchSoundVolume, false);

                if (!string.IsNullOrEmpty(loopingSound) && loopingSoundVolume > 0.0f)
                {
                    loopingOutput = JUtil.SetupIVASound(internalProp, loopingSound, loopingSoundVolume, true);
                }

                perPodMasterSwitchValid = !string.IsNullOrEmpty(perPodMasterSwitchName);

                JUtil.LogMessage(this, "Configuration complete in prop {0} ({1}).", internalProp.propID, internalProp.propName);

                startupComplete = true;
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Exception configuring prop {0} ({1}): {2}", internalProp.propID, internalProp.propName, e);
                JUtil.AnnoyUser(this);
                enabled = false;
            }
        }

        public void OnDestroy()
        {
            //JUtil.LogMessage(this, "OnDestroy()");
            if (colorShiftMaterial != null)
            {
                UnityEngine.Object.Destroy(colorShiftMaterial);
                colorShiftMaterial = null;
            }
            actionHandler = null;
            transferGetter = null;
            transferSetter = null;
            audioOutput = null;
            loopingOutput = null;

            if (del != null)
            {
                rpmComp.UnregisterResourceCallback(resourceName, del);
            }
        }

        private void SetInternalLights(bool value)
        {
            for (int i = 0; i < lightObjects.Length; ++i)
            {
                lightObjects[i].enabled = value;
            }
        }

        public void Click()
        {
            bool switchEnabled = true;

            if (!forcedShutdown)
            {
                if (perPodMasterSwitchValid)
                {
                    switchEnabled = rpmComp.GetPersistentVariable(perPodMasterSwitchName, false);
                }
                if (masterVariable != null)
                {
                    switchEnabled = masterVariable.IsInRange();
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
                    if (!forcedShutdown && !customGroupState)
                    {
                        customGroupState = true;
                        if (persistentVarValid)
                        {
                            rpmComp.SetPersistentVariable(persistentVarName, switchGroupIdentifier);
                        }
                    }
                    // else: can't turn off a radio group switch.
                }
                else if (customAction == CustomActions.Plugin && stateVariable != null)
                {
                    int ivalue = stateVariable.AsInt();
                    customGroupState = (ivalue < 1) && !forcedShutdown;
                }
                else
                {
                    customGroupState = !customGroupState;
                    if (persistentVarValid)
                    {
                        rpmComp.SetPersistentVariable(persistentVarName, customGroupState);
                    }
                }
            }
            else
            {
                vessel.ActionGroups.ToggleGroup(kspAction);
            }

            // Now we do extra things that with regular actions can't happen.
            switch (customAction)
            {
                case CustomActions.IntLight:
                    SetInternalLights(customGroupState);
                    break;
                case CustomActions.Plugin:
                    actionHandler(customGroupState);
                    break;
                case CustomActions.Stage:
                    if (InputLockManager.IsUnlocked(ControlTypes.STAGING))
                    {
                        StageManager.ActivateNextStage();
                    }
                    break;
                case CustomActions.TransferToPersistent:
                    if (stateVariable != null)
                    {
                        // stateVariable can disable the button functionality.
                        int ivalue = stateVariable.AsInt();
                        if (ivalue < 1)
                        {
                            return; // early - button disabled
                        }
                    }
                    float getValue = transferGetter.AsFloat();
                    rpmComp.SetPersistentVariable(transferPersistentName, getValue);
                    break;
                case CustomActions.TransferFromPersistent:
                    if (stateVariable != null)
                    {
                        // stateVariable can disable the button functionality.
                        int ivalue = stateVariable.AsInt();
                        if (ivalue < 1)
                        {
                            return; // early - button disabled
                        }
                    }
                    if (rpmComp.HasPersistentVariable(transferPersistentName))
                    {
                        transferSetter(rpmComp.GetPersistentVariable(transferPersistentName, 0.0).MassageToDouble());
                    }
                    break;
                case CustomActions.TransferFromVariable:
                    if (stateVariable != null)
                    {
                        // stateVariable can disable the button functionality.
                        int ivalue = stateVariable.AsInt();
                        if (ivalue < 1)
                        {
                            return; // early - button disabled
                        }
                    }
                    double xferValue = transferGetter.AsDouble();
                    transferSetter(xferValue);
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

            if (!JUtil.IsActiveVessel(vessel))
            {
                if (loopingOutput != null && currentState == true)
                {
                    loopingOutput.audio.volume = 0.0f;
                }
                return;
            }
            else if (loopingOutput != null && currentState == true && loopingOutput.Active)
            {
                loopingOutput.audio.volume = loopingSoundVolume * GameSettings.SHIP_VOLUME;
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

            // Bizarre, but looks like I need to animate things offscreen if I want them in the right condition when camera comes back.
            // So there's no check for internal cameras.

            bool newState;
            if (isPluginAction && stateVariable != null)
            {
                try
                {
                    newState = (stateVariable.AsInt()) > 0;
                }
                catch
                {
                    newState = currentState;
                }
            }
            else if (isCustomAction)
            {
                if (string.IsNullOrEmpty(switchTransform) && perPodPersistenceValid)
                {
                    if (switchGroupIdentifier >= 0)
                    {
                        int activeGroupId = rpmComp.GetPersistentVariable(persistentVarName, 0).MassageToInt();
                        newState = (switchGroupIdentifier == activeGroupId);
                        customGroupState = newState;
                    }
                    else
                    {
                        // If the switch transform is not given, and the global comp.Persistence value is, this means this is a slave module.
                        newState = rpmComp.GetPersistentVariable(persistentVarName, false);
                    }
                }
                else
                {
                    // Otherwise it's a master module. But it still might have to follow the clicks on other copies of the same prop...
                    if (perPodPersistenceValid)
                    {
                        if (switchGroupIdentifier >= 0)
                        {
                            int activeGroupId = rpmComp.GetPersistentVariable(persistentVarName, 0).MassageToInt();
                            newState = (switchGroupIdentifier == activeGroupId);
                            customGroupState = newState;
                        }
                        else
                        {
                            newState = rpmComp.GetPersistentVariable(persistentVarName, customGroupState);
                        }
                    }
                    else
                    {
                        newState = customGroupState;
                    }
                }
            }
            else
            {
                newState = vessel.ActionGroups[kspAction];
            }

            // If needsElectricCharge is true and there is no charge, the state value is overridden to false and the click action is reexecuted.
            if (needsElectricChargeValue)
            {
                forcedShutdown |= resourceDepleted;
            }

            if (perPodMasterSwitchValid)
            {
                bool switchEnabled = rpmComp.GetPersistentVariable(perPodMasterSwitchName, false);
                if (!switchEnabled)
                {
                    // If the master switch is 'off', this switch needs to turn off
                    newState = false;
                    forcedShutdown = true;
                }
            }

            if (masterVariable != null)
            {
                if (!masterVariable.IsInRange())
                {
                    newState = false;
                    forcedShutdown = true;
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

                if (loopingOutput != null && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
                    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
                {
                    if (newState)
                    {
                        loopingOutput.audio.Play();
                    }
                    else
                    {
                        loopingOutput.audio.Stop();
                    }
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
                else if (colorShiftMaterial != null)
                {
                    colorShiftMaterial.SetColor(colorNameId, (newState ^ reverse ? enabledColorValue : disabledColorValue));
                }
                currentState = newState;
            }
        }

        /// <summary>
        /// This little callback allows RasterPropMonitorComputer to notify
        /// this module when its required resource has gone above or below the
        /// arbitrary and hard-coded threshold of 0.01, so that each switch is
        /// not forced to query every update "How much power is there?".
        /// </summary>
        /// <param name="newValue"></param>
        void ResourceDepletedCallback(bool newValue)
        {
            resourceDepleted = newValue;
        }
    }
}
