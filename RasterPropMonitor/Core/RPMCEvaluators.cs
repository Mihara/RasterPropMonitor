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
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JSI
{
    public partial class RasterPropMonitorComputer : PartModule
    {
        private VariableEvaluator sideSlipEvaluator;
        internal float Sideslip
        {
            get
            {
                if (sideSlipEvaluator == null)
                {
                    sideSlipEvaluator = SideSlip();
                }
                RPMVesselComputer comp = RPMVesselComputer.Instance(vid);
                return sideSlipEvaluator(string.Empty, this, comp).MassageToFloat();
            }
        }

        private VariableEvaluator angleOfAttackEvaluator;
        internal float AbsoluteAoA
        {
            get
            {
                if (angleOfAttackEvaluator == null)
                {
                    angleOfAttackEvaluator = AngleOfAttack();
                }

                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                return ((comp.RotationVesselSurface.eulerAngles.x > 180.0f) ? (360.0f - comp.RotationVesselSurface.eulerAngles.x) : -comp.RotationVesselSurface.eulerAngles.x) - angleOfAttackEvaluator(string.Empty, this, comp).MassageToFloat();
            }
        }

        #region evaluator
        //static // uncomment this to make sure there's no non-static methods being generated
        internal VariableEvaluator GetEvaluator(string input, out bool cacheable)
        {
            cacheable = true;

            if (input.IndexOf("_", StringComparison.Ordinal) > -1)
            {
                string[] tokens = input.Split('_');
                
                // MOARdV TODO: Restructure this as a switch statement on tokens[0]

                // Is loaded?
                if (tokens.Length >= 2 && tokens[0] == "ISLOADED")
                {
                    string assemblyname = input.Substring(input.IndexOf("_", StringComparison.Ordinal) + 1);

                    if (RPMGlobals.knownLoadedAssemblies.Contains(assemblyname))
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return 1.0f; };
                    }
                    else
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return 0.0f; };
                    }
                }


                if (tokens.Length == 2 && tokens[0] == "SYSR")
                {
                    foreach (KeyValuePair<string, string> resourceType in RPMGlobals.systemNamedResources)
                    {
                        if (tokens[1].StartsWith(resourceType.Key, StringComparison.Ordinal))
                        {
                            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                            {
                                return comp.resources.ListElement(variable); 
                            };
                        }
                    }
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return variable; };
                }

                // If input starts with "LISTR" we're handling it specially -- it's a list of all resources.
                // The variables are named like LISTR_<number>_<NAME|VAL|MAX>
                if (tokens.Length == 3 && tokens[0] == "LISTR")
                {
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        string[] toks = variable.Split('_');
                        ushort resourceID = Convert.ToUInt16(toks[1]);
                        string resourceName = comp.resources.GetActiveResourceByIndex(resourceID);
                        if (toks[2] == "NAME")
                        {
                            return resourceName;
                        }
                        if (string.IsNullOrEmpty(resourceName))
                        {
                            return 0d;
                        }
                        else
                        {
                            return toks[2].StartsWith("STAGE", StringComparison.Ordinal) ?
                                comp.resources.ListElement(resourceName, toks[2].Substring("STAGE".Length), true) :
                                comp.resources.ListElement(resourceName, toks[2], false);
                        }
                    };
                }

                // We do similar things for crew rosters.
                // The syntax is therefore CREW_<index>_<FIRST|LAST|FULL>
                // Part-local crew list is identical but CREWLOCAL_.
                if (tokens.Length == 3 && (tokens[0] == "CREW" || tokens[0] == "CREWLOCAL"))
                {
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        string[] toks = variable.Split('_');
                        ushort crewSeatID = Convert.ToUInt16(toks[1]);
                        switch (toks[0])
                        {
                            case "CREW":
                                return CrewListElement(toks[2], crewSeatID, comp.vesselCrew, comp.vesselCrewMedical);
                            case "CREWLOCAL":
                                return CrewListElement(toks[2], crewSeatID, rpmComp.localCrew, rpmComp.localCrewMedical);
                        }
                        return variable;
                    };
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
                            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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

                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return variable; };
                }

                // Custom variables - if the first token is CUSTOM, MAPPED, MATH, or SELECT, we'll evaluate it here
                if (tokens.Length > 1 && (tokens[0] == "CUSTOM" || tokens[0] == "MAPPED" || tokens[0] == "MATH" || tokens[0] == "SELECT"))
                {
                    if (RPMGlobals.customVariables.ContainsKey(input))
                    {
                        var o = RPMGlobals.customVariables[input];
                        return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return o.Evaluate(rpmComp, comp); };
                    }
                    else
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return variable; };
                    }
                }

                // Strings stored in module configuration.
                if (tokens.Length == 2 && tokens[0] == "STOREDSTRING")
                {
                    int storedStringNumber;
                    if (int.TryParse(tokens[1], out storedStringNumber) && storedStringNumber >= 0)
                    {
                        return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                        return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return method().GetHashCode(); };
                        }
                        else if (mi.ReturnType == typeof(double))
                        {
                            Func<double> method = (Func<double>)pluginMethod;
                            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return method(); };
                        }
                        else if (mi.ReturnType == typeof(string))
                        {
                            Func<string> method = (Func<string>)pluginMethod;
                            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return method(); };
                        }
                        else
                        {
                            JUtil.LogErrorMessage(this, "Unable to create a plugin handler for return type {0}", mi.ReturnType);
                            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return variable; };

                        }
                    }

                    string[] internalModule = tokens[1].Split(':');
                    if (internalModule.Length != 2)
                    {
                        JUtil.LogErrorMessage(this, "Badly-formed plugin name in {0}", input);
                        return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return variable; };
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
                        return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return -1; };
                    }
                    else
                    {
                        Func<bool> pluginCall = (Func<bool>)JUtil.GetMethod(tokens[1], propToUse, typeof(Func<bool>));
                        if (pluginCall == null)
                        {
                            Func<double> pluginNumericCall = (Func<double>)JUtil.GetMethod(tokens[1], propToUse, typeof(Func<double>));
                            if (pluginNumericCall != null)
                            {
                                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return pluginNumericCall(); };
                            }
                            else
                            {
                                // Doesn't exist -- return nothing
                                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return -1; };
                            }
                        }
                        else
                        {
                            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return pluginCall().GetHashCode(); };
                        }
                    }
                }
            }

            if (input.StartsWith("AGMEMO", StringComparison.Ordinal))
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                {
                    uint groupID;
                    if (uint.TryParse(variable.Substring(6), out groupID) && groupID < 10)
                    {
                        string[] tokens;
                        if (RPMVesselComputer.actionGroupMemo[groupID].IndexOf('|') > 1 && (tokens = RPMVesselComputer.actionGroupMemo[groupID].Split('|')).Length == 2)
                        {
                            if (rpmComp.vessel.ActionGroups.groups[RPMVesselComputer.actionGroupID[groupID]])
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                {
                    uint groupID;
                    if (uint.TryParse(variable.Substring(7), out groupID) && groupID < 10)
                    {
                        return (rpmComp.vessel.ActionGroups.groups[RPMVesselComputer.actionGroupID[groupID]]).GetHashCode();
                    }
                    return input;
                };
            }

            // Handle many/most variables
            switch (input)
            {
                // Speeds.
                case "VERTSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.speedVertical; 
                    };
                case "VERTSPEEDLOG10":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return JUtil.PseudoLog10(comp.speedVertical); 
                    };
                case "VERTSPEEDROUNDED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.speedVerticalRounded; 
                    };
                case "RADARALTVERTSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.radarAltitudeRate; 
                    };
                case "TERMINALVELOCITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    { 
                        return rpmComp.TerminalVelocity(); 
                    };
                case "SURFSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.srfSpeed; };
                case "SURFSPEEDMACH":
                    // Mach number wiggles around 1e-7 when sitting in launch
                    // clamps before launch, so pull it down to zero if it's close.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp.vessel.mach < 0.001) ? 0.0 : rpmComp.vessel.mach; };
                case "ORBTSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.GetVel().magnitude; };
                case "TRGTSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.velocityRelativeTarget.magnitude; 
                    };
                case "HORZVELOCITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.speedHorizontal; 
                    };
                case "HORZVELOCITYFORWARD":
                    // Negate it, since this is actually movement on the Z axis,
                    // and we want to treat it as a 2D projection on the surface
                    // such that moving "forward" has a positive value.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return -Vector3d.Dot(rpmComp.vessel.srf_velocity, comp.SurfaceForward); 
                    };
                case "HORZVELOCITYRIGHT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return Vector3d.Dot(rpmComp.vessel.srf_velocity, comp.SurfaceRight); 
                    };
                case "EASPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        double densityRatio = (AeroExtensions.GetCurrentDensity(rpmComp.vessel) / 1.225);
                        return rpmComp.vessel.srfSpeed * Math.Sqrt(densityRatio);
                    };
                case "IASPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        double densityRatio = (AeroExtensions.GetCurrentDensity(rpmComp.vessel) / 1.225);
                        double pressureRatio = AeroExtensions.StagnationPressureCalc(rpmComp.vessel.mainBody, rpmComp.vessel.mach);
                        return rpmComp.vessel.srfSpeed * Math.Sqrt(densityRatio) * pressureRatio;
                    };
                case "APPROACHSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.approachSpeed; 
                    };
                case "SELECTEDSPEED":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        switch (FlightGlobals.speedDisplayMode)
                        {
                            case FlightGlobals.SpeedDisplayModes.Orbit:
                                return rpmComp.vessel.orbit.GetVel().magnitude;
                            case FlightGlobals.SpeedDisplayModes.Surface:
                                return rpmComp.vessel.srfSpeed;
                            case FlightGlobals.SpeedDisplayModes.Target:
                                return comp.velocityRelativeTarget.magnitude;
                        }
                        return double.NaN;
                    };
                case "TGTRELX":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.TimeToImpact(); };
                case "SPEEDATIMPACT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.SpeedAtImpact(comp.totalCurrentThrust); 
                    };
                case "BESTSPEEDATIMPACT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.SpeedAtImpact(comp.totalLimitedMaximumThrust); 
                    };
                case "SUICIDEBURNSTARTSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.vessel.orbit.PeA > 0.0)
                        {
                            return double.NaN;
                        }
                        else
                        {
                            return comp.SuicideBurnCountdown();
                        }
                    };

                case "LATERALBRAKEDISTANCE":
                    // (-(SHIP:SURFACESPEED)^2)/(2*(ship:maxthrust/ship:mass)) 
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.totalLimitedMaximumThrust <= 0.0)
                        {
                            // It should be impossible for wet mass to be zero.
                            return -1.0;
                        }
                        return (comp.speedHorizontal * comp.speedHorizontal) / (2.0 * comp.totalLimitedMaximumThrust / comp.totalShipWetMass);
                    };

                // Altitudes
                case "ALTITUDE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.altitudeASL;
                    };
                case "ALTITUDELOG10":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return JUtil.PseudoLog10(comp.altitudeASL);
                    };
                case "RADARALT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.altitudeTrue;
                    };
                case "RADARALTLOG10":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return JUtil.PseudoLog10(comp.altitudeTrue);
                    };
                case "RADARALTOCEAN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.vessel.mainBody.ocean)
                        {
                            return Math.Min(comp.altitudeASL, comp.altitudeTrue);
                        }
                        return comp.altitudeTrue;
                    };
                case "RADARALTOCEANLOG10":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.vessel.mainBody.ocean)
                        {
                            return JUtil.PseudoLog10(Math.Min(comp.altitudeASL,comp.altitudeTrue));
                        }
                        return JUtil.PseudoLog10(comp.altitudeTrue);
                    };
                case "ALTITUDEBOTTOM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.altitudeBottom;
                    };
                case "ALTITUDEBOTTOMLOG10":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return JUtil.PseudoLog10(comp.altitudeBottom);
                    };
                case "TERRAINHEIGHT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.terrainAltitude; };
                case "TERRAINDELTA":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.terrainDelta;
                    };
                case "TERRAINHEIGHTLOG10":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return JUtil.PseudoLog10(rpmComp.vessel.terrainAltitude); };
                case "DISTTOATMOSPHERETOP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return rpmComp.vessel.orbit.referenceBody.atmosphereDepth - comp.altitudeASL;
                    };

                // Atmospheric values
                case "ATMPRESSURE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.staticPressurekPa * PhysicsGlobals.KpaToAtmospheres; };
                case "ATMDENSITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.atmDensity; };
                case "DYNAMICPRESSURE":
                    return DynamicPressure();
                case "ATMOSPHEREDEPTH":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.vessel.mainBody.atmosphere)
                        {
                            float depth;
                            try
                            {
                                depth = comp.linearAtmosGauge.gauge.Value.Clamp(0.0f, 1.0f);
                            }
                            catch
                            {
                                depth = (float)((RPMGlobals.upperAtmosphereLimit + Math.Log(FlightGlobals.getAtmDensity(rpmComp.vessel.staticPressurekPa * PhysicsGlobals.KpaToAtmospheres, FlightGlobals.Bodies[1].atmosphereTemperatureSeaLevel) /
                                FlightGlobals.getAtmDensity(FlightGlobals.currentMainBody.atmospherePressureSeaLevel, FlightGlobals.currentMainBody.atmosphereTemperatureSeaLevel))) / RPMGlobals.upperAtmosphereLimit).Clamp(0.0f, 1.0f);
                            }

                            return depth;
                        }
                        else
                        {
                            return 0.0f;
                        }
                    };

                // Masses.
                case "MASSDRY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.totalShipDryMass; 
                    };
                case "MASSWET":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.totalShipWetMass; 
                    };
                case "MASSRESOURCES":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.totalShipWetMass - comp.totalShipDryMass; 
                    };
                case "MASSPROPELLANT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.resources.PropellantMass(false); 
                    };
                case "MASSPROPELLANTSTAGE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.resources.PropellantMass(true); 
                    };

                // The delta V calculation.
                case "DELTAV":
                    return DeltaV();
                case "DELTAVSTAGE":
                    return DeltaVStage();

                // Thrust and related
                case "THRUST":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.totalCurrentThrust;
                    };
                case "THRUSTMAX":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.totalLimitedMaximumThrust;
                    };
                case "THRUSTMAXRAW":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.totalRawMaximumThrust;
                    };
                case "THRUSTLIMIT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.totalRawMaximumThrust > 0.0f) ? comp.totalLimitedMaximumThrust / comp.totalRawMaximumThrust : 0.0f;
                    };
                case "TWR":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.totalCurrentThrust / (comp.totalShipWetMass * comp.localGeeASL));
                    };
                case "TWRMAX":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.totalLimitedMaximumThrust / (comp.totalShipWetMass * comp.localGeeASL));
                    };
                case "ACCEL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.totalCurrentThrust / comp.totalShipWetMass);
                    };
                case "MAXACCEL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.totalLimitedMaximumThrust / comp.totalShipWetMass);
                    };
                case "GFORCE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.geeForce_immediate; };
                case "EFFECTIVEACCEL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.acceleration.magnitude; };
                case "REALISP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.actualAverageIsp;
                    };
                case "MAXISP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.actualMaxIsp;
                    };
                case "CURRENTENGINEFUELFLOW":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.currentEngineFuelFlow;
                    };
                case "MAXENGINEFUELFLOW":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.maxEngineFuelFlow;
                    };
                case "HOVERPOINT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.localGeeDirect / (comp.totalLimitedMaximumThrust / comp.totalShipWetMass)).Clamp(0.0f, 1.0f);
                    };
                case "HOVERPOINTEXISTS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return ((comp.localGeeDirect / (comp.totalLimitedMaximumThrust / comp.totalShipWetMass)) > 1.0f) ? -1.0 : 1.0;
                    };
                case "EFFECTIVERAWTHROTTLE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.totalRawMaximumThrust > 0.0f) ? (comp.totalCurrentThrust / comp.totalRawMaximumThrust) : 0.0f;
                    };
                case "EFFECTIVETHROTTLE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.totalLimitedMaximumThrust > 0.0f) ? (comp.totalCurrentThrust / comp.totalLimitedMaximumThrust) : 0.0f;
                    };
                case "DRAG":
                    return DragForce();
                case "DRAGACCEL":
                    return DragAccel();
                case "LIFT":
                    return LiftForce();
                case "LIFTACCEL":
                    return LiftAccel();
                case "ACCELPROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return Vector3.Dot(rpmComp.vessel.acceleration, comp.prograde);
                    };
                case "ACCELRADIAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return Vector3.Dot(rpmComp.vessel.acceleration, comp.radialOut);
                    };
                case "ACCELNORMAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return Vector3.Dot(rpmComp.vessel.acceleration, comp.normalPlus);
                    };
                case "ACCELSURFPROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return Vector3.Dot(rpmComp.vessel.acceleration, rpmComp.vessel.srf_velocity.normalized); };
                case "ACCELFORWARD":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return Vector3.Dot(rpmComp.vessel.acceleration, comp.forward);
                    };
                case "ACCELRIGHT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return Vector3.Dot(rpmComp.vessel.acceleration, comp.right);
                    };
                case "ACCELTOP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return Vector3.Dot(rpmComp.vessel.acceleration, comp.top);
                    };

                // Maneuvers
                case "MNODETIMESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null)
                        {
                            return -(rpmComp.node.UT - Planetarium.GetUniversalTime());
                        }
                        return double.NaN;
                    };
                case "MNODEDV":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null)
                        {
                            return rpmComp.node.GetBurnVector(rpmComp.vessel.orbit).magnitude;
                        }
                        return 0d;
                    };
                case "MNODEBURNTIMESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null && comp.totalLimitedMaximumThrust > 0 && comp.actualAverageIsp > 0.0f)
                        {
                            return comp.actualAverageIsp * (1.0f - Math.Exp(-rpmComp.node.GetBurnVector(rpmComp.vessel.orbit).magnitude / comp.actualAverageIsp / RPMGlobals.gee)) / (comp.totalLimitedMaximumThrust / (comp.totalShipWetMass * RPMGlobals.gee));
                        }
                        return double.NaN;
                    };
                case "MNODEEXISTS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return rpmComp.node == null ? -1d : 1d;
                    };

                case "MNODEDVPROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null)
                        {
                            Vector3d burnVector = rpmComp.node.GetBurnVector(rpmComp.vessel.orbit);
                            return Vector3d.Dot(burnVector, rpmComp.vessel.orbit.Prograde(rpmComp.node.UT));
                        }
                        return 0.0;
                    };
                case "MNODEDVNORMAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null)
                        {
                            Vector3d burnVector = rpmComp.node.GetBurnVector(rpmComp.vessel.orbit);
                            // NormalPlus seems to be backwards...
                            return -Vector3d.Dot(burnVector, rpmComp.vessel.orbit.NormalPlus(rpmComp.node.UT));
                        }
                        return 0.0;
                    };
                case "MNODEDVRADIAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null)
                        {
                            Vector3d burnVector = rpmComp.node.GetBurnVector(rpmComp.vessel.orbit);
                            return Vector3d.Dot(burnVector, rpmComp.vessel.orbit.RadialPlus(rpmComp.node.UT));
                        }
                        return 0.0;
                    };

                case "MNODEPERIAPSIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null && rpmComp.node.nextPatch != null)
                        {
                            return rpmComp.node.nextPatch.PeA;
                        }
                        return double.NaN;
                    };
                case "MNODEAPOAPSIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null && rpmComp.node.nextPatch != null)
                        {
                            return rpmComp.node.nextPatch.ApA;
                        }
                        return double.NaN;
                    };
                case "MNODEINCLINATION":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null && rpmComp.node.nextPatch != null)
                        {
                            return rpmComp.node.nextPatch.inclination;
                        }
                        return double.NaN;
                    };
                case "MNODEECCENTRICITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null && rpmComp.node.nextPatch != null)
                        {
                            return rpmComp.node.nextPatch.eccentricity;
                        }
                        return double.NaN;
                    };

                case "MNODETARGETCLOSESTAPPROACHTIME":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null || comp.targetOrbit == null || rpmComp.node == null || rpmComp.node.nextPatch == null)
                        {
                            return double.NaN;
                        }
                        else
                        {
                            double approachTime, approachDistance;
                            approachDistance = JUtil.GetClosestApproach(rpmComp.node.nextPatch, comp.target, out approachTime);
                            return approachTime - Planetarium.GetUniversalTime();
                        }
                    };
                case "MNODETARGETCLOSESTAPPROACHDISTANCE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null || comp.targetOrbit == null || rpmComp.node == null || rpmComp.node.nextPatch == null)
                        {
                            return double.NaN;
                        }
                        else
                        {
                            double approachTime;
                            return JUtil.GetClosestApproach(rpmComp.node.nextPatch, comp.target, out approachTime);
                        }
                    };
                case "MNODERELATIVEINCLINATION":
                    // MechJeb's targetables don't have orbits.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null || comp.targetOrbit == null || rpmComp.node == null || rpmComp.node.nextPatch == null)
                        {
                            return double.NaN;
                        }
                        else
                        {
                            return comp.targetOrbit.referenceBody != rpmComp.node.nextPatch.referenceBody ?
                                -1d :
                                Math.Abs(Vector3d.Angle(rpmComp.node.nextPatch.SwappedOrbitNormal(), comp.targetOrbit.SwappedOrbitNormal()));
                        }
                    };

                // Orbital parameters
                case "ORBITBODY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.name; };
                case "PERIAPSIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                            return rpmComp.vessel.orbit.PeA;
                        return double.NaN;
                    };
                case "APOAPSIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                        {
                            return rpmComp.vessel.orbit.ApA;
                        }
                        return double.NaN;
                    };
                case "INCLINATION":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                        {
                            return rpmComp.vessel.orbit.inclination;
                        }
                        return double.NaN;
                    };
                case "ECCENTRICITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                        {
                            return rpmComp.vessel.orbit.eccentricity;
                        }
                        return double.NaN;
                    };
                case "SEMIMAJORAXIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                        {
                            return rpmComp.vessel.orbit.semiMajorAxis;
                        }
                        return double.NaN;
                    };

                case "ORBPERIODSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                            return rpmComp.vessel.orbit.period;
                        return double.NaN;
                    };
                case "TIMETOAPSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                            return rpmComp.vessel.orbit.timeToAp;
                        return double.NaN;
                    };
                case "TIMETOPESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                            return rpmComp.vessel.orbit.eccentricity < 1 ?
                                rpmComp.vessel.orbit.timeToPe :
                                -rpmComp.vessel.orbit.meanAnomaly / (2 * Math.PI / rpmComp.vessel.orbit.period);
                        return double.NaN;
                    };
                case "TIMESINCELASTAP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                            return rpmComp.vessel.orbit.period - rpmComp.vessel.orbit.timeToAp;
                        return double.NaN;
                    };
                case "TIMESINCELASTPE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                            return rpmComp.vessel.orbit.period - (rpmComp.vessel.orbit.eccentricity < 1 ? rpmComp.vessel.orbit.timeToPe : -rpmComp.vessel.orbit.meanAnomaly / (2 * Math.PI / rpmComp.vessel.orbit.period));
                        return double.NaN;
                    };
                case "TIMETONEXTAPSIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                        {
                            double apsisType = NextApsisType(rpmComp.vessel);
                            if (apsisType < 0.0)
                            {
                                return rpmComp.vessel.orbit.eccentricity < 1 ?
                                    rpmComp.vessel.orbit.timeToPe :
                                    -rpmComp.vessel.orbit.meanAnomaly / (2 * Math.PI / rpmComp.vessel.orbit.period);
                            }
                            return rpmComp.vessel.orbit.timeToAp;
                        }
                        return 0.0;
                    };
                case "NEXTAPSIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                        {
                            double apsisType = NextApsisType(rpmComp.vessel);
                            if (apsisType < 0.0)
                            {
                                return rpmComp.vessel.orbit.PeA;
                            }
                            if (apsisType > 0.0)
                            {
                                return rpmComp.vessel.orbit.ApA;
                            }
                        }
                        return double.NaN;
                    };
                case "NEXTAPSISTYPE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return NextApsisType(rpmComp.vessel);
                    };
                case "ORBITMAKESSENSE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                            return 1d;
                        return -1d;
                    };
                case "TIMETOANEQUATORIAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility && rpmComp.vessel.orbit.AscendingNodeEquatorialExists())
                            return rpmComp.vessel.orbit.TimeOfAscendingNodeEquatorial(Planetarium.GetUniversalTime()) - Planetarium.GetUniversalTime();
                        return double.NaN;
                    };
                case "TIMETODNEQUATORIAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility && rpmComp.vessel.orbit.DescendingNodeEquatorialExists())
                            return rpmComp.vessel.orbit.TimeOfDescendingNodeEquatorial(Planetarium.GetUniversalTime()) - Planetarium.GetUniversalTime();
                        return double.NaN;
                    };
                case "TIMETOATMOSPHERESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        double timeToAtm = 0.0;
                        if (rpmComp.orbitSensibility && rpmComp.vessel.orbit.referenceBody.atmosphere == true)
                        {
                            try
                            {
                                double now = Planetarium.GetUniversalTime();
                                timeToAtm = rpmComp.vessel.orbit.NextTimeOfRadius(now, rpmComp.vessel.orbit.referenceBody.atmosphereDepth + rpmComp.vessel.orbit.referenceBody.Radius) - now;
                                timeToAtm = Math.Max(timeToAtm, 0.0);
                            }
                            catch
                            {
                                //...
                            }
                        }
                        return timeToAtm;
                    };

                // SOI changes in orbits.
                case "ENCOUNTEREXISTS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                        {
                            switch (rpmComp.vessel.orbit.patchEndTransition)
                            {
                                case Orbit.PatchTransitionType.ESCAPE:
                                    return -1d;
                                case Orbit.PatchTransitionType.ENCOUNTER:
                                    return 1d;
                            }
                        }
                        return 0d;
                    };
                case "ENCOUNTERTIME":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility &&
                            (rpmComp.vessel.orbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER ||
                            rpmComp.vessel.orbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE))
                        {
                            return rpmComp.vessel.orbit.UTsoi - Planetarium.GetUniversalTime();
                        }
                        return 0.0;
                    };
                case "ENCOUNTERBODY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility)
                        {
                            switch (rpmComp.vessel.orbit.patchEndTransition)
                            {
                                case Orbit.PatchTransitionType.ENCOUNTER:
                                    return rpmComp.vessel.orbit.nextPatch.referenceBody.bodyName;
                                case Orbit.PatchTransitionType.ESCAPE:
                                    return rpmComp.vessel.mainBody.referenceBody.bodyName;
                            }
                        }
                        return string.Empty;
                    };

                // Time
                case "UTSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (GameSettings.KERBIN_TIME)
                        {
                            return Planetarium.GetUniversalTime() + 426 * 6 * 60 * 60;
                        }
                        return Planetarium.GetUniversalTime() + 365 * 24 * 60 * 60;
                    };
                case "TIMEOFDAYSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (GameSettings.KERBIN_TIME)
                        {
                            return Planetarium.GetUniversalTime() % (6.0 * 60.0 * 60.0);
                        }
                        else
                        {
                            return Planetarium.GetUniversalTime() % (24.0 * 60.0 * 60.0);
                        }
                    };
                case "METSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.missionTime; };

                // Names!
                case "NAME":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.vesselName; };
                case "VESSELTYPE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.vesselType.ToString(); };
                case "TARGETTYPE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetVessel != null)
                        {
                            return comp.targetVessel.vesselType.ToString();
                        }
                        if (comp.targetDockingNode != null)
                        {
                            return "Port";
                        }
                        if (comp.targetBody != null)
                        {
                            return "Celestial";
                        }
                        return "Position";
                    };

                // Coordinates.
                case "LATITUDE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return rpmComp.vessel.mainBody.GetLatitude(comp.CoM);
                    };
                case "LONGITUDE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return JUtil.ClampDegrees180(rpmComp.vessel.mainBody.GetLongitude(comp.CoM));
                    };
                case "TARGETLATITUDE":
                case "LATITUDETGT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    { // These targetables definitely don't have any coordinates.
                        if (comp.target == null || comp.target is CelestialBody)
                        {
                            return double.NaN;
                        }
                        // These definitely do.
                        if (comp.target is Vessel || comp.target is ModuleDockingNode)
                        {
                            return comp.target.GetVessel().mainBody.GetLatitude(comp.target.GetTransform().position);
                        }
                        // We're going to take a guess here and expect MechJeb's PositionTarget and DirectionTarget,
                        // which don't have vessel structures but do have a transform.
                        return rpmComp.vessel.mainBody.GetLatitude(comp.target.GetTransform().position);
                    };
                case "TARGETLONGITUDE":
                case "LONGITUDETGT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null || comp.target is CelestialBody)
                        {
                            return double.NaN;
                        }
                        if (comp.target is Vessel || comp.target is ModuleDockingNode)
                        {
                            return JUtil.ClampDegrees180(comp.target.GetVessel().mainBody.GetLongitude(comp.target.GetTransform().position));
                        }
                        return rpmComp.vessel.mainBody.GetLongitude(comp.target.GetTransform().position);
                    };

                // Orientation
                case "HEADING":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.rotationVesselSurface.eulerAngles.y;
                    };
                case "PITCH":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.rotationVesselSurface.eulerAngles.x > 180.0f) ? (360.0f - comp.rotationVesselSurface.eulerAngles.x) : -comp.rotationVesselSurface.eulerAngles.x;
                    };
                case "ROLL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.rotationVesselSurface.eulerAngles.z > 180.0f) ? (360.0f - comp.rotationVesselSurface.eulerAngles.z) : -comp.rotationVesselSurface.eulerAngles.z;
                    };
                case "PITCHRATE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return -rpmComp.vessel.angularVelocity.x * Mathf.Rad2Deg; };
                case "ROLLRATE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return -rpmComp.vessel.angularVelocity.y * Mathf.Rad2Deg; };
                case "YAWRATE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return -rpmComp.vessel.angularVelocity.z * Mathf.Rad2Deg; };
                case "ANGLEOFATTACK":
                    return AngleOfAttack();
                case "SIDESLIP":
                    return SideSlip();

                case "PITCHSURFPROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.GetRelativePitch(rpmComp.vessel.srf_velocity.normalized); 
                    };
                case "PITCHSURFRETROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.GetRelativePitch(-rpmComp.vessel.srf_velocity.normalized);
                    };
                case "PITCHPROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativePitch(comp.prograde);
                    };
                case "PITCHRETROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativePitch(-comp.prograde);
                    };
                case "PITCHRADIALIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativePitch(-comp.radialOut);
                    };
                case "PITCHRADIALOUT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativePitch(comp.radialOut);
                    };
                case "PITCHNORMALPLUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativePitch(comp.normalPlus);
                    };
                case "PITCHNORMALMINUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativePitch(-comp.normalPlus);
                    };
                case "PITCHNODE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null)
                        {
                            return comp.GetRelativePitch(rpmComp.node.GetBurnVector(rpmComp.vessel.orbit).normalized);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "PITCHTARGET":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null)
                        {
                            return comp.GetRelativePitch(-comp.targetSeparation.normalized);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "PITCHTARGETRELPLUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.velocityRelativeTarget.sqrMagnitude > 0.0)
                        {
                            return comp.GetRelativePitch(comp.velocityRelativeTarget.normalized);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "PITCHTARGETRELMINUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.velocityRelativeTarget.sqrMagnitude > 0.0)
                        {
                            return comp.GetRelativePitch(-comp.velocityRelativeTarget.normalized);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "YAWSURFPROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativeYaw(rpmComp.vessel.srf_velocity.normalized);
                    };
                case "YAWSURFRETROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativeYaw(-rpmComp.vessel.srf_velocity.normalized);
                    };
                case "YAWPROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativeYaw(comp.prograde);
                    };
                case "YAWRETROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativeYaw(-comp.prograde);
                    };
                case "YAWRADIALIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativeYaw(-comp.radialOut);
                    };
                case "YAWRADIALOUT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativeYaw(comp.radialOut);
                    };
                case "YAWNORMALPLUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativeYaw(comp.normalPlus);
                    };
                case "YAWNORMALMINUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.GetRelativeYaw(-comp.normalPlus);
                    };
                case "YAWNODE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.node != null)
                        {
                            return comp.GetRelativeYaw(rpmComp.node.GetBurnVector(rpmComp.vessel.orbit).normalized);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "YAWTARGET":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null)
                        {
                            return comp.GetRelativeYaw(-comp.targetSeparation.normalized);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "YAWTARGETRELPLUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.velocityRelativeTarget.sqrMagnitude > 0.0)
                        {
                            return comp.GetRelativeYaw(comp.velocityRelativeTarget.normalized);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "YAWTARGETRELMINUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.velocityRelativeTarget.sqrMagnitude > 0.0)
                        {
                            return comp.GetRelativeYaw(-comp.velocityRelativeTarget.normalized);
                        }
                        else
                        {
                            return 0.0;
                        }
                    };


                // comp.targeting. Probably the most finicky bit right now.
                case "TARGETNAME":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null)
                            return string.Empty;
                        if (comp.target is Vessel || comp.target is CelestialBody || comp.target is ModuleDockingNode)
                            return comp.target.GetName();
                        // What remains is MechJeb's ITargetable implementations, which also can return a name,
                        // but the newline they return in some cases needs to be removed.
                        return comp.target.GetName().Replace('\n', ' ');
                    };
                case "TARGETDISTANCE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null)
                            return comp.targetDistance;
                        return -1d;
                    };
                case "TARGETGROUNDDISTANCE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null)
                        {
                            Vector3d targetGroundPos = comp.target.ProjectPositionOntoSurface(rpmComp.vessel.mainBody);
                            if (targetGroundPos != Vector3d.zero)
                            {
                                return Vector3d.Distance(targetGroundPos, rpmComp.vessel.ProjectPositionOntoSurface());
                            }
                        }
                        return -1d;
                    };
                case "RELATIVEINCLINATION":
                    // MechJeb's comp.targetables don't have orbits.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbit != null)
                        {
                            return comp.targetOrbit.referenceBody != rpmComp.vessel.orbit.referenceBody ?
                                -1d :
                                Math.Abs(Vector3d.Angle(rpmComp.vessel.GetOrbit().SwappedOrbitNormal(), comp.targetOrbit.SwappedOrbitNormal()));
                        }
                        return double.NaN;
                    };
                case "TARGETORBITBODY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbit != null)
                            return comp.targetOrbit.referenceBody.name;
                        return string.Empty;
                    };
                case "TARGETEXISTS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null)
                            return -1d;
                        if (comp.target is Vessel)
                            return 1d;
                        return 0d;
                    };
                case "TARGETISDOCKINGPORT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null)
                            return -1d;
                        if (comp.target is ModuleDockingNode)
                            return 1d;
                        return 0d;
                    };
                case "TARGETISVESSELORPORT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null)
                            return -1d;
                        if (comp.target is ModuleDockingNode || comp.target is Vessel)
                            return 1d;
                        return 0d;
                    };
                case "TARGETISCELESTIAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null)
                            return -1d;
                        if (comp.target is CelestialBody)
                            return 1d;
                        return 0d;
                    };
                case "TARGETISPOSITION":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null)
                        {
                            return -1d;
                        }
                        else if (comp.target is PositionTarget)
                        {
                            return 1d;
                        }
                        else
                        {
                            return 0d;
                        }
                    };
                case "TARGETSITUATION":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target is Vessel)
                            return SituationString(comp.target.GetVessel().situation);
                        return string.Empty;
                    };
                case "TARGETALTITUDE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null)
                        {
                            return -1d;
                        }
                        if (comp.target is CelestialBody)
                        {
                            if (comp.targetBody == rpmComp.vessel.mainBody || comp.targetBody == Planetarium.fetch.Sun)
                            {
                                return 0d;
                            }
                            else
                            {
                                return comp.targetBody.referenceBody.GetAltitude(comp.targetBody.position);
                            }
                        }
                        if (comp.target is Vessel || comp.target is ModuleDockingNode)
                        {
                            return comp.target.GetVessel().mainBody.GetAltitude(comp.target.GetVessel().CoM);
                        }
                        else
                        {
                            return rpmComp.vessel.mainBody.GetAltitude(comp.target.GetTransform().position);
                        }
                    };
                // MOARdV: I don't think these are needed - I don't remember why we needed comp.targetOrbit
                //if (comp.targetOrbit != null)
                //{
                //    return comp.targetOrbit.altitude;
                //}
                //return -1d;
                case "TARGETSEMIMAJORAXIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null)
                            return double.NaN;
                        if (comp.targetOrbit != null)
                            return comp.targetOrbit.semiMajorAxis;
                        return double.NaN;
                    };
                case "TIMETOANWITHTARGETSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null || comp.targetOrbit == null)
                            return double.NaN;
                        return rpmComp.vessel.GetOrbit().TimeOfAscendingNode(comp.targetOrbit, Planetarium.GetUniversalTime()) - Planetarium.GetUniversalTime();
                    };
                case "TIMETODNWITHTARGETSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null || comp.targetOrbit == null)
                            return double.NaN;
                        return rpmComp.vessel.GetOrbit().TimeOfDescendingNode(comp.targetOrbit, Planetarium.GetUniversalTime()) - Planetarium.GetUniversalTime();
                    };
                case "TARGETCLOSESTAPPROACHTIME":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null || comp.targetOrbit == null || rpmComp.orbitSensibility == false)
                        {
                            return double.NaN;
                        }
                        else
                        {
                            double approachTime, approachDistance;
                            approachDistance = JUtil.GetClosestApproach(rpmComp.vessel.GetOrbit(), comp.target, out approachTime);
                            return approachTime - Planetarium.GetUniversalTime();
                        }
                    };
                case "TARGETCLOSESTAPPROACHDISTANCE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target == null || comp.targetOrbit == null || rpmComp.orbitSensibility == false)
                        {
                            return double.NaN;
                        }
                        else
                        {
                            double approachTime;
                            return JUtil.GetClosestApproach(rpmComp.vessel.GetOrbit(), comp.target, out approachTime);
                        }
                    };

                // Space Objects (asteroid) specifics
                case "TARGETSIGNALSTRENGTH":
                    // MOARdV:
                    // Based on observation, it appears the discovery
                    // level bitfield is basically unused - either the
                    // craft is Owned (-1) or Unowned (29 - which is the
                    // OR of all the bits).  However, maybe career mode uses
                    // the bits, so I will make a guess on what knowledge is
                    // appropriate here.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetVessel != null && comp.targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && comp.targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                        {
                            return comp.targetVessel.DiscoveryInfo.GetSignalStrength(comp.targetVessel.DiscoveryInfo.lastObservedTime);
                        }
                        else
                        {
                            return -1.0;
                        }
                    };

                case "TARGETSIGNALSTRENGTHCAPTION":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetVessel != null && comp.targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && comp.targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                        {
                            return DiscoveryInfo.GetSignalStrengthCaption(comp.targetVessel.DiscoveryInfo.GetSignalStrength(comp.targetVessel.DiscoveryInfo.lastObservedTime));
                        }
                        else
                        {
                            return "";
                        }
                    };

                case "TARGETLASTOBSERVEDTIMEUT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetVessel != null && comp.targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && comp.targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                        {
                            return comp.targetVessel.DiscoveryInfo.lastObservedTime;
                        }
                        else
                        {
                            return -1.0;
                        }
                    };

                case "TARGETLASTOBSERVEDTIMESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetVessel != null && comp.targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && comp.targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                        {
                            return Math.Max(Planetarium.GetUniversalTime() - comp.targetVessel.DiscoveryInfo.lastObservedTime, 0.0);
                        }
                        else
                        {
                            return -1.0;
                        }
                    };

                case "TARGETSIZECLASS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetVessel != null && comp.targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && comp.targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                        {
                            return comp.targetVessel.DiscoveryInfo.objectSize;
                        }
                        else
                        {
                            return "";
                        }
                    };

                case "TARGETDISTANCEX":    //distance to comp.target along the yaw axis (j and l rcs keys)
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return Vector3d.Dot(comp.targetSeparation, rpmComp.vessel.GetTransform().right);
                    };
                case "TARGETDISTANCEY":   //distance to comp.target along the pitch axis (i and k rcs keys)
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return Vector3d.Dot(comp.targetSeparation, rpmComp.vessel.GetTransform().forward);
                    };
                case "TARGETDISTANCEZ":  //closure distance from comp.target - (h and n rcs keys)
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return -Vector3d.Dot(comp.targetSeparation, rpmComp.vessel.GetTransform().up);
                    };

                case "TARGETDISTANCESCALEDX":    //scaled and clamped version of comp.targetDISTANCEX.  Returns a number between 100 and -100, with precision increasing as distance decreases.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        double scaledX = Vector3d.Dot(comp.targetSeparation, rpmComp.vessel.GetTransform().right);
                        double zdist = -Vector3d.Dot(comp.targetSeparation, rpmComp.vessel.GetTransform().up);
                        if (zdist < .1)
                            scaledX = scaledX / (0.1 * Math.Sign(zdist));
                        else
                            scaledX = ((scaledX + zdist) / (zdist + zdist)) * (100) - 50;
                        if (scaledX > 100) scaledX = 100;
                        if (scaledX < -100) scaledX = -100;
                        return scaledX;
                    };


                case "TARGETDISTANCESCALEDY":  //scaled and clamped version of comp.targetDISTANCEY.  These two numbers will control the position needles on a docking port alignment gauge.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        double scaledY = Vector3d.Dot(comp.targetSeparation, rpmComp.vessel.GetTransform().forward);
                        double zdist2 = -Vector3d.Dot(comp.targetSeparation, rpmComp.vessel.GetTransform().up);
                        if (zdist2 < .1)
                            scaledY = scaledY / (0.1 * Math.Sign(zdist2));
                        else
                            scaledY = ((scaledY + zdist2) / (zdist2 + zdist2)) * (100) - 50;
                        if (scaledY > 100) scaledY = 100;
                        if (scaledY < -100) scaledY = -100;
                        return scaledY;
                    };

                // TODO: I probably should return something else for vessels. But not sure what exactly right now.
                case "TARGETANGLEX":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null)
                        {
                            if (comp.targetDockingNode != null)
                                return JUtil.NormalAngle(-comp.targetDockingNode.GetTransform().forward, FlightGlobals.ActiveVessel.ReferenceTransform.up, FlightGlobals.ActiveVessel.ReferenceTransform.forward);
                            if (comp.target is Vessel)
                                return JUtil.NormalAngle(-comp.target.GetFwdVector(), comp.forward, comp.up);
                            return 0d;
                        }
                        return 0d;
                    };
                case "TARGETANGLEY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null)
                        {
                            if (comp.targetDockingNode != null)
                                return JUtil.NormalAngle(-comp.targetDockingNode.GetTransform().forward, FlightGlobals.ActiveVessel.ReferenceTransform.up, -FlightGlobals.ActiveVessel.ReferenceTransform.right);
                            if (comp.target is Vessel)
                            {
                                JUtil.NormalAngle(-comp.target.GetFwdVector(), comp.forward, -comp.right);
                            }
                            return 0d;
                        }
                        return 0d;
                    };
                case "TARGETANGLEZ":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null)
                        {
                            if (comp.targetDockingNode != null)
                                return (360 - (JUtil.NormalAngle(-comp.targetDockingNode.GetTransform().up, FlightGlobals.ActiveVessel.ReferenceTransform.forward, FlightGlobals.ActiveVessel.ReferenceTransform.up))) % 360;
                            if (comp.target is Vessel)
                            {
                                return JUtil.NormalAngle(comp.target.GetTransform().up, comp.up, -comp.forward);
                            }
                            return 0d;
                        }
                        return 0d;
                    };
                case "TARGETANGLEDEV":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null)
                        {
                            return Vector3d.Angle(rpmComp.vessel.ReferenceTransform.up, FlightGlobals.fetch.vesselTargetDirection);
                        }
                        return 180d;
                    };

                case "TARGETAPOAPSIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbitSensibility)
                            return comp.targetOrbit.ApA;
                        return double.NaN;
                    };
                case "TARGETPERIAPSIS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbitSensibility)
                            return comp.targetOrbit.PeA;
                        return double.NaN;
                    };
                case "TARGETINCLINATION":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbitSensibility)
                            return comp.targetOrbit.inclination;
                        return double.NaN;
                    };
                case "TARGETECCENTRICITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbitSensibility)
                            return comp.targetOrbit.eccentricity;
                        return double.NaN;
                    };
                case "TARGETORBITALVEL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbitSensibility)
                            return comp.targetOrbit.orbitalSpeed;
                        return double.NaN;
                    };
                case "TARGETTIMETOAPSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbitSensibility)
                            return comp.targetOrbit.timeToAp;
                        return double.NaN;
                    };
                case "TARGETORBPERIODSECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbit != null && comp.targetOrbitSensibility)
                            return comp.targetOrbit.period;
                        return double.NaN;
                    };
                case "TARGETTIMETOPESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.target != null && comp.targetOrbitSensibility)
                            return comp.targetOrbit.eccentricity < 1 ?
                                comp.targetOrbit.timeToPe :
                                -comp.targetOrbit.meanAnomaly / (2 * Math.PI / comp.targetOrbit.period);
                        return double.NaN;
                    };
                case "TARGETLAUNCHTIMESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetVessel != null && comp.targetVessel.mainBody == rpmComp.vessel.mainBody && (rpmComp.vessel.situation == Vessel.Situations.LANDED || rpmComp.vessel.situation == Vessel.Situations.PRELAUNCH || rpmComp.vessel.situation == Vessel.Situations.SPLASHED))
                        {
                            // MOARdV TODO: Make phase angle a variable?
                            return TimeToPhaseAngle(12.7, rpmComp.vessel.mainBody, rpmComp.vessel.longitude, comp.target.GetOrbit());
                        }
                        else
                        {
                            return 0.0;
                        }
                    };
                case "TARGETPLANELAUNCHTIMESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetVessel != null && comp.targetVessel.mainBody == rpmComp.vessel.mainBody && (rpmComp.vessel.situation == Vessel.Situations.LANDED || rpmComp.vessel.situation == Vessel.Situations.PRELAUNCH || rpmComp.vessel.situation == Vessel.Situations.SPLASHED))
                        {
                            return TimeToPlane(rpmComp.vessel.mainBody, rpmComp.vessel.latitude, rpmComp.vessel.longitude, comp.target.GetOrbit());
                        }
                        else
                        {
                            return 0.0;
                        }
                    };

                // Protractor-type values (phase angle, ejection angle)
                case "TARGETBODYPHASEANGLE":
                    // comp.targetOrbit is always null if comp.targetOrbitSensibility is false,
                    // so no need to test if the orbit makes sense.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        Protractor.Update(rpmComp.vessel, comp.altitudeASL, comp.targetOrbit);
                        return Protractor.PhaseAngle;
                    };
                case "TARGETBODYPHASEANGLESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        Protractor.Update(rpmComp.vessel, comp.altitudeASL, comp.targetOrbit);
                        return Protractor.TimeToPhaseAngle;
                    };
                case "TARGETBODYEJECTIONANGLE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        Protractor.Update(rpmComp.vessel, comp.altitudeASL, comp.targetOrbit);
                        return Protractor.EjectionAngle;
                    };
                case "TARGETBODYEJECTIONANGLESECS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        Protractor.Update(rpmComp.vessel, comp.altitudeASL, comp.targetOrbit);
                        return Protractor.TimeToEjectionAngle;
                    };
                case "TARGETBODYCLOSESTAPPROACH":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp.orbitSensibility == true)
                        {
                            double approachTime;
                            return JUtil.GetClosestApproach(rpmComp.vessel.GetOrbit(), comp.target, out approachTime);
                        }
                        else
                        {
                            return -1.0;
                        }
                    };
                case "TARGETBODYMOONEJECTIONANGLE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        Protractor.Update(rpmComp.vessel, comp.altitudeASL, comp.targetOrbit);
                        return Protractor.MoonEjectionAngle;
                    };
                case "TARGETBODYEJECTIONALTITUDE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        Protractor.Update(rpmComp.vessel, comp.altitudeASL, comp.targetOrbit);
                        return Protractor.EjectionAltitude;
                    };
                case "TARGETBODYDELTAV":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        Protractor.Update(rpmComp.vessel, comp.altitudeASL, comp.targetOrbit);
                        return Protractor.TargetBodyDeltaV;
                    };
                case "PREDICTEDLANDINGALTITUDE":
                    return LandingAltitude();
                case "PREDICTEDLANDINGLATITUDE":
                    return LandingLatitude();
                case "PREDICTEDLANDINGLONGITUDE":
                    return LandingLongitude();
                case "PREDICTEDLANDINGERROR":
                    return LandingError();

                // FLight control status
                case "THROTTLE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.mainThrottle; };
                case "STICKPITCH":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.pitch; };
                case "STICKROLL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.roll; };
                case "STICKYAW":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.yaw; };
                case "STICKPITCHTRIM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.pitchTrim; };
                case "STICKROLLTRIM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.rollTrim; };
                case "STICKYAWTRIM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.yawTrim; };
                case "STICKRCSX":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.X; };
                case "STICKRCSY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.Y; };
                case "STICKRCSZ":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ctrlState.Z; };
                case "PRECISIONCONTROL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (FlightInputHandler.fetch.precisionMode).GetHashCode(); };

                // Staging and other stuff
                case "STAGE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return StageManager.CurrentStage; };
                case "STAGEREADY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (StageManager.CanSeparate && InputLockManager.IsUnlocked(ControlTypes.STAGING)).GetHashCode(); };
                case "SITUATION":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return SituationString(rpmComp.vessel.situation); };
                case "RANDOM":
                    cacheable = false;
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return UnityEngine.Random.value; };
                case "RANDOMNORMAL":
                    cacheable = false;
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        // Box-Muller method tweaked to prevent a 0 in u.
                        float u = UnityEngine.Random.Range(0.0009765625f, 1.0f);
                        float v = UnityEngine.Random.Range(0.0f, 2.0f * Mathf.PI);
                        float x = Mathf.Sqrt(-2.0f * Mathf.Log(u)) * Mathf.Cos(v);
                        // TODO: verify the stddev - I believe it is 1; mean is 0.
                        return x;
                    };
                case "PODTEMPERATURE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.temperature + RPMGlobals.KelvinToCelsius) : 0.0; };
                case "PODTEMPERATUREKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.temperature) : 0.0; };
                case "PODSKINTEMPERATURE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.skinTemperature + RPMGlobals.KelvinToCelsius) : 0.0; };
                case "PODSKINTEMPERATUREKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.skinTemperature) : 0.0; };
                case "PODMAXSKINTEMPERATURE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.skinMaxTemp + RPMGlobals.KelvinToCelsius) : 0.0; };
                case "PODMAXSKINTEMPERATUREKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.skinMaxTemp) : 0.0; };
                case "PODMAXTEMPERATURE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.maxTemp + RPMGlobals.KelvinToCelsius) : 0.0; };
                case "PODMAXTEMPERATUREKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.maxTemp) : 0.0; };
                case "PODNETFLUX":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (rpmComp != null && rpmComp.part != null) ? (rpmComp.part.thermalConductionFlux + rpmComp.part.thermalConvectionFlux + rpmComp.part.thermalInternalFlux + rpmComp.part.thermalRadiationFlux) : 0.0; };
                case "EXTERNALTEMPERATURE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.externalTemperature + RPMGlobals.KelvinToCelsius; };
                case "EXTERNALTEMPERATUREKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.externalTemperature; };
                case "AMBIENTTEMPERATURE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.atmosphericTemperature + RPMGlobals.KelvinToCelsius; };
                case "AMBIENTTEMPERATUREKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.atmosphericTemperature; };
                case "HEATSHIELDTEMPERATURE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (double)comp.heatShieldTemperature + RPMGlobals.KelvinToCelsius;
                    };
                case "HEATSHIELDTEMPERATUREKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.heatShieldTemperature;
                    };
                case "HEATSHIELDTEMPERATUREFLUX":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.heatShieldFlux;
                    };
                case "HOTTESTPARTTEMP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.hottestPartTemperature;
                    };
                case "HOTTESTPARTMAXTEMP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.hottestPartMaxTemperature;
                    };
                case "HOTTESTPARTTEMPRATIO":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.hottestPartMaxTemperature > 0.0f) ? (comp.hottestPartTemperature / comp.hottestPartMaxTemperature) : 0.0f;
                    };
                case "HOTTESTPARTNAME":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.hottestPartName;
                    };
                case "HOTTESTENGINETEMP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return comp.hottestEngineTemperature;
                    };
                case "HOTTESTENGINEMAXTEMP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.hottestEngineMaxTemperature;
                    };
                case "HOTTESTENGINETEMPRATIO":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return (comp.hottestEngineMaxTemperature > 0.0f) ? (comp.hottestEngineTemperature / comp.hottestEngineMaxTemperature) : 0.0f; 
                    };
                case "SLOPEANGLE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.slopeAngle; 
                    };
                case "SPEEDDISPLAYMODE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return GameSettings.KERBIN_TIME.GetHashCode(); };
                case "ISDOCKINGPORTREFERENCE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
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
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion; };
                case "MECHJEBAVAILABLE":
                    return MechJebAvailable();
                case "TIMEWARPPHYSICS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (TimeWarp.CurrentRate > 1.0f && TimeWarp.WarpMode == TimeWarp.Modes.LOW).GetHashCode(); };
                case "TIMEWARPNONPHYSICS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (TimeWarp.CurrentRate > 1.0f && TimeWarp.WarpMode == TimeWarp.Modes.HIGH).GetHashCode(); };
                case "TIMEWARPACTIVE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return (TimeWarp.CurrentRate > 1.0f).GetHashCode(); };
                case "TIMEWARPCURRENT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return TimeWarp.CurrentRate; };


                // Compound variables which exist to stave off the need to parse logical and arithmetic expressions. :)
                case "GEARALARM":
                    // Returns 1 if vertical speed is negative, gear is not extended, and radar altitude is less than 50m.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return (comp.speedVerticalRounded < 0.0 && !rpmComp.vessel.ActionGroups.groups[RPMVesselComputer.gearGroupNumber] && comp.altitudeBottom < 100.0).GetHashCode(); 
                    };
                case "GROUNDPROXIMITYALARM":
                    // Returns 1 if, at maximum acceleration, in the time remaining until ground impact, it is impossible to get a vertical speed higher than -10m/s.
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return (comp.SpeedAtImpact(comp.totalLimitedMaximumThrust) < -10d).GetHashCode(); 
                    };
                case "TUMBLEALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return (comp.speedVerticalRounded < 0.0 && comp.altitudeBottom < 100.0 && comp.speedHorizontal > 5.0).GetHashCode(); 
                    };
                case "SLOPEALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return (comp.speedVerticalRounded < 0.0 && comp.altitudeBottom < 100.0 && comp.slopeAngle > 15.0f).GetHashCode(); 
                    };
                case "DOCKINGANGLEALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (comp.targetDockingNode != null && comp.targetDistance < 10.0 && comp.approachSpeed > 0.0f &&
                            (Math.Abs(JUtil.NormalAngle(-comp.targetDockingNode.GetFwdVector(), comp.forward, comp.up)) > 1.5 ||
                            Math.Abs(JUtil.NormalAngle(-comp.targetDockingNode.GetFwdVector(), comp.forward, -comp.right)) > 1.5)).GetHashCode();
                    };
                case "DOCKINGSPEEDALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return (comp.targetDockingNode != null && comp.approachSpeed > 2.5f && comp.targetDistance < 15.0).GetHashCode();
                    };
                case "ALTITUDEALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return (comp.speedVerticalRounded < 0.0 && comp.altitudeBottom < 150.0).GetHashCode(); 
                    };
                case "PODTEMPERATUREALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (rpmComp != null && rpmComp.part != null)
                        {
                            double tempRatio = rpmComp.part.temperature / rpmComp.part.maxTemp;
                            if (tempRatio > 0.85d)
                            {
                                return 1d;
                            }
                            else if (tempRatio > 0.75d)
                            {
                                return 0d;
                            }
                        }
                        return -1d;
                    };
                // Well, it's not a compound but it's an alarm...
                case "ENGINEOVERHEATALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.anyEnginesOverheating.GetHashCode(); 
                    };
                case "ENGINEFLAMEOUTALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.anyEnginesFlameout.GetHashCode();
                    };
                case "IMPACTALARM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    { 
                        return (rpmComp != null && rpmComp.part != null && rpmComp.vessel.srfSpeed > rpmComp.part.crashTolerance).GetHashCode(); 
                    };

                // SCIENCE!!
                case "SCIENCEDATA":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.totalDataAmount; 
                    };
                case "SCIENCECOUNT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return comp.totalExperimentCount; 
                    };
                case "BIOMENAME":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    { 
                        return rpmComp.vessel.CurrentBiome(); 
                    };
                case "BIOMEID":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        return ScienceUtil.GetExperimentBiome(rpmComp.vessel.mainBody, rpmComp.vessel.latitude, rpmComp.vessel.longitude); 
                    };

                // Some of the new goodies in 0.24.
                case "REPUTATION":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return Reputation.Instance != null ? Reputation.CurrentRep : 0.0f; };
                case "FUNDS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return Funding.Instance != null ? Funding.Instance.Funds : 0.0; };


                // Action group flags. To properly format those, use this format:
                // {0:on;0;OFF}
                case "GEAR":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ActionGroups.groups[RPMVesselComputer.gearGroupNumber].GetHashCode(); };
                case "BRAKES":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ActionGroups.groups[RPMVesselComputer.brakeGroupNumber].GetHashCode(); };
                case "SAS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ActionGroups.groups[RPMVesselComputer.sasGroupNumber].GetHashCode(); };
                case "LIGHTS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ActionGroups.groups[RPMVesselComputer.lightGroupNumber].GetHashCode(); };
                case "RCS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.ActionGroups.groups[RPMVesselComputer.rcsGroupNumber].GetHashCode(); };
                // 0.90 SAS mode fields:
                case "SASMODESTABILITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    { 
                        //if(rpmComp.vessel.Autopilot == null)
                        //{
                        //    return 0.0;
                        //}
                        return (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.StabilityAssist) ? 1.0 : 0.0; 
                    };
                case "SASMODEPROGRADE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        //if (rpmComp.vessel.Autopilot == null)
                        //{
                        //    return 0.0;
                        //}
                        return (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Prograde) ? 1.0 :
                            (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Retrograde) ? -1.0 : 0.0;
                    };
                case "SASMODENORMAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        //if (rpmComp.vessel.Autopilot == null)
                        //{
                        //    return 0.0;
                        //}
                        return (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Normal) ? 1.0 :
                            (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Antinormal) ? -1.0 : 0.0;
                    };
                case "SASMODERADIAL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        //if (rpmComp.vessel.Autopilot == null)
                        //{
                        //    return 0.0;
                        //}
                        return (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.RadialOut) ? 1.0 :
                            (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.RadialIn) ? -1.0 : 0.0;
                    };
                case "SASMODETARGET":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        //if (rpmComp.vessel.Autopilot == null)
                        //{
                        //    return 0.0;
                        //}
                        return (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Target) ? 1.0 :
                            (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.AntiTarget) ? -1.0 : 0.0;
                    };
                case "SASMODEMANEUVER":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                    {
                        //if (rpmComp.vessel.Autopilot == null)
                        //{
                        //    return 0.0;
                        //}
                        return (rpmComp.vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Maneuver) ? 1.0 : 0.0; 
                    };


                // Database information about planetary bodies.
                case "ORBITBODYATMOSPHERE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.atmosphere ? 1d : -1d; };
                case "TARGETBODYATMOSPHERE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.atmosphere ? 1d : -1d;
                        return 0d;
                    };
                case "ORBITBODYOXYGEN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.atmosphereContainsOxygen ? 1d : -1d; };
                case "TARGETBODYOXYGEN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.atmosphereContainsOxygen ? 1d : -1d;
                        return -1d;
                    };
                case "ORBITBODYSCALEHEIGHT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.atmosphereDepth; };
                case "TARGETBODYSCALEHEIGHT":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.atmosphereDepth;
                        return -1d;
                    };
                case "ORBITBODYRADIUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.Radius; };
                case "TARGETBODYRADIUS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.Radius;
                        return -1d;
                    };
                case "ORBITBODYMASS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.Mass; };
                case "TARGETBODYMASS":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.Mass;
                        return -1d;
                    };
                case "ORBITBODYROTATIONPERIOD":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.rotationPeriod; };
                case "TARGETBODYROTATIONPERIOD":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.rotationPeriod;
                        return -1d;
                    };
                case "ORBITBODYSOI":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.sphereOfInfluence; };
                case "TARGETBODYSOI":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.sphereOfInfluence;
                        return -1d;
                    };
                case "ORBITBODYGEEASL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.GeeASL; };
                case "TARGETBODYGEEASL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.GeeASL;
                        return -1d;
                    };
                case "ORBITBODYGM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.gravParameter; };
                case "TARGETBODYGM":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.gravParameter;
                        return -1d;
                    };
                case "ORBITBODYATMOSPHERETOP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return rpmComp.vessel.orbit.referenceBody.atmosphereDepth; };
                case "TARGETBODYATMOSPHERETOP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return comp.targetBody.atmosphereDepth;
                        return -1d;
                    };
                case "ORBITBODYESCAPEVEL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return Math.Sqrt(2 * rpmComp.vessel.orbit.referenceBody.gravParameter / rpmComp.vessel.orbit.referenceBody.Radius); };
                case "TARGETBODYESCAPEVEL":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return Math.Sqrt(2 * comp.targetBody.gravParameter / comp.targetBody.Radius);
                        return -1d;
                    };
                case "ORBITBODYAREA":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return 4.0 * Math.PI * rpmComp.vessel.orbit.referenceBody.Radius * rpmComp.vessel.orbit.referenceBody.Radius; };
                case "TARGETBODYAREA":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                            return 4 * Math.PI * comp.targetBody.Radius * comp.targetBody.Radius;
                        return -1d;
                    };
                case "ORBITBODYSYNCORBITALTITUDE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        double syncRadius = Math.Pow(rpmComp.vessel.orbit.referenceBody.gravParameter / Math.Pow(2.0 * Math.PI / rpmComp.vessel.orbit.referenceBody.rotationPeriod, 2.0), 1.0 / 3.0);
                        return syncRadius > rpmComp.vessel.orbit.referenceBody.sphereOfInfluence ? double.NaN : syncRadius - rpmComp.vessel.orbit.referenceBody.Radius;
                    };
                case "TARGETBODYSYNCORBITALTITUDE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                        {
                            double syncRadiusT = Math.Pow(comp.targetBody.gravParameter / Math.Pow(2 * Math.PI / comp.targetBody.rotationPeriod, 2), 1 / 3d);
                            return syncRadiusT > comp.targetBody.sphereOfInfluence ? double.NaN : syncRadiusT - comp.targetBody.Radius;
                        }
                        return -1d;
                    };
                case "ORBITBODYSYNCORBITVELOCITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        return (2 * Math.PI / rpmComp.vessel.orbit.referenceBody.rotationPeriod) *
                            Math.Pow(rpmComp.vessel.orbit.referenceBody.gravParameter / Math.Pow(2.0 * Math.PI / rpmComp.vessel.orbit.referenceBody.rotationPeriod, 2), 1.0 / 3.0d);
                    };
                case "TARGETBODYSYNCORBITVELOCITY":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                        {
                            return (2 * Math.PI / comp.targetBody.rotationPeriod) *
                            Math.Pow(comp.targetBody.gravParameter / Math.Pow(2 * Math.PI / comp.targetBody.rotationPeriod, 2), 1 / 3d);
                        }
                        return -1d;
                    };
                case "ORBITBODYSYNCORBITCIRCUMFERENCE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return 2 * Math.PI * Math.Pow(rpmComp.vessel.orbit.referenceBody.gravParameter / Math.Pow(2 * Math.PI / rpmComp.vessel.orbit.referenceBody.rotationPeriod, 2), 1 / 3d); };
                case "TARGETBODYSYNCORBICIRCUMFERENCE":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                        {
                            return 2 * Math.PI * Math.Pow(comp.targetBody.gravParameter / Math.Pow(2 * Math.PI / comp.targetBody.rotationPeriod, 2), 1 / 3d);
                        }
                        return -1d;
                    };
                case "ORBITBODYSURFACETEMP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return FlightGlobals.currentMainBody.atmosphereTemperatureSeaLevel + RPMGlobals.KelvinToCelsius; };
                case "TARGETBODYSURFACETEMP":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                        {
                            return comp.targetBody.atmosphereTemperatureSeaLevel + RPMGlobals.KelvinToCelsius;
                        }
                        return -1d;
                    };
                case "ORBITBODYSURFACETEMPKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return FlightGlobals.currentMainBody.atmosphereTemperatureSeaLevel; };
                case "TARGETBODYSURFACETEMPKELVIN":
                    return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
                    {
                        if (comp.targetBody != null)
                        {
                            return comp.targetBody.atmosphereTemperatureSeaLevel;
                        }
                        return -1d;
                    };
            }

            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return variable; };
        }
