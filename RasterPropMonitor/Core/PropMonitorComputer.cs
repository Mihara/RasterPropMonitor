using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JSI
{
	public class RasterPropMonitorComputer: PartModule
	{
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
		private readonly DefaultableDictionary<string,Vector2d> resources = new DefaultableDictionary<string,Vector2d>(Vector2d.zero);
		private readonly DefaultableDictionary<string,Vector2d> activeResources = new DefaultableDictionary<string, Vector2d>(Vector2d.zero);
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
		private double localG;
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
		private double moonEjectionAngle;
		private double ejectionAltitude;
		private double targetBodyDeltaV;
		// Local data fetching variables...
		private int gearGroupNumber;
		private int brakeGroupNumber;
		private int sasGroupNumber;
		private int lightGroupNumber;
		private int rcsGroupNumber;
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
			if (!HighLogic.LoadedSceneIsEditor) {
				gearGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Gear);
				brakeGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Brakes);
				sasGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.SAS);
				lightGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Light);
				rcsGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.RCS);

				FetchPerPartData();
				standardAtmosphere = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(0, FlightGlobals.Bodies[1]));
			}
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (state != StartState.Editor) {
				// Parse vessel description here for special lines:
					
				string[] descriptionStrings = vesselDescription.UnMangleConfigText().Split(JUtil.lineSeparator, StringSplitOptions.None);
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
			if (HighLogic.LoadedSceneIsEditor)
				// I think it can't be null. But for some unclear reason, the newline in this case is always 0A, rather than Environment.NewLine.
				vesselDescription = EditorLogic.fetch.shipDescriptionField.Text.Replace(editorNewline, "$$$");
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
			// MOARdV warning: This method is more convoluted than it strictly
			// needs to be.  It's set up for developing the algorithms and
			// understanding how they work together.  Clean up and
			// optimization is still TODO.


			// Only calculate these values for planets and moons, and not the
			// sun, nor vessels.  I may add vessels later, once I am confident
			// I understand the equations well enough to validate the results.
			if (targetBody != null && targetBody != Planetarium.fetch.Sun) {
				// Get some basic metrics on our orbital situation.
				// "orbit depth" for the sake of this discussion counts how
				// far removed from orbiting the sun the target is.
				int vesselOrbitDepth;
				CelestialBody vesselSystem;
				if (vessel.mainBody == Planetarium.fetch.Sun) {
					// We are orbiting Kerbol
					vesselOrbitDepth = 0;
					vesselSystem = null;
				} else if (vessel.mainBody.referenceBody == Planetarium.fetch.Sun) {
					// We are orbiting a planet.
					vesselOrbitDepth = 1;
					vesselSystem = vessel.mainBody;
				} else {
					// We are orbiting a moon.
					vesselOrbitDepth = 2;
					vesselSystem = vessel.mainBody.referenceBody;
				}

				bool targetIsMoon;
				CelestialBody targetSystem;
				if (targetOrbit.referenceBody == Planetarium.fetch.Sun) {
					// Target is a planet
					targetIsMoon = false;
					targetSystem = targetBody;
				} else {
					// Target is a moon.
					targetIsMoon = true;
					targetSystem = targetBody.referenceBody;
				}

				// Used if we have a valid phase angle
				double delta_theta = 0.0;

				if (vesselOrbitDepth == 0) {
					// We are orbiting Kerbol and ...

					// ... actually, it doesn't matter what the target is.

					phaseAngle = UpdatePhaseAngleSimple(vessel.orbit, targetSystem.orbit);
					delta_theta = (360.0 / vessel.orbit.period) - (360.0 / targetSystem.orbit.period);

					ejectionAngle = -1.0;
					timeToEjectionAngle = -1.0;

					moonEjectionAngle = -1.0;
					ejectionAltitude = -1.0;
					targetBodyDeltaV = CalculateDeltaV(targetSystem);

				} else if (vesselOrbitDepth == 1) {
					// We are orbiting a planet and ...

					// Mihara: Just to keep my IDE happy.
					if (vesselSystem == null || vesselSystem.orbit == null)
						throw new ArithmeticException("Basic assumptions about the universe turned out to be wrong.");

					if (!targetIsMoon) {
						// ... our target is a planet

						phaseAngle = UpdatePhaseAngleAdjacent(vesselSystem.orbit, targetSystem.orbit);
						delta_theta = (360.0 / vesselSystem.orbit.period) - (360.0 / targetSystem.orbit.period);

						ejectionAngle = (CalculateDesiredEjectionAngle(vessel.mainBody, targetBody) - CurrentEjectAngle() + 360.0) % 360.0;
						targetBodyDeltaV = CalculateDeltaV(targetBody);
					} else if (vesselSystem == targetSystem) {
						// ... our target is a moon of this planet

						phaseAngle = UpdatePhaseAngleSimple(vessel.orbit, targetBody.orbit);
						delta_theta = (360.0 / vessel.orbit.period) - (360.0 / targetBody.orbit.period);

						ejectionAngle = -1.0;
						targetBodyDeltaV = CalculateDeltaV(targetBody);
					} else {
						// ... our target orbits a different planet.

						phaseAngle = UpdatePhaseAngleAdjacent(vesselSystem.orbit, targetSystem.orbit);
						delta_theta = (360.0 / vesselSystem.orbit.period) - (360.0 / targetSystem.orbit.period);

						ejectionAngle = (CalculateDesiredEjectionAngle(vessel.mainBody, targetSystem) - CurrentEjectAngle() + 360.0) % 360.0;
						targetBodyDeltaV = CalculateDeltaV(targetSystem);
					}

					moonEjectionAngle = -1.0;
					ejectionAltitude = -1.0;
				} else {
					// We are orbiting a moon and ...

					// Mihara: Just to keep my IDE happy.
					if (vesselSystem == null || vesselSystem.orbit == null)
						throw new ArithmeticException("Basic assumptions about the universe turned out to be wrong.");

					if (vesselSystem != targetSystem) {
						// ... our target is or orbits a different planet.

						phaseAngle = UpdatePhaseAngleOberth(vesselSystem.orbit, targetSystem.orbit);
						delta_theta = (360.0 / vesselSystem.orbit.period) - (360.0 / targetSystem.orbit.period);

						ejectionAngle = -1.0;

						moonEjectionAngle = (MoonAngle() - CurrentEjectAngle() + 360.0) % 360.0;
						ejectionAltitude = 1.05 * vesselSystem.maxAtmosphereAltitude;
						targetBodyDeltaV = CalculateDeltaV(targetSystem);
					} else if (!targetIsMoon) {
						// ... we are targeting our parent planet.

						phaseAngle = -1.0;
						timeToPhaseAngle = -1.0;

						ejectionAngle = -1.0;

						moonEjectionAngle = -1.0;
						ejectionAltitude = -1.0;
						targetBodyDeltaV = -1.0;
					} else {
						// ... we are targeting a sibling moon.

						phaseAngle = UpdatePhaseAngleAdjacent(vessel.mainBody.orbit, targetBody.GetOrbit());
						delta_theta = (360.0 / vessel.mainBody.orbit.period) - (360.0 / targetBody.GetOrbit().period);
						
						ejectionAngle = (CalculateDesiredEjectionAngle(vessel.mainBody, targetBody) - CurrentEjectAngle() + 360.0) % 360.0;

						moonEjectionAngle = -1.0;
						ejectionAltitude = -1.0;
						targetBodyDeltaV = CalculateDeltaV(targetBody);
					}

				}

				if (phaseAngle >= 0.0) {
					if (delta_theta > 0.0) {
						timeToPhaseAngle = phaseAngle / delta_theta;
					} else {
						timeToPhaseAngle = Math.Abs((360.0 - phaseAngle) / delta_theta);
					}
				}

				if (ejectionAngle >= 0.0) {
					timeToEjectionAngle = ejectionAngle * vessel.orbit.period / 360.0;
				} else {
					timeToEjectionAngle = -1.0;
				}

				targetClosestApproach = GetClosestApproach(targetBody);
			} else {
				// No valid orbit.  Make sure the angles are cleared out.
				phaseAngle = -1.0;
				timeToPhaseAngle = -1.0;
				ejectionAngle = -1.0;
				timeToEjectionAngle = -1.0;
				targetClosestApproach = -1.0;
				moonEjectionAngle = -1.0;
				ejectionAltitude = -1.0;
				targetBodyDeltaV = -1.0;
			}
		}
		//--- Protractor utility methods
		private double CalculateDeltaV(CelestialBody dest)    //calculates ejection v to reach destination
		{
			if (vessel.mainBody == dest.orbit.referenceBody) {
				double radius = dest.referenceBody.Radius;
				double u = dest.referenceBody.gravParameter;
				double d_alt = CalcMeanAlt(dest.orbit);
				double alt = (vessel.mainBody.GetAltitude(vessel.findWorldCenterOfMass())) + radius;
				double v = Math.Sqrt(u / alt) * (Math.Sqrt((2 * d_alt) / (alt + d_alt)) - 1);
				return Math.Abs((Math.Sqrt(u / alt) + v) - vessel.orbit.GetVel().magnitude);
			} else {
				CelestialBody orig = vessel.mainBody;
				double d_alt = CalcMeanAlt(dest.orbit);
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
		//calculates ejection angle to reach destination body from origin body
		private double CalculateDesiredEjectionAngle(CelestialBody orig, CelestialBody dest)
		{
			double o_alt = CalcMeanAlt(orig.orbit);
			double d_alt = CalcMeanAlt(dest.orbit);
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

		private Orbit GetClosestOrbit(CelestialBody targetCelestial)
		{
			Orbit checkorbit = vessel.orbit;
			int orbitcount = 0;

			while (checkorbit.nextPatch != null && checkorbit.patchEndTransition != Orbit.PatchTransitionType.FINAL && orbitcount < 3) {
				checkorbit = checkorbit.nextPatch;
				orbitcount += 1;
				if (checkorbit.referenceBody == targetCelestial) {
					return checkorbit;
				}

			}
			checkorbit = vessel.orbit;
			orbitcount = 0;

			while (checkorbit.nextPatch != null && checkorbit.patchEndTransition != Orbit.PatchTransitionType.FINAL && orbitcount < 3) {
				checkorbit = checkorbit.nextPatch;
				orbitcount += 1;
				if (checkorbit.referenceBody == targetCelestial.orbit.referenceBody) {
					return checkorbit;
				}
			}

			return vessel.orbit;
		}

		private double GetClosestApproach(CelestialBody targetCelestial)
		{
			Orbit closestorbit = GetClosestOrbit(targetCelestial);
			if (closestorbit.referenceBody == targetCelestial) {
				return closestorbit.PeA;
			}
			if (closestorbit.referenceBody == targetCelestial.referenceBody) {
				return MinTargetDistance(targetCelestial, closestorbit.StartUT, closestorbit.period / 10, closestorbit) - targetCelestial.Radius;
			}
			return MinTargetDistance(targetCelestial, Planetarium.GetUniversalTime(), closestorbit.period / 10, closestorbit) - targetCelestial.Radius;
		}

		private static double MinTargetDistance(CelestialBody target, double time, double dt, Orbit vesselorbit)
		{
			var dist_at_int = new double[11];
			for (int i = 0; i <= 10; i++) {
				double step = time + i * dt;
				dist_at_int[i] = (target.getPositionAtUT(step) - vesselorbit.getPositionAtUT(step)).magnitude;
			}
			double mindist = dist_at_int.Min();
			double maxdist = dist_at_int.Max();
			int minindex = Array.IndexOf(dist_at_int, mindist);

			if ((maxdist - mindist) / maxdist >= 0.00001) {
				mindist = MinTargetDistance(target, time + ((minindex - 1) * dt), dt / 5, vesselorbit);
			}

			return mindist;
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

				// MOARdV: I think this gets the computation right.  High thrust will
				// result in NaN, which is already handled.
				/*
				double accelerationAtMaxThrust = localG - (totalMaximumThrust / totalShipWetMass);
				double timeToImpactAtMaxThrust = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * accelerationAtMaxThrust * altitude)) / accelerationAtMaxThrust;
				bestPossibleSpeedAtImpact = speedVertical - accelerationAtMaxThrust * timeToImpactAtMaxThrust;
				if (double.IsNaN(bestPossibleSpeedAtImpact))
					bestPossibleSpeedAtImpact = 0;
				*/
				bestPossibleSpeedAtImpact = SpeedAtImpact(totalMaximumThrust, totalShipWetMass, localG, speedVertical, altitude);
				expectedSpeedAtImpact = SpeedAtImpact(totalCurrentThrust, totalShipWetMass, localG, speedVertical, altitude);

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
			resources.Clear();
			totalShipDryMass = totalShipWetMass = totalCurrentThrust = totalMaximumThrust = 0;
			totalDataAmount = 0;
			double averageIspContribution = 0;

			anyEnginesOverheating = false;

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
						// This part is an engine, so check if it's overheating here.
						anyEnginesOverheating |= thatPart.temperature / thatPart.maxTemp > 0.7;

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

			// We can use the stock routines to get at the per-stage resources.
			activeResources.Clear();
			foreach (Vessel.ActiveResource thatResource in vessel.GetActiveResources()) {
				activeResources.Add(thatResource.info.name, new Vector2d(Math.Round(thatResource.amount, 2), Math.Round(thatResource.maxAmount, 2)));
			}

			// I seriously hope you don't have crew jumping in and out more than once per second.
			vesselCrew = (vessel.GetVesselCrew()).ToArray();
			// The sneaky bit: This way we can get at their panic and whee values!
			vesselCrewMedical = new kerbalExpressionSystem[vesselCrew.Length];
			for (int i = 0; i < vesselCrew.Length; i++) {
				vesselCrewMedical[i] = vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>();
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

		private static object ResourceListElement(string resourceName, string valueType, DefaultableDictionary<string,Vector2d> dataSource)
		{
			PartResourceDefinition resourceDef = PartResourceLibrary.Instance.GetDefinition(resourceName);
			switch (valueType) {
				case "":
				case "VAL":
					return dataSource[resourceName].x.Clamp(0d, dataSource[resourceName].y);
				case "DENSITY":
					if (resourceDef == null)
						return 0d;
					return resourceDef.density;
				case "MASS":
					if (resourceDef == null)
						return 0d;
					return resourceDef.density * dataSource[resourceName].x;
				case "MAXMASS":
					if (resourceDef == null)
						return 0d;
					return resourceDef.density * dataSource[resourceName].y;
				case "MAX":
					return dataSource[resourceName].y;
				case "PERCENT":
					return (dataSource[resourceName].y > 0) ? dataSource[resourceName].x / dataSource[resourceName].y : 0d;
			}
			return 0d;
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
					if (tokens[2] == "NAME") {
						return resourceID >= resources.Count ? string.Empty : resourcesAlphabetic[resourceID];
					}
					if (resourceID >= resources.Count)
						return 0d;
					return tokens[2].StartsWith("STAGE", StringComparison.Ordinal) ? 
						ResourceListElement(resourcesAlphabetic[resourceID], tokens[2].Substring("STAGE".Length), activeResources) : 
						ResourceListElement(resourcesAlphabetic[resourceID], tokens[2], resources);
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
					return actionGroupMemo[groupID];
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
					return altitudeASL - altitudeTrue;
				case "TERRAINHEIGHTLOG10":
					return JUtil.PseudoLog10(altitudeASL - altitudeTrue);

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
				case "EFFECTIVEACCEL":
					return vessel.acceleration.magnitude;
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
					return (speedVerticalRounded < 0 && !FlightGlobals.ActiveVessel.ActionGroups.groups[gearGroupNumber] && altitudeBottom < 100).GetHashCode();
				case "GROUNDPROXIMITYALARM":
					// Returns 1 if, at maximum acceleration, in the time remaining until ground impact, it is impossible to get a vertical speed higher than -10m/s.
					return (bestPossibleSpeedAtImpact < -10d).GetHashCode();
				case "TUMBLEALARM":
					return (speedVerticalRounded < 0 && altitudeTrue < 100 && horzVelocity > 5).GetHashCode();
				case "SLOPEALARM":
					return (speedVerticalRounded < 0 && altitudeTrue < 100 && slopeAngle > 15).GetHashCode();
				case "DOCKINGANGLEALARM":
					return (targetDockingNode != null && targetDistance < 10 && approachSpeed > 0 &&
					(Math.Abs(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, up)) > 1.5 ||
					Math.Abs(JUtil.NormalAngle(-targetDockingNode.GetFwdVector(), forward, -right)) > 1.5)).GetHashCode();
				case "DOCKINGSPEEDALARM":
					return (targetDockingNode != null && approachSpeed > 2.5 && targetDistance < 15).GetHashCode();
				case "ALTITUDEALARM":
					return (speedVerticalRounded < 0 && altitudeTrue < 150).GetHashCode();
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
					string argument = input.Substring(resourceType.Key.Length);
					if (argument.StartsWith("STAGE", StringComparison.Ordinal)) {
						argument = argument.Substring("STAGE".Length);
						return ResourceListElement(resourceType.Value, argument, activeResources);
					}
					return ResourceListElement(resourceType.Value, argument, resources);
				}
			}

			// Didn't recognise anything so we return the string we got, that helps debugging.
			return input;
		}
	}
}

