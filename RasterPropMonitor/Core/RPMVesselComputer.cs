//#define SHOW_FIXEDUPDATE_TIMING
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
using UnityEngine;

// MOARdV TODO:
// Add callbacks for docking, undocking, staging, vessel switching
// ? GameEvents.onJointBreak
// + GameEvents.onUndock
// ? GameEvents.onSameVesselDock
// ? GameEvents.onSameVesselUndock
// + GameEvents.onPartCouple
// + GameEvents.onStageActivate
// ? GameEvents.onStageSeparation
// + GameEvents.onVesselChange
// ? GameEvents.OnVesselModified
//
// ? GameEvents.onCrewOnEva
// ? GameEvents.onCrewTransferred
// ? GameEvents.onKerbalAdded
// ? GameEvents.onKerbalRemoved
//
// Things to look at ?
// FlightUIController.(LinearGauge)atmos
namespace JSI
{
    public partial class RPMVesselComputer : VesselModule
    {
        #region Static Variables
        /*
         * This region contains static variables - variables that only need to
         * exist in a single instance.  They are instantiated by the first
         * vessel to enter flight, and released by the last vessel before a
         * scene change.
         */
        private static Dictionary<Guid, RPMVesselComputer> instances;

        private static Dictionary<string, CustomVariable> customVariables;
        private static List<string> knownLoadedAssemblies;
        private static Dictionary<string, MappedVariable> mappedVariables;
        private static Dictionary<string, MathVariable> mathVariables;
        private static SortedDictionary<string, string> systemNamedResources;
        private static List<TriggeredEventTemplate> triggeredEvents;
        private static List<IJSIModule> installedModules;

        private static Protractor protractor = null;

        private static readonly int gearGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Gear);
        private static readonly int brakeGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Brakes);
        private static readonly int sasGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.SAS);
        private static readonly int lightGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Light);
        private static readonly int rcsGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.RCS);
        private static readonly int[] actionGroupID = {
            BaseAction.GetGroupIndex(KSPActionGroup.Custom10),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom01),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom02),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom03),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom04),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom05),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom06),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom07),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom08),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom09)
        };
        private readonly string[] actionGroupMemo = {
            "AG0",
            "AG1",
            "AG2",
            "AG3",
            "AG4",
            "AG5",
            "AG6",
            "AG7",
            "AG8",
            "AG9"
        };
        private const float gee = 9.81f;
        private readonly double upperAtmosphereLimit = Math.Log(100000.0);
        private static double standardAtmosphere = -1.0;
        #endregion

        #region Instance Variables
        /*
         * This region contains variables that apply per-instance (per vessel).
         */
        private Vessel vessel;
        private NavBall navBall;
        private ManeuverNode node;
        private Part part;
        internal Part ReferencePart
        {
            // Return the part that RPMVesselComputer considers the reference
            // part (the part we're "in" during IVA).
            get
            {
                return part;
            }
        }
        private ExternalVariableHandlers plugins;
        private RasterPropMonitorComputer rpmComp;

        // Data refresh
        private int dataUpdateCountdown;
        private int refreshDataRate = 60;
        private bool timeToUpdate = false;

        // Processing cache!
        private readonly DefaultableDictionary<string, object> resultCache = new DefaultableDictionary<string, object>(null);

        private Dictionary<string, Func<bool>> pluginBoolVariables = new Dictionary<string, Func<bool>>();
        private Dictionary<string, Func<double>> pluginDoubleVariables = new Dictionary<string, Func<double>>();
        private Dictionary<string, PluginEvaluator> pluginVariables = new Dictionary<string, PluginEvaluator>();

        // Craft-relative basis vectors
        private Vector3 forward;
        public Vector3 Forward
        {
            get
            {
                return forward;
            }
        }
        private Vector3 right;
        //public Vector3 Right
        //{
        //    get
        //    {
        //        return right;
        //    }
        //}
        private Vector3 top;
        //public Vector3 Top
        //{
        //    get
        //    {
        //        return top;
        //    }
        //}

        // Orbit-relative vectors
        private Vector3 prograde;
        public Vector3 Prograde
        {
            get
            {
                return prograde;
            }
        }
        private Vector3 radialOut;
        public Vector3 RadialOut
        {
            get
            {
                return radialOut;
            }
        }
        private Vector3 normalPlus;
        public Vector3 NormalPlus
        {
            get
            {
                return normalPlus;
            }
        }

        // Surface-relative vectors
        private Vector3 up;
        public Vector3 Up
        {
            get
            {
                return up;
            }
        }
        // surfaceRight is the projection of the right vector onto the surface.
        // If up x right is a degenerate vector (rolled on the side), we use
        // the forward vector to compose a new basis
        private Vector3 surfaceRight;
        //public Vector3 SurfaceRight
        //{
        //    get
        //    {
        //        return surfaceRight;
        //    }
        //}
        // surfaceForward is the cross of the up vector and right vector, so
        // that surface velocity can be decomposed to surface-relative components.
        private Vector3 surfaceForward;
        public Vector3 SurfaceForward
        {
            get
            {
                return surfaceForward;
            }
        }

        private Quaternion rotationVesselSurface;
        public Quaternion RotationVesselSurface
        {
            get
            {
                return rotationVesselSurface;
            }
        }

        // Helper to get sideslip for the HUD
        internal float Sideslip
        {
            get
            {
                return (float)SideSlip();
            }
        }
        // Helper to get the AoA in absolute terms (instead of relative to the
        // nose) for the HUD.
        internal float AbsoluteAoA
        {
            get
            {
                return ((rotationVesselSurface.eulerAngles.x > 180.0f) ? (360.0f - rotationVesselSurface.eulerAngles.x) : -rotationVesselSurface.eulerAngles.x) - (float)AngleOfAttack();
            }
        }

        // Tracked vessel variables
        private float actualAverageIsp;
        private float actualMaxIsp;
        private double altitudeASL;
        //public double AltitudeASL
        //{
        //    get
        //    {
        //        return altitudeASL;
        //    }
        //}
        private double altitudeBottom;
        private double altitudeTrue;
        private bool anyEnginesFlameout;
        private bool anyEnginesOverheating;
        private Vector3d CoM;
        private float heatShieldTemperature;
        private float heatShieldFlux;
        private float localGeeASL;
        private float localGeeDirect;
        private bool orbitSensibility;
        private ResourceDataStorage resources = new ResourceDataStorage();
        private string[] resourcesAlphabetic = null;
        private float slopeAngle;
        private double speedHorizontal;
        private double speedVertical;
        private double speedVerticalRounded;
        private float totalCurrentThrust;
        private float totalDataAmount;
        private float totalExperimentCount;
        private float totalMaximumThrust;
        private float totalShipDryMass;
        private float totalShipWetMass;

        private List<ProtoCrewMember> vesselCrew = new List<ProtoCrewMember>();
        private List<kerbalExpressionSystem> vesselCrewMedical = new List<kerbalExpressionSystem>();
        private List<ProtoCrewMember> localCrew = new List<ProtoCrewMember>();
        private List<kerbalExpressionSystem> localCrewMedical = new List<kerbalExpressionSystem>();

        private double lastAltitudeBottomSampleTime;
        private double lastAltitudeBottom, terrainDelta;
        // radarAltitudeRate as computed using a simple exponential smoothing.
        private float radarAltitudeRate = 0.0f;
        private double lastRadarAltitudeTime;

        // Target values
        private ITargetable target;
        private CelestialBody targetBody;
        private ModuleDockingNode targetDockingNode;
        private Vessel targetVessel;
        private Orbit targetOrbit;
        private bool targetOrbitSensibility;
        private double targetDistance;
        private Vector3d targetSeparation;
        public Vector3d TargetSeparation
        {
            get
            {
                return targetSeparation;
            }
        }
        private Vector3d velocityRelativeTarget;
        private float approachSpeed;
        private Quaternion targetOrientation;

        // Diagnostics
