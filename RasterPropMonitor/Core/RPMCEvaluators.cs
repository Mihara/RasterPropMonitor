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
using System.Reflection;
using System.Text;

namespace JSI
{
    public partial class RasterPropMonitorComputer : PartModule
    {
        internal RPMVesselComputer.VariableEvaluator GetEvaluator(string input, RPMVesselComputer comp, out bool cacheable)
        {
            if (input.IndexOf("_", StringComparison.Ordinal) > -1)
            {
                string[] tokens = input.Split('_');
                cacheable = true;

                // Is loaded?
                if (tokens.Length >= 2 && tokens[0] == "ISLOADED")
                {
                    string assemblyname = input.Substring(input.IndexOf("_", StringComparison.Ordinal) + 1);

                    if (RPMGlobals.knownLoadedAssemblies.Contains(assemblyname))
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp) => { return 1.0f; };
                    }
                    else
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp) => { return 0.0f; };
                    }
                }

                // Periodic variables - A value that toggles between 0 and 1 with
                // the specified (game clock) period.
                if (tokens.Length > 1 && tokens[0] == "PERIOD")
                {
                    if (tokens[1].Substring(tokens[1].Length - 2) == "HZ")
                    {
                        double period;
                        if (double.TryParse(tokens[1].Substring(0, tokens[1].Length - 2), out period) && period > 0.0)
                        {
                            return (string variable, RasterPropMonitorComputer rpmComp) =>
                            {
                                string[] toks = variable.Split('_');
                                double pd;
                                double.TryParse(toks[1].Substring(0, toks[1].Length - 2), out pd);
                                double invPeriod = 1.0 / pd;

                                double remainder = Planetarium.GetUniversalTime() % invPeriod;

                                return (remainder > invPeriod * 0.5).GetHashCode();
                            };

                        }
                    }

                    return (string variable, RasterPropMonitorComputer rpmComp) => { return variable; };
                }

                // Custom variables - if the first token is CUSTOM, MAPPED, MATH, or SELECT, we'll evaluate it here
                if (tokens.Length > 1 && (tokens[0] == "CUSTOM" || tokens[0] == "MAPPED" || tokens[0] == "MATH" || tokens[0] == "SELECT"))
                {
                    if (RPMGlobals.customVariables.ContainsKey(input))
                    {
                        var o = RPMGlobals.customVariables[input];
                        return (string variable, RasterPropMonitorComputer rpmComp) => { return o.Evaluate(rpmComp); };
                    }
                    else
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp) => { return variable; };
                    }
                }

                // Strings stored in module configuration.
                if (tokens.Length == 2 && tokens[0] == "STOREDSTRING")
                {
                    int storedStringNumber;
                    if (int.TryParse(tokens[1], out storedStringNumber) && storedStringNumber >= 0)
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp) =>
                        {
                            if (rpmComp == null)
                            {
                                return "";
                            }

                            string[] toks = variable.Split('_');
                            int storedNumber;
                            int.TryParse(toks[1], out storedNumber);
                            if (storedNumber < rpmComp.storedStringsArray.Count)
                            {
                                return rpmComp.storedStringsArray[storedNumber];
                            }
                            else
                            {
                                return "";
                            }
                        };
                    }
                    else
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp) =>
                        {
                            if (rpmComp == null)
                            {
                                return "";
                            }

                            string[] toks = variable.Split('_');
                            int stringNumber;
                            if (int.TryParse(toks[1], out stringNumber) && stringNumber >= 0 && stringNumber < rpmComp.storedStringsArray.Count)
                            {
                                return rpmComp.storedStrings[stringNumber];
                            }
                            else
                            {
                                return "";
                            }
                        };
                    }
                }

                if (tokens.Length > 1 && tokens[0] == "PERSISTENT")
                {
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        string substring = variable.Substring("PERSISTENT".Length + 1);
                        if (rpmComp != null)
                        {
                            if (rpmComp.HasPersistentVariable(substring))
                            {
                                return rpmComp.GetPersistentVariable(substring, 0.0f).MassageToFloat();
                            }
                            else
                            {
                                return -1.0f;
                            }
                        }
                        else
                        {
                            return -1.0f;
                        }
                    };
                }

                if (tokens.Length == 2 && tokens[0] == "PLUGIN")
                {
                    Delegate pluginMethod = GetInternalMethod(tokens[1]);
                    if (pluginMethod != null)
                    {
                        MethodInfo mi = pluginMethod.Method;
                        if (mi.ReturnType == typeof(bool))
                        {
                            Func<bool> method = (Func<bool>)pluginMethod;
                            return (string variable, RasterPropMonitorComputer rpmComp) => { return method().GetHashCode(); };
                        }
                        else if (mi.ReturnType == typeof(double))
                        {
                            Func<double> method = (Func<double>)pluginMethod;
                            return (string variable, RasterPropMonitorComputer rpmComp) => { return method(); };
                        }
                        else if (mi.ReturnType == typeof(string))
                        {
                            Func<string> method = (Func<string>)pluginMethod;
                            return (string variable, RasterPropMonitorComputer rpmComp) => { return method(); };
                        }
                        else
                        {
                            JUtil.LogErrorMessage(this, "Unable to create a plugin handler for return type {0}", mi.ReturnType);
                            return (string variable, RasterPropMonitorComputer rpmComp) => { return variable; };

                        }
                    }

                    string[] internalModule = tokens[1].Split(':');
                    if (internalModule.Length != 2)
                    {
                        JUtil.LogErrorMessage(this, "Badly-formed plugin name in {0}", input);
                        return (string variable, RasterPropMonitorComputer rpmComp) => { return variable; };
                    }

                    InternalProp propToUse = null;
                    foreach (InternalProp thisProp in part.internalModel.props)
                    {
                        foreach (InternalModule module in thisProp.internalModules)
                        {
                            if (module != null && module.ClassName == internalModule[0])
                            {
                                propToUse = thisProp;
                                break;
                            }
                        }
                    }

                    if (propToUse == null)
                    {
                        JUtil.LogErrorMessage(this, "Tried to look for method with propToUse still null?");
                        return (string variable, RasterPropMonitorComputer rpmComp) => { return -1; };
                    }
                    else
                    {
                        Func<bool> pluginCall = (Func<bool>)JUtil.GetMethod(tokens[1], propToUse, typeof(Func<bool>));
                        if (pluginCall == null)
                        {
                            Func<double> pluginNumericCall = (Func<double>)JUtil.GetMethod(tokens[1], propToUse, typeof(Func<double>));
                            if (pluginNumericCall != null)
                            {
                                return (string variable, RasterPropMonitorComputer rpmComp) => { return pluginNumericCall(); };
                            }
                            else
                            {
                                // Doesn't exist -- return nothing
                                return (string variable, RasterPropMonitorComputer rpmComp) => { return -1; };
                            }
                        }
                        else
                        {
                            return (string variable, RasterPropMonitorComputer rpmComp) => { return pluginCall().GetHashCode(); };
                        }
                    }
                }
            }

            if (input.StartsWith("AGMEMO", StringComparison.Ordinal))
            {
                cacheable = true;
                return (string variable, RasterPropMonitorComputer rpmComp) =>
                {
                    uint groupID;
                    if (uint.TryParse(variable.Substring(6), out groupID) && groupID < 10)
                    {
                        string[] tokens;
                        if (RPMVesselComputer.actionGroupMemo[groupID].IndexOf('|') > 1 && (tokens = RPMVesselComputer.actionGroupMemo[groupID].Split('|')).Length == 2)
                        {
                            if (vessel.ActionGroups.groups[RPMVesselComputer.actionGroupID[groupID]])
                                return tokens[0];
                            return tokens[1];
                        }
                        return RPMVesselComputer.actionGroupMemo[groupID];
                    }
                    return input;
                };
            }
            // Action group state.
            if (input.StartsWith("AGSTATE", StringComparison.Ordinal))
            {
                cacheable = true;
                return (string variable, RasterPropMonitorComputer rpmComp) =>
                {
                    uint groupID;
                    if (uint.TryParse(variable.Substring(7), out groupID) && groupID < 10)
                    {
                        return (vessel.ActionGroups.groups[RPMVesselComputer.actionGroupID[groupID]]).GetHashCode();
                    }
                    return input;
                };
            }

            return comp.GetEvaluator(input, out cacheable);
        }

        /// <summary>
        /// Creates a new PluginEvaluator object for the method supplied (if
        /// the method exists), attached to an IJSIModule.
        /// </summary>
        /// <param name="packedMethod"></param>
        /// <returns></returns>
        internal Delegate GetInternalMethod(string packedMethod)
        {
            string[] tokens = packedMethod.Split(':');
            if (tokens.Length != 2 || string.IsNullOrEmpty(tokens[0]) || string.IsNullOrEmpty(tokens[1]))
            {
                JUtil.LogErrorMessage(this, "Bad format on {0}", packedMethod);
                throw new ArgumentException("stateMethod incorrectly formatted");
            }

            // Backwards compatibility:
            if (tokens[0] == "MechJebRPMButtons")
            {
                tokens[0] = "JSIMechJeb";
            }
            else if (tokens[0] == "JSIGimbal")
            {
                tokens[0] = "JSIInternalRPMButtons";
            }
            IJSIModule jsiModule = null;
            foreach (IJSIModule module in installedModules)
            {
                if (module.GetType().Name == tokens[0])
                {
                    jsiModule = module;
                    break;
                }
            }

            //JUtil.LogMessage(this, "searching for {0} : {1}", tokens[0], tokens[1]);
            Delegate pluginEval = null;
            if (jsiModule != null)
            {
                foreach (MethodInfo m in jsiModule.GetType().GetMethods())
                {
                    if (m.Name == tokens[1])
                    {
                        //JUtil.LogMessage(this, "Found method {1}: return type is {0}, IsStatic is {2}, with {3} parameters", m.ReturnType, tokens[1],m.IsStatic, m.GetParameters().Length);
                        ParameterInfo[] parms = m.GetParameters();
                        if (parms.Length > 0)
                        {
                            JUtil.LogErrorMessage(this, "GetInternalMethod failed: {1} parameters in plugin method {0}", packedMethod, parms.Length);
                            return null;
                        }

                        if (m.ReturnType == typeof(bool))
                        {
                            try
                            {
                                pluginEval = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<bool>), m) : Delegate.CreateDelegate(typeof(Func<bool>), jsiModule, m);
                            }
                            catch (Exception e)
                            {
                                JUtil.LogErrorMessage(this, "Failed creating a delegate for {0}: {1}", packedMethod, e);
                            }
                        }
                        else if (m.ReturnType == typeof(double))
                        {
                            try
                            {
                                pluginEval = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<double>), m) : Delegate.CreateDelegate(typeof(Func<double>), jsiModule, m);
                            }
                            catch (Exception e)
                            {
                                JUtil.LogErrorMessage(this, "Failed creating a delegate for {0}: {1}", packedMethod, e);
                            }
                        }
                        else if (m.ReturnType == typeof(string))
                        {
                            try
                            {
                                pluginEval = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<string>), m) : Delegate.CreateDelegate(typeof(Func<string>), jsiModule, m);
                            }
                            catch (Exception e)
                            {
                                JUtil.LogErrorMessage(this, "Failed creating a delegate for {0}: {1}", packedMethod, e);
                            }
                        }
                        else
                        {
                            JUtil.LogErrorMessage(this, "I need to support a return type of {0}", m.ReturnType);
                            throw new Exception("Not Implemented");
                        }
                    }
                }

                if (pluginEval == null)
                {
                    JUtil.LogErrorMessage(this, "I failed to find the method for {0}:{1}", tokens[0], tokens[1]);
                }
            }

            return pluginEval;
        }
    }
}
