using System;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;

namespace JSI
{
    public class ExternalVariableHandlers
    {
        private readonly List<VariableHandler> handlers = new List<VariableHandler>();

        public ExternalVariableHandlers(Part ourPart)
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("RPMCVARIABLEHANDLER"))
            {
                if (!node.HasValue("name") || !node.HasValue("method"))
                {
                    JUtil.LogMessage(this, "A variable handler configuration block is missing key items and was ignored.");
                }
                else
                {
                    handlers.Add(new VariableHandler(node, ourPart));
                }
            }
            foreach (VariableHandler thatHandler in handlers)
            {
                JUtil.LogMessage(this, "Variable handler {0} is known and {1:;;\"not\"} loaded.", thatHandler.handlerName, thatHandler.active.GetHashCode());
            }
        }

        public bool ProcessVariable(string variable, out object result, out bool cacheable)
        {
            result = null;
            cacheable = true;
            foreach (VariableHandler handler in handlers)
            {
                if (handler.ProcessVariable(variable, out result, out cacheable) && result != null)
                {
                    return true;
                }
            }
            return false;
        }

        private class VariableHandler
        {
            public readonly string handlerName;
            public readonly bool active;
            private readonly Func<string, object> handlerFunction;

            private struct VariableRecord
            {
                public double defaultValue;
                public string defaultString;
                public bool cacheable;
                public bool fallback;
            };

            private readonly Dictionary<string, VariableRecord> handledVariables = new Dictionary<string, VariableRecord>();

            public bool ProcessVariable(string variable, out object result, out bool cacheable)
            {
                cacheable = true;
                result = null;
                if (handledVariables.ContainsKey(variable))
                {
                    if (active)
                    {
                        result = handlerFunction(variable);
                        cacheable = handledVariables[variable].cacheable;
                        if (result == null)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (handledVariables[variable].fallback)
                        {
                            return false;
                        }
                        result = string.IsNullOrEmpty(handledVariables[variable].defaultString) ?
                                 (object)handledVariables[variable].defaultValue : handledVariables[variable].defaultString;
                    }
                    return true;
                }
                return false;
            }

            public VariableHandler(ConfigNode node, Part ourPart)
            {
                handlerName = node.GetValue("name");
                foreach (string variableRecord in node.GetValues("variable"))
                {
                    var record = new VariableRecord();
                    string[] tokens = variableRecord.Split(',');
                    if (tokens.Length >= 2)
                    {
                        double defaultDouble;
                        if (double.TryParse(tokens[1], NumberStyles.Any, CultureInfo.InvariantCulture, out defaultDouble))
                        {
                            record.defaultValue = defaultDouble;
                        }
                        else
                        {
                            if (tokens[1].Trim() == "fallback")
                            {
                                record.fallback = true;
                            }
                            else
                            {
                                record.defaultString = tokens[1];
                            }
                        }
                    }
                    if (tokens.Length >= 3)
                    {
                        record.cacheable = bool.Parse(tokens[2]);
                    }
                    handledVariables.Add(tokens[0], record);
                }

                active = InstantiateHandler(node, ourPart, out handlerFunction);
            }

            private static bool InstantiateHandler(ConfigNode node, Part ourPart, out Func<string, object> handlerFunction)
            {
                handlerFunction = null;
                var handlerConfiguration = new ConfigNode("MODULE");
                node.CopyTo(handlerConfiguration);
                string moduleName = node.GetValue("name");
                string methodName = node.GetValue("method");

                // Since we're working with part modules here, and starting in a pod config,
                // we'll keep one instance per pod, which will let them instantiate with their own config if needed.
                MonoBehaviour thatModule = null;
                foreach (PartModule potentialModule in ourPart.Modules)
                {
                    if (potentialModule.ClassName == moduleName)
                    {
                        thatModule = potentialModule;
                        break;
                    }
                }
                if (thatModule == null)
                {
                    try
                    {
                        thatModule = ourPart.AddModule(handlerConfiguration);
                    }
                    catch
                    {
                        JUtil.LogErrorMessage(null, "Caught exception when trying to instantiate module '{0}'. Something's fishy here", moduleName);
                    }
                }
                if (thatModule == null)
                {
                    JUtil.LogMessage(null, "Warning, variable handler module \"{0}\" could not be loaded. This could be perfectly normal.", moduleName);
                    return false;
                }
                foreach (MethodInfo m in thatModule.GetType().GetMethods())
                {
                    if (m.Name == node.GetValue("method"))
                    {
                        try
                        {
                            handlerFunction = (Func<string, object>)Delegate.CreateDelegate(typeof(Func<string, object>), thatModule, m);
                        }
                        catch
                        {
                            JUtil.LogErrorMessage(null, "Error, incorrect variable handler configuration for module {0}", moduleName);
                            return false;
                        }
                        break;
                    }
                }
                return true;
            }
        }
    }
}
