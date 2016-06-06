//#define SHOW_VARIABLE_QUERY_COUNTER
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
using System.Collections.Generic;
using UnityEngine;

namespace JSI
{
    // MOARdV TODO:
    // Crew:
    // onCrewBoardVessel
    // onCrewOnEva
    // onCrewTransferred
    public partial class RasterPropMonitorComputer : PartModule
    {
        // The only public configuration variable.
        [KSPField]
        public string storedStrings = string.Empty;

        // The OTHER public configuration variable.
        [KSPField]
        public string triggeredEvents = string.Empty;

        // Yes, it's a really braindead way of doing it, but I ran out of elegant ones,
        // because nothing appears to work as documented -- IF it's documented.
        // This one is sure to work and isn't THAT much of a performance drain, really.
        // Pull requests welcome
        // Vessel description storage and related code.
        [KSPField(isPersistant = true)]
        public string vesselDescription = string.Empty;
        private string vesselDescriptionForDisplay = string.Empty;
        private readonly string editorNewline = ((char)0x0a).ToString();
        private string lastVesselDescription = string.Empty;

        internal List<string> storedStringsArray = new List<string>();

        // Local variables
        private ManeuverNode node;
        private bool orbitSensibility;

        private List<ProtoCrewMember> localCrew = new List<ProtoCrewMember>();
        private List<kerbalExpressionSystem> localCrewMedical = new List<kerbalExpressionSystem>();

        // Processing cache!
        private readonly List<IJSIModule> installedModules = new List<IJSIModule>();
        private readonly DefaultableDictionary<string, object> resultCache = new DefaultableDictionary<string, object>(null);
        private readonly DefaultableDictionary<string, VariableCache> variableCache = new DefaultableDictionary<string, VariableCache>(null);
        private readonly HashSet<string> unrecognizedVariables = new HashSet<string>();
        private uint masterSerialNumber = 0u;

        // Data refresh
        private int dataUpdateCountdown;
        private int refreshDataRate = 60;
        private bool timeToUpdate = false;

        // Callback system
        private Dictionary<string, List<Action<float>>> onChangeCallbacks = new Dictionary<string, List<Action<float>>>();
        private Dictionary<string, float> onChangeValue = new Dictionary<string, float>();
        private Dictionary<string, List<Action<bool>>> onResourceCallbacks = new Dictionary<string, List<Action<bool>>>();
        private Dictionary<string, bool> onResourceValue = new Dictionary<string, bool>();
        private bool forceCallbackRefresh = false;

        // Diagnostics
        private int debug_fixedUpdates = 0;
        private DefaultableDictionary<string, int> debug_callCount = new DefaultableDictionary<string, int>(0);
#if SHOW_VARIABLE_QUERY_COUNTER
        private int debug_varsProcessed = 0;
        private long debug_totalVars = 0;
#endif

        [KSPField(isPersistant = true)]
        public string RPMCid = string.Empty;
        private Guid id = Guid.Empty;
        /// <summary>
        /// The Guid of the vessel to which we belong.  We update this very
        /// obsessively to avoid it being out-of-sync with our craft.
        /// </summary>
        private Guid vid = Guid.Empty;

        private ExternalVariableHandlers plugins = null;
        internal Dictionary<string, Color32> overrideColors = new Dictionary<string, Color32>();

        // Public functions:
        // Request the instance, create it if one doesn't exist:
        public static RasterPropMonitorComputer Instantiate(MonoBehaviour referenceLocation, bool createIfMissing)
        {
            var thatProp = referenceLocation as InternalProp;
            var thatPart = referenceLocation as Part;
            if (thatPart == null)
            {
                if (thatProp == null)
                {
                    //throw new ArgumentException("Cannot instantiate RPMC in this location.");
                    return null;
                }
                thatPart = thatProp.part;
            }
            for (int i = 0; i < thatPart.Modules.Count; i++)
            {
                if (thatPart.Modules[i].ClassName == typeof(RasterPropMonitorComputer).Name)
                {
                    return thatPart.Modules[i] as RasterPropMonitorComputer;
                }
            }
            return (createIfMissing) ? thatPart.AddModule(typeof(RasterPropMonitorComputer).Name) as RasterPropMonitorComputer : null;
        }

