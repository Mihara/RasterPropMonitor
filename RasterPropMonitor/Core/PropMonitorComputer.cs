using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Diagnostics;

namespace JSI
{
	public class RasterPropMonitorComputer: PartModule
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
		private Vector3d coM;
		private Vector3d up;
		private Vector3d forward;
		private Vector3d right;
		private Vector3d north;
		private Quaternion rotationVesselSurface;
		private Quaternion rotationSurface;
		private Vector3d velocityVesselSurface;
		private Vector3d velocityVesselOrbit;
		private Vector3d velocityRelativeTarget;
		private double speedVertical;
		private double speedVerticalRounded;
		private double horzVelocity, horzVelocityForward, horzVelocityRight;
		private ITargetable target;
		private ModuleDockingNode targetDockingNode;
		private Vessel targetVessel;
		private double targetDistance;
		private Vector3d targetSeparation;
		private double approachSpeed;
		private Quaternion targetOrientation;
		private ManeuverNode node;
		private double time;
		private ProtoCrewMember[] vesselCrew;
		private kerbalExpressionSystem[] vesselCrewMedical;
		private ProtoCrewMember[] localCrew;
		private kerbalExpressionSystem[] localCrewMedical;
		private double altitudeASL;
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
		private double totalDataAmount;
		private double secondsToImpact;
		private double bestPossibleSpeedAtImpact, expectedSpeedAtImpact;
		private double localGeeASL, localGeeDirect;
		private double standardAtmosphere;
		private double slopeAngle;
		private double atmPressure;
		private double dynamicPressure;
		private readonly double upperAtmosphereLimit = Math.Log(100000);
		private CelestialBody targetBody;
		private double phaseAngle;
		private double timeToPhaseAngle;
		private double ejectionAngle;
		private double timeToEjectionAngle;
		private double targetClosestApproach;
		private double targetTimeAtClosestApproach;
		private double moonEjectionAngle;
		private double ejectionAltitude;
		private double targetBodyDeltaV;
		private double lastTimePerSecond;
		private double terrainHeight, lastTerrainHeight, terrainDelta;
		private ExternalVariableHandlers plugins;
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

		// Now this whole mess can get deprecated.
		private readonly Dictionary<string,string> namedResources = new Dictionary<string,string> {
			// Stock resources...
			{ "ELECTRIC", "ElectricCharge" },
			{ "FUEL", "LiquidFuel" },
			{ "OXIDIZER", "Oxidizer" },
			{ "MONOPROP", "MonoPropellant" },
			{ "RSINTAKEAIR", "IntakeAir" },
			{ "XENON", "XenonGas" },
			{ "SOLIDFUEL", "SolidFuel" },
			// Mod resources...
			{ "KETHANE", "Kethane" },
			// Modular fuels.
			{ "MFLH2", "LiquidH2" },
			{ "MFLOX", "LiquidOxygen" },
			{ "MFN2O4", "N2O4" },
			{ "MFMMH", "MMH" },
			{ "MFAEROZINE", "Aerozine" },
			{ "MFUDMH", "UDMH" },
			{ "MFHYDRAZINE", "Hydrazine" },
			{ "MFMETHANE", "Methane" },
			{ "MFNUCLEARFUEL", "nuclearFuel" },
			{ "MFNUCLEARWASTE", "nuclearWaste" },
			// Life support resources -- apparently common for TAC and Ioncross these days.
			{ "LSFOOD","Food" },
			{ "LSWATER","Water" },
			{ "LSOXYGEN","Oxygen" },
			{ "LSCO2","CarbonDioxide" },
			{ "LSWASTE","Waste" },
			{ "LSWASTEWATER","WasteWater" },
			// ECLSS resources
			{ "ECLSSCO2","CO2" },
			{ "ECLSSO2C","O2 Candle" },
			// Deadly reentry ablative shielding
			{ "ABLATIVESHIELD","AblativeShielding" },
			// Interstellar
			{ "ISTTHERMALPOWER","ThermalPower" },
			{ "ISTMEGAJOULES","Megajoules" },
			{ "ISTANTIMATTER","Antimatter" },
			{ "ISTINTAKEATM","IntakeAtm" },
			{ "ISTUF4","UF4" },
			{ "ISTTHF4","ThF4" },
			{ "ISTACTINIDES","Actinides" },
			{ "ISTDEPLETEDFUEL","DepletedFuel" },
			{ "ISTSCIENCE","Science" },
			{ "ISTVACUUMPLASMA", "VacuumPlasma" },
			{ "ISTARGON", "Argon" },
			{ "ISTALIMINIUM", "Aluminium" },
			{ "ISTEXOTICMATTER", "ExoticMatter" },
			{ "ISTDEUTERIUM", "Deuterium" },
			{ "ISTLITHIUM", "Lithium" },
			{ "ISTTRITIUM", "Tritium" },
			{ "ISTWASTEHEAT", "WasteHeat" },
			{ "ISTLQDMETHANE", "LqdMethane" },
			// Launchpads
			{ "ELPROCKETPARTS","RocketParts" },
			{ "ELPMETAL", "Metal" },
			{ "ELPORE", "Ore" },
			// Near Future
			{ "NFARGON","ArgonGas" },
			{ "NFHYDROGEN","HydrogenGas" },
			{ "NFPTFE", "Polytetrafluoroethylene" },
			{ "NFSTOREDCHARGE","StoredCharge" },
			{ "NFURANIUM","EnrichedUranium" },
			{ "NFDEPLETEDURANIUM","DepletedUranium" },
		};

		// Ok, this is to deprecate the named resources mechanic entirely...
		private static SortedDictionary<string,string> systemNamedResources;

		private static List<string> knownLoadedAssemblies;

		private string[] storedStringsArray;

		// Processing cache!
		private readonly DefaultableDictionary<string,object> resultCache = new DefaultableDictionary<string,object>(null);
		// Public functions:
		// Request the instance, create it if one doesn't exist:
		public static RasterPropMonitorComputer Instantiate(MonoBehaviour referenceLocation)
		{
			var thatProp = referenceLocation as InternalProp;
			var thatPart = referenceLocation as Part;
			if (thatPart == null) {
				if (thatProp == null)
					throw new ArgumentException("Cannot instantiate RPMC in this location.");
				thatPart = thatProp.part;
			}
			for (int i = 0; i < thatPart.Modules.Count; i++)
				if (thatPart.Modules[i].ClassName == typeof(RasterPropMonitorComputer).Name) {
					var other = thatPart.Modules[i] as RasterPropMonitorComputer;
					return other;
				}
			return thatPart.AddModule(typeof(RasterPropMonitorComputer).Name) as RasterPropMonitorComputer;
		}
		// Set refresh rates.
		public void UpdateRefreshRates(int rate, int dataRate)
		{
			refreshTextRate = Math.Min(rate, refreshTextRate);
			refreshDataRate = Math.Min(dataRate, refreshDataRate);
		}
		// Internal persistence interface:
		public void SetVar(string varname, int value)
		{
			var variables = ParseData(data);
			try {
				variables.Add(varname, value);
			} catch (ArgumentException) {
				variables[varname] = value;
			}
			data = UnparseData(variables);
		}

		public int? GetVar(string varname)
		{
			var variables = ParseData(data);
			return variables.ContainsKey(varname) ? (int?)variables[varname] : (int?)null;
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

			if (!HighLogic.LoadedSceneIsEditor) {

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

				for (int i = 0; i < 10; i++) {
					actionGroupID[i] = BaseAction.GetGroupIndex(customGroups[i]);
				}

				FetchPerPartData();
				standardAtmosphere = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(0, FlightGlobals.Bodies[1]));

				// Let's deal with the system resource library.
				// This dictionary is sorted so that longer names go first to prevent false identification - they're compared in order.
				systemNamedResources = new SortedDictionary<string,string>(new ResourceNameLengthComparer());
				foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions) {
					string varname = thatResource.name.ToUpperInvariant().Replace(' ', '-').Replace('_', '-');
					systemNamedResources.Add(varname, thatResource.name);
					JUtil.LogMessage(this, "Remembering system resource {1} as SYSR_{0}", varname, thatResource.name);
				}