#endregion

        #region eval helpers
        private static object CrewListElement(string element, int seatID, IList<ProtoCrewMember> crewList, IList<kerbalExpressionSystem> crewMedical)
        {
            bool exists = (crewList != null) && (seatID < crewList.Count);
            bool valid = exists && crewList[seatID] != null;
            switch (element)
            {
                case "PRESENT":
                    return valid ? 1d : -1d;
                case "EXISTS":
                    return exists ? 1d : -1d;
                case "FIRST":
                    return valid ? crewList[seatID].name.Split()[0] : string.Empty;
                case "LAST":
                    return valid ? crewList[seatID].name.Split()[1] : string.Empty;
                case "FULL":
                    return valid ? crewList[seatID].name : string.Empty;
                case "STUPIDITY":
                    return valid ? crewList[seatID].stupidity : -1d;
                case "COURAGE":
                    return valid ? crewList[seatID].courage : -1d;
                case "BADASS":
                    return valid ? crewList[seatID].isBadass.GetHashCode() : -1d;
                case "PANIC":
                    return (valid && crewMedical[seatID] != null) ? crewMedical[seatID].panicLevel : -1d;
                case "WHEE":
                    return (valid && crewMedical[seatID] != null) ? crewMedical[seatID].wheeLevel : -1d;
                case "TITLE":
                    return valid ? crewList[seatID].experienceTrait.Title : string.Empty;
                case "LEVEL":
                    return valid ? (float)crewList[seatID].experienceLevel : -1d;
                case "EXPERIENCE":
                    return valid ? crewList[seatID].experience : -1d;
                default:
                    return "???!";
            }

        }

        /// <summary>
        /// According to C# specification, switch-case is compiled to a constant hash table.
        /// So this is actually more efficient than a dictionary, who'd have thought.
        /// </summary>
        /// <param name="situation"></param>
        /// <returns></returns>
        private static string SituationString(Vessel.Situations situation)
        {
            switch (situation)
            {
                case Vessel.Situations.FLYING:
                    return "Flying";
                case Vessel.Situations.SUB_ORBITAL:
                    return "Sub-orbital";
                case Vessel.Situations.ESCAPING:
                    return "Escaping";
                case Vessel.Situations.LANDED:
                    return "Landed";
                case Vessel.Situations.DOCKED:
                    return "Docked"; // When does this ever happen exactly, I wonder?
                case Vessel.Situations.PRELAUNCH:
                    return "Ready to launch";
                case Vessel.Situations.ORBITING:
                    return "Orbiting";
                case Vessel.Situations.SPLASHED:
                    return "Splashed down";
            }
            return "??!";
        }

        /// <summary>
        /// Returns a number identifying the next apsis type
        /// </summary>
        /// <returns></returns>
        private static double NextApsisType(Vessel vessel)
        {
            if (JUtil.OrbitMakesSense(vessel))
            {
                if (vessel.orbit.eccentricity < 1.0)
                {
                    // Which one will we reach first?
                    return (vessel.orbit.timeToPe < vessel.orbit.timeToAp) ? -1.0 : 1.0;
                } 	// Ship is hyperbolic.  There is no Ap.  Have we already
                // passed Pe?
                return (-vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period) > 0.0) ? -1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Originally from MechJeb
        /// Computes the time until the phase angle between the launchpad and the target equals the given angle.
        /// The convention used is that phase angle is the angle measured starting at the target and going east until
        /// you get to the launchpad. 
        /// The time returned will not be exactly accurate unless the target is in an exactly circular orbit. However,
        /// the time returned will go to exactly zero when the desired phase angle is reached.
        /// </summary>
        /// <param name="phaseAngle"></param>
        /// <param name="launchBody"></param>
        /// <param name="launchLongitude"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private static double TimeToPhaseAngle(double phaseAngle, CelestialBody launchBody, double launchLongitude, Orbit target)
        {
            double launchpadAngularRate = 360 / launchBody.rotationPeriod;
            double targetAngularRate = 360.0 / target.period;
            if (Vector3d.Dot(-target.GetOrbitNormal().SwizzleXZY().normalized, launchBody.angularVelocity) < 0) targetAngularRate *= -1; //retrograde target

            Vector3d currentLaunchpadDirection = launchBody.GetSurfaceNVector(0, launchLongitude);
            Vector3d currentTargetDirection = target.SwappedRelativePositionAtUT(Planetarium.GetUniversalTime());
            currentTargetDirection = Vector3d.Exclude(launchBody.angularVelocity, currentTargetDirection);

            double currentPhaseAngle = Math.Abs(Vector3d.Angle(currentLaunchpadDirection, currentTargetDirection));
            if (Vector3d.Dot(Vector3d.Cross(currentTargetDirection, currentLaunchpadDirection), launchBody.angularVelocity) < 0)
            {
                currentPhaseAngle = 360 - currentPhaseAngle;
            }

            double phaseAngleRate = launchpadAngularRate - targetAngularRate;

            double phaseAngleDifference = JUtil.ClampDegrees360(phaseAngle - currentPhaseAngle);

            if (phaseAngleRate < 0)
            {
                phaseAngleRate *= -1;
                phaseAngleDifference = 360 - phaseAngleDifference;
            }


            return phaseAngleDifference / phaseAngleRate;
        }

        /// <summary>
        /// Originally from MechJeb
        /// Computes the time required for the given launch location to rotate under the target orbital plane. 
        /// If the latitude is too high for the launch location to ever actually rotate under the target plane,
        /// returns the time of closest approach to the target plane.
        /// I have a wonderful proof of this formula which this comment is too short to contain.
        /// </summary>
        /// <param name="launchBody"></param>
        /// <param name="launchLatitude"></param>
        /// <param name="launchLongitude"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private static double TimeToPlane(CelestialBody launchBody, double launchLatitude, double launchLongitude, Orbit target)
        {
            double inc = Math.Abs(Vector3d.Angle(-target.GetOrbitNormal().SwizzleXZY().normalized, launchBody.angularVelocity));
            Vector3d b = Vector3d.Exclude(launchBody.angularVelocity, -target.GetOrbitNormal().SwizzleXZY().normalized).normalized; // I don't understand the sign here, but this seems to work
            b *= launchBody.Radius * Math.Sin(Math.PI / 180 * launchLatitude) / Math.Tan(Math.PI / 180 * inc);
            Vector3d c = Vector3d.Cross(-target.GetOrbitNormal().SwizzleXZY().normalized, launchBody.angularVelocity).normalized;
            double cMagnitudeSquared = Math.Pow(launchBody.Radius * Math.Cos(Math.PI / 180 * launchLatitude), 2) - b.sqrMagnitude;
            if (cMagnitudeSquared < 0) cMagnitudeSquared = 0;
            c *= Math.Sqrt(cMagnitudeSquared);
            Vector3d a1 = b + c;
            Vector3d a2 = b - c;

            Vector3d longitudeVector = launchBody.GetSurfaceNVector(0, launchLongitude);

            double angle1 = Math.Abs(Vector3d.Angle(longitudeVector, a1));
            if (Vector3d.Dot(Vector3d.Cross(longitudeVector, a1), launchBody.angularVelocity) < 0) angle1 = 360 - angle1;
            double angle2 = Math.Abs(Vector3d.Angle(longitudeVector, a2));
            if (Vector3d.Dot(Vector3d.Cross(longitudeVector, a2), launchBody.angularVelocity) < 0) angle2 = 360 - angle2;

            double angle = Math.Min(angle1, angle2);
            return (angle / 360) * launchBody.rotationPeriod;
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
        private Func<double> evaluateTerminalVelocity;
        private bool evaluateTerminalVelocityReady;
        private Func<double> evaluateTimeToImpact;
        private bool evaluateTimeToImpactReady;

        private VariableEvaluator AngleOfAttack()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return comp.FallbackEvaluateAngleOfAttack(); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor(); };
            }
        }

        private VariableEvaluator DeltaV()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return (comp.actualAverageIsp * RPMGlobals.gee) * Math.Log(comp.totalShipWetMass / (comp.totalShipWetMass - comp.resources.PropellantMass(false))); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor(); };
            }
        }

        private VariableEvaluator DeltaVStage()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return (comp.actualAverageIsp * RPMGlobals.gee) * Math.Log(comp.totalShipWetMass / (comp.totalShipWetMass - comp.resources.PropellantMass(true))); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor(); };
            }
        }

        private VariableEvaluator DragAccel()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return comp.FallbackEvaluateDragForce() / comp.totalShipWetMass; 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return accessor() / comp.totalShipWetMass; 
                };
            }
        }

        private VariableEvaluator DragForce()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return comp.FallbackEvaluateDragForce(); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor(); };
            }
        }

        private VariableEvaluator DynamicPressure()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return vessel.dynamicPressurekPa; };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor(); };
            }
        }

        private VariableEvaluator LandingError()
        {
            Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingError", typeof(Func<double>));

            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor(); };
        }

        private VariableEvaluator LandingAltitude()
        {
            Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingAltitude", typeof(Func<double>));

            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
            {
                double est = accessor();
                return (est == 0.0) ? comp.estLandingAltitude : est;
            };
        }

        private VariableEvaluator LandingLatitude()
        {
            Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLatitude", typeof(Func<double>));

            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
            {
                double est = accessor();
                return (est == 0.0) ? comp.estLandingLatitude : est;
            };
        }

        private VariableEvaluator LandingLongitude()
        {
            Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLongitude", typeof(Func<double>));

            return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) =>
            {
                double est = accessor();
                return (est == 0.0) ? comp.estLandingLongitude : est;
            };
        }

        private VariableEvaluator LiftAccel()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return comp.FallbackEvaluateLiftForce() / comp.totalShipWetMass; 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return accessor() / comp.totalShipWetMass; 
                };
            }
        }

        private VariableEvaluator LiftForce()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return comp.FallbackEvaluateLiftForce(); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor(); };
            }
        }

        private VariableEvaluator MechJebAvailable()
        {
            Func<bool> accessor = null;

            accessor = (Func<bool>)GetInternalMethod("JSIMechJeb:GetMechJebAvailable", typeof(Func<bool>));
            if (accessor == null)
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return false; };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor().GetHashCode(); };
            }
        }

        private VariableEvaluator SideSlip()
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
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => 
                {
                    return comp.FallbackEvaluateSideSlip(); 
                };
            }
            else
            {
                return (string variable, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp) => { return accessor(); };
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

        internal delegate object VariableEvaluator(string s, RasterPropMonitorComputer rpmComp, RPMVesselComputer comp);
        internal class VariableCache
        {
            internal object cachedValue = null;
            internal readonly VariableEvaluator accessor;
            internal uint serialNumber = 0;
            internal readonly bool cacheable;

            internal VariableCache(bool cacheable, VariableEvaluator accessor)
            {
                this.cacheable = cacheable;
                this.accessor = accessor;
            }
        }
    }
}
