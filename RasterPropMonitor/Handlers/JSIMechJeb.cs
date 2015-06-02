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
    /// 
    /// TODO: Add an Update method, and cache the results, instead of computing
    /// on the fly.  Even better: Move these methods out of InternalModule, and
    /// have RPMC instantiate them as child objects.
    /// </summary>
    internal class JSIMechJeb : InternalModule
    {
        // MechJebCore
        private static readonly Type mjMechJebCore_t;
        // MechJebCore.GetComputerModule(string)
        private static readonly MethodInfo mjGetComputerModule;
        // MechJebCore.target
        private static readonly FieldInfo mjCoreTarget;

        // AbsoluteVector
        private static readonly Type mjAbsoluteVector_t;
        // AbsoluteVector.latitude
        private static readonly FieldInfo mjAbsoluteVectorLat;
        // AbsoluteVector.longitude
        private static readonly FieldInfo mjAbsoluteVectorLon;
        // AbsoluteVector.(double)
        private static readonly MethodInfo mjAbsoluteVectorToDouble;

        // EditableAngle
        private static readonly Type mjEditableAngle_t;

        // MechJebModuleLandingPredictions
        private static readonly Type mjModuleLandingPredictions_t;
        // MechJebModuleLandingPredictions.GetResult()
        private static readonly MethodInfo mjPredictionsGetResult;
        // MechJebModuleLandingPredictions.enabled
        private static readonly MethodInfo mjPredictionsEnabled;

        // ReentrySimulation.Result
        private static readonly Type mjReentryResult_t;
        // ReentrySimulation.Result.outcome
        private static readonly FieldInfo mjReentryOutcome;
        // ReentrySimulation.Result.endPosition
        private static readonly FieldInfo mjReentryEndPosition;

        // MechJebModuleStageStats
        private static readonly Type mjModuleStageStats_t;
        // MechJebModuleStageStats.RequestUpdate()
        private static readonly MethodInfo mjRequestUpdate;
        // MechJebModuleStageStats.vacStats[]
        private static readonly FieldInfo mjVacStageStats;
        // MechJebModuleStageStats.atmoStats[]
        private static readonly FieldInfo mjAtmStageStats;

        // MechJebModuleTargetController
        private static readonly Type mjModuleTargetController_t;
        // MechJebModuleTargetController.targetLatitude
        private static readonly FieldInfo mjTargetLongitude;
        // MechJebModuleTargetController.targetLatitude
        private static readonly FieldInfo mjTargetLatitude;

        // KER VesselSimulator.Stage (MJ version)
        private static readonly Type mjStage_t;
        // VesselSimulator.Stage.deltaV
        private static readonly FieldInfo mjStageDv;

        // VesselExtensions.GetMasterMechJeb()
        private static readonly MethodInfo mjGetMasterMechJeb;

        public static readonly bool mjFound;

        static JSIMechJeb()
        {
            try
            {
                //--- Load all the types
                mjMechJebCore_t = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebCore");
                if (mjMechJebCore_t == null)
                {
                    throw new ArgumentNullException("mjMechJebCore_t");
                }

                Type mjVesselExtensions = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.VesselExtensions");
                if (mjVesselExtensions == null)
                {
                    throw new ArgumentNullException("mjVesselExtensions");
                }

                mjModuleLandingPredictions_t = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleLandingPredictions");
                if (mjModuleLandingPredictions_t == null)
                {
                    throw new ArgumentNullException("mjModuleLandingPredictions_t");
                }

                Type mjReentrySim_t = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.ReentrySimulation");
                if (mjReentrySim_t == null)
                {
                    throw new ArgumentNullException("mjReentrySim_t");
                }

                mjReentryResult_t = mjReentrySim_t.GetNestedType("Result");
                if (mjReentryResult_t == null)
                {
                    throw new ArgumentNullException("mjReentryResult_t");
                }

                mjModuleStageStats_t = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleStageStats");
                if (mjModuleStageStats_t == null)
                {
                    throw new ArgumentNullException("mjModuleStageStats_t");
                }

                mjAbsoluteVector_t = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.AbsoluteVector");
                if (mjAbsoluteVector_t == null)
                {
                    throw new ArgumentNullException("mjAbsoluteVector_t");
                }

                // Because KerbalEngineer.VesselSimulator.Stage is in KER and
                // MJ, we need to select the one in MechJeb.
                mjStage_t = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "KerbalEngineer.VesselSimulator.Stage" && t.Assembly == mjMechJebCore_t.Assembly);
                if (mjStage_t == null)
                {
                    throw new ArgumentNullException("mjStage_t");
                }

                mjModuleTargetController_t = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleTargetController");
                if (mjModuleTargetController_t == null)
                {
                    throw new ArgumentNullException("mjModuleTargetController_t");
                }

                mjEditableAngle_t = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "MuMech.EditableAngle");
                if (mjEditableAngle_t == null)
                {
                    throw new ArgumentNullException("mjEditableAngle_t");
                }
                foreach(MethodInfo method in mjEditableAngle_t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    // The method name reports as "op_Implicit", but there are two
                    if (method.ReturnType == typeof(System.Double))
                    {
                        mjAbsoluteVectorToDouble = method;
                        break;
                    }
                }
                if (mjAbsoluteVectorToDouble == null)
                {
                    throw new ArgumentNullException("mjAbsoluteVectorToDouble");
                }

                //--- Get the methods we need to use
                mjGetComputerModule = mjMechJebCore_t.GetMethod("GetComputerModule", new Type[] { typeof(string) });
                if (mjGetComputerModule == null)
                {
                    throw new ArgumentNullException("mjGetComputerModule");
                }

                mjGetMasterMechJeb = mjVesselExtensions.GetMethod("GetMasterMechJeb", BindingFlags.Static | BindingFlags.Public);
                if (mjGetMasterMechJeb == null)
                {
                    throw new ArgumentNullException("mjGetMasterMechJeb");
                }

                mjRequestUpdate = mjModuleStageStats_t.GetMethod("RequestUpdate", BindingFlags.Instance | BindingFlags.Public);
                if (mjRequestUpdate == null)
                {
                    throw new ArgumentNullException("mjRequestUpdate");
                }

                mjPredictionsGetResult = mjModuleLandingPredictions_t.GetMethod("GetResult", BindingFlags.Instance | BindingFlags.Public);
                if (mjPredictionsGetResult == null)
                {
                    throw new ArgumentNullException("mjPredictionsGetResult");
                }

                PropertyInfo mjPredictionsEnabledProperty = mjModuleLandingPredictions_t.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
                if(mjPredictionsEnabledProperty != null)
                {
                    mjPredictionsEnabled = mjPredictionsEnabledProperty.GetGetMethod();
                }
                if (mjPredictionsEnabled == null)
                {
                    throw new ArgumentNullException("mjPredictionsEnabled");
                }

                //--- And get the fields
                mjVacStageStats = mjModuleStageStats_t.GetField("vacStats", BindingFlags.Instance | BindingFlags.Public);
                if (mjVacStageStats == null)
                {
                    throw new ArgumentNullException("mjVacStageStats");
                }

                mjAtmStageStats = mjModuleStageStats_t.GetField("atmoStats", BindingFlags.Instance | BindingFlags.Public);
                if (mjAtmStageStats == null)
                {
                    throw new ArgumentNullException("mjAtmStageStats");
                }

                mjStageDv = mjStage_t.GetField("deltaV", BindingFlags.Instance | BindingFlags.Public);
                if(mjStageDv == null)
                {
                    throw new ArgumentNullException("mjStageDv");
                }

                mjReentryOutcome = mjReentryResult_t.GetField("outcome", BindingFlags.Instance | BindingFlags.Public);
                if (mjReentryOutcome == null)
                {
                    throw new ArgumentNullException("mjReentryOutcome");
                }

                mjReentryEndPosition = mjReentryResult_t.GetField("endPosition", BindingFlags.Instance | BindingFlags.Public);
                if (mjReentryEndPosition == null)
                {
                    throw new ArgumentNullException("mjReentryEndPosition");
                }

                mjAbsoluteVectorLat = mjAbsoluteVector_t.GetField("latitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjAbsoluteVectorLat == null)
                {
                    throw new ArgumentNullException("mjAbsoluteVectorLat");
                }

                mjAbsoluteVectorLon = mjAbsoluteVector_t.GetField("longitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjAbsoluteVectorLon == null)
                {
                    throw new ArgumentNullException("mjAbsoluteVectorLon");
                }

                mjTargetLongitude = mjModuleTargetController_t.GetField("targetLongitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjTargetLongitude == null)
                {
                    throw new ArgumentNullException("mjTargetLongitude");
                }

                mjTargetLatitude = mjModuleTargetController_t.GetField("targetLatitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjTargetLatitude == null)
                {
                    throw new ArgumentNullException("mjTargetLatitude");
                }

                mjCoreTarget = mjMechJebCore_t.GetField("target", BindingFlags.Instance | BindingFlags.Public);
                if (mjCoreTarget == null)
                {
                    throw new ArgumentNullException("mjCoreTarget");
                }

                mjFound = true;
            }
            catch(Exception e)
            {
                mjFound = false;
                print("JSIMechJeb: Exception triggered when configuring: " + e);
            }

            print("JSIMechJeb: mjFound is " + mjFound);
        }

        #region Internal Methods
        /// <summary>
        /// Invokes VesselExtensions.GetMasterMechJeb()
        /// </summary>
        /// <returns>The master MJ object</returns>
        internal object GetMasterMechJeb()
        {
            if (mjFound)
            {
                foreach (Part part in vessel.Parts)
                {
                    foreach (PartModule module in part.Modules)
                    {
                        if (module.GetType() == mjMechJebCore_t)
                        {
                            return mjGetMasterMechJeb.Invoke(null, new object[] { vessel });
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Return the latest landing simulation results, or null if there aren't any.
        /// </summary>
        /// <returns></returns>
        private object GetLandingResults(object masterMechJeb)
        {
            object mjCore = (masterMechJeb == null) ? GetMasterMechJeb() : masterMechJeb;
            if (mjCore != null)
            {
                object predictor = mjGetComputerModule.Invoke(mjCore, new object[] { "MechJebModuleLandingPredictions" });
                if (predictor != null && (bool)mjPredictionsEnabled.Invoke(predictor, null) == true)
                {
                    return mjPredictionsGetResult.Invoke(predictor, null);
                }
            }

            return null;
        }
        #endregion

        #region Defined Variable queries
        /// <summary>
        /// Returns whether we've been able to link with MechJeb.  While this
        /// really should be static, it isn't, since RPMC always assumes it
        /// should supply the 'this' pointer (or whatever C# calls it).
        /// </summary>
        /// <returns>true if MJ is available for query</returns>
        public bool GetMechJebAvailable()
        {
            return mjFound;
        }

        /// <summary>
        /// Returns the predicted landing error to a target.
        /// </summary>
        /// <returns>-1 if the prediction is unavailable for whatever reason</returns>
        public double GetLandingError()
        {
            double landingError = -1.0;
            if (mjFound)
            {
                try
                {
                    object masterMechJeb = GetMasterMechJeb();
                    object result = GetLandingResults(masterMechJeb);
                    if(result != null)
                    {
                        object outcome = mjReentryOutcome.GetValue(result);
                        if (outcome != null && outcome.ToString() == "LANDED")
                        {
                            object endPosition = mjReentryEndPosition.GetValue(result);
                            if (endPosition != null)
                            {
                                double lat = (double)mjAbsoluteVectorLat.GetValue(endPosition);
                                double lon = (double)mjAbsoluteVectorLon.GetValue(endPosition);
                                double alt = FinePrint.Utilities.CelestialUtilities.TerrainAltitude(vessel.mainBody, lat, lon);

                                object target = mjCoreTarget.GetValue(masterMechJeb);
                                object targetLatField = mjTargetLatitude.GetValue(target);
                                object targetLonField = mjTargetLongitude.GetValue(target);
                                double targetLat = (double)mjAbsoluteVectorToDouble.Invoke(null, new object[] {targetLatField});
                                double targetLon = (double)mjAbsoluteVectorToDouble.Invoke(null, new object[] {targetLonField});
                                double targetAlt = FinePrint.Utilities.CelestialUtilities.TerrainAltitude(vessel.mainBody, targetLat, targetLon);

                                landingError = Vector3d.Distance(vessel.mainBody.GetRelSurfacePosition(lat, lon, alt),
                                                   vessel.mainBody.GetRelSurfacePosition(targetLat, targetLon, targetAlt));
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    JUtil.LogErrorMessage(this, "Exception trap in GetLandingError(): {0}", e);
                }
            }

            return landingError;
        }

        /// <summary>
        /// Returns net dV of the vessel.
        /// </summary>
        /// <returns>Returns NaN if MJ is unavailable.</returns>
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
                            else
                            {
                                dV = 0.0;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    JUtil.LogErrorMessage(this, "Exception trap in GetDeltaV(): {0}", e);
                }
            }

            return dV;
        }

        /// <summary>
        /// Returns dV of the active stage
        /// </summary>
        /// <returns>Returns NaN if MJ is unavailable.</returns>
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
                            else
                            {
                                dV = 0.0;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    JUtil.LogErrorMessage(this, "Exception trap in GetStageDeltaV(): {0}", e);
                }
            }

            return dV;
        }
        #endregion
    }
}
