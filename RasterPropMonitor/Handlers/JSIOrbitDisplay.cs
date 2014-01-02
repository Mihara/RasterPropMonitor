// JSIOrbitDisplay: Display a schematic (line-art) drawing of the vessel's
// orbit, marking highlights (Pe, Ap, AN, DN), along with the mainbody's
// surface and atmosphere (if applicable).
using System;
using UnityEngine;

namespace JSI
{
	public class JSIOrbitDisplay:InternalModule
	{
		[KSPField]
		public string backgroundColor = string.Empty;
		private Color backgroundColorValue = Color.black;
		[KSPField]
		public string iconColorSelf = string.Empty;
		private Color iconColorSelfValue = new Color(1f, 1f, 1f, 0.6f);
		[KSPField]
		public string iconColorTarget = string.Empty;
		private Color iconColorTargetValue = new Color32(255, 235, 4, 153);
		[KSPField]
		public string iconColorShadow = string.Empty;
		private Color iconColorShadowValue = new Color(0f, 0f, 0f, 0.5f);
		[KSPField]
		public string iconColorAP = string.Empty;
		private Color iconColorAPValue = new Color(0f, 1f, 0f, 0.5f);
		[KSPField]
		public string iconColorPE = string.Empty;
		private Color iconColorPEValue = new Color(1f, 0f, 0f, 0.5f);

		[KSPField]
		public Vector4 orbitDisplayPosition = new Vector4(0f, 0f, 512f, 512f);
		[KSPField]
		public float iconPixelSize = 8f;
		[KSPField]
		public Vector2 iconShadowShift = new Vector2(1, 1);

		[KSPField]
		public int orbitPoints = 120;

		private bool startupComplete = false;
		private Material iconMaterial;
		private Material lineMaterial = JUtil.DrawLineMaterial();

		// MOARdV: Move this to JUtil?
		private enum OtherIcon
		{
			None,
			PE,
			AP,
			AN,
			DN,
			NODE,
		}

		// All units in pixels.  Assumes GL.Begin(LINES) and GL.Color() have
		// already been called for this circle.
		private static void DrawCircle(float centerX, float centerY, float radius, int maxOrbitPoints)
		{
			// Figure out the tessellation level to use, based on circle size
			// and user limits.
			float circumferenceInPixels = 2.0f * Mathf.PI * radius;
			// Our ideal is a tessellation that gives us 2 pixels per segment,
			// which should look like a smooth circle.
			int idealOrbitPoints = Math.Max(1, (int)(circumferenceInPixels / 2.0f));
			int numSegments = Math.Min(maxOrbitPoints, idealOrbitPoints);
			float dTheta = (float)(2.0 * Math.PI / (double)(numSegments));
			float theta = 0.0f;

			Vector3 lastVertex = new Vector3(centerX + radius, 0.0f, 0.0f);
			for (int i = 0; i < numSegments; ++i) {
				GL.Vertex(lastVertex);
				theta += dTheta;

				float cosTheta = Mathf.Cos(theta);
				float sinTheta = Mathf.Sin(theta);
				Vector3 newVertex = new Vector3(centerX + cosTheta * radius, centerY + sinTheta * radius, 0.0f);
				GL.Vertex(newVertex);
				// Pity LINE_STRIP isn't supported.  We have to double the
				// number of vertices we shove at the GPU.
				lastVertex = newVertex;
			}
			/* // Draw a bounding box for debugging:
			GL.Vertex3(centerX - radius, centerY - radius, 0.0f);
			GL.Vertex3(centerX + radius, centerY - radius, 0.0f);
			GL.Vertex3(centerX + radius, centerY - radius, 0.0f);
			GL.Vertex3(centerX + radius, centerY + radius, 0.0f);
			GL.Vertex3(centerX + radius, centerY + radius, 0.0f);
			GL.Vertex3(centerX - radius, centerY + radius, 0.0f);
			GL.Vertex3(centerX - radius, centerY + radius, 0.0f);
			GL.Vertex3(centerX - radius, centerY - radius, 0.0f);
			*/
		}

