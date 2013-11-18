using System;
using System.Text;
using UnityEngine;

namespace JSI
{
	static class JUtil
	{
		/*
		public static RasterPropMonitorComputer GetComputer(InternalProp thatProp)
		{
			// I hate copypaste, and this is what I'm going to do about it.
			if (thatProp.part != null) {
				foreach (InternalProp prop in thatProp.part.internalModel.props) {
					RasterPropMonitorComputer other = prop.FindModelComponent<RasterPropMonitorComputer>();
					if (other != null) {
						return other;
					}
				}
			}
			thatProp.AddModule(typeof(RasterPropMonitorComputer).Name);
			return thatProp.FindModelComponent<RasterPropMonitorComputer>();
		}
		*/
		public static RasterPropMonitorComputer GetComputer(InternalProp thatProp)
		{
			// I hate copypaste, and this is what I'm going to do about it.
			if (thatProp.part != null) {
				for (int i = 0; i < thatProp.part.Modules.Count; i++)
					if (thatProp.part.Modules[i].ClassName == typeof(RasterPropMonitorComputer).Name) {
						RasterPropMonitorComputer other = thatProp.part.Modules[i] as RasterPropMonitorComputer;
						return other;
					}
				return thatProp.part.AddModule(typeof(RasterPropMonitorComputer).Name) as RasterPropMonitorComputer;
			}
			return null;
		}

		public static string WordWrap(string text, int maxLineLength)
		{
			StringBuilder sb = new StringBuilder();
			int currentIndex;
			int lastWrap;
			char[] prc = { ' ', ',', '.', '?', '!', ':', ';', '-' };
			char[] ws = { ' ' };

			foreach (string line in text.Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)) {
				currentIndex = 0;
				lastWrap = 0;
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

		public static Vector3d SwapYZ(Vector3d v)
		{
			return v.xzy;
		}
		public static Vector3d SwappedOrbitNormal(Orbit o)
		{
			return -SwapYZ(o.GetOrbitNormal()).normalized;
		}

		public static double NormalAngle(Vector3 a, Vector3 b, Vector3 up)
		{
			return SignedAngle(Vector3.Cross(up, a), Vector3.Cross(up, b), up);
		}

		public static float SignedAngle(Vector3 v1, Vector3 v2, Vector3 up)
		{
			if (Vector3.Dot(Vector3.Cross(v1, v2), up) < 0)
				return -Vector3.Angle(v1, v2);
			return Vector3.Angle(v1, v2);
		}
	}
}

