//#define HACK_IN_A_NAVPOINT
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Diagnostics;

namespace JSI
{
    public class RasterPropMonitorComputer : PartModule
    {
        // The only public configuration variable.
        [KSPField]
        public bool debugLogging = true;
        // The OTHER public configuration variable.
        [KSPField]
        public string storedStrings = string.Empty;

        // Persistence for internal modules.
        [KSPField(isPersistant = true)]
        public string data = "";
        // Yes, it's a really braindead way of doing it, but I ran out of elegant ones,
        // because nothing appears to work as documented -- IF it's documented.
        // This one is sure to work and isn't THAT much of a performance drain, really.
        // Pull requests welcome
        // Vessel description storage and related code.
        [KSPField(isPersistant = true)]
        public string vesselDescription = string.Empty;
        private string vesselDescriptionForDisplay = string.Empty;
        private readonly string editorNewline = ((char)0x0a).ToString();
        // Public interface.
        public bool updateForced;
        // Data common for various variable calculations
        private int vesselNumParts;
        private int updateCountdown;
        private int dataUpdateCountdown;
        private int refreshTextRate = int.MaxValue;
        private int refreshDataRate = int.MaxValue;

        // Craft center
        public Vector3d CoM
        {
            get
            {
                return vessel.CoM;
            }
        }

        // Craft-relative basis vectors
        public Vector3d Forward
        {
            get
            {
                return vessel.GetTransform().up;
            }
        }
        public Vector3d Right
        {
            get
            {
                return vessel.GetTransform().right;
            }
        }

        // Surface-relative vectors
        private Vector3d up;
        public Vector3d Up
        {
            get
            {
                return up;
            }
        }
        private Vector3d north;
        public Vector3d North
        {
            get
            {
                return north;
            }
        }
        // surfaceRight is the projection of the right vector onto the surface.
        // If up x right is a degenerate vector (rolled on the side), we use
        // the forward vector to compose a new basis
        private Vector3d surfaceRight;
        public Vector3d SurfaceRight
        {
            get
            {
                return surfaceRight;
            }
        }
        // surfaceForward is the cross of the up vector and right vector, so
        // that surface velocity can be decomposed to surface-relative components.
        private Vector3d surfaceForward;
        public Vector3d SurfaceForward
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
        private Quaternion rotationSurface;

        public Vector3d VelocityVesselSurface
        {
            get
            {
                return vessel.srf_velocity;
            }
        }

        public Vector3d VelocityVesselOrbit
        {
            get
            {
                return vessel.orbit.GetVel();
            }
        }

        private Vector3d velocityRelativeTarget;
        private double speedVertical
        {
            get
            {
                return vessel.verticalSpeed;
            }
        }
        private double speedVerticalRounded;

        private double horzVelocity;
        private ITargetable target;
        private ModuleDockingNode targetDockingNode;
        private Vessel targetVessel;
        private double targetDistance;
        private Vector3d targetSeparation;
        public Vector3d TargetSeparation
        {
            get
            {
                return targetSeparation;
            }
        }
        private double approachSpeed;
        private Quaternion targetOrientation;
        private ManeuverNode node;

        public double Time
        {
            get
            {
                return Planetarium.GetUniversalTime();
            }
        }
        private ProtoCrewMember[] vesselCrew;
        private kerbalExpressionSystem[] vesselCrewMedical;
        private ProtoCrewMember[] localCrew;
        private kerbalExpressionSystem[] localCrewMedical;

        public double AltitudeASL
        {
            get
            {
                return vessel.mainBody.GetAltitude(vessel.CoM);
            }
        }
        private double altitudeTrue;
        private double altitudeBottom;
        private Orbit targetOrbit;
        private bool orbitSensibility;
        private bool targetOrbitSensibility;
        private readonly ResourceDataStorage resources = new ResourceDataStorage();
        private string[] resourcesAlphabetic;
        private double totalShipDryMass;
        private double totalShipWetMass;
        private double totalCurrentThrust;
        private double totalMaximumThrust;
        private double actualAverageIsp;
        private bool anyEnginesOverheating;
        private bool anyEnginesFlameout;
        private float totalDataAmount;
        private double secondsToImpact;
        private double bestPossibleSpeedAtImpact, expectedSpeedAtImpact;
        private double localGeeASL, localGeeDirect;
        private double standardAtmosphere;
        private float slopeAngle;
        private readonly double upperAtmosphereLimit = Math.Log(100000);
        private float heatShieldTemperature;
        private float heatShieldFlux;

        private CelestialBody targetBody;
        private Protractor protractor = null;
        private double lastTimePerSecond;
        private double lastTerrainHeight, terrainDelta;
        private ExternalVariableHandlers plugins;
        private PersistenceAccessor persistence;
        public PersistenceAccessor Persistence
        {
            get
            {
                if (persistence == null)
                {
                    persistence = new PersistenceAccessor(this);
                }
                return persistence;
            }
        }
        // Local data fetching variables...
        private int gearGroupNumber;
        private int brakeGroupNumber;
        private int sasGroupNumber;
        private int lightGroupNumber;
        private int rcsGroupNumber;
        private readonly int[] actionGroupID = new int[10];
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
			"AG9",
		};

        // Some constant things...
        private const double gee = 9.81d;
        private const double KelvinToCelsius = -273.15;

        // Ok, this is to deprecate the named resources mechanic entirely...
        private static SortedDictionary<string, string> systemNamedResources;