		// This draws the primary orbit ellipse, which is always centered
		// at the origin (for computational simplicity)
		private static void DrawPrimaryOrbitEllipse(float semiMajorAxis, float semiMinorAxis, int maxOrbitPoints)
		{
			// TODO: figure out the circumference of an ellipse so I can
			// make an appropriate tessellation level.
			// Circumference based on Ramanujan's approximation, per
			// Wikipedia.
			float circumferenceInPixels = Mathf.PI * (3.0f*(semiMinorAxis+semiMajorAxis) - Mathf.Sqrt(10.0f * semiMajorAxis * semiMinorAxis + 3.0f* (semiMinorAxis*semiMinorAxis + semiMajorAxis*semiMajorAxis)));
			int idealOrbitPoints = Math.Max(1, (int)(circumferenceInPixels / 2.0f));
			int numSegments = Math.Min(maxOrbitPoints, idealOrbitPoints);

			float dTheta = (float)(2.0 * Math.PI / (double)(numSegments));
			float theta = 0.0f;

			Vector3 lastVertex = new Vector3(semiMajorAxis, 0.0f, 0.0f);
			for (int i = 0; i < numSegments; ++i)
			{
				GL.Vertex(lastVertex);
				theta += dTheta;

				float cosTheta = Mathf.Cos(theta);
				float sinTheta = Mathf.Sin(theta);
				Vector3 newVertex = new Vector3(cosTheta * semiMajorAxis, sinTheta * semiMinorAxis, 0.0f);
				GL.Vertex(newVertex);
				// Pity LINE_STRIP isn't supported.  We have to double the
				// number of vertices we shove at the GPU.
				lastVertex = newVertex;
			}

			/* // Draw a bounding box for debugging:
			GL.Vertex3(-semiMajorAxis, -semiMinorAxis, 0.0f);
			GL.Vertex3(semiMajorAxis, -semiMinorAxis, 0.0f);
			GL.Vertex3(semiMajorAxis, -semiMinorAxis, 0.0f);
			GL.Vertex3(semiMajorAxis, semiMinorAxis, 0.0f);
			GL.Vertex3(semiMajorAxis, semiMinorAxis, 0.0f);
			GL.Vertex3(-semiMajorAxis, semiMinorAxis, 0.0f);
			GL.Vertex3(-semiMajorAxis, semiMinorAxis, 0.0f);
			GL.Vertex3(-semiMajorAxis, -semiMinorAxis, 0.0f);
			*/
		}

