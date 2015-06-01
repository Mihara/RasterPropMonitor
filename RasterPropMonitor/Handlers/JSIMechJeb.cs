using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;

namespace JSI
{
    /// <summary>
    /// JSIMechJeb provides an interface with the MechJeb plugin using
    /// reflection, which allows us to avoid the need for hard dependencies.
    /// </summary>
    internal class JSIMechJeb : InternalModule
    {
        private static readonly Type mjMechJebCore;
        private static readonly Type mjComputerModule;
        private static readonly Type mjModuleStageStats;
        private static readonly Type mjStage;
        private static readonly MethodInfo mjGetComputerModule;
        private static readonly MethodInfo mjGetMasterMechJeb;
        private static readonly MethodInfo mjRequestUpdate;
        private static readonly FieldInfo mjVacStageStats;
        private static readonly FieldInfo mjAtmStageStats;
        private static readonly FieldInfo mjStageDv;
        public static readonly bool mjFound;

        static JSIMechJeb()
        {
            try
            {
                mjMechJebCore = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebCore");

                Type mjVesselExtensions = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.VesselExtensions");
                if (mjVesselExtensions == null)
                {
                    throw new ArgumentNullException();
                }

                mjComputerModule = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.ComputerModule");
                if (mjComputerModule == null)
                {
                    throw new ArgumentNullException();
                }

                mjModuleStageStats = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleStageStats");
                if (mjModuleStageStats == null)
                {
                    throw new ArgumentNullException();
                }

                // Because KerbalEngineer.VesselSimulator.Stage is in KER and
                // MJ, we use FirstOrDefault here instead of SingleOrDefault.
                mjStage = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .FirstOrDefault(t => t.FullName == "KerbalEngineer.VesselSimulator.Stage");
                if (mjStage == null)
                {
                    throw new ArgumentNullException(mjStage.ToString());
                }

                mjGetComputerModule = mjMechJebCore.GetMethod("GetComputerModule", new Type[] { typeof(string) });
                if (mjGetComputerModule == null)
                {
                    throw new ArgumentNullException(mjGetComputerModule.ToString());
                }

                mjGetMasterMechJeb = mjVesselExtensions.GetMethod("GetMasterMechJeb", BindingFlags.Static | BindingFlags.Public);
                if (mjGetMasterMechJeb == null)
                {
                    throw new ArgumentNullException(mjGetMasterMechJeb.ToString());
                }

                mjRequestUpdate = mjModuleStageStats.GetMethod("RequestUpdate", BindingFlags.Instance | BindingFlags.Public);
                if (mjRequestUpdate == null)
                {
                    throw new ArgumentNullException(mjRequestUpdate.ToString());
                }

                mjVacStageStats = mjModuleStageStats.GetField("vacStats", BindingFlags.Instance | BindingFlags.Public);
                if (mjVacStageStats == null)
                {
                    throw new ArgumentNullException(mjVacStageStats.ToString());
                }

                mjAtmStageStats = mjModuleStageStats.GetField("atmoStats", BindingFlags.Instance | BindingFlags.Public);
                if (mjAtmStageStats == null)
                {
                    throw new ArgumentNullException(mjAtmStageStats.ToString());
                }

                mjStageDv = mjStage.GetField("deltaV", BindingFlags.Instance | BindingFlags.Public);
                if(mjStageDv == null)
                {
                    throw new ArgumentNullException(mjStageDv.ToString());
                }

                mjFound = true;
            }
            catch(Exception e)
            {
                mjMechJebCore = null;
                mjGetMasterMechJeb = null;
                mjFound = false;
            }

            print("JSIMechJeb: mjFound is " + mjFound);
        }

        public static bool GetMechJebAvailable()
        {
            return mjFound;
        }

        private object GetMasterMechJeb()
        {
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module.GetType() == mjMechJebCore)
                    {
                        return mjGetMasterMechJeb.Invoke(null, new object[] { vessel });
                    }
                }
            }

            return null;
        }

        public double GetDeltaV()
        {
            double dV = double.NaN;
            if (mjFound)
            {
                try
                {
                    object mjCore = GetMasterMechJeb();
                    if (mjCore != null)
                    {
                        object stagestats = mjGetComputerModule.Invoke(mjCore, new object[] {"MechJebModuleStageStats"});

                        mjRequestUpdate.Invoke(stagestats, new object[] { this });

                        object atmStatsO = mjAtmStageStats.GetValue(stagestats);
                        object vacStatsO = mjVacStageStats.GetValue(stagestats);

                        if(atmStatsO != null && vacStatsO != null)
                        {
                            object[] atmStats = (object[])(atmStatsO);
                            object[] vacStats = (object[])(vacStatsO);

                            if (atmStats.Length > 0 && vacStats.Length > 0)
                            {
                                double dVatm = 0.0;
                                double dVvac = 0.0;

                                foreach (object stage in atmStats)
                                {
                                    dVatm += (double)mjStageDv.GetValue(stage);
                                }
                                foreach (object stage in vacStats)
                                {
                                    dVvac += (double)mjStageDv.GetValue(stage);
                                }
                                dV = UtilMath.LerpUnclamped(dVvac, dVatm, vessel.atmDensity);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    JUtil.LogErrorMessage(this, "Exception trap in GetDeltaV()");
                }
            }

            return dV;
        }

        public double GetStageDeltaV()
        {
            double dV = double.NaN;
            if (mjFound)
            {
                try
                {
                    object mjCore = GetMasterMechJeb();
                    if (mjCore != null)
                    {
                        object stagestats = mjGetComputerModule.Invoke(mjCore, new object[] { "MechJebModuleStageStats" });

                        mjRequestUpdate.Invoke(stagestats, new object[] { this });

                        object atmStatsO = mjAtmStageStats.GetValue(stagestats);
                        object vacStatsO = mjVacStageStats.GetValue(stagestats);

                        if (atmStatsO != null && vacStatsO != null)
                        {
                            object[] atmStats = (object[])(atmStatsO);
                            object[] vacStats = (object[])(vacStatsO);

                            if (atmStats.Length > 0 && vacStats.Length > 0)
                            {
                                double dVatm = (double)mjStageDv.GetValue(atmStats[atmStats.Length-1]);
                                double dVvac = (double)mjStageDv.GetValue(vacStats[vacStats.Length - 1]);

                                dV = UtilMath.LerpUnclamped(dVvac, dVatm, vessel.atmDensity);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    JUtil.LogErrorMessage(this, "Exception trap in GetStageDeltaV()");
                }
            }

            return dV;
        }
    }
}