        private static List<string> knownLoadedAssemblies;

        private string[] storedStringsArray;

        private Dictionary<string, CustomVariable> customVariables = new Dictionary<string, CustomVariable>();
        private Dictionary<string, MappedVariable> mappedVariables = new Dictionary<string, MappedVariable>();
        private Dictionary<string, Func<bool>> pluginBoolVariables = new Dictionary<string, Func<bool>>();
        private Dictionary<string, Func<double>> pluginDoubleVariables = new Dictionary<string, Func<double>>();

        private List<IJSIModule> installedModules = new List<IJSIModule>();

        // Plugin evaluator reflections
        private Func<bool> evaluateMechJebAvailable;
        private Func<double> evaluateDeltaV;
        private Func<double> evaluateDeltaVStage;
        private Func<double> evaluateLandingError;
        private Func<double> evaluateLandingAltitude;
        private Func<double> evaluateLandingLatitude;
        private Func<double> evaluateLandingLongitude;
        private Func<double> evaluateTerminalVelocity;

        // Processing cache!
        private readonly DefaultableDictionary<string, object> resultCache = new DefaultableDictionary<string, object>(null);

        // Public functions:
        // Request the instance, create it if one doesn't exist:
        public static RasterPropMonitorComputer Instantiate(MonoBehaviour referenceLocation)
        {
            var thatProp = referenceLocation as InternalProp;
            var thatPart = referenceLocation as Part;
            if (thatPart == null)
            {
                if (thatProp == null)
                {
                    throw new ArgumentException("Cannot instantiate RPMC in this location.");
                }
                thatPart = thatProp.part;
            }
            for (int i = 0; i < thatPart.Modules.Count; i++)
            {
                if (thatPart.Modules[i].ClassName == typeof(RasterPropMonitorComputer).Name)
                {
                    return thatPart.Modules[i] as RasterPropMonitorComputer;
                }
            }
            return thatPart.AddModule(typeof(RasterPropMonitorComputer).Name) as RasterPropMonitorComputer;
        }

        // Set refresh rates.
        public void UpdateRefreshRates(int textRate, int dataRate)
        {
            refreshTextRate = Math.Min(textRate, refreshTextRate);
            refreshDataRate = Math.Min(dataRate, refreshDataRate);
        }

