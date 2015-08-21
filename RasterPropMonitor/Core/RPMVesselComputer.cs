//#define HACK_IN_A_NAVPOINT
//#define SHOW_FIXEDUPDATE_TIMING
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
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
    public class RPMVesselComputer : VesselModule
    {
        #region Static Variables
        /*
         * This region contains static variables - variables that only need to
         * exist in a single instance.  They are instantiated by the first
         * vessel to enter flight, and released by the last vessel before a
         * scene change.
         */
        private static Dictionary<Vessel, RPMVesselComputer> instances;

        private static Dictionary<string, CustomVariable> customVariables;
        private static List<string> knownLoadedAssemblies;
        private static Dictionary<string, MappedVariable> mappedVariables;
        private static SortedDictionary<string, string> systemNamedResources;
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
        private const float KelvinToCelsius = -273.15f;
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
        private ExternalVariableHandlers plugins;

        // Data refresh
        private int dataUpdateCountdown;
        private int refreshDataRate = 60;
        private bool timeToUpdate = false;

        // Processing cache!
        private readonly DefaultableDictionary<string, object> resultCache = new DefaultableDictionary<string, object>(null);

        private Dictionary<string, Func<bool>> pluginBoolVariables = new Dictionary<string, Func<bool>>();
        private Dictionary<string, Func<double>> pluginDoubleVariables = new Dictionary<string, Func<double>>();

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
        private string[] resourcesAlphabetic;
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

        private ProtoCrewMember[] vesselCrew;
        private kerbalExpressionSystem[] vesselCrewMedical;
        private ProtoCrewMember[] localCrew;
        private kerbalExpressionSystem[] localCrewMedical;

        private double lastTimePerSecond;
        private double lastTerrainHeight, terrainDelta;

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
        private double approachSpeed;
        private Quaternion targetOrientation;

        // Plugin-modifiable Evaluators
        private Func<bool> evaluateMechJebAvailable;
        private Func<double> evaluateAngleOfAttack;
        private Func<double> evaluateDeltaV;
        private Func<double> evaluateDeltaVStage;
        private Func<double> evaluateDynamicPressure;
        private Func<double> evaluateLandingError;
        private Func<double> evaluateLandingAltitude;
        private Func<double> evaluateLandingLatitude;
        private Func<double> evaluateLandingLongitude;
        private Func<double> evaluateSideSlip;
        private Func<double> evaluateTerminalVelocity;

        // Diagnostics
#if SHOW_FIXEDUPDATE_TIMING
        private Stopwatch stopwatch = new Stopwatch();
#endif
        #endregion

        public static RPMVesselComputer Instance(Vessel v)
        {
            if (instances == null)
            {
                JUtil.LogErrorMessage(null, "Computer.Instance called with uninitialized insances.");
                return null;
            }
            if (!instances.ContainsKey(v))
            {
                JUtil.LogErrorMessage(null, "Computer.Instance called with unrecognized vessel {0}.", v.vesselName);
                throw new Exception("Computer.Instance called with an unrecognized vessel");
                //return null;
            }

            return instances[v];
        }

        #region VesselModule Overrides
        public void Awake()
        {
            if (instances == null)
            {
                JUtil.LogMessage(this, "Initializing RPM version {0}", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
                instances = new Dictionary<Vessel, RPMVesselComputer>();
            }
            if (protractor == null)
            {
                protractor = new Protractor();
            }
            // MOARdV TODO: Only add this instance to the library if there is
            // crew capacity.  Probes should not apply.  Except, what about docking?
            vessel = GetComponent<Vessel>();
            if(vessel == null)
            {
                throw new Exception("RPMVesselComputer: GetComponent<Vessel>() returned null");
            }

            if (instances.ContainsKey(vessel))
            {
                JUtil.LogErrorMessage(this, "Awake for vessel {0}, but it's already in the dictionary.", (vessel == null) ? "null" : vessel.vesselName);
            }
            instances.Add(vessel, this);

            JUtil.LogMessage(this, "Awake for vessel {0}.", (vessel == null) ? "null" : vessel.vesselName);
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

            if(installedModules == null)
            {
                 installedModules = new List<IJSIModule>();

                 installedModules.Add(new JSIParachute(vessel));
                 installedModules.Add(new JSIMechJeb(vessel));
                 installedModules.Add(new JSIInternalRPMButtons(vessel));
                 installedModules.Add(new JSIGimbal(vessel));
                 installedModules.Add(new JSIFAR(vessel));
            }
        }

        public void Start()
        {
            JUtil.LogMessage(this, "Start for vessel {0}", (vessel == null) ? "null" : vessel.vesselName);
            navBall = FlightUIController.fetch.GetComponentInChildren<NavBall>();
            if (standardAtmosphere < 0.0)
            {
                standardAtmosphere = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(0, FlightGlobals.Bodies[1]), FlightGlobals.Bodies[1].atmosphereTemperatureSeaLevel);
            }

            if (JUtil.IsActiveVessel(vessel))
            {
                for (int i = 0; i < installedModules.Count; ++i)
                {
                    installedModules[i].Invalidate(vessel);
                }

                FetchPerPartData();
                FetchAltitudes();
                FetchVesselData();
                FetchTargetData();
            }
        }

        public void OnDestroy()
        {
            JUtil.LogMessage(this, "OnDestroy for vessel {0}", (vessel == null) ? "null" : vessel.vesselName);
            GameEvents.onGameSceneLoadRequested.Remove(LoadSceneCallback);
            GameEvents.onVesselChange.Remove(VesselChangeCallback);
            GameEvents.onStageActivate.Remove(StageActivateCallback);
            GameEvents.onUndock.Remove(UndockCallback);
            GameEvents.onVesselWasModified.Remove(VesselModifiedCallback);

            if (!instances.ContainsKey(vessel))
            {
                JUtil.LogErrorMessage(this, "OnDestroy for vessel {0}, but it's not in the dictionary.", (vessel == null) ? "null" : vessel.vesselName);
            }
            else
            {
                instances.Remove(vessel);
            }

            resultCache.Clear();

            vessel = null;
            navBall = null;
            node = null;
            part = null;

            pluginBoolVariables = null;
            pluginDoubleVariables = null;

            target = null;
            targetDockingNode = null;
            targetVessel = null;
            targetOrbit = null;
            targetBody = null;

            resources = null;
            resourcesAlphabetic = null;
            vesselCrew = null;
            vesselCrewMedical = null;
            localCrew = null;
            localCrewMedical = null;

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
            // FixedUpdate tracks values related to the vessel (position, CoM, etc)
            // MOARdV TODO: FixedUpdate only if in IVA?  What about transparent pods?
            if (JUtil.VesselIsInIVA(vessel) && timeToUpdate)
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
                }

#if SHOW_FIXEDUPDATE_TIMING
                long newPart = stopwatch.ElapsedMilliseconds;
#endif

                timeToUpdate = false;
                resultCache.Clear();

                for (int i = 0; i < installedModules.Count; ++i)
                {
                    installedModules[i].Invalidate(vessel);
                }
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
        public object ProcessVariable(string input, PersistenceAccessor persistence)
        {
            object returnValue = resultCache[input];
            if (returnValue == null)
            {
                bool cacheable;
                try
                {
                    if (plugins == null || !plugins.ProcessVariable(input, out returnValue, out cacheable))
                    {
                        returnValue = VariableToObject(input, persistence, out cacheable);
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
            else
            {
                JUtil.LogMessage(this, "Not in IVA");
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
        /// Estimates the number of seconds before impact.  It's not precise,
        /// since precise is also computationally expensive.
        /// </summary>
        /// <returns></returns>
        private double EstimateSecondsToImpact()
        {
            double secondsToImpact;
            if (vessel.situation == Vessel.Situations.SUB_ORBITAL || vessel.situation == Vessel.Situations.FLYING)
            {
                // Mental note: the local g taken from vessel.mainBody.GeeASL will suffice.
                //  t = (v+sqrt(v²+2gd))/g or something.

                // What is the vertical component of current acceleration?
                double accelUp = Vector3d.Dot(vessel.acceleration, up);

                double altitude = altitudeTrue;
                if (vessel.mainBody.ocean && altitudeASL > 0.0)
                {
                    // AltitudeTrue shows distance above the floor of the ocean,
                    // so use ASL if it's closer in this case, and we're not
                    // already below SL.
                    altitude = Math.Min(altitudeASL, altitudeTrue);
                }

                if (accelUp < 0.0 || speedVertical >= 0.0 || Planetarium.TimeScale > 1.0)
                {
                    // If accelUp is negative, we can't use it in the general
                    // equation for finding time to impact, since it could
                    // make the term inside the sqrt go negative.
                    // If we're going up, we can use this as well, since
                    // the precision is not critical.
                    // If we are warping, accelUp is always zero, so if we
                    // do not use this case, we would fall to the simple
                    // formula, which is wrong.
                    secondsToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * localGeeASL * altitude)) / localGeeASL;
                }
                else if (accelUp > 0.005)
                {
                    // This general case takes into account vessel acceleration,
                    // so estimates on craft that include parachutes or do
                    // powered descents are more accurate.
                    secondsToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * accelUp * altitude)) / accelUp;
                }
                else
                {
                    // If accelUp is small, we get floating point precision
                    // errors that tend to make secondsToImpact get really big.
                    secondsToImpact = altitude / -speedVertical;
                }
            }
            else
            {
                secondsToImpact = Double.NaN;
            }

            return secondsToImpact;
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
        }

        /// <summary>
        /// Update all of the data that is part dependent (and thus requires iterating over the vessel)
        /// </summary>
        private void FetchPerPartData()
        {
            totalShipDryMass = totalShipWetMass = 0.0f;
            totalCurrentThrust = totalMaximumThrust = 0.0f;
            totalDataAmount = totalExperimentCount = 0.0f;
            heatShieldTemperature = heatShieldFlux = 0.0f;
            float hottestShield = float.MinValue;

            float averageIspContribution = 0.0f;

            anyEnginesOverheating = anyEnginesFlameout = false;

            resources.StartLoop(Planetarium.GetUniversalTime());

            foreach (Part thatPart in vessel.parts)
            {
                foreach (PartResource resource in thatPart.Resources)
                {
                    resources.Add(resource);
                }

                if (thatPart.physicalSignificance != Part.PhysicalSignificance.NONE)
                {
                    totalShipDryMass += thatPart.mass;
                    totalShipWetMass += thatPart.mass;
                }

                totalShipWetMass += thatPart.GetResourceMass();

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
                    //else if(pm is ModuleParachute)
                    //{
                    //    var thatParachute = pm as ModuleParachute;

                    //    JUtil.LogMessage(this, "ModuleParachute.deploySafe is {0}", thatParachute.deploySafe);
                    //}
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

            if (averageIspContribution > 0.0f)
            {
                actualAverageIsp = totalMaximumThrust / averageIspContribution;
            }
            else
            {
                actualAverageIsp = 0.0f;
            }

            resourcesAlphabetic = resources.Alphabetic();

            // We can use the stock routines to get at the per-stage resources.
            foreach (Vessel.ActiveResource thatResource in vessel.GetActiveResources())
            {
                resources.SetActive(thatResource);
            }

            // I seriously hope you don't have crew jumping in and out more than once per second.
            vesselCrew = (vessel.GetVesselCrew()).ToArray();
            // The sneaky bit: This way we can get at their panic and whee values!
            vesselCrewMedical = new kerbalExpressionSystem[vesselCrew.Length];
            for (int i = 0; i < vesselCrew.Length; i++)
            {
                vesselCrewMedical[i] = (vesselCrew[i].KerbalRef != null) ? vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>() : null;
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
                    localCrew = new ProtoCrewMember[part.internalModel.seats.Count];
                    localCrewMedical = new kerbalExpressionSystem[localCrew.Length];
                    for (int i = 0; i < part.internalModel.seats.Count; i++)
                    {
                        localCrew[i] = part.internalModel.seats[i].crew;
                        localCrewMedical[i] = localCrew[i] == null ? null : localCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>();
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
                    approachSpeed = speedVertical;
                }
                else
                {
                    // In all other cases, that should work. I think.
                    approachSpeed = Vector3d.Dot(velocityRelativeTarget, (target.GetTransform().position - vessel.GetTransform().position).normalized);
                }
            }
            else
            {
                velocityRelativeTarget = targetSeparation = Vector3d.zero;
                targetOrbit = null;
                targetDistance = 0.0;
                approachSpeed = 0.0;
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
#if HACK_IN_A_NAVPOINT
            //--- MOARdV: Keeping this hack around since I don't have a career
            // game with waypoints to use for reference.
            if(FinePrint.WaypointManager.navIsActive() == false)
            {
                double lat = vessel.mainBody.GetLatitude(coM) + 0.1;
                double lon = vessel.mainBody.GetLongitude(coM) + 0.05;
                FinePrint.WaypointManager.navWaypoint.SetupNavWaypoint(vessel.mainBody, lat, lon, 1000.0, "Squad/Contracts/Icons/seismic", Color.blue);
                FinePrint.WaypointManager.activateNavPoint();
            }
#endif
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

            if (Planetarium.GetUniversalTime() >= lastTimePerSecond + 1.0)
            {
                terrainDelta = vessel.terrainAltitude - lastTerrainHeight;
                lastTerrainHeight = vessel.terrainAltitude;
                lastTimePerSecond = Planetarium.GetUniversalTime();
            }

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
        /// Get an internal method (one that is built into an IJSIModule)
        /// </summary>
        /// <param name="packedMethod"></param>
        /// <param name="delegateType"></param>
        /// <returns></returns>
        private Delegate GetInternalMethod(string packedMethod, Type delegateType)
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
                        stateCall = Delegate.CreateDelegate(delegateType, jsiModule, m);
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
        /// Original code from FAR.
        /// </summary>
        /// <param name="normalizedVectorOfInterest">The normalized vector we want to measure</param>
        /// <returns>Yaw in degrees</returns>
        private double GetRelativeYaw(Vector3 normalizedVectorOfInterest)
        {
            //velocity vector projected onto the vehicle-horizontal plane
            Vector3 tmpVec = Vector3.ProjectOnPlane(normalizedVectorOfInterest, top);
            float dotyaw = Vector3.Dot(tmpVec.normalized, right);
            float yaw = Mathf.Rad2Deg * Mathf.Asin(dotyaw);
            if (float.IsNaN(yaw))
            {
                yaw = (dotyaw > 0.0f) ? 90.0f : -90.0f;
            }

            return yaw;
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
        
        //--- The guts of the variable processor
        #region VariableToObject
        /// <summary>
        /// The core of the variable processor.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="propId"></param>
        /// <param name="cacheable"></param>
        /// <returns></returns>
        private object VariableToObject(string input, PersistenceAccessor persistence, out bool cacheable)
        {
            // Some variables may not cacheable, because they're meant to be different every time like RANDOM,
            // or immediate. they will set this flag to false.
            cacheable = true;

            // It's slightly more optimal if we take care of that before the main switch body.
            if (input.IndexOf("_", StringComparison.Ordinal) > -1)
            {
                string[] tokens = input.Split('_');

                // If input starts with ISLOADED, this is a query on whether a specific DLL has been loaded into the system.
                // So we look it up in our list.
                if (tokens.Length == 2 && tokens[0] == "ISLOADED")
                {
                    return knownLoadedAssemblies.Contains(tokens[1]) ? 1d : 0d;
                }

                // If input starts with SYSR, this is a named system resource which we should recognise and return.
                // The qualifier rules did not change since individually named resources got deprecated.
                if (tokens.Length == 2 && tokens[0] == "SYSR")
                {
                    foreach (KeyValuePair<string, string> resourceType in RPMVesselComputer.systemNamedResources)
                    {
                        if (tokens[1].StartsWith(resourceType.Key, StringComparison.Ordinal))
                        {
                            string argument = tokens[1].Substring(resourceType.Key.Length);
                            if (argument.StartsWith("STAGE", StringComparison.Ordinal))
                            {
                                argument = argument.Substring("STAGE".Length);
                                return resources.ListElement(resourceType.Value, argument, true);
                            }
                            return resources.ListElement(resourceType.Value, argument, false);
                        }
                    }
                }

                // If input starts with "LISTR" we're handling it specially -- it's a list of all resources.
                // The variables are named like LISTR_<number>_<NAME|VAL|MAX>
                if (tokens.Length == 3 && tokens[0] == "LISTR")
                {
                    ushort resourceID = Convert.ToUInt16(tokens[1]);
                    if (tokens[2] == "NAME")
                    {
                        return resourceID >= resourcesAlphabetic.Length ? string.Empty : resourcesAlphabetic[resourceID];
                    }
                    if (resourceID >= resourcesAlphabetic.Length)
                        return 0d;
                    return tokens[2].StartsWith("STAGE", StringComparison.Ordinal) ?
                        resources.ListElement(resourcesAlphabetic[resourceID], tokens[2].Substring("STAGE".Length), true) :
                        resources.ListElement(resourcesAlphabetic[resourceID], tokens[2], false);
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
                            double invPeriod = 1.0 / period;

                            double remainder = Planetarium.GetUniversalTime() % invPeriod;

                            return (remainder > invPeriod * 0.5).GetHashCode();
                        }
                    }

                    return input;
                }

                // Custom variables - if the first token is CUSTOM, we'll evaluate it here
                if (tokens.Length > 1 && tokens[0] == "CUSTOM")
                {
                    if (customVariables.ContainsKey(input))
                    {
                        return customVariables[input].Evaluate(this, persistence);
                    }
                    else
                    {
                        return input;
                    }
                }

                // Mapped variables - if the first token is MAPPED, we'll evaluate it here
                if (tokens.Length > 1 && tokens[0] == "MAPPED")
                {
                    if (mappedVariables.ContainsKey(input))
                    {
                        return mappedVariables[input].Evaluate(this, persistence);
                    }
                    else
                    {
                        return input;
                    }
                }

                // Plugin variables.  Let's get crazy!
                if (tokens.Length == 2 && tokens[0] == "PLUGIN")
                {
                    if (pluginBoolVariables.ContainsKey(tokens[1]))
                    {
                        Func<bool> pluginCall = pluginBoolVariables[tokens[1]];
                        if (pluginCall != null)
                        {
                            return pluginCall().GetHashCode();
                        }
                        else
                        {
                            return -1;
                        }
                    }
                    else if (pluginDoubleVariables.ContainsKey(tokens[1]))
                    {
                        Func<double> pluginCall = pluginDoubleVariables[tokens[1]];
                        if (pluginCall != null)
                        {
                            return pluginCall();
                        }
                        else
                        {
                            return double.NaN;
                        }
                    }
                    else
                    {
                        string[] internalModule = tokens[1].Split(':');
                        if (internalModule.Length != 2)
                        {
                            JUtil.LogErrorMessage(this, "Badly-formed plugin name in {0}", input);
                            return input;
                        }

                        InternalProp propToUse = null;
                        if (part != null)
                        {
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
                        }
                        if (propToUse == null && persistence != null && persistence.prop != null)
                        {
                            //if (part.internalModel.props.Count == 0)
                            //{
                            //    JUtil.LogErrorMessage(this, "How did RPM get invoked in an IVA with no props?");
                            //    pluginBoolVariables.Add(tokens[1], null);
                            //    return float.NaN;
                            //}

                            //if (persistence.prop != null)
                            {
                                propToUse = persistence.prop;
                            }
                            //else
                            //{
                            //    propToUse = part.internalModel.props[0];
                            //}
                        }

                        Func<bool> pluginCall = (Func<bool>)GetMethod(tokens[1], propToUse, typeof(Func<bool>));
                        if (pluginCall == null)
                        {
                            Func<double> pluginNumericCall = (Func<double>)GetMethod(tokens[1], propToUse, typeof(Func<double>));

                            if (pluginNumericCall != null)
                            {
                                JUtil.LogMessage(this, "Adding {0} as a Func<double>", tokens[1]);
                                pluginDoubleVariables.Add(tokens[1], pluginNumericCall);
                                return pluginNumericCall();
                            }
                            else
                            {
                                // Only register the plugin variable as unavailable if we were called with persistence
                                if (propToUse == null)
                                {
                                    JUtil.LogErrorMessage(this, "Tried to look for method with propToUse still null?");
                                    pluginBoolVariables.Add(tokens[1], null);
                                }
                                return -1;
                            }
                        }
                        else
                        {
                            JUtil.LogMessage(this, "Adding {0} as a Func<bool>", tokens[1]);
                            pluginBoolVariables.Add(tokens[1], pluginCall);

                            return pluginCall().GetHashCode();
                        }
                    }
                }

                if (tokens.Length > 1 && tokens[0] == "PROP")
                {
                    string substr = input.Substring("PROP".Length + 1);

                    if (persistence != null && persistence.HasPropVar(substr))
                    {
                        // Can't cache - multiple props could call in here.
                        cacheable = false;
                        return (float)persistence.GetPropVar(substr);
                    }
                    else
                    {
                        return input;
                    }
                }

                if (tokens.Length > 1 && tokens[0] == "PERSISTENT")
                {
                    string substr = input.Substring("PERSISTENT".Length + 1);

                    if (persistence != null && persistence.HasVar(substr))
                    {
                        // MOARdV TODO: Can this be cacheable?  Should only have one
                        // active part at a time, so I think it's safe.
                        return (float)persistence.GetVar(substr);
                    }
                    else
                    {
                        return -1.0f;
                    }
                }

                // We do similar things for crew rosters.
                // The syntax is therefore CREW_<index>_<FIRST|LAST|FULL>
                // Part-local crew list is identical but CREWLOCAL_.
                if (tokens.Length == 3)
                {
                    ushort crewSeatID = Convert.ToUInt16(tokens[1]);
                    switch (tokens[0])
                    {
                        case "CREW":
                            return CrewListElement(tokens[2], crewSeatID, vesselCrew, vesselCrewMedical);
                        case "CREWLOCAL":
                            return CrewListElement(tokens[2], crewSeatID, localCrew, localCrewMedical);
                    }
                }

                // Strings stored in module configuration.
                if (tokens.Length == 2 && tokens[0] == "STOREDSTRING")
                {
                    int storedStringNumber;
                    if (persistence != null && int.TryParse(tokens[1], out storedStringNumber) && storedStringNumber >= 0)
                    {
                        return persistence.GetStoredString(storedStringNumber);
                    }
                    return "";
                }
            }

            if (input.StartsWith("AGMEMO", StringComparison.Ordinal))
            {
                uint groupID;
                if (uint.TryParse(input.Substring(6), out groupID) && groupID < 10)
                {
                    string[] tokens;
                    if (actionGroupMemo[groupID].IndexOf('|') > 1 && (tokens = actionGroupMemo[groupID].Split('|')).Length == 2)
                    {
                        if (vessel.ActionGroups.groups[RPMVesselComputer.actionGroupID[groupID]])
                            return tokens[0];
                        return tokens[1];
                    }
                    return actionGroupMemo[groupID];
                }
                return input;
            }
            // Action group state.
            if (input.StartsWith("AGSTATE", StringComparison.Ordinal))
            {
                uint groupID;
                if (uint.TryParse(input.Substring(7), out groupID) && groupID < 10)
                {
                    return (vessel.ActionGroups.groups[actionGroupID[groupID]]).GetHashCode();
                }
                return input;
            }

            switch (input)
            {

                // It's a bit crude, but it's simple enough to populate.
                // Would be a bit smoother if I had eval() :)

                // Speeds.
                case "VERTSPEED":
                    return speedVertical;
                case "VERTSPEEDLOG10":
                    return JUtil.PseudoLog10(speedVertical);
                case "VERTSPEEDROUNDED":
                    return speedVerticalRounded;
                case "TERMINALVELOCITY":
                    return TerminalVelocity();
                case "SURFSPEED":
                    return vessel.srfSpeed;
                case "SURFSPEEDMACH":
                    // Mach number wiggles around 1e-7 when sitting in launch
                    // clamps before launch, so pull it down to zero if it's close.
                    return (vessel.mach < 0.001) ? 0.0 : vessel.mach;
                case "ORBTSPEED":
                    return vessel.orbit.GetVel().magnitude;
                case "TRGTSPEED":
                    return velocityRelativeTarget.magnitude;
                case "HORZVELOCITY":
                    return speedHorizontal;
                case "HORZVELOCITYFORWARD":
                    // Negate it, since this is actually movement on the Z axis,
                    // and we want to treat it as a 2D projection on the surface
                    // such that moving "forward" has a positive value.
                    return -Vector3d.Dot(vessel.srf_velocity, surfaceForward);
                case "HORZVELOCITYRIGHT":
                    return Vector3d.Dot(vessel.srf_velocity, surfaceRight);
                case "EASPEED":
                    return vessel.srfSpeed * Math.Sqrt(vessel.atmDensity / standardAtmosphere);
                case "APPROACHSPEED":
                    return approachSpeed;
                case "SELECTEDSPEED":
                    switch (FlightUIController.speedDisplayMode)
                    {
                        case FlightUIController.SpeedDisplayModes.Orbit:
                            return vessel.orbit.GetVel().magnitude;
                        case FlightUIController.SpeedDisplayModes.Surface:
                            return vessel.srfSpeed;
                        case FlightUIController.SpeedDisplayModes.Target:
                            return velocityRelativeTarget.magnitude;
                    }
                    return double.NaN;


                case "TGTRELX":
                    if (target != null)
                    {
                        return Vector3d.Dot(FlightGlobals.ship_tgtVelocity, FlightGlobals.ActiveVessel.ReferenceTransform.right);
                    }
                    else
                    {
                        return 0.0;
                    }

                case "TGTRELY":
                    if (target != null)
                    {
                        return Vector3d.Dot(FlightGlobals.ship_tgtVelocity, FlightGlobals.ActiveVessel.ReferenceTransform.forward);
                    }
                    else
                    {
                        return 0.0;
                    }
                case "TGTRELZ":
                    if (target != null)
                    {
                        return Vector3d.Dot(FlightGlobals.ship_tgtVelocity, FlightGlobals.ActiveVessel.ReferenceTransform.up);
                    }
                    else
                    {
                        return 0.0;
                    }

                // Time to impact. This is quite imprecise, because a precise calculation pulls in pages upon pages of MechJeb code.
                // It accounts for gravity now, though. Pull requests welcome.
                case "TIMETOIMPACTSECS":
                    {
                        double secondsToImpact = EstimateSecondsToImpact();
                        return (double.IsNaN(secondsToImpact) || secondsToImpact > 365.0 * 24.0 * 60.0 * 60.0 || secondsToImpact < 0.0) ? -1.0 : secondsToImpact;
                    }
                case "SPEEDATIMPACT":
                    return SpeedAtImpact(totalCurrentThrust);
                case "BESTSPEEDATIMPACT":
                    return SpeedAtImpact(totalMaximumThrust);
                case "SUICIDEBURNSTARTSECS":
                    if (vessel.orbit.PeA > 0.0)
                    {
                        return double.NaN;
                    }
                    return SuicideBurnCountdown();

                case "LATERALBRAKEDISTANCE":
                    // (-(SHIP:SURFACESPEED)^2)/(2*(ship:maxthrust/ship:mass)) 
                    if (totalMaximumThrust <= 0.0)
                    {
                        // It should be impossible for wet mass to be zero.
                        return -1.0;
                    }
                    return (speedHorizontal * speedHorizontal) / (2.0 * totalMaximumThrust / totalShipWetMass);

                // Altitudes
                case "ALTITUDE":
                    return altitudeASL;
                case "ALTITUDELOG10":
                    return JUtil.PseudoLog10(altitudeASL);
                case "RADARALT":
                    return altitudeTrue;
                case "RADARALTLOG10":
                    return JUtil.PseudoLog10(altitudeTrue);
                case "RADARALTOCEAN":
                    if (vessel.mainBody.ocean)
                    {
                        return Math.Min(altitudeASL, altitudeTrue);
                    }
                    return altitudeTrue;
                case "RADARALTOCEANLOG10":
                    if (vessel.mainBody.ocean)
                    {
                        return JUtil.PseudoLog10(Math.Min(altitudeASL, altitudeTrue));
                    }
                    return JUtil.PseudoLog10(altitudeTrue);
                case "ALTITUDEBOTTOM":
                    return altitudeBottom;
                case "ALTITUDEBOTTOMLOG10":
                    return JUtil.PseudoLog10(altitudeBottom);
                case "TERRAINHEIGHT":
                    return vessel.terrainAltitude;
                case "TERRAINDELTA":
                    return terrainDelta;
                case "TERRAINHEIGHTLOG10":
                    return JUtil.PseudoLog10(vessel.terrainAltitude);
                case "DISTTOATMOSPHERETOP":
                    return vessel.orbit.referenceBody.atmosphereDepth - altitudeASL;

                // Atmospheric values
                case "ATMPRESSURE":
                    return vessel.staticPressurekPa * PhysicsGlobals.KpaToAtmospheres;
                case "ATMDENSITY":
                    return vessel.atmDensity;
                case "DYNAMICPRESSURE":
                    return DynamicPressure();
                case "ATMOSPHEREDEPTH":
                    if (vessel.mainBody.atmosphere)
                    {
                        return ((upperAtmosphereLimit + Math.Log(FlightGlobals.getAtmDensity(vessel.staticPressurekPa * PhysicsGlobals.KpaToAtmospheres, FlightGlobals.Bodies[1].atmosphereTemperatureSeaLevel) /
                        FlightGlobals.getAtmDensity(FlightGlobals.currentMainBody.atmospherePressureSeaLevel, FlightGlobals.currentMainBody.atmosphereTemperatureSeaLevel))) / upperAtmosphereLimit).Clamp(0d, 1d);
                    }
                    return 0d;

                // Masses.
                case "MASSDRY":
                    return totalShipDryMass;
                case "MASSWET":
                    return totalShipWetMass;
                case "MASSRESOURCES":
                    return totalShipWetMass - totalShipDryMass;
                case "MASSPROPELLANT":
                    return resources.PropellantMass(false);
                case "MASSPROPELLANTSTAGE":
                    return resources.PropellantMass(true);

                // The delta V calculation.
                case "DELTAV":
                    return DeltaV();
                case "DELTAVSTAGE":
                    return DeltaVStage();

                // Thrust and related
                case "THRUST":
                    return (double)totalCurrentThrust;
                case "THRUSTMAX":
                    return (double)totalMaximumThrust;
                case "TWR":
                    return (double)(totalCurrentThrust / (totalShipWetMass * localGeeASL));
                case "TWRMAX":
                    return (double)(totalMaximumThrust / (totalShipWetMass * localGeeASL));
                case "ACCEL":
                    return (double)(totalCurrentThrust / totalShipWetMass);
                case "MAXACCEL":
                    return (double)(totalMaximumThrust / totalShipWetMass);
                case "GFORCE":
                    return vessel.geeForce_immediate;
                case "EFFECTIVEACCEL":
                    return vessel.acceleration.magnitude;
                case "REALISP":
                    return (double)actualAverageIsp;
                case "HOVERPOINT":
                    return (double)(localGeeDirect / (totalMaximumThrust / totalShipWetMass)).Clamp(0.0f, 1.0f);
                case "HOVERPOINTEXISTS":
                    return ((localGeeDirect / (totalMaximumThrust / totalShipWetMass)) > 1.0f) ? -1.0 : 1.0;
                case "EFFECTIVETHROTTLE":
                    return (totalMaximumThrust > 0.0f) ? (double)(totalCurrentThrust / totalMaximumThrust) : 0.0;

                // Maneuvers
                case "MNODETIMESECS":
                    if (node != null)
                    {
                        return -(node.UT - Planetarium.GetUniversalTime());
                    }
                    return double.NaN;
                case "MNODEDV":
                    if (node != null)
                    {
                        return node.GetBurnVector(vessel.orbit).magnitude;
                    }
                    return 0d;
                case "MNODEBURNTIMESECS":
                    if (node != null && totalMaximumThrust > 0 && actualAverageIsp > 0)
                    {
                        return actualAverageIsp * (1 - Math.Exp(-node.GetBurnVector(vessel.orbit).magnitude / actualAverageIsp / gee)) / (totalMaximumThrust / (totalShipWetMass * gee));
                    }
                    return double.NaN;
                case "MNODEEXISTS":
                    return node == null ? -1d : 1d;


                // Orbital parameters
                case "ORBITBODY":
                    return vessel.orbit.referenceBody.name;
                case "PERIAPSIS":
                    if (orbitSensibility)
                        return vessel.orbit.PeA;
                    return double.NaN;
                case "APOAPSIS":
                    if (orbitSensibility)
                    {
                        return vessel.orbit.ApA;
                    }
                    return double.NaN;
                case "INCLINATION":
                    if (orbitSensibility)
                    {
                        return vessel.orbit.inclination;
                    }
                    return double.NaN;
                case "ECCENTRICITY":
                    if (orbitSensibility)
                    {
                        return vessel.orbit.eccentricity;
                    }
                    return double.NaN;
                case "SEMIMAJORAXIS":
                    if (orbitSensibility)
                    {
                        return vessel.orbit.semiMajorAxis;
                    }
                    return double.NaN;

                case "ORBPERIODSECS":
                    if (orbitSensibility)
                        return vessel.orbit.period;
                    return double.NaN;
                case "TIMETOAPSECS":
                    if (orbitSensibility)
                        return vessel.orbit.timeToAp;
                    return double.NaN;
                case "TIMETOPESECS":
                    if (orbitSensibility)
                        return vessel.orbit.eccentricity < 1 ?
                            vessel.orbit.timeToPe :
                            -vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period);
                    return double.NaN;
                case "TIMESINCELASTAP":
                    if (orbitSensibility)
                        return vessel.orbit.period - vessel.orbit.timeToAp;
                    return double.NaN;
                case "TIMESINCELASTPE":
                    if (orbitSensibility)
                        return vessel.orbit.period - (vessel.orbit.eccentricity < 1 ? vessel.orbit.timeToPe : -vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period));
                    return double.NaN;
                case "TIMETONEXTAPSIS":
                    if (orbitSensibility)
                    {
                        double apsisType = NextApsisType();
                        if (apsisType < 0.0)
                        {
                            return vessel.orbit.eccentricity < 1 ?
                                vessel.orbit.timeToPe :
                                -vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period);
                        }
                        return vessel.orbit.timeToAp;
                    }
                    return double.NaN;
                case "NEXTAPSIS":
                    if (orbitSensibility)
                    {
                        double apsisType = NextApsisType();
                        if (apsisType < 0.0)
                        {
                            return vessel.orbit.PeA;
                        }
                        if (apsisType > 0.0)
                        {
                            return vessel.orbit.ApA;
                        }
                    }
                    return double.NaN;
                case "NEXTAPSISTYPE":
                    return NextApsisType();
                case "ORBITMAKESSENSE":
                    if (orbitSensibility)
                        return 1d;
                    return -1d;
                case "TIMETOANEQUATORIAL":
                    if (orbitSensibility && vessel.orbit.AscendingNodeEquatorialExists())
                        return vessel.orbit.TimeOfAscendingNodeEquatorial(Planetarium.GetUniversalTime()) - Planetarium.GetUniversalTime();
                    return double.NaN;
                case "TIMETODNEQUATORIAL":
                    if (orbitSensibility && vessel.orbit.DescendingNodeEquatorialExists())
                        return vessel.orbit.TimeOfDescendingNodeEquatorial(Planetarium.GetUniversalTime()) - Planetarium.GetUniversalTime();
                    return double.NaN;

                // SOI changes in orbits.
                case "ENCOUNTEREXISTS":
                    if (orbitSensibility)
                    {
                        switch (vessel.orbit.patchEndTransition)
                        {
                            case Orbit.PatchTransitionType.ESCAPE:
                                return -1d;
                            case Orbit.PatchTransitionType.ENCOUNTER:
                                return 1d;
                        }
                    }
                    return 0d;
                case "ENCOUNTERTIME":
                    if (orbitSensibility &&
                        (vessel.orbit.patchEndTransition == Orbit.PatchTransitionType.ENCOUNTER ||
                        vessel.orbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE))
                    {
                        return vessel.orbit.UTsoi - Planetarium.GetUniversalTime();
                    }
                    return double.NaN;
                case "ENCOUNTERBODY":
                    if (orbitSensibility)
                    {
                        switch (vessel.orbit.patchEndTransition)
                        {
                            case Orbit.PatchTransitionType.ENCOUNTER:
                                return vessel.orbit.nextPatch.referenceBody.bodyName;
                            case Orbit.PatchTransitionType.ESCAPE:
                                return vessel.mainBody.referenceBody.bodyName;
                        }
                    }
                    return string.Empty;

                // Time
                case "UTSECS":
                    if (GameSettings.KERBIN_TIME)
                    {
                        return Planetarium.GetUniversalTime() + 426 * 6 * 60 * 60;
                    }
                    return Planetarium.GetUniversalTime() + 365 * 24 * 60 * 60;
                case "METSECS":
                    return vessel.missionTime;

                // Names!
                case "NAME":
                    return vessel.vesselName;
                case "VESSELTYPE":
                    return vessel.vesselType.ToString();
                case "TARGETTYPE":
                    if (targetVessel != null)
                    {
                        return targetVessel.vesselType.ToString();
                    }
                    if (targetDockingNode != null)
                    {
                        return "Port";
                    }
                    if (targetBody != null)
                    {
                        return "Celestial";
                    }
                    return "Position";

                // Coordinates.
                case "LATITUDE":
                    return vessel.mainBody.GetLatitude(CoM);
                case "LONGITUDE":
                    return JUtil.ClampDegrees180(vessel.mainBody.GetLongitude(CoM));
                case "TARGETLATITUDE":
                case "LATITUDETGT":
                    // These targetables definitely don't have any coordinates.
                    if (target == null || target is CelestialBody)
                    {
                        return double.NaN;
                    }
                    // These definitely do.
                    if (target is Vessel || target is ModuleDockingNode)
                    {
                        return target.GetVessel().mainBody.GetLatitude(target.GetTransform().position);
                    }
                    // We're going to take a guess here and expect MechJeb's PositionTarget and DirectionTarget,
                    // which don't have vessel structures but do have a transform.
                    return vessel.mainBody.GetLatitude(target.GetTransform().position);
                case "TARGETLONGITUDE":
                case "LONGITUDETGT":
                    if (target == null || target is CelestialBody)
                    {
                        return double.NaN;
                    }
                    if (target is Vessel || target is ModuleDockingNode)
                    {
                        return JUtil.ClampDegrees180(target.GetVessel().mainBody.GetLongitude(target.GetTransform().position));
                    }
                    return vessel.mainBody.GetLongitude(target.GetTransform().position);

                // Orientation
                case "HEADING":
                    return rotationVesselSurface.eulerAngles.y;
                case "PITCH":
                    return (rotationVesselSurface.eulerAngles.x > 180) ? (360.0 - rotationVesselSurface.eulerAngles.x) : -rotationVesselSurface.eulerAngles.x;
                case "ROLL":
                    return (rotationVesselSurface.eulerAngles.z > 180) ? (360.0 - rotationVesselSurface.eulerAngles.z) : -rotationVesselSurface.eulerAngles.z;
                case "ANGLEOFATTACK":
                    return AngleOfAttack();
                case "SIDESLIP":
                    return SideSlip();
                // These values get odd when they're way out on the edge of the
                // navball because they're projected into two dimensions.
                case "PITCHPROGRADE":
                    return GetRelativePitch(prograde);
                case "PITCHRETROGRADE":
                    return GetRelativePitch(-prograde);
                case "PITCHRADIALIN":
                    return GetRelativePitch(-radialOut);
                case "PITCHRADIALOUT":
                    return GetRelativePitch(radialOut);
                case "PITCHNORMALPLUS":
                    return GetRelativePitch(normalPlus);
                case "PITCHNORMALMINUS":
                    return GetRelativePitch(-normalPlus);
                case "PITCHNODE":
                    if (node != null)
                    {
                        return GetRelativePitch(node.GetBurnVector(vessel.orbit).normalized);
                    }
                    else
                    {
                        return 0.0;
                    }
                case "PITCHTARGET":
                    if (target != null)
                    {
                        return GetRelativePitch(targetSeparation.normalized);
                    }
                    else
                    {
                        return 0.0;
                    }
                case "YAWPROGRADE":
                    return GetRelativeYaw(prograde);
                case "YAWRETROGRADE":
                    return GetRelativeYaw(-prograde);
                case "YAWRADIALIN":
                    return GetRelativeYaw(-radialOut);
                case "YAWRADIALOUT":
                    return GetRelativeYaw(radialOut);
                case "YAWNORMALPLUS":
                    return GetRelativeYaw(normalPlus);
                case "YAWNORMALMINUS":
                    return GetRelativeYaw(-normalPlus);
                case "YAWNODE":
                    if (node != null)
                    {
                        return GetRelativeYaw(node.GetBurnVector(vessel.orbit).normalized);
                    }
                    else
                    {
                        return 0.0;
                    }
                case "YAWTARGET":
                    if (target != null)
                    {
                        return GetRelativeYaw(targetSeparation.normalized);
                    }
                    else
                    {
                        return 0.0;
                    }

                // Targeting. Probably the most finicky bit right now.
                case "TARGETNAME":
                    if (target == null)
                        return string.Empty;
                    if (target is Vessel || target is CelestialBody || target is ModuleDockingNode)
                        return target.GetName();
                    // What remains is MechJeb's ITargetable implementations, which also can return a name,
                    // but the newline they return in some cases needs to be removed.
                    return target.GetName().Replace('\n', ' ');
                case "TARGETDISTANCE":
                    if (target != null)
                        return targetDistance;
                    return -1d;
                case "TARGETGROUNDDISTANCE":
                    if (target != null)
                    {
                        Vector3d targetGroundPos = target.ProjectPositionOntoSurface(vessel.mainBody);
                        if (targetGroundPos != Vector3d.zero)
                        {
                            return Vector3d.Distance(targetGroundPos, vessel.ProjectPositionOntoSurface());
                        }
                    }
                    return -1d;
                case "RELATIVEINCLINATION":
                    // MechJeb's targetables don't have orbits.
                    if (target != null && targetOrbit != null)
                    {
                        return targetOrbit.referenceBody != vessel.orbit.referenceBody ?
                            -1d :
                            Math.Abs(Vector3d.Angle(vessel.GetOrbit().SwappedOrbitNormal(), targetOrbit.SwappedOrbitNormal()));
                    }
                    return double.NaN;
                case "TARGETORBITBODY":
                    if (target != null && targetOrbit != null)
                        return targetOrbit.referenceBody.name;
                    return string.Empty;
                case "TARGETEXISTS":
                    if (target == null)
                        return -1d;
                    if (target is Vessel)
                        return 1d;
                    return 0d;
                case "TARGETISDOCKINGPORT":
                    if (target == null)
                        return -1d;
                    if (target is ModuleDockingNode)
                        return 1d;
                    return 0d;
                case "TARGETISVESSELORPORT":
                    if (target == null)
                        return -1d;
                    if (target is ModuleDockingNode || target is Vessel)
                        return 1d;
                    return 0d;
                case "TARGETISCELESTIAL":
                    if (target == null)
                        return -1d;
                    if (target is CelestialBody)
                        return 1d;
                    return 0d;
                case "TARGETSITUATION":
                    if (target is Vessel)
                        return SituationString(target.GetVessel().situation);
                    return string.Empty;
                case "TARGETALTITUDE":
                    if (target == null)
                    {
                        return -1d;
                    }
                    if (target is CelestialBody)
                    {
                        if (targetBody == vessel.mainBody || targetBody == Planetarium.fetch.Sun)
                        {
                            return 0d;
                        }
                        else
                        {
                            return targetBody.referenceBody.GetAltitude(targetBody.position);
                        }
                    }
                    if (target is Vessel || target is ModuleDockingNode)
                    {
                        return target.GetVessel().mainBody.GetAltitude(target.GetVessel().CoM);
                    }
                    else
                    {
                        return vessel.mainBody.GetAltitude(target.GetTransform().position);
                    }
                // MOARdV: I don't think these are needed - I don't remember why we needed targetOrbit
                //if (targetOrbit != null)
                //{
                //    return targetOrbit.altitude;
                //}
                //return -1d;
                case "TARGETSEMIMAJORAXIS":
                    if (target == null)
                        return double.NaN;
                    if (targetOrbit != null)
                        return targetOrbit.semiMajorAxis;
                    return double.NaN;
                case "TIMETOANWITHTARGETSECS":
                    if (target == null || targetOrbit == null)
                        return double.NaN;
                    return vessel.GetOrbit().TimeOfAscendingNode(targetOrbit, Planetarium.GetUniversalTime()) - Planetarium.GetUniversalTime();
                case "TIMETODNWITHTARGETSECS":
                    if (target == null || targetOrbit == null)
                        return double.NaN;
                    return vessel.GetOrbit().TimeOfDescendingNode(targetOrbit, Planetarium.GetUniversalTime()) - Planetarium.GetUniversalTime();
                case "TARGETCLOSESTAPPROACHTIME":
                    if (target == null || targetOrbit == null || orbitSensibility == false)
                    {
                        return double.NaN;
                    }
                    else
                    {
                        double approachTime, approachDistance;
                        approachDistance = JUtil.GetClosestApproach(vessel.GetOrbit(), target, out approachTime);
                        return approachTime - Planetarium.GetUniversalTime();
                    }
                case "TARGETCLOSESTAPPROACHDISTANCE":
                    if (target == null || targetOrbit == null || orbitSensibility == false)
                    {
                        return double.NaN;
                    }
                    else
                    {
                        double approachTime;
                        return JUtil.GetClosestApproach(vessel.GetOrbit(), target, out approachTime);
                    }

                // Space Objects (asteroid) specifics
                case "TARGETSIGNALSTRENGTH":
                    // MOARdV:
                    // Based on observation, it appears the discovery
                    // level bitfield is basically unused - either the
                    // craft is Owned (-1) or Unowned (29 - which is the
                    // OR of all the bits).  However, maybe career mode uses
                    // the bits, so I will make a guess on what knowledge is
                    // appropriate here.
                    if (targetVessel != null && targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                    {
                        return targetVessel.DiscoveryInfo.GetSignalStrength(targetVessel.DiscoveryInfo.lastObservedTime);
                    }
                    else
                    {
                        return -1.0;
                    }

                case "TARGETSIGNALSTRENGTHCAPTION":
                    if (targetVessel != null && targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                    {
                        return DiscoveryInfo.GetSignalStrengthCaption(targetVessel.DiscoveryInfo.GetSignalStrength(targetVessel.DiscoveryInfo.lastObservedTime));
                    }
                    else
                    {
                        return "";
                    }

                case "TARGETLASTOBSERVEDTIMEUT":
                    if (targetVessel != null && targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                    {
                        return targetVessel.DiscoveryInfo.lastObservedTime;
                    }
                    else
                    {
                        return -1.0;
                    }

                case "TARGETLASTOBSERVEDTIMESECS":
                    if (targetVessel != null && targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                    {
                        return Math.Max(Planetarium.GetUniversalTime() - targetVessel.DiscoveryInfo.lastObservedTime, 0.0);
                    }
                    else
                    {
                        return -1.0;
                    }

                case "TARGETSIZECLASS":
                    if (targetVessel != null && targetVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned && targetVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Presence))
                    {
                        return targetVessel.DiscoveryInfo.objectSize;
                    }
                    else
                    {
                        return "";
                    }

                // Ok, what are X, Y and Z here anyway?
                case "TARGETDISTANCEX":    //distance to target along the yaw axis (j and l rcs keys)
                    return Vector3d.Dot(targetSeparation, vessel.GetTransform().right);
                case "TARGETDISTANCEY":   //distance to target along the pitch axis (i and k rcs keys)
                    return Vector3d.Dot(targetSeparation, vessel.GetTransform().forward);
                case "TARGETDISTANCEZ":  //closure distance from target - (h and n rcs keys)
                    return -Vector3d.Dot(targetSeparation, vessel.GetTransform().up);

                case "TARGETDISTANCESCALEDX":    //scaled and clamped version of TARGETDISTANCEX.  Returns a number between 100 and -100, with precision increasing as distance decreases.
                    double scaledX = Vector3d.Dot(targetSeparation, vessel.GetTransform().right);
                    double zdist = -Vector3d.Dot(targetSeparation, vessel.GetTransform().up);
                    if (zdist < .1)
                        scaledX = scaledX / (0.1 * Math.Sign(zdist));
                    else
                        scaledX = ((scaledX + zdist) / (zdist + zdist)) * (100) - 50;
                    if (scaledX > 100) scaledX = 100;
                    if (scaledX < -100) scaledX = -100;
                    return scaledX;


                case "TARGETDISTANCESCALEDY":  //scaled and clamped version of TARGETDISTANCEY.  These two numbers will control the position needles on a docking port alignment gauge.
                    double scaledY = Vector3d.Dot(targetSeparation, vessel.GetTransform().forward);
                    double zdist2 = -Vector3d.Dot(targetSeparation, vessel.GetTransform().up);
                    if (zdist2 < .1)
                        scaledY = scaledY / (0.1 * Math.Sign(zdist2));
                    else
                        scaledY = ((scaledY + zdist2) / (zdist2 + zdist2)) * (100) - 50;
                    if (scaledY > 100) scaledY = 100;
                    if (scaledY < -100) scaledY = -100;
                    return scaledY;

                // TODO: I probably should return something else for vessels. But not sure what exactly right now.
                case "TARGETANGLEX":
                    if (target != null)
                    {
                        if (targetDockingNode != null)
                            return JUtil.NormalAngle(-targetDockingNode.GetTransform().forward, FlightGlobals.ActiveVessel.ReferenceTransform.up, FlightGlobals.ActiveVessel.ReferenceTransform.forward);
                        if (target is Vessel)
                            return JUtil.NormalAngle(-target.GetFwdVector(), forward, up);
                        return 0d;
                    }
                    return 0d;
                case "TARGETANGLEY":
                    if (target != null)
                    {
                        if (targetDockingNode != null)
                            return JUtil.NormalAngle(-targetDockingNode.GetTransform().forward, FlightGlobals.ActiveVessel.ReferenceTransform.up, -FlightGlobals.ActiveVessel.ReferenceTransform.right);
                        if (target is Vessel)
                        {
                            JUtil.NormalAngle(-target.GetFwdVector(), forward, -right);
                        }
                        return 0d;
                    }
                    return 0d;
                case "TARGETANGLEZ":
                    if (target != null)
                    {
                        if (targetDockingNode != null)
                            return (360 - (JUtil.NormalAngle(-targetDockingNode.GetTransform().up, FlightGlobals.ActiveVessel.ReferenceTransform.forward, FlightGlobals.ActiveVessel.ReferenceTransform.up))) % 360;
                        if (target is Vessel)
                        {
                            return JUtil.NormalAngle(target.GetTransform().up, up, -forward);
                        }
                        return 0d;
                    }
                    return 0d;
                case "TARGETANGLEDEV":
                    if (target != null)
                    {
                        return Vector3d.Angle(vessel.ReferenceTransform.up, FlightGlobals.fetch.vesselTargetDirection);
                    }
                    return 180d;

                case "TARGETAPOAPSIS":
                    if (target != null && targetOrbitSensibility)
                        return targetOrbit.ApA;
                    return double.NaN;
                case "TARGETPERIAPSIS":
                    if (target != null && targetOrbitSensibility)
                        return targetOrbit.PeA;
                    return double.NaN;
                case "TARGETINCLINATION":
                    if (target != null && targetOrbitSensibility)
                        return targetOrbit.inclination;
                    return double.NaN;
                case "TARGETECCENTRICITY":
                    if (target != null && targetOrbitSensibility)
                        return targetOrbit.eccentricity;
                    return double.NaN;
                case "TARGETORBITALVEL":
                    if (target != null && targetOrbitSensibility)
                        return targetOrbit.orbitalSpeed;
                    return double.NaN;
                case "TARGETTIMETOAPSECS":
                    if (target != null && targetOrbitSensibility)
                        return targetOrbit.timeToAp;
                    return double.NaN;
                case "TARGETORBPERIODSECS":
                    if (target != null && targetOrbit != null && targetOrbitSensibility)
                        return targetOrbit.period;
                    return double.NaN;
                case "TARGETTIMETOPESECS":
                    if (target != null && targetOrbitSensibility)
                        return targetOrbit.eccentricity < 1 ?
                            targetOrbit.timeToPe :
                            -targetOrbit.meanAnomaly / (2 * Math.PI / targetOrbit.period);
                    return double.NaN;

                // Protractor-type values (phase angle, ejection angle)
                case "TARGETBODYPHASEANGLE":
                    // targetOrbit is always null if targetOrbitSensibility is false,
                    // so no need to test if the orbit makes sense.
                    protractor.Update(vessel, altitudeASL, targetOrbit);
                    return protractor.PhaseAngle;
                case "TARGETBODYPHASEANGLESECS":
                    protractor.Update(vessel, altitudeASL, targetOrbit);
                    return protractor.TimeToPhaseAngle;
                case "TARGETBODYEJECTIONANGLE":
                    protractor.Update(vessel, altitudeASL, targetOrbit);
                    return protractor.EjectionAngle;
                case "TARGETBODYEJECTIONANGLESECS":
                    protractor.Update(vessel, altitudeASL, targetOrbit);
                    return protractor.TimeToEjectionAngle;
                case "TARGETBODYCLOSESTAPPROACH":
                    if (orbitSensibility == true)
                    {
                        double approachTime;
                        return JUtil.GetClosestApproach(vessel.GetOrbit(), target, out approachTime);
                    }
                    else
                    {
                        return -1.0;
                    }
                case "TARGETBODYMOONEJECTIONANGLE":
                    protractor.Update(vessel, altitudeASL, targetOrbit);
                    return protractor.MoonEjectionAngle;
                case "TARGETBODYEJECTIONALTITUDE":
                    protractor.Update(vessel, altitudeASL, targetOrbit);
                    return protractor.EjectionAltitude;
                case "TARGETBODYDELTAV":
                    protractor.Update(vessel, altitudeASL, targetOrbit);
                    return protractor.TargetBodyDeltaV;

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
                    return vessel.ctrlState.mainThrottle;
                case "STICKPITCH":
                    return vessel.ctrlState.pitch;
                case "STICKROLL":
                    return vessel.ctrlState.roll;
                case "STICKYAW":
                    return vessel.ctrlState.yaw;
                case "STICKPITCHTRIM":
                    return vessel.ctrlState.pitchTrim;
                case "STICKROLLTRIM":
                    return vessel.ctrlState.rollTrim;
                case "STICKYAWTRIM":
                    return vessel.ctrlState.yawTrim;
                case "STICKRCSX":
                    return vessel.ctrlState.X;
                case "STICKRCSY":
                    return vessel.ctrlState.Y;
                case "STICKRCSZ":
                    return vessel.ctrlState.Z;
                case "PRECISIONCONTROL":
                    return (FlightInputHandler.fetch.precisionMode).GetHashCode();

                // Staging and other stuff
                case "STAGE":
                    return Staging.CurrentStage;
                case "STAGEREADY":
                    return (Staging.separate_ready && InputLockManager.IsUnlocked(ControlTypes.STAGING)).GetHashCode();
                case "SITUATION":
                    return SituationString(vessel.situation);
                case "RANDOM":
                    cacheable = false;
                    return UnityEngine.Random.value;
                case "PODTEMPERATURE":
                    return (part != null) ? (part.temperature + KelvinToCelsius) : 0.0;
                case "PODTEMPERATUREKELVIN":
                    return (part != null) ? (part.temperature) : 0.0;
                case "PODSKINTEMPERATURE":
                    return (part != null) ? (part.skinTemperature + KelvinToCelsius) : 0.0;
                case "PODSKINTEMPERATUREKELVIN":
                    return (part != null) ? (part.skinTemperature) : 0.0;
                case "PODMAXTEMPERATURE":
                    return (part != null) ? (part.maxTemp + KelvinToCelsius) : 0.0;
                case "PODMAXTEMPERATUREKELVIN":
                    return (part != null) ? (part.maxTemp) : 0.0;
                case "PODNETFLUX":
                    return (part != null) ? (part.thermalConductionFlux + part.thermalConvectionFlux + part.thermalInternalFlux + part.thermalRadiationFlux) : 0.0;
                case "EXTERNALTEMPERATURE":
                    return vessel.externalTemperature + KelvinToCelsius;
                case "EXTERNALTEMPERATUREKELVIN":
                    return vessel.externalTemperature;
                case "HEATSHIELDTEMPERATURE":
                    return (double)heatShieldTemperature + KelvinToCelsius;
                case "HEATSHIELDTEMPERATUREKELVIN":
                    return heatShieldTemperature;
                case "HEATSHIELDTEMPERATUREFLUX":
                    return heatShieldFlux;
                case "SLOPEANGLE":
                    return slopeAngle;
                case "SPEEDDISPLAYMODE":
                    switch (FlightUIController.speedDisplayMode)
                    {
                        case FlightUIController.SpeedDisplayModes.Orbit:
                            return 1d;
                        case FlightUIController.SpeedDisplayModes.Surface:
                            return 0d;
                        case FlightUIController.SpeedDisplayModes.Target:
                            return -1d;
                    }
                    return double.NaN;
                case "ISONKERBINTIME":
                    return GameSettings.KERBIN_TIME.GetHashCode();
                case "ISDOCKINGPORTREFERENCE":
                    ModuleDockingNode thatPort = null;
                    foreach (PartModule thatModule in vessel.GetReferenceTransformPart().Modules)
                    {
                        thatPort = thatModule as ModuleDockingNode;
                        if (thatPort != null)
                            break;
                    }
                    if (thatPort != null)
                        return 1d;
                    return 0d;
                case "ISCLAWREFERENCE":
                    ModuleGrappleNode thatClaw = null;
                    foreach (PartModule thatModule in vessel.GetReferenceTransformPart().Modules)
                    {
                        thatClaw = thatModule as ModuleGrappleNode;
                        if (thatClaw != null)
                            break;
                    }
                    if (thatClaw != null)
                        return 1d;
                    return 0d;
                case "ISREMOTEREFERENCE":
                    ModuleCommand thatPod = null;
                    foreach (PartModule thatModule in vessel.GetReferenceTransformPart().Modules)
                    {
                        thatPod = thatModule as ModuleCommand;
                        if (thatPod != null)
                            break;
                    }
                    if (thatPod == null)
                        return 1d;
                    return 0d;
                case "FLIGHTUIMODE":
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

                // Meta.
                case "RPMVERSION":
                    return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
                // That would return only the "AssemblyVersion" version which in our case does not change anymore.
                // We use "AsssemblyFileVersion" for actual version numbers now to facilitate hardlinking.
                // return Assembly.GetExecutingAssembly().GetName().Version.ToString();

                case "MECHJEBAVAILABLE":
                    return MechJebAvailable().GetHashCode();

                // Compound variables which exist to stave off the need to parse logical and arithmetic expressions. :)
                case "GEARALARM":
                    // Returns 1 if vertical speed is negative, gear is not extended, and radar altitude is less than 50m.
                    return (speedVerticalRounded < 0 && !vessel.ActionGroups.groups[RPMVesselComputer.gearGroupNumber] && altitudeBottom < 100).GetHashCode();
                case "GROUNDPROXIMITYALARM":
                    // Returns 1 if, at maximum acceleration, in the time remaining until ground impact, it is impossible to get a vertical speed higher than -10m/s.
                    return (SpeedAtImpact(totalMaximumThrust) < -10d).GetHashCode();
                case "TUMBLEALARM":
                    return (speedVerticalRounded < 0 && altitudeBottom < 100 && speedHorizontal > 5).GetHashCode();
                case "SLOPEALARM":
                    return (speedVerticalRounded < 0.0 && altitudeBottom < 100.0 && slopeAngle > 15.0f).GetHashCode();
                case "DOCKINGANGLEALARM":
                    return (targetDockingNode != null && targetDistance < 10 && approachSpeed > 0 &&
                    (Math.Abs(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, up)) > 1.5 ||
                    Math.Abs(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, -right)) > 1.5)).GetHashCode();
                case "DOCKINGSPEEDALARM":
                    return (targetDockingNode != null && approachSpeed > 2.5 && targetDistance < 15).GetHashCode();
                case "ALTITUDEALARM":
                    return (speedVerticalRounded < 0 && altitudeBottom < 150).GetHashCode();
                case "PODTEMPERATUREALARM":
                    if (part != null)
                    {
                        double tempRatio = part.temperature / part.maxTemp;
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
                // Well, it's not a compound but it's an alarm...
                case "ENGINEOVERHEATALARM":
                    return anyEnginesOverheating.GetHashCode();
                case "ENGINEFLAMEOUTALARM":
                    return anyEnginesFlameout.GetHashCode();
                case "IMPACTALARM":
                    return (part != null && vessel.srfSpeed > part.crashTolerance).GetHashCode();

                // SCIENCE!!
                case "SCIENCEDATA":
                    return totalDataAmount;
                case "SCIENCECOUNT":
                    return totalExperimentCount;
                case "BIOMENAME":
                    return vessel.CurrentBiome();
                case "BIOMEID":
                    return ScienceUtil.GetExperimentBiome(vessel.mainBody, vessel.latitude, vessel.longitude);

                // Some of the new goodies in 0.24.
                case "REPUTATION":
                    return Reputation.Instance != null ? (double)Reputation.CurrentRep : 0;
                case "FUNDS":
                    return Funding.Instance != null ? Funding.Instance.Funds : 0;

                // Action group flags. To properly format those, use this format:
                // {0:on;0;OFF}
                case "GEAR":
                    return vessel.ActionGroups.groups[RPMVesselComputer.gearGroupNumber].GetHashCode();
                case "BRAKES":
                    return vessel.ActionGroups.groups[RPMVesselComputer.brakeGroupNumber].GetHashCode();
                case "SAS":
                    return vessel.ActionGroups.groups[RPMVesselComputer.sasGroupNumber].GetHashCode();
                case "LIGHTS":
                    return vessel.ActionGroups.groups[RPMVesselComputer.lightGroupNumber].GetHashCode();
                case "RCS":
                    return vessel.ActionGroups.groups[RPMVesselComputer.rcsGroupNumber].GetHashCode();

                // 0.90 SAS mode fields:
                case "SASMODESTABILITY":
                    return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.StabilityAssist) ? 1.0 : 0.0;
                case "SASMODEPROGRADE":
                    return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Prograde) ? 1.0 :
                        (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Retrograde) ? -1.0 : 0.0;
                case "SASMODENORMAL":
                    return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Normal) ? 1.0 :
                        (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Antinormal) ? -1.0 : 0.0;
                case "SASMODERADIAL":
                    return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.RadialOut) ? 1.0 :
                        (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.RadialIn) ? -1.0 : 0.0;
                case "SASMODETARGET":
                    return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Target) ? 1.0 :
                        (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.AntiTarget) ? -1.0 : 0.0;
                case "SASMODEMANEUVER":
                    return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Maneuver) ? 1.0 : 0.0;

                // Database information about planetary bodies.
                case "ORBITBODYATMOSPHERE":
                    return vessel.orbit.referenceBody.atmosphere ? 1d : -1d;
                case "TARGETBODYATMOSPHERE":
                    if (targetBody != null)
                        return targetBody.atmosphere ? 1d : -1d;
                    return 0d;
                case "ORBITBODYOXYGEN":
                    return vessel.orbit.referenceBody.atmosphereContainsOxygen ? 1d : -1d;
                case "TARGETBODYOXYGEN":
                    if (targetBody != null)
                        return targetBody.atmosphereContainsOxygen ? 1d : -1d;
                    return -1d;
                case "ORBITBODYSCALEHEIGHT":
                    return vessel.orbit.referenceBody.atmosphereDepth;
                case "TARGETBODYSCALEHEIGHT":
                    if (targetBody != null)
                        return targetBody.atmosphereDepth;
                    return -1d;
                case "ORBITBODYRADIUS":
                    return vessel.orbit.referenceBody.Radius;
                case "TARGETBODYRADIUS":
                    if (targetBody != null)
                        return targetBody.Radius;
                    return -1d;
                case "ORBITBODYMASS":
                    return vessel.orbit.referenceBody.Mass;
                case "TARGETBODYMASS":
                    if (targetBody != null)
                        return targetBody.Mass;
                    return -1d;
                case "ORBITBODYROTATIONPERIOD":
                    return vessel.orbit.referenceBody.rotationPeriod;
                case "TARGETBODYROTATIONPERIOD":
                    if (targetBody != null)
                        return targetBody.rotationPeriod;
                    return -1d;
                case "ORBITBODYSOI":
                    return vessel.orbit.referenceBody.sphereOfInfluence;
                case "TARGETBODYSOI":
                    if (targetBody != null)
                        return targetBody.sphereOfInfluence;
                    return -1d;
                case "ORBITBODYGEEASL":
                    return vessel.orbit.referenceBody.GeeASL;
                case "TARGETBODYGEEASL":
                    if (targetBody != null)
                        return targetBody.GeeASL;
                    return -1d;
                case "ORBITBODYGM":
                    return vessel.orbit.referenceBody.gravParameter;
                case "TARGETBODYGM":
                    if (targetBody != null)
                        return targetBody.gravParameter;
                    return -1d;
                case "ORBITBODYATMOSPHERETOP":
                    return vessel.orbit.referenceBody.atmosphereDepth;
                case "TARGETBODYATMOSPHERETOP":
                    if (targetBody != null)
                        return targetBody.atmosphereDepth;
                    return -1d;
                case "ORBITBODYESCAPEVEL":
                    return Math.Sqrt(2 * vessel.orbit.referenceBody.gravParameter / vessel.orbit.referenceBody.Radius);
                case "TARGETBODYESCAPEVEL":
                    if (targetBody != null)
                        return Math.Sqrt(2 * targetBody.gravParameter / targetBody.Radius);
                    return -1d;
                case "ORBITBODYAREA":
                    return 4 * Math.PI * vessel.orbit.referenceBody.Radius * vessel.orbit.referenceBody.Radius;
                case "TARGETBODYAREA":
                    if (targetBody != null)
                        return 4 * Math.PI * targetBody.Radius * targetBody.Radius;
                    return -1d;
                case "ORBITBODYSYNCORBITALTITUDE":
                    double syncRadius = Math.Pow(vessel.orbit.referenceBody.gravParameter / Math.Pow(2 * Math.PI / vessel.orbit.referenceBody.rotationPeriod, 2), 1 / 3d);
                    return syncRadius > vessel.orbit.referenceBody.sphereOfInfluence ? double.NaN : syncRadius - vessel.orbit.referenceBody.Radius;
                case "TARGETBODYSYNCORBITALTITUDE":
                    if (targetBody != null)
                    {
                        double syncRadiusT = Math.Pow(targetBody.gravParameter / Math.Pow(2 * Math.PI / targetBody.rotationPeriod, 2), 1 / 3d);
                        return syncRadiusT > targetBody.sphereOfInfluence ? double.NaN : syncRadiusT - targetBody.Radius;
                    }
                    return -1d;
                case "ORBITBODYSYNCORBITVELOCITY":
                    return (2 * Math.PI / vessel.orbit.referenceBody.rotationPeriod) *
                    Math.Pow(vessel.orbit.referenceBody.gravParameter / Math.Pow(2 * Math.PI / vessel.orbit.referenceBody.rotationPeriod, 2), 1 / 3d);
                case "TARGETBODYSYNCORBITVELOCITY":
                    if (targetBody != null)
                    {
                        return (2 * Math.PI / targetBody.rotationPeriod) *
                        Math.Pow(targetBody.gravParameter / Math.Pow(2 * Math.PI / targetBody.rotationPeriod, 2), 1 / 3d);
                    }
                    return -1d;
                case "ORBITBODYSYNCORBITCIRCUMFERENCE":
                    return 2 * Math.PI * Math.Pow(vessel.orbit.referenceBody.gravParameter / Math.Pow(2 * Math.PI / vessel.orbit.referenceBody.rotationPeriod, 2), 1 / 3d);
                case "TARGETBODYSYNCORBICIRCUMFERENCE":
                    if (targetBody != null)
                    {
                        return 2 * Math.PI * Math.Pow(targetBody.gravParameter / Math.Pow(2 * Math.PI / targetBody.rotationPeriod, 2), 1 / 3d);
                    }
                    return -1d;
            }
            return null;
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
                //JUtil.LogMessage(this, " ... tearing down statics");
                customVariables = null;
                knownLoadedAssemblies = null;
                mappedVariables = null;
                systemNamedResources = null;

                protractor = null;

                for (int i = 0; i < installedModules.Count; ++i)
                {
                    installedModules[i].Invalidate(null);
                }
                installedModules = null;
            }
        }

        private void PartCoupleCallback(GameEvents.FromToAction<Part, Part> action)
        {
            if(action.from.vessel == vessel || action.to.vessel == vessel)
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
            if (v == vessel)
            {
                //JUtil.LogMessage(this, "onVesselChange({0}), I am {1}, so I am becoming active", v.vesselName, vessel.vesselName);
                timeToUpdate = true;
                for (int i = 0; i < installedModules.Count; ++i)
                {
                    installedModules[i].Invalidate(vessel);
                }
            }
        }

        private void VesselModifiedCallback(Vessel v)
        {
            if (v == vessel && JUtil.IsActiveVessel(vessel))
            {
                //JUtil.LogMessage(this, "onVesselModified({0}), I am {1}, so I am modified", v.vesselName, vessel.vesselName);
                timeToUpdate = true;
            }
        }
        #endregion

        //--- Fallback evaluators
        #region FallbackEvaluators
        private double FallbackEvaluateAngleOfAttack()
        {
            // Code courtesy FAR.
            Transform refTransform = vessel.GetTransform();
            Vector3 velVectorNorm = vessel.srf_velocity.normalized;

            Vector3 tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.forward * Vector3.Dot(refTransform.forward, velVectorNorm);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            double AoA = Vector3.Dot(tmpVec.normalized, refTransform.forward);
            AoA = Mathf.Rad2Deg * Math.Asin(AoA);
            if (double.IsNaN(AoA))
            {
                AoA = 0.0;
            }

            return AoA;
        }

        private double FallbackEvaluateDeltaV()
        {
            return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(false)));
        }

        private double FallbackEvaluateDeltaVStage()
        {
            return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(true)));
        }

        private double FallbackEvaluateDynamicPressure()
        {
            return vessel.dynamicPressurekPa;
        }

        private double FallbackEvaluateSideSlip()
        {
            // Code courtesy FAR.
            Transform refTransform = vessel.GetTransform();
            Vector3 velVectorNorm = vessel.srf_velocity.normalized;

            Vector3 tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.right * Vector3.Dot(refTransform.right, velVectorNorm);     //velocity vector projected onto the vehicle-horizontal plane
            double sideslipAngle = Vector3.Dot(tmpVec.normalized, refTransform.right);
            sideslipAngle = Mathf.Rad2Deg * Math.Asin(sideslipAngle);
            if (double.IsNaN(sideslipAngle))
            {
                sideslipAngle = 0.0;
            }

            return sideslipAngle;
        }

        private double FallbackEvaluateTerminalVelocity()
        {
            // Terminal velocity computation based on MechJeb 2.5.1 or one of the later snapshots
            if (altitudeASL > vessel.mainBody.RealMaxAtmosphereAltitude())
            {
                return float.PositiveInfinity;
            }

            Vector3d pureDragV = Vector3d.zero, pureLiftV = Vector3d.zero;

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part p = vessel.parts[i];

                pureDragV += -p.dragVectorDir * p.dragScalar;

                if (!p.hasLiftModule)
                {
                    Vector3 bodyLift = p.transform.rotation * (p.bodyLiftScalar * p.DragCubes.LiftForce);
                    bodyLift = Vector3.ProjectOnPlane(bodyLift, -p.dragVectorDir);
                    pureLiftV += bodyLift;

                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        PartModule pm = p.Modules[m];
                        if (!pm.isEnabled)
                        {
                            continue;
                        }

                        if (pm is ModuleControlSurface)
                        {
                            ModuleControlSurface cs = (pm as ModuleControlSurface);

                            if (p.ShieldedFromAirstream || cs.deploy)
                                continue;

                            pureLiftV += cs.liftForce;
                            pureDragV += cs.dragForce;
                        }
                        else if (pm is ModuleLiftingSurface)
                        {
                            ModuleLiftingSurface liftingSurface = (ModuleLiftingSurface)pm;
                            pureLiftV += liftingSurface.liftForce;
                            pureDragV += liftingSurface.dragForce;
                        }
                    }
                }
            }

            pureDragV = pureDragV / totalShipWetMass;
            pureLiftV = pureLiftV / totalShipWetMass;

            Vector3d force = pureDragV + pureLiftV;
            double drag = Vector3d.Dot(force, -vessel.srf_velocity.normalized);

            return Math.Sqrt(localGeeDirect / drag) * vessel.srfSpeed;
        }
        #endregion

        //--- Plugin-enabled evaluators
        #region PluginEvaluators
        private double AngleOfAttack()
        {
            if (evaluateAngleOfAttack == null)
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
                    accessor = FallbackEvaluateAngleOfAttack;
                }

                evaluateAngleOfAttack = accessor;
            }

            return evaluateAngleOfAttack();
        }

        private double DeltaV()
        {
            if (evaluateDeltaV == null)
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
                    accessor = FallbackEvaluateDeltaV;
                }

                evaluateDeltaV = accessor;
            }

            return evaluateDeltaV();
        }

        private double DeltaVStage()
        {
            if (evaluateDeltaVStage == null)
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
                    accessor = FallbackEvaluateDeltaVStage;
                }

                evaluateDeltaVStage = accessor;
            }

            return evaluateDeltaVStage();
        }

        private double DynamicPressure()
        {
            if (evaluateDynamicPressure == null)
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
                    accessor = FallbackEvaluateDynamicPressure;
                }

                evaluateDynamicPressure = accessor;
            }

            return evaluateDynamicPressure();
        }

        private double LandingError()
        {
            if (evaluateLandingError == null)
            {
                evaluateLandingError = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingError", typeof(Func<double>));
            }

            return evaluateLandingError();
        }

        private double LandingAltitude()
        {
            if (evaluateLandingAltitude == null)
            {
                evaluateLandingAltitude = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingAltitude", typeof(Func<double>));
            }

            return evaluateLandingAltitude();
        }

        private double LandingLatitude()
        {
            if (evaluateLandingLatitude == null)
            {
                evaluateLandingLatitude = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLatitude", typeof(Func<double>));
            }

            return evaluateLandingLatitude();
        }

        private double LandingLongitude()
        {
            if (evaluateLandingLongitude == null)
            {
                evaluateLandingLongitude = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLongitude", typeof(Func<double>));
            }

            return evaluateLandingLongitude();
        }

        private bool MechJebAvailable()
        {
            if (evaluateMechJebAvailable == null)
            {
                Func<bool> accessor = null;

                accessor = (Func<bool>)GetInternalMethod("JSIMechJeb:GetMechJebAvailable", typeof(Func<bool>));
                if (accessor == null)
                {
                    accessor = JUtil.ReturnFalse;
                }

                evaluateMechJebAvailable = accessor;
            }

            return evaluateMechJebAvailable();
        }

        private double SideSlip()
        {
            if (evaluateSideSlip == null)
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
                    accessor = FallbackEvaluateSideSlip;
                }

                evaluateSideSlip = accessor;
            }

            return evaluateSideSlip();
        }

        private double TerminalVelocity()
        {
            if (evaluateTerminalVelocity == null)
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

                if (accessor == null)
                {
                    accessor = FallbackEvaluateTerminalVelocity;
                }

                evaluateTerminalVelocity = accessor;
            }

            return evaluateTerminalVelocity();
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