		public bool RenderOrbit(RenderTexture screen, float cameraAspect)
		{
			if (!startupComplete) {
				JUtil.AnnoyUser(this);
			}

			GL.Clear(true, true, backgroundColorValue);
			GL.PushMatrix();
			GL.LoadPixelMatrix(-orbitDisplayPosition.z * 0.5f, orbitDisplayPosition.z * 0.5f, -orbitDisplayPosition.w * 0.5f, orbitDisplayPosition.w * 0.5f);
			GL.Viewport(new Rect(orbitDisplayPosition.x, screen.height - orbitDisplayPosition.y - orbitDisplayPosition.w, orbitDisplayPosition.z, orbitDisplayPosition.w));
			//GL.Viewport(new Rect(orbitDisplayPosition.x, orbitDisplayPosition.y, orbitDisplayPosition.z, orbitDisplayPosition.w));

			double pixelScalar;
			double focus;
			if (vessel.orbit.eccentricity < 1.0) {
				// Convert orbital parameters to a format that's handy for drawing an ellipse:

				// Distance from the primary focus (mainBody's CoM) to the periapsis point.
				double distanceFA = vessel.orbit.PeR;
				// Distance from the primary focus to the apoapsis point.
				double distanceFA1 = vessel.orbit.ApR;
				// Distance from the primary focus to the center of the ellipse
				focus = (distanceFA1 - distanceFA) * 0.5;
				double semiMajorAxis = vessel.orbit.semiMajorAxis;
				double semiMinorAxis = vessel.orbit.semiMinorAxis;

				// Figure out our scaling (pixels/meter)
				double horizPixelSize = (orbitDisplayPosition.z - iconPixelSize) / (2.0 * semiMajorAxis);
				double vertPixelSize = (orbitDisplayPosition.w - iconPixelSize) / (2.0 * semiMinorAxis);
				pixelScalar = Math.Min(horizPixelSize, vertPixelSize);

				lineMaterial.SetPass(0);
				GL.Begin(GL.LINES);

				// Draw the planet
				GL.Color(vessel.mainBody.orbitDriver.Renderer.orbitColor);
				DrawCircle((float)(-focus * pixelScalar), 0.0f, (float)(vessel.mainBody.Radius * pixelScalar), orbitPoints);

				// Draw the atmosphere
				if (vessel.mainBody.atmosphere) {
					GL.Color(vessel.mainBody.atmosphericAmbientColor);
					DrawCircle((float)(-focus * pixelScalar), 0.0f, (float)((vessel.mainBody.Radius + vessel.mainBody.maxAtmosphereAltitude) * pixelScalar), orbitPoints);
				}

				// Draw the orbit
				GL.Color(iconColorSelfValue);
				DrawPrimaryOrbitEllipse((float)(semiMajorAxis * pixelScalar), (float)(semiMinorAxis * pixelScalar), orbitPoints);

				GL.End();

				// Draw the orbital features:
				DrawIcon((float)(-semiMajorAxis * pixelScalar), 0.0f, VesselType.Unknown, iconColorPEValue, OtherIcon.PE);
				DrawIcon((float)(semiMajorAxis * pixelScalar), 0.0f, VesselType.Unknown, iconColorAPValue, OtherIcon.AP);

				// Where are we?
				// MOARdV: Theta seems to be 180 degrees from where I expect
				// it, so I am adding a half circle here.
				double cosTheta = Math.Cos(vessel.orbit.trueAnomaly * (Math.PI / 180.0) + Math.PI);
				double sinTheta = Math.Sin(vessel.orbit.trueAnomaly * (Math.PI / 180.0) + Math.PI);
				double distFromFocus = vessel.orbit.semiLatusRectum / (1.0 - vessel.orbit.eccentricity * cosTheta);

				DrawIcon((float)((-focus + cosTheta * distFromFocus) * pixelScalar), (float)(sinTheta * distFromFocus * pixelScalar), vessel.vesselType, iconColorSelfValue);
			} else {
				// MOARdV TODO: ecc >= 1.0 (parabola / hyperbola)
				pixelScalar = 0.0;
				focus = 0.0;
			}

			GL.PopMatrix();
			GL.Viewport(new Rect(0, 0, screen.width, screen.height));

			return true;
		}

		private void DrawIcon(float xPos, float yPos, VesselType vt, Color iconColor, OtherIcon icon = OtherIcon.None)
		{
			// MOARdV TODO: These icons are all upside down, since I am using
			// an inverted matrix.
			var position = new Rect(xPos - iconPixelSize * 0.5f, yPos - iconPixelSize * 0.5f,
							   iconPixelSize, iconPixelSize);

			Rect shadow = position;
			shadow.x += iconShadowShift.x;
			shadow.y += iconShadowShift.y;

			iconMaterial.color = iconColorShadowValue;
			Graphics.DrawTexture(shadow, MapView.OrbitIconsMap, VesselTypeIcon(vt, icon), 0, 0, 0, 0, iconMaterial);

			iconMaterial.color = iconColor;
			Graphics.DrawTexture(position, MapView.OrbitIconsMap, VesselTypeIcon(vt, icon), 0, 0, 0, 0, iconMaterial);
		}

		public void Start()
		{
			if (!string.IsNullOrEmpty(backgroundColor)) {
				backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);
			}
			if (!string.IsNullOrEmpty(iconColorSelf)) {
				iconColorSelfValue = ConfigNode.ParseColor32(iconColorSelf);
			}
			if (!string.IsNullOrEmpty(iconColorTarget)) {
				iconColorTargetValue = ConfigNode.ParseColor32(iconColorTarget);
			}
			if (!string.IsNullOrEmpty(iconColorShadow)) {
				iconColorShadowValue = ConfigNode.ParseColor32(iconColorShadow);
			}
			if (!string.IsNullOrEmpty(iconColorAP)) {
				iconColorAPValue = ConfigNode.ParseColor32(iconColorAP);
			}
			if (!string.IsNullOrEmpty(iconColorPE)) {
				iconColorPEValue = ConfigNode.ParseColor32(iconColorPE);
			}

			iconMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
			iconMaterial.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);

			startupComplete = true;
		}

		private static Rect VesselTypeIcon(VesselType type, OtherIcon icon)
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
}
