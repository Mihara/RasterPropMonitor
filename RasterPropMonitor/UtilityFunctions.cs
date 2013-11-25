using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

namespace JSI
{
	public static class JUtil
	{
		public static RasterPropMonitorComputer GetComputer(InternalProp thatProp)
		{
			// I hate copypaste, and this is what I'm going to do about it.
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

		public static FXGroup SetupIVASound(InternalProp thatProp, string buttonClickSound, float buttonClickVolume, bool loopState)
		{
			FXGroup audioOutput = null;
			if (!string.IsNullOrEmpty(buttonClickSound)) {
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
			if (Vector3.Dot(Vector3.Cross(v1, v2), up) < 0)
				return -Vector3.Angle(v1, v2);
			return Vector3.Angle(v1, v2);
		}
	}

	public static class JStringExtensions
	{
		public static string EnforceSlashes(this string input)
		{
			return input.Replace('\\', '/');
		}
		public static string UnMangleConfigText(this string input) {
			return input.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
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
		Dictionary<TKey, TValue> d = new Dictionary<TKey, TValue>();
		TValue defaultValue;

		public DefaultableDictionary(TValue defaultValue)
		{
			this.defaultValue = defaultValue;
		}

		public TValue this [TKey key] {
			get {
				if (d.ContainsKey(key))
					return d[key];
				return defaultValue;
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

