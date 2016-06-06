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
using System.Linq;
using System.Text;

namespace JSI
{
    /// <summary>
    /// JSIParachute provides an interface to control parachutes aboard the
    /// craft.  The feature works with stock parachutes (although the
    /// Arm/Disarm function doesn't do anything), but is specifically
    /// designed to work with RealChutes.  And, thanks to reflection, I don't
    /// need a hard dependency with RealChutes.
    /// </summary>
    public class JSIParachute : IJSIModule
    {
        static internal readonly Type rcModuleRealChute;
        static private readonly DynamicFuncBool getAnyDeployed;
        static private readonly DynamicAction armChute;
        static private readonly DynamicAction disarmChute;
        static private readonly DynamicAction deployChute;
        static private readonly DynamicAction cutChute;
        static private readonly FieldInfo rcArmed;
        static private readonly FieldInfo rcSafeState;
        static internal readonly bool rcFound;

        // From RealChute:
        public enum SafeState
        {
            SAFE,
            RISKY,
            DANGEROUS
        }

        static JSIParachute()
        {
            try
            {
                rcModuleRealChute = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "RealChute.RealChuteModule");
                if (rcModuleRealChute == null)
                {
                    rcFound = false;
                    JUtil.LogMessage(null, "rcModuleRealChute is null");
                    return;
                }

                PropertyInfo rcAnyDeployed = rcModuleRealChute.GetProperty("AnyDeployed", BindingFlags.Instance | BindingFlags.Public);
                if (rcAnyDeployed == null)
                {
                    JUtil.LogMessage(null, "rcAnyDeployed is null");
                }
                MethodInfo rcGetAnyDeployed = rcAnyDeployed.GetGetMethod();
                getAnyDeployed = DynamicMethodDelegateFactory.CreateFuncBool(rcGetAnyDeployed);
                if (getAnyDeployed == null)
                {
                    JUtil.LogMessage(null, "getAnyDeployed is null");
                }

                MethodInfo rcArmChute = rcModuleRealChute.GetMethod("GUIArm", BindingFlags.Instance | BindingFlags.Public);
                armChute = DynamicMethodDelegateFactory.CreateAction(rcArmChute);
                if (armChute == null)
                {
                    JUtil.LogMessage(null, "armChute is null");
                }

                MethodInfo rcDisarmChute = rcModuleRealChute.GetMethod("GUIDisarm", BindingFlags.Instance | BindingFlags.Public);
                disarmChute = DynamicMethodDelegateFactory.CreateAction(rcDisarmChute);
                if (disarmChute == null)
                {
                    JUtil.LogMessage(null, "disarmChute is null");
                }

                MethodInfo rcDeployChute = rcModuleRealChute.GetMethod("GUIDeploy", BindingFlags.Instance | BindingFlags.Public);
                deployChute = DynamicMethodDelegateFactory.CreateAction(rcDeployChute);
                if (deployChute == null)
                {
                    JUtil.LogMessage(null, "deployChute is null");
                }

                MethodInfo rcCutChute = rcModuleRealChute.GetMethod("GUICut", BindingFlags.Instance | BindingFlags.Public);
                cutChute = DynamicMethodDelegateFactory.CreateAction(rcCutChute);
                if (cutChute == null)
                {
                    JUtil.LogMessage(null, "cutChute is null");
                }

                rcArmed = rcModuleRealChute.GetField("armed", BindingFlags.Instance | BindingFlags.Public);
                if (rcArmed == null)
                {
                    JUtil.LogMessage(null, "rcArmed is null");
                }

                rcSafeState = rcModuleRealChute.GetField("safeState", BindingFlags.Instance | BindingFlags.Public);
                if (rcSafeState == null)
                {
                    JUtil.LogMessage(null, "rcSafeState is null");
                }
            }
            catch (Exception e)
            {
                JUtil.LogMessage(null, "static JSIParachute exception {0}", e);
                rcModuleRealChute = null;
                getAnyDeployed = null;
                armChute = null;
                disarmChute = null;
                deployChute = null;
                cutChute = null;
                rcArmed = null;
                rcSafeState = null;
            }

            if (rcModuleRealChute != null
                && armChute != null
                && getAnyDeployed != null
                && disarmChute != null
                && deployChute != null
                && cutChute != null
                && rcArmed != null
                && rcSafeState != null
                )
            {
                rcFound = true;
            }
            else
            {
                rcFound = false;
            }
        }

        public JSIParachute(Vessel myVessel)
        {
            vessel = myVessel;
            JUtil.LogMessage(this, "A supported version of RealChute is {0}", (rcFound) ? "present" : "not available");
        }