#if SHOW_FIXEDUPDATE_TIMING
        private Stopwatch stopwatch = new Stopwatch();
#endif
        #endregion

        public static RPMVesselComputer Instance(Vessel v)
        {
            if (instances == null)
            {
                JUtil.LogErrorMessage(null, "RPMVesselComputer.Instance called with uninitialized instances.");
                throw new Exception("RPMVesselComputer.Instance called with uninitialized instances.");
            }

            if (!instances.ContainsKey(v.id))
            {
                JUtil.LogMessage(null, "RPMVesselComputer.Instance called with unrecognized vessel {0} ({1}).", v.vesselName, v.id);
                RPMVesselComputer comp = v.GetComponent<RPMVesselComputer>();
                if (comp == null)
                {
                    throw new Exception("RPMVesselComputer.Instance called with an unrecognized vessel, and I can't find one on the vessel.");
                }

                instances.Add(v.id, comp);
            }

            return instances[v.id];
        }

        #region VesselModule Overrides
        public void Awake()
        {
            if (instances == null)
            {
                JUtil.LogMessage(this, "Initializing RPM version {0}", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
                instances = new Dictionary<Guid, RPMVesselComputer>();
            }
            if (protractor == null)
            {
                protractor = new Protractor();
            }
            // MOARdV TODO: Only add this instance to the library if there is
            // crew capacity.  Probes should not apply.  Except, what about docking?
            vessel = GetComponent<Vessel>();
            if (vessel == null)
            {
                throw new Exception("RPMVesselComputer: GetComponent<Vessel>() returned null");
            }
            if (instances.ContainsKey(vessel.id))
            {
                JUtil.LogErrorMessage(this, "Awake for vessel {0} ({1}), but it's already in the dictionary.", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName, vessel.id);
            }
            else
            {
                instances.Add(vessel.id, this);
                JUtil.LogMessage(this, "Awake for vessel {0} ({1}).", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName, vessel.id);
            }

            GameEvents.onGameSceneLoadRequested.Add(LoadSceneCallback);
            GameEvents.onVesselChange.Add(VesselChangeCallback);
            GameEvents.onStageActivate.Add(StageActivateCallback);
            GameEvents.onUndock.Add(UndockCallback);
            GameEvents.onVesselWasModified.Add(VesselModifiedCallback);

            if (customVariables == null)
            {
                customVariables = new Dictionary<string, CustomVariable>();
                // And parse known custom variables
                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("RPM_CUSTOM_VARIABLE"))
                {
                    string varName = node.GetValue("name");

                    try
                    {
                        CustomVariable customVar = new CustomVariable(node);

                        if (!string.IsNullOrEmpty(varName) && customVar != null)
                        {
                            string completeVarName = "CUSTOM_" + varName;
                            customVariables.Add(completeVarName, customVar);
                            JUtil.LogMessage(this, "I know about {0}", completeVarName);
                        }
                    }
                    catch
                    {

                    }
                }
            }

            if (knownLoadedAssemblies == null)
            {
                knownLoadedAssemblies = new List<string>();
                foreach (AssemblyLoader.LoadedAssembly thatAssembly in AssemblyLoader.loadedAssemblies)
                {
                    string thatName = thatAssembly.assembly.GetName().Name;
                    knownLoadedAssemblies.Add(thatName.ToUpper());
                    JUtil.LogMessage(this, "I know that {0} ISLOADED_{1}", thatName, thatName.ToUpper());
                }

            }

            if (mappedVariables == null)
            {
                if (!GameDatabase.Instance.IsReady())
                {
                    throw new Exception("GameDatabase is not ready?");
                }

                mappedVariables = new Dictionary<string, MappedVariable>();
                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("RPM_MAPPED_VARIABLE"))
                {
                    string varName = node.GetValue("mappedVariable");

                    try
                    {
                        MappedVariable mappedVar = new MappedVariable(node);

                        if (!string.IsNullOrEmpty(varName) && mappedVar != null)
                        {
                            string completeVarName = "MAPPED_" + varName;
                            mappedVariables.Add(completeVarName, mappedVar);
                            JUtil.LogMessage(this, "I know about {0}", completeVarName);
                        }
                    }
                    catch
                    {

                    }
                }
            }

            if (mathVariables == null)
            {
                mathVariables = new Dictionary<string, MathVariable>();
                // And parse known custom variables
                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("RPM_MATH_VARIABLE"))
                {
                    string varName = node.GetValue("name");

                    try
                    {
                        MathVariable mathVar = new MathVariable(node);

                        if (!string.IsNullOrEmpty(varName) && mathVar != null)
                        {
                            string completeVarName = "MATH_" + varName;
                            mathVariables.Add(completeVarName, mathVar);
                            JUtil.LogMessage(this, "I know about {0}", completeVarName);
                        }
                    }
                    catch
                    {

                    }
                }
            }

            if (systemNamedResources == null)
            {
                // Let's deal with the system resource library.
                // This dictionary is sorted so that longer names go first to prevent false identification - they're compared in order.
                systemNamedResources = new SortedDictionary<string, string>(new ResourceNameLengthComparer());
                foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions)
                {
                    string varname = thatResource.name.ToUpperInvariant().Replace(' ', '-').Replace('_', '-');
                    systemNamedResources.Add(varname, thatResource.name);
                    JUtil.LogMessage(this, "Remembering system resource {1} as SYSR_{0}", varname, thatResource.name);
                }
            }

            if (installedModules == null)
            {
                installedModules = new List<IJSIModule>();

                installedModules.Add(new JSIParachute());
                installedModules.Add(new JSIMechJeb());
                installedModules.Add(new JSIInternalRPMButtons());
                installedModules.Add(new JSIGimbal());
                installedModules.Add(new JSIFAR());
                installedModules.Add(new JSIKAC());
            }

            if (triggeredEvents == null)
            {
                triggeredEvents = new List<TriggeredEventTemplate>();

                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("RPM_TRIGGERED_EVENT"))
                {
                    string eventName = node.GetValue("eventName").Trim();

                    try
                    {
                        TriggeredEventTemplate triggeredVar = new TriggeredEventTemplate(node);

                        if (!string.IsNullOrEmpty(eventName) && triggeredVar != null)
                        {
                            triggeredEvents.Add(triggeredVar);
                            JUtil.LogMessage(this, "I know about event {0}", eventName);
                        }
                    }
                    catch (Exception e)
                    {
                        JUtil.LogMessage(this, "Error adding triggered event {0}: {1}", eventName, e);
                    }
                }
            }
        }

        public void Start()
        {
            JUtil.LogMessage(this, "Start for vessel {0} ({1})", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName, vessel.id);
            navBall = FlightUIController.fetch.GetComponentInChildren<NavBall>();
            if (standardAtmosphere < 0.0)
            {
                standardAtmosphere = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(0, FlightGlobals.Bodies[1]), FlightGlobals.Bodies[1].atmosphereTemperatureSeaLevel);
            }

            if (JUtil.IsActiveVessel(vessel))
            {
                IJSIModule.vessel = vessel;

                FetchPerPartData();
                FetchAltitudes();
                FetchVesselData();
                FetchTargetData();
            }
        }

        public void OnDestroy()
        {
            JUtil.LogMessage(this, "OnDestroy for vessel {0} ({1})", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName, vessel.id);
            GameEvents.onGameSceneLoadRequested.Remove(LoadSceneCallback);
            GameEvents.onVesselChange.Remove(VesselChangeCallback);
            GameEvents.onStageActivate.Remove(StageActivateCallback);
            GameEvents.onUndock.Remove(UndockCallback);
            GameEvents.onVesselWasModified.Remove(VesselModifiedCallback);

            if (!instances.ContainsKey(vessel.id))
            {
                JUtil.LogErrorMessage(this, "OnDestroy for vessel {0}, but it's not in the dictionary.", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName);
            }
            else
            {
                instances.Remove(vessel.id);
            }

            resultCache.Clear();

            vessel = null;
            navBall = null;
            node = null;
            part = null;
            rpmComp = null;

            pluginBoolVariables = null;
            pluginDoubleVariables = null;
            pluginVariables = null;

            target = null;
            targetDockingNode = null;
            targetVessel = null;
            targetOrbit = null;
            targetBody = null;

            resources = null;
            resourcesAlphabetic = null;

            vesselCrew.Clear();
            vesselCrewMedical.Clear();
            localCrew.Clear();
            localCrewMedical.Clear();

            evaluateMechJebAvailable = null;
            evaluateAngleOfAttack = null;
            evaluateDeltaV = null;
            evaluateDeltaVStage = null;
            evaluateDynamicPressure = null;
            evaluateLandingError = null;
            evaluateLandingAltitude = null;
            evaluateLandingLatitude = null;
            evaluateLandingLongitude = null;
            evaluateSideSlip = null;
            evaluateTerminalVelocity = null;
        }

        public void Update()
        {
            if (JUtil.IsActiveVessel(vessel) && UpdateCheck())
            {
                timeToUpdate = true;
            }
        }

        public void FixedUpdate()
        {
            // MOARdV TODO: FixedUpdate only if in IVA?  What about transparent pods?
            if (JUtil.VesselIsInIVA(vessel))
            {
                UpdateVariables();
            }
        }

        public void UpdateVariables()
        {
            // Update values related to the vessel (position, CoM, etc)
            if (timeToUpdate)
            {
#if SHOW_FIXEDUPDATE_TIMING
                stopwatch.Reset();
                stopwatch.Start();
#endif
                Part newpart = DeduceCurrentPart();
                if (newpart != part)
                {
                    part = newpart;
                    // We instantiate plugins late.
                    if (part == null)
                    {
                        JUtil.LogErrorMessage(this, "Unable to deduce the current part");
                    }
                    else if (plugins == null)
                    {
                        plugins = new ExternalVariableHandlers(part);
                    }
                    // Refresh some per-part values .. ?
                    rpmComp = RasterPropMonitorComputer.Instantiate(part);
                }

#if SHOW_FIXEDUPDATE_TIMING
                long newPart = stopwatch.ElapsedMilliseconds;
#endif

                timeToUpdate = false;
                resultCache.Clear();

                IJSIModule.vessel = vessel;
#if SHOW_FIXEDUPDATE_TIMING
                long invalidate = stopwatch.ElapsedMilliseconds;
#endif

                FetchPerPartData();
#if SHOW_FIXEDUPDATE_TIMING
                long perpart = stopwatch.ElapsedMilliseconds;
#endif
                FetchAltitudes();
#if SHOW_FIXEDUPDATE_TIMING
                long altitudes = stopwatch.ElapsedMilliseconds;
#endif
                FetchVesselData();
#if SHOW_FIXEDUPDATE_TIMING
                long vesseldata = stopwatch.ElapsedMilliseconds;
#endif
                FetchTargetData();

                for (int i = 0; i < activeTriggeredEvents.Count; ++i)
                {
                    activeTriggeredEvents[i].Update(this);
                }
#if SHOW_FIXEDUPDATE_TIMING
                long targetdata = stopwatch.ElapsedMilliseconds;
                stopwatch.Stop();

                JUtil.LogMessage(this, "FixedUpdate net ms: deduceNewPart = {0}, invalidate = {1}, FetchPerPart = {2}, FetchAlt = {3}, FetchVessel = {4}, FetchTarget = {5}",
                    newPart, invalidate, perpart, altitudes, vesseldata, targetdata);
#endif
            }
        }
        #endregion

        #region Interface Methods
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
        /// This intermediary will cache the results so that multiple variable
        /// requests within the frame would not result in duplicated code.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="propId"></param>
        /// <returns></returns>
        public object ProcessVariable(string input, int propId = -1)
        {
            object returnValue = resultCache[input];
            if (returnValue == null)
            {
                bool cacheable;
                try
                {
                    if (plugins == null || !plugins.ProcessVariable(input, out returnValue, out cacheable))
                    {
                        if (rpmComp == null)
                        {
                            if (part == null)
                            {
                                part = DeduceCurrentPart();
                            }

                            if (part != null)
                            {
                                rpmComp = RasterPropMonitorComputer.Instantiate(part);
                            }
                        }

                        returnValue = VariableToObject(input, propId, out cacheable);
                    }
                }
                catch (Exception e)
                {
                    JUtil.LogErrorMessage(this, "Processing error while processing {0}: {1}", input, e.Message);
                    // Most of the variables are doubles...
                    return double.NaN;
                }

                if (cacheable && returnValue != null)
                {
                    //JUtil.LogMessage(this, "Found variable \"{0}\"!  It was {1}", input, returnValue);
                    resultCache.Add(input, returnValue);
                }
            }

            return returnValue;
        }

        /// <summary>
        /// Initialize vessel description-based values.
        /// </summary>
        /// <param name="vesselDescription"></param>
        internal void SetVesselDescription(string vesselDescription)
        {
            string[] descriptionStrings = vesselDescription.UnMangleConfigText().Split(JUtil.LineSeparator, StringSplitOptions.None);
            for (int i = 0; i < descriptionStrings.Length; i++)
            {
                if (descriptionStrings[i].StartsWith("AG", StringComparison.Ordinal) && descriptionStrings[i][3] == '=')
                {
                    uint groupID;
                    if (uint.TryParse(descriptionStrings[i][2].ToString(), out groupID))
                    {
                        actionGroupMemo[groupID] = descriptionStrings[i].Substring(4).Trim();
                        descriptionStrings[i] = string.Empty;
                    }
                }
            }
            //vesselDescriptionForDisplay = string.Join(Environment.NewLine, descriptionStrings).MangleConfigText();

        }

        /// <summary>
        /// Set the refresh rate (number of Update() calls per triggered update).
        /// The lower of the current data rate and the new data rate is used.
        /// </summary>
        /// <param name="newDataRate">New data rate</param>
        internal void UpdateDataRefreshRate(int newDataRate)
        {
            refreshDataRate = Math.Max(1, Math.Min(newDataRate, refreshDataRate));
        }
        #endregion

        #region Internal Methods
        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="element"></param>
        /// <param name="seatID"></param>
        /// <param name="crewList"></param>
        /// <param name="crewMedical"></param>
        /// <returns></returns>
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
        /// Try to figure out which part on the craft is the current part.
        /// </summary>
        /// <returns></returns>
        private Part DeduceCurrentPart()
        {
            Part currentPart = null;

            if (JUtil.VesselIsInIVA(vessel))
            {
                foreach (Part thisPart in InternalModelParts(vessel))
                {
                    foreach (InternalSeat thatSeat in thisPart.internalModel.seats)
                    {
                        if (thatSeat.kerbalRef != null)
                        {
                            if (thatSeat.kerbalRef.eyeTransform == InternalCamera.Instance.transform.parent)
                            {
                                currentPart = thisPart;
                                break;
                            }
                        }
                    }

                    if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal)
                    {
                        foreach (Transform thisTransform in thisPart.internalModel.GetComponentsInChildren<Transform>())
                        {
                            if (thisTransform == InternalCamera.Instance.transform.parent)
                            {
                                currentPart = thisPart;
                                break;
                            }
                        }
                    }
                }
            }

            return currentPart;
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private static IEnumerable<Part> InternalModelParts(Vessel vessel)
        {
            foreach (Part thatPart in vessel.parts)
            {
                if (thatPart.internalModel != null)
                {
                    yield return thatPart;
                }
            }
        }

        /// <summary>
        /// Fetch altitude-related values
        /// </summary>
        private void FetchAltitudes()
        {
            CoM = vessel.CoM;
            altitudeASL = vessel.mainBody.GetAltitude(CoM);
            altitudeTrue = altitudeASL - vessel.terrainAltitude;
            // MOARdV notes - on a test ship (Mk1-2 pod on a FASA Gemini-based launch stack):
            // vessel.heightFromSurface appears to be -1 at all times.
            // vessel.heightFromTerrain, sometime around 12.5km ASL, goes to -1; otherwise, it's about 8m higher than altitudeTrue reports.
            //  which means ASL isn't computed from CoM in vessel?
            // vessel.altitude reports ~10.7m higher than altitudeASL (CoM) - so it may be that vessel altitude is based on the root part.
            // sfc.distance in the raycast below is likewise 10.7m below vessel.heightFromTerrain, although heightFromTerrain goes
            //  to -1 before the raycast starts failing.
            // vessel.pqsAltitude reports distance to the surface (effectively, altitudeTrue).
            RaycastHit sfc;
            if (Physics.Raycast(CoM, -up, out sfc, (float)altitudeASL + 10000.0F, 1 << 15))
            {
                slopeAngle = Vector3.Angle(up, sfc.normal);
                //JUtil.LogMessage(this, "sfc.distance = {0}, vessel.heightFromTerrain = {1}", sfc.distance, vessel.heightFromTerrain);
            }
            else
            {
                slopeAngle = -1.0f;
            }
            //JUtil.LogMessage(this, "vessel.altitude = {0}, vessel.pqsAltitude = {2}, altitudeASL = {1}", vessel.altitude, altitudeASL, vessel.pqsAltitude);

            float priorAltitudeBottom = (float)altitudeBottom;
            altitudeBottom = (vessel.mainBody.ocean) ? Math.Min(altitudeASL, altitudeTrue) : altitudeTrue;
            if (altitudeBottom < 500d)
            {
                double lowestPoint = altitudeASL;
                foreach (Part p in vessel.parts)
                {
                    if (p.collider != null)
                    {
                        Vector3d bottomPoint = p.collider.ClosestPointOnBounds(vessel.mainBody.position);
                        double partBottomAlt = vessel.mainBody.GetAltitude(bottomPoint);
                        lowestPoint = Math.Min(lowestPoint, partBottomAlt);
                    }
                }
                lowestPoint -= altitudeASL;
                altitudeBottom += lowestPoint;
            }
            altitudeBottom = Math.Max(0.0, altitudeBottom);

            float d1 = (float)altitudeBottom - priorAltitudeBottom;
            float t1 = (float)(Planetarium.GetUniversalTime() - lastRadarAltitudeTime);
            // simple exponential smoothing - radar altitude gets very noisy when terrain is hilly.
            const float alpha = 0.0625f;
            radarAltitudeRate = radarAltitudeRate * (1.0f - alpha) + (d1 / t1) * alpha;
            lastRadarAltitudeTime = Planetarium.GetUniversalTime();

            if (Planetarium.GetUniversalTime() >= lastAltitudeBottomSampleTime + 1.0)
            {
                terrainDelta = altitudeBottom - lastAltitudeBottom;
                lastAltitudeBottom = altitudeBottom;
                lastAltitudeBottomSampleTime = Planetarium.GetUniversalTime();
            }
        }

        /// <summary>
        /// Update all of the data that is part dependent (and thus requires iterating over the vessel)
        /// </summary>
        private void FetchPerPartData()
        {
            totalCurrentThrust = totalMaximumThrust = 0.0f;
            totalDataAmount = totalExperimentCount = 0.0f;
            heatShieldTemperature = heatShieldFlux = 0.0f;
            float hottestShield = float.MinValue;
            float totalResourceMass = 0.0f;

            float averageIspContribution = 0.0f;
            float maxIspContribution = 0.0f;

            anyEnginesOverheating = anyEnginesFlameout = false;

            resources.StartLoop(Planetarium.GetUniversalTime());

            foreach (Part thatPart in vessel.parts)
            {
                foreach (PartResource resource in thatPart.Resources)
                {
                    resources.Add(resource);
                }

                totalResourceMass += thatPart.GetResourceMass();

                foreach (PartModule pm in thatPart.Modules)
                {
                    if (!pm.isEnabled)
                    {
                        continue;
                    }

                    if (pm is ModuleEngines || pm is ModuleEnginesFX)
                    {
                        var thatEngineModule = pm as ModuleEngines;
                        anyEnginesOverheating |= (thatPart.skinTemperature / thatPart.skinMaxTemp > 0.9) || (thatPart.temperature / thatPart.maxTemp > 0.9);
                        anyEnginesFlameout |= (thatEngineModule.isActiveAndEnabled && thatEngineModule.flameout);

                        totalCurrentThrust += GetCurrentThrust(thatEngineModule);
                        float maxThrust = GetMaximumThrust(thatEngineModule);
                        totalMaximumThrust += maxThrust;
                        float realIsp = GetRealIsp(thatEngineModule);
                        if (realIsp > 0.0f)
                        {
                            averageIspContribution += maxThrust / realIsp;
                        }

                        foreach (Propellant thatResource in thatEngineModule.propellants)
                        {
                            resources.MarkPropellant(thatResource);
                        }

                        float minIsp, maxIsp;
                        thatEngineModule.atmosphereCurve.FindMinMaxValue(out minIsp, out maxIsp);
                        if (maxIsp > 0.0f)
                        {
                            maxIspContribution += maxThrust / maxIsp;
                        }
                    }
                    else if (pm is ModuleAblator)
                    {
                        var thatAblator = pm as ModuleAblator;

                        // Even though the interior contains a lot of heat, I think ablation is based on skin temp.
                        // Although it seems odd that the skin temp quickly cools off after re-entry, while the
                        // interior temp doesn't move cool much (for instance, I saw a peak ablator skin temp
                        // of 950K, while the interior eventually reached 345K after the ablator had cooled below
                        // 390K.  By the time the capsule landed, skin temp matched exterior temp (304K) but the
                        // interior still held 323K.
                        if (thatPart.skinTemperature - thatAblator.ablationTempThresh > hottestShield)
                        {
                            hottestShield = (float)(thatPart.skinTemperature - thatAblator.ablationTempThresh);
                            heatShieldTemperature = (float)(thatPart.skinTemperature);
                            heatShieldFlux = (float)(thatPart.thermalConvectionFlux + thatPart.thermalRadiationFlux);
                        }
                    }
                    //else if (pm is ModuleScienceExperiment)
                    //{
                    //    var thatExperiment = pm as ModuleScienceExperiment;
                    //    JUtil.LogMessage(this, "Experiment: {0} in {1} (action name {2}):", thatExperiment.experiment.experimentTitle, thatPart.partInfo.name, thatExperiment.experimentActionName);
                    //    JUtil.LogMessage(this, " - collection action {0}, collect warning {1}, is collectable {2}", thatExperiment.collectActionName, thatExperiment.collectWarningText, thatExperiment.dataIsCollectable);
                    //    JUtil.LogMessage(this, " - Inoperable {0}, resetActionName {1}, resettable {2}, reset on EVA {3}, review {4}", thatExperiment.Inoperable, thatExperiment.resetActionName, thatExperiment.resettable, thatExperiment.resettableOnEVA, thatExperiment.reviewActionName);
                    //}
                    //else if (pm is ModuleScienceContainer)
                    //{
                    //    var thatContainer = pm as ModuleScienceContainer;
                    //    JUtil.LogMessage(this, "Container: in {0}: allow repeats {1}, isCollectable {2}, isRecoverable {3}, isStorable {4}, evaOnlyStorage {5}", thatPart.partInfo.name,
                    //        thatContainer.allowRepeatedSubjects, thatContainer.dataIsCollectable, thatContainer.dataIsRecoverable, thatContainer.dataIsStorable, thatContainer.evaOnlyStorage);
                    //}
                }

                foreach (IScienceDataContainer container in thatPart.FindModulesImplementing<IScienceDataContainer>())
                {
                    foreach (ScienceData datapoint in container.GetData())
                    {
                        if (datapoint != null)
                        {
                            totalDataAmount += datapoint.dataAmount;
                            totalExperimentCount += 1.0f;
                        }
                    }
                }
            }

            totalShipWetMass = vessel.GetTotalMass();
            totalShipDryMass = totalShipWetMass - totalResourceMass;

            if (averageIspContribution > 0.0f)
            {
                actualAverageIsp = totalMaximumThrust / averageIspContribution;
            }
            else
            {
                actualAverageIsp = 0.0f;
            }

            if (maxIspContribution > 0.0f)
            {
                actualMaxIsp = totalMaximumThrust / maxIspContribution;
            }
            else
            {
                actualMaxIsp = 0.0f;
            }

            resources.GetActiveResourceNames(ref resourcesAlphabetic);

            // We can use the stock routines to get at the per-stage resources.
            foreach (Vessel.ActiveResource thatResource in vessel.GetActiveResources())
            {
                resources.SetActive(thatResource);
            }

            // MOARdV TODO: Migrate this to a callback system:
            // I seriously hope you don't have crew jumping in and out more than once per second.
            vesselCrew = vessel.GetVesselCrew();
            // The sneaky bit: This way we can get at their panic and whee values!
            if (vesselCrewMedical.Count != vesselCrew.Count)
            {
                vesselCrewMedical.Clear();
                for (int i = 0; i < vesselCrew.Count; i++)
                {
                    vesselCrewMedical.Add((vesselCrew[i].KerbalRef != null) ? vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>() : null);
                }
            }
            else
            {
                for (int i = 0; i < vesselCrew.Count; i++)
                {
                    vesselCrewMedical[i] = (vesselCrew[i].KerbalRef != null) ? vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>() : null;
                }
            }

            // Part-local list is assembled somewhat differently.
            // Mental note: Actually, there's a list of ProtoCrewMember in part.protoModuleCrew. 
            // But that list loses information about seats, which is what we'd like to keep in this particular case.
            if (part != null)
            {
                if (part.internalModel == null)
                {
                    JUtil.LogMessage(this, "Running on a part with no IVA, how did that happen?");
                }
                else
                {
                    if (localCrew.Count != part.internalModel.seats.Count)
                    {
                        localCrew.Clear();
                        localCrewMedical.Clear();
                        for (int i = 0; i < part.internalModel.seats.Count; i++)
                        {
                            localCrew.Add(part.internalModel.seats[i].crew);
                            localCrewMedical.Add((localCrew[i] == null) ? null : localCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>());
                        }
                    }
                    else
                    {
                        for (int i = 0; i < part.internalModel.seats.Count; i++)
                        {
                            localCrew[i] = part.internalModel.seats[i].crew;
                            localCrewMedical[i] = (localCrew[i]) == null ? null : localCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fetch data on any targets being tracked.
        /// </summary>
        private void FetchTargetData()
        {
            target = FlightGlobals.fetch.VesselTarget;

            if (target != null)
            {
                targetSeparation = vessel.GetTransform().position - target.GetTransform().position;
                targetOrientation = target.GetTransform().rotation;

                targetVessel = target as Vessel;
                targetBody = target as CelestialBody;
                targetDockingNode = target as ModuleDockingNode;

                targetDistance = Vector3.Distance(target.GetTransform().position, vessel.GetTransform().position);

                if (targetVessel != null || targetDockingNode != null)
                {
                    targetOrbitSensibility = JUtil.OrbitMakesSense(target.GetVessel());
                }
                else
                {
                    // All celestial bodies except the sun have orbits that make sense.
                    targetOrbitSensibility = targetBody != null && targetBody != Planetarium.fetch.Sun;
                }

                targetOrbit = targetOrbitSensibility ? target.GetOrbit() : null;

                // TODO: Actually, there's a lot of nonsensical cases here that need more reasonable handling.
                // Like what if we're targeting a vessel landed on a moon of another planet?...
                if (targetOrbit != null)
                {
                    velocityRelativeTarget = vessel.orbit.GetVel() - target.GetOrbit().GetVel();
                }
                else
                {
                    velocityRelativeTarget = vessel.orbit.GetVel();
                }

                // If our target is somehow our own celestial body, approach speed is equal to vertical speed.
                if (targetBody == vessel.mainBody)
                {
                    approachSpeed = (float)speedVertical;
                }
                else
                {
                    // In all other cases, that should work. I think.
                    approachSpeed = Vector3.Dot(velocityRelativeTarget, (target.GetTransform().position - vessel.GetTransform().position).normalized);
                }
            }
            else
            {
                velocityRelativeTarget = targetSeparation = Vector3d.zero;
                targetOrbit = null;
                targetDistance = 0.0;
                approachSpeed = 0.0f;
                targetBody = null;
                targetVessel = null;
                targetDockingNode = null;
                targetOrientation = vessel.GetTransform().rotation;
                targetOrbitSensibility = false;
            }
        }

        /// <summary>
        /// Update ship-wide data.
        /// </summary>
        private void FetchVesselData()
        {
            orbitSensibility = JUtil.OrbitMakesSense(vessel);

            localGeeASL = (float)(vessel.orbit.referenceBody.GeeASL * gee);
            localGeeDirect = (float)FlightGlobals.getGeeForceAtPosition(CoM).magnitude;

            speedVertical = vessel.verticalSpeed;
            speedVerticalRounded = Math.Ceiling(speedVertical * 20.0) / 20.0;
            speedHorizontal = Math.Sqrt(vessel.srfSpeed * vessel.srfSpeed - speedVertical * speedVertical);

            // Record the vessel-relative basis
            // north isn't actually used anywhere...
            right = vessel.GetTransform().right;
            forward = vessel.GetTransform().up;
            top = vessel.GetTransform().forward;

            //north = Vector3.ProjectOnPlane((vessel.mainBody.position + (Vector3d)vessel.mainBody.transform.up * vessel.mainBody.Radius) - CoM, up).normalized;
            // Generate the surface-relative basis (up, surfaceRight, surfaceForward)
            up = FlightGlobals.upAxis;
            surfaceForward = Vector3.Cross(up, right);
            // If the craft is rolled sharply to the side, we have to re-do our basis.
            if (surfaceForward.sqrMagnitude < 0.5f)
            {
                surfaceRight = Vector3.Cross(forward, up);
                surfaceForward = Vector3.Cross(up, surfaceRight);
            }
            else
            {
                surfaceRight = Vector3.Cross(surfaceForward, up);
            }

            rotationVesselSurface = Quaternion.Inverse(navBall.relativeGymbal);

            prograde = vessel.orbit.GetVel().normalized;
            radialOut = Vector3.ProjectOnPlane(up, prograde).normalized;
            normalPlus = -Vector3.Cross(radialOut, prograde).normalized;

            if (vessel.patchedConicSolver != null)
            {
                node = vessel.patchedConicSolver.maneuverNodes.Count > 0 ? vessel.patchedConicSolver.maneuverNodes[0] : null;
            }
            else
            {
                node = null;
            }
        }

        /// <summary>
        /// Get the current thrust of the engine
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        private static float GetCurrentThrust(ModuleEngines engine)
        {
            if (engine != null)
            {
                if ((!engine.EngineIgnited) || (!engine.isEnabled) || (!engine.isOperational))
                {
                    return 0.0f;
                }
                else
                {
                    return engine.finalThrust;
                }
            }
            else
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Creates a new PluginEvaluator object for the method supplied (if
        /// the method exists), attached to an IJSIModule.
        /// </summary>
        /// <param name="packedMethod"></param>
        /// <returns></returns>
        private PluginEvaluator GetInternalMethod(string packedMethod)
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
            PluginEvaluator pluginEval = null;
            if (jsiModule != null)
            {
                foreach (MethodInfo m in jsiModule.GetType().GetMethods())
                {
                    if (m.Name == tokens[1])
                    {
                        //JUtil.LogMessage(this, "Found method {1}: return type is {0}, IsStatic is {2}, with {3} parameters", m.ReturnType, tokens[1],m.IsStatic, m.GetParameters().Length);
                        ParameterInfo[] parms = m.GetParameters();
                        bool usesVessel = false;
                        if (parms.Length == 1)
                        {
                            if (parms[0].ParameterType == typeof(Vessel))
                            {
                                usesVessel = true;
                            }
                            else
                            {
                                JUtil.LogErrorMessage(this, "GetInternalMethod failed: unexpected first parameter in plugin method {0}", packedMethod);
                                return null;
                            }
                        }
                        else if (parms.Length > 1)
                        {
                            JUtil.LogErrorMessage(this, "GetInternalMethod failed: {1} parameters in plugin method {0}", packedMethod, parms.Length);
                            return null;
                        }

                        if (m.ReturnType == typeof(bool))
                        {
                            try
                            {
                                if (usesVessel)
                                {
                                    Delegate method = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<Vessel, bool>), m) : Delegate.CreateDelegate(typeof(Func<Vessel, bool>), jsiModule, m);
                                    pluginEval = new PluginBoolVessel(method);
                                }
                                else
                                {
                                    Delegate method = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<bool>), m) : Delegate.CreateDelegate(typeof(Func<bool>), jsiModule, m);
                                    pluginEval = new PluginBoolVoid(method);
                                }
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
                                if (usesVessel)
                                {
                                    Delegate method = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<Vessel, double>), m) : Delegate.CreateDelegate(typeof(Func<Vessel, double>), jsiModule, m);
                                    pluginEval = new PluginDoubleVessel(method);
                                }
                                else
                                {
                                    Delegate method = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<double>), m) : Delegate.CreateDelegate(typeof(Func<double>), jsiModule, m);
                                    pluginEval = new PluginDoubleVoid(method);
                                }
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
                                if (usesVessel)
                                {
                                    Delegate method = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<Vessel, string>), m) : Delegate.CreateDelegate(typeof(Func<Vessel, string>), jsiModule, m);
                                    pluginEval = new PluginStringVessel(method);
                                }
                                else
                                {
                                    Delegate method = (m.IsStatic) ? Delegate.CreateDelegate(typeof(Func<string>), m) : Delegate.CreateDelegate(typeof(Func<string>), jsiModule, m);
                                    pluginEval = new PluginStringVoid(method);
                                }
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
        /// Get the maximum thrust of the engine.
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        private static float GetMaximumThrust(ModuleEngines engine)
        {
            if (engine != null)
            {
                if ((!engine.EngineIgnited) || (!engine.isEnabled) || (!engine.isOperational))
                {
                    return 0.0f;
                }

                float vacISP = engine.atmosphereCurve.Evaluate(0.0f);
                float maxThrustAtAltitude = engine.maxThrust * engine.realIsp / vacISP;

                return maxThrustAtAltitude * (engine.thrustPercentage / 100.0f);
            }
            else
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Get the instantaneous ISP of the engine.
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        private static float GetRealIsp(ModuleEngines engine)
        {
            if (engine != null)
            {
                if ((!engine.EngineIgnited) || (!engine.isEnabled) || (!engine.isOperational))
                {
                    return 0.0f;
                }
                else
                {
                    return engine.realIsp;
                }
            }
            else
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Determines the pitch angle between the vector supplied and the front of the craft.
        /// Original code from FAR.
        /// </summary>
        /// <param name="normalizedVectorOfInterest">The normalized vector we want to measure</param>
        /// <returns>Pitch in degrees</returns>
        private double GetRelativePitch(Vector3 normalizedVectorOfInterest)
        {
            // vector projected onto a plane that divides the airplane into left and right halves
            Vector3 tmpVec = Vector3.ProjectOnPlane(normalizedVectorOfInterest, right);
            float dotpitch = Vector3.Dot(tmpVec.normalized, top);
            float pitch = Mathf.Rad2Deg * Mathf.Asin(dotpitch);
            if (float.IsNaN(pitch))
            {
                pitch = (dotpitch > 0.0f) ? 90.0f : -90.0f;
            }

            return pitch;
        }

        /// <summary>
        /// Determines the yaw angle between the vector supplied and the front of the craft.
        /// Original code from FAR, changed to Unity Vector3.Angle to provide the range 0-180.
        /// </summary>
        /// <param name="normalizedVectorOfInterest">The normalized vector we want to measure</param>
        /// <returns>Yaw in degrees</returns>
        private double GetRelativeYaw(Vector3 normalizedVectorOfInterest)
        {
            //velocity vector projected onto the vehicle-horizontal plane
            Vector3 tmpVec = Vector3.ProjectOnPlane(normalizedVectorOfInterest, top).normalized;
            float dotyaw = Vector3.Dot(tmpVec, right);
            float angle = Vector3.Angle(tmpVec, forward);

            if (dotyaw < 0.0f)
            {
                angle = -angle;
            }
            return angle;
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

        /// <summary>
        /// Returns a number identifying the next apsis type
        /// </summary>
        /// <returns></returns>
        private double NextApsisType()
        {
            if (orbitSensibility)
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
        /// Computes the estimated speed at impact based on the parameters supplied.
        /// </summary>
        /// <param name="thrust"></param>
        /// <param name="mass"></param>
        /// <param name="freeFall"></param>
        /// <param name="currentSpeed"></param>
        /// <param name="currentAltitude"></param>
        /// <returns></returns>
        private double SpeedAtImpact(float thrust)
        {
            float acceleration = localGeeASL - (thrust / totalShipWetMass);
            double timeToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2.0f * acceleration * altitudeTrue)) / acceleration;
            double speedAtImpact = speedVertical - acceleration * timeToImpact;
            if (double.IsNaN(speedAtImpact))
            {
                speedAtImpact = 0.0;
            }
            return speedAtImpact;
        }

        /// <summary>
        /// Estimates how long before a suicide burn needs to start in order to
        /// avoid crashing.
        /// </summary>
        /// <returns></returns>
        private double SuicideBurnCountdown()
        {
            Orbit orbit = vessel.orbit;
            if (orbit.PeA > 0.0) throw new ArgumentException("SuicideBurnCountdown: periapsis is above the ground");

            double angleFromHorizontal = 90 - Vector3d.Angle(-vessel.srf_velocity, up);
            angleFromHorizontal = JUtil.Clamp(angleFromHorizontal, 0.0, 90.0);
            double sine = Math.Sin(angleFromHorizontal * Math.PI / 180.0);
            double g = localGeeDirect;
            double T = totalMaximumThrust / totalShipWetMass;
            double decelTerm = (2.0 * g * sine) * (2.0 * g * sine) + 4.0 * (T * T - g * g);
            if (decelTerm < 0.0)
            {
                return double.NaN;
            }

            double effectiveDecel = 0.5 * (-2.0 * g * sine + Math.Sqrt(decelTerm));
            double decelTime = speedHorizontal / effectiveDecel;

            Vector3d estimatedLandingSite = CoM + 0.5 * decelTime * vessel.srf_velocity;
            double terrainRadius = vessel.mainBody.Radius + vessel.mainBody.TerrainAltitude(estimatedLandingSite);
            double impactTime = 0;
            try
            {
                impactTime = orbit.NextTimeOfRadius(Planetarium.GetUniversalTime(), terrainRadius);
            }
            catch (ArgumentException)
            {
                return double.NaN;
            }
            return impactTime - decelTime / 2.0 - Planetarium.GetUniversalTime();
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
            if (Vector3d.Dot(-target.GetOrbitNormal().Reorder(132).normalized, launchBody.angularVelocity) < 0) targetAngularRate *= -1; //retrograde target

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
        /// Determines if enough screen updates have passed to trigger another data update.
        /// </summary>
        /// <returns>true if it's time to update things</returns>
        private bool UpdateCheck()
        {
            if (--dataUpdateCountdown < 0)
            {
                dataUpdateCountdown = refreshDataRate;
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        //--- Callbacks for registered GameEvent
        #region GameEvent Callbacks
        private void LoadSceneCallback(GameScenes data)
        {
            //JUtil.LogMessage(this, "onGameSceneLoadRequested({0}), active vessel is {1}", data, vessel.vesselName);

            // Are we leaving Flight?  If so, let's get rid of all of the tables we've created.
            if (data != GameScenes.FLIGHT && customVariables != null)
            {
                customVariables = null;
                knownLoadedAssemblies = null;
                mappedVariables = null;
                systemNamedResources = null;
                triggeredEvents = null;

                protractor = null;

                IJSIModule.vessel = null;
                installedModules = null;

                VariableOrNumber.Clear();
            }
        }

        private void PartCoupleCallback(GameEvents.FromToAction<Part, Part> action)
        {
            if (action.from.vessel == vessel || action.to.vessel == vessel)
            {
                //JUtil.LogMessage(this, "onPartCouple(), I am {0} ({1} and {2} are docking)", vessel.vesselName, action.from.vessel.vesselName, action.to.vessel.vesselName);
                timeToUpdate = true;
            }
        }

        private void StageActivateCallback(int stage)
        {
            if (JUtil.IsActiveVessel(vessel))
            {
                //JUtil.LogMessage(this, "onStageActivate({0}), active vessel is {1}", stage, vessel.vesselName);
                timeToUpdate = true;
            }
        }

        private void UndockCallback(EventReport report)
        {
            if (JUtil.IsActiveVessel(vessel))
            {
                //JUtil.LogMessage(this, "onUndock({1}), I am {0}", vessel.vesselName, report.eventType);
                timeToUpdate = true;
            }
        }

        private void VesselChangeCallback(Vessel v)
        {
            if (v.id == vessel.id)
            {
                //JUtil.LogMessage(this, "onVesselChange({0}), I am {1}, so I am becoming active", v.vesselName, vessel.vesselName);
                timeToUpdate = true;
                resultCache.Clear();
                IJSIModule.vessel = vessel;
            }
        }

        private void VesselModifiedCallback(Vessel v)
        {
            if (v.id == vessel.id && JUtil.IsActiveVessel(vessel))
            {
                //JUtil.LogMessage(this, "onVesselModified({0}), I am {1}, so I am modified", v.vesselName, vessel.vesselName);
                timeToUpdate = true;
            }
        }
        #endregion

        private class ResourceNameLengthComparer : IComparer<String>
        {
            public int Compare(string x, string y)
            {
                // Note that we need longer strings first so we invert numbers.
                int lengthComparison = -x.Length.CompareTo(y.Length);
                return lengthComparison == 0 ? -string.Compare(x, y, StringComparison.Ordinal) : lengthComparison;
            }
        }
    }
}