				// Now let's collect a list of all assemblies loaded on the system.

				knownLoadedAssemblies = new List<string>();
				foreach (AssemblyLoader.LoadedAssembly thatAssembly in AssemblyLoader.loadedAssemblies) {
					string thatName = thatAssembly.assembly.GetName().Name;
					knownLoadedAssemblies.Add(thatName.ToUpper());
					JUtil.LogMessage(this, "I know that {0} ISLOADED_{1}", thatName, thatName.ToUpper());
				}

				// Now let's parse our stored strings...

				if (!string.IsNullOrEmpty(storedStrings)) {
					storedStringsArray = storedStrings.Split('|');
				}

				// We instantiate plugins late.
				plugins = new ExternalVariableHandlers(this);

			}
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (state != StartState.Editor) {
				// Parse vessel description here for special lines:
					
				string[] descriptionStrings = vesselDescription.UnMangleConfigText().Split(JUtil.LineSeparator, StringSplitOptions.None);
				for (int i = 0; i < descriptionStrings.Length; i++) {
					if (descriptionStrings[i].StartsWith("AG", StringComparison.Ordinal) && descriptionStrings[i][3] == '=') {
						uint groupID;
						if (uint.TryParse(descriptionStrings[i][2].ToString(), out groupID)) {
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
			if (vesselNumParts != vessel.Parts.Count || updateCountdown <= 0 || dataUpdateCountdown <= 0 || updateForced) {
				updateCountdown = refreshTextRate;
				if (vesselNumParts != vessel.Parts.Count || dataUpdateCountdown <= 0 || updateForced) {
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

		private static string UnparseData(Dictionary<string,int> variables)
		{
			var tokens = new List<string>();
			foreach (KeyValuePair<string,int> item in variables) {
				tokens.Add(item.Key + "$" + item.Value);
			}
			return String.Join("|", tokens.ToArray());
		}

		private static Dictionary<string,int> ParseData(string dataString)
		{
			var variables = new Dictionary<string,int>();
			if (!string.IsNullOrEmpty(dataString))
				foreach (string varstring in dataString.Split ('|')) {
					string[] tokens = varstring.Split('$');
					int value;
					int.TryParse(tokens[1], out value);
					variables.Add(tokens[0], value);
				}

			return variables;

		}
		// I don't remember why exactly, but I think it has to be out of OnUpdate to work in editor...
		public void Update()
		{
			if (HighLogic.LoadedSceneIsEditor) {
				// well, it looks sometimes it might become null..

				// For some unclear reason, the newline in this case is always 0A, rather than Environment.NewLine.
				vesselDescription = EditorLogic.fetch.shipDescriptionField != null ? EditorLogic.fetch.shipDescriptionField.Text.Replace(editorNewline, "$$$") : string.Empty;
			}
		}

		public override void OnUpdate()
		{
			if (!JUtil.IsActiveVessel(vessel))
				return;

			if (!UpdateCheck())
				return;

			// We clear the cache every frame.
			resultCache.Clear();

			FetchCommonData();
			UpdateTransferAngles();
		}
		// Update phase angle, ejection angle, and closest approach values.
		// Code derived from the Protractor plug-in.
		private void UpdateTransferAngles()
		{
			// MOARdV: Warning: I haven't actually tested this with a docking port...
			if (targetOrbitSensibility) {
				bool isSimpleTransfer;
				Orbit orbitOfOrigin;
				Orbit orbitOfDestination;
				int upshiftLevels;

				FindProtractorOrbitParameters(vessel.orbit, targetOrbit, out isSimpleTransfer, out orbitOfOrigin, out orbitOfDestination, out upshiftLevels);

				double delta_theta = 0.0;
				if (isSimpleTransfer) {
					// Simple transfer: we orbit the same referenceBody as the target.
					phaseAngle = UpdatePhaseAngleSimple(orbitOfOrigin, orbitOfDestination);
					delta_theta = (360.0 / orbitOfOrigin.period) - (360.0 / orbitOfDestination.period);

					ejectionAngle = -1.0;

					moonEjectionAngle = -1.0;
					ejectionAltitude = -1.0;

					targetBodyDeltaV = CalculateDeltaV(orbitOfDestination);
				} else if (upshiftLevels == 1) {
					// Our referenceBody orbits the same thing as our target.
					phaseAngle = UpdatePhaseAngleAdjacent(orbitOfOrigin, orbitOfDestination);
					delta_theta = (360.0 / orbitOfOrigin.period) - (360.0 / orbitOfDestination.period);

					ejectionAngle = (CalculateDesiredEjectionAngle(vessel.mainBody, orbitOfDestination) - CurrentEjectAngle() + 360.0) % 360.0;

					moonEjectionAngle = -1.0;
					ejectionAltitude = -1.0;

					targetBodyDeltaV = CalculateDeltaV(orbitOfDestination);
				} else if (upshiftLevels == 2) {
					// Our referenceBody is a moon and we're doing an Oberth transfer.
					phaseAngle = UpdatePhaseAngleOberth(orbitOfOrigin, orbitOfDestination);
					delta_theta = (360.0 / orbitOfOrigin.period) - (360.0 / orbitOfDestination.period);

					ejectionAngle = -1.0;

					moonEjectionAngle = (MoonAngle() - CurrentEjectAngle() + 360.0) % 360.0;
					ejectionAltitude = 1.05 * vessel.mainBody.referenceBody.maxAtmosphereAltitude;
					targetBodyDeltaV = CalculateDeltaV(orbitOfDestination);
				} else {
					// What case does this cover?  I *think* it can't happen.
					phaseAngle = -1.0;
					ejectionAngle = -1.0;
					moonEjectionAngle = -1.0;
					ejectionAltitude = -1.0;
					targetBodyDeltaV = -1.0;
				}

				if (phaseAngle >= 0.0) {
					if (delta_theta > 0.0) {
						timeToPhaseAngle = phaseAngle / delta_theta;
					} else {
						timeToPhaseAngle = Math.Abs((360.0 - phaseAngle) / delta_theta);
					}
				} else {
					timeToPhaseAngle = -1.0;
				}

				if (ejectionAngle >= 0.0) {
					timeToEjectionAngle = ejectionAngle * vessel.orbit.period / 360.0;
				} else {
					timeToEjectionAngle = -1.0;
				}

				if (targetBody != null) {
					targetClosestApproach = JUtil.GetClosestApproach(vessel.orbit, targetBody, out targetTimeAtClosestApproach);
				} else if (targetDockingNode != null) {
					targetClosestApproach = JUtil.GetClosestApproach(vessel.orbit, targetDockingNode.GetVessel().GetOrbit(), out targetTimeAtClosestApproach);

				} else {
					if (targetVessel == null) {
						// Analysis disable once NotResolvedInText
						throw new ArgumentNullException("RasterPropMonitorComputer: Updating closest approach, but all appropriate targets are null");
					}
					targetClosestApproach = JUtil.GetClosestApproach(vessel.orbit, targetOrbit, out targetTimeAtClosestApproach);
				}
			} else {
				// We ain't targetin' nothin'...
				phaseAngle = -1.0;
				timeToPhaseAngle = -1.0;
				ejectionAngle = -1.0;
				timeToEjectionAngle = -1.0;
				targetClosestApproach = -1.0;
				targetTimeAtClosestApproach = -1.0;
				moonEjectionAngle = -1.0;
				ejectionAltitude = -1.0;
				targetBodyDeltaV = -1.0;

				// unless maybe a landed vessel
				if (orbitSensibility && targetVessel != null && targetVessel.LandedOrSplashed)
					targetOrbit = JUtil.ClosestApproachSrfOrbit(vessel.orbit, targetVessel, out targetTimeAtClosestApproach, out targetClosestApproach);
			}
		}
		//--- Protractor utility methods
		/// <summary>
		/// FindProtractorOrbitParameters takes the current vessel orbit, and
		/// the orbit of the target vessel / body, and it determines the
		/// parameters needed for computing the phase angle, ejection angle,
		/// and moon ejection angle (where appropriate).
		/// </summary>
		/// <param name="vesselOrbit"></param>
		/// <param name="targetOrbit"></param>
		/// <param name="isSimpleTransfer"></param>
		/// <param name="newVesselOrbit"></param>
		/// <param name="newTargetOrbit"></param>
		/// <param name="upshiftLevels"></param>
		static private void FindProtractorOrbitParameters(Orbit vesselOrbit, Orbit targetOrbit,
		                                                  out bool isSimpleTransfer, out Orbit newVesselOrbit, out Orbit newTargetOrbit,
		                                                  out int upshiftLevels)
		{
			// Test for the early out case
			if (vesselOrbit.referenceBody == targetOrbit.referenceBody) {
				// Target orbits the same body we do.
				isSimpleTransfer = true;
				newVesselOrbit = vesselOrbit;
				newTargetOrbit = targetOrbit;
				upshiftLevels = 0;
			} else if (vesselOrbit.referenceBody == Planetarium.fetch.Sun) {
				// We orbit the sun.  We need the target's sun-orbiting
				// parameters.
				isSimpleTransfer = true;
				newVesselOrbit = vesselOrbit;
				newTargetOrbit = GetSunOrbit(targetOrbit);
				upshiftLevels = 0;
			} else {
				// Not a simple case.
				int vesselDistFromSun = GetDistanceFromSun(vesselOrbit);
				int targetDistFromSun = GetDistanceFromSun(targetOrbit);
				isSimpleTransfer = false;

				if (targetDistFromSun == 0) {
					// Target orbits the sun.
					newVesselOrbit = GetReferencePlanet(vesselOrbit).GetOrbit();
					newTargetOrbit = targetOrbit;
					upshiftLevels = vesselDistFromSun;
				} else if (GetReferencePlanet(vesselOrbit) != GetReferencePlanet(targetOrbit)) {
					// Interplanetary transfer
					newVesselOrbit = GetReferencePlanet(vesselOrbit).GetOrbit();
					newTargetOrbit = GetReferencePlanet(targetOrbit).GetOrbit();
					upshiftLevels = vesselDistFromSun;
				} else {
					// vessel and target are in the same planetary system.
					--vesselDistFromSun;
					--targetDistFromSun;
					if (vesselDistFromSun == 0) {
						// Vessel orbits the planet; the target *must* orbit a
						// moon, or we would have found it in a previous case.
						if (targetDistFromSun != 1) {
							throw new ArithmeticException("RasterPropMonitorComputer::FindProtractorOrbitParameters(): vessel and target are in the same planetary system, but the target isn't orbiting a moon (but should be).");
						}

						newVesselOrbit = vesselOrbit;
						newTargetOrbit = targetOrbit.referenceBody.GetOrbit();
						isSimpleTransfer = true;
					} else {
						// Vessel is orbiting a moon; target is either a moon,
						// or a vessel orbiting a moon.
						newVesselOrbit = vesselOrbit.referenceBody.GetOrbit();
						newTargetOrbit = (targetDistFromSun == 1) ? targetOrbit.referenceBody.GetOrbit() : targetOrbit;
					}
					upshiftLevels = vesselDistFromSun;
				}
			}
		}

		private static CelestialBody GetReferencePlanet(Orbit o)
		{
			if (o.referenceBody == Planetarium.fetch.Sun) {
				// I think this shouldn't happen...
				return o.referenceBody;
			}
			// Orbit is around a planet or a moon?
			return o.referenceBody.GetOrbit().referenceBody == Planetarium.fetch.Sun ? o.referenceBody : o.referenceBody.GetOrbit().referenceBody;
		}

		/// <summary>
		/// Counts how many reference bodies there are between the supplied
		/// orbit and the sun.  0 indicates the orbit is around the sun, 1
		/// indicates the orbit is around a planet, and 2 indicates the orbit
		/// is around a moon.
		/// </summary>
		/// <param name="startingOrbit"></param>
		/// <returns></returns>
		static private int GetDistanceFromSun(Orbit startingOrbit)
		{
			int count = 0;
			while (startingOrbit.referenceBody != Planetarium.fetch.Sun) {
				++count;
				startingOrbit = startingOrbit.referenceBody.GetOrbit();
			}
			return count;
		}

		/// <summary>
		/// GetSunOrbit walks up the given orbit's referenceBody chain to
		/// return the parent orbit that
		/// </summary>
		/// <param name="orbitOfOrigin"></param>
		/// <returns></returns>
		static private Orbit GetSunOrbit(Orbit orbitOfOrigin)
		{
			while (orbitOfOrigin.referenceBody != Planetarium.fetch.Sun) {
				orbitOfOrigin = orbitOfOrigin.referenceBody.GetOrbit();
				if (orbitOfOrigin == null) {
					throw new ArithmeticException("RasterPropMonitorComputer::GetSunOrbit() could not find a solar orbit.");
				}
			}

			return orbitOfOrigin;
		}

		private double CalculateDeltaV(Orbit destOrbit)    //calculates ejection v to reach destination
		{
			if (vessel.mainBody == destOrbit.referenceBody) {
				double radius = destOrbit.referenceBody.Radius;
				double u = destOrbit.referenceBody.gravParameter;
				double d_alt = CalcMeanAlt(destOrbit);
				double alt = (vessel.mainBody.GetAltitude(vessel.findWorldCenterOfMass())) + radius;
				double v = Math.Sqrt(u / alt) * (Math.Sqrt((2 * d_alt) / (alt + d_alt)) - 1);
				return Math.Abs((Math.Sqrt(u / alt) + v) - vessel.orbit.GetVel().magnitude);
			} else {
				CelestialBody orig = vessel.mainBody;
				double d_alt = CalcMeanAlt(destOrbit);
				double o_radius = orig.Radius;
				double u = orig.referenceBody.gravParameter;
				double o_mu = orig.gravParameter;
				double o_soi = orig.sphereOfInfluence;
				double o_alt = CalcMeanAlt(orig.orbit);
				double exitalt = o_alt + o_soi;
				double v2 = Math.Sqrt(u / exitalt) * (Math.Sqrt((2 * d_alt) / (exitalt + d_alt)) - 1);
				double r = o_radius + (vessel.mainBody.GetAltitude(vessel.findWorldCenterOfMass()));
				double v = Math.Sqrt((r * (o_soi * v2 * v2 - 2 * o_mu) + 2 * o_soi * o_mu) / (r * o_soi));
				return Math.Abs(v - vessel.orbit.GetVel().magnitude);
			}
		}

		private double MoonAngle()  //calculates eject angle for moon -> planet in preparation for planet -> planet transfer
		{
			CelestialBody orig = vessel.mainBody;
			double o_alt = CalcMeanAlt(orig.orbit);
			double d_alt = (orig.orbit.referenceBody.Radius + orig.orbit.referenceBody.maxAtmosphereAltitude) * 1.05;
			double o_soi = orig.sphereOfInfluence;
			double o_radius = orig.Radius;
			double o_mu = orig.gravParameter;
			double u = orig.referenceBody.gravParameter;
			double exitalt = o_alt + o_soi;
			double v2 = Math.Sqrt(u / exitalt) * (Math.Sqrt((2.0 * d_alt) / (exitalt + d_alt)) - 1.0);
			double r = o_radius + (orig.GetAltitude(vessel.findWorldCenterOfMass()));
			double v = Math.Sqrt((r * (o_soi * v2 * v2 - 2.0 * o_mu) + 2 * o_soi * o_mu) / (r * o_soi));
			double eta = Math.Abs(v * v / 2.0 - o_mu / r);
			double h = r * v;
			double e = Math.Sqrt(1.0 + ((2.0 * eta * h * h) / (o_mu * o_mu)));
			double eject = (180.0 - (Math.Acos(1.0 / e) * (180.0 / Math.PI))) % 360.0;

			eject = (o_alt > d_alt) ? (180.0 - eject) : (360.0 - eject);

			return (vessel.orbit.inclination > 90.0 && !(vessel.Landed)) ? (360.0 - eject) : eject;
		}
		// Simple phase angle: transfer from sun -> planet or planet -> moon
		private double UpdatePhaseAngleSimple(Orbit srcOrbit, Orbit destOrbit)
		{
			if (destOrbit == null) {
				JUtil.LogMessage(this, "!!! UpdatePhaseAngleSimple got a NULL orbit !!!");
				return 0.0;
			}

			// MOARdV TODO: Can this be made more accurate using the orbit
			// altitude at the point of intercept?
			double destAlt = CalcMeanAlt(destOrbit);

			double phase = CurrentPhase(srcOrbit, destOrbit) - DesiredPhase(altitudeASL + vessel.mainBody.Radius, destAlt, vessel.mainBody.gravParameter);
			phase = (phase + 360.0) % 360.0;

			return phase;
		}
		// Adjacent phase angle: transfer planet -> planet or moon -> moon
		private double UpdatePhaseAngleAdjacent(Orbit srcOrbit, Orbit destOrbit)
		{
			if (destOrbit == null) {
				JUtil.LogMessage(this, "!!! UpdatePhaseAngleAdjacent got a NULL orbit !!!");
				return 0.0;
			}

			double srcAlt = CalcMeanAlt(srcOrbit);
			double destAlt = CalcMeanAlt(destOrbit);

			double phase = CurrentPhase(srcOrbit, destOrbit) - DesiredPhase(srcAlt, destAlt, vessel.mainBody.gravParameter);
			phase = (phase + 360.0) % 360.0;

			return phase;
		}
		// Oberth phase angle: transfer moon -> another planet
		private double UpdatePhaseAngleOberth(Orbit srcOrbit, Orbit destOrbit)
		{
			if (destOrbit == null) {
				JUtil.LogMessage(this, "!!! UpdatePhaseAngleOberth got a NULL orbit !!!");
				return 0.0;
			}

			//double srcAlt = CalcMeanAlt(srcOrbit);
			//double destAlt = CalcMeanAlt(destOrbit);

			double phase = CurrentPhase(srcOrbit, destOrbit) - OberthDesiredPhase(destOrbit);
			phase = (phase + 360.0) % 360.0;

			return phase;
		}
		// project two vectors to 2D plane and returns the angle between them
		private static double Angle2d(Vector3d vector1, Vector3d vector2)
		{
			Vector3d v1 = Vector3d.Project(new Vector3d(vector1.x, 0, vector1.z), vector1);
			Vector3d v2 = Vector3d.Project(new Vector3d(vector2.x, 0, vector2.z), vector2);
			return Vector3d.Angle(v1, v2);
		}

		private static double CalcMeanAlt(Orbit orbit)
		{
			return orbit.semiMajorAxis * (1.0 + orbit.eccentricity * orbit.eccentricity / 2.0);
		}
		// calculates angle between vessel's position and prograde of orbited body
		// MOARdV: The parameter 'check' is always NULL in protractor.  Factored it out
		private double CurrentEjectAngle()
		{
			Vector3d vesselvec = vessel.orbit.getRelativePositionAtUT(time);

			// get planet's position relative to universe
			Vector3d bodyvec = vessel.mainBody.orbit.getRelativePositionAtUT(time);

			double eject = Angle2d(vesselvec, Quaternion.AngleAxis(90.0f, Vector3d.forward) * bodyvec);

			if (Angle2d(vesselvec, Quaternion.AngleAxis(180.0f, Vector3d.forward) * bodyvec) > Angle2d(vesselvec, bodyvec)) {
				eject = 360.0 - eject;//use cross vector to determine up or down
			}

			return eject;
		}

		private double CalculateDesiredEjectionAngle(CelestialBody orig, Orbit dest)
		{
			double o_alt = CalcMeanAlt(orig.orbit);
			double d_alt = CalcMeanAlt(dest);
			double o_soi = orig.sphereOfInfluence;
			double o_radius = orig.Radius;
			double o_mu = orig.gravParameter;
			double u = orig.referenceBody.gravParameter;
			double exitalt = o_alt + o_soi;
			double v2 = Math.Sqrt(u / exitalt) * (Math.Sqrt((2 * d_alt) / (exitalt + d_alt)) - 1);
			double r = o_radius + (vessel.mainBody.GetAltitude(vessel.findWorldCenterOfMass()));
			double v = Math.Sqrt((r * (o_soi * v2 * v2 - 2 * o_mu) + 2 * o_soi * o_mu) / (r * o_soi));
			double eta = Math.Abs(v * v / 2 - o_mu / r);
			double h = r * v;
			double e = Math.Sqrt(1 + ((2 * eta * h * h) / (o_mu * o_mu)));
			double eject = (180 - (Math.Acos(1 / e) * (180 / Math.PI))) % 360;

			eject = o_alt > d_alt ? 180 - eject : 360 - eject;

			return vessel.orbit.inclination > 90 && !(vessel.Landed) ? 360 - eject : eject;
		}
		// Compute the current phase of the target.
		private double CurrentPhase(Orbit originOrbit, Orbit destinationOrbit)
		{
			Vector3d vecthis = originOrbit.getRelativePositionAtUT(time);
			Vector3d vectarget = destinationOrbit.getRelativePositionAtUT(time);

			double phase = Angle2d(vecthis, vectarget);

			vecthis = Quaternion.AngleAxis(90.0f, Vector3d.forward) * vecthis;

			if (Angle2d(vecthis, vectarget) > 90.0)
				phase = 360.0 - phase;

			return (phase + 360.0) % 360.0;
		}
		// Calculates phase angle for rendezvous between two bodies orbiting same parent
		private static double DesiredPhase(double vesselAlt, double destAlt, double gravParameter)
		{
			double o_alt = vesselAlt;

			double d_alt = destAlt;
			double u = gravParameter;
			double th = Math.PI * Math.Sqrt(Math.Pow(o_alt + d_alt, 3.0) / (8.0 * u));
			double phase = (180.0 - Math.Sqrt(u / d_alt) * (th / d_alt) * (180.0 / Math.PI));

			while (phase < 0.0)
				phase += 360.0;

			return phase % 360.0;
		}
		// For going from a moon to another planet exploiting oberth effect
		private double OberthDesiredPhase(Orbit destOrbit)
		{
			CelestialBody moon = vessel.mainBody;
			CelestialBody planet = vessel.mainBody.referenceBody;
			double planetalt = CalcMeanAlt(planet.orbit);
			double destalt = CalcMeanAlt(destOrbit);
			double moonalt = CalcMeanAlt(moon.orbit);
			double usun = Planetarium.fetch.Sun.gravParameter;
			double uplanet = planet.gravParameter;
			double oberthalt = (planet.Radius + planet.maxAtmosphereAltitude) * 1.05;

			double th1 = Math.PI * Math.Sqrt(Math.Pow(moonalt + oberthalt, 3.0) / (8.0 * uplanet));
			double th2 = Math.PI * Math.Sqrt(Math.Pow(planetalt + destalt, 3.0) / (8.0 * usun));

			double phase = (180.0 - Math.Sqrt(usun / destalt) * ((th1 + th2) / destalt) * (180.0 / Math.PI));

			while (phase < 0.0)
				phase += 360.0;

			return phase % 360.0;
		}
		//--- End Protractor imports
		// Sigh. MechJeb math.
		private static double GetCurrentThrust(PartModule engine)
		{
			var straightEngine = engine as ModuleEngines;
			var flippyEngine = engine as ModuleEnginesFX;
			if (straightEngine != null) {
				if ((!straightEngine.EngineIgnited) || (!straightEngine.isEnabled) || (!straightEngine.isOperational))
					return 0;
				return straightEngine.finalThrust;
			}
			if (flippyEngine != null) {
				if ((!flippyEngine.EngineIgnited) || (!flippyEngine.isEnabled) || (!flippyEngine.isOperational))
					return 0;
				return flippyEngine.finalThrust;
			}
			return 0;
		}

		private static double GetMaximumThrust(PartModule engine)
		{
			var straightEngine = engine as ModuleEngines;
			var flippyEngine = engine as ModuleEnginesFX;
			if (straightEngine != null) {
				if ((!straightEngine.EngineIgnited) || (!straightEngine.isEnabled) || (!straightEngine.isOperational))
					return 0;
				return straightEngine.maxThrust * (straightEngine.thrustPercentage / 100d);
			}
			if (flippyEngine != null) {
				if ((!flippyEngine.EngineIgnited) || (!flippyEngine.isEnabled) || (!flippyEngine.isOperational))
					return 0;
				return flippyEngine.maxThrust * (flippyEngine.thrustPercentage / 100d);
			}
			return 0;
		}

		private static double GetRealIsp(PartModule engine)
		{
			var straightEngine = engine as ModuleEngines;
			var flippyEngine = engine as ModuleEnginesFX;
			if (straightEngine != null) {
				if ((!straightEngine.EngineIgnited) || (!straightEngine.isEnabled) || (!straightEngine.isOperational))
					return 0;
				return straightEngine.realIsp;
			}
			if (flippyEngine != null) {
				if ((!flippyEngine.EngineIgnited) || (!flippyEngine.isEnabled) || (!flippyEngine.isOperational))
					return 0;
				return flippyEngine.realIsp;
			}
			return 0;
		}

		private void FetchCommonData()
		{
			localGeeASL = vessel.orbit.referenceBody.GeeASL * gee;
			coM = vessel.findWorldCenterOfMass();
			localGeeDirect = FlightGlobals.getGeeForceAtPosition(coM).magnitude;
			up = (coM - vessel.mainBody.position).normalized;
			forward = vessel.GetTransform().up;
			right = vessel.GetTransform().right;
			north = Vector3d.Exclude(up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - coM).normalized;
			rotationSurface = Quaternion.LookRotation(north, up);
			rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);

			velocityVesselOrbit = vessel.orbit.GetVel();
			velocityVesselSurface = velocityVesselOrbit - vessel.mainBody.getRFrmVel(coM);

			speedVertical = Vector3d.Dot(velocityVesselSurface, up);
			speedVerticalRounded = Math.Ceiling(speedVertical * 20) / 20;
			target = FlightGlobals.fetch.VesselTarget;
			if (vessel.patchedConicSolver != null) {
				node = vessel.patchedConicSolver.maneuverNodes.Count > 0 ? vessel.patchedConicSolver.maneuverNodes[0] : null;
			} else {
				node = null;
			}
			time = Planetarium.GetUniversalTime();
			FetchAltitudes();
			terrainHeight = altitudeASL - altitudeTrue;
			if (time >= lastTimePerSecond + 1) {
				terrainDelta = terrainHeight - lastTerrainHeight;
				lastTerrainHeight = terrainHeight;
				lastTimePerSecond = time;
			}

			horzVelocity = (velocityVesselSurface - (speedVertical * up)).magnitude;
			horzVelocityForward = Vector3d.Dot(velocityVesselSurface, forward);
			horzVelocityRight = Vector3d.Dot(velocityVesselSurface, right);

			atmPressure = FlightGlobals.getStaticPressure(altitudeASL, vessel.mainBody);
			dynamicPressure = 0.5 * velocityVesselSurface.sqrMagnitude * vessel.atmDensity;

			if (target != null) {
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
				if (targetOrbit != null) {
					velocityRelativeTarget = vessel.orbit.GetVel() - target.GetOrbit().GetVel();
				} else {
					velocityRelativeTarget = vessel.orbit.GetVel();
				}

				// If our target is somehow our own celestial body, approach speed is equal to vertical speed.
				if (targetBody == vessel.mainBody)
					approachSpeed = speedVertical;
				// In all other cases, that should work. I think.
				approachSpeed = Vector3d.Dot(velocityRelativeTarget, (target.GetTransform().position - vessel.GetTransform().position).normalized);
			} else {
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
			if (vessel.situation == Vessel.Situations.SUB_ORBITAL || vessel.situation == Vessel.Situations.FLYING) {
				// Mental note: the local g taken from vessel.mainBody.GeeASL will suffice.
				//  t = (v+sqrt(v²+2gd))/g or something.

				// What is the vertical component of current acceleration?
				double accelUp = Vector3d.Dot(vessel.acceleration, up);

				double altitude = altitudeTrue;
				if (vessel.mainBody.ocean && altitudeASL > 0.0) {
					// AltitudeTrue shows distance above the floor of the ocean,
					// so use ASL if it's closer in this case, and we're not
					// already below SL.
					altitude = Math.Min(altitudeASL, altitudeTrue);
				}

				if (accelUp < 0.0 || speedVertical >= 0.0 || Planetarium.TimeScale > 1.0) {
					// If accelUp is negative, we can't use it in the general
					// equation for finding time to impact, since it could
					// make the term inside the sqrt go negative.
					// If we're going up, we can use this as well, since
					// the precision is not critical.
					// If we are warping, accelUp is always zero, so if we
					// do not use this case, we would fall to the simple
					// formula, which is wrong.
					secondsToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * localGeeASL * altitude)) / localGeeASL;
				} else if (accelUp > 0.005) {
					// This general case takes into account vessel acceleration,
					// so estimates on craft that include parachutes or do
					// powered descents are more accurate.
					secondsToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * accelUp * altitude)) / accelUp;
				} else {
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

			} else {
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
			totalDataAmount = 0;
			double averageIspContribution = 0;

			anyEnginesOverheating = false;

			resources.StartLoop(time);

			foreach (Part thatPart in vessel.parts) {

				foreach (PartResource resource in thatPart.Resources) {
					resources.Add(resource);
				}

				if (thatPart.physicalSignificance != Part.PhysicalSignificance.NONE) {
					totalShipDryMass += thatPart.mass;
					totalShipWetMass += thatPart.mass;
				}

				totalShipWetMass += thatPart.GetResourceMass();

				foreach (PartModule pm in thatPart.Modules) {
					if (!pm.isEnabled)
						continue;
					var thatEngineModule = pm as ModuleEngines;
					var thatEngineModuleFX = pm as ModuleEnginesFX;
					if (thatEngineModule != null || thatEngineModuleFX != null) {
						// This part is an engine, so check if it's overheating here.
						anyEnginesOverheating |= thatPart.temperature / thatPart.maxTemp > 0.9;

						totalCurrentThrust += GetCurrentThrust(pm);
						totalMaximumThrust += GetMaximumThrust(pm);
						double realIsp = GetRealIsp(pm);
						if (realIsp > 0)
							averageIspContribution += GetMaximumThrust(pm) / realIsp;
					}

					if (thatEngineModule != null)
						foreach (Propellant thatResource in thatEngineModule.propellants)
							resources.MarkPropellant(thatResource);
					if (thatEngineModuleFX != null)
						foreach (Propellant thatResource in thatEngineModuleFX.propellants)
							resources.MarkPropellant(thatResource);
				}

				foreach (IScienceDataContainer container in thatPart.FindModulesImplementing<IScienceDataContainer>()) {
					foreach (ScienceData datapoint in container.GetData()) {
						if (datapoint != null)
							totalDataAmount += datapoint.dataAmount;
					}
				}
			}

			if (averageIspContribution > 0)
				actualAverageIsp = totalMaximumThrust / averageIspContribution;
			else
				actualAverageIsp = 0;

			resourcesAlphabetic = resources.Alphabetic();

			// We can use the stock routines to get at the per-stage resources.
			foreach (Vessel.ActiveResource thatResource in vessel.GetActiveResources()) {
				resources.SetActive(thatResource);
			}

			// I seriously hope you don't have crew jumping in and out more than once per second.
			vesselCrew = (vessel.GetVesselCrew()).ToArray();
			// The sneaky bit: This way we can get at their panic and whee values!
			vesselCrewMedical = new kerbalExpressionSystem[vesselCrew.Length];
			for (int i = 0; i < vesselCrew.Length; i++) {
				vesselCrewMedical[i] = vesselCrew[i].KerbalRef != null ? vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>() : null;
			}

			// Part-local list is assembled somewhat differently.
			// Mental note: Actually, there's a list of ProtoCrewMember in part.protoModuleCrew. 
			// But that list loses information about seats, which is what we'd like to keep in this particular case.
			if (part.internalModel == null) {
				JUtil.LogMessage(this, "Running on a part with no IVA, how did that happen?");
			} else {
				localCrew = new ProtoCrewMember[part.internalModel.seats.Count];
				localCrewMedical = new kerbalExpressionSystem[localCrew.Length];
				for (int i = 0; i < part.internalModel.seats.Count; i++) {
					localCrew[i] = part.internalModel.seats[i].crew;
					localCrewMedical[i] = localCrew[i] == null ? null : localCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>();
				}
			}
		}
		// Another piece from MechJeb.
		private void FetchAltitudes()
		{
			altitudeASL = vessel.mainBody.GetAltitude(coM);
			slopeAngle = -1;
			RaycastHit sfc;
			if (Physics.Raycast(coM, -up, out sfc, (float)altitudeASL + 10000.0F, 1 << 15)) {
				slopeAngle = Vector3.Angle(up, sfc.normal);
				altitudeTrue = sfc.distance;
			} else if (vessel.mainBody.pqsController != null) {
				// from here: http://kerbalspaceprogram.com/forum/index.php?topic=10324.msg161923#msg161923
				altitudeTrue = vessel.mainBody.GetAltitude(coM) -
				(vessel.mainBody.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(vessel.mainBody.GetLongitude(coM), Vector3d.down) *
				QuaternionD.AngleAxis(vessel.mainBody.GetLatitude(coM), Vector3d.forward) *
				Vector3d.right) - vessel.mainBody.pqsController.radius);
			} else
				altitudeTrue = vessel.mainBody.GetAltitude(coM);
			altitudeBottom = (vessel.mainBody.ocean) ? Math.Min(altitudeASL, altitudeTrue) : altitudeTrue;
			if (altitudeBottom < 500d) {
				double lowestPoint = altitudeASL;
				foreach (Part p in vessel.parts) {
					if (p.collider != null) {
						Vector3d bottomPoint = p.collider.ClosestPointOnBounds(vessel.mainBody.position);
						double partBottomAlt = vessel.mainBody.GetAltitude(bottomPoint);
						lowestPoint = Math.Min(lowestPoint, partBottomAlt);
					}
				}
				lowestPoint -= altitudeASL;
				altitudeBottom += lowestPoint;
			}

			if (altitudeBottom < 0)
				altitudeBottom = 0;
		}
		// According to C# specification, switch-case is compiled to a constant hash table.
		// So this is actually more efficient than a dictionary, who'd have thought.
		private static string SituationString(Vessel.Situations situation)
		{
			switch (situation) {
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
		public object ProcessVariable(string input)
		{
			if (resultCache[input] != null)
				return resultCache[input];
			bool cacheable;
			object returnValue;
			try {
				if (!plugins.ProcessVariable(input, out returnValue, out cacheable))
					returnValue = VariableToObject(input, out cacheable);
			} catch (Exception e) {
				JUtil.LogErrorMessage(this, "Processing error while processing {0}: {1}", input, e.Message);
				// Most of the variables are doubles...
				return double.NaN;
			}
			if (cacheable) {
				resultCache.Add(input, returnValue);
				return resultCache[input];
			}
			return returnValue;
		}

		private static object CrewListElement(string element, int seatID, IList<ProtoCrewMember> crewList, IList<kerbalExpressionSystem> crewMedical)
		{
			bool exists = seatID < crewList.Count;
			bool valid = exists && crewList[seatID] != null;
			switch (element) {
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
				default:
					return "???!";
			}

		}

		private double NextApsisType()
		{
			if (orbitSensibility) {
				if (vessel.orbit.eccentricity < 1.0) {
					// Which one will we reach first?
					return (vessel.orbit.timeToPe < vessel.orbit.timeToAp) ? -1.0 : 1.0;
				} 	// Ship is hyperbolic.  There is no Ap.  Have we already
				// passed Pe?
				return (-vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period) > 0.0) ? -1.0 : 0.0;
			}

			return 0.0;
		}

		private object VariableToObject(string input, out bool cacheable)
		{

			// Some variables may not cacheable, because they're meant to be different every time like RANDOM,
			// or immediate. they will set this flag to false.
			cacheable = true;

			// It's slightly more optimal if we take care of that before the main switch body.
			if (input.IndexOf("_", StringComparison.Ordinal) > -1) {
				string[] tokens = input.Split('_');

				// Strings stored in module configuration.
				if (tokens.Length == 2 && tokens[0] == "STOREDSTRING") {
					int storedStringNumber;
					if (int.TryParse(tokens[1], out storedStringNumber) && storedStringNumber >= 0 && storedStringsArray.Length > storedStringNumber)
						return storedStringsArray[storedStringNumber];
					return "";
				}

				// If input starts with ISLOADED, this is a query on whether a specific DLL has been loaded into the system.
				// So we look it up in our list.
				if (tokens.Length == 2 && tokens[0] == "ISLOADED") {
					return knownLoadedAssemblies.Contains(tokens[1]) ? 1d : 0d;
				}

				// If input starts with SYSR, this is a named system resource which we should recognise and return.
				// The qualifier rules did not change since individually named resources got deprecated.
				if (tokens.Length == 2 && tokens[0] == "SYSR") {
					foreach (KeyValuePair<string, string> resourceType in systemNamedResources) {
						if (tokens[1].StartsWith(resourceType.Key, StringComparison.Ordinal)) {
							string argument = tokens[1].Substring(resourceType.Key.Length);
							if (argument.StartsWith("STAGE", StringComparison.Ordinal)) {
								argument = argument.Substring("STAGE".Length);
								return resources.ListElement(resourceType.Value, argument, true);
							}
							return resources.ListElement(resourceType.Value, argument, false);
						}
					}
				}

				// If input starts with "LISTR" we're handling it specially -- it's a list of all resources.
				// The variables are named like LISTR_<number>_<NAME|VAL|MAX>
				if (tokens.Length == 3 && tokens[0] == "LISTR") {
					ushort resourceID = Convert.ToUInt16(tokens[1]);
					if (tokens[2] == "NAME") {
						return resourceID >= resourcesAlphabetic.Length ? string.Empty : resourcesAlphabetic[resourceID];
					}
					if (resourceID >= resourcesAlphabetic.Length)
						return 0d;
					return tokens[2].StartsWith("STAGE", StringComparison.Ordinal) ? 
						resources.ListElement(resourcesAlphabetic[resourceID], tokens[2].Substring("STAGE".Length), true) : 
						resources.ListElement(resourcesAlphabetic[resourceID], tokens[2], false);
				}

				// We do similar things for crew rosters.
				// The syntax is therefore CREW_<index>_<FIRST|LAST|FULL>
				// Part-local crew list is identical but CREWLOCAL_.
				if (tokens.Length == 3) { 
					ushort crewSeatID = Convert.ToUInt16(tokens[1]);
					switch (tokens[0]) {
						case "CREW":
							return CrewListElement(tokens[2], crewSeatID, vesselCrew, vesselCrewMedical);
						case "CREWLOCAL":
							return CrewListElement(tokens[2], crewSeatID, localCrew, localCrewMedical);
					}
				}

			}

			// Action group memo strings from vessel description.
			if (input.StartsWith("AGMEMO", StringComparison.Ordinal)) {
				uint groupID;
				if (uint.TryParse(input.Substring(6), out groupID) && groupID < 10) {
					string[] tokens;
					if (actionGroupMemo[groupID].IndexOf('|') > 1 && (tokens = actionGroupMemo[groupID].Split('|')).Length == 2) {
						if (vessel.ActionGroups.groups[actionGroupID[groupID]])
							return tokens[0];
						return tokens[1];
					}
					return actionGroupMemo[groupID];
				}
				return input;
			}
			// Action group state.
			if (input.StartsWith("AGSTATE", StringComparison.Ordinal)) {
				uint groupID;
				if (uint.TryParse(input.Substring(7), out groupID) && groupID < 10) {
					return (vessel.ActionGroups.groups[actionGroupID[groupID]]).GetHashCode();
				}
				return input;
			}


			switch (input) {

			// It's a bit crude, but it's simple enough to populate.
			// Would be a bit smoother if I had eval() :)

			// Speeds.
				case "VERTSPEED":
					return speedVertical;
				case "VERTSPEEDLOG10":
					return JUtil.PseudoLog10(speedVertical);
				case "VERTSPEEDROUNDED":
					return speedVerticalRounded;
				case "SURFSPEED":
					return velocityVesselSurface.magnitude;
				case "ORBTSPEED":
					return velocityVesselOrbit.magnitude;
				case "TRGTSPEED":
					return velocityRelativeTarget.magnitude;
				case "HORZVELOCITY":
					return horzVelocity;
				case "HORZVELOCITYFORWARD":
					return horzVelocityForward;
				case "HORZVELOCITYRIGHT":
					return horzVelocityRight;
				case "EASPEED":
					return vessel.srf_velocity.magnitude * Math.Sqrt(vessel.atmDensity / standardAtmosphere);
				case "APPROACHSPEED":
					return approachSpeed;
				case "SELECTEDSPEED":
					switch (FlightUIController.speedDisplayMode) {
						case FlightUIController.SpeedDisplayModes.Orbit:
							return velocityVesselOrbit.magnitude;
						case FlightUIController.SpeedDisplayModes.Surface:
							return velocityVesselSurface.magnitude;
						case FlightUIController.SpeedDisplayModes.Target:
							return velocityRelativeTarget.magnitude;
					}
					return double.NaN;
				case "SPEEDATIMPACT":
					return expectedSpeedAtImpact;
				case "BESTSPEEDATIMPACT":
					return bestPossibleSpeedAtImpact;

			// The way Engineer does it...
		//		case "TGTRELX":
		//			return FlightGlobals.ship_tgtVelocity.x;
		//		case "TGTRELY":
		//			return FlightGlobals.ship_tgtVelocity.y;
		//		case "TGTRELZ":
		//			return FlightGlobals.ship_tgtVelocity.z;

				//The way NavyFish does it...
				case "TGTRELX":
					if (target != null && targetDockingNode != null) {
						Transform targetTransform = targetDockingNode.GetTransform();
						float normalVelocity = Vector3.Dot(FlightGlobals.ship_tgtVelocity, targetTransform.forward.normalized);
						Vector3 globalTransverseVelocity = FlightGlobals.ship_tgtVelocity - normalVelocity * targetTransform.forward.normalized;
						return Vector3.Dot(globalTransverseVelocity, FlightGlobals.ActiveVessel.ReferenceTransform.right);
					} else {
						return 0;
					}

				case "TGTRELY":
					if (target != null && targetDockingNode != null) {
						Transform targetTransform2 = targetDockingNode.GetTransform();
						float normalVelocity2 = Vector3.Dot(FlightGlobals.ship_tgtVelocity, targetTransform2.forward.normalized);
						Vector3 globalTransverseVelocity2 = FlightGlobals.ship_tgtVelocity - normalVelocity2 * targetTransform2.forward.normalized;
						return Vector3.Dot(globalTransverseVelocity2, FlightGlobals.ActiveVessel.ReferenceTransform.forward);
					} else {
						return 0;
					}
				case "TGTRELZ":
					//I THINK this is the way approachspeed should be calculated as well.  This is the number that NavyFish uses for ClosureV.
					if (targetDockingNode != null) {
						return -Vector3.Dot(FlightGlobals.ship_tgtVelocity, targetDockingNode.GetTransform().forward.normalized);
					} else {
						return 0;
					}
               


			// Time to impact. This is quite imprecise, because a precise calculation pulls in pages upon pages of MechJeb code.
			// It accounts for gravity now, though. Pull requests welcome.
				case "TIMETOIMPACTSECS":
					if (Double.IsNaN(secondsToImpact) || secondsToImpact > 365 * 24 * 60 * 60 || secondsToImpact < 0)
						return -1d;
					return secondsToImpact;

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
						return Math.Min(altitudeASL, altitudeTrue);
					return altitudeTrue;
				case "RADARALTOCEANLOG10":
					if (vessel.mainBody.ocean)
						return JUtil.PseudoLog10(Math.Min(altitudeASL, altitudeTrue));
					return JUtil.PseudoLog10(altitudeTrue);
				case "ALTITUDEBOTTOM":
					return altitudeBottom;
				case "ALTITUDEBOTTOMLOG10":
					return JUtil.PseudoLog10(altitudeBottom);
				case "TERRAINHEIGHT":
					return terrainHeight;
				case "TERRAINDELTA":
					return terrainDelta;
				case "TERRAINHEIGHTLOG10":
					return JUtil.PseudoLog10(terrainHeight);

			// Atmospheric values
				case "ATMPRESSURE":
					return atmPressure;
				case "ATMDENSITY":
					return vessel.atmDensity;
				case "DYNAMICPRESSURE":
					return dynamicPressure;
				case "ATMOSPHEREDEPTH":
					if (vessel.mainBody.atmosphere) {
						return ((upperAtmosphereLimit + Math.Log(FlightGlobals.getAtmDensity(atmPressure) /
						FlightGlobals.getAtmDensity(FlightGlobals.currentMainBody.staticPressureASL))) / upperAtmosphereLimit).Clamp(0d, 1d);
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

			// The primitive delta V calculation.

				case "DELTAV":
					return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(false)));
				case "DELTAVSTAGE":
					return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(true)));

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
						return -(node.UT - time);
					return double.NaN;
				case "MNODEDV":
					if (node != null)
						return node.GetBurnVector(vessel.orbit).magnitude;
					return 0d;
				case "MNODEBURNTIMESECS":
					if (node != null && totalMaximumThrust > 0 && actualAverageIsp > 0)
						return actualAverageIsp * (1 - Math.Exp(-node.GetBurnVector(vessel.orbit).magnitude / actualAverageIsp / gee)) / (totalMaximumThrust / (totalShipWetMass * gee));
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
						return vessel.orbit.ApA;
					return double.NaN;
				case "INCLINATION":
					if (orbitSensibility)
						return vessel.orbit.inclination;
					return double.NaN;
				case "ECCENTRICITY":
					if (orbitSensibility)
						return vessel.orbit.eccentricity;
					return double.NaN;
				case "SEMIMAJORAXIS":
					if (orbitSensibility)
						return vessel.orbit.semiMajorAxis;
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
					if (orbitSensibility) {
						double apsisType = NextApsisType();
						if (apsisType < 0.0) {
							return vessel.orbit.eccentricity < 1 ?
								vessel.orbit.timeToPe :
								-vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period);
						}
						return vessel.orbit.timeToAp;
					}
					return double.NaN;
				case "NEXTAPSIS":
					if (orbitSensibility) {
						double apsisType = NextApsisType();
						if (apsisType < 0.0) {
							return vessel.orbit.PeA;
						}
						if (apsisType > 0.0) {
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
						return vessel.orbit.TimeOfAscendingNodeEquatorial(time) - time;
					return double.NaN;
				case "TIMETODNEQUATORIAL":
					if (orbitSensibility && vessel.orbit.DescendingNodeEquatorialExists())
						return vessel.orbit.TimeOfDescendingNodeEquatorial(time) - time;
					return double.NaN;
			// SOI changes in orbits.
				case "ENCOUNTEREXISTS":
					if (orbitSensibility) {
						switch (vessel.orbit.patchEndTransition) {
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
					    vessel.orbit.patchEndTransition == Orbit.PatchTransitionType.ESCAPE)) {
						return vessel.orbit.UTsoi - time;
					}
					return double.NaN;
				case "ENCOUNTERBODY":
					if (orbitSensibility) {
						switch (vessel.orbit.patchEndTransition) { 
							case Orbit.PatchTransitionType.ENCOUNTER:
								return vessel.orbit.nextPatch.referenceBody.bodyName;
							case Orbit.PatchTransitionType.ESCAPE:
								return vessel.mainBody.referenceBody.bodyName;
						}
					}
					return string.Empty;

			// Time
				case "UTSECS":
					if (GameSettings.KERBIN_TIME) {
						return time + 426 * 6 * 60 * 60;
					}
					return time + 365 * 24 * 60 * 60;
				case "METSECS":
					return vessel.missionTime;
			// Names!
				case "NAME":
					return vessel.vesselName;
				case "VESSELTYPE":
					return vessel.vesselType.ToString();
				case "TARGETTYPE":
					if (targetVessel != null) {
						return targetVessel.vesselType.ToString();
					}
					if (targetDockingNode != null)
						return "Port";
					if (targetBody != null)
						return "Celestial";
					return "Position";

			// Coordinates.
				case "LATITUDE":
					return vessel.mainBody.GetLatitude(coM);
				case "LONGITUDE":
					return JUtil.ClampDegrees180(vessel.mainBody.GetLongitude(coM));
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
				case "RELATIVEINCLINATION":
					// MechJeb's targetables don't have orbits.
					if (target != null && targetOrbit != null) {
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
					if (targetVessel != null) {
						return targetVessel.mainBody.GetAltitude(targetVessel.findWorldCenterOfMass());
					}
					if (targetOrbit != null) {
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
					return vessel.GetOrbit().TimeOfAscendingNode(targetOrbit, time) - time;
				case "TIMETODNWITHTARGETSECS":
					if (target == null || targetOrbit == null)
						return double.NaN;
					return vessel.GetOrbit().TimeOfDescendingNode(targetOrbit, time) - time;
				case "TARGETCLOSESTAPPROACHTIME":
					if (target == null || targetOrbit == null)
						return double.NaN;
					return targetTimeAtClosestApproach - time;
				case "TARGETCLOSESTAPPROACHDISTANCE":
					if (target == null || targetOrbit == null)
						return double.NaN;
					return targetClosestApproach;


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
					if (scaledY> 100) scaledY = 100;
					if (scaledY < -100) scaledY = -100;
					return scaledY;

			// TODO: I probably should return something else for vessels. But not sure what exactly right now.
				case "TARGETANGLEX":
					if (target != null) {
						if (targetDockingNode != null)
							return JUtil.NormalAngle(-targetDockingNode.GetTransform().forward, FlightGlobals.ActiveVessel.ReferenceTransform.up, FlightGlobals.ActiveVessel.ReferenceTransform.forward);
						if (target is Vessel)
							return JUtil.NormalAngle(-target.GetFwdVector(), forward, up);
						return 0d;
					}
					return 0d;
				case "TARGETANGLEY":
					if (target != null) {
						if (targetDockingNode != null)
							return JUtil.NormalAngle(-targetDockingNode.GetTransform().forward, FlightGlobals.ActiveVessel.ReferenceTransform.up, -FlightGlobals.ActiveVessel.ReferenceTransform.right);
						if (target is Vessel) {
							JUtil.NormalAngle(-target.GetFwdVector(), forward, -right);
						}
						return 0d;
					}
					return 0d;
				case "TARGETANGLEZ":
					if (target != null) {
						if (targetDockingNode != null)
							return (360 - (JUtil.NormalAngle(-targetDockingNode.GetTransform().up, FlightGlobals.ActiveVessel.ReferenceTransform.forward, FlightGlobals.ActiveVessel.ReferenceTransform.up))) % 360;
						if (target is Vessel) {
							return JUtil.NormalAngle(target.GetTransform().up, up, -forward);
						}
						return 0d;
					}
					return 0d;
				case "TARGETANGLEDEV":
					if (target != null) {
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
					return phaseAngle;
				case "TARGETBODYPHASEANGLESECS":
					return timeToPhaseAngle;
				case "TARGETBODYEJECTIONANGLE":
					return ejectionAngle;
				case "TARGETBODYEJECTIONANGLESECS":
					return timeToEjectionAngle;
				case "TARGETBODYCLOSESTAPPROACH":
					return targetClosestApproach;
				case "TARGETBODYMOONEJECTIONANGLE":
					return moonEjectionAngle;
				case "TARGETBODYEJECTIONALTITUDE":
					return ejectionAltitude;
				case "TARGETBODYDELTAV":
					return targetBodyDeltaV;

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
					return part.temperature;
				case "SLOPEANGLE":
					return slopeAngle;
				case "SPEEDDISPLAYMODE":
					switch (FlightUIController.speedDisplayMode) {
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
					foreach (PartModule thatModule in vessel.GetReferenceTransformPart().Modules) {
						thatPort = thatModule as ModuleDockingNode;
						if (thatPort != null)
							break;
					}
					if (thatPort != null)
						return 1d;
					return 0d;
				case "ISCLAWREFERENCE":
					ModuleGrappleNode thatClaw = null;
					foreach (PartModule thatModule in vessel.GetReferenceTransformPart().Modules) {
						thatClaw = thatModule as ModuleGrappleNode;
						if (thatClaw != null)
							break;
					}
					if (thatClaw != null)
						return 1d;
					return 0d;
				case "ISREMOTEREFERENCE":
					ModuleCommand thatPod = null;
					foreach (PartModule thatModule in vessel.GetReferenceTransformPart().Modules) {
						thatPod = thatModule as ModuleCommand;
						if (thatPod != null)
							break;
					}
					if (thatPod == null)
						return 1d;
					return 0d;
				case "FLIGHTUIMODE":
					switch (FlightUIModeController.Instance.Mode) {
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
					(Math.Abs(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, up)) > 1.5 ||
					Math.Abs(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, -right)) > 1.5)).GetHashCode();
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
					return vessel.orbit.referenceBody.atmosphereScaleHeight;
				case "TARGETBODYSCALEHEIGHT":
					if (targetBody != null)
						return targetBody.atmosphereScaleHeight;
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
					return vessel.orbit.referenceBody.maxAtmosphereAltitude;
				case "TARGETBODYATMOSPHERETOP":
					if (targetBody != null)
						return targetBody.maxAtmosphereAltitude;
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
					if (targetBody != null) {
						double syncRadiusT = Math.Pow(targetBody.gravParameter / Math.Pow(2 * Math.PI / targetBody.rotationPeriod, 2), 1 / 3d);
						return syncRadiusT > targetBody.sphereOfInfluence ? double.NaN : syncRadiusT - targetBody.Radius;
					}
					return -1d;
				case "ORBITBODYSYNCORBITVELOCITY":
					return (2 * Math.PI / vessel.orbit.referenceBody.rotationPeriod) *
					Math.Pow(vessel.orbit.referenceBody.gravParameter / Math.Pow(2 * Math.PI / vessel.orbit.referenceBody.rotationPeriod, 2), 1 / 3d);
				case "TARGETBODYSYNCORBITVELOCITY":
					if (targetBody != null) {
						return (2 * Math.PI / targetBody.rotationPeriod) *
						Math.Pow(targetBody.gravParameter / Math.Pow(2 * Math.PI / targetBody.rotationPeriod, 2), 1 / 3d);
					}
					return -1d;
				case "ORBITBODYSYNCORBITCIRCUMFERENCE":
					return 2 * Math.PI * Math.Pow(vessel.orbit.referenceBody.gravParameter / Math.Pow(2 * Math.PI / vessel.orbit.referenceBody.rotationPeriod, 2), 1 / 3d);
				case "TARGETBODYSYNCORBICIRCUMFERENCE":
					if (targetBody != null) {
						return 2 * Math.PI * Math.Pow(targetBody.gravParameter / Math.Pow(2 * Math.PI / targetBody.rotationPeriod, 2), 1 / 3d);
					}
					return -1d;
			}

			// Individually named resources are deprecated, but still in.
			foreach (KeyValuePair<string, string> resourceType in namedResources) {
				if (input.StartsWith(resourceType.Key, StringComparison.Ordinal)) {
					string argument = input.Substring(resourceType.Key.Length);
					if (argument.StartsWith("STAGE", StringComparison.Ordinal)) {
						argument = argument.Substring("STAGE".Length);
						return resources.ListElement(resourceType.Value, argument, true);
					}
					return resources.ListElement(resourceType.Value, argument, false);
				}
			}

			// Didn't recognise anything so we return the string we got, that helps debugging.
			return input;
		}

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