        public void ArmParachutes(bool state)
        {
            if (rcFound && vessel != null)
            {
                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                if (state)
                {
                    for (int i = 0; i < comp.availableRealChutes.Count; ++i)
                    {
                        armChute(comp.availableRealChutes[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < comp.availableRealChutes.Count; ++i)
                    {
                        disarmChute(comp.availableRealChutes[i]);
                    }
                }
            }
        }

        public bool ArmParachutesState()
        {
            if (vessel == null)
            {
                return false; // early
            }

            bool anyArmed = false;
            if (rcFound)
            {
                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                for (int i = 0; i < comp.availableRealChutes.Count; ++i)
                {
                    if ((bool)rcArmed.GetValue(comp.availableRealChutes[i]) == true)
                    {
                        anyArmed = true;
                        break;
                    }
                }
            }

            return anyArmed;
        }

        /// <summary>
        /// Cuts any deployed parachutes.  To avoid accidental cuts,
        /// CutParachutes is a separate method from DeployParachutes.
        /// </summary>
        /// <param name="state"></param>
        public void CutParachutes(bool state)
        {
            if (!state && vessel != null)
            {
                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);

                if (rcFound)
                {
                    for (int i = 0; i < comp.availableRealChutes.Count; ++i)
                    {
                        cutChute(comp.availableRealChutes[i]);
                    }
                }

                for(int i=0; i<comp.availableParachutes.Count; ++i)
                {
                    if (comp.availableParachutes[i].deploymentState == ModuleParachute.deploymentStates.DEPLOYED || comp.availableParachutes[i].deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED)
                    {
                        comp.availableParachutes[i].CutParachute();
                    }
                }
            }
        }

        /// <summary>
        /// Deploys stowed parachutes.
        /// </summary>
        /// <param name="state"></param>
        public void DeployParachutes(bool state)
        {
            if (state && vessel!=null)
            {
                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);

                if (rcFound)
                {
                    for (int i = 0; i < comp.availableRealChutes.Count; ++i)
                    {
                        deployChute(comp.availableRealChutes[i]);
                    }
                }

                for (int i = 0; i < comp.availableParachutes.Count; ++i)
                {
                    if (comp.availableParachutes[i].deploymentState == ModuleParachute.deploymentStates.STOWED)
                    {
                        comp.availableParachutes[i].Deploy();
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if any parachutes have deployed.
        /// </summary>
        /// <returns></returns>
        public bool DeployParachutesState()
        {
            if (vessel == null)
            {
                return false; // early
            }

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            bool anyDeployed = comp.anyParachutesDeployed;

            if (rcFound && !anyDeployed)
            {
                for (int i = 0; i < comp.availableRealChutes.Count; ++i)
                {
                    if (getAnyDeployed(comp.availableRealChutes[i]) == true)
                    {
                        anyDeployed = true;
                        break;
                    }
                }
            }

            return anyDeployed;
        }

        /// <summary>
        /// Returns true if all parachutes are within their safe-to-deploy envelope.
        /// </summary>
        /// <returns></returns>
        public bool ParachutesSafeState()
        {
            if (vessel == null)
            {
                return false; // early
            }

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            bool allSafe = comp.allParachutesSafe;
            for (int i = 0; i < comp.availableRealChutes.Count; ++i)
            {
                object state = rcSafeState.GetValue(comp.availableRealChutes[i]);
                if ((int)state != (int)SafeState.SAFE)
                {
                    allSafe = false;
                    break;
                }
            }

            return allSafe;
        }

        /// <summary>
        /// Returns a numeric value indicating the degree of safety in deploying.
        /// </summary>
        /// <returns>1 if all parachutes are safe, -1 if no parachute is safe,
        /// and 0 if it is a mix.</returns>
        public double ParachuteSafetyValue()
        {
            bool allSafe = true;
            bool allDangerous = true;

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);

            for (int i = 0; i < comp.availableParachutes.Count; ++i)
            {
                if (comp.availableParachutes[i].deploySafe != "Safe")
                {
                    allSafe = false;
                }
                else
                {
                    allDangerous = false;
                }
            }

            for (int i = 0; i < comp.availableRealChutes.Count; ++i)
            {
                object state = rcSafeState.GetValue(comp.availableRealChutes[i]);
                if ((int)state != (int)SafeState.SAFE)
                {
                    allSafe = false;
                }
                if ((int)state != (int)SafeState.DANGEROUS)
                {
                    allDangerous = false;
                }
            }

            if (allSafe)
            {
                return 1.0;
            }
            else if (allDangerous)
            {
                return -1.0;
            }
            else
            {
                return 0.0;
            }
        }
    }
}
