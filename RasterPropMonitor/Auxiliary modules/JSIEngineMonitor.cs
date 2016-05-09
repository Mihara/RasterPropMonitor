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
#if ENABLE_ENGINE_MONITOR
namespace JSI
{
    /// <summary>
    /// Provides a module for RasterPropMonitor to use for providing granular
    /// control of an engine as well as detailed information.
    /// </summary>
    class JSIEngineMonitor : PartModule
    {
        // Internal data storage.
        [KSPField(isPersistant = true)]
        public int enginedID = 0;

        // Fields to handle right-click GUI.
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Engine ID: ")]
        public string engineDisplayName;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ID +")]
        public void IdPlus()
        {
            enginedID++;
            UpdateName();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ID -")]
        public void IdMinus()
        {
            enginedID--;
            if (enginedID <= 0)
                enginedID = 0;
            UpdateName();
        }

        private void UpdateName()
        {
            engineDisplayName = (enginedID > 0) ? enginedID.ToString() : "Untracked";
        }

        private int mmeIndex = -1;
        private int engineMode1Index = -1;
        private int engineMode2Index = -1;

        public void Start()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

            bool startedOkay = true;
            for(int i=0; i<part.Modules.Count; ++i)
            {
                if(part.Modules[i] is MultiModeEngine)
                {
                    if (mmeIndex == -1)
                    {
                        mmeIndex = i;
                        JUtil.LogMessage(this, "Start(): mmeIndex = {0}", i);
                    }
                    else
                    {
                        JUtil.LogErrorMessage(this, "Found more than one MultiModeEngine on {0} - I don't know what to do with it.", part.name);
                        startedOkay = false;
                    }
                }
                else if(part.Modules[i] is ModuleEngines)
                {
                    if(engineMode1Index == -1)
                    {
                        engineMode1Index = i;
                        JUtil.LogMessage(this, "Start(): engineMode1Index = {0}", i);
                    }
                    else if(engineMode2Index == -1)
                    {
                        engineMode2Index = i;
                        JUtil.LogMessage(this, "Start(): engineMode2Index = {0}", i);
                    }
                    else
                    {
                        JUtil.LogErrorMessage(this, "Found more than 2 ModuleEngines on {0} - I don't know what to do with them.", part.name);
                        startedOkay = false;
                    }
                }
            }

            if (engineMode1Index == -1 || !startedOkay)
            {
                JUtil.LogErrorMessage(this, "Unable to initialize - no ModuleEngine, or too many engines");
                Destroy(this);
                // No engines!
            }
            else
            {
                UpdateName();
            }
        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            if (!JUtil.IsActiveVessel(vessel))
            {
                return;
            }
        }

        public bool GetRunningPrimary()
        {
            if(mmeIndex == -1)
            {
                return true;
            }
            else
            {
                MultiModeEngine mme = part.Modules[mmeIndex] as MultiModeEngine;
                return mme.runningPrimary;
            }
        }

        public void SetRunningPrimary(bool state)
        {
            try
            {
                if (mmeIndex > -1)
                {
                    MultiModeEngine mme = part.Modules[mmeIndex] as MultiModeEngine;

                    // There doesn't appear to be a simpler way to switch modes.
                    // One this I could do is store the even index instead of looping
                    // over it every time we change modes.
                    if (mme.runningPrimary != state)
                    {
                        var ev = mme.Events["ModeEvent"];
                        if (ev != null)
                        {
                            ev.Invoke();
                        }
                    }
                }
            }
            catch
            {
                // no-op
            }
        }
    }
}
#endif