        /// <summary>
        /// Wrapper for ExternalVariablesHandler.ProcessVariable.
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="result"></param>
        /// <param name="cacheable"></param>
        /// <returns></returns>
        internal bool ProcessVariable(string variable, out object result, out bool cacheable)
        {
            return plugins.ProcessVariable(variable, out result, out cacheable);
        }

        // Page handler interface for vessel description page.
        // Analysis disable UnusedParameter
        public string VesselDescriptionRaw(int screenWidth, int screenHeight)
        {
            // Analysis restore UnusedParameter
            return vesselDescriptionForDisplay.UnMangleConfigText();
        }

        // Analysis disable UnusedParameter
        public string VesselDescriptionWordwrapped(int screenWidth, int screenHeight)
        {
            // Analysis restore UnusedParameter
            return JUtil.WordWrap(vesselDescriptionForDisplay.UnMangleConfigText(), screenWidth);
        }

        /// <summary>
        /// This intermediary will cache the results so that multiple variable
        /// requests within the frame would not result in duplicated code.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public object ProcessVariable(string input, RPMVesselComputer comp)
        {
#if SHOW_VARIABLE_QUERY_COUNTER
            ++debug_varsProcessed;
#endif

            if (RPMGlobals.debugShowVariableCallCount)
            {
                debug_callCount[input] = debug_callCount[input] + 1;
            }

            if (comp == null)
            {
                comp = RPMVesselComputer.Instance(vid);
            }
            VariableCache vc = variableCache[input];
            if (vc != null)
            {
                if (!(vc.cacheable && vc.serialNumber == masterSerialNumber))
                {
                    try
                    {
                        object newValue = vc.accessor(input, this, comp);
                        vc.serialNumber = masterSerialNumber;
                        vc.cachedValue = newValue;
                    }
                    catch (Exception e)
                    {
                        JUtil.LogErrorMessage(this, "Processing error while processing {0}: {1}", input, e.Message);
                        variableCache.Remove(input);
                    }
                }

                return vc.cachedValue;
            }
            else
            {
                bool cacheable;
                VariableEvaluator evaluator = GetEvaluator(input, out cacheable);
                if (evaluator != null)
                {
                    vc = new VariableCache(cacheable, evaluator);
                    try
                    {
                        object newValue = vc.accessor(input, this, comp);
                        vc.serialNumber = masterSerialNumber;
                        vc.cachedValue = newValue;

                        if (newValue.ToString() == input && !unrecognizedVariables.Contains(input))
                        {
                            unrecognizedVariables.Add(input);
                            JUtil.LogMessage(this, "Unrecognized variable {0}", input);
                        }
                    }
                    catch (Exception e)
                    {
                        JUtil.LogErrorMessage(this, "Processing error while adding {0}: {1}", input, e.Message);
                        //variableCache.Clear();
                        return -1;
                    }

                    variableCache[input] = vc;
                    return vc.cachedValue;
                }
            }

            object returnValue = resultCache[input];
            if (returnValue == null)
            {
                bool cacheable = true;
                try
                {
                    if (!ProcessVariable(input, out returnValue, out cacheable))
                    {
                        cacheable = false;
                        returnValue = input;
                        if (!unrecognizedVariables.Contains(input))
                        {
                            unrecognizedVariables.Add(input);
                            JUtil.LogMessage(this, "Unrecognized variable {0}", input);
                        }
                    }
                }
                catch (Exception e)
                {
                    JUtil.LogErrorMessage(this, "Processing error while processing {0}: {1}", input, e.Message);
                    // Most of the variables are doubles...
                    return double.NaN;
                }

                if (cacheable && returnValue != null)
                {
                    //JUtil.LogMessage(this, "Found variable \"{0}\"!  It was {1}", input, returnValue);
                    resultCache.Add(input, returnValue);
                }
            }

            return returnValue;
        }

