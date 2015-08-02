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
        private static readonly Type rcModuleRealChute;
        private static readonly MethodInfo rcGetAnyDeployed;
        private static readonly MethodInfo rcArmChute;
        private static readonly MethodInfo rcDisarmChute;
        private static readonly MethodInfo rcDeployChute;
        private static readonly MethodInfo rcCutChute;
        private static readonly FieldInfo rcArmed;
        private static readonly bool rcFound;

        private bool anyDeployed;
        private bool anyArmed;
        private bool allSafe;

        static JSIParachute()
        {
            try
            {
                rcModuleRealChute = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "RealChute.RealChuteModule");

                PropertyInfo rcAnyDeployed = rcModuleRealChute.GetProperty("anyDeployed", BindingFlags.Instance | BindingFlags.Public);
                if (rcAnyDeployed == null)
                {
                    throw new ArgumentNullException();
                }
                rcGetAnyDeployed = rcAnyDeployed.GetGetMethod();

                rcArmChute = rcModuleRealChute.GetMethod("GUIArm", BindingFlags.Instance | BindingFlags.Public);
                rcDisarmChute = rcModuleRealChute.GetMethod("GUIDisarm", BindingFlags.Instance | BindingFlags.Public);
                rcDeployChute = rcModuleRealChute.GetMethod("GUIDeploy", BindingFlags.Instance | BindingFlags.Public);
                rcCutChute = rcModuleRealChute.GetMethod("GUICut", BindingFlags.Instance | BindingFlags.Public);

                rcArmed = rcModuleRealChute.GetField("armed", BindingFlags.Instance | BindingFlags.Public);
            }
            catch (Exception)
            {
                rcModuleRealChute = null;
                rcGetAnyDeployed = null;
                rcArmChute = null;
                rcDisarmChute = null;
                rcDeployChute = null;
                rcCutChute = null;
                rcArmed = null;
            }

            if (rcModuleRealChute != null && rcArmChute != null &&
                rcGetAnyDeployed != null && rcDisarmChute != null && rcDeployChute != null &&
                rcCutChute != null && rcArmed != null)
            {
                rcFound = true;
            }
            else
            {
                rcFound = false;
            }
        }

        public JSIParachute(Vessel _vessel) : base(_vessel) { }

        public void ArmParachutes(bool state)
        {
            if (rcFound)
            {
                if (state)
                {
                    foreach (PartModule module in FindRealChuteIn(vessel))
                    {
                        rcArmChute.Invoke(module, null);
                    }
                }
                else
                {
                    foreach (PartModule module in FindRealChuteIn(vessel))
                    {
                        rcDisarmChute.Invoke(module, null);
                    }
                }
            }

            anyArmed = state;
        }

        public bool ArmParachutesState()
        {
            if (moduleInvalidated)
            {
                UpdateParachuteState();
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
                        rcCutChute.Invoke(module, null);
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
                        rcDeployChute.Invoke(module, null);
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
            if (moduleInvalidated)
            {
                UpdateParachuteState();
            }

            return anyDeployed;
        }

        public bool ParachutesSafeState()
        {
            if (moduleInvalidated)
            {
                UpdateParachuteState();
            }

            return allSafe;
        }

        private void UpdateParachuteState()
        {
            moduleInvalidated = false;

            anyDeployed = false;

            allSafe = true;

            if (vessel == null)
            {
                return; // early
            }

            if (rcFound)
            {
                foreach (PartModule module in FindRealChuteIn(vessel))
                {
                    if ((bool)rcGetAnyDeployed.Invoke(module, null) == true)
                    {
                        anyDeployed = true;
                        break;
                    }
                }

                anyArmed = false;
                foreach (PartModule module in FindRealChuteIn(vessel))
                {
                    if ((bool)rcArmed.GetValue(module) == true)
                    {
                        anyArmed = true;
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

            if (allSafe)
            {
                allSafe = true;
                foreach (ModuleParachute module in FindStockChuteIn(vessel))
                {
                    if (module.deploySafe != "Safe")
                    {
                        allSafe = false;
                        break;
                    }
                }
            }
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