        // Page handler interface for vessel description page.
        // Analysis disable UnusedParameter
        public string VesselDescriptionRaw(int screenWidth, int screenHeight)
        {
            // Analysis restore UnusedParameter
            return vesselDescriptionForDisplay.UnMangleConfigText();
        }
        // Analysis disable UnusedParameter
        public string VesselDescriptionWordwrapped(int screenWidth, int screenHeight)
        {
            // Analysis restore UnusedParameter
            return JUtil.WordWrap(vesselDescriptionForDisplay.UnMangleConfigText(), screenWidth);
        }
        // Damnit, looks like this needs a separate start.
        public void Start()
        {
            JUtil.debugLoggingEnabled = debugLogging;

            if (!HighLogic.LoadedSceneIsEditor)
            {
                JUtil.LogMessage(this, "Initializing RPM version {0}", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
                gearGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Gear);
                brakeGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Brakes);
                sasGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.SAS);
                lightGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Light);
                rcsGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.RCS);

                KSPActionGroup[] customGroups = {
					KSPActionGroup.Custom10,
					KSPActionGroup.Custom01,
					KSPActionGroup.Custom02,
					KSPActionGroup.Custom03,
					KSPActionGroup.Custom04,
					KSPActionGroup.Custom05,
					KSPActionGroup.Custom06,
					KSPActionGroup.Custom07,
					KSPActionGroup.Custom08,
					KSPActionGroup.Custom09,
				};

                for (int i = 0; i < 10; i++)
                {
                    actionGroupID[i] = BaseAction.GetGroupIndex(customGroups[i]);
                }

                FetchPerPartData();
                standardAtmosphere = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(0, FlightGlobals.Bodies[1]), FlightGlobals.Bodies[1].atmosphereTemperatureSeaLevel);

                // Let's deal with the system resource library.
                // This dictionary is sorted so that longer names go first to prevent false identification - they're compared in order.
                systemNamedResources = new SortedDictionary<string, string>(new ResourceNameLengthComparer());
                foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions)
                {
                    string varname = thatResource.name.ToUpperInvariant().Replace(' ', '-').Replace('_', '-');
                    systemNamedResources.Add(varname, thatResource.name);
                    JUtil.LogMessage(this, "Remembering system resource {1} as SYSR_{0}", varname, thatResource.name);
                }

                // Now let's collect a list of all assemblies loaded on the system.

                knownLoadedAssemblies = new List<string>();
                foreach (AssemblyLoader.LoadedAssembly thatAssembly in AssemblyLoader.loadedAssemblies)
                {
                    string thatName = thatAssembly.assembly.GetName().Name;
                    knownLoadedAssemblies.Add(thatName.ToUpper());
                    JUtil.LogMessage(this, "I know that {0} ISLOADED_{1}", thatName, thatName.ToUpper());
                }

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

                // And parse known mapped variables
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


                // Now let's parse our stored strings...
                if (!string.IsNullOrEmpty(storedStrings))
                {
                    storedStringsArray = storedStrings.Split('|');
                }

                // We instantiate plugins late.
                plugins = new ExternalVariableHandlers(this);

                if (persistence == null)
                {
                    persistence = new PersistenceAccessor(this);
                }

                protractor = new Protractor(this);

                installedModules.Add(new JSIParachute(vessel));
                installedModules.Add(new JSIMechJeb(vessel));
                installedModules.Add(new JSIInternalRPMButtons(vessel));
                installedModules.Add(new JSIGimbal(vessel));
            }
        }

        public Delegate GetMethod(string packedMethod, InternalProp internalProp, Type delegateType)
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

            var methodInfo = delegateType.GetMethod("Invoke");
            if (jsiModule == null)
            {
                // Fall back - this method isn't part of the core RPM system
                return JUtil.GetMethod(packedMethod, internalProp, delegateType);
            }

            Type returnType = delegateType.GetMethod("Invoke").ReturnType;
            Delegate stateCall = null;
            foreach (MethodInfo m in jsiModule.GetType().GetMethods())
            {
                if (!string.IsNullOrEmpty(tokens[1]) && m.Name == tokens[1] && IsEquivalent(m, methodInfo))
                {
                    stateCall = Delegate.CreateDelegate(delegateType, jsiModule, m);
                }
            }

            return stateCall;
        }

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

        public override void OnStart(PartModule.StartState state)
        {
            if (state != StartState.Editor)
            {
                // Parse vessel description here for special lines:

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
                vesselDescriptionForDisplay = string.Join(Environment.NewLine, descriptionStrings).MangleConfigText();
            }
        }

        private bool UpdateCheck()
        {
            if (vesselNumParts != vessel.Parts.Count || updateCountdown <= 0 || dataUpdateCountdown <= 0 || updateForced)
            {
                updateCountdown = refreshTextRate;
                if (vesselNumParts != vessel.Parts.Count || dataUpdateCountdown <= 0 || updateForced)
                {
                    dataUpdateCountdown = refreshDataRate;
                    vesselNumParts = vessel.Parts.Count;
                    FetchPerPartData();
                }
                updateForced = false;
                return true;
            }
            dataUpdateCountdown--;
            updateCountdown--;
            return false;
        }

        // I don't remember why exactly, but I think it has to be out of OnUpdate to work in editor...
        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                // well, it looks sometimes it might become null..

                // For some unclear reason, the newline in this case is always 0A, rather than Environment.NewLine.
                vesselDescription = EditorLogic.fetch.shipDescriptionField != null ? EditorLogic.fetch.shipDescriptionField.Text.Replace(editorNewline, "$$$") : string.Empty;
            }
        }

        public override void OnUpdate()
        {
            if (!JUtil.IsActiveVessel(vessel))
            {
                return;
            }

            if (!UpdateCheck())
            {
                return;
            }

            // We clear the cache every frame.
            resultCache.Clear();
            foreach (IJSIModule module in installedModules)
            {
                module.Invalidate();
            }

            FetchCommonData();
        }

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

        // Valid only for the active vessel.  Imported from MechJeb
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
            double decelTime = horzVelocity / effectiveDecel;

            Vector3d estimatedLandingSite = CoM + 0.5 * decelTime * vessel.srf_velocity;
            double terrainRadius = vessel.mainBody.Radius + vessel.mainBody.TerrainAltitude(estimatedLandingSite);
            double impactTime = 0;
            try
            {
                impactTime = orbit.NextTimeOfRadius(Time, terrainRadius);
            }
            catch (ArgumentException)
            {
                return double.NaN;
            }
            return impactTime - decelTime / 2.0 - Time;
        }

        private void FetchCommonData()
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
            localGeeASL = vessel.orbit.referenceBody.GeeASL * gee;
            localGeeDirect = FlightGlobals.getGeeForceAtPosition(CoM).magnitude;
            up = (CoM - vessel.mainBody.position).normalized;
            north = Vector3.ProjectOnPlane((vessel.mainBody.position + (Vector3d)vessel.mainBody.transform.up * vessel.mainBody.Radius) - CoM, up).normalized;
            rotationSurface = Quaternion.LookRotation(north, up);
            rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);

            // Generate the surface-relative basis (up, surfaceRight, surfaceForward)
            surfaceForward = Vector3d.Cross(up, Right);
            // If the craft is rolled sharply to the side, we have to re-do our basis.
            if (surfaceForward.sqrMagnitude < 0.5)
            {
                surfaceRight = Vector3d.Cross(Forward, up);
                surfaceForward = Vector3d.Cross(up, surfaceRight);
            }
            else
            {
                surfaceRight = Vector3d.Cross(surfaceForward, up);
            }

            speedVerticalRounded = Math.Ceiling(speedVertical * 20.0) / 20.0;
            target = FlightGlobals.fetch.VesselTarget;
            if (vessel.patchedConicSolver != null)
            {
                node = vessel.patchedConicSolver.maneuverNodes.Count > 0 ? vessel.patchedConicSolver.maneuverNodes[0] : null;
            }
            else
            {
                node = null;
            }

            FetchAltitudes();

            if (Time >= lastTimePerSecond + 1.0)
            {
                terrainDelta = vessel.terrainAltitude - lastTerrainHeight;
                lastTerrainHeight = vessel.terrainAltitude;
                lastTimePerSecond = Time;
            }

            horzVelocity = (VelocityVesselSurface - (speedVertical * up)).magnitude;

            if (target != null)
            {
                targetSeparation = vessel.GetTransform().position - target.GetTransform().position;
                targetOrientation = target.GetTransform().rotation;

                targetVessel = target as Vessel;
                targetBody = target as CelestialBody;
                targetDockingNode = target as ModuleDockingNode;

                targetDistance = Vector3.Distance(target.GetTransform().position, vessel.GetTransform().position);

                // This is kind of messy.
                targetOrbitSensibility = false;
                // All celestial bodies except the sun have orbits that make sense.
                targetOrbitSensibility |= targetBody != null && targetBody != Planetarium.fetch.Sun;

                if (targetVessel != null)
                    targetOrbitSensibility = JUtil.OrbitMakesSense(targetVessel);
                if (targetDockingNode != null)
                    targetOrbitSensibility = JUtil.OrbitMakesSense(target.GetVessel());

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
                    approachSpeed = speedVertical;
                // In all other cases, that should work. I think.
                approachSpeed = Vector3d.Dot(velocityRelativeTarget, (target.GetTransform().position - vessel.GetTransform().position).normalized);
            }
            else
            {
                velocityRelativeTarget = targetSeparation = Vector3d.zero;
                targetOrbit = null;
                targetDistance = 0;
                approachSpeed = 0;
                targetBody = null;
                targetVessel = null;
                targetDockingNode = null;
                targetOrientation = vessel.GetTransform().rotation;
                targetOrbitSensibility = false;
            }
            orbitSensibility = JUtil.OrbitMakesSense(vessel);
            if (vessel.situation == Vessel.Situations.SUB_ORBITAL || vessel.situation == Vessel.Situations.FLYING)
            {
                // Mental note: the local g taken from vessel.mainBody.GeeASL will suffice.
                //  t = (v+sqrt(v²+2gd))/g or something.

                // What is the vertical component of current acceleration?
                double accelUp = Vector3d.Dot(vessel.acceleration, up);

                double altitude = altitudeTrue;
                if (vessel.mainBody.ocean && AltitudeASL > 0.0)
                {
                    // AltitudeTrue shows distance above the floor of the ocean,
                    // so use ASL if it's closer in this case, and we're not
                    // already below SL.
                    altitude = Math.Min(AltitudeASL, altitudeTrue);
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

                // MOARdV: I think this gets the computation right.  High thrust will
                // result in NaN, which is already handled.
                /*
                double accelerationAtMaxThrust = localG - (totalMaximumThrust / totalShipWetMass);
                double timeToImpactAtMaxThrust = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * accelerationAtMaxThrust * altitude)) / accelerationAtMaxThrust;
                bestPossibleSpeedAtImpact = speedVertical - accelerationAtMaxThrust * timeToImpactAtMaxThrust;
                if (double.IsNaN(bestPossibleSpeedAtImpact))
                    bestPossibleSpeedAtImpact = 0;
                */
                bestPossibleSpeedAtImpact = SpeedAtImpact(totalMaximumThrust, totalShipWetMass, localGeeASL, speedVertical, altitude);
                expectedSpeedAtImpact = SpeedAtImpact(totalCurrentThrust, totalShipWetMass, localGeeASL, speedVertical, altitude);

            }
            else
            {
                secondsToImpact = Double.NaN;
                bestPossibleSpeedAtImpact = 0;
                expectedSpeedAtImpact = 0;
            }
        }

        private static double SpeedAtImpact(double thrust, double mass, double freeFall, double currentSpeed, double currentAltitude)
        {
            double acceleration = freeFall - (thrust / mass);
            double timeToImpact = (currentSpeed + Math.Sqrt(currentSpeed * currentSpeed + 2 * acceleration * currentAltitude)) / acceleration;
            double speedAtImpact = currentSpeed - acceleration * timeToImpact;
            if (double.IsNaN(speedAtImpact))
                speedAtImpact = 0;
            return speedAtImpact;
        }

        private void FetchPerPartData()
        {
            totalShipDryMass = totalShipWetMass = totalCurrentThrust = totalMaximumThrust = 0;
            totalDataAmount = 0.0f;
            heatShieldTemperature = heatShieldFlux = 0.0f;
            float hottestShield = float.MinValue;

            float averageIspContribution = 0.0f;

            anyEnginesOverheating = anyEnginesFlameout = false;

            resources.StartLoop(Time);

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
                        anyEnginesOverheating |= thatPart.temperature / thatPart.maxTemp > 0.9;
                        anyEnginesFlameout |= (thatEngineModule.isActiveAndEnabled && thatEngineModule.flameout);

                        totalCurrentThrust += GetCurrentThrust(thatEngineModule);
                        float maxThrust = GetMaximumThrust(thatEngineModule);
                        totalMaximumThrust += maxThrust;
                        float realIsp = GetRealIsp(thatEngineModule);
                        if (realIsp > 0)
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

                        if (thatPart.temperature - thatAblator.ablationTempThresh > hottestShield)
                        {
                            hottestShield = (float)(thatPart.temperature - thatAblator.ablationTempThresh);
                            heatShieldTemperature = (float)(thatPart.temperature);
                            heatShieldFlux = (float)(thatPart.thermalConductionFlux + thatPart.thermalConvectionFlux + thatPart.thermalInternalFlux + thatPart.thermalRadiationFlux);
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
                actualAverageIsp = 0.0;
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
                vesselCrewMedical[i] = vesselCrew[i].KerbalRef != null ? vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>() : null;
            }

            // Part-local list is assembled somewhat differently.
            // Mental note: Actually, there's a list of ProtoCrewMember in part.protoModuleCrew. 
            // But that list loses information about seats, which is what we'd like to keep in this particular case.
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
        // Another piece from MechJeb.
        private void FetchAltitudes()
        {
            altitudeTrue = AltitudeASL - vessel.terrainAltitude;

            RaycastHit sfc;
            if (Physics.Raycast(CoM, -up, out sfc, (float)AltitudeASL + 10000.0F, 1 << 15))
            {
                slopeAngle = Vector3.Angle(up, sfc.normal);
            }
            else
            {
                slopeAngle = -1.0f;
            }

            altitudeBottom = (vessel.mainBody.ocean) ? Math.Min(AltitudeASL, altitudeTrue) : altitudeTrue;
            if (altitudeBottom < 500d)
            {
                double lowestPoint = AltitudeASL;
                foreach (Part p in vessel.parts)
                {
                    if (p.collider != null)
                    {
                        Vector3d bottomPoint = p.collider.ClosestPointOnBounds(vessel.mainBody.position);
                        double partBottomAlt = vessel.mainBody.GetAltitude(bottomPoint);
                        lowestPoint = Math.Min(lowestPoint, partBottomAlt);
                    }
                }
                lowestPoint -= AltitudeASL;
                altitudeBottom += lowestPoint;
            }
            altitudeBottom = Math.Max(0.0, altitudeBottom);
        }

        // According to C# specification, switch-case is compiled to a constant hash table.
        // So this is actually more efficient than a dictionary, who'd have thought.
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

        // This intermediary will cache the results so that multiple variable requests within the frame would not result in duplicated code.
        // If I actually break down and decide to do expressions, however primitive, this will also be the function responsible.
        public object ProcessVariable(string input, int propId)
        {
            if (resultCache[input] != null)
            {
                return resultCache[input];
            }

            bool cacheable;
            object returnValue;
            try
            {
                if (!plugins.ProcessVariable(input, out returnValue, out cacheable))
                {
                    returnValue = VariableToObject(input, propId, out cacheable);
                }
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Processing error while processing {0}: {1}", input, e.Message);
                // Most of the variables are doubles...
                return double.NaN;
            }
            if (cacheable)
            {
                resultCache.Add(input, returnValue);
                return resultCache[input];
            }
            return returnValue;
        }

        private static object CrewListElement(string element, int seatID, IList<ProtoCrewMember> crewList, IList<kerbalExpressionSystem> crewMedical)
        {
            bool exists = seatID < crewList.Count;
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

        private object VariableToObject(string input, int propId, out bool cacheable)
        {

            // Some variables may not cacheable, because they're meant to be different every time like RANDOM,
            // or immediate. they will set this flag to false.
            cacheable = true;

            // It's slightly more optimal if we take care of that before the main switch body.
            if (input.IndexOf("_", StringComparison.Ordinal) > -1)
            {
                string[] tokens = input.Split('_');

                // Strings stored in module configuration.
                if (tokens.Length == 2 && tokens[0] == "STOREDSTRING")
                {
                    int storedStringNumber;
                    if (int.TryParse(tokens[1], out storedStringNumber) && storedStringNumber >= 0 && storedStringsArray.Length > storedStringNumber)
                        return storedStringsArray[storedStringNumber];
                    return "";
                }

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
                    foreach (KeyValuePair<string, string> resourceType in systemNamedResources)
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

                // Custom variables - if the first token is CUSTOM, we'll evaluate it here
                if (tokens.Length > 1 && tokens[0] == "CUSTOM")
                {
                    if (customVariables.ContainsKey(input))
                    {
                        return customVariables[input].Evaluate(this);
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
                        return mappedVariables[input].Evaluate(this);
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
                            if (part.internalModel.props.Count == 0)
                            {
                                JUtil.LogErrorMessage(this, "How did RPM get invoked in an IVA with no props?");
                                pluginBoolVariables.Add(tokens[1], null);
                                return float.NaN;
                            }

                            if (propId >= 0 && propId < part.internalModel.props.Count)
                            {
                                propToUse = part.internalModel.props[propId];
                            }
                            else
                            {
                                propToUse = part.internalModel.props[0];
                            }
                        }

                        if (propToUse == null)
                        {
                            JUtil.LogErrorMessage(this, "Wait - propToUse is still null?");
                            pluginBoolVariables.Add(tokens[1], null);
                            return -1;
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
                                pluginBoolVariables.Add(tokens[1], null);
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

                // Prop variables - kind-of hackish.  I am probably going to get rid of this after further thought.
                if (tokens.Length > 1 && tokens[0] == "PROP")
                {
                    string substr = input.Substring("PROP".Length + 1);

                    if (persistence.HasPropVar(substr, propId))
                    {
                        cacheable = false;
                        return (float)persistence.GetPropVar(substr, propId);
                    }
                    else
                    {
                        return input;
                    }
                }

                if (tokens.Length > 1 && tokens[0] == "PERSISTENT")
                {
                    string substr = input.Substring("PERSISTENT".Length + 1);

                    if (persistence.HasVar(substr))
                    {
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

            }

            // Action group memo strings from vessel description.
            if (input.StartsWith("AGMEMO", StringComparison.Ordinal))
            {
                uint groupID;
                if (uint.TryParse(input.Substring(6), out groupID) && groupID < 10)
                {
                    string[] tokens;
                    if (actionGroupMemo[groupID].IndexOf('|') > 1 && (tokens = actionGroupMemo[groupID].Split('|')).Length == 2)
                    {
                        if (vessel.ActionGroups.groups[actionGroupID[groupID]])
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
                    return VelocityVesselSurface.magnitude;
                case "SURFSPEEDMACH":
                    // Mach number wiggles around 1e-7 when sitting in launch
                    // clamps before launch, so pull it down to zero if it's close.
                    return (vessel.mach < 0.001) ? 0.0 : vessel.mach;
                case "ORBTSPEED":
                    return VelocityVesselOrbit.magnitude;
                case "TRGTSPEED":
                    return velocityRelativeTarget.magnitude;
                case "HORZVELOCITY":
                    return horzVelocity;
                case "HORZVELOCITYFORWARD":
                    // Negate it, since this is actually movement on the Z axis,
                    // and we want to treat it as a 2D projection on the surface
                    // such that moving "forward" has a positive value.
                    return -Vector3d.Dot(VelocityVesselSurface, surfaceForward);
                case "HORZVELOCITYRIGHT":
                    return Vector3d.Dot(VelocityVesselSurface, surfaceRight);
                case "EASPEED":
                    return vessel.srf_velocity.magnitude * Math.Sqrt(vessel.atmDensity / standardAtmosphere);
                case "APPROACHSPEED":
                    return approachSpeed;
                case "SELECTEDSPEED":
                    switch (FlightUIController.speedDisplayMode)
                    {
                        case FlightUIController.SpeedDisplayModes.Orbit:
                            return VelocityVesselOrbit.magnitude;
                        case FlightUIController.SpeedDisplayModes.Surface:
                            return VelocityVesselSurface.magnitude;
                        case FlightUIController.SpeedDisplayModes.Target:
                            return velocityRelativeTarget.magnitude;
                    }
                    return double.NaN;
                case "SPEEDATIMPACT":
                    return expectedSpeedAtImpact;
                case "BESTSPEEDATIMPACT":
                    return bestPossibleSpeedAtImpact;

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
                    if (Double.IsNaN(secondsToImpact) || secondsToImpact > 365 * 24 * 60 * 60 || secondsToImpact < 0)
                        return -1d;
                    return secondsToImpact;

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
                    return (horzVelocity * horzVelocity) / (2.0 * totalMaximumThrust / totalShipWetMass);

                // Altitudes
                case "ALTITUDE":
                    return AltitudeASL;
                case "ALTITUDELOG10":
                    return JUtil.PseudoLog10(AltitudeASL);
                case "RADARALT":
                    return altitudeTrue;
                case "RADARALTLOG10":
                    return JUtil.PseudoLog10(altitudeTrue);
                case "RADARALTOCEAN":
                    if (vessel.mainBody.ocean)
                    {
                        return Math.Min(AltitudeASL, altitudeTrue);
                    }
                    return altitudeTrue;
                case "RADARALTOCEANLOG10":
                    if (vessel.mainBody.ocean)
                    {
                        return JUtil.PseudoLog10(Math.Min(AltitudeASL, altitudeTrue));
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
                    return vessel.orbit.referenceBody.atmosphereDepth - AltitudeASL;

                // Atmospheric values
                case "ATMPRESSURE":
                    return vessel.staticPressurekPa * PhysicsGlobals.KpaToAtmospheres;
                case "ATMDENSITY":
                    return vessel.atmDensity;
                case "DYNAMICPRESSURE":
                    return vessel.dynamicPressurekPa * 1000.0;
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
                    return totalCurrentThrust;
                case "THRUSTMAX":
                    return totalMaximumThrust;
                case "TWR":
                    return totalCurrentThrust / (totalShipWetMass * localGeeASL);
                case "TWRMAX":
                    return totalMaximumThrust / (totalShipWetMass * localGeeASL);
                case "ACCEL":
                    return totalCurrentThrust / totalShipWetMass;
                case "MAXACCEL":
                    return totalMaximumThrust / totalShipWetMass;
                case "GFORCE":
                    return vessel.geeForce_immediate;
                case "EFFECTIVEACCEL":
                    return vessel.acceleration.magnitude;
                case "REALISP":
                    return actualAverageIsp;
                case "HOVERPOINT":
                    return (localGeeDirect / (totalMaximumThrust / totalShipWetMass)).Clamp(0d, 1d);
                case "HOVERPOINTEXISTS":
                    return (localGeeDirect / (totalMaximumThrust / totalShipWetMass)) > 1 ? -1d : 1d;
                case "EFFECTIVETHROTTLE":
                    return (totalMaximumThrust > 0.0) ? (totalCurrentThrust / totalMaximumThrust) : 0.0;

                // Maneuvers
                case "MNODETIMESECS":
                    if (node != null)
                    {
                        return -(node.UT - Time);
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
                        return vessel.orbit.TimeOfAscendingNodeEquatorial(Time) - Time;
                    return double.NaN;
                case "TIMETODNEQUATORIAL":
                    if (orbitSensibility && vessel.orbit.DescendingNodeEquatorialExists())
                        return vessel.orbit.TimeOfDescendingNodeEquatorial(Time) - Time;
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
                        return vessel.orbit.UTsoi - Time;
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
                        return Time + 426 * 6 * 60 * 60;
                    }
                    return Time + 365 * 24 * 60 * 60;
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
                        return "Port";
                    if (targetBody != null)
                        return "Celestial";
                    return "Position";

                // Coordinates.
                case "LATITUDE":
                    return vessel.mainBody.GetLatitude(CoM);
                case "LONGITUDE":
                    return JUtil.ClampDegrees180(vessel.mainBody.GetLongitude(CoM));
                case "LATITUDETGT":
                    // These targetables definitely don't have any coordinates.
                    if (target == null || target is CelestialBody)
                        return double.NaN;
                    // These definitely do.
                    if (target is Vessel || target is ModuleDockingNode)
                        return target.GetVessel().mainBody.GetLatitude(target.GetTransform().position);
                    // We're going to take a guess here and expect MechJeb's PositionTarget and DirectionTarget,
                    // which don't have vessel structures but do have a transform.
                    return vessel.mainBody.GetLatitude(target.GetTransform().position);
                case "LONGITUDETGT":
                    if (target == null || target is CelestialBody)
                        return double.NaN;
                    if (target is Vessel || target is ModuleDockingNode)
                        return JUtil.ClampDegrees180(target.GetVessel().mainBody.GetLongitude(target.GetTransform().position));
                    return vessel.mainBody.GetLongitude(target.GetTransform().position);

                // Orientation
                case "HEADING":
                    return rotationVesselSurface.eulerAngles.y;
                case "PITCH":
                    return (rotationVesselSurface.eulerAngles.x > 180) ? (360.0 - rotationVesselSurface.eulerAngles.x) : -rotationVesselSurface.eulerAngles.x;
                case "ROLL":
                    return (rotationVesselSurface.eulerAngles.z > 180) ? (rotationVesselSurface.eulerAngles.z - 360.0) : rotationVesselSurface.eulerAngles.z;

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
                        return -1d;
                    if (targetVessel != null)
                    {
                        return targetVessel.mainBody.GetAltitude(targetVessel.CoM);
                    }
                    if (targetOrbit != null)
                    {
                        return targetOrbit.altitude;
                    }
                    return -1d;
                case "TARGETSEMIMAJORAXIS":
                    if (target == null)
                        return double.NaN;
                    if (targetOrbit != null)
                        return targetOrbit.semiMajorAxis;
                    return double.NaN;
                case "TIMETOANWITHTARGETSECS":
                    if (target == null || targetOrbit == null)
                        return double.NaN;
                    return vessel.GetOrbit().TimeOfAscendingNode(targetOrbit, Time) - Time;
                case "TIMETODNWITHTARGETSECS":
                    if (target == null || targetOrbit == null)
                        return double.NaN;
                    return vessel.GetOrbit().TimeOfDescendingNode(targetOrbit, Time) - Time;
                case "TARGETCLOSESTAPPROACHTIME":
                    if (target == null || targetOrbit == null || orbitSensibility == false)
                    {
                        return double.NaN;
                    }
                    else
                    {
                        double approachTime, approachDistance;
                        approachDistance = JUtil.GetClosestApproach(vessel.GetOrbit(), target, out approachTime);
                        return approachTime - Time;
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
                        return Math.Max(Time - targetVessel.DiscoveryInfo.lastObservedTime, 0.0);
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
                            return JUtil.NormalAngle(-target.GetFwdVector(), Forward, up);
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
                            JUtil.NormalAngle(-target.GetFwdVector(), Forward, -Right);
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
                            return JUtil.NormalAngle(target.GetTransform().up, up, -Forward);
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
                    protractor.Update(vessel, targetOrbit);
                    return protractor.PhaseAngle;
                case "TARGETBODYPHASEANGLESECS":
                    protractor.Update(vessel, targetOrbit);
                    return protractor.TimeToPhaseAngle;
                case "TARGETBODYEJECTIONANGLE":
                    protractor.Update(vessel, targetOrbit);
                    return protractor.EjectionAngle;
                case "TARGETBODYEJECTIONANGLESECS":
                    protractor.Update(vessel, targetOrbit);
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
                    protractor.Update(vessel, targetOrbit);
                    return protractor.MoonEjectionAngle;
                case "TARGETBODYEJECTIONALTITUDE":
                    protractor.Update(vessel, targetOrbit);
                    return protractor.EjectionAltitude;
                case "TARGETBODYDELTAV":
                    protractor.Update(vessel, targetOrbit);
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
                    return part.temperature + KelvinToCelsius;
                case "PODTEMPERATUREKELVIN":
                    return part.temperature;
                case "PODMAXTEMPERATURE":
                    return part.maxTemp + KelvinToCelsius;
                case "PODMAXTEMPERATUREKELVIN":
                    return part.maxTemp;
                case "PODNETFLUX":
                    return part.thermalConductionFlux + part.thermalConvectionFlux + part.thermalInternalFlux + part.thermalRadiationFlux;
                case "EXTERNALTEMPERATURE":
                    return vessel.externalTemperature + KelvinToCelsius;
                case "EXTERNALTEMPERATUREKELVIN":
                    return vessel.externalTemperature;
                case "HEATSHIELDTEMPERATURE":
                    return (double)heatShieldTemperature + KelvinToCelsius;
                case "HEATSHIELDTEMPERATUREKELVIN":
                    return heatShieldTemperature;
                case "HEATSHIELDFLUX":
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
                    return (speedVerticalRounded < 0 && !vessel.ActionGroups.groups[gearGroupNumber] && altitudeBottom < 100).GetHashCode();
                case "GROUNDPROXIMITYALARM":
                    // Returns 1 if, at maximum acceleration, in the time remaining until ground impact, it is impossible to get a vertical speed higher than -10m/s.
                    return (bestPossibleSpeedAtImpact < -10d).GetHashCode();
                case "TUMBLEALARM":
                    return (speedVerticalRounded < 0 && altitudeBottom < 100 && horzVelocity > 5).GetHashCode();
                case "SLOPEALARM":
                    return (speedVerticalRounded < 0 && altitudeBottom < 100 && slopeAngle > 15).GetHashCode();
                case "DOCKINGANGLEALARM":
                    return (targetDockingNode != null && targetDistance < 10 && approachSpeed > 0 &&
                    (Math.Abs(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), Forward, up)) > 1.5 ||
                    Math.Abs(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), Forward, -Right)) > 1.5)).GetHashCode();
                case "DOCKINGSPEEDALARM":
                    return (targetDockingNode != null && approachSpeed > 2.5 && targetDistance < 15).GetHashCode();
                case "ALTITUDEALARM":
                    return (speedVerticalRounded < 0 && altitudeBottom < 150).GetHashCode();
                case "PODTEMPERATUREALARM":
                    double tempRatio = part.temperature / part.maxTemp;
                    if (tempRatio > 0.85d)
                        return 1d;
                    if (tempRatio > 0.75d)
                        return 0d;
                    return -1d;
                // Well, it's not a compound but it's an alarm...
                case "ENGINEOVERHEATALARM":
                    return anyEnginesOverheating.GetHashCode();
                case "ENGINEFLAMEOUTALARM":
                    return anyEnginesFlameout.GetHashCode();
                case "IMPACTALARM":
                    return (VelocityVesselSurface.magnitude > part.crashTolerance).GetHashCode();


                // SCIENCE!!
                case "SCIENCEDATA":
                    return totalDataAmount;
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
                    return vessel.ActionGroups.groups[gearGroupNumber].GetHashCode();
                case "BRAKES":
                    return vessel.ActionGroups.groups[brakeGroupNumber].GetHashCode();
                case "SAS":
                    return vessel.ActionGroups.groups[sasGroupNumber].GetHashCode();
                case "LIGHTS":
                    return vessel.ActionGroups.groups[lightGroupNumber].GetHashCode();
                case "RCS":
                    return vessel.ActionGroups.groups[rcsGroupNumber].GetHashCode();

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

            // Didn't recognise anything so we return the string we got, that helps debugging.
            return input;
        }

        //--- Fallback evaluators
        #region FallbackEvaluators
        private double FallbackEvaluateDeltaV()
        {
            return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(false)));
        }

        private double FallbackEvaluateDeltaVStage()
        {
            return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(true)));
        }

        private double FallbackTerminalVelocity()
        {
            // Terminal velocity computation based on MechJeb 2.5.1 or one of the later snapshots
            if (AltitudeASL > vessel.mainBody.RealMaxAtmosphereAltitude())
            {
                return float.MaxValue;
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

            return Math.Sqrt(localGeeDirect / drag) * vessel.srf_velocity.magnitude;
        }
        #endregion

        //--- Plugin-enabled evaluators
        #region PluginEvaluators
        private double DeltaV()
        {
            if (evaluateDeltaV == null)
            {
                Func<double> accessor = null;

                accessor = (Func<double>)GetMethod("JSIMechJeb:GetDeltaV", part.internalModel.props[0], typeof(Func<double>));
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

                accessor = (Func<double>)GetMethod("JSIMechJeb:GetStageDeltaV", part.internalModel.props[0], typeof(Func<double>));
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

        private double LandingError()
        {
            if (evaluateLandingError == null)
            {
                evaluateLandingError = (Func<double>)GetMethod("JSIMechJeb:GetLandingError", part.internalModel.props[0], typeof(Func<double>));
            }

            return evaluateLandingError();
        }

        private double LandingAltitude()
        {
            if (evaluateLandingAltitude == null)
            {
                evaluateLandingAltitude = (Func<double>)GetMethod("JSIMechJeb:GetLandingAltitude", part.internalModel.props[0], typeof(Func<double>));
            }

            return evaluateLandingAltitude();
        }

        private double LandingLatitude()
        {
            if (evaluateLandingLatitude == null)
            {
                evaluateLandingLatitude = (Func<double>)GetMethod("JSIMechJeb:GetLandingLatitude", part.internalModel.props[0], typeof(Func<double>));
            }

            return evaluateLandingLatitude();
        }

        private double LandingLongitude()
        {
            if (evaluateLandingLongitude == null)
            {
                evaluateLandingLongitude = (Func<double>)GetMethod("JSIMechJeb:GetLandingLongitude", part.internalModel.props[0], typeof(Func<double>));
            }

            return evaluateLandingLongitude();
        }

        private bool MechJebAvailable()
        {
            if (evaluateMechJebAvailable == null)
            {
                Func<bool> accessor = null;

                accessor = (Func<bool>)GetMethod("JSIMechJeb:GetMechJebAvailable", part.internalModel.props[0], typeof(Func<bool>));
                if (accessor == null)
                {
                    accessor = JUtil.ReturnFalse;
                }

                evaluateMechJebAvailable = accessor;
            }

            return evaluateMechJebAvailable();
        }

        private double TerminalVelocity()
        {
            if (evaluateTerminalVelocity == null)
            {
                evaluateTerminalVelocity = FallbackTerminalVelocity;
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

