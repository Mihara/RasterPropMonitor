using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JSI
{
	public class RasterPropMonitorComputer: PartModule
	{
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
		private double horzVelocity;
		private ITargetable target;
		private ModuleDockingNode targetDockingNode;
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
		private readonly DefaultableDictionary<string,Vector2d> resources = new DefaultableDictionary<string,Vector2d>(Vector2d.zero);
		private string[] resourcesAlphabetic;
		private double totalShipDryMass;
		private double totalShipWetMass;
		private double totalCurrentThrust;
		private double totalMaximumThrust;
		private double actualAverageIsp;
		private double totalDataAmount;
		private double secondsToImpact;
		private double bestPossibleSpeedAtImpact;
		private double localG;
		private double standardAtmosphere;
		private double slopeAngle;
		private double atmPressure;
		private double dynamicPressure;
		private readonly double upperAtmosphereLimit = Math.Log(100000);
		// the 'Q' value
		private CelestialBody targetBody;
		// Local data fetching variables...
		private int gearGroupNumber;
		private int brakeGroupNumber;
		private int sasGroupNumber;
		private int lightGroupNumber;
		private int rcsGroupNumber;
		// This is only here to support the deprecated DMS and KDT variables.
		// These should be gone as soon as possible along with this class instance.
		private static readonly SIFormatProvider fp = new SIFormatProvider();
		// Some constant things...
		private const double gee = 9.81d;
		private readonly Dictionary<string,string> namedResources = new Dictionary<string,string> {
			// Stock resources...
			{ "ELECTRIC", "ElectricCharge" },
			{ "FUEL", "LiquidFuel" },
			{ "OXIDIZER", "Oxidizer" },
			{ "MONOPROP", "MonoPropellant" },
			{ "RSINTAKEAIR", "IntakeAir" },
			{ "XENON", "XenonGas" },
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
		// Processing cache!
		private readonly DefaultableDictionary<string,object> resultCache = new DefaultableDictionary<string,object>(null);

		public static RasterPropMonitorComputer Instantiate(InternalProp thatProp)
		{
			if (thatProp.part != null) {
				for (int i = 0; i < thatProp.part.Modules.Count; i++)
					if (thatProp.part.Modules[i].ClassName == typeof(RasterPropMonitorComputer).Name) {
						var other = thatProp.part.Modules[i] as RasterPropMonitorComputer;
						return other;
					}
				return thatProp.part.AddModule(typeof(RasterPropMonitorComputer).Name) as RasterPropMonitorComputer;
			}
			return null;
		}
		// TODO: Figure out if I can keep it at Start or OnAwake is better since it's a PartModule now.
		public void Start()
		{
			// Well, it looks like we have to do that bit just like in Firespitter.
			gearGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Gear);
			brakeGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Brakes);
			sasGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.SAS);
			lightGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Light);
			rcsGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.RCS);

			if (HighLogic.LoadedSceneIsFlight) {
				FetchPerPartData();
				standardAtmosphere = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(0, FlightGlobals.Bodies[1]));
			}
		}

		public void UpdateRefreshRates(int rate, int dataRate)
		{
			refreshTextRate = Math.Min(rate, refreshTextRate);
			refreshDataRate = Math.Min(dataRate, refreshDataRate);
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

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!UpdateCheck())
				return;

			// We clear the cache every frame.
			resultCache.Clear();

			FetchCommonData();
		}
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
				return straightEngine.maxThrust;
			}
			if (flippyEngine != null) {
				if ((!flippyEngine.EngineIgnited) || (!flippyEngine.isEnabled) || (!flippyEngine.isOperational))
					return 0;
				return flippyEngine.maxThrust;
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
			localG = vessel.orbit.referenceBody.GeeASL * gee;
			coM = vessel.findWorldCenterOfMass();
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
			node = vessel.patchedConicSolver.maneuverNodes.Count > 0 ? vessel.patchedConicSolver.maneuverNodes[0] : null;
			time = Planetarium.GetUniversalTime();
			FetchAltitudes();

			horzVelocity = (velocityVesselSurface - (speedVertical * up)).magnitude;

			atmPressure = FlightGlobals.getStaticPressure(altitudeASL, vessel.mainBody);
			dynamicPressure = 0.5 * velocityVesselSurface.sqrMagnitude * vessel.atmDensity;

			if (target != null) {
				targetSeparation = vessel.GetTransform().position - target.GetTransform().position;
				targetOrientation = target.GetTransform().rotation;

				var targetVessel = target as Vessel;

				targetBody = target as CelestialBody;	
				targetDockingNode = target as ModuleDockingNode;

				targetDistance = Vector3.Distance(target.GetTransform().position, vessel.GetTransform().position);

				// This is kind of messy.
				targetOrbitSensibility = false;
				// All celestial bodies except the sun have orbits that make sense.
				targetOrbitSensibility |= targetBody != null && targetBody != FlightGlobals.Bodies[0];

				if (targetVessel != null)
					targetOrbitSensibility = JUtil.OrbitMakesSense(targetVessel);
				if (targetDockingNode != null)
					targetOrbitSensibility = JUtil.OrbitMakesSense(target.GetVessel());

				if (targetOrbitSensibility)
					targetOrbit = target.GetOrbit();

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
					secondsToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * localG * altitude)) / localG;
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

				// This is probably nonsense, but will do for the moment.
				bestPossibleSpeedAtImpact = speedVertical - Math.Sqrt(2 * (localG - (totalMaximumThrust / totalShipWetMass)) * altitude);
				if (double.IsNaN(bestPossibleSpeedAtImpact))
					bestPossibleSpeedAtImpact = 0;
			} else {
				secondsToImpact = Double.NaN;
				bestPossibleSpeedAtImpact = 0;
			}
		}

		private void FetchPerPartData()
		{
			resources.Clear();
			totalShipDryMass = totalShipWetMass = totalCurrentThrust = totalMaximumThrust = 0;
			totalDataAmount = 0;
			double averageIspContribution = 0;

			foreach (Part thatPart in vessel.parts) {
				// The cute way of using vector2d in place of a tuple is from Firespitter.
				// Hey, it works.
				foreach (PartResource resource in thatPart.Resources) {
					resources[resource.resourceName] += new Vector2d(resource.amount, resource.maxAmount);
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
						totalCurrentThrust += GetCurrentThrust(pm);
						totalMaximumThrust += GetMaximumThrust(pm);
						double realIsp = GetRealIsp(pm);
						if (realIsp > 0)
							averageIspContribution += GetMaximumThrust(pm) / realIsp;
					} 
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

			resourcesAlphabetic = resources.Keys.ToArray();

			// Turns out, all those extra small tails in resources interfere with string formatting.
			foreach (string resource in resourcesAlphabetic) {
				Vector2d values = resources[resource];
				resources[resource] = new Vector2d(Math.Round(values.x, 2), Math.Round(values.y, 2));
			}

			Array.Sort(resourcesAlphabetic);
			// I seriously hope you don't have crew jumping in and out more than once per second.
			vesselCrew = (vessel.GetVesselCrew()).ToArray();
			// The sneaky bit: This way we can get at their panic and whee values!
			vesselCrewMedical = new kerbalExpressionSystem[vesselCrew.Length];
			for (int i = 0; i < vesselCrew.Length; i++) {
				vesselCrewMedical[i] = vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>();
			}

			// Part-local list is assembled somewhat differently.
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
			altitudeBottom = altitudeTrue;
			if (altitudeTrue < 500d) {
				double lowestPoint = altitudeASL;
				foreach (Part p in vessel.parts) {
					if (p.collider != null) {
						Vector3d bottomPoint = p.collider.ClosestPointOnBounds(vessel.mainBody.position);
						double partBottomAlt = vessel.mainBody.GetAltitude(bottomPoint);
						lowestPoint = Math.Min(lowestPoint, partBottomAlt);
					}
				}
				altitudeBottom = (altitudeTrue - altitudeASL) + lowestPoint;
			}
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
		// Another MechJeb import.
		private string CurrentBiome()
		{
			if (vessel.landedAt != string.Empty)
				return vessel.landedAt;
			string biome = JUtil.CBAttributeMapGetAtt(vessel.mainBody.BiomeMap, vessel.latitude * Math.PI / 180d, vessel.longitude * Math.PI / 180d).name;
			switch (vessel.situation) {
			//ExperimentSituations.SrfLanded
				case Vessel.Situations.LANDED:
				case Vessel.Situations.PRELAUNCH:
					return vessel.mainBody.theName + "'s " + (biome == "" ? "surface" : biome);
			//ExperimentSituations.SrfSplashed
				case Vessel.Situations.SPLASHED:
					return vessel.mainBody.theName + "'s " + (biome == "" ? "oceans" : biome);
				case Vessel.Situations.FLYING:
					if (vessel.altitude < vessel.mainBody.scienceValues.flyingAltitudeThreshold)                        
						//ExperimentSituations.FlyingLow
						return "Flying over " + vessel.mainBody.theName + (biome == "" ? "" : "'s " + biome);                
						//ExperimentSituations.FlyingHigh
					return "Upper atmosphere of " + vessel.mainBody.theName + (biome == "" ? "" : "'s " + biome);
				default:
					if (vessel.altitude < vessel.mainBody.scienceValues.spaceAltitudeThreshold)
						//ExperimentSituations.InSpaceLow
						return "Space just above " + vessel.mainBody.theName;
						// ExperimentSituations.InSpaceHigh
					return "Space high over " + vessel.mainBody.theName;
			}
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
					return valid ? crewMedical[seatID].panicLevel : -1d;
				case "WHEE":
					return valid ? crewMedical[seatID].wheeLevel : -1d;
				default:
					return "???!";
			}

		}

		private object VariableToObject(string input, out bool cacheable)
		{

			// Some variables may not cacheable, because they're meant to be different every time like RANDOM,
			// or immediate. they will set this flag to false.
			cacheable = true;

			// It's slightly more optimal if we take care of that before the main switch body.
			if (input.IndexOf("_", StringComparison.Ordinal) > -1) {
				string[] tokens = input.Split('_');

				// If input starts with "LISTR" we're handling it specially -- it's a list of all resources.
				// The variables are named like LISTR_<number>_<NAME|VAL|MAX>
				if (tokens.Length == 3 && tokens[0] == "LISTR") {
					ushort resourceID = Convert.ToUInt16(tokens[1]);
					switch (tokens[2]) {
						case "NAME":
							if (resourceID >= resources.Count)
								return string.Empty;
							return resourcesAlphabetic[resourceID];
						case "VAL":
							if (resourceID >= resources.Count)
								return 0d;
							return resources[resourcesAlphabetic[resourceID]].x;
						case "MAX":
							if (resourceID >= resources.Count)
								return 0d;
							return resources[resourcesAlphabetic[resourceID]].y;
						case "PERCENT":
							if (resourceID >= resources.Count)
								return 0d;
							if (resources[resourcesAlphabetic[resourceID]].y > 0)
								return resources[resourcesAlphabetic[resourceID]].x / resources[resourcesAlphabetic[resourceID]].y;
							return 0d;
					}


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

			switch (input) {

			// It's a bit crude, but it's simple enough to populate.
			// Would be a bit smoother if I had eval() :)

			// Speeds.
				case "VERTSPEED":
					return speedVertical;
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

			// The way Engineer does it...
				case "TGTRELX":
					return FlightGlobals.ship_tgtVelocity.x;
				case "TGTRELY":
					return FlightGlobals.ship_tgtVelocity.y;
				case "TGTRELZ":
					return FlightGlobals.ship_tgtVelocity.z;

			// Time to impact. This is quite imprecise, because a precise calculation pulls in pages upon pages of MechJeb code.
			// It accounts for gravity now, though. Pull requests welcome.
				case "TIMETOIMPACTSECS":
					if (Double.IsNaN(secondsToImpact) || secondsToImpact > 365 * 24 * 60 * 60 || secondsToImpact < 0)
						return -1d;
					return secondsToImpact;

			// Altitudes
				case "ALTITUDE":
					return altitudeASL;
				case "RADARALT":
					return altitudeTrue;
				case "ALTITUDEBOTTOM":
					return altitudeBottom;
				case "TERRAINHEIGHT":
					return altitudeASL - altitudeTrue;

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

			// Thrust and related
				case "THRUST":
					return totalCurrentThrust;
				case "THRUSTMAX":
					return totalMaximumThrust;
				case "TWR":
					return totalCurrentThrust / (totalShipWetMass * localG);
				case "TWRMAX":
					return totalMaximumThrust / (totalShipWetMass * localG);
				case "ACCEL":
					return totalCurrentThrust / totalShipWetMass;
				case "MAXACCEL":
					return totalMaximumThrust / totalShipWetMass;
				case "GFORCE":
					return vessel.geeForce_immediate;
				case "REALISP":
					return actualAverageIsp;

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
				case "ORBITMAKESSENSE":
					if (orbitSensibility)
						return 1d;
					return -1d;

			// Time
				case "UTSECS":
					return time + 365 * 24 * 60 * 60;
				case "METSECS":
					return vessel.missionTime;
			// Names!
				case "NAME":
					return vessel.vesselName;


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
						return JUtil.ClampDegrees180(target.GetVessel().mainBody.GetLatitude(target.GetTransform().position));
					return vessel.mainBody.GetLatitude(target.GetTransform().position);

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
					var targetVessel = target as Vessel;
					if (targetVessel != null) {
						return targetVessel.mainBody.GetAltitude(targetVessel.findWorldCenterOfMass());
					}
					if (targetOrbit != null) {
						return targetOrbit.altitude;
					}
					return -1d;
				case "TIMETOANWITHTARGETSECS":
					if (target == null || targetOrbit == null || (target is Vessel && !targetOrbitSensibility))
						return double.NaN;
					return vessel.GetOrbit().TimeOfAscendingNode(targetOrbit, time) - time;
				case "TIMETODNWITHTARGETSECS":
					if (target == null || targetOrbit == null || (target is Vessel && !targetOrbitSensibility))
						return double.NaN;
					return vessel.GetOrbit().TimeOfDescendingNode(targetOrbit, time) - time;

			// Ok, what are X, Y and Z here anyway?
				case "TARGETDISTANCEX":
					return Vector3d.Dot(targetSeparation, vessel.GetTransform().right);
				case "TARGETDISTANCEY":
					return Vector3d.Dot(targetSeparation, vessel.GetTransform().forward);
				case "TARGETDISTANCEZ":
					return Vector3d.Dot(targetSeparation, vessel.GetTransform().up);

			// TODO: I probably should return something else for vessels. But not sure what exactly right now.
				case "TARGETANGLEX":
					if (target != null) {
						if (targetDockingNode != null)
							return JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, up);
						if (target is Vessel)
							return JUtil.NormalAngle(-target.GetFwdVector(), forward, up);
						return 0d;
					}
					return 0d;
				case "TARGETANGLEY":
					if (target != null) {
						if (targetDockingNode != null)
							return JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, -right);
						if (target is Vessel) {
							JUtil.NormalAngle(-target.GetFwdVector(), forward, -right);
						}
						return 0d;
					}
					return 0d;
				case "TARGETANGLEZ":
					if (target != null) {
						if (targetDockingNode != null)
							return JUtil.NormalAngle(targetDockingNode.GetTransform().up, up, -forward);
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
					return Staging.separate_ready.GetHashCode();
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

			// Compound variables which exist to stave off the need to parse logical and arithmetic expressions. :)
				case "GEARALARM":
					// Returns 1 if vertical speed is negative, gear is not extended, and radar altitude is less than 50m.
					return (speedVerticalRounded < 0 && !FlightGlobals.ActiveVessel.ActionGroups.groups[gearGroupNumber] && altitudeBottom < 50).GetHashCode();
				case "GROUNDPROXIMITYALARM":
					// Returns 1 if, at maximum acceleration, in the time remaining until ground impact, it is impossible to get a vertical speed higher than -10m/s.
					return (bestPossibleSpeedAtImpact < -10d).GetHashCode();
				case "TUMBLEALARM":
					return (speedVerticalRounded < 0 && altitudeTrue < 100 && horzVelocity > 5).GetHashCode();
				case "SLOPEALARM":
					return (speedVerticalRounded < 0 && altitudeTrue < 100 && slopeAngle > 10).GetHashCode();
				case "DOCKINGANGLEALARM":
					return (targetDockingNode != null && targetDistance < 10 && approachSpeed > 0 &&
					(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, up) > 1.5 ||
					JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, -right) > 1.5)).GetHashCode();
				case "DOCKINGSPEEDALARM":
					return (targetDockingNode != null && approachSpeed > 3 && targetDistance < 10).GetHashCode();
				case "ALTITUDEALARM":
					return (speedVerticalRounded < 0 && altitudeTrue < 100).GetHashCode();
					

			// SCIENCE!!
				case "SCIENCEDATA":
					return totalDataAmount;
				case "BIOMENAME":
					return CurrentBiome();
				case "BIOMEID":
					return JUtil.CBAttributeMapGetAtt(vessel.mainBody.BiomeMap, vessel.latitude * Math.PI / 180d, vessel.longitude * Math.PI / 180d).name;

			// Action group flags. To properly format those, use this format:
			// {0:on;0;OFF}
				case "GEAR":
					return FlightGlobals.ActiveVessel.ActionGroups.groups[gearGroupNumber].GetHashCode();
				case "BRAKES":
					return FlightGlobals.ActiveVessel.ActionGroups.groups[brakeGroupNumber].GetHashCode();
				case "SAS":
					return FlightGlobals.ActiveVessel.ActionGroups.groups[sasGroupNumber].GetHashCode();
				case "LIGHTS":
					return FlightGlobals.ActiveVessel.ActionGroups.groups[lightGroupNumber].GetHashCode();
				case "RCS":
					return FlightGlobals.ActiveVessel.ActionGroups.groups[rcsGroupNumber].GetHashCode();

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

			// These variables are no longer documented and are DEPRECATED. They will be removed as soon as I can see that people aren't using them.
				case "LATITUDE_DMS":
					return string.Format(fp, "{0:DMSd+ mm+ ss+ N}", vessel.mainBody.GetLatitude(coM));
				case "LONGITUDE_DMS":
					return string.Format(fp, "{0:DMSd+ mm+ ss+ E}", JUtil.ClampDegrees180(vessel.mainBody.GetLongitude(coM)));
				case "LATITUDETGT_DMS":
					if (target is Vessel)
						return string.Format(fp, "{0:DMSd+ mm+ ss+ N}", target.GetVessel().mainBody.GetLatitude(target.GetVessel().GetWorldPos3D()));
					return string.Empty;
				case "LONGITUDETGT_DMS":
					if (target is Vessel)
						return string.Format(fp, "{0:DMSd+ mm+ ss+ E}", JUtil.ClampDegrees180(target.GetVessel().mainBody.GetLongitude(target.GetVessel().GetWorldPos3D())));
					return string.Empty;
				case "TIMETOIMPACT":
					if (Double.IsNaN(secondsToImpact) || secondsToImpact > 365 * 24 * 60 * 60 || secondsToImpact < 0) {
						return string.Empty;
					}
					return string.Format(fp, "{0:KDTddd:hh:mm:ss.f}", secondsToImpact); 
				case "MNODETIME":
					if (node != null)
						return string.Format(fp, "{0:KDT+yy:ddd:hh:mm:ss.f}", -(node.UT - time));
					return string.Empty;
				case "MNODEBURNTIME":
					if (node != null && totalMaximumThrust > 0 && actualAverageIsp > 0)
						return string.Format(fp, "{0:KDThh:mm:ss.f}",
							actualAverageIsp * (1 - Math.Exp(-node.GetBurnVector(vessel.orbit).magnitude / actualAverageIsp / gee)) / (totalMaximumThrust / (totalShipWetMass * gee))
						);
					return string.Empty;
				case "ORBPERIOD":
					if (orbitSensibility)
						return string.Format(fp, "{0:KDTyy:ddd:hh:mm:ss.f}", vessel.orbit.period);
					return string.Empty;
				case "TIMETOAP":
					if (orbitSensibility)
						return string.Format(fp, "{0:KDTyy:ddd:hh:mm:ss.f}", vessel.orbit.timeToAp);
					return string.Empty;
				case "TIMETOPE":
					if (orbitSensibility)
						return vessel.orbit.eccentricity < 1 ? 
							string.Format(fp, "{0:KDT-yy:ddd:hh:mm:ss.f}", vessel.orbit.timeToPe) : 
							string.Format(fp, "{0:KDT-yy:ddd:hh:mm:ss.f}", -vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period));
					return string.Empty;

				case "UT":
					return string.Format(fp, "{0:KDTyy:ddd:hh:mm:ss.f}", time + 365 * 24 * 60 * 60);

				case "MET":
					return string.Format(fp, "{0:KDTyy:ddd:hh:mm:ss.f}", vessel.missionTime);
				case "TIMETOANWITHTARGET":
					if (target == null || targetOrbit == null || (target is Vessel && !targetOrbitSensibility))
						return string.Empty;
					return string.Format(fp, "{0:KDT-yy:ddd:hh:mm:ss.f}", vessel.GetOrbit().TimeOfAscendingNode(targetOrbit, time) - time);
				case "TIMETODNWITHTARGET":
					if (target == null || targetOrbit == null || (target is Vessel && !targetOrbitSensibility))
						return string.Empty;
					return string.Format(fp, "{0:KDT-yy:ddd:hh:mm:ss.f}", vessel.GetOrbit().TimeOfDescendingNode(targetOrbit, time) - time);
				case "TARGETTIMETOAP":
					if (target != null && targetOrbitSensibility)
						return string.Format(fp, "{0:KDTyy:ddd:hh:mm:ss.f}", targetOrbit.timeToAp);
					return string.Empty;
				case "TARGETORBPERIOD":
					if (target != null && targetOrbit != null && targetOrbitSensibility)
						return string.Format(fp, "{0:KDTyy:ddd:hh:mm:ss.f}", targetOrbit.period);
					return string.Empty;
				case "TARGETTIMETOPE":
					if (target != null && targetOrbitSensibility)
						return targetOrbit.eccentricity < 1 ? 
							string.Format(fp, "{0:KDT-yy:ddd:hh:mm:ss.f}", targetOrbit.timeToPe) : 
							string.Format(fp, "{0:KDT-yy:ddd:hh:mm:ss.f}", -targetOrbit.meanAnomaly / (2 * Math.PI / targetOrbit.period));
					return string.Empty;
			}



			// Named resources are all the same and better off processed like this:
			foreach (KeyValuePair<string, string> resourceType in namedResources) {
				if (input.StartsWith(resourceType.Key, StringComparison.Ordinal)) {
					if (input.EndsWith("PERCENT", StringComparison.Ordinal)) {
						if (resources[resourceType.Value].y > 0)
							return (resources[resourceType.Value].x / resources[resourceType.Value].y).Clamp(0d, 1d);
						return 0d;
					}
					return input.EndsWith("MAX", StringComparison.Ordinal) ? resources[resourceType.Value].y :
						resources[resourceType.Value].x.Clamp(0d, resources[resourceType.Value].y);
				}
			}


			// Didn't recognise anything so we return the string we got, that helps debugging.
			return input;
		}
	}
}

