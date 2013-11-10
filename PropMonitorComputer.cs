using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JSI
{
	public class RasterPropMonitorComputer: InternalModule
	{
		public bool updateForced = false;
		// Data common for various variable calculations
		private int vesselNumParts;
		private int updateCountdown = 0;
		private int dataUpdateCountdown = 0;
		private int refreshRate = int.MaxValue;
		private int refreshDataRate = int.MaxValue;
		private Vector3d CoM;
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
		private ITargetable target;
		private Vector3d targetSeparation;
		private Quaternion targetOrientation;
		private ManeuverNode node;
		private double time;
		private ProtoCrewMember[] VesselCrew;
		private double altitudeASL;
		private double altitudeTrue;
		private Orbit targetorbit;
		private bool orbitSensibility = false;
		private bool targetOrbitSensibility = false;
		private Dictionary<string,Vector2d> resources = new Dictionary<string,Vector2d> ();
		private string[] resourcesAlphabetic;
		private double totalShipDryMass;
		private double totalShipWetMass;
		private double totalCurrentThrust;
		private double totalMaximumThrust;
		private double totalDataAmount;
		private double secondsToImpact;
		// Local data fetching variables...
		private int gearGroupNumber;
		private int brakeGroupNumber;
		private int SASGroupNumber;
		private int lightGroupNumber;
		private int RCSGroupNumber;

		public void Start ()
		{
			// Well, it looks like we have to do that bit just like in Firespitter.
			gearGroupNumber = BaseAction.GetGroupIndex (KSPActionGroup.Gear);
			brakeGroupNumber = BaseAction.GetGroupIndex (KSPActionGroup.Brakes);
			SASGroupNumber = BaseAction.GetGroupIndex (KSPActionGroup.SAS);
			lightGroupNumber = BaseAction.GetGroupIndex (KSPActionGroup.Light);
			RCSGroupNumber = BaseAction.GetGroupIndex (KSPActionGroup.RCS);

			if (HighLogic.LoadedSceneIsFlight)
				fetchPerPartData ();
		}

		public void updateRefreshRates (int rate, int dataRate)
		{
			refreshRate = Math.Min (rate, refreshRate);
			refreshDataRate = Math.Min (dataRate, refreshDataRate);
		}

		private bool updateCheck ()
		{
			if (vesselNumParts != vessel.Parts.Count || updateCountdown <= 0 || dataUpdateCountdown <= 0 || updateForced) {
				updateCountdown = refreshRate;
				if (vesselNumParts != vessel.Parts.Count || dataUpdateCountdown <= 0 || updateForced) {
					dataUpdateCountdown = refreshDataRate;
					vesselNumParts = vessel.Parts.Count;
					fetchPerPartData ();
				}
				updateForced = false;
				return true;
			} else {
				dataUpdateCountdown--;
				updateCountdown--;
				return false;
			}
		}

		public override void OnUpdate ()
		{
			if (!HighLogic.LoadedSceneIsFlight)
				return;

			if (!updateCheck ())
				return;

			if ((CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal) &&
			    vessel == FlightGlobals.ActiveVessel) {

				fetchCommonData ();
			}
		}
		// Sigh. MechJeb math.
		private double getCurrentThrust (ModuleEngines engine)
		{
			if ((!engine.EngineIgnited) || (!engine.isEnabled) || (!engine.isOperational))
				return 0;
			return engine.finalThrust;
		}

		private double getMaximumThrust (ModuleEngines engine)
		{
			if ((!engine.EngineIgnited) || (!engine.isEnabled) || (!engine.isOperational))
				return 0;
			return engine.maxThrust;
		}
		// Some snippets from MechJeb...
		private static double ClampDegrees360 (double angle)
		{
			angle = angle % 360.0;
			if (angle < 0)
				return angle + 360.0;
			else
				return angle;
		}
		//keeps angles in the range -180 to 180
		private double ClampDegrees180 (double angle)
		{
			angle = ClampDegrees360 (angle);
			if (angle > 180)
				angle -= 360;
			return angle;
		}

		public void fetchCommonData ()
		{
			CoM = vessel.findWorldCenterOfMass ();
			up = (CoM - vessel.mainBody.position).normalized;
			forward = vessel.GetTransform ().up;
			right = vessel.GetTransform ().right;
			north = Vector3d.Exclude (up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - CoM).normalized;
			rotationSurface = Quaternion.LookRotation (north, up);
			rotationVesselSurface = Quaternion.Inverse (Quaternion.Euler (90, 0, 0) * Quaternion.Inverse (vessel.GetTransform ().rotation) * rotationSurface);

			velocityVesselOrbit = vessel.orbit.GetVel ();
			velocityVesselSurface = velocityVesselOrbit - vessel.mainBody.getRFrmVel (CoM);

			speedVertical = Vector3d.Dot (velocityVesselSurface, up);
			target = FlightGlobals.fetch.VesselTarget;
			if (vessel.patchedConicSolver.maneuverNodes.Count > 0)
				node = vessel.patchedConicSolver.maneuverNodes.First ();
			else
				node = null;
			time = Planetarium.GetUniversalTime ();
			altitudeASL = vessel.mainBody.GetAltitude (CoM);
			fetchTrueAltitude ();
			if (target != null) {
				velocityRelativeTarget = vessel.orbit.GetVel () - target.GetOrbit ().GetVel ();
				targetSeparation = vessel.GetTransform ().position - target.GetTransform ().position;
				targetOrientation = target.GetTransform ().rotation;
				targetorbit = target.GetOrbit ();
				if (target is Vessel)
					targetOrbitSensibility = orbitMakesSense (target as Vessel);
				else
					targetOrbitSensibility = true;
			} else {
				velocityRelativeTarget = targetSeparation = Vector3d.zero;
				targetOrientation = new Quaternion ();
				targetOrbitSensibility = false;
			}
			orbitSensibility = orbitMakesSense (vessel);
			if (vessel.situation == Vessel.Situations.SUB_ORBITAL || vessel.situation == Vessel.Situations.FLYING)
				secondsToImpact = -(altitudeTrue / speedVertical);
			else
				secondsToImpact = Double.NaN;

		}

		public void fetchPerPartData ()
		{
			resources.Clear ();
			totalShipDryMass = totalShipWetMass = totalCurrentThrust = totalMaximumThrust = 0;
			totalDataAmount = 0;

			foreach (Part part in vessel.parts) {
				// The cute way of using vector2d in place of a tuple is from Firespitter.
				// Hey, it works.
				foreach (PartResource resource in part.Resources) {

					try { // I wonder if that's faster.
						resources.Add (resource.resourceName, new Vector2d (resource.amount, resource.maxAmount));
					} catch (ArgumentException) {
						resources [resource.resourceName] += new Vector2d (resource.amount, resource.maxAmount);
					}

					/*if (!resources.ContainsKey ((resource.resourceName)))
						resources.Add (resource.resourceName, new Vector2d (resource.amount, resource.maxAmount));
					else
						resources [resource.resourceName] += new Vector2d (resource.amount, resource.maxAmount);*/
				}
				totalShipDryMass += part.mass;
				totalShipWetMass += part.mass + part.GetResourceMass ();

				foreach (PartModule pm in part.Modules) {
					if (!pm.isEnabled)
						continue;
					if (pm is ModuleEngines) {
						totalCurrentThrust += getCurrentThrust (pm as ModuleEngines);
						totalMaximumThrust += getMaximumThrust (pm as ModuleEngines);
					} 
				}

				foreach (IScienceDataContainer container in part.FindModulesImplementing<IScienceDataContainer>()) {
					ScienceData[] data = container.GetData ();
					foreach (ScienceData datapoint in data) {
						if (datapoint != null)
							totalDataAmount += datapoint.dataAmount;
					}
				}

			}

			resourcesAlphabetic = resources.Keys.ToArray ();

			// Turns out, all those extra small tails in resources interfere with string formatting.
			foreach (string resource in resourcesAlphabetic) {
				Vector2d values = resources [resource];
				resources [resource] = new Vector2d (Math.Round (values.x, 2), values.y);
			}

			Array.Sort (resourcesAlphabetic);
			// I seriously hope you don't have crew jumping in and out more than once per second.
			VesselCrew = (vessel.GetVesselCrew ()).ToArray ();
		}

		private double getResourceByName (string name)
		{
			Vector2d result;
			if (resources.TryGetValue (name, out result))
				return result.x;
			else
				return 0;
		}

		private double getMaxResourceByName (string name)
		{
			Vector2d result;
			if (resources.TryGetValue (name, out result))
				return result.y;
			else
				return 0;
		}

		private static string AngleToDMS (double angle)
		{
			int degrees = (int)Math.Floor (Math.Abs (angle));
			int minutes = (int)Math.Floor (60 * (Math.Abs (angle) - degrees));
			int seconds = (int)Math.Floor (3600 * (Math.Abs (angle) - degrees - minutes / 60.0));

			return String.Format ("{0:0}° {1:00}' {2:00}\"", degrees, minutes, seconds);
		}

		private static string latitudeDMS (double latitude)
		{
			return AngleToDMS (latitude) + (latitude > 0 ? " N" : " S");
		}

		private string longitudeDMS (double longitude)
		{
			double clampedLongitude = ClampDegrees180 (longitude);
			return AngleToDMS (clampedLongitude) + (clampedLongitude > 0 ? " E" : " W");
		}

		private static Vector3d SwapYZ (Vector3d v)
		{
			return v.xzy;
		}

		private static Vector3d SwappedOrbitNormal (Orbit o)
		{
			return -SwapYZ (o.GetOrbitNormal ()).normalized;
		}
		// Another piece from MechJeb.
		private void fetchTrueAltitude ()
		{
			RaycastHit sfc;
			if (Physics.Raycast (CoM, -up, out sfc, (float)altitudeASL + 10000.0F, 1 << 15)) {
				altitudeTrue = sfc.distance;
			} else if (vessel.mainBody.pqsController != null) {
				// from here: http://kerbalspaceprogram.com/forum/index.php?topic=10324.msg161923#msg161923
				altitudeTrue = vessel.mainBody.GetAltitude (CoM) -
				(vessel.mainBody.pqsController.GetSurfaceHeight (QuaternionD.AngleAxis (vessel.mainBody.GetLongitude (CoM), Vector3d.down) *
				QuaternionD.AngleAxis (vessel.mainBody.GetLatitude (CoM), Vector3d.forward) *
				Vector3d.right) - vessel.mainBody.pqsController.radius);
			} else
				altitudeTrue = vessel.mainBody.GetAltitude (CoM);
		}

		private bool orbitMakesSense (Vessel thatvessel)
		{
			if (thatvessel.situation == Vessel.Situations.FLYING ||
			    thatvessel.situation == Vessel.Situations.SUB_ORBITAL ||
			    thatvessel.situation == Vessel.Situations.ORBITING ||
			    thatvessel.situation == Vessel.Situations.ESCAPING ||
			    thatvessel.situation == Vessel.Situations.DOCKED) // Not sure about this last one.
				return true;
			return false;
		}

		private static float normalAngle (Vector3 a, Vector3 b, Vector3 up)
		{
			return signedAngle (Vector3.Cross (up, a), Vector3.Cross (up, b), up);
		}

		private static float signedAngle (Vector3 v1, Vector3 v2, Vector3 up)
		{
			if (Vector3.Dot (Vector3.Cross (v1, v2), up) < 0)
				return -Vector3.Angle (v1, v2);
			return Vector3.Angle (v1, v2);
		}
		// According to C# specification, switch-case is compiled to a constant hash table.
		// So this is actually more efficient than a dictionary, who'd have thought.
		private static string situationString (Vessel.Situations situation)
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

		private static string FormatDateTime (double seconds, bool signed, bool noyears, bool plusskip)
		{
			// I'd love to know when exactly does this happen, but I'll let it slide for now..
			if (Double.IsNaN (seconds))
				return "";

			TimeSpan span = TimeSpan.FromSeconds (Math.Abs (seconds));
			int years = (int)Math.Floor (span.TotalDays / 365);
			span -= new TimeSpan (365 * years, 0, 0, 0);
			double fracseconds = Math.Round (span.TotalSeconds - Math.Floor (span.TotalSeconds), 1);

			string formatstring = (signed ? (plusskip ? "{0:+;-; }" : "{0: ;-; }") : "") + (noyears ? "" : "{1:00}:") + "{2:000}:{3:00}:{4:00}:{5:00.0}";

			return String.Format (formatstring, Math.Sign (seconds), years, span.Days, span.Hours, span.Minutes, span.Seconds + fracseconds);

		}

		public object processVariable (string input)
		{
			switch (input) {

			// It's a bit crude, but it's simple enough to populate.
			// Would be a bit smoother if I had eval() :)

			// Speeds.
			case "VERTSPEED":
				return speedVertical;
			case "SURFSPEED":
				return velocityVesselSurface.magnitude;
			case "ORBTSPEED":
				return velocityVesselOrbit.magnitude;
			case "TRGTSPEED":
				return velocityRelativeTarget.magnitude;
			case "HORZVELOCITY":
				return (velocityVesselSurface - (speedVertical * up)).magnitude;
			// The way Engineer does it...
			case "TGTRELX":
				return FlightGlobals.ship_tgtVelocity.x;
			case "TGTRELY":
				return FlightGlobals.ship_tgtVelocity.y;
			case "TGTRELZ":
				return FlightGlobals.ship_tgtVelocity.z;

			// Time to impact. This is VERY VERY imprecise because a precise calculation pulls in pages upon pages of MechJeb code.
			// If anyone's up to doing that smoothly be my guest.
			case "TIMETOIMPACT":
				if (Double.IsNaN (secondsToImpact) || secondsToImpact > 365 * 24 * 60 * 60 || secondsToImpact < 0) {
					return "";
				} else
					return FormatDateTime (secondsToImpact, false, true, false); 
			case "TIMETOIMPACTSECS":
				if (Double.IsNaN (secondsToImpact) || secondsToImpact > 365 * 24 * 60 * 60 || secondsToImpact < 0)
					return -1;
				else
					return secondsToImpact;

			// Altitudes
			case "ALTITUDE":
				return altitudeASL;
			case "RADARALT":
				return altitudeTrue;

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
				return totalCurrentThrust / (totalShipWetMass * vessel.orbit.referenceBody.GeeASL * 9.81);
			case "TWRMAX":
				return totalMaximumThrust / (totalShipWetMass * vessel.orbit.referenceBody.GeeASL * 9.81);
			case "ACCEL":
				return totalCurrentThrust / totalShipWetMass;
			case "MAXACCEL":
				return totalMaximumThrust / totalShipWetMass;
			case "GFORCE":
				return vessel.geeForce_immediate;
			case "THROTTLE":
				return vessel.ctrlState.mainThrottle;

			// Maneuvers
			case "MNODETIME":
				if (node != null)
					return FormatDateTime (-(node.UT - time), true, false, true);
				else
					return "";
			case "MNODEDV":
				if (node != null)
					return node.GetBurnVector (vessel.orbit).magnitude;
				else
					return 0;
			case "MNODEEXISTS":
				if (node != null)
					return 1;
				else
					return -1;

			// Orbital parameters
			case "ORBITBODY":
				return vessel.orbit.referenceBody.name;
			case "PERIAPSIS":
				if (orbitSensibility)
					return FlightGlobals.ship_orbit.PeA;
				else
					return 0;
			case "APOAPSIS":
				if (orbitSensibility)
					return FlightGlobals.ship_orbit.ApA;
				else
					return 0;
			case "INCLINATION":
				if (orbitSensibility)
					return FlightGlobals.ship_orbit.inclination;
				else
					return 0;
			case "ECCENTRICITY":
				if (orbitSensibility)
					return vessel.orbit.eccentricity;
				else
					return 0;
			// Time to apoapsis and periapsis are converted to DateTime objects and their formatting trickery applies.
			case "ORBPERIOD":
				if (orbitSensibility)
					return FormatDateTime (vessel.orbit.period, false, false, false);
				else
					return "";
			case "TIMETOAP":
				if (orbitSensibility)
					return FormatDateTime (vessel.orbit.timeToAp, false, false, false);
				else
					return "";
			case "TIMETOPE":
				if (orbitSensibility) {
					if (vessel.orbit.eccentricity < 1)
						return FormatDateTime (vessel.orbit.timeToPe, true, false, false);
					else
						return FormatDateTime (-vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period), true, false, false);
				} else
					return "";
			case "ORBITMAKESSENSE":
				if (orbitSensibility)
					return 1;
				else
					return -1;

			// Time
			case "UT":
				return FormatDateTime (time + 365 * 24 * 60 * 60, false, false, false);
			case "MET":
				return FormatDateTime (vessel.missionTime, false, false, false);

			// Names!
			case "NAME":
				return vessel.vesselName;


			// Coordinates.
			case "LATITUDE":
				return vessel.mainBody.GetLatitude (CoM);
			case "LONGITUDE":
				return ClampDegrees180 (vessel.mainBody.GetLongitude (CoM));
			case "LATITUDETGT":
				if (target is Vessel) {
					return target.GetVessel ().mainBody.GetLatitude (target.GetTransform ().position);
				} else
					return vessel.mainBody.GetLatitude (CoM);
			case "LONGITUDETGT":
				if (target is Vessel) {
					return ClampDegrees180 (target.GetVessel ().mainBody.GetLatitude (target.GetTransform ().position));
				} else
					return ClampDegrees180 (vessel.mainBody.GetLatitude (CoM));


			// Coordinates in degrees-minutes-seconds. Strictly speaking it would be better to teach String.Format to handle them, but that is currently beyond me.
			case "LATITUDE_DMS":
				return latitudeDMS (vessel.mainBody.GetLatitude (CoM));
			case "LONGITUDE_DMS":
				return longitudeDMS (vessel.mainBody.GetLongitude (CoM));
			case "LATITUDETGT_DMS":
				if (target is Vessel) {
					return latitudeDMS (target.GetVessel ().mainBody.GetLatitude (target.GetVessel ().GetWorldPos3D ()));
				} else
					return latitudeDMS (vessel.mainBody.GetLatitude (CoM));
			case "LONGITUDETGT_DMS":
				if (target is Vessel) {
					return longitudeDMS (ClampDegrees180 (target.GetVessel ().mainBody.GetLongitude (target.GetVessel ().GetWorldPos3D ())));
				} else
					return longitudeDMS (ClampDegrees180 (vessel.mainBody.GetLongitude (CoM)));


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
					return "";
				if (target is Vessel || target is CelestialBody)
					return target.GetName ();
				// Later, I think I want to get this to return the ship's name, not the docking node name...
				if (target is ModuleDockingNode)
					return target.GetName ();
				return "???!";
			case "TARGETDISTANCE":
				if (target != null) {
					return Vector3.Distance (target.GetTransform ().position, vessel.GetTransform ().position);
				} else
					return -1;
			case "RELATIVEINCLINATION":
				if (target != null) {
					if (targetorbit.referenceBody != vessel.orbit.referenceBody)
						return -1;
					return Math.Abs (Vector3d.Angle (SwappedOrbitNormal (vessel.GetOrbit ()), SwappedOrbitNormal (targetorbit)));
				} else
					return -1;
			case "TARGETORBITBODY":
				if (target != null)
					return targetorbit.referenceBody.name;
				else
					return "";
			case "TARGETEXISTS":
				if (target == null)
					return -1;
				if (target is Vessel)
					return 1;
				return 0;
			case "TARGETSITUATION":
				if (target is Vessel)
					return situationString (target.GetVessel ().situation);
				else
					return "";
			case "TARGETALTITUDE":
				if (target == null)
					return -1;
				if (target is Vessel) {
					return (target as Vessel).mainBody.GetAltitude ((target as Vessel).findWorldCenterOfMass ());
				} else
					return targetorbit.altitude;

			// Ok, what are X, Y and Z here anyway?
			case "TARGETDISTANCEX":
				return Vector3d.Dot (targetSeparation, vessel.GetTransform ().right);
			case "TARGETDISTANCEY":
				return Vector3d.Dot (targetSeparation, vessel.GetTransform ().forward);
			case "TARGETDISTANCEZ":
				return Vector3d.Dot (targetSeparation, vessel.GetTransform ().up);

			// I probably should return something else for vessels. But not sure what exactly right now.
			case "TARGETANGLEX":
				if (target != null) {
					if (target is ModuleDockingNode)
						return normalAngle (-(target as ModuleDockingNode).GetFwdVector (), forward, up);
					else if (target is Vessel) {
						return normalAngle (-target.GetFwdVector (), forward, up);
					}
					return 0;
				} else
					return 0;
			case "TARGETANGLEY":
				if (target != null) {
					if (target is ModuleDockingNode)
						return normalAngle (-(target as ModuleDockingNode).GetFwdVector (), forward, -right);
					if (target is Vessel) {
						normalAngle (-target.GetFwdVector (), forward, -right);
					}
					return 0;
				} else
					return 0;
			case "TARGETANGLEZ":
				if (target != null) {
					if (target is ModuleDockingNode)
						return normalAngle ((target as ModuleDockingNode).GetTransform ().up, up, -forward);
					if (target is Vessel) {
						return normalAngle (target.GetTransform ().up, up, -forward);
					}
					return 0;
				} else
					return 0;
			
			// There goes the neighbourhood...
			case "TARGETAPOAPSIS":
				if (target != null && targetOrbitSensibility)
					return targetorbit.ApA;
				else
					return 0;
			case "TARGETPERIAPSIS":
				if (target != null && targetOrbitSensibility)
					return targetorbit.PeA;
				else
					return 0;
			case "TARGETINCLINATION":
				if (target != null && targetOrbitSensibility)
					return targetorbit.inclination;
				else
					return 0;
			case "TARGETECCENTRICITY":
				if (target != null && targetOrbitSensibility)
					return targetorbit.eccentricity;
				else
					return 0;
			case "TARGETORBITALVEL":
				if (target != null && targetOrbitSensibility)
					return targetorbit.orbitalSpeed;
				else
					return 0;
			case "TARGETTIMETOAP":
				if (target != null && targetOrbitSensibility)
					return FormatDateTime (targetorbit.timeToAp, false, false, false);
				else
					return "";
			case "TARGETORBPERIOD":
				if (target != null && targetOrbitSensibility)
					return FormatDateTime (targetorbit.period, false, false, false);
				else
					return "";
			case "TARGETTIMETOPE":
				if (target != null && targetOrbitSensibility) {
					if (vessel.orbit.eccentricity < 1)
						return FormatDateTime (targetorbit.timeToPe, true, false, false);
					else
						return FormatDateTime (-targetorbit.meanAnomaly / (2 * Math.PI / targetorbit.period), true, false, false);
				} else
					return "";


			// Stock resources by name.
			case "ELECTRIC":
				return getResourceByName ("ElectricCharge");
			case "ELECTRICMAX":
				return getMaxResourceByName ("ElectricCharge");
			case "FUEL":
				return getResourceByName ("LiquidFuel");
			case "FUELMAX":
				return getMaxResourceByName ("LiquidFuel");
			case "OXIDIZER":
				return getResourceByName ("Oxidizer");
			case "OXIDIZERMAX":
				return getMaxResourceByName ("Oxidizer");
			case "MONOPROP":
				return getResourceByName ("MonoPropellant");
			case "MONOPROPMAX":
				return getMaxResourceByName ("MonoPropellant");
			case "XENON":
				return getResourceByName ("XenonGas");
			case "XENONMAX":
				return getMaxResourceByName ("XenonGas");

			// Staging and other stuff
			case "STAGE":
				return Staging.CurrentStage;
			case "SITUATION":
				return situationString (vessel.situation);

			// SCIENCE!!
			case "SCIENCEDATA":
				return totalDataAmount;

			// Action group flags. To properly format those, use this format:
			// {0:on;0;OFF}
			case "GEAR":
				return FlightGlobals.ActiveVessel.ActionGroups.groups [gearGroupNumber].GetHashCode ();
			case "BRAKES":
				return FlightGlobals.ActiveVessel.ActionGroups.groups [brakeGroupNumber].GetHashCode ();
			case "SAS":
				return FlightGlobals.ActiveVessel.ActionGroups.groups [SASGroupNumber].GetHashCode ();
			case "LIGHTS":
				return FlightGlobals.ActiveVessel.ActionGroups.groups [lightGroupNumber].GetHashCode ();
			case "RCS":
				return FlightGlobals.ActiveVessel.ActionGroups.groups [RCSGroupNumber].GetHashCode ();

			}
			// If input starts with "LISTR" we're handling it specially -- it's a list of all resources.
			// The variables are named like LISTR_<number>_<NAME|VAL|MAX>
			string[] tokens = input.Split ('_');

			if (tokens.Length == 3 && tokens [0] == "LISTR") {
				ushort resourceID = Convert.ToUInt16 (tokens [1]);
				switch (tokens [2]) {
				case "NAME":
					if (resourceID >= resources.Count)
						return "";
					else
						return resourcesAlphabetic [resourceID];
				case "VAL":
					if (resourceID >= resources.Count)
						return 0;
					else
						return resources [resourcesAlphabetic [resourceID]].x;
				case "MAX":
					if (resourceID >= resources.Count)
						return 0;
					else
						return resources [resourcesAlphabetic [resourceID]].y;
				}


			}

			// We do similar things for crew rosters.
			// The syntax is therefore CREW_<index>_<FIRST|LAST|FULL>
			if (tokens.Length == 3 && tokens [0] == "CREW") { 
				ushort crewSeatID = Convert.ToUInt16 (tokens [1]);
				if (tokens [2] == "PRESENT") {
					if (crewSeatID >= VesselCrew.Length)
						return -1;
					else
						return 1;
				}
				if (crewSeatID >= VesselCrew.Length)
					return "";
				string kerbalname = VesselCrew [crewSeatID].name;
				string[] tokenisedname = kerbalname.Split ();
				switch (tokens [2]) {
				case "FIRST":
					return tokenisedname [0];
				case "LAST":
					return tokenisedname [1];
				case "FULL":
					return kerbalname;
				default:
					return "???!";
				}
			}

			// Didn't recognise anything so we return the string we got, that helps debugging.
			return input;
		}
	}
}