        /// <summary>
        /// Register a callback to receive notifications when a variable has changed.
        /// Used to prevent polling of low-frequency, high-utilization variables.
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="cb"></param>
        public void RegisterCallback(string variableName, Action<float> cb)
        {
            //JUtil.LogMessage(this, "RegisterCallback with {1} for {0}", variableName, RPMCid);
            if (onChangeCallbacks.ContainsKey(variableName))
            {
                onChangeCallbacks[variableName].Add(cb);
            }
            else
            {
                var callbackList = new List<Action<float>>();
                callbackList.Add(cb);
                onChangeCallbacks[variableName] = callbackList;
                onChangeValue[variableName] = float.MaxValue;
            }
            forceCallbackRefresh = true;
        }

        /// <summary>
        /// Unregister a callback for receiving variable update notifications.
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="cb"></param>
        public void UnregisterCallback(string variableName, Action<float> cb)
        {
            //JUtil.LogMessage(this, "UnregisterCallback with {1} for {0}", variableName, RPMCid);
            if (onChangeCallbacks.ContainsKey(variableName))
            {
                try
                {
                    onChangeCallbacks[variableName].Remove(cb);
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Register for a resource callback.  Resource callbacks provide a boolean that is
        /// updated when the named resource drops above or below 0.01f.
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="cb"></param>
        public void RegisterResourceCallback(string variableName, Action<bool> cb)
        {
            if (onResourceCallbacks.ContainsKey(variableName))
            {
                onResourceCallbacks[variableName].Add(cb);
            }
            else
            {
                var callbackList = new List<Action<bool>>();
                callbackList.Add(cb);
                onResourceCallbacks[variableName] = callbackList;

                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                bool initValue = (ProcessVariable(variableName, comp).MassageToFloat() < 0.01f);
                onResourceValue[variableName] = initValue;
            }
            cb(onResourceValue[variableName]);

            forceCallbackRefresh = true;
        }

        /// <summary>
        /// Remove a previously-registered resource change callback
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="cb"></param>
        public void UnregisterResourceCallback(string variableName, Action<bool> cb)
        {
            if (onResourceCallbacks.ContainsKey(variableName))
            {
                try
                {
                    onResourceCallbacks[variableName].Remove(cb);
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Set the refresh rate (number of Update() calls per triggered update).
        /// The lower of the current data rate and the new data rate is used.
        /// </summary>
        /// <param name="newDataRate">New data rate</param>
        internal void UpdateDataRefreshRate(int newDataRate)
        {
            refreshDataRate = Math.Max(1, Math.Min(newDataRate, refreshDataRate));

            RPMVesselComputer comp = null;
            if (RPMVesselComputer.TryGetInstance(vessel, ref comp))
            {
                comp.UpdateDataRefreshRate(newDataRate);
            }
        }

        #region Monobehaviour
        public void Start()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                vid = vessel.id;

                GameEvents.onVesselWasModified.Add(onVesselWasModified);
                GameEvents.onVesselChange.Add(onVesselChange);

                installedModules.Add(new JSIParachute(vessel));
                installedModules.Add(new JSIMechJeb(vessel));
                installedModules.Add(new JSIInternalRPMButtons(vessel));
                installedModules.Add(new JSIFAR(vessel));
                installedModules.Add(new JSIKAC(vessel));
#if ENABLE_ENGINE_MONITOR
                installedModules.Add(new JSIEngine(vessel));
#endif
                installedModules.Add(new JSIPilotAssistant(vessel));
                installedModules.Add(new JSIChatterer(vessel));

                if (string.IsNullOrEmpty(RPMCid))
                {
                    id = Guid.NewGuid();
                    RPMCid = id.ToString();
                    if (part.internalModel != null)
                    {
                        JUtil.LogMessage(this, "Start: Creating GUID {0} in {1}", id, part.internalModel.internalName);
                    }
                    else
                    {
                        JUtil.LogMessage(this, "Start: Creating GUID {0}", id);
                    }
                }
                else
                {
                    id = new Guid(RPMCid);
                    if (part.internalModel != null)
                    {
                        JUtil.LogMessage(this, "Start: Loading GUID string {0} in {1}", RPMCid, part.internalModel.internalName);
                    }
                    else
                    {
                        JUtil.LogMessage(this, "Start: Loading GUID {0}", id);
                    }
                }

                plugins = new ExternalVariableHandlers(part);

                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                if (!string.IsNullOrEmpty(vesselDescription))
                {
                    comp.SetVesselDescription(vesselDescription);
                }

                // Make sure we have the description strings parsed.
                string[] descriptionStrings = vesselDescription.UnMangleConfigText().Split(JUtil.LineSeparator, StringSplitOptions.None);
                for (int i = 0; i < descriptionStrings.Length; i++)
                {
                    if (descriptionStrings[i].StartsWith("AG", StringComparison.Ordinal) && descriptionStrings[i][3] == '=')
                    {
                        uint groupID;
                        if (uint.TryParse(descriptionStrings[i][2].ToString(), out groupID))
                        {
                            descriptionStrings[i] = string.Empty;
                        }
                    }
                }
                vesselDescriptionForDisplay = string.Join(Environment.NewLine, descriptionStrings).MangleConfigText();
                if (string.IsNullOrEmpty(vesselDescriptionForDisplay))
                {
                    vesselDescriptionForDisplay = " "; // Workaround for issue #466.
                }

                // Now let's parse our stored strings...
                if (!string.IsNullOrEmpty(storedStrings))
                {
                    var storedStringsSplit = storedStrings.Split('|');
                    for (int i = 0; i < storedStringsSplit.Length; ++i)
                    {
                        storedStringsArray.Add(storedStringsSplit[i]);
                    }
                }

                // TODO: If there are triggered events, register for an undock
                // callback so we can void and rebuild the callbacks after undocking.
                // Although it didn't work when I tried it...
                if (!string.IsNullOrEmpty(triggeredEvents))
                {
                    string[] varstring = triggeredEvents.Split('|');
                    for (int i = 0; i < varstring.Length; ++i)
                    {
                        AddTriggeredEvent(varstring[i].Trim());
                    }
                }

                ConfigNode[] moduleConfigs = part.partInfo.partConfig.GetNodes("MODULE");
                for (int moduleId = 0; moduleId < moduleConfigs.Length; ++moduleId)
                {
                    if (moduleConfigs[moduleId].GetValue("name") == moduleName)
                    {
                        ConfigNode[] overrideColorSetup = moduleConfigs[moduleId].GetNodes("RPM_COLOROVERRIDE");
                        for (int colorGrp = 0; colorGrp < overrideColorSetup.Length; ++colorGrp)
                        {
                            ConfigNode[] colorConfig = overrideColorSetup[colorGrp].GetNodes("COLORDEFINITION");
                            for (int defIdx = 0; defIdx < colorConfig.Length; ++defIdx)
                            {
                                if (colorConfig[defIdx].HasValue("name") && colorConfig[defIdx].HasValue("color"))
                                {
                                    string name = "COLOR_" + (colorConfig[defIdx].GetValue("name").Trim());
                                    Color32 color = ConfigNode.ParseColor32(colorConfig[defIdx].GetValue("color").Trim());
                                    if (overrideColors.ContainsKey(name))
                                    {
                                        overrideColors[name] = color;
                                    }
                                    else
                                    {
                                        overrideColors.Add(name, color);
                                    }
                                }
                            }
                        }
                    }
                }

                // part.internalModel can be null if the craft is loaded, but isn't the active/IVA craft
                if (part.internalModel != null)
                {
                    for (int i = 0; i < part.internalModel.seats.Count; i++)
                    {
                        localCrew.Add(part.internalModel.seats[i].crew);
                        localCrewMedical.Add((localCrew[i] == null) ? null : localCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>());
                    }
                }

                UpdateLocalVars();
            }
        }

        private void UpdateLocalVars()
        {
            if (vessel.patchedConicSolver != null)
            {
                node = vessel.patchedConicSolver.maneuverNodes.Count > 0 ? vessel.patchedConicSolver.maneuverNodes[0] : null;
            }
            else
            {
                node = null;
            }

            orbitSensibility = JUtil.OrbitMakesSense(vessel);

            if (part.internalModel != null)
            {
                if (part.internalModel.seats.Count != localCrew.Count)
                {
                    // This can happen when the internalModel is loaded when
                    // it wasn't previously, which appears to occur on docking
                    // for instance.
                    localCrew.Clear();
                    localCrewMedical.Clear();
                    for (int i = 0; i < part.internalModel.seats.Count; i++)
                    {
                        localCrew.Add(part.internalModel.seats[i].crew);
                        localCrewMedical.Add((localCrew[i] == null) ? null : localCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>());
                    }

                }
                // TODO: Not polling this - find the callbacks for it
                for (int i = 0; i < part.internalModel.seats.Count; i++)
                {
                    localCrew[i] = part.internalModel.seats[i].crew;
                    localCrewMedical[i] = (localCrew[i]) == null ? null : localCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>();
                }
            }
        }

        public void FixedUpdate()
        {
            if (JUtil.RasterPropMonitorShouldUpdate(vessel) && timeToUpdate)
            {
                UpdateLocalVars();

                ++masterSerialNumber;

#if SHOW_VARIABLE_QUERY_COUNTER
                int debug_callbacksProcessed = 0;
                int debug_callbackQueriesMade = 0;
#endif
                RPMVesselComputer comp = RPMVesselComputer.Instance(vid);
                foreach (var cbVal in onChangeCallbacks)
                {
                    float previousValue = onChangeValue[cbVal.Key];
                    float newVal = ProcessVariable(cbVal.Key, comp).MassageToFloat();
                    if (!Mathf.Approximately(newVal, previousValue) || forceCallbackRefresh == true)
                    {
                        for (int i = 0; i < cbVal.Value.Count; ++i)
                        {
#if SHOW_VARIABLE_QUERY_COUNTER
                            ++debug_callbacksProcessed;
#endif
                            cbVal.Value[i](newVal);
                        }

                        onChangeValue[cbVal.Key] = newVal;
                    }
#if SHOW_VARIABLE_QUERY_COUNTER
                    ++debug_callbackQueriesMade;
#endif
                }

                foreach (var cbrVal in onResourceCallbacks)
                {
                    float newVal = ProcessVariable(cbrVal.Key, comp).MassageToFloat();
                    bool newDepleted = (newVal < 0.01f);
                    JUtil.LogMessage(this, "Checking {0} depletion: now {1}, was {2}", cbrVal.Key, newDepleted, onResourceValue[cbrVal.Key]);
                    if (newDepleted != onResourceValue[cbrVal.Key])
                    {
                        for (int i = 0; i < cbrVal.Value.Count; ++i)
                        {
                            cbrVal.Value[i](newDepleted);
                        }

                        onResourceValue[cbrVal.Key] = newDepleted;
                    }
                }

                ++debug_fixedUpdates;

                forceCallbackRefresh = false;
                timeToUpdate = false;
#if SHOW_VARIABLE_QUERY_COUNTER
                debug_totalVars += debug_varsProcessed;
                JUtil.LogMessage(this, "{1} vars processed and {2} callbacks called for {3} callback variables ({0:0.0} avg. vars per FixedUpdate) ---", (float)(debug_totalVars) / (float)(debug_fixedUpdates), debug_varsProcessed, debug_callbacksProcessed, debug_callbackQueriesMade);
                debug_varsProcessed = 0;
#endif

                for (int i = 0; i < activeTriggeredEvents.Count; ++i)
                {
                    activeTriggeredEvents[i].Update(this, comp);
                }
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                // well, it looks sometimes it might become null..
                string s = EditorLogic.fetch.shipDescriptionField != null ? EditorLogic.fetch.shipDescriptionField.text : string.Empty;
                if (s != lastVesselDescription)
                {
                    lastVesselDescription = s;
                    // For some unclear reason, the newline in this case is always 0A, rather than Environment.NewLine.
                    vesselDescription = s.Replace(editorNewline, "$$$");
                }
            }
            else if (JUtil.IsActiveVessel(vessel))
            {
                if (--dataUpdateCountdown < 0)
                {
                    dataUpdateCountdown = refreshDataRate;
                    timeToUpdate = true;
                }
            }
        }

        public void OnDestroy()
        {
            if (!string.IsNullOrEmpty(RPMCid))
            {
                JUtil.LogMessage(this, "OnDestroy: GUID {0}", RPMCid);
            }

#if SHOW_VARIABLE_QUERY_COUNTER
            debug_fixedUpdates = Math.Max(debug_fixedUpdates, 1);
            JUtil.LogMessage(this, "{3}: {0} total variables queried in {1} FixedUpdate calls, or {2:0.0} variables/call",
                debug_totalVars, debug_fixedUpdates, (float)(debug_totalVars) / (float)(debug_fixedUpdates), RPMCid);
#endif

            GameEvents.onVesselWasModified.Remove(onVesselWasModified);
            GameEvents.onVesselChange.Remove(onVesselChange);

            if (RPMGlobals.debugShowVariableCallCount)
            {
                List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>();
                l.AddRange(debug_callCount);
                l.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                {
                    return a.Value - b.Value;
                });
                for (int i = 0; i < l.Count; ++i)
                {
                    JUtil.LogMessage(this, "{0} queried {1} times {2:0.0} calls/FixedUpdate", l[i].Key, l[i].Value, (float)(l[i].Value) / (float)(debug_fixedUpdates));
                }
            }

            localCrew.Clear();
            localCrewMedical.Clear();

            variableCache.Clear();
            resultCache.Clear();
        }

        private void onVesselChange(Vessel who)
        {
            if (who.id == vessel.id)
            {
                vid = vessel.id;
                //JUtil.LogMessage(this, "onVesselChange(): RPMCid {0} / vessel {1}", RPMCid, vid);
                forceCallbackRefresh = true;
                variableCache.Clear();
                resultCache.Clear();

                for (int i = 0; i < installedModules.Count; ++i)
                {
                    installedModules[i].vessel = vessel;
                }

                RPMVesselComputer comp = null;
                if (RPMVesselComputer.TryGetInstance(vessel, ref comp))
                {
                    comp.UpdateDataRefreshRate(refreshDataRate);
                }
            }
        }

        private void onVesselWasModified(Vessel who)
        {
            if (who.id == vessel.id)
            {
                vid = vessel.id;
                JUtil.LogMessage(this, "onVesselWasModified(): RPMCid {0} / vessel {1}", RPMCid, vid);
                forceCallbackRefresh = true;
                variableCache.Clear();
                resultCache.Clear();

                for (int i = 0; i < installedModules.Count; ++i)
                {
                    installedModules[i].vessel = vessel;
                }

                RPMVesselComputer comp = null;
                if (RPMVesselComputer.TryGetInstance(vessel, ref comp))
                {
                    comp.UpdateDataRefreshRate(refreshDataRate);
                }
            }
        }
        #endregion
    }
}
