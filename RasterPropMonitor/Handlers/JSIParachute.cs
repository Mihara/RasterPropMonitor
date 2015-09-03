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
        static private readonly Type rcModuleRealChute;
        static private readonly DynamicFuncBool getAnyDeployed;
        static private readonly DynamicAction armChute;
        static private readonly DynamicAction disarmChute;
        static private readonly DynamicAction deployChute;
        static private readonly DynamicAction cutChute;
        static private readonly FieldInfo rcArmed;
        static private readonly bool rcFound;

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

                PropertyInfo rcAnyDeployed = rcModuleRealChute.GetProperty("anyDeployed", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo rcGetAnyDeployed = rcAnyDeployed.GetGetMethod();
                getAnyDeployed = DynamicMethodDelegateFactory.CreateFuncBool(rcGetAnyDeployed);
                if (getAnyDeployed == null)
                {
                    JUtil.LogMessage(null, "getAnyDeployed is null");
                }

                MethodInfo rcArmChute = rcModuleRealChute.GetMethod("GUIArm", BindingFlags.Instance | BindingFlags.Public);
                armChute = DynamicMethodDelegateFactory.CreateAction(rcArmChute);
                if(armChute == null)
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
            }
            catch(Exception e)
            {
                JUtil.LogMessage(null, "static JSIParachute exception {0}", e);
                rcModuleRealChute = null;
                getAnyDeployed = null;
                armChute = null;
                disarmChute = null;
                deployChute = null;
                cutChute = null;
                rcArmed = null;
            }

            if (rcModuleRealChute != null 
                && armChute != null
                && getAnyDeployed != null
                && disarmChute != null 
                && deployChute != null
                && cutChute != null 
                && rcArmed != null
                )
            {
                rcFound = true;
            }
            else
            {
                rcFound = false;
            }
        }

        public JSIParachute(Vessel _vessel) : base(_vessel)
        {
            JUtil.LogMessage(this, "A supported version of RealChute is {0}", (rcFound) ? "present" : "not available");
        }

        public void ArmParachutes(bool state)
        {
            if (rcFound)
            {
                if (state)
                {
                    foreach (PartModule module in FindRealChuteIn(vessel))
                    {
                        armChute(module);
                    }
                }
                else
                {
                    foreach (PartModule module in FindRealChuteIn(vessel))
                    {
                        disarmChute(module);
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
                foreach (PartModule module in FindRealChuteIn(vessel))
                {
                    if ((bool)rcArmed.GetValue(module) == true)
                    {
                        anyArmed = true;
                        break;
                    }
                }
            }

            return anyArmed;
        }

        // Just to avoid accidental cuts, CutParachutes is a separate method from DeployParachutes
        public void CutParachutes(bool state)
        {
            if (!state)
            {
                if (rcFound)
                {
                    foreach (PartModule module in FindRealChuteIn(vessel))
                    {
                        cutChute(module);
                    }
                }

                foreach (ModuleParachute module in FindStockChuteIn(vessel))
                {
                    if (module.deploymentState == ModuleParachute.deploymentStates.DEPLOYED || module.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED)
                    {
                        module.CutParachute();
                    }
                }
            }
        }

        public void DeployParachutes(bool state)
        {
            if (state)
            {
                if (rcFound)
                {
                    foreach (PartModule module in FindRealChuteIn(vessel))
                    {
                        deployChute(module);
                    }
                }

                foreach (ModuleParachute module in FindStockChuteIn(vessel))
                {
                    if (module.deploymentState == ModuleParachute.deploymentStates.STOWED)
                    {
                        module.Deploy();
                    }
                }
            }
        }

        public bool DeployParachutesState()
        {
            if (vessel == null)
            {
                return false; // early
            }

            bool anyDeployed = false;

            if (rcFound)
            {
                foreach (PartModule module in FindRealChuteIn(vessel))
                {
                    if (getAnyDeployed(module) == true)
                    {
                        anyDeployed = true;
                        break;
                    }
                }
            }

            if (!anyDeployed)
            {
                foreach (ModuleParachute module in FindStockChuteIn(vessel))
                {
                    if (module.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED || module.deploymentState == ModuleParachute.deploymentStates.DEPLOYED)
                    {
                        anyDeployed = true;
                        break;
                    }
                }
            }

            return anyDeployed;
        }

        public bool ParachutesSafeState()
        {
            if (vessel == null)
            {
                return false; // early
            }

            bool allSafe = true;
            foreach (ModuleParachute module in FindStockChuteIn(vessel))
            {
                if (module.deploySafe != "Safe")
                {
                    allSafe = false;
                    break;
                }
            }

            return allSafe;
        }

        private static IEnumerable<PartModule> FindRealChuteIn(Vessel vessel)
        {
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module.GetType() == rcModuleRealChute)
                    {
                        yield return module;
                    }
                }
            }
        }

        private static IEnumerable<ModuleParachute> FindStockChuteIn(Vessel vessel)
        {
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleParachute)
                    {
                        yield return (module as ModuleParachute);
                    }
                }
            }
        }
    }
}
