using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

namespace JSI
{
	public static class MapIcons
	{
		public enum OtherIcon
		{
			None,
			PE,
			AP,
			AN,
			DN,
			NODE,
			SHIPATINTERCEPT,
			TGTATINTERCEPT,
			ENTERSOI,
			EXITSOI,
			PLANET,
		}

		public static Rect VesselTypeIcon(VesselType type, OtherIcon icon)
		{
			int x = 0;
			int y = 0;
			const float symbolSpan = 0.2f;
			if (icon != OtherIcon.None) {
				switch (icon) {
					case OtherIcon.AP:
						x = 1;
						y = 4;
						break;
					case OtherIcon.PE:
						x = 0;
						y = 4;
						break;
					case OtherIcon.AN:
						x = 2;
						y = 4;
						break;
					case OtherIcon.DN:
						x = 3;
						y = 4;
						break;
					case OtherIcon.NODE:
						x = 2;
						y = 1;
						break;
					case OtherIcon.SHIPATINTERCEPT:
						x = 0;
						y = 1;
						break;
					case OtherIcon.TGTATINTERCEPT:
						x = 1;
						y = 1;
						break;
					case OtherIcon.ENTERSOI:
						x = 0;
						y = 2;
						break;
					case OtherIcon.EXITSOI:
						x = 1;
						y = 2;
						break;
					case OtherIcon.PLANET:
						// Not sure if it is (2,3) or (3,2) - both are round
						x = 2;
						y = 3;
						break;
				}
			} else {
				switch (type) {
					case VesselType.Base:
						x = 2;
						y = 0;
						break;
					case VesselType.Debris:
						x = 1;
						y = 3;
						break;
					case VesselType.EVA:
						x = 2;
						y = 2;
						break;
					case VesselType.Flag:
						x = 4;
						y = 0;
						break;
					case VesselType.Lander:
						x = 3;
						y = 0;
						break;
					case VesselType.Probe:
						x = 1;
						y = 0;
						break;
					case VesselType.Rover:
						x = 0;
						y = 0;
						break;
					case VesselType.Ship:
						x = 0;
						y = 3;
						break;
					case VesselType.Station:
						x = 3;
						y = 1;
						break;
					case VesselType.Unknown:
						x = 3;
						y = 3;
						break;
					default:
						x = 3;
						y = 2;
						break;
				}
			}
			var result = new Rect();
			result.x = symbolSpan * x;
			result.y = symbolSpan * y;
			result.height = result.width = symbolSpan;
			return result;
		}
	}

	public static class GizmoIcons
	{
		public enum IconType
		{
			PROGRADE,
			RETROGRADE,
			MANEUVERPLUS,
			MANEUVERMINUS,
			TARGETPLUS,
			TARGETMINUS,
			NORMALPLUS,
			NORMALMINUS,
			RADIALPLUS,
			RADIALMINUS,
		};

		public static Rect GetIconLocation(IconType type)
		{
			Rect loc = new Rect(0.0f, 0.0f, 1.0f/3.0f, 1.0f/3.0f);
			switch(type)
			{
				case IconType.PROGRADE:
					loc.x = 0.0f / 3.0f;
					loc.y = 2.0f / 3.0f;
					break;
				case IconType.RETROGRADE:
					loc.x = 1.0f / 3.0f;
					loc.y = 2.0f / 3.0f;
					break;
				case IconType.MANEUVERPLUS:
					loc.x = 2.0f / 3.0f;
					loc.y = 0.0f / 3.0f;
					break;
				case IconType.MANEUVERMINUS:
					loc.x = 1.0f / 3.0f;
					loc.y = 2.0f / 3.0f;
					break;
				case IconType.TARGETPLUS:
					loc.x = 2.0f / 3.0f;
					loc.y = 2.0f / 3.0f;
					break;
				case IconType.TARGETMINUS:
					loc.x = 2.0f / 3.0f;
					loc.y = 1.0f / 3.0f;
					break;
				case IconType.NORMALPLUS:
					loc.x = 0.0f / 3.0f;
					loc.y = 0.0f / 3.0f;
					break;
				case IconType.NORMALMINUS:
					loc.x = 1.0f / 3.0f;
					loc.y = 0.0f / 3.0f;
					break;
				case IconType.RADIALPLUS:
					loc.x = 1.0f / 3.0f;
					loc.y = 1.0f / 3.0f;
					break;
				case IconType.RADIALMINUS:
					loc.x = 0.0f / 3.0f;
					loc.y = 1.0f / 3.0f;
					break;
			}

			return loc;
		}
	}

