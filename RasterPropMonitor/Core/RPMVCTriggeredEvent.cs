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

namespace JSI
{
    public partial class RPMVesselComputer : VesselModule
    {
        internal class TriggeredEventTemplate
        {
            // Name of this event
            internal readonly string eventName;
            internal readonly string variableName;
            internal readonly string range;
            internal readonly string triggerEvent;
            internal readonly string triggerState;
            internal readonly string eventState;
            internal readonly string oneShot;

            internal TriggeredEventTemplate(ConfigNode node)
            {
                if(!node.HasValue("eventName"))
                {
                    throw new Exception("TriggeredEvent: eventName field not found");
                }
                eventName = node.GetValue("eventName").Trim();

                if (!node.HasValue("variableName"))
                {
                    throw new Exception("TriggeredEvent: variableName field not found");
                }
                variableName = node.GetValue("variableName").Trim();

                if (!node.HasValue("range"))
                {
                    throw new Exception("TriggeredEvent: range field not found");
                }
                range = node.GetValue("range").Trim();

                if (!node.HasValue("triggerEvent"))
                {
                    throw new Exception("TriggeredEvent: triggerEvent field not found");
                }
                triggerEvent = node.GetValue("triggerEvent").Trim();

                if (!node.HasValue("triggerState"))
                {
                    throw new Exception("TriggeredEvent: triggerState field not found");
                }
                triggerState = node.GetValue("triggerState").Trim();

                if (node.HasValue("eventState"))
                {
                    eventState = node.GetValue("eventState").Trim();
                }
                if (node.HasValue("oneShot"))
                {
                    oneShot = node.GetValue("oneShot").Trim();
                }
            }
        }

        internal class TriggeredEvent
        {
            // Name of this event
            internal readonly string eventName;
            // Name of the variable we're keying off of (for sorting ? may not be needed)
            //string variableName;
            // The tester
            private readonly VariableOrNumberRange variable;
            // built-in action to trigger
            private readonly KSPActionGroup action;
            // Is the action from a plugin?
            private readonly bool isPluginAction;
            // The plugin action to trigger
            private readonly Action<bool> pluginAction;
            // State query for the plugin (only needed if toggleAction is true)
            private readonly Func<bool> pluginState;
            // Is the event a toggle (set to false if currently true, true if currently false)?
            private readonly bool toggleAction;
            // If it's not a toggle, what state do we use as the parameter?
            private readonly bool triggerState;
            private readonly bool oneShot;

            // Has the event been armed (variable was out of bounds)
            bool armed;
            // Once armed, has it been triggered?
            bool triggered;

            internal TriggeredEvent(TriggeredEventTemplate template, RPMVesselComputer comp)
            {
                eventName = template.eventName;
                if (string.IsNullOrEmpty(eventName))
                {
                    throw new Exception("TriggeredEvent: eventName not valid");
                }

                string[] tokens = template.range.Split(',');
                if (string.IsNullOrEmpty(template.variableName))
                {
                    throw new Exception("TriggeredEvent: variableName not valid");
                }
                if (tokens.Length != 2)
                {
                    throw new Exception("TriggeredEvent: tokens not valid");
                }
                variable = new VariableOrNumberRange(template.variableName, tokens[0], tokens[1]);

                if (JSIActionGroupSwitch.groupList.ContainsKey(template.triggerEvent))
                {
                    isPluginAction = false;
                    action = JSIActionGroupSwitch.groupList[template.triggerEvent];
                }
                else
                {
                    isPluginAction = true;
                    pluginAction = (Action<bool>)comp.GetInternalMethod(template.triggerEvent, typeof(Action<bool>));

                    if (pluginAction == null)
                    {
                        throw new Exception("TriggeredEvent: Unable to initialize pluginAction");
                    }
                }

                if (string.IsNullOrEmpty(template.triggerState))
                {
                    throw new Exception("TriggeredEvent: Unable to initialize triggerState");
                }
                if (bool.TryParse(template.triggerState, out triggerState))
                {
                    toggleAction = false;
                }
                else if (template.triggerState.ToLower() == "toggle")
                {
                    toggleAction = true;

                    if (isPluginAction)
                    {
                        pluginState = (Func<bool>)comp.GetInternalMethod(template.eventState, typeof(Func<bool>));
                        if (pluginState == null)
                        {
                            throw new Exception("TriggeredEvent: Unable to initialize pluginState");
                        }
                    }
                }
                else
                {
                    throw new Exception("TriggeredEvent: Unable to determine triggerState");
                }

                if (!string.IsNullOrEmpty(template.oneShot))
                {
                    if (!bool.TryParse(template.oneShot, out oneShot))
                    {
                        oneShot = false;
                    }
                }
                else
                {
                    oneShot = false;
                }

                JUtil.LogMessage(this, "Triggered Event {0} created", eventName);
            }

            internal void Update(RPMVesselComputer comp)
            {
                bool inRange = variable.IsInRange(comp);
                if (armed)
                {
                    if (inRange)
                    {
                        if (!triggered)
                        {
                            JUtil.LogMessage(this, "Event {0} triggered", eventName);
                            triggered = true;
                            armed = oneShot;
                            DoEvent(comp.vessel);
                        }
                    }
                }
                else if (!inRange)
                {
                    JUtil.LogMessage(this, "Event {0} armed", eventName);
                    armed = true;
                    triggered = false;
                }
            }

            private void DoEvent(Vessel vessel)
            {
                if (toggleAction)
                {
                    if (isPluginAction)
                    {
                        pluginAction(!pluginState());
                    }
                    else
                    {
                        vessel.ActionGroups.ToggleGroup(action);
                    }
                }
                else
                {
                    if (isPluginAction)
                    {
                        pluginAction(triggerState);
                    }
                    else
                    {
                        vessel.ActionGroups.SetGroup(action, triggerState);
                    }
                }
            }
        }

        private List<TriggeredEvent> activeTriggeredEvents = new List<TriggeredEvent>();

        internal void AddTriggeredEvent(string eventName)
        {
            for (int i = 0; i < activeTriggeredEvents.Count; ++i)
            {
                if (activeTriggeredEvents[i].eventName == eventName)
                {
                    // Already registered this event
                    return;
                }
            }

            for (int i = 0; i < RPMGlobals.triggeredEvents.Count; ++i)
            {
                if (RPMGlobals.triggeredEvents[i].eventName == eventName)
                {
                    activeTriggeredEvents.Add(new TriggeredEvent(RPMGlobals.triggeredEvents[i], this));
                }
            }
        }
    }
}
