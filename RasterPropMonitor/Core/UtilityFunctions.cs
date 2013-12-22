using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace JSI
{
	public static class JUtil
	{
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
			return IsActiveVessel(thatVessel) && IsInIVA();
		}

		public static bool IsActiveVessel(Vessel thatVessel)
		{
			return (HighLogic.LoadedSceneIsFlight && (thatVessel == FlightGlobals.ActiveVessel));
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
		// Piling all the extension methods into the same utility class to reduce the number of classes.
		// Because DLL size. Not really important and probably a bad practice, but one function static classes are silly.
		public static string EnforceSlashes(this string input)
		{
			return input.Replace('\\', '/');
		}

		public static string UnMangleConfigText(this string input)
		{
			return input.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
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

	public static class CelestialBodyExtensions
	{
		public static double TerrainAltitude(this CelestialBody body, Vector3d worldPosition)
		{
			return body.TerrainAltitude(body.GetLatitude(worldPosition), body.GetLongitude(worldPosition));
		}

		public static double TerrainAltitude(this CelestialBody body, double latitude, double longitude)
		{
			if (body.pqsController == null)
				return 0;

			Vector3d pqsRadialVector = QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right;
			double ret = body.pqsController.GetSurfaceHeight(pqsRadialVector) - body.pqsController.radius;
			if (ret < 0)
				ret = 0;
			return ret;
		}
	}
	// Should I just import the entire class from MJ?...
	public static class OrbitExtensions
	{
		public static Vector3d SwapYZ(Vector3d v)
		{
			return v.xzy;
		}

		public static Vector3d SwappedOrbitNormal(this Orbit o)
		{
			return -SwapYZ(o.GetOrbitNormal()).normalized;
		}

		public static double TimeOfAscendingNode(this Orbit a, Orbit b, double uT)
		{
			return a.TimeOfTrueAnomaly(a.AscendingNodeTrueAnomaly(b), uT);
		}

		public static double TimeOfDescendingNode(this Orbit a, Orbit b, double uT)
		{
			return a.TimeOfTrueAnomaly(a.DescendingNodeTrueAnomaly(b), uT);
		}

		public static double TimeOfTrueAnomaly(this Orbit o, double trueAnomaly, double uT)
		{
			return o.UTAtMeanAnomaly(o.GetMeanAnomalyAtEccentricAnomaly(o.GetEccentricAnomalyAtTrueAnomaly(trueAnomaly)), uT);
		}

		public static double AscendingNodeTrueAnomaly(this Orbit a, Orbit b)
		{
			Vector3d vectorToAN = Vector3d.Cross(a.SwappedOrbitNormal(), b.SwappedOrbitNormal());
			return a.TrueAnomalyFromVector(vectorToAN);
		}

		public static double DescendingNodeTrueAnomaly(this Orbit a, Orbit b)
		{
			return JUtil.ClampDegrees360(a.AscendingNodeTrueAnomaly(b) + 180);
		}

		public static double TrueAnomalyFromVector(this Orbit o, Vector3d vec)
		{
			Vector3d projected = Vector3d.Exclude(o.SwappedOrbitNormal(), vec);
			Vector3d vectorToPe = SwapYZ(o.eccVec);
			double angleFromPe = Math.Abs(Vector3d.Angle(vectorToPe, projected));

			//If the vector points to the infalling part of the orbit then we need to do 360 minus the
			//angle from Pe to get the true anomaly. Test this by taking the the cross product of the
			//orbit normal and vector to the periapsis. This gives a vector that points to center of the 
			//outgoing side of the orbit. If vectorToAN is more than 90 degrees from this vector, it occurs
			//during the infalling part of the orbit.
			if (Math.Abs(Vector3d.Angle(projected, Vector3d.Cross(o.SwappedOrbitNormal(), vectorToPe))) < 90) {
				return angleFromPe;
			}
			return 360 - angleFromPe;
		}

		public static double UTAtMeanAnomaly(this Orbit o, double meanAnomaly, double uT)
		{
			double currentMeanAnomaly = o.MeanAnomalyAtUT(uT);
			double meanDifference = meanAnomaly - currentMeanAnomaly;
			if (o.eccentricity < 1)
				meanDifference = JUtil.ClampRadiansTwoPi(meanDifference);
			return uT + meanDifference / o.MeanMotion();
		}

		public static double MeanAnomalyAtUT(this Orbit o, double uT)
		{
			double ret = o.meanAnomalyAtEpoch + o.MeanMotion() * (uT - o.epoch);
			if (o.eccentricity < 1)
				ret = JUtil.ClampRadiansTwoPi(ret);
			return ret;
		}

		public static double MeanMotion(this Orbit o)
		{
			return Math.Sqrt(o.referenceBody.gravParameter / Math.Abs(Math.Pow(o.semiMajorAxis, 3)));
		}

		public static double GetMeanAnomalyAtEccentricAnomaly(this Orbit o, double eE)
		{
			double e = o.eccentricity;
			if (e < 1) { //elliptical orbits
				return JUtil.ClampRadiansTwoPi(eE - (e * Math.Sin(eE)));
			} //hyperbolic orbits
			return (e * Math.Sinh(eE)) - eE;
		}

		public static Vector3d SwappedAbsolutePositionAtUT(this Orbit o, double UT)
		{
			return o.referenceBody.position + o.SwappedRelativePositionAtUT(UT);
		}

		public static Vector3d SwappedRelativePositionAtUT(this Orbit o, double UT)
		{
			return SwapYZ(o.getRelativePositionAtUT(UT));
		}
		//distance from the center of the planet
		public static double Radius(this Orbit o, double UT)
		{
			return o.SwappedRelativePositionAtUT(UT).magnitude;
		}

		public static double GetEccentricAnomalyAtTrueAnomaly(this Orbit o, double trueAnomaly)
		{
			double e = o.eccentricity;
			trueAnomaly = JUtil.ClampDegrees360(trueAnomaly);
			trueAnomaly = trueAnomaly * (Math.PI / 180);

			if (e < 1) { //elliptical orbits
				double cosE = (e + Math.Cos(trueAnomaly)) / (1 + e * Math.Cos(trueAnomaly));
				double sinE = Math.Sqrt(1 - (cosE * cosE));
				if (trueAnomaly > Math.PI)
					sinE *= -1;

				return JUtil.ClampRadiansTwoPi(Math.Atan2(sinE, cosE));
			} else {  //hyperbolic orbits
				double coshE = (e + Math.Cos(trueAnomaly)) / (1 + e * Math.Cos(trueAnomaly));
				if (coshE < 1)
					throw new ArgumentException("OrbitExtensions.GetEccentricAnomalyAtTrueAnomaly: True anomaly of " + trueAnomaly + " radians is not attained by orbit with eccentricity " + o.eccentricity);

				double E = JUtil.Acosh(coshE);
				if (trueAnomaly > Math.PI)
					E *= -1;

				return E;
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