	public static class JUtil
	{
		public static readonly string[] VariableListSeparator = { "$&$" };
		public static readonly string[] VariableSeparator = { };
		public static readonly string[] LineSeparator = { Environment.NewLine };

		public static void MakeReferencePart(this Part thatPart)
		{
			if (thatPart != null) {
				foreach (PartModule thatModule in thatPart.Modules) {
					var thatNode = thatModule as ModuleDockingNode;
					var thatPod = thatModule as ModuleCommand;
					if (thatNode != null) {
						thatNode.MakeReferenceTransform();
						break;
					}
					if (thatPod != null) {
						thatPod.MakeReference();
						break;
					}
				}
			}
		}
		/* I wonder why this isn't working. 
		 * It's like the moment I unseat a kerbal, no matter what else I do,
		 * the entire internal goes poof. Although I'm pretty sure it doesn't quite,
		 * because the modules keep working and generating errors.
		 * What's really going on here, and why the same thing works for Crew Manifest?
		public static void ReseatKerbalInPart(this Kerbal thatKerbal) {
			if (thatKerbal.InPart == null || !JUtil.VesselIsInIVA(thatKerbal.InPart.vessel))
				return;

			InternalModel thatModel = thatKerbal.InPart.internalModel;
			Part thatPart = thatKerbal.InPart;
			int spareSeat = thatModel.GetNextAvailableSeatIndex();
			if (spareSeat >= 0) {
				ProtoCrewMember crew = thatKerbal.protoCrewMember;
				CameraManager.Instance.SetCameraFlight();
				thatPart.internalModel.UnseatKerbal(crew);
				thatPart.internalModel.SitKerbalAt(crew,thatPart.internalModel.seats[spareSeat]);
				thatPart.internalModel.part.vessel.SpawnCrew();
				CameraManager.Instance.SetCameraIVA(thatPart.internalModel.seats[spareSeat].kerbalRef,true);
			}
		}
		*/
		public static bool ActiveKerbalIsLocal(this Part thisPart)
		{
			return FindCurrentKerbal(thisPart) != null;
		}

		public static int CurrentActiveSeat(this Part thisPart)
		{
			Kerbal activeKerbal = thisPart.FindCurrentKerbal();
			return activeKerbal != null ? activeKerbal.protoCrewMember.seatIdx : -1;
		}

		public static Kerbal FindCurrentKerbal(this Part thisPart)
		{
			if (thisPart.internalModel == null || !JUtil.VesselIsInIVA(thisPart.vessel))
				return null;
			// InternalCamera instance does not contain a reference to the kerbal it's looking from.
			// So we have to search through all of them...
			Kerbal thatKerbal = null;
			foreach (InternalSeat thatSeat in thisPart.internalModel.seats) {
				if (thatSeat.kerbalRef != null) {
					if (thatSeat.kerbalRef.eyeTransform == InternalCamera.Instance.transform.parent) {
						thatKerbal = thatSeat.kerbalRef;
						break;
					}
				}
			}
			return thatKerbal;
		}

		public static Material DrawLineMaterial()
		{
			var lineMaterial = new Material("Shader \"Lines/Colored Blended\" {" +
			                   "SubShader { Pass {" +
			                   "   BindChannels { Bind \"Color\",color }" +
			                   "   Blend SrcAlpha OneMinusSrcAlpha" +
			                   "   ZWrite Off Cull Off Fog { Mode Off }" +
			                   "} } }");
			lineMaterial.hideFlags = HideFlags.HideAndDontSave;
			lineMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
			return lineMaterial;
		}

		public static Texture2D GetGizmoTexture()
		{
			// This clever method at getting at the stock texture asset originates in Enhanced Navball.
			ManeuverGizmo maneuverGizmo = MapView.ManeuverNodePrefab.GetComponent<ManeuverGizmo>();
			ManeuverGizmoHandle maneuverGizmoHandle = maneuverGizmo.handleNormal;
			Transform gizmoTransform = maneuverGizmoHandle.flag;
			Renderer gizmoRenderer = gizmoTransform.renderer;
			return (Texture2D)gizmoRenderer.sharedMaterial.mainTexture;
		}

		public static void AnnoyUser(object caller)
		{
			ScreenMessages.PostScreenMessage(string.Format("{0}: INITIALIZATION ERROR, CHECK CONFIGURATION.", caller.GetType().Name), 120, ScreenMessageStyle.UPPER_CENTER);
		}

