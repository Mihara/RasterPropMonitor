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
    public class RasterPropMonitorComputer : PartModule
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

        [KSPField(isPersistant = true)]
        public string RPMCid = string.Empty;
        private Guid id = Guid.Empty;

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
                    throw new ArgumentException("Cannot instantiate RPMC in this location.");
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

        public void Start()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                if(string.IsNullOrEmpty(RPMCid))
                {
                    id = Guid.NewGuid();
                    RPMCid = id.ToString();
                    JUtil.LogMessage(this, "Start: Creating GUID {0}", id);
                }
                else
                {
                    id = new Guid(RPMCid);
                    JUtil.LogMessage(this, "Start: Loading GUID string {0} into {1}", RPMCid, id);
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
                        comp.AddTriggeredEvent(varstring[i].Trim());
                    }
                }

                ConfigNode[] moduleConfigs = part.partInfo.partConfig.GetNodes("MODULE");
                for (int moduleId = 0; moduleId < moduleConfigs.Length; ++moduleId)
                {
                    if (moduleConfigs[moduleId].GetValue("name") == moduleName)
                    {
                        ConfigNode[] overrideColorSetup = moduleConfigs[moduleId].GetNodes("RPM_COLOROVERRIDE");
                        for(int colorGrp=0; colorGrp < overrideColorSetup.Length; ++colorGrp)
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
        }
    }
}
