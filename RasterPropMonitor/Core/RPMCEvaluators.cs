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
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JSI
{
    public partial class RasterPropMonitorComputer : PartModule
    {
        private RPMVesselComputer.VariableEvaluator sideSlipEvaluator;
        internal float Sideslip
        {
            get
            {
                if (sideSlipEvaluator == null)
                {
                    sideSlipEvaluator = SideSlip();
                }
                return sideSlipEvaluator(string.Empty, this).MassageToFloat();
            }
        }

        private RPMVesselComputer.VariableEvaluator angleOfAttackEvaluator;
        internal float AbsoluteAoA
        {
            get
            {
                if (angleOfAttackEvaluator == null)
                {
                    angleOfAttackEvaluator = AngleOfAttack();
                }

                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                return ((comp.RotationVesselSurface.eulerAngles.x > 180.0f) ? (360.0f - comp.RotationVesselSurface.eulerAngles.x) : -comp.RotationVesselSurface.eulerAngles.x) - angleOfAttackEvaluator(string.Empty, this).MassageToFloat();
            }
        }

        #region evaluator
        internal RPMVesselComputer.VariableEvaluator GetEvaluator(string input, RPMVesselComputer comp, out bool cacheable)
        {
            cacheable = true;

            if (input.IndexOf("_", StringComparison.Ordinal) > -1)
            {
                string[] tokens = input.Split('_');

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

            // Handle many/most variables
            switch (input)
            {
                // Speeds.
                case "VERTSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.speedVertical; 
                    };
                case "VERTSPEEDLOG10":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return JUtil.PseudoLog10(vcomp.speedVertical); 
                    };
                case "VERTSPEEDROUNDED":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.speedVerticalRounded; 
                    };
                case "RADARALTVERTSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.radarAltitudeRate; 
                    };
                case "TERMINALVELOCITY":
                    return (string variable, RasterPropMonitorComputer rpmComp) => { return TerminalVelocity(); };
                case "SURFSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) => { return vessel.srfSpeed; };
                case "SURFSPEEDMACH":
                    // Mach number wiggles around 1e-7 when sitting in launch
                    // clamps before launch, so pull it down to zero if it's close.
                    return (string variable, RasterPropMonitorComputer rpmComp) => { return (vessel.mach < 0.001) ? 0.0 : vessel.mach; };
                case "ORBTSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) => { return vessel.orbit.GetVel().magnitude; };
                case "TRGTSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.velocityRelativeTarget.magnitude; 
                    };
                case "HORZVELOCITY":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.speedHorizontal; 
                    };
                case "HORZVELOCITYFORWARD":
                    // Negate it, since this is actually movement on the Z axis,
                    // and we want to treat it as a 2D projection on the surface
                    // such that moving "forward" has a positive value.
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return -Vector3d.Dot(vessel.srf_velocity, vcomp.SurfaceForward); 
                    };
                case "HORZVELOCITYRIGHT":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return Vector3d.Dot(vessel.srf_velocity, vcomp.SurfaceRight); 
                    };
                case "EASPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        double densityRatio = (AeroExtensions.GetCurrentDensity(vessel) / 1.225);
                        return vessel.srfSpeed * Math.Sqrt(densityRatio);
                    };
                case "IASPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        double densityRatio = (AeroExtensions.GetCurrentDensity(vessel) / 1.225);
                        double pressureRatio = AeroExtensions.StagnationPressureCalc(vessel.mainBody, vessel.mach);
                        return vessel.srfSpeed * Math.Sqrt(densityRatio) * pressureRatio;
                    };
                case "APPROACHSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.approachSpeed; 
                    };
                case "SELECTEDSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        switch (FlightGlobals.speedDisplayMode)
                        {
                            case FlightGlobals.SpeedDisplayModes.Orbit:
                                return vessel.orbit.GetVel().magnitude;
                            case FlightGlobals.SpeedDisplayModes.Surface:
                                return vessel.srfSpeed;
                            case FlightGlobals.SpeedDisplayModes.Target:
                                RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                                return vcomp.velocityRelativeTarget.magnitude;
                        }
                        return double.NaN;
                    };
                case "TGTRELX":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        if (FlightGlobals.fetch.VesselTarget != null)
                        {
                            return Vector3d.Dot(FlightGlobals.ship_tgtVelocity, FlightGlobals.ActiveVessel.ReferenceTransform.right);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };

                case "TGTRELY":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        if (FlightGlobals.fetch.VesselTarget != null)
                        {
                            return Vector3d.Dot(FlightGlobals.ship_tgtVelocity, FlightGlobals.ActiveVessel.ReferenceTransform.forward);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "TGTRELZ":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        if (FlightGlobals.fetch.VesselTarget != null)
                        {
                            return Vector3d.Dot(FlightGlobals.ship_tgtVelocity, FlightGlobals.ActiveVessel.ReferenceTransform.up);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                
                case "TIMETOIMPACTSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp) => { return TimeToImpact(); };
                case "SPEEDATIMPACT":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.SpeedAtImpact(vcomp.totalCurrentThrust); 
                    };
                case "BESTSPEEDATIMPACT":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.SpeedAtImpact(vcomp.totalLimitedMaximumThrust); 
                    };
                case "SUICIDEBURNSTARTSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        if (vessel.orbit.PeA > 0.0)
                        {
                            return double.NaN;
                        }
                        else
                        {
                            RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                            return vcomp.SuicideBurnCountdown();
                        }
                    };

                case "LATERALBRAKEDISTANCE":
                    // (-(SHIP:SURFACESPEED)^2)/(2*(ship:maxthrust/ship:mass)) 
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        if (vcomp.totalLimitedMaximumThrust <= 0.0)
                        {
                            // It should be impossible for wet mass to be zero.
                            return -1.0;
                        }
                        return (vcomp.speedHorizontal * vcomp.speedHorizontal) / (2.0 * vcomp.totalLimitedMaximumThrust / vcomp.totalShipWetMass);
                    };
                    //...
                case "DYNAMICPRESSURE":
                    return DynamicPressure();
                    //...

                // Masses.
                case "MASSDRY":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.totalShipDryMass; 
                    };
                case "MASSWET":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.totalShipWetMass; 
                    };
                case "MASSRESOURCES":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.totalShipWetMass - vcomp.totalShipDryMass; 
                    };
                case "MASSPROPELLANT":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.resources.PropellantMass(false); 
                    };
                case "MASSPROPELLANTSTAGE":
                    return (string variable, RasterPropMonitorComputer rpmComp) => 
                    {
                        RPMVesselComputer vcomp = RPMVesselComputer.Instance(rpmComp.vessel);
                        return vcomp.resources.PropellantMass(true); 
                    };

                // The delta V calculation.
                case "DELTAV":
                    return DeltaV();
                case "DELTAVSTAGE":
                    return DeltaVStage();
                    //...
                case "DRAG":
                    return DragForce();
                case "DRAGACCEL":
                    return DragAccel();
                case "LIFT":
                    return LiftForce();
                case "LIFTACCEL":
                    return LiftAccel();
                case "ANGLEOFATTACK":
                    return AngleOfAttack();
                case "SIDESLIP":
                    return SideSlip();
                case "PREDICTEDLANDINGALTITUDE":
                    return LandingAltitude();
                case "PREDICTEDLANDINGLATITUDE":
                    return LandingLatitude();
                case "PREDICTEDLANDINGLONGITUDE":
                    return LandingLongitude();
                case "PREDICTEDLANDINGERROR":
                    return LandingError();
                    //...
                case "SPEEDDISPLAYMODE":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        switch (FlightGlobals.speedDisplayMode)
                        {
                            case FlightGlobals.SpeedDisplayModes.Orbit:
                                return 1d;
                            case FlightGlobals.SpeedDisplayModes.Surface:
                                return 0d;
                            case FlightGlobals.SpeedDisplayModes.Target:
                                return -1d;
                        }
                        return double.NaN;
                    };
                case "ISONKERBINTIME":
                    return (string variable, RasterPropMonitorComputer rpmComp) => { return GameSettings.KERBIN_TIME.GetHashCode(); };
                case "ISDOCKINGPORTREFERENCE":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        ModuleDockingNode thatPort = null;
                        Part referencePart = rpmComp.vessel.GetReferenceTransformPart();
                        if (referencePart != null)
                        {
                            foreach (PartModule thatModule in referencePart.Modules)
                            {
                                thatPort = thatModule as ModuleDockingNode;
                                if (thatPort != null)
                                    break;
                            }
                        }
                        if (thatPort != null)
                            return 1d;
                        return 0d;
                    };
                case "ISCLAWREFERENCE":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        ModuleGrappleNode thatClaw = null;
                        Part referencePart = rpmComp.vessel.GetReferenceTransformPart();
                        if (referencePart != null)
                        {
                            foreach (PartModule thatModule in referencePart.Modules)
                            {
                                thatClaw = thatModule as ModuleGrappleNode;
                                if (thatClaw != null)
                                    break;
                            }
                        }
                        if (thatClaw != null)
                            return 1d;
                        return 0d;
                    };
                case "ISREMOTEREFERENCE":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        ModuleCommand thatPod = null;
                        Part referencePart = rpmComp.vessel.GetReferenceTransformPart();
                        if (referencePart != null)
                        {
                            foreach (PartModule thatModule in referencePart.Modules)
                            {
                                thatPod = thatModule as ModuleCommand;
                                if (thatPod != null)
                                    break;
                            }
                        }
                        if (thatPod == null)
                            return 1d;
                        return 0d;
                    };
                case "FLIGHTUIMODE":
                    return (string variable, RasterPropMonitorComputer rpmComp) =>
                    {
                        switch (FlightUIModeController.Instance.Mode)
                        {
                            case FlightUIMode.DOCKING:
                                return 1d;
                            case FlightUIMode.STAGING:
                                return -1d;
                            case FlightUIMode.ORBITAL:
                                return 0d;
                        }
                        return double.NaN;
                    };

                // Meta.
                case "RPMVERSION":
                    return (string variable, RasterPropMonitorComputer rpmComp) => { return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion; };
                case "MECHJEBAVAILABLE":
                    return MechJebAvailable();
            }

            // If we made it here, it's in the comp.
            return comp.GetEvaluator(input, out cacheable);
        }
        #endregion

        #region delegation
        /// <summary>
        /// Get a plugin or internal method.
        /// </summary>
        /// <param name="packedMethod">The method to fetch in the format ModuleName:MethodName</param>
        /// <param name="internalProp">The internal prop that should be used to instantiate InternalModule plugin methods.</param>
        /// <param name="delegateType">The expected signature of the method.</param>
        /// <returns></returns>
        public Delegate GetMethod(string packedMethod, InternalProp internalProp, Type delegateType)
        {
            Delegate returnValue = GetInternalMethod(packedMethod, delegateType);
            if (returnValue == null && internalProp != null)
            {
                returnValue = JUtil.GetMethod(packedMethod, internalProp, delegateType);
            }

            return returnValue;
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

        /// <summary>
        /// Get an internal method (one that is built into an IJSIModule)
        /// </summary>
        /// <param name="packedMethod"></param>
        /// <param name="delegateType"></param>
        /// <returns></returns>
        public Delegate GetInternalMethod(string packedMethod, Type delegateType)
        {
            string[] tokens = packedMethod.Split(':');
            if (tokens.Length != 2)
            {
                JUtil.LogErrorMessage(this, "Bad format on {0}", packedMethod);
                throw new ArgumentException("stateMethod incorrectly formatted");
            }

            // Backwards compatibility:
            if (tokens[0] == "MechJebRPMButtons")
            {
                tokens[0] = "JSIMechJeb";
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

            Delegate stateCall = null;
            if (jsiModule != null)
            {
                var methodInfo = delegateType.GetMethod("Invoke");
                Type returnType = methodInfo.ReturnType;
                foreach (MethodInfo m in jsiModule.GetType().GetMethods())
                {
                    if (!string.IsNullOrEmpty(tokens[1]) && m.Name == tokens[1] && IsEquivalent(m, methodInfo))
                    {
                        if (m.IsStatic)
                        {
                            stateCall = Delegate.CreateDelegate(delegateType, m);
                        }
                        else
                        {
                            stateCall = Delegate.CreateDelegate(delegateType, jsiModule, m);
                        }
                    }
                }
            }

            return stateCall;
        }

        /// <summary>
        /// Returns whether two methods are effectively equal
        /// </summary>
        /// <param name="method1"></param>
        /// <param name="method2"></param>
        /// <returns></returns>
        private static bool IsEquivalent(MethodInfo method1, MethodInfo method2)
        {
            if (method1.ReturnType == method2.ReturnType)
            {
                var m1Parms = method1.GetParameters();
                var m2Parms = method2.GetParameters();
                if (m1Parms.Length == m2Parms.Length)
                {
                    for (int i = 0; i < m1Parms.Length; ++i)
                    {
                        if (m1Parms[i].GetType() != m2Parms[i].GetType())
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region pluginevaluators
        //private Func<double> evaluateAngleOfAttack;
        //private Func<double> evaluateSideSlip;
        private Func<double> evaluateTerminalVelocity;
        private bool evaluateTerminalVelocityReady;
        private Func<double> evaluateTimeToImpact;
        private bool evaluateTimeToImpactReady;

        //private float EvaluateAngleOfAttack()
        //{
        //    if (evaluateAngleOfAttack == null)
        //    {
        //        Func<double> accessor = null;

        //        accessor = (Func<double>)GetInternalMethod("JSIFAR:GetAngleOfAttack", typeof(Func<double>));
        //        if (accessor != null)
        //        {
        //            double value = accessor();
        //            if (double.IsNaN(value))
        //            {
        //                accessor = null;
        //            }
        //        }

        //        if (accessor == null)
        //        {
        //            evaluateAngleOfAttack = FallbackEvaluateAngleOfAttack;
        //        }
        //        else
        //        {
        //            evaluateAngleOfAttack = accessor;
        //        }
        //    }

        //    return (float)evaluateAngleOfAttack();
        //}

        private RPMVesselComputer.VariableEvaluator AngleOfAttack()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIFAR:GetAngleOfAttack", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return comp.FallbackEvaluateAngleOfAttack(); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
            }
        }

        private RPMVesselComputer.VariableEvaluator DeltaV()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetDeltaV", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return (comp.actualAverageIsp * RPMVesselComputer.gee) * Math.Log(comp.totalShipWetMass / (comp.totalShipWetMass - comp.resources.PropellantMass(false))); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
            }
        }

        private RPMVesselComputer.VariableEvaluator DeltaVStage()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetStageDeltaV", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return (comp.actualAverageIsp * RPMVesselComputer.gee) * Math.Log(comp.totalShipWetMass / (comp.totalShipWetMass - comp.resources.PropellantMass(true))); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
            }
        }

        private RPMVesselComputer.VariableEvaluator DragAccel()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIFAR:GetDragForce", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return comp.FallbackEvaluateDragForce() / comp.totalShipWetMass; 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return accessor() / comp.totalShipWetMass; 
                };
            }
        }

        private RPMVesselComputer.VariableEvaluator DragForce()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIFAR:GetDragForce", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return comp.FallbackEvaluateDragForce(); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
            }
        }

        private RPMVesselComputer.VariableEvaluator DynamicPressure()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIFAR:GetDynamicPressure", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return vessel.dynamicPressurekPa; };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
            }
        }

        private RPMVesselComputer.VariableEvaluator LandingError()
        {
            Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingError", typeof(Func<double>));

            return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        }

        private RPMVesselComputer.VariableEvaluator LandingAltitude()
        {
            Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingAltitude", typeof(Func<double>));

            return (string variable, RasterPropMonitorComputer rpmComp) =>
            {
                double est = accessor();
                RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                return (est == 0.0) ? comp.estLandingAltitude : est;
            };
        }

        private RPMVesselComputer.VariableEvaluator LandingLatitude()
        {
            Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLatitude", typeof(Func<double>));

            return (string variable, RasterPropMonitorComputer rpmComp) =>
            {
                double est = accessor();
                RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                return (est == 0.0) ? comp.estLandingLatitude : est;
            };
        }

        private RPMVesselComputer.VariableEvaluator LandingLongitude()
        {
            Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLongitude", typeof(Func<double>));

            return (string variable, RasterPropMonitorComputer rpmComp) =>
            {
                double est = accessor();
                RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                return (est == 0.0) ? comp.estLandingLongitude : est;
            };
        }

        private RPMVesselComputer.VariableEvaluator LiftAccel()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIFAR:GetLiftForce", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return comp.FallbackEvaluateLiftForce() / comp.totalShipWetMass; 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return accessor() / comp.totalShipWetMass; 
                };
            }
        }

        private RPMVesselComputer.VariableEvaluator LiftForce()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIFAR:GetLiftForce", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return comp.FallbackEvaluateLiftForce(); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
            }
        }

        private RPMVesselComputer.VariableEvaluator MechJebAvailable()
        {
            Func<bool> accessor = null;

            accessor = (Func<bool>)GetInternalMethod("JSIMechJeb:GetMechJebAvailable", typeof(Func<bool>));
            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return false; };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor().GetHashCode(); };
            }
        }

        //private float EvaluateSideSlip()
        //{
        //    if (evaluateSideSlip == null)
        //    {
        //        Func<double> accessor = null;

        //        accessor = (Func<double>)GetInternalMethod("JSIFAR:GetSideSlip", typeof(Func<double>));
        //        if (accessor != null)
        //        {
        //            double value = accessor();
        //            if (double.IsNaN(value))
        //            {
        //                accessor = null;
        //            }
        //        }

        //        if (accessor == null)
        //        {
        //            evaluateSideSlip = FallbackEvaluateSideSlip;
        //        }
        //        else
        //        {
        //            evaluateSideSlip = accessor;
        //        }
        //    }

        //    return (float)evaluateSideSlip();
        //}

        private RPMVesselComputer.VariableEvaluator SideSlip()
        {
            Func<double> accessor = null;

            accessor = (Func<double>)GetInternalMethod("JSIFAR:GetSideSlip", typeof(Func<double>));
            if (accessor != null)
            {
                double value = accessor();
                if (double.IsNaN(value))
                {
                    accessor = null;
                }
            }

            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => 
                {
                    RPMVesselComputer comp = RPMVesselComputer.Instance(rpmComp.vessel);
                    return comp.FallbackEvaluateSideSlip(); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
            }
        }

        internal double TerminalVelocity()
        {
            if (evaluateTerminalVelocityReady == false)
            {
                Func<double> accessor = null;

                accessor = (Func<double>)GetInternalMethod("JSIFAR:GetTerminalVelocity", typeof(Func<double>));
                if (accessor != null)
                {
                    double value = accessor();
                    if (value < 0.0)
                    {
                        accessor = null;
                    }
                }

                if (accessor == null)
                {
                    accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetTerminalVelocity", typeof(Func<double>));
                    double value = accessor();
                    if (double.IsNaN(value))
                    {
                        accessor = null;
                    }
                }

                evaluateTerminalVelocity = accessor;
                evaluateTerminalVelocityReady = true;
            }

            if (evaluateTerminalVelocity == null)
            {
                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                return comp.FallbackEvaluateTerminalVelocity();
            }
            else
            {
                return evaluateTerminalVelocity();
            }
        }

        private double TimeToImpact()
        {
            if (evaluateTimeToImpactReady == false)
            {
                Func<double> accessor = null;

                if (accessor == null)
                {
                    accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingTime", typeof(Func<double>));
                    double value = accessor();
                    if (double.IsNaN(value))
                    {
                        accessor = null;
                    }
                }

                evaluateTimeToImpact = accessor;

                evaluateTimeToImpactReady = true;
            }

            double timeToImpact;
            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);

            if (evaluateTimeToImpact != null)
            {
                timeToImpact = evaluateTimeToImpact();
            }
            else
            {
                timeToImpact = comp.FallbackEvaluateTimeToImpact();
            }

            if (double.IsNaN(timeToImpact) || timeToImpact > 365.0 * 24.0 * 60.0 * 60.0 || timeToImpact < 0.0)
            {
                timeToImpact = -1.0;
            }
            else if (timeToImpact == 0.0)
            {
                return comp.estLandingUT - Planetarium.GetUniversalTime();
            }
            return timeToImpact;
        }
        #endregion
    }
}