		public static bool VesselIsInIVA(Vessel thatVessel)
		{
			// TODO: Inactive IVAs are renderer.enabled = false, this can and should be used;
			return IsActiveVessel(thatVessel) && IsInIVA();
		}

		public static bool IsActiveVessel(Vessel thatVessel)
		{
			return (HighLogic.LoadedSceneIsFlight && thatVessel.isActiveVessel);
		}

		public static bool IsInIVA()
		{
			return CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA || CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal;
		}

		public static void LogMessage(object caller, string line, params object[] list)
		{
			Debug.Log(String.Format(caller.GetType().Name + ": " + line, list));
		}

		public static void LogErrorMessage(object caller, string line, params object[] list)
		{
			Debug.LogError(String.Format(caller.GetType().Name + ": " + line, list));
		}
		// Working in a generic to make that a generic function for all numbers is too much work
		// and we only need these two anyway.
		public static float DualLerp(float from, float to, float from2, float to2, float value)
		{
			if (from2 < to2) {
				if (value < from2)
					value = from2;
				else if (value > to2)
					value = to2;
			} else {
				if (value < to2)
					value = to2;
				else if (value > from2)
					value = from2;	
			}
			return (to - from) * ((value - from2) / (to2 - from2)) + from;
		}

		public static double DualLerp(double from, double to, double from2, double to2, double value)
		{
			if (from2 < to2) {
				if (value < from2)
					value = from2;
				else if (value > to2)
					value = to2;
			} else {
				if (value < to2)
					value = to2;
				else if (value > from2)
					value = from2;	
			}
			return (to - from) * ((value - from2) / (to2 - from2)) + from;
		}
		// Convert a variable to a log10-like value (log10 for values > 1,
		// pass-through for values [-1, 1], and -log10(abs(value)) for values
		// < -1.  Useful for logarithmic VSI and altitude strips.
		public static double PseudoLog10(double value)
		{
			if (Math.Abs(value) <= 1.0) {
				return value;
			}
			return (1.0 + Math.Log10(Math.Abs(value))) * Math.Sign(value);
		}

		public static float PseudoLog10(float value)
		{
			if (Mathf.Abs(value) <= 1.0f) {
				return value;
			}
			return (1.0f + Mathf.Log10(Mathf.Abs(value))) * Mathf.Sign(value);
		}

