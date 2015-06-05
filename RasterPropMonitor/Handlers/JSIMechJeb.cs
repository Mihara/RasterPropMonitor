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
    internal class JSIMechJeb : IJSIModule
    {
        #region Reflection Definitions
        // MechJebCore
        private static readonly Type mjMechJebCore_t;
        // MechJebCore.GetComputerModule(string)
        private static readonly MethodInfo mjGetComputerModule;
        // MechJebCore.target
        private static readonly FieldInfo mjCoreTarget;

        // AbsoluteVector
        // AbsoluteVector.latitude
        private static readonly FieldInfo mjAbsoluteVectorLat;
        // AbsoluteVector.longitude
        private static readonly FieldInfo mjAbsoluteVectorLon;
        // AbsoluteVector.(double)
        private static readonly MethodInfo mjAbsoluteVectorToDouble;

        // MechJebModuleLandingPredictions
        // MechJebModuleLandingPredictions.GetResult()
        private static readonly MethodInfo mjPredictionsGetResult;

        // ReentrySimulation.Result
        // ReentrySimulation.Result.outcome
        private static readonly FieldInfo mjReentryOutcome;
        // ReentrySimulation.Result.endPosition
        private static readonly FieldInfo mjReentryEndPosition;

        // ComputerModule
        // ComputerModule.enabled (get)
        private static readonly MethodInfo mjModuleEnabled;

        // MechJebModuleStageStats
        // MechJebModuleStageStats.RequestUpdate()
        private static readonly MethodInfo mjRequestUpdate;
        // MechJebModuleStageStats.vacStats[]
        private static readonly FieldInfo mjVacStageStats;
        // MechJebModuleStageStats.atmoStats[]
        private static readonly FieldInfo mjAtmStageStats;

        // MechJebModuleTargetController
        // MechJebModuleTargetController.targetLatitude
        private static readonly FieldInfo mjTargetLongitude;
        // MechJebModuleTargetController.targetLatitude
        private static readonly FieldInfo mjTargetLatitude;

        // KER VesselSimulator.Stage (MJ version)
        // VesselSimulator.Stage.deltaV
        private static readonly FieldInfo mjStageDv;

        // VesselExtensions.GetMasterMechJeb()
        private static readonly MethodInfo mjGetMasterMechJeb;
        #endregion

        private static readonly bool mjFound;

        private bool landingCurrent, deltaVCurrent;
        private double deltaV, deltaVStage;

        private double landingLat, landingLon, landingAlt, landingErr = -1.0;

        static JSIMechJeb()
        {
            try
            {
                var loadedMechJebAssy = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name == "MechJeb2");

                if(loadedMechJebAssy == null)
                {
                    mjFound = false;
                    return;
                }

                //--- Process all the reflection info
                // MechJebCore
                mjMechJebCore_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebCore");
                if (mjMechJebCore_t == null)
                {
                    throw new ArgumentNullException("mjMechJebCore_t");
                }
                mjGetComputerModule = mjMechJebCore_t.GetMethod("GetComputerModule", new Type[] { typeof(string) });
                if (mjGetComputerModule == null)
                {
                    throw new ArgumentNullException("mjGetComputerModule");
                }
                mjCoreTarget = mjMechJebCore_t.GetField("target", BindingFlags.Instance | BindingFlags.Public);
                if (mjCoreTarget == null)
                {
                    throw new ArgumentNullException("mjCoreTarget");
                }

                // VesselExtensions
                Type mjVesselExtensions_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.VesselExtensions");
                if (mjVesselExtensions_t == null)
                {
                    throw new ArgumentNullException("mjVesselExtensions_t");
                }
                mjGetMasterMechJeb = mjVesselExtensions_t.GetMethod("GetMasterMechJeb", BindingFlags.Static | BindingFlags.Public);
                if (mjGetMasterMechJeb == null)
                {
                    throw new ArgumentNullException("mjGetMasterMechJeb");
                }

                // MechJebModuleLandingPredictions
                Type mjModuleLandingPredictions_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleLandingPredictions");
                if (mjModuleLandingPredictions_t == null)
                {
                    throw new ArgumentNullException("mjModuleLandingPredictions_t");
                }
                mjPredictionsGetResult = mjModuleLandingPredictions_t.GetMethod("GetResult", BindingFlags.Instance | BindingFlags.Public);
                if (mjPredictionsGetResult == null)
                {
                    throw new ArgumentNullException("mjPredictionsGetResult");
                }

                // MuMech.ReentrySimulation.Result
                Type mjReentrySim_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.ReentrySimulation");
                if (mjReentrySim_t == null)
                {
                    throw new ArgumentNullException("mjReentrySim_t");
                }
                Type mjReentryResult_t = mjReentrySim_t.GetNestedType("Result");
                if (mjReentryResult_t == null)
                {
                    throw new ArgumentNullException("mjReentryResult_t");
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

                // MechJebModuleStageStats
                Type mjModuleStageStats_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleStageStats");
                if (mjModuleStageStats_t == null)
                {
                    throw new ArgumentNullException("mjModuleStageStats_t");
                }
                mjRequestUpdate = mjModuleStageStats_t.GetMethod("RequestUpdate", BindingFlags.Instance | BindingFlags.Public);
                if (mjRequestUpdate == null)
                {
                    throw new ArgumentNullException("mjRequestUpdate");
                }
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

                // AbsoluteVector
                Type mjAbsoluteVector_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.AbsoluteVector");
                if (mjAbsoluteVector_t == null)
                {
                    throw new ArgumentNullException("mjAbsoluteVector_t");
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

                // KerbalEngineer.VesselSimulator.Stage
                Type mjStage_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "KerbalEngineer.VesselSimulator.Stage");
                if (mjStage_t == null)
                {
                    throw new ArgumentNullException("mjStage_t");
                }
                mjStageDv = mjStage_t.GetField("deltaV", BindingFlags.Instance | BindingFlags.Public);
                if (mjStageDv == null)
                {
                    throw new ArgumentNullException("mjStageDv");
                }

                // MechJebModuleTargetController
                Type mjModuleTargetController_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleTargetController");
                if (mjModuleTargetController_t == null)
                {
                    throw new ArgumentNullException("mjModuleTargetController_t");
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

                // EditableAngle
                Type mjEditableAngle_t = loadedMechJebAssy.assembly.GetExportedTypes()
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

                // ComputerModule
                Type mjComputerModule_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.ComputerModule");
                PropertyInfo mjModuleEnabledProperty = mjComputerModule_t.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
                if (mjModuleEnabledProperty != null)
                {
                    mjModuleEnabled = mjModuleEnabledProperty.GetGetMethod();
                }
                if (mjModuleEnabled == null)
                {
                    throw new ArgumentNullException("mjModuleEnabled");
                }

                mjFound = true;
            }
            catch(Exception e)
            {
                mjFound = false;
                JUtil.LogMessage(null, "JSIMechJeb: Exception triggered when configuring: " + e);
            }

            JUtil.LogMessage(null, "JSIMechJeb: mjFound is " + mjFound);
        }

        public JSIMechJeb(Vessel _vessel) : base(_vessel) { }

        private void InvalidateResults()
        {
            landingCurrent = false;
            deltaVCurrent = false;
        
            deltaV = 0.0;
            deltaVStage = 0.0;

            landingLat = 0.0;
            landingLon = 0.0;
            landingAlt = 0.0;
            landingErr = -1.0;
        }

        #region Internal Methods
        /// <summary>
        /// Invokes VesselExtensions.GetMasterMechJeb()
        /// </summary>
        /// <returns>The master MJ object</returns>
        private object GetMasterMechJeb()
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
        /// Fetch a computer module from the MJ core
        /// </summary>
        /// <param name="masterMechJeb"></param>
        /// <param name="computerModule"></param>
        /// <returns></returns>
        private object GetComputerModule(object masterMechJeb, string computerModule)
        {
            return mjGetComputerModule.Invoke(masterMechJeb, new object[] { computerModule });
        }

        private bool ModuleEnabled(object module)
        {
            return (bool)mjModuleEnabled.Invoke(module, null);
        }

        /// <summary>
        /// Return the latest landing simulation results, or null if there aren't any.
        /// </summary>
        /// <returns></returns>
        private object GetLandingResults(object masterMechJeb)
        {
            object predictor = GetComputerModule(masterMechJeb, "MechJebModuleLandingPredictions");
            if (predictor != null && ModuleEnabled(predictor) == true)
            {
                return mjPredictionsGetResult.Invoke(predictor, null);
            }

            return null;
        }
        #endregion

        #region Updater methods
        /// <summary>
        /// Update the landing prediction stats
        /// </summary>
        private void UpdateLandingStats()
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
                            landingLat = (double)mjAbsoluteVectorLat.GetValue(endPosition);
                            landingLon = (double)mjAbsoluteVectorLon.GetValue(endPosition);
                            // Small fudge factor - we define 0, 0 as "invalid", so always make sure
                            // this value is just off of 0.  This is an error of about 0* 0' 0.4" for
                            // this case only
                            if (landingLat == 0.0)
                            {
                                landingLat = 0.0001;
                            }
                            if (landingLon == 0.0)
                            {
                                landingLon = 0.0001;
                            }
                            landingAlt = FinePrint.Utilities.CelestialUtilities.TerrainAltitude(vessel.mainBody, landingLat, landingLon);

                            object target = mjCoreTarget.GetValue(masterMechJeb);
                            object targetLatField = mjTargetLatitude.GetValue(target);
                            object targetLonField = mjTargetLongitude.GetValue(target);
                            double targetLat = (double)mjAbsoluteVectorToDouble.Invoke(null, new object[] {targetLatField});
                            double targetLon = (double)mjAbsoluteVectorToDouble.Invoke(null, new object[] {targetLonField});
                            double targetAlt = FinePrint.Utilities.CelestialUtilities.TerrainAltitude(vessel.mainBody, targetLat, targetLon);

                            landingErr = Vector3d.Distance(vessel.mainBody.GetRelSurfacePosition(landingLat, landingLon, landingAlt),
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

        /// <summary>
        /// Updates dV stats (dV and dVStage)
        /// </summary>
        private void UpdateDeltaVStats()
        {
            try
            {
                object mjCore = GetMasterMechJeb();
                if (mjCore != null)
                {
                    object stagestats = GetComputerModule(mjCore, "MechJebModuleStageStats");

                    mjRequestUpdate.Invoke(stagestats, new object[] { this });

                    object atmStatsO = mjAtmStageStats.GetValue(stagestats);
                    object vacStatsO = mjVacStageStats.GetValue(stagestats);

                    if (atmStatsO != null && vacStatsO != null)
                    {
                        object[] atmStats = (object[])(atmStatsO);
                        object[] vacStats = (object[])(vacStatsO);

                        if (atmStats.Length > 0 && vacStats.Length == atmStats.Length)
                        {
                            double atmospheresLocal = vessel.staticPressurekPa * PhysicsGlobals.KpaToAtmospheres;

                            deltaV = deltaVStage = 0.0;

                            for(int i=0; i<atmStats.Length; ++i)
                            {
                                double atm = (double)mjStageDv.GetValue(atmStats[i]);
                                double vac = (double)mjStageDv.GetValue(vacStats[i]);
                                double stagedV = UtilMath.LerpUnclamped(vac, atm, atmospheresLocal);

                                deltaV += stagedV;

                                if(i == (atmStats.Length-1))
                                {
                                    deltaVStage = stagedV;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Exception trap in UpdateDeltaVStats(): {0}", e);
            }
        }
        #endregion

        #region External interface
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
            if (mjFound)
            {
                if (moduleInvalidated)
                {
                    InvalidateResults();
                    moduleInvalidated = false;
                }

                if (landingCurrent == false)
                {
                    UpdateLandingStats();
                }

                return landingErr;
            }
            else
            {
                return -1.0;
            }
        }

        /// <summary>
        /// Returns the predicted latitude at landing.
        /// </summary>
        /// <returns>-1 if the prediction is unavailable for whatever reason</returns>
        public double GetLandingLatitude()
        {
            if (mjFound)
            {
                if (moduleInvalidated)
                {
                    InvalidateResults();
                    moduleInvalidated = false;
                }

                if (landingCurrent == false)
                {
                    UpdateLandingStats();
                }

                return landingLat;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the predicted longitude at landing.
        /// </summary>
        /// <returns>-1 if the prediction is unavailable for whatever reason</returns>
        public double GetLandingLongitude()
        {
            if (mjFound)
            {
                if (moduleInvalidated)
                {
                    InvalidateResults();
                    moduleInvalidated = false;
                }

                if (landingCurrent == false)
                {
                    UpdateLandingStats();
                }

                return landingLon;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the predicted altitude at landing.
        /// </summary>
        /// <returns>-1 if the prediction is unavailable for whatever reason</returns>
        public double GetLandingAltitude()
        {
            if (mjFound)
            {
                if (moduleInvalidated)
                {
                    InvalidateResults();
                    moduleInvalidated = false;
                }

                if (landingCurrent == false)
                {
                    UpdateLandingStats();
                }

                return landingAlt;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Returns net dV of the vessel.
        /// </summary>
        /// <returns>Returns NaN if MJ is unavailable.</returns>
        public double GetDeltaV()
        {
            if (mjFound)
            {
                if (moduleInvalidated)
                {
                    InvalidateResults();
                    moduleInvalidated = false;
                }

                if (deltaVCurrent == false)
                {
                    UpdateDeltaVStats();
                }

                return deltaV;
            }
            else
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Returns dV of the active stage
        /// </summary>
        /// <returns>Returns NaN if MJ is unavailable.</returns>
        public double GetStageDeltaV()
        {
            if (mjFound)
            {
                if (moduleInvalidated)
                {
                    InvalidateResults();
                    moduleInvalidated = false;
                }

                if(deltaVCurrent == false)
                {
                    UpdateDeltaVStats();
                }

                return deltaVStage;
            }
            else
            {
                return double.NaN;
            }
        }
        #endregion
    }
}
