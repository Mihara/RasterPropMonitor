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
    /// JSIMechJeb provides an interface with the MechJeb plugin using
    /// reflection, which allows us to avoid the need for hard dependencies.
    /// </summary>
    internal class JSIMechJeb : IJSIModule
    {
        #region Reflection Definitions
        // MechJebCore.GetComputerModule(string)
        private static readonly DynamicMethodDelegate getComputerModule;
        // MechJebCore.target
        private static readonly FieldInfo mjCoreTarget;
        // MechJebCore.node
        private static readonly FieldInfo mjCoreNode;
        // MechJebCore.attitude
        private static readonly FieldInfo mjCoreAttitude;
        // MechJebCore.vesselState
        private static readonly FieldInfo mjCoreVesselState;

        // AbsoluteVector
        // AbsoluteVector.latitude
        private static readonly FieldInfo mjAbsoluteVectorLat;
        // AbsoluteVector.longitude
        private static readonly FieldInfo mjAbsoluteVectorLon;
        // AbsoluteVector.(double)
        private static readonly DynamicMethodDelegate absoluteVectorToDouble;

        // MechJebModuleLandingPredictions
        // MechJebModuleLandingPredictions.GetResult()
        private static readonly DynamicMethodDelegate getPredictionsResult;

        // ReentrySimulation.Result
        // ReentrySimulation.Result.outcome
        private static readonly FieldInfo mjReentryOutcome;
        // ReentrySimulation.Result.endPosition
        private static readonly FieldInfo mjReentryEndPosition;
        // ReentrySimulation.Result.endUT
        private static readonly FieldInfo mjReentryTime;

        // ComputerModule
        // ComputerModule.enabled (get)
        private static readonly DynamicFuncBool moduleEnabled;
        private static readonly FieldInfo mjModuleUsers;

        // MechJebModuleStageStats
        // MechJebModuleStageStats.RequestUpdate()
        private static readonly DynamicMethodDelegate requestUpdate;
        // MechJebModuleStageStats.vacStats[]
        private static readonly FieldInfo mjVacStageStats;
        // MechJebModuleStageStats.atmoStats[]
        private static readonly FieldInfo mjAtmStageStats;

        // MechJebModuleTargetController
        // MechJebModuleTargetController.targetLatitude
        private static readonly FieldInfo mjTargetLongitude;
        // MechJebModuleTargetController.targetLatitude
        private static readonly FieldInfo mjTargetLatitude;
        // MechJebModuleTargetController.PositionTargetExists (get)
        private static readonly DynamicFuncBool getPositionTargetExists;
        // MechJebModuleTargetController.NormalTargetExists (get)
        private static readonly DynamicFuncBool getNormalTargetExists;
        // TargetOrbit (get)
        private static readonly DynamicFuncObject getTargetOrbit;

        // MechJebModuleSmartASS
        // MechJebModuleSmartASS.target
        private static readonly FieldInfo mjSmartassTarget;
        // MechJebModuleSmartASS.Engage
        private static readonly DynamicMethodDelegate engageSmartass;
        // MechJebModuleSmartASS.forceRol
        private static readonly FieldInfo mjSmartassForceRol;
        // MechJebModuleSmartASS.rol
        private static readonly FieldInfo mjSmartassRol;
        // MechJebModuleSmartASS.ModeTexts
        public static string[] ModeTexts;
        // MechJebModuleSmartASS.TargetTexts
        public static string[] TargetTexts;

        // MechJebModuleNodeExecutor
        // MechJebModuleNodeExecutor.ExecuteOneNode(obj controller)
        private static readonly DynamicMethodDelegate executeOneNode;
        // MechJebModuleNodeExecutor.Abort()
        private static readonly DynamicAction abortNode;

        // FuelFlowSimulation.StageStats
        // FuelFlowSimulation.StageStats.deltaV
        private static readonly FieldInfo mjStageDv;
        // FuelFlowSimulation.StageStats[].Length
        private static readonly DynamicFuncInt stageStatsGetLength;
        // FuelFlowSimulation.StageStats[].Get
        private static readonly DynamicMethodDelegate stageStatsGetIndex;

        // UserPool
        // UserPool.Add
        private static readonly DynamicMethodDelegate addUser;
        // UserPool.Remove
        private static readonly DynamicMethodDelegate removeUser;
        // UserPool.Contains
        private static readonly DynamicMethodDelegate containsUser;

        // VesselState
        // VesselState.TerminalVelocity
        private static readonly DynamicFuncDouble terminalVelocity;

        // MechJebModuleLandingAutopilot
        // MechJebModuleLandingAutopilot.LandAtPositionTarget
        private static readonly DynamicMethodDelegate landAtPositionTarget;
        // MechJebModuleLandingAutopilot.LandUntargeted
        private static readonly DynamicMethodDelegate landUntargeted;
        // MechJebModuleLandingAutopilot.StopLanding
        private static readonly DynamicAction stopLanding;

        // Spaceplane autopilot
        private static readonly DynamicMethodDelegate spaceplaneAutoland;
        private static readonly DynamicMethodDelegate spaceplaneHoldHeading;
        private static readonly DynamicAction spaceplaneAPOff;
        private static readonly FieldInfo spaceplaneAPMode;
        private static readonly FieldInfo spaceplaneAltitude;
        private static readonly FieldInfo spaceplaneHeading;
        private static readonly FieldInfo spaceplaneGlideslope;

        // Ascent Autopilot
        private static readonly FieldInfo launchOrbitAltitude;
        // Ascent Guidance
        private static readonly FieldInfo launchOrbitInclination;

        // EditableDoubleMult
        private static readonly DynamicMethodDelegate setEditableDoubleMult;
        private static readonly DynamicFuncDouble getEditableDoubleMult;
        private static readonly FieldInfo getEditableDoubleMultMultiplier;

        // EditableDouble
        // EditableDouble.val (set)
        private static readonly DynamicMethodDelegate setEditableDouble;
        // EditableDouble.val (get)
        private static readonly DynamicFuncDouble getEditableDouble;

        // VesselExtensions.GetMasterMechJeb()
        private static readonly DynamicMethodDelegate getMasterMechJeb;
        // VesselExtensions.PlaceManeuverNode()
        private static readonly DynamicMethodDelegate placeManeuverNode;

        // OrbitalManeuverCalculator
        // OrbitalManeuverCalculator.mjDeltaVAndTimeForHohmannTransfer
        private static readonly MethodInfo mjDeltaVAndTimeForHohmannTransfer;
        // MOARdV TODO: There appears to be extra instructions needed to handle out parameters
        //private static readonly DynamicMethodDelegate deltaVAndTimeForHohmannTransfer;
        // OrbitalManeuverCalculator.DeltaVAndTimeForInterplanetaryTransferEjection
        private static readonly MethodInfo mjDeltaVAndTimeForInterplanetaryTransferEjection;
        //private static readonly DynamicMethodDelegate deltaVAndTimeForInterplanetaryTransferEjection;
        private static readonly MethodInfo mjDeltaVToMatchVelocities;
        // OrbitalManeuverCalculator.DeltaVToCircularize
        private static readonly DynamicMethodDelegate deltaVToCircularize;
        // OrbitalManeuverCalculator.DeltaVToChangeApoapsis
        private static readonly DynamicMethodDelegate deltaVToChangeApoapsis;
        // OrbitalManeuverCalculator.DeltaVToChangePeriapsis
        private static readonly DynamicMethodDelegate deltaVToChangePeriapsis;

        // Ascent Autopilot Engaged (starting MJ dev 514)
        private static readonly DynamicFuncBool getAscentAutopilotEngaged;
        private static readonly DynamicMethodDelegate setAscentAutopilotEngaged;
        #endregion

        #region MechJeb enum imports
        public enum Mode
        {
            ORBITAL = 0,
            SURFACE = 1,
            TARGET = 2,
            ADVANCED = 3,
            AUTO = 4,
        }

        public enum Target
        {
            OFF = 0,
            KILLROT = 1,
            NODE = 2,
            SURFACE = 3,
            PROGRADE = 4,
            RETROGRADE = 5,
            NORMAL_PLUS = 6,
            NORMAL_MINUS = 7,
            RADIAL_PLUS = 8,
            RADIAL_MINUS = 9,
            RELATIVE_PLUS = 10,
            RELATIVE_MINUS = 11,
            TARGET_PLUS = 12,
            TARGET_MINUS = 13,
            PARALLEL_PLUS = 14,
            PARALLEL_MINUS = 15,
            ADVANCED = 16,
            AUTO = 17,
            SURFACE_PROGRADE = 18,
            SURFACE_RETROGRADE = 19,
            HORIZONTAL_PLUS = 20,
            HORIZONTAL_MINUS = 21,
            VERTICAL_PLUS = 22,
        }

        public enum SpaceplaneMode
        {
            AUTOLAND = 0,
            HOLD = 1,
            OFF = 2,
        }

        // Imported directly from MJ
        public static readonly Mode[] Target2Mode = new Mode[] { Mode.ORBITAL, Mode.ORBITAL, Mode.ORBITAL, Mode.SURFACE, Mode.ORBITAL, Mode.ORBITAL, Mode.ORBITAL, Mode.ORBITAL, Mode.ORBITAL, Mode.ORBITAL, Mode.TARGET, Mode.TARGET, Mode.TARGET, Mode.TARGET, Mode.TARGET, Mode.TARGET, Mode.ADVANCED, Mode.AUTO, Mode.SURFACE, Mode.SURFACE, Mode.SURFACE, Mode.SURFACE, Mode.SURFACE };

        public enum TimeReference
        {
            COMPUTED = 0,
            X_FROM_NOW = 1,
            APOAPSIS = 2,
            PERIAPSIS = 3,
            ALTITUDE = 4,
            EQ_ASCENDING = 5,
            EQ_DESCENDING = 6,
            REL_ASCENDING = 7,
            REL_DESCENDING = 8,
            CLOSEST_APPROACH = 9,
        }
        #endregion

        static private readonly bool mjFound;

        private double deltaV, deltaVStage;

        private double lastUpdate = 0.0;
        private double landingLat, landingLon, landingAlt, landingErr = -1.0, landingTime = -1.0;

        static JSIMechJeb()
        {
            Type mjMechJebCore_t = null;
            try
            {
                var loadedMechJebAssy = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name == "MechJeb2");

                if (loadedMechJebAssy == null)
                {
                    mjFound = false;
                    //JUtil.LogMessage(this, "A supported version of MechJeb is {0}", (mjFound) ? "present" : "not available");

                    return;
                }

                //--- Process all the reflection info
                // MechJebCore
                mjMechJebCore_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebCore");
                if (mjMechJebCore_t == null)
                {
                    return;
                }
                MethodInfo mjGetComputerModule = mjMechJebCore_t.GetMethod("GetComputerModule", new Type[] { typeof(string) });
                if (mjGetComputerModule == null)
                {
                    throw new NotImplementedException("mjGetComputerModule");
                }
                getComputerModule = DynamicMethodDelegateFactory.Create(mjGetComputerModule);
                mjCoreTarget = mjMechJebCore_t.GetField("target", BindingFlags.Instance | BindingFlags.Public);
                if (mjCoreTarget == null)
                {
                    throw new NotImplementedException("mjCoreTarget");
                }
                mjCoreNode = mjMechJebCore_t.GetField("node", BindingFlags.Instance | BindingFlags.Public);
                if (mjCoreNode == null)
                {
                    throw new NotImplementedException("mjCoreNode");
                }
                mjCoreAttitude = mjMechJebCore_t.GetField("attitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjCoreAttitude == null)
                {
                    throw new NotImplementedException("mjCoreAttitude");
                }
                mjCoreVesselState = mjMechJebCore_t.GetField("vesselState", BindingFlags.Instance | BindingFlags.Public);
                if (mjCoreVesselState == null)
                {
                    throw new NotImplementedException("mjCoreVesselState");
                }

                // VesselExtensions 
                Type mjVesselExtensions_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.VesselExtensions");
                if (mjVesselExtensions_t == null)
                {
                    throw new NotImplementedException("mjVesselExtensions_t");
                }
                MethodInfo mjGetMasterMechJeb = mjVesselExtensions_t.GetMethod("GetMasterMechJeb", BindingFlags.Static | BindingFlags.Public);
                if (mjGetMasterMechJeb == null)
                {
                    throw new NotImplementedException("mjGetMasterMechJeb");
                }
                getMasterMechJeb = DynamicMethodDelegateFactory.Create(mjGetMasterMechJeb);
                MethodInfo mjPlaceManeuverNode = mjVesselExtensions_t.GetMethod("PlaceManeuverNode", BindingFlags.Static | BindingFlags.Public);
                if (mjPlaceManeuverNode == null)
                {
                    throw new NotImplementedException("mjPlaceManeuverNode");
                }
                placeManeuverNode = DynamicMethodDelegateFactory.Create(mjPlaceManeuverNode);

                // VesselState
                Type mjVesselState_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.VesselState");
                if (mjVesselState_t == null)
                {
                    throw new NotImplementedException("mjVesselState_t");
                }
                MethodInfo mjTerminalVelocity = mjVesselState_t.GetMethod("TerminalVelocity", BindingFlags.Instance | BindingFlags.Public);
                if (mjTerminalVelocity == null)
                {
                    throw new NotImplementedException("mjTerminalVelocity");
                }
                terminalVelocity = DynamicMethodDelegateFactory.CreateFuncDouble(mjTerminalVelocity);

                // SmartASS
                Type mjSmartass_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleSmartASS");
                mjSmartassTarget = mjSmartass_t.GetField("target", BindingFlags.Instance | BindingFlags.Public);
                if (mjSmartassTarget == null)
                {
                    throw new NotImplementedException("mjSmartassTarget");
                }
                MethodInfo mjSmartassEngage = mjSmartass_t.GetMethod("Engage", BindingFlags.Instance | BindingFlags.Public);
                if (mjSmartassEngage == null)
                {
                    throw new NotImplementedException("mjSmartassEngage");
                }
                engageSmartass = DynamicMethodDelegateFactory.Create(mjSmartassEngage);
                mjSmartassForceRol = mjSmartass_t.GetField("forceRol", BindingFlags.Instance | BindingFlags.Public);
                if (mjSmartassForceRol == null)
                {
                    throw new NotImplementedException("mjSmartassForceRol");
                }
                mjSmartassRol = mjSmartass_t.GetField("rol", BindingFlags.Instance | BindingFlags.Public);
                if (mjSmartassRol == null)
                {
                    throw new NotImplementedException("mjSmartassRol");
                }
                FieldInfo TargetTextsInfo = mjSmartass_t.GetField("TargetTexts", BindingFlags.Static | BindingFlags.Public);
                TargetTexts = (string[])TargetTextsInfo.GetValue(null);
                if (TargetTexts == null)
                {
                    throw new NotImplementedException("TargetTexts");
                }
                FieldInfo ModeTextsInfo = mjSmartass_t.GetField("ModeTexts", BindingFlags.Static | BindingFlags.Public);
                ModeTexts = (string[])ModeTextsInfo.GetValue(null);
                if (ModeTexts == null)
                {
                    throw new NotImplementedException("ModeTexts");
                }

                // Landing Predictions
                Type mjModuleLandingPredictions_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleLandingPredictions");
                if (mjModuleLandingPredictions_t == null)
                {
                    throw new NotImplementedException("mjModuleLandingPredictions_t");
                }
                MethodInfo mjPredictionsGetResult = mjModuleLandingPredictions_t.GetMethod("GetResult", BindingFlags.Instance | BindingFlags.Public);
                if (mjPredictionsGetResult == null)
                {
                    throw new NotImplementedException("mjPredictionsGetResult");
                }
                getPredictionsResult = DynamicMethodDelegateFactory.Create(mjPredictionsGetResult);

                // AbsoluteVector
                Type mjAbsoluteVector_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.AbsoluteVector");
                if (mjAbsoluteVector_t == null)
                {
                    throw new NotImplementedException("mjAbsoluteVector_t");
                }
                mjAbsoluteVectorLat = mjAbsoluteVector_t.GetField("latitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjAbsoluteVectorLat == null)
                {
                    throw new NotImplementedException("mjAbsoluteVectorLat");
                }
                mjAbsoluteVectorLon = mjAbsoluteVector_t.GetField("longitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjAbsoluteVectorLon == null)
                {
                    throw new NotImplementedException("mjAbsoluteVectorLon");
                }

                // MechJebModuleAscentAutopilot
                Type mjMechJebModuleAscentAutopilot_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleAscentAutopilot");
                if (mjMechJebModuleAscentAutopilot_t == null)
                {
                    throw new NotImplementedException("mjMechJebModuleAscentAutopilot_t");
                }
                launchOrbitAltitude = mjMechJebModuleAscentAutopilot_t.GetField("desiredOrbitAltitude");
                if (launchOrbitAltitude == null)
                {
                    throw new NotImplementedException("launchOrbitAltitude");
                }
                // MOARdV TODO: when the next version of MJ is out, this will be the only way to engage
                // the AP, so we will want to throw an exception if aapEngaged is null.
                PropertyInfo aapEngaged = mjMechJebModuleAscentAutopilot_t.GetProperty("Engaged");
                if (aapEngaged != null)
                {
                    MethodInfo getter = aapEngaged.GetGetMethod();
                    getAscentAutopilotEngaged = DynamicMethodDelegateFactory.CreateFuncBool(getter);
                    if (getAscentAutopilotEngaged == null)
                    {
                        throw new NotImplementedException("getAscentAutopilotEngaged");
                    }

                    MethodInfo setter = aapEngaged.GetSetMethod();
                    setAscentAutopilotEngaged = DynamicMethodDelegateFactory.Create(setter);
                    if (setAscentAutopilotEngaged == null)
                    {
                        throw new NotImplementedException("setAscentAutopilotEngaged");
                    }
                }
                // MechJebModuleAscentAutopilot
                Type mjMechJebModuleAscentGuidance_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleAscentGuidance");
                if (mjMechJebModuleAscentGuidance_t == null)
                {
                    throw new NotImplementedException("mjMechJebModuleAscentGuidance_t");
                }
                launchOrbitInclination = mjMechJebModuleAscentGuidance_t.GetField("desiredInclination");
                if (launchOrbitInclination == null)
                {
                    throw new NotImplementedException("launchOrbitInclination");
                }

                Type mjEditableDoubleMult_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.EditableDoubleMult");
                if (mjEditableDoubleMult_t == null)
                {
                    throw new NotImplementedException("mjEditableDoubleMult_t");
                }
                getEditableDoubleMultMultiplier = mjEditableDoubleMult_t.GetField("multiplier");
                if (getEditableDoubleMultMultiplier == null)
                {
                    throw new NotImplementedException("getEditableDoubleMultMultiplier");
                }
                PropertyInfo edmVal = mjEditableDoubleMult_t.GetProperty("val");
                if (edmVal == null)
                {
                    throw new NotImplementedException("edmVal");
                }
                // getEditableDoubleMult
                MethodInfo mjGetEDM = edmVal.GetGetMethod();
                if (mjGetEDM != null)
                {
                    getEditableDoubleMult = DynamicMethodDelegateFactory.CreateFuncDouble(mjGetEDM);
                }
                // setEditableDoubleMult
                MethodInfo mjSetEDM = edmVal.GetSetMethod();
                if (mjSetEDM != null)
                {
                    setEditableDoubleMult = DynamicMethodDelegateFactory.Create(mjSetEDM);
                }

                // EditableAngle
                Type mjEditableAngle_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.EditableAngle");
                if (mjEditableAngle_t == null)
                {
                    throw new NotImplementedException("mjEditableAngle_t");
                }
                MethodInfo mjAbsoluteVectorToDouble = null;
                foreach (MethodInfo method in mjEditableAngle_t.GetMethods(BindingFlags.Static | BindingFlags.Public))
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
                    throw new NotImplementedException("mjAbsoluteVectorToDouble");
                }
                absoluteVectorToDouble = DynamicMethodDelegateFactory.Create(mjAbsoluteVectorToDouble);

                // MechJebModuleTargetController
                Type mjModuleTargetController_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleTargetController");
                if (mjModuleTargetController_t == null)
                {
                    throw new NotImplementedException("mjModuleTargetController_t");
                }
                mjTargetLongitude = mjModuleTargetController_t.GetField("targetLongitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjTargetLongitude == null)
                {
                    throw new NotImplementedException("mjTargetLongitude");
                }
                mjTargetLatitude = mjModuleTargetController_t.GetField("targetLatitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjTargetLatitude == null)
                {
                    throw new NotImplementedException("mjTargetLatitude");
                }
                PropertyInfo mjPositionTargetExists = mjModuleTargetController_t.GetProperty("PositionTargetExists", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo mjGetPositionTargetExists = null;
                if (mjPositionTargetExists != null)
                {
                    mjGetPositionTargetExists = mjPositionTargetExists.GetGetMethod();
                }
                if (mjGetPositionTargetExists == null)
                {
                    throw new NotImplementedException("mjGetPositionTargetExists");
                }
                getPositionTargetExists = DynamicMethodDelegateFactory.CreateFuncBool(mjGetPositionTargetExists);
                PropertyInfo mjNormalTargetExists = mjModuleTargetController_t.GetProperty("NormalTargetExists", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo mjGetNormalTargetExists = null;
                if (mjNormalTargetExists != null)
                {
                    mjGetNormalTargetExists = mjNormalTargetExists.GetGetMethod();
                }
                if (mjGetNormalTargetExists == null)
                {
                    throw new NotImplementedException("mjGetNormalTargetExists");
                }
                getNormalTargetExists = DynamicMethodDelegateFactory.CreateFuncBool(mjGetNormalTargetExists);
                PropertyInfo mjTargetOrbit = mjModuleTargetController_t.GetProperty("TargetOrbit", BindingFlags.Instance | BindingFlags.Public); ;
                MethodInfo mjGetTargetOrbit = null;
                if (mjTargetOrbit != null)
                {
                    mjGetTargetOrbit = mjTargetOrbit.GetGetMethod();
                }
                if (mjGetTargetOrbit == null)
                {
                    throw new NotImplementedException("mjGetTargetOrbit");
                }
                getTargetOrbit = DynamicMethodDelegateFactory.CreateFuncObject(mjGetTargetOrbit);

                // MuMech.FuelFlowSimulation
                Type mjFuelFlowSimulation_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.FuelFlowSimulation");
                if (mjFuelFlowSimulation_t == null)
                {
                    throw new NotImplementedException("mjFuelFlowSimulation_t");
                }
                // MuMech.FuelFlowSimulation.Stats
                Type mjFuelFlowSimulationStats_t = mjFuelFlowSimulation_t.GetNestedType("Stats");
                if (mjFuelFlowSimulationStats_t == null)
                {
                    throw new NotImplementedException("mjFuelFlowSimulationStats_t");
                }
                mjStageDv = mjFuelFlowSimulationStats_t.GetField("deltaV", BindingFlags.Instance | BindingFlags.Public);
                if (mjStageDv == null)
                {
                    throw new NotImplementedException("mjStageDv");
                }

                // MuMech.ReentrySimulation.Result
                Type mjReentrySim_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.ReentrySimulation");
                if (mjReentrySim_t == null)
                {
                    throw new NotImplementedException("mjReentrySim_t");
                }
                Type mjReentryResult_t = mjReentrySim_t.GetNestedType("Result");
                if (mjReentryResult_t == null)
                {
                    throw new NotImplementedException("mjReentryResult_t");
                }
                mjReentryOutcome = mjReentryResult_t.GetField("outcome", BindingFlags.Instance | BindingFlags.Public);
                if (mjReentryOutcome == null)
                {
                    throw new NotImplementedException("mjReentryOutcome");
                }
                mjReentryEndPosition = mjReentryResult_t.GetField("endPosition", BindingFlags.Instance | BindingFlags.Public);
                if (mjReentryEndPosition == null)
                {
                    throw new NotImplementedException("mjReentryEndPosition");
                }
                mjReentryTime = mjReentryResult_t.GetField("endUT", BindingFlags.Instance | BindingFlags.Public);
                if (mjReentryTime == null)
                {
                    throw new NotImplementedException("mjReentryTime");
                }

                // UserPool
                Type mjUserPool_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.UserPool");
                MethodInfo mjAddUser = mjUserPool_t.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
                if (mjAddUser == null)
                {
                    throw new NotImplementedException("mjAddUser");
                }
                addUser = DynamicMethodDelegateFactory.Create(mjAddUser);
                MethodInfo mjRemoveUser = mjUserPool_t.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public);
                if (mjRemoveUser == null)
                {
                    throw new NotImplementedException("mjRemoveUser");
                }
                removeUser = DynamicMethodDelegateFactory.Create(mjRemoveUser);
                MethodInfo mjContainsUser = mjUserPool_t.GetMethod("Contains", BindingFlags.Instance | BindingFlags.Public);
                if (mjContainsUser == null)
                {
                    throw new NotImplementedException("mjContainsUser");
                }
                containsUser = DynamicMethodDelegateFactory.Create(mjContainsUser);

                // MechJebModuleLandingAutopilot
                Type mjLandingAutopilot_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleLandingAutopilot");
                MethodInfo mjLandAtPositionTarget = mjLandingAutopilot_t.GetMethod("LandAtPositionTarget", BindingFlags.Instance | BindingFlags.Public);
                if (mjLandAtPositionTarget == null)
                {
                    throw new NotImplementedException("mjLandAtPositionTarget");
                }
                landAtPositionTarget = DynamicMethodDelegateFactory.Create(mjLandAtPositionTarget);
                MethodInfo mjLandUntargeted = mjLandingAutopilot_t.GetMethod("LandUntargeted", BindingFlags.Instance | BindingFlags.Public);
                if (mjLandUntargeted == null)
                {
                    throw new NotImplementedException("mjLandUntargeted");
                }
                landUntargeted = DynamicMethodDelegateFactory.Create(mjLandUntargeted);
                MethodInfo mjStopLanding = mjLandingAutopilot_t.GetMethod("StopLanding", BindingFlags.Instance | BindingFlags.Public);
                if (mjStopLanding == null)
                {
                    throw new NotImplementedException("mjStopLanding");
                }
                stopLanding = DynamicMethodDelegateFactory.CreateAction(mjStopLanding);

                Type mjSpaceplaneAutopilot_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleSpaceplaneAutopilot");
                MethodInfo mjSPAutoland = mjSpaceplaneAutopilot_t.GetMethod("Autoland", BindingFlags.Instance | BindingFlags.Public);
                if (mjSPAutoland == null)
                {
                    throw new NotImplementedException("mjSPAutoland");
                }
                spaceplaneAutoland = DynamicMethodDelegateFactory.Create(mjSPAutoland);
                MethodInfo mjSPHoldHeadingAndAltitude = mjSpaceplaneAutopilot_t.GetMethod("HoldHeadingAndAltitude", BindingFlags.Instance | BindingFlags.Public);
                if (mjSPHoldHeadingAndAltitude == null)
                {
                    throw new NotImplementedException("mjSPHoldHeadingAndAltitude");
                }
                spaceplaneHoldHeading = DynamicMethodDelegateFactory.Create(mjSPHoldHeadingAndAltitude);
                MethodInfo mjSPAutopilotOff = mjSpaceplaneAutopilot_t.GetMethod("AutopilotOff", BindingFlags.Instance | BindingFlags.Public);
                if (mjSPAutopilotOff == null)
                {
                    throw new NotImplementedException("mjSPAutopilotOff");
                }
                spaceplaneAPOff = DynamicMethodDelegateFactory.CreateAction(mjSPAutopilotOff);
                spaceplaneAPMode = mjSpaceplaneAutopilot_t.GetField("mode", BindingFlags.Instance | BindingFlags.Public);
                if (spaceplaneAPMode == null)
                {
                    throw new NotImplementedException("spaceplaneAPMode");
                }
                spaceplaneAltitude = mjSpaceplaneAutopilot_t.GetField("targetAltitude", BindingFlags.Instance | BindingFlags.Public);
                if (spaceplaneAltitude == null)
                {
                    throw new NotImplementedException("spaceplaneAltitude");
                }
                spaceplaneHeading = mjSpaceplaneAutopilot_t.GetField("targetHeading", BindingFlags.Instance | BindingFlags.Public);
                if (spaceplaneHeading == null)
                {
                    throw new NotImplementedException("spaceplaneHeading");
                }
                spaceplaneGlideslope = mjSpaceplaneAutopilot_t.GetField("glideslope", BindingFlags.Instance | BindingFlags.Public);
                if (spaceplaneGlideslope == null)
                {
                    throw new NotImplementedException("spaceplaneGlideslope");
                }

                // EditableDouble
                Type mjEditableDouble_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.EditableDouble");
                PropertyInfo mjEditableDoubleVal = mjEditableDouble_t.GetProperty("val", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo mjGetEditableDouble = null, mjSetEditableDouble = null;
                if (mjEditableDoubleVal != null)
                {
                    mjGetEditableDouble = mjEditableDoubleVal.GetGetMethod();
                    mjSetEditableDouble = mjEditableDoubleVal.GetSetMethod();
                }
                if (mjGetEditableDouble == null)
                {
                    throw new NotImplementedException("mjGetEditableDouble");
                }
                getEditableDouble = DynamicMethodDelegateFactory.CreateFuncDouble(mjGetEditableDouble);
                if (mjSetEditableDouble == null)
                {
                    throw new NotImplementedException("mjSetEditableDouble");
                }
                setEditableDouble = DynamicMethodDelegateFactory.Create(mjSetEditableDouble);

                // OrbitalManeuverCalculator
                Type mjOrbitalManeuverCalculator_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.OrbitalManeuverCalculator");
                mjDeltaVAndTimeForHohmannTransfer = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVAndTimeForHohmannTransfer", BindingFlags.Static | BindingFlags.Public);
                if (mjDeltaVAndTimeForHohmannTransfer == null)
                {
                    throw new NotImplementedException("mjDeltaVAndTimeForHohmannTransfer");
                }
                //deltaVAndTimeForHohmannTransfer = DynamicMethodDelegateFactory.Create(mjDeltaVAndTimeForHohmannTransfer);
                mjDeltaVToMatchVelocities = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVToMatchVelocities", BindingFlags.Static | BindingFlags.Public);
                if (mjDeltaVToMatchVelocities == null)
                {
                    throw new NotImplementedException("mjDeltaVToMatchVelocities");
                }
                mjDeltaVAndTimeForInterplanetaryTransferEjection = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVAndTimeForInterplanetaryTransferEjection", BindingFlags.Static | BindingFlags.Public);
                if (mjDeltaVAndTimeForInterplanetaryTransferEjection == null)
                {
                    throw new NotImplementedException("mjDeltaVAndTimeForInterplanetaryTransferEjection");
                }
                //deltaVAndTimeForInterplanetaryTransferEjection = DynamicMethodDelegateFactory.Create(mjDeltaVAndTimeForInterplanetaryTransferEjection);
                MethodInfo mjDeltaVToCircularize = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVToCircularize", BindingFlags.Static | BindingFlags.Public);
                if (mjDeltaVToCircularize == null)
                {
                    throw new NotImplementedException("mjDeltaVToCircularize");
                }
                deltaVToCircularize = DynamicMethodDelegateFactory.Create(mjDeltaVToCircularize);
                MethodInfo mjDeltaVToChangeApoapsis = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVToChangeApoapsis", BindingFlags.Static | BindingFlags.Public);
                if (mjDeltaVToChangeApoapsis == null)
                {
                    throw new NotImplementedException("mjDeltaVToChangeApoapsis");
                }
                deltaVToChangeApoapsis = DynamicMethodDelegateFactory.Create(mjDeltaVToChangeApoapsis);
                MethodInfo mjDeltaVToChangePeriapsis = mjOrbitalManeuverCalculator_t.GetMethod("DeltaVToChangePeriapsis", BindingFlags.Static | BindingFlags.Public);
                if (mjDeltaVToChangePeriapsis == null)
                {
                    throw new NotImplementedException("mjDeltaVToChangePeriapsis");
                }
                deltaVToChangePeriapsis = DynamicMethodDelegateFactory.Create(mjDeltaVToChangePeriapsis);

                // MechJebModuleStageStats
                Type mjModuleStageStats_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleStageStats");
                if (mjModuleStageStats_t == null)
                {
                    throw new NotImplementedException("mjModuleStageStats_t");
                }
                MethodInfo mjRequestUpdate = mjModuleStageStats_t.GetMethod("RequestUpdate", BindingFlags.Instance | BindingFlags.Public);
                if (mjRequestUpdate == null)
                {
                    throw new NotImplementedException("mjRequestUpdate");
                }
                requestUpdate = DynamicMethodDelegateFactory.Create(mjRequestUpdate);
                mjVacStageStats = mjModuleStageStats_t.GetField("vacStats", BindingFlags.Instance | BindingFlags.Public);
                if (mjVacStageStats == null)
                {
                    throw new NotImplementedException("mjVacStageStats");
                }

                // Updated MechJeb (post 2.5.1) switched from using KER back to
                // its internal FuelFlowSimulation.  This sim uses an array of
                // structs, which entails a couple of extra hoops to jump through
                // when reading via reflection.
                mjAtmStageStats = mjModuleStageStats_t.GetField("atmoStats", BindingFlags.Instance | BindingFlags.Public);
                if (mjAtmStageStats == null)
                {
                    throw new NotImplementedException("mjAtmStageStats");
                }

                PropertyInfo mjStageStatsLength = mjVacStageStats.FieldType.GetProperty("Length");
                if (mjStageStatsLength == null)
                {
                    throw new NotImplementedException("mjStageStatsLength");
                }
                MethodInfo mjStageStatsGetLength = mjStageStatsLength.GetGetMethod();
                if (mjStageStatsGetLength == null)
                {
                    throw new NotImplementedException("mjStageStatsGetLength");
                }
                stageStatsGetLength = DynamicMethodDelegateFactory.CreateFuncInt(mjStageStatsGetLength);
                MethodInfo mjStageStatsGetIndex = mjVacStageStats.FieldType.GetMethod("Get");
                if (mjStageStatsGetIndex == null)
                {
                    throw new NotImplementedException("mjStageStatsGetIndex");
                }
                stageStatsGetIndex = DynamicMethodDelegateFactory.Create(mjStageStatsGetIndex);

                // MechJebModuleNodeExecutor
                Type mjNodeExecutor_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.MechJebModuleNodeExecutor");
                MethodInfo mjExecuteOneNode = mjNodeExecutor_t.GetMethod("ExecuteOneNode", BindingFlags.Instance | BindingFlags.Public);
                if (mjExecuteOneNode == null)
                {
                    throw new NotImplementedException("mjExecuteOneNode");
                }
                executeOneNode = DynamicMethodDelegateFactory.Create(mjExecuteOneNode);
                MethodInfo mjAbortNode = mjNodeExecutor_t.GetMethod("Abort", BindingFlags.Instance | BindingFlags.Public);
                if (mjAbortNode == null)
                {
                    throw new NotImplementedException("mjAbortNode");
                }
                abortNode = DynamicMethodDelegateFactory.CreateAction(mjAbortNode);

                // Computer Module
                Type mjComputerModule_t = loadedMechJebAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "MuMech.ComputerModule");
                PropertyInfo mjModuleEnabledProperty = mjComputerModule_t.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo mjModuleEnabled = null;
                if (mjModuleEnabledProperty != null)
                {
                    mjModuleEnabled = mjModuleEnabledProperty.GetGetMethod();
                }
                if (mjModuleEnabled == null)
                {
                    throw new NotImplementedException("mjModuleEnabled");
                }
                moduleEnabled = DynamicMethodDelegateFactory.CreateFuncBool(mjModuleEnabled);
                mjModuleUsers = mjComputerModule_t.GetField("users", BindingFlags.Instance | BindingFlags.Public);
                if (mjModuleUsers == null)
                {
                    throw new NotImplementedException("mjModuleUsers");
                }
            }
            catch (Exception e)
            {
                mjMechJebCore_t = null;
                JUtil.LogMessage(null, "Exception initializing JSIMechJeb: {0}", e);
            }

            if (mjMechJebCore_t != null && getMasterMechJeb != null)
            {
                mjFound = true;
            }
            else
            {
                mjFound = false;
            }
        }

        public JSIMechJeb()
        {
            JUtil.LogMessage(this, "A supported version of MechJeb is {0}", (mjFound) ? "present" : "not available");
        }

        #region Internal Methods
        /// <summary>
        /// Invokes VesselExtensions.GetMasterMechJeb()
        /// </summary>
        /// <returns>The master MechJeb on the vessel</returns>
        static private object GetMasterMechJeb(Vessel vessel)
        {
            object activeJeb = null;
            try
            {
                // Is MechJeb installed?
                if (mjFound && vessel != null)
                {
                    activeJeb = getMasterMechJeb(null, new object[] { vessel });
                }
            }
            catch { }

            return activeJeb;
        }

        /// <summary>
        /// Fetch a computer module from the MJ core
        /// </summary>
        /// <param name="masterMechJeb"></param>
        /// <param name="computerModule"></param>
        /// <returns></returns>
        static private object GetComputerModule(object masterMechJeb, string computerModule)
        {
            if (masterMechJeb != null)
            {
                return getComputerModule(masterMechJeb, new object[] { computerModule });
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns whether the supplied MechJeb ComputerModule is enabled
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        static private bool ModuleEnabled(object module)
        {
            if (module != null)
            {
                return moduleEnabled(module);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Return the latest landing simulation results, or null if there aren't any.
        /// </summary>
        /// <returns></returns>
        static private object GetLandingResults(object masterMechJeb)
        {
            object predictor = GetComputerModule(masterMechJeb, "MechJebModuleLandingPredictions");
            if (predictor != null && ModuleEnabled(predictor) == true)
            {
                return getPredictionsResult(predictor, new object[] { });
            }

            return null;
        }

        static private void EnactTargetAction(Vessel vessel, Target action)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");

            if (activeSmartass != null)
            {
                mjSmartassTarget.SetValue(activeSmartass, (int)action);

                engageSmartass(activeSmartass, new object[] { true });
            }
        }

        private bool ReturnTargetState(Target action)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");

            if (activeSmartass != null)
            {
                object target = mjSmartassTarget.GetValue(activeSmartass);
                return (int)action == (int)target;
            }
            else
            {
                return false;
            }
        }
        #endregion

        #region Updater methods
        /// <summary>
        /// Update the landing prediction stats
        /// </summary>
        private void UpdateLandingStats(object activeJeb)
        {
            if (Planetarium.GetUniversalTime() - lastUpdate < 0.5)
            {
                // Don't update more than twice a second.
                return;
            }
            try
            {
                object result = GetLandingResults(activeJeb);
                if (result != null)
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

                            object target = mjCoreTarget.GetValue(activeJeb);
                            object targetLatField = mjTargetLatitude.GetValue(target);
                            object targetLonField = mjTargetLongitude.GetValue(target);
                            double targetLat = (double)absoluteVectorToDouble(null, new object[] { targetLatField });
                            double targetLon = (double)absoluteVectorToDouble(null, new object[] { targetLonField });
                            double targetAlt = FinePrint.Utilities.CelestialUtilities.TerrainAltitude(vessel.mainBody, targetLat, targetLon);

                            landingErr = Vector3d.Distance(vessel.mainBody.GetRelSurfacePosition(landingLat, landingLon, landingAlt),
                                                vessel.mainBody.GetRelSurfacePosition(targetLat, targetLon, targetAlt));
                        }
                        object endTime = mjReentryTime.GetValue(result);
                        landingTime = (double)endTime;

                        lastUpdate = Planetarium.GetUniversalTime();
                    }
                }
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Exception trap in GetLandingError(): {0}", e);
            }
        }

        /// <summary>
        /// Updates dV stats (dV and dVStage)
        /// </summary>
        private void UpdateDeltaVStats(object activeJeb)
        {
            try
            {
                object stagestats = GetComputerModule(activeJeb, "MechJebModuleStageStats");

                requestUpdate(stagestats, new object[] { this });

                int atmStatsLength = 0, vacStatsLength = 0;

                object atmStatsO = mjAtmStageStats.GetValue(stagestats);
                object vacStatsO = mjVacStageStats.GetValue(stagestats);
                if (atmStatsO != null)
                {
                    atmStatsLength = stageStatsGetLength(atmStatsO);
                }
                if (vacStatsO != null)
                {
                    vacStatsLength = stageStatsGetLength(vacStatsO);
                }

                deltaV = deltaVStage = 0.0;

                if (atmStatsLength > 0 && atmStatsLength == vacStatsLength)
                {
                    double atmospheresLocal = vessel.staticPressurekPa * PhysicsGlobals.KpaToAtmospheres;

                    for (int i = 0; i < atmStatsLength; ++i)
                    {
                        object atmStat = stageStatsGetIndex(atmStatsO, new object[] { i });
                        object vacStat = stageStatsGetIndex(vacStatsO, new object[] { i });
                        if (atmStat == null || vacStat == null)
                        {
                            throw new NotImplementedException("atmStat or vacState did not evaluate");
                        }

                        float atm = (float)mjStageDv.GetValue(atmStat);
                        float vac = (float)mjStageDv.GetValue(vacStat);
                        double stagedV = UtilMath.LerpUnclamped(vac, atm, atmospheresLocal);

                        deltaV += stagedV;

                        if (i == (atmStatsLength - 1))
                        {
                            deltaVStage = stagedV;
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
            object activeJeb = null;
            try
            {
                activeJeb = GetMasterMechJeb(vessel);
            }
            catch { }

            return (activeJeb != null);
        }

        public void SetSmartassMode(Target t)
        {
            EnactTargetAction(vessel, t);
        }

        /// <summary>
        /// Return the current Smartass mode
        /// </summary>
        /// <returns></returns>
        public int GetSmartassMode()
        {
            object activeJeb = GetMasterMechJeb(vessel);

            if (activeJeb != null)
            {
                object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");

                if (activeSmartass != null)
                {
                    object target = mjSmartassTarget.GetValue(activeSmartass);
                    return (int)target;
                }
            }

            return (int)Target.OFF;
        }

        /// <summary>
        /// Returns the predicted landing error to a target.
        /// </summary>
        /// <returns>-1 if the prediction is unavailable for whatever reason</returns>
        public double GetLandingError()
        {
            object activeJeb = GetMasterMechJeb(vessel);

            if (activeJeb != null)
            {
                UpdateLandingStats(activeJeb);

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
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                UpdateLandingStats(activeJeb);

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
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                UpdateLandingStats(activeJeb);

                return landingLon;
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Provide the MechJeb estimate for landing time.
        /// </summary>
        /// <returns></returns>
        public double GetLandingTime()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                UpdateLandingStats(activeJeb);

                return Math.Max(0.0, landingTime - Planetarium.GetUniversalTime());
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
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                UpdateLandingStats(activeJeb);

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
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                UpdateDeltaVStats(activeJeb);

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
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                UpdateDeltaVStats(activeJeb);

                return deltaVStage;
            }
            else
            {
                return double.NaN;
            }
        }

        public double GetLaunchAltitude()
        {
            double alt = 0.0;
            object activeJeb = GetMasterMechJeb(vessel);
            object ascent = GetComputerModule(activeJeb, "MechJebModuleAscentAutopilot");
            if (ascent != null)
            {
                object desiredAlt = launchOrbitAltitude.GetValue(ascent);
                if (desiredAlt != null)
                {
                    //object mult_o = getEditableDoubleMultMultiplier.GetValue(desiredAlt);
                    object alt_o = getEditableDoubleMult(desiredAlt);

                    if (alt_o != null)
                    {
                        alt = (double)alt_o;
                    }
                }
            }

            return alt;
        }

        public void SetLaunchAltitude(double altitude)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ascent = GetComputerModule(activeJeb, "MechJebModuleAscentAutopilot");
            if (ascent != null)
            {
                object desiredAlt = launchOrbitAltitude.GetValue(ascent);
                if (desiredAlt != null)
                {
                    setEditableDoubleMult(desiredAlt, new object[] { altitude });
                }
            }
        }

        public double GetLaunchInclination()
        {
            double angle = 0.0;
            object activeJeb = GetMasterMechJeb(vessel);
            object ascent = GetComputerModule(activeJeb, "MechJebModuleAscentGuidance");
            if (ascent != null)
            {
                object inclination = launchOrbitInclination.GetValue(ascent);
                angle = getEditableDouble(inclination);
            }
            return angle;
        }

        public void SetLaunchInclination(double inclination)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ascent = GetComputerModule(activeJeb, "MechJebModuleAscentGuidance");
            if (ascent != null)
            {
                object incline = launchOrbitInclination.GetValue(ascent);
                setEditableDouble(incline, new object[] { inclination });
            }
        }

        public double GetForceRollAngle()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");
            if (activeSmartass != null)
            {
                object forceRol = mjSmartassForceRol.GetValue(activeSmartass);
                object rolValue = mjSmartassRol.GetValue(activeSmartass);
                return getEditableDouble(rolValue);
            }
            else
            {
                return 0.0;
            }

        }

        public double GetTerminalVelocity()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                object vesselState = mjCoreVesselState.GetValue(activeJeb);
                if (vesselState != null)
                {
                    double value = terminalVelocity(vesselState);
                    return (double.IsNaN(value)) ? double.PositiveInfinity : value;
                }
            }

            return double.NaN;
        }

        /// <summary>
        /// Returns true when the current MJ target is a ground target.
        /// </summary>
        /// <returns></returns>
        public bool PositionTargetExists()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                object target = mjCoreTarget.GetValue(activeJeb);
                if (getPositionTargetExists(target))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when any autopilots are enabled (and thus Smartass is
        /// in "AUTO" mode).
        /// </summary>
        /// <returns></returns>
        public bool AutopilotEnabled()
        {
            // It appears the SmartASS module does not know if MJ is in
            // automatic pilot mode (like Ascent Guidance or Landing
            // Guidance) without querying indirectly like this.
            // MOARdV BUG: This doesn't seem to work if any of the
            // attitude settings are active (like "Prograde").
            //if (activeJeb.attitude.enabled && !activeJeb.attitude.users.Contains(activeSmartass))
            object activeJeb = GetMasterMechJeb(vessel);
            object attitude = mjCoreAttitude.GetValue(activeJeb);
            if (ModuleEnabled(attitude))
            {
                object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");
                object users = mjModuleUsers.GetValue(attitude);
                return (bool)containsUser(users, new object[] { activeSmartass });
            }

            return false;
        }

        private bool ForceRollState(double roll)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");
            if (activeSmartass != null)
            {
                object forceRol = mjSmartassForceRol.GetValue(activeSmartass);
                object rolValue = mjSmartassRol.GetValue(activeSmartass);
                double rol = getEditableDouble(rolValue);

                return (bool)forceRol && (Math.Abs(roll - rol) < 0.5);
            }
            else
            {
                return false;
            }
        }

        public bool GetModuleExists(string moduleName)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object module = GetComputerModule(activeJeb, moduleName);

            return (module != null);
        }

        public void ForceRoll(bool state, double roll)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");
            if (activeSmartass != null)
            {
                if (state)
                {
                    object rolValue = mjSmartassRol.GetValue(activeSmartass);
                    setEditableDouble(rolValue, new object[] { roll });
                    mjSmartassRol.SetValue(activeSmartass, rolValue);
                }
                mjSmartassForceRol.SetValue(activeSmartass, state);
                engageSmartass(activeSmartass, new object[] { true });
            }
        }

        public void CircularizeAtAltitude(double altitude)
        {
            if (GetMechJebAvailable() && altitude >= vessel.orbit.PeA && altitude <= vessel.orbit.ApA)
            {
                // Add validation
                double UT = vessel.orbit.NextTimeOfRadius(Planetarium.GetUniversalTime(), vessel.orbit.referenceBody.Radius + altitude);

                Vector3d dV;

                dV = (Vector3d)deltaVToCircularize(null, new object[] { vessel.orbit, UT });

                if (vessel.patchedConicSolver != null)
                {
                    while (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                    {
                        vessel.patchedConicSolver.RemoveManeuverNode(vessel.patchedConicSolver.maneuverNodes.Last());
                    }
                }

                placeManeuverNode(null, new object[] { vessel, vessel.orbit, dV, UT });
            }
        }

        public void ChangeApoapsis(double altitude)
        {
            if (GetMechJebAvailable() && altitude >= vessel.orbit.PeA)
            {
                double UT = vessel.orbit.NextPeriapsisTime(Planetarium.GetUniversalTime());

                Vector3d dV;

                dV = (Vector3d)deltaVToChangeApoapsis(null, new object[] { vessel.orbit, UT, vessel.orbit.referenceBody.Radius + altitude });

                if (vessel.patchedConicSolver != null)
                {
                    while (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                    {
                        vessel.patchedConicSolver.RemoveManeuverNode(vessel.patchedConicSolver.maneuverNodes.Last());
                    }
                }

                placeManeuverNode(null, new object[] { vessel, vessel.orbit, dV, UT });
            }
        }

        public void ChangePeriapsis(double altitude)
        {
            if (GetMechJebAvailable() && altitude <= vessel.orbit.ApA)
            {
                double UT = vessel.orbit.NextApoapsisTime(Planetarium.GetUniversalTime());

                Vector3d dV;

                dV = (Vector3d)deltaVToChangePeriapsis(null, new object[] { vessel.orbit, UT, vessel.orbit.referenceBody.Radius + altitude });

                if (vessel.patchedConicSolver != null)
                {
                    while (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                    {
                        vessel.patchedConicSolver.RemoveManeuverNode(vessel.patchedConicSolver.maneuverNodes.Last());
                    }
                }

                placeManeuverNode(null, new object[] { vessel, vessel.orbit, dV, UT });
            }
        }

        public void CircularizeAt(double UT)
        {
            Vector3d dV;

            dV = (Vector3d)deltaVToCircularize(null, new object[] { vessel.orbit, UT });

            if (vessel.patchedConicSolver != null)
            {
                while (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                {
                    vessel.patchedConicSolver.RemoveManeuverNode(vessel.patchedConicSolver.maneuverNodes.Last());
                }
            }

            placeManeuverNode(null, new object[] { vessel, vessel.orbit, dV, UT });
        }

        public double SpaceplaneHoldAltitude()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object altitude = spaceplaneAltitude.GetValue(ap);
                return getEditableDouble(altitude);
            }
            else
            {
                return 0.0;
            }
        }

        public void SetSpaceplaneHoldAltitude(double altitude)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object holdAltitude = spaceplaneAltitude.GetValue(ap);
                setEditableDouble(holdAltitude, new object[] { altitude });
            }
        }

        public bool SpaceplaneAltitudeProximity()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object holdaltitude = spaceplaneAltitude.GetValue(ap);
                double goalAltitude = getEditableDouble(holdaltitude);
                double currentAltitude = vessel.altitude;

                return (Math.Abs(currentAltitude - goalAltitude) <= 500.0);
            }
            else
            {
                return false;
            }
        }

        public double SpaceplaneHoldHeading()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object heading = spaceplaneHeading.GetValue(ap);
                return getEditableDouble(heading);
            }
            else
            {
                return 0.0;
            }

        }

        public void SetSpaceplaneHoldHeading(double heading)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object holdHeading = spaceplaneHeading.GetValue(ap);
                setEditableDouble(holdHeading, new object[] { heading });
            }
        }

        public double SpaceplaneGlideslope()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object slope = spaceplaneGlideslope.GetValue(ap);
                return getEditableDouble(slope);
            }
            else
            {
                return 0.0;
            }

        }

        public void SetSpaceplaneGlideslope(double angle)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object slope = spaceplaneGlideslope.GetValue(ap);
                setEditableDouble(slope, new object[] { angle });
            }
        }
        #endregion

        #region MechJebRPMButtons

        /// <summary>
        /// Enables / disable "Execute One Node"
        /// </summary>
        /// <param name="state"></param>
        public void ButtonNodeExecute(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                object node = mjCoreNode.GetValue(activeJeb);
                object mp = GetComputerModule(activeJeb, "MechJebModuleManeuverPlanner");
                if (node != null && mp != null)
                {
                    if (state)
                    {
                        if (!ModuleEnabled(node))
                        {
                            executeOneNode(node, new object[] { mp });
                        }
                    }
                    else
                    {
                        abortNode(node);
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether the maneuver node executor is enabled.
        /// </summary>
        /// <returns></returns>
        public bool ButtonNodeExecuteState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                object ap = mjCoreNode.GetValue(activeJeb);
                return ModuleEnabled(ap);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Enable the Ascent Autopilot
        /// </summary>
        /// <param name="state"></param>
        public void ButtonAscentGuidance(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleAscentAutopilot");

            if (ap != null)
            {
                // MOARdV TODO: When MJ 2.5.4 (or higher) is out, remove the
                // null check here and eliminate the else path, since getAAPEngaged
                // will be the only valid path.
                if (setAscentAutopilotEngaged != null)
                {
                    setAscentAutopilotEngaged(ap, new object[] { state });
                }
                else
                {
                    object users = mjModuleUsers.GetValue(ap);
                    if (users == null)
                    {
                        throw new NotImplementedException("mjModuleUsers(ap) was null");
                    }

                    object agPilot = GetComputerModule(activeJeb, "MechJebModuleAscentGuidance");
                    if (agPilot == null)
                    {
                        JUtil.LogErrorMessage(this, "Unable to fetch MechJebModuleAscentGuidance");
                        return;
                    }

                    if (ModuleEnabled(ap))
                    {
                        removeUser(users, new object[] { agPilot });
                    }
                    else
                    {
                        addUser(users, new object[] { agPilot });
                    }
                }
            }
        }

        /// <summary>
        /// Indicates whether the ascent autopilot is enabled
        /// </summary>
        /// <returns></returns>
        public bool ButtonAscentGuidanceState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                object ap = GetComputerModule(activeJeb, "MechJebModuleAscentAutopilot");

                // MOARdV TODO: When MJ 2.5.4 (or higher) is out, remove the
                // null check here and eliminate the else path, since getAAPEngaged
                // will be the only valid path.
                if (getAscentAutopilotEngaged != null)
                {
                    return getAscentAutopilotEngaged(ap);
                }
                else
                {
                    return ModuleEnabled(ap);
                }
            }
            else
            {
                return false;
            }
        }

        public void ButtonDockingGuidance(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object autopilot = GetComputerModule(activeJeb, "MechJebModuleDockingAutopilot");
            object autopilotController = GetComputerModule(activeJeb, "MechJebModuleDockingGuidance");

            if (autopilot != null && autopilotController != null)
            {
                object users = mjModuleUsers.GetValue(autopilot);
                if (users == null)
                {
                    throw new NotImplementedException("mjModuleUsers(autopilot) was null");
                }
                if (ModuleEnabled(autopilot))
                {
                    removeUser(users, new object[] { autopilotController });
                }
                else if (FlightGlobals.fetch.VesselTarget is ModuleDockingNode)
                {
                    addUser(users, new object[] { autopilotController });
                }
            }
        }

        public bool ButtonDockingGuidanceState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleDockingAutopilot");
            return ModuleEnabled(ap);
        }

        /// <summary>
        /// Plot a Hohmann transfer to the current target.  This is an instant
        /// fire-and-forget function, not a toggle switch
        /// </summary>
        /// <param name="state">unused</param>
        public void ButtonPlotHohmannTransfer(bool state)
        {
            if (!ButtonPlotHohmannTransferState())
            {
                // Target is not one MechJeb can successfully plot.
                return;
            }

            object activeJeb = GetMasterMechJeb(vessel);

            object target = mjCoreTarget.GetValue(activeJeb);
            Orbit targetOrbit = (Orbit)getTargetOrbit(target);
            Orbit o = vessel.orbit;
            Vector3d dV;
            double nodeUT = 0.0;
            if (o.referenceBody == targetOrbit.referenceBody)
            {
                object[] args = new object[] { o, targetOrbit, Planetarium.GetUniversalTime(), nodeUT };
                dV = (Vector3d)mjDeltaVAndTimeForHohmannTransfer.Invoke(null, args);
                nodeUT = (double)args[3];
            }
            else
            {
                object[] args = new object[] { o, Planetarium.GetUniversalTime(), targetOrbit, true, nodeUT };
                dV = (Vector3d)mjDeltaVAndTimeForInterplanetaryTransferEjection.Invoke(null, args);
                nodeUT = (double)args[4];
            }

            if (vessel.patchedConicSolver != null)
            {
                while (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                {
                    vessel.patchedConicSolver.RemoveManeuverNode(vessel.patchedConicSolver.maneuverNodes.Last());
                }
            }

            placeManeuverNode(null, new object[] { vessel, o, dV, nodeUT });
        }

        /// <summary>
        /// Indicate whether a Hohmann Transfer Orbit can be plotted
        /// </summary>
        /// <returns>true if a transfer can be plotted, false if not</returns>
        public bool ButtonPlotHohmannTransferState()
        {
            if (!mjFound)
            {
                return false;
            }

            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb == null)
            {
                return false;
            }

            object target = mjCoreTarget.GetValue(activeJeb);
            if (target == null)
            {
                return false;
            }

            // Most of these conditions are directly from MJ, or derived from
            // it.
            if (getNormalTargetExists(target) == false)
            {
                return false;
            }

            Orbit o = vessel.orbit;
            if (o.eccentricity > 0.2)
            {
                // Need fairly circular orbit to plot.
                return false;
            }

            Orbit targetOrbit = (Orbit)getTargetOrbit(target);
            if (o.referenceBody == targetOrbit.referenceBody)
            {
                // Target is in our SoI

                if (targetOrbit.eccentricity >= 1.0)
                {
                    // can't intercept hyperbolic targets
                    return false;
                }

                if (o.RelativeInclination(targetOrbit) > 30.0 && o.RelativeInclination(targetOrbit) < 150.0)
                {
                    // Target is in a drastically different orbital plane.
                    return false;
                }
            }
            else
            {
                // Target is not in our SoI
                if (o.referenceBody.referenceBody == null)
                {
                    // Can't plot a transfer from an orbit around the sun (really?)
                    return false;
                }
                if (o.referenceBody.referenceBody != targetOrbit.referenceBody)
                {
                    return false;
                }
                if (o.referenceBody.orbit.RelativeInclination(targetOrbit) > 30.0)
                {
                    // Can't handle highly inclined targets
                    return false;
                }
            }

            // Did we get through all the tests?  Then we can plot an orbit!
            return true;
        }

        /// <summary>
        /// Enables / disables landing guidance.  When a target is selected and
        /// this mode is enabled, the ship goes into "Land at Target" mode.  If
        /// a target is not selected, it becomes "Land Somewhere".
        /// </summary>
        /// <param name="state"></param>
        public void ButtonLandingGuidance(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                object autopilot = GetComputerModule(activeJeb, "MechJebModuleLandingAutopilot");
                if (state != ModuleEnabled(autopilot))
                {
                    if (state)
                    {
                        object landingGuidanceAP = GetComputerModule(activeJeb, "MechJebModuleLandingGuidance");
                        if (landingGuidanceAP != null)
                        {
                            object target = mjCoreTarget.GetValue(activeJeb);
                            if (getPositionTargetExists(target))
                            {
                                landAtPositionTarget(autopilot, new object[] { landingGuidanceAP });
                            }
                            else
                            {
                                landUntargeted(autopilot, new object[] { landingGuidanceAP });
                            }
                        }
                    }
                    else
                    {
                        stopLanding(autopilot);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the current state of the landing guidance feature
        /// </summary>
        /// <returns>true if on, false if not</returns>
        public bool ButtonLandingGuidanceState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb != null)
            {
                object ap = GetComputerModule(activeJeb, "MechJebModuleLandingAutopilot");
                return ModuleEnabled(ap);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Toggles SmartASS Force Roll mode.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");
            if (activeSmartass != null)
            {
                mjSmartassForceRol.SetValue(activeSmartass, state);
                engageSmartass(activeSmartass, new object[] { true });
            }
        }

        /// <summary>
        /// Indicates whether SmartASS Force Roll is on or off
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRollState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object activeSmartass = GetComputerModule(activeJeb, "MechJebModuleSmartASS");
            if (activeSmartass != null)
            {
                object forceRol = mjSmartassForceRol.GetValue(activeSmartass);
                return (bool)forceRol;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Force the roll to zero degrees.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll0(bool state)
        {
            ForceRoll(state, 0.0);
        }
        /// <summary>
        /// Returns true when Force Roll is on, and the roll is set to 0.
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRoll0State()
        {
            return ForceRollState(0.0);
        }

        /// <summary>
        /// Force the roll to +90 degrees.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll90(bool state)
        {
            ForceRoll(state, 90.0);
        }
        /// <summary>
        /// Returns true when Force Roll is on, and the roll is set to 90.
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRoll90State()
        {
            return ForceRollState(90.0);
        }

        /// <summary>
        /// Force the roll to 180 degrees.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll180(bool state)
        {
            ForceRoll(state, 180.0);
        }

        /// <summary>
        /// Returns true when Force Roll is on, and the roll is set to 180.
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRoll180State()
        {
            return ForceRollState(180.0);
        }

        /// <summary>
        /// Force the roll to -90 degrees.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll270(bool state)
        {
            ForceRoll(state, -90.0);
        }

        /// <summary>
        /// Returns true when Force Roll is on, and the roll is set to -90.
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRoll270State()
        {
            return ForceRollState(-90.0);
        }

        /// <summary>
        /// The MechJeb landing prediction simulator runs on a separate thread,
        /// and it may be costly for lower-end computers to leave it running
        /// all the time.  This button allows the player to indicate whether
        /// it needs to be running, or not.
        /// </summary>
        /// <param name="state">Enable/disable</param>
        public void ButtonEnableLandingPrediction(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object predictor = GetComputerModule(activeJeb, "MechJebModuleLandingPredictions");
            object landingGuidanceAP = GetComputerModule(activeJeb, "MechJebModuleLandingGuidance");

            if (predictor != null && landingGuidanceAP != null)
            {
                object users = mjModuleUsers.GetValue(predictor);
                if (users == null)
                {
                    throw new NotImplementedException("mjModuleUsers(predictor) was null");
                }
                if (state)
                {
                    addUser(users, new object[] { landingGuidanceAP });
                }
                else
                {
                    removeUser(users, new object[] { landingGuidanceAP });
                }
            }
        }

        /// <summary>
        /// Returns whether the landing prediction simulator is currently
        /// running.
        /// </summary>
        /// <returns></returns>
        public bool ButtonEnableLandingPredictionState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleLandingPredictions");
            return ModuleEnabled(ap);
        }

        /// <summary>
        /// Engages / disengages Rendezvous Autopilot
        /// </summary>
        /// <param name="state"></param>
        public void ButtonRendezvousAutopilot(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object autopilot = GetComputerModule(activeJeb, "MechJebModuleRendezvousAutopilot");
            object autopilotController = GetComputerModule(activeJeb, "MechJebModuleRendezvousAutopilotWindow");

            if (autopilot != null && autopilotController != null)
            {
                object users = mjModuleUsers.GetValue(autopilot);
                if (users == null)
                {
                    throw new NotImplementedException("mjModuleUsers(autopilot) was null");
                }
                if (state)
                {
                    addUser(users, new object[] { autopilotController });
                }
                else
                {
                    removeUser(users, new object[] { autopilotController });
                }
            }
        }

        /// <summary>
        /// Indicates whether the Rendezvous Autopilot is engaged.
        /// </summary>
        /// <returns></returns>
        public bool ButtonRendezvousAutopilotState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleRendezvousAutopilot");
            return ModuleEnabled(ap);
        }

        /// <summary>
        /// Instructs the craft to hold heading (or disables autopilot).
        /// </summary>
        /// <param name="state"></param>
        public void ButtonSpaceplaneHoldHeading(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            object controller = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneGuidance");
            if (ap != null && controller != null)
            {
                if (state)
                {
                    spaceplaneHoldHeading(ap, new object[] { controller });
                }
                else
                {
                    spaceplaneAPOff(ap);
                }
            }
        }

        /// <summary>
        /// Returns true if SP autopilot is in Hold Heading mode
        /// </summary>
        /// <returns></returns>
        public bool ButtonSpaceplaneHoldHeadingState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object mode = spaceplaneAPMode.GetValue(ap);
                return ((int)mode == (int)SpaceplaneMode.HOLD);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Enables / disabled spaceplane Autoland mode
        /// </summary>
        /// <returns></returns>
        public void ButtonSpaceplaneAutoland(bool state)
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            object controller = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneGuidance");
            if (ap != null && controller != null)
            {
                if (state)
                {
                    spaceplaneAutoland(ap, new object[] { controller });
                }
                else
                {
                    spaceplaneAPOff(ap);
                }
            }
        }

        /// <summary>
        /// Returns true if SP autopilot is in Autoland mode
        /// </summary>
        /// <returns></returns>
        public bool ButtonSpaceplaneAutolandState()
        {
            object activeJeb = GetMasterMechJeb(vessel);
            object ap = GetComputerModule(activeJeb, "MechJebModuleSpaceplaneAutopilot");
            if (ap != null)
            {
                object mode = spaceplaneAPMode.GetValue(ap);
                return ((int)mode == (int)SpaceplaneMode.AUTOLAND);
            }
            else
            {
                return false;
            }
        }

        public void MatchVelocities(bool state)
        {
            if (!MatchVelocitiesState())
            {
                return;
            }

            try
            {
                object activeJeb = GetMasterMechJeb(vessel);
                if (activeJeb == null)
                {
                    return;
                }

                object target = mjCoreTarget.GetValue(activeJeb);
                if (target == null)
                {
                    return;
                }

                Orbit targetOrbit = (Orbit)getTargetOrbit(target);
                Orbit o = vessel.orbit;
                Vector3d dV;
                double nodeUT;// closest approach time
                JUtil.GetClosestApproach(o, targetOrbit, out nodeUT);

                object[] args = new object[] { o, nodeUT, targetOrbit };
                dV = (Vector3d)mjDeltaVToMatchVelocities.Invoke(null, args);

                if (vessel.patchedConicSolver != null)
                {
                    while (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                    {
                        vessel.patchedConicSolver.RemoveManeuverNode(vessel.patchedConicSolver.maneuverNodes.Last());
                    }
                }

                placeManeuverNode(null, new object[] { vessel, o, dV, nodeUT });
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "MatchVelocities tripped an exception: {0}", e);
            }
        }

        public bool MatchVelocitiesState()
        {
            if (!mjFound)
            {
                return false;
            }

            object activeJeb = GetMasterMechJeb(vessel);
            if (activeJeb == null)
            {
                return false;
            }

            object target = mjCoreTarget.GetValue(activeJeb);
            if (target == null)
            {
                return false;
            }

            // Most of these conditions are directly from MJ, or derived from
            // it.
            if (getNormalTargetExists(target) == false)
            {
                return false;
            }
            else
            {
                return true;
            }
        }


        // All the other buttons are pretty much identical and just use different enum values.

        // Off button
        // Analysis disable once UnusedParameter
        public void ButtonOff(bool state)
        {
            EnactTargetAction(vessel, Target.OFF);
        }

        public bool ButtonOffState()
        {
            return ReturnTargetState(Target.OFF);
        }

        // NODE button
        public void ButtonNode(bool state)
        {
            if (vessel.patchedConicSolver != null)
            {
                if (state && vessel.patchedConicSolver.maneuverNodes.Count > 0)
                {
                    EnactTargetAction(vessel, Target.NODE);
                }
                else if (!state)
                {
                    EnactTargetAction(vessel, Target.OFF);
                }
            }
        }

        public bool ButtonNodeState()
        {
            return ReturnTargetState(Target.NODE);
        }

        // KillRot button
        public void ButtonKillRot(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.KILLROT : Target.OFF);
        }

        public bool ButtonKillRotState()
        {
            return ReturnTargetState(Target.KILLROT);
        }

        // Prograde button
        public void ButtonPrograde(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.PROGRADE : Target.OFF);
        }
        public bool ButtonProgradeState()
        {
            return ReturnTargetState(Target.PROGRADE);
        }

        // Retrograde button
        public void ButtonRetrograde(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.RETROGRADE : Target.OFF);
        }
        public bool ButtonRetrogradeState()
        {
            return ReturnTargetState(Target.RETROGRADE);
        }

        // NML+ button
        public void ButtonNormalPlus(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.NORMAL_PLUS : Target.OFF);
        }
        public bool ButtonNormalPlusState()
        {
            return ReturnTargetState(Target.NORMAL_PLUS);
        }

        // NML- button
        public void ButtonNormalMinus(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.NORMAL_MINUS : Target.OFF);
        }
        public bool ButtonNormalMinusState()
        {
            return ReturnTargetState(Target.NORMAL_MINUS);
        }

        // RAD+ button
        public void ButtonRadialPlus(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.RADIAL_PLUS : Target.OFF);
        }
        public bool ButtonRadialPlusState()
        {
            return ReturnTargetState(Target.RADIAL_PLUS);
        }

        // RAD- button
        public void ButtonRadialMinus(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.RADIAL_MINUS : Target.OFF);
        }
        public bool ButtonRadialMinusState()
        {
            return ReturnTargetState(Target.RADIAL_MINUS);
        }

        // Surface prograde button
        public void ButtonSurfacePrograde(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.SURFACE_PROGRADE : Target.OFF);
        }
        public bool ButtonSurfaceProgradeState()
        {
            return ReturnTargetState(Target.SURFACE_PROGRADE);
        }

        // Surface Retrograde button
        public void ButtonSurfaceRetrograde(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.SURFACE_RETROGRADE : Target.OFF);
        }
        public bool ButtonSurfaceRetrogradeState()
        {
            return ReturnTargetState(Target.SURFACE_RETROGRADE);
        }

        // Horizontal + button
        public void ButtonHorizontalPlus(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.HORIZONTAL_PLUS : Target.OFF);
        }
        public bool ButtonHorizontalPlusState()
        {
            return ReturnTargetState(Target.HORIZONTAL_PLUS);
        }

        // Horizontal - button
        public void ButtonHorizontalMinus(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.HORIZONTAL_MINUS : Target.OFF);
        }
        public bool ButtonHorizontalMinusState()
        {
            return ReturnTargetState(Target.HORIZONTAL_MINUS);
        }

        // Up button
        public void ButtonVerticalPlus(bool state)
        {
            EnactTargetAction(vessel, (state) ? Target.VERTICAL_PLUS : Target.OFF);
        }
        public bool ButtonVerticalPlusState()
        {
            return ReturnTargetState(Target.VERTICAL_PLUS);
        }

        // Target group buttons additionally require a target to be set to press.
        // TGT+ button
        public void ButtonTargetPlus(bool state)
        {
            if (!state)
            {
                EnactTargetAction(vessel, Target.OFF);
            }
            else if (FlightGlobals.fetch.VesselTarget != null)
            {
                EnactTargetAction(vessel, Target.TARGET_PLUS);
            }
        }
        public bool ButtonTargetPlusState()
        {
            return ReturnTargetState(Target.TARGET_PLUS);
        }

        // TGT- button
        public void ButtonTargetMinus(bool state)
        {
            if (!state)
            {
                EnactTargetAction(vessel, Target.OFF);
            }
            else if (FlightGlobals.fetch.VesselTarget != null)
            {
                EnactTargetAction(vessel, Target.TARGET_MINUS);
            }
        }
        public bool ButtonTargetMinusState()
        {
            return ReturnTargetState(Target.TARGET_MINUS);
        }

        // RVEL+ button
        public void ButtonRvelPlus(bool state)
        {
            if (!state)
            {
                EnactTargetAction(vessel, Target.OFF);
            }
            else if (FlightGlobals.fetch.VesselTarget != null)
            {
                EnactTargetAction(vessel, Target.RELATIVE_PLUS);
            }
        }
        public bool ButtonRvelPlusState()
        {
            return ReturnTargetState(Target.RELATIVE_PLUS);
        }

        // RVEL- button
        public void ButtonRvelMinus(bool state)
        {
            if (!state)
            {
                EnactTargetAction(vessel, Target.OFF);
            }
            else if (FlightGlobals.fetch.VesselTarget != null)
            {
                EnactTargetAction(vessel, Target.RELATIVE_MINUS);
            }
        }
        public bool ButtonRvelMinusState()
        {
            return ReturnTargetState(Target.RELATIVE_MINUS);
        }

        // PAR+ button
        public void ButtonParPlus(bool state)
        {
            if (!state)
            {
                EnactTargetAction(vessel, Target.OFF);
            }
            else if (FlightGlobals.fetch.VesselTarget != null)
            {
                EnactTargetAction(vessel, Target.PARALLEL_PLUS);
            }
        }
        public bool ButtonParPlusState()
        {
            return ReturnTargetState(Target.PARALLEL_PLUS);
        }

        // PAR- button
        public void ButtonParMinus(bool state)
        {
            if (!state)
            {
                EnactTargetAction(vessel, Target.OFF);
            }
            else if (FlightGlobals.fetch.VesselTarget != null)
            {
                EnactTargetAction(vessel, Target.PARALLEL_MINUS);
            }
        }
        public bool ButtonParMinusState()
        {
            return ReturnTargetState(Target.PARALLEL_MINUS);
        }

        #endregion
    }
}