		public static string LoadPageDefinition(string pageDefinition)
		{
			try {
				return string.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + pageDefinition.EnforceSlashes(), Encoding.UTF8));
			} catch {
				return pageDefinition.UnMangleConfigText();
			}
		}

		public static Color32 HexRGBAToColor(string hex)
		{
			byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
			byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
			byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
			byte a = hex.Length >= 8 ? byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) : byte.MaxValue;
			return new Color32(r, g, b, a);
		}

		public static string ColorToColorTag(Color32 color)
		{
			var result = new StringBuilder();
			result.Append("[#");
			result.Append(color.r.ToString("X").PadLeft(2, '0'));
			result.Append(color.g.ToString("X").PadLeft(2, '0'));
			result.Append(color.b.ToString("X").PadLeft(2, '0'));
			result.Append(color.a.ToString("X").PadLeft(2, '0'));
			result.Append("]");
			return result.ToString();
		}

		public static bool OrbitMakesSense(Vessel thatVessel)
		{
			if (thatVessel == null)
				return false;
			if (thatVessel.situation == Vessel.Situations.FLYING ||
			    thatVessel.situation == Vessel.Situations.SUB_ORBITAL ||
			    thatVessel.situation == Vessel.Situations.ORBITING ||
			    thatVessel.situation == Vessel.Situations.ESCAPING ||
			    thatVessel.situation == Vessel.Situations.DOCKED) // Not sure about this last one.
				return true;
			return false;
		}

		public static FXGroup SetupIVASound(InternalProp thatProp, string buttonClickSound, float buttonClickVolume, bool loopState)
		{
			FXGroup audioOutput = null;
			if (!string.IsNullOrEmpty(buttonClickSound.EnforceSlashes())) {
				audioOutput = new FXGroup("RPM" + thatProp.propID);
				audioOutput.audio = thatProp.gameObject.AddComponent<AudioSource>();
				audioOutput.audio.clip = GameDatabase.Instance.GetAudioClip(buttonClickSound);
				audioOutput.audio.Stop();
				audioOutput.audio.volume = GameSettings.SHIP_VOLUME * buttonClickVolume;
				audioOutput.audio.rolloffMode = AudioRolloffMode.Logarithmic;
				audioOutput.audio.maxDistance = 10f;
				audioOutput.audio.minDistance = 2f;
				audioOutput.audio.dopplerLevel = 0f;
				audioOutput.audio.panLevel = 1f;
				audioOutput.audio.playOnAwake = false;
				audioOutput.audio.loop = loopState;
				audioOutput.audio.pitch = 1f;
			}
			return audioOutput;
		}

		public static string WordWrap(string text, int maxLineLength)
		{
			var sb = new StringBuilder();
			char[] prc = { ' ', ',', '.', '?', '!', ':', ';', '-' };
			char[] ws = { ' ' };

			foreach (string line in text.Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)) {
				int currentIndex;
				int lastWrap = 0;
				do {
					currentIndex = lastWrap + maxLineLength > line.Length ? line.Length : (line.LastIndexOfAny(prc, Math.Min(line.Length - 1, lastWrap + maxLineLength)) + 1);
					if (currentIndex <= lastWrap)
						currentIndex = Math.Min(lastWrap + maxLineLength, line.Length);
					sb.AppendLine(line.Substring(lastWrap, currentIndex - lastWrap).Trim(ws));
					lastWrap = currentIndex;
				} while(currentIndex < line.Length);
			}
			return sb.ToString();
		}
		// Some snippets from MechJeb...
		public static double ClampDegrees360(double angle)
		{
			angle = angle % 360.0;
			if (angle < 0)
				return angle + 360.0;
			return angle;
		}
		//keeps angles in the range -180 to 180
		public static double ClampDegrees180(double angle)
		{
			angle = ClampDegrees360(angle);
			if (angle > 180)
				angle -= 360;
			return angle;
		}

		public static double ClampRadiansTwoPi(double angle)
		{
			angle = angle % (2 * Math.PI);
			if (angle < 0)
				return angle + 2 * Math.PI;
			return angle;
		}
		//acosh(x) = log(x + sqrt(x^2 - 1))
		public static double Acosh(double x)
		{
			return Math.Log(x + Math.Sqrt(x * x - 1));
		}

		public static double NormalAngle(Vector3 a, Vector3 b, Vector3 up)
		{
			return SignedAngle(Vector3.Cross(up, a), Vector3.Cross(up, b), up);
		}

		public static double SignedAngle(Vector3 v1, Vector3 v2, Vector3 up)
		{
			return Vector3.Dot(Vector3.Cross(v1, v2), up) < 0 ? -Vector3.Angle(v1, v2) : Vector3.Angle(v1, v2);
		}
		//Another MechJeb function I have very little understanding of.
		public static CBAttributeMap.MapAttribute CBAttributeMapGetAtt(CBAttributeMap cbmap, double lat, double lon)
		{
			if (cbmap.Map == null) {
				return cbmap.defaultAttribute;
			}

			lon -= Math.PI / 2d;
			if (lon < 0d) {
				lon += 2d * Math.PI;
			}

			float v = (float)(lat / Math.PI) + 0.5f;
			float u = (float)(lon / (2d * Math.PI));

			Color pixelBilinear = cbmap.Map.GetPixelBilinear(u, v);
			CBAttributeMap.MapAttribute defaultAttribute = cbmap.defaultAttribute;
			if (!cbmap.exactSearch) {
				float maxValue = float.MaxValue;
				for (int i = 0; i < cbmap.Attributes.Length; i++) {
					var vector = (Vector4)(cbmap.Attributes[i].mapColor - pixelBilinear);
					float sqrMagnitude = vector.sqrMagnitude;
					// Analysis disable once CompareOfFloatsByEqualityOperator
					if ((sqrMagnitude < maxValue) && ((cbmap.nonExactThreshold == -1f) || (sqrMagnitude < cbmap.nonExactThreshold))) {
						defaultAttribute = cbmap.Attributes[i];
						maxValue = sqrMagnitude;
					}
				}
			} else
				for (int j = 0; j < cbmap.Attributes.Length; j++)
					if (pixelBilinear == cbmap.Attributes[j].mapColor) {
						defaultAttribute = cbmap.Attributes[j];
						break;
					}
			return defaultAttribute;
		}

		public static Orbit OrbitFromStateVectors(Vector3d pos, Vector3d vel, CelestialBody body, double UT)
		{
			Orbit ret = new Orbit();
			ret.UpdateFromStateVectors(OrbitExtensions.SwapYZ(pos - body.position), OrbitExtensions.SwapYZ(vel), body, UT);
			return ret;
		}

		// Closest Approach algorithms based on Protractor mod
		public static double GetClosestApproach(Orbit vesselOrbit, CelestialBody targetCelestial, out double timeAtClosestApproach)
		{
			Orbit closestorbit = GetClosestOrbit(vesselOrbit, targetCelestial);
			if (closestorbit.referenceBody == targetCelestial) {
				timeAtClosestApproach = closestorbit.StartUT + ((closestorbit.eccentricity < 1.0) ?
					closestorbit.timeToPe :
					-closestorbit.meanAnomaly / (2 * Math.PI / closestorbit.period));
				return closestorbit.PeA;
			}
			if (closestorbit.referenceBody == targetCelestial.referenceBody) {
				return MinTargetDistance(closestorbit, targetCelestial.orbit, closestorbit.StartUT, closestorbit.period / 10, out timeAtClosestApproach) - targetCelestial.Radius;
			}
			return MinTargetDistance(closestorbit, targetCelestial.orbit, Planetarium.GetUniversalTime(), closestorbit.period / 10, out timeAtClosestApproach) - targetCelestial.Radius;
		}

		public static double GetClosestApproach(Orbit vesselOrbit, Orbit targetOrbit, out double timeAtClosestApproach)
		{
			Orbit closestorbit = GetClosestOrbit(vesselOrbit, targetOrbit);

			return MinTargetDistance(closestorbit, targetOrbit, Planetarium.GetUniversalTime(), closestorbit.period / 10, out timeAtClosestApproach);
		}

		// Closest Approach support methods
		private static Orbit GetClosestOrbit(Orbit vesselOrbit, CelestialBody targetCelestial)
		{
			Orbit checkorbit = vesselOrbit;
			int orbitcount = 0;

			while (checkorbit.nextPatch != null && checkorbit.patchEndTransition != Orbit.PatchTransitionType.FINAL && orbitcount < 3) {
				checkorbit = checkorbit.nextPatch;
				orbitcount += 1;
				if (checkorbit.referenceBody == targetCelestial) {
					return checkorbit;
				}

			}
			checkorbit = vesselOrbit;
			orbitcount = 0;

			while (checkorbit.nextPatch != null && checkorbit.patchEndTransition != Orbit.PatchTransitionType.FINAL && orbitcount < 3) {
				checkorbit = checkorbit.nextPatch;
				orbitcount += 1;
				if (checkorbit.referenceBody == targetCelestial.orbit.referenceBody) {
					return checkorbit;
				}
			}

			return vesselOrbit;
		}

		private static Orbit GetClosestOrbit(Orbit vesselOrbit, Orbit targetOrbit)
		{
			Orbit checkorbit = vesselOrbit;
			int orbitcount = 0;

			while (checkorbit.nextPatch != null && checkorbit.patchEndTransition != Orbit.PatchTransitionType.FINAL && orbitcount < 3) {
				checkorbit = checkorbit.nextPatch;
				orbitcount += 1;
				if (checkorbit.referenceBody == targetOrbit.referenceBody) {
					return checkorbit;
				}

			}

			return vesselOrbit;
		}

		private static double MinTargetDistance(Orbit vesselOrbit, Orbit targetOrbit, double time, double dt, out double timeAtClosestApproach)
		{
			var dist_at_int = new double[11];
			for (int i = 0; i <= 10; i++) {
				double step = time + i * dt;
				dist_at_int[i] = (targetOrbit.getPositionAtUT(step) - vesselOrbit.getPositionAtUT(step)).magnitude;
			}
			double mindist = dist_at_int.Min();
			double maxdist = dist_at_int.Max();
			int minindex = Array.IndexOf(dist_at_int, mindist);

			if ((maxdist - mindist) / maxdist >= 0.00001) {
				mindist = MinTargetDistance(vesselOrbit, targetOrbit, time + ((minindex - 1) * dt), dt / 5, out timeAtClosestApproach);
			} else {
				timeAtClosestApproach = time + minindex * dt;
			}

			return mindist;
		}

		// Piling all the extension methods into the same utility class to reduce the number of classes.
		// Because DLL size. Not really important and probably a bad practice, but one function static classes are silly.
		public static float? GetFloat(this string source)
		{
			float result;
			return float.TryParse(source, out result) ? result : (float?)null;
		}

		public static float? GetFloat(this ConfigNode node, string valueName)
		{
			return node.HasValue(valueName) ? node.GetValue(valueName).GetFloat() : (float?)null;
		}

		public static int? GetInt(this string source)
		{
			int result;
			return int.TryParse(source, out result) ? result : (int?)null;
		}

		public static int? GetInt(this ConfigNode node, string valueName)
		{
			return node.HasValue(valueName) ? node.GetValue(valueName).GetInt() : (int?)null;
		}

		public static string EnforceSlashes(this string input)
		{
			return input.Replace('\\', '/');
		}

		public static string UnMangleConfigText(this string input)
		{
			return input.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
		}

		public static string MangleConfigText(this string input)
		{
			return input.Replace("{", "<=").Replace("}", "=>").Replace(Environment.NewLine, "$$$");
		}

		public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
		{
			if (val.CompareTo(min) < 0)
				return min;
			return val.CompareTo(max) > 0 ? max : val;
		}

		public static float MassageToFloat(this object thatValue)
		{
			// RPMC only produces doubles, floats, ints and strings.
			if (thatValue is double)
				return (float)(double)thatValue;
			if (thatValue is float)
				return (float)thatValue;
			if (thatValue is int)
				return (float)(int)thatValue;
			return float.NaN;
		}

		public static double MassageToDouble(this object thatValue)
		{
			// RPMC only produces doubles, floats, ints and strings.
			if (thatValue is double)
				return (double)thatValue;
			if (thatValue is float)
				return (double)(float)thatValue;
			if (thatValue is int)
				return (double)(int)thatValue;
			return double.NaN;
		}
	}
	// This, instead, is a static class on it's own because it needs it's private static variables.
	public static class InstallationPathWarning
	{
		private static readonly List<string> warnedList = new List<string>();
		private const string gameData = "GameData";
		private static readonly string[] pathSep = { gameData };

		public static void Warn(string path = "JSI/RasterPropMonitor/Plugins")
		{
			string assemblyPath = Assembly.GetCallingAssembly().Location;
			string fileName = Path.GetFileName(assemblyPath);
			if (!warnedList.Contains(fileName)) {
				string installedLocation = Path.GetDirectoryName(assemblyPath).Split(pathSep, StringSplitOptions.None)[1].TrimStart('/').TrimStart('\\').EnforceSlashes();
				if (installedLocation != path) {
					ScreenMessages.PostScreenMessage(string.Format("ERROR: {0} must be in GameData/{1} but it's in GameData/{2}", fileName, path, installedLocation),
						120, ScreenMessageStyle.UPPER_CENTER);
					Debug.LogError("RasterPropMonitor components are incorrectly installed. I should stop working and make you fix it, but KSP won't let me.");
				}
				warnedList.Add(fileName);
			}
		}
	}
	// This handy class is also from MechJeb.
	//A simple wrapper around a Dictionary, with the only change being that
	//accessing the value of a nonexistent key returns a default value instead of an error.
	class DefaultableDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	{
		readonly Dictionary<TKey, TValue> d = new Dictionary<TKey, TValue>();
		readonly TValue defaultValue;

		public DefaultableDictionary(TValue defaultValue)
		{
			this.defaultValue = defaultValue;
		}

		public TValue this [TKey key] {
			get {
				return d.ContainsKey(key) ? d[key] : defaultValue;
			}
			set {
				if (d.ContainsKey(key))
					d[key] = value;
				else
					d.Add(key, value);
			}
		}

		public void Add(TKey key, TValue value)
		{
			d.Add(key, value);
		}

		public bool ContainsKey(TKey key)
		{
			return d.ContainsKey(key);
		}

		public ICollection<TKey> Keys { get { return d.Keys; } }

		public bool Remove(TKey key)
		{
			return d.Remove(key);
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			return d.TryGetValue(key, out value);
		}

		public ICollection<TValue> Values { get { return d.Values; } }

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			((IDictionary<TKey, TValue>)d).Add(item);
		}

		public void Clear()
		{
			d.Clear();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return ((IDictionary<TKey, TValue>)d).Contains(item);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			((IDictionary<TKey, TValue>)d).CopyTo(array, arrayIndex);
		}

		public int Count { get { return d.Count; } }

		public bool IsReadOnly { get { return ((IDictionary<TKey, TValue>)d).IsReadOnly; } }

		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			return ((IDictionary<TKey, TValue>)d).Remove(item);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return d.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((System.Collections.IEnumerable)d).GetEnumerator();
		}
	}
}

