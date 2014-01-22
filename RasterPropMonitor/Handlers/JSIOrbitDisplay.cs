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
		public string orbitColorSelf = string.Empty;
		private Color orbitColorSelfValue = MapView.PatchColors[0];
		[KSPField]
		public string iconColorTarget = string.Empty;
		private Color iconColorTargetValue = new Color32(255, 235, 4, 153);
		[KSPField]
		public string iconColorShadow = string.Empty;
		private Color iconColorShadowValue = new Color(0f, 0f, 0f, 0.5f);
		[KSPField]
		public string iconColorAP = string.Empty;
		private Color iconColorAPValue = MapView.PatchColors[0];
		[KSPField]
		public string iconColorPE = string.Empty;
		private Color iconColorPEValue = MapView.PatchColors[0];
		[KSPField]
		public string iconColorClosestApproach = string.Empty;
		private Color iconColorClosestApproachValue = new Color(0.7f, 0.0f, 0.7f, 0.6f);
		[KSPField]
		public string orbitColorNextNode = string.Empty;
		private Color orbitColorNextNodeValue = MapView.PatchColors[1];
		[KSPField]
		public Vector4 orbitDisplayPosition = new Vector4(0f, 0f, 512f, 512f);
		[KSPField]
		public float iconPixelSize = 8f;
		[KSPField]
		public Vector2 iconShadowShift = new Vector2(1, 1);
		[KSPField]
		public int orbitPoints = 120;
		private bool startupComplete;
		private Material iconMaterial;
		private readonly Material lineMaterial = JUtil.DrawLineMaterial();
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

			var lastVertex = new Vector3(centerX + radius, centerY, 0.0f);
			for (int i = 0; i < numSegments; ++i) {
				GL.Vertex(lastVertex);
				theta += dTheta;

				float cosTheta = Mathf.Cos(theta);
				float sinTheta = Mathf.Sin(theta);
				var newVertex = new Vector3(centerX + cosTheta * radius, centerY + sinTheta * radius, 0.0f);
				GL.Vertex(newVertex);
				// Pity LINE_STRIP isn't supported.  We have to double the
				// number of vertices we shove at the GPU.
				lastVertex = newVertex;
			}
		}

		private static void DrawOrbit(Orbit o, CelestialBody referenceBody, Matrix4x4 screenTransform, int numSegments)
		{
			if (o.activePatch == false) {
				return;
			}

			// determine the time frame to render:
			double startUT = o.StartUT;
			double endUT = o.EndUT;
			endUT = Math.Min(o.EndUT, startUT + o.period);

			double dT = (endUT - startUT) / (double)numSegments;
			double t = startUT;

			Vector3 lastVertex = screenTransform.MultiplyPoint3x4(o.SwappedRelativePositionAtUT(t) + (o.referenceBody.getTruePositionAtUT(t)) - (referenceBody.getTruePositionAtUT(t)));
			for (int i = 0; i < numSegments; ++i) {
				GL.Vertex(lastVertex);

				t += dT;

				Vector3 newVertex = screenTransform.MultiplyPoint3x4(o.SwappedRelativePositionAtUT(t) + (o.referenceBody.getTruePositionAtUT(t)) - (referenceBody.getTruePositionAtUT(t)));
				GL.Vertex(newVertex);

				lastVertex = newVertex;
			}
		}

		// Fallback method: The orbit should be valid, but it's not showing as
		// active.  I've encountered this when targeting a vessel.
		private static void DrawOrbit(Orbit o, Matrix4x4 screenTransform, int numSegments)
		{
			// determine the time frame to render:
			double startUT = Planetarium.GetUniversalTime();
			double endUT = startUT + o.period;

			double dT = (endUT - startUT) / (double)numSegments;
			double t = startUT;

			Vector3 lastVertex = screenTransform.MultiplyPoint3x4(o.SwappedRelativePositionAtUT(t));
			for (int i = 0; i < numSegments; ++i) {
				GL.Vertex(lastVertex);

				t += dT;

				Vector3 newVertex = screenTransform.MultiplyPoint3x4(o.SwappedRelativePositionAtUT(t));
				GL.Vertex(newVertex);

				lastVertex = newVertex;
			}
		}

		static private bool NextApExists(Orbit o)
		{
			return (o.eccentricity < 1.0 && o.NextApoapsisTime(o.StartUT) < o.EndUT);
		}

		static private bool NextPeExists(Orbit o)
		{
			return (o.NextPeriapsisTime(o.StartUT) < o.EndUT);
		}

		// Analysis disable once UnusedParameter
		public bool RenderOrbit(RenderTexture screen, float cameraAspect)
		{
			if (!startupComplete) {
				JUtil.AnnoyUser(this);
			}

			// Make sure the parameters fit on the screen.
			Vector4 displayPosition = orbitDisplayPosition;
			displayPosition.z = Mathf.Min(screen.width - displayPosition.x, displayPosition.z);
			displayPosition.w = Mathf.Min(screen.height - displayPosition.y, displayPosition.w);

			// Here is our pixel budget in each direction:
			double horizPixelSize = displayPosition.z - iconPixelSize;
			double vertPixelSize = displayPosition.w - iconPixelSize;

			// Find a basis for transforming values into the framework of
			// vessel.orbit.  The rendering framework assumes the periapsis
			// is drawn directly to the right of the mainBody center of mass.
			// It assumes the orbit's prograde direction is "up" (screen
			// relative) at the periapsis, providing a counter-clockwise
			// motion for vessel.
			// Once we have the basic transform, we will add in scalars
			// that will ultimately transform an arbitrary point (relative to
			// the planet's center) into screen space.
			Matrix4x4 screenTransform = Matrix4x4.identity;
			double now = Planetarium.GetUniversalTime();
			double timeAtPe = vessel.orbit.NextPeriapsisTime(now);

			// Get the 3 direction vectors, based on Pe being on the right of the screen
			// OrbitExtensions provides handy utilities to get these.
			Vector3d right = vessel.orbit.Up(timeAtPe);
			Vector3d forward = vessel.orbit.SwappedOrbitNormal();
			// MOARdV: OrbitExtensions.Horizontal is unstable.  I've seen it
			// become (0, 0, 0) intermittently in flight.  Instead, use the
			// cross product of the other two.
			// We flip the sign of this vector because we are using an inverted
			// y coordinate system to keep the icons right-side up.
			Vector3d up = -Vector3d.Cross(forward, right);
			//Vector3d up = -vessel.orbit.Horizontal(timeAtPe);

			screenTransform.SetRow(0, new Vector4d(right.x, right.y, right.z, 0.0));
			screenTransform.SetRow(1, new Vector4d(up.x, up.y, up.z, 0.0));
			screenTransform.SetRow(2, new Vector4d(forward.x, forward.y, forward.z, 0.0));

			// Figure out our bounds.  First, make sure the entire planet
			// fits on the screen.
			double maxX = vessel.mainBody.Radius;
			double minX = -maxX;
			double maxY = maxX;
			double minY = -maxX;

			if (vessel.mainBody.atmosphere) {
				maxX += vessel.mainBody.maxAtmosphereAltitude;
				minX = -maxX;
				maxY = maxX;
				minY = -maxX;
			}

			// Now make sure the entire orbit fits on the screen.
			// The PeR, ApR, and semiMinorAxis are all one dimensional, so we
			// can just apply them directly to these values.
			maxX = Math.Max(maxX, vessel.orbit.PeR);
			if (vessel.orbit.eccentricity < 1.0) {
				minX = Math.Min(minX, -vessel.orbit.ApR);

				maxY = Math.Max(maxY, vessel.orbit.semiMinorAxis);
				minY = Math.Min(minY, -vessel.orbit.semiMinorAxis);
			}

			// Make sure the vessel shows up on-screen.  Since a hyperbolic
			// orbit doesn't have a meaningful ApR, we use this as a proxy for
			// how far we need to extend the bounds to show the vessel.
			Vector3 vesselPos = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(now));
			maxX = Math.Max(maxX, vesselPos.x);
			minX = Math.Min(minX, vesselPos.x);
			maxY = Math.Max(maxY, vesselPos.y);
			minY = Math.Min(minY, vesselPos.y);

			// Account for a target vessel
			var targetVessel = FlightGlobals.fetch.VesselTarget as Vessel;
			if (targetVessel != null) {

				if (targetVessel.mainBody == vessel.mainBody) {
					double tgtPe = targetVessel.orbit.NextPeriapsisTime(now);

					vesselPos = screenTransform.MultiplyPoint3x4(targetVessel.orbit.SwappedRelativePositionAtUT(tgtPe));
					maxX = Math.Max(maxX, vesselPos.x);
					minX = Math.Min(minX, vesselPos.x);
					maxY = Math.Max(maxY, vesselPos.y);
					minY = Math.Min(minY, vesselPos.y);

					if (targetVessel.orbit.eccentricity < 1.0) {
						vesselPos = screenTransform.MultiplyPoint3x4(targetVessel.orbit.SwappedRelativePositionAtUT(targetVessel.orbit.NextApoapsisTime(now)));
						maxX = Math.Max(maxX, vesselPos.x);
						minX = Math.Min(minX, vesselPos.x);
						maxY = Math.Max(maxY, vesselPos.y);
						minY = Math.Min(minY, vesselPos.y);
					}

					vesselPos = screenTransform.MultiplyPoint3x4(targetVessel.orbit.SwappedRelativePositionAtUT(now));
					maxX = Math.Max(maxX, vesselPos.x);
					minX = Math.Min(minX, vesselPos.x);
					maxY = Math.Max(maxY, vesselPos.y);
					minY = Math.Min(minY, vesselPos.y);
				} else {
					// We only care about tgtVessel if it is in the same SoI.
					targetVessel = null;
				}
			}

			ManeuverNode node = (vessel.patchedConicSolver.maneuverNodes.Count > 0) ? vessel.patchedConicSolver.maneuverNodes[0] : null;
			if (node != null) {
				double nodePe = node.nextPatch.NextPeriapsisTime(now);
				vesselPos = screenTransform.MultiplyPoint3x4(node.nextPatch.SwappedRelativePositionAtUT(nodePe));
				maxX = Math.Max(maxX, vesselPos.x);
				minX = Math.Min(minX, vesselPos.x);
				maxY = Math.Max(maxY, vesselPos.y);
				minY = Math.Min(minY, vesselPos.y);

				if (node.nextPatch.eccentricity < 1.0) {
					double nodeAp = node.nextPatch.NextApoapsisTime(now);
					vesselPos = screenTransform.MultiplyPoint3x4(node.nextPatch.SwappedRelativePositionAtUT(nodeAp));
					maxX = Math.Max(maxX, vesselPos.x);
					minX = Math.Min(minX, vesselPos.x);
					maxY = Math.Max(maxY, vesselPos.y);
					minY = Math.Min(minY, vesselPos.y);
				}
			}

			// Add translation.  This will ensure that all of the features
			// under consideration above will be displayed.
			screenTransform[0, 3] = -0.5f * (float)(maxX + minX);
			screenTransform[1, 3] = -0.5f * (float)(maxY + minY);

			double neededWidth = maxX - minX;
			double neededHeight = maxY - minY;

			// Pick a scalar that will fit the bounding box we just created.
			float pixelScalar = (float)Math.Min(horizPixelSize / neededWidth, vertPixelSize / neededHeight);
			screenTransform = Matrix4x4.Scale(new Vector3(pixelScalar, pixelScalar, pixelScalar)) * screenTransform;

			GL.Clear(true, true, backgroundColorValue);
			GL.PushMatrix();
			GL.LoadPixelMatrix(-displayPosition.z * 0.5f, displayPosition.z * 0.5f, displayPosition.w * 0.5f, -displayPosition.w * 0.5f);
			GL.Viewport(new Rect(displayPosition.x, screen.height - displayPosition.y - displayPosition.w, displayPosition.z, displayPosition.w));

			lineMaterial.SetPass(0);
			GL.Begin(GL.LINES);

			// Draw the planet:
			Vector3 focusCenter = screenTransform.MultiplyPoint3x4(new Vector3(0.0f, 0.0f, 0.0f));

			// orbitDriver is null on the sun, so we'll just use white instead.
			GL.Color((vessel.mainBody.orbitDriver == null) ? new Color(1.0f, 1.0f, 1.0f) : vessel.mainBody.orbitDriver.orbitColor);
			DrawCircle(focusCenter.x, focusCenter.y, (float)(vessel.mainBody.Radius * pixelScalar), orbitPoints);
			if (vessel.mainBody.atmosphere) {
				// Use the atmospheric ambient to color the atmosphere circle.
				GL.Color(vessel.mainBody.atmosphericAmbientColor);

				DrawCircle(focusCenter.x, focusCenter.y, (float)((vessel.mainBody.Radius + vessel.mainBody.maxAtmosphereAltitude) * pixelScalar), orbitPoints);
			}

			if (targetVessel != null) {
				GL.Color(iconColorTargetValue);
				if (!targetVessel.orbit.activePatch && targetVessel.orbit.eccentricity < 1.0 && targetVessel.orbit.referenceBody == vessel.orbit.referenceBody) {
					// For some reason, activePatch is false for targetVessel.
					// If we have a stable orbit for the target, use a fallback
					// rendering method:
					DrawOrbit(targetVessel.orbit, screenTransform, orbitPoints);
				} else {
					DrawOrbit(targetVessel.orbit, vessel.orbit.referenceBody, screenTransform, orbitPoints);
				}
			}

			if (node != null) {
				GL.Color(orbitColorNextNodeValue);
				DrawOrbit(node.nextPatch, vessel.orbit.referenceBody, screenTransform, orbitPoints);
			}

			if (vessel.orbit.nextPatch != null && vessel.orbit.nextPatch.activePatch) {
				GL.Color(orbitColorNextNodeValue);
				DrawOrbit(vessel.orbit.nextPatch, vessel.orbit.referenceBody, screenTransform, orbitPoints);
			}

			// Draw the vessel orbit
			GL.Color(orbitColorSelfValue);
			DrawOrbit(vessel.orbit, vessel.orbit.referenceBody, screenTransform, orbitPoints);

			// Done drawing lines.
			GL.End();

			// Draw target vessel icons.
			Vector3 transformedPosition;
			if (targetVessel != null) {

				if (NextPeExists(targetVessel.orbit)) {
					transformedPosition = screenTransform.MultiplyPoint3x4(targetVessel.orbit.SwappedRelativePositionAtUT(targetVessel.orbit.NextPeriapsisTime(targetVessel.orbit.StartUT)));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorTargetValue, MapIcons.OtherIcon.PE);
				}

				if (NextApExists(targetVessel.orbit)) {
					transformedPosition = screenTransform.MultiplyPoint3x4(targetVessel.orbit.SwappedRelativePositionAtUT(targetVessel.orbit.NextApoapsisTime(now)));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorTargetValue, MapIcons.OtherIcon.AP);
				}

				transformedPosition = screenTransform.MultiplyPoint3x4(targetVessel.orbit.SwappedRelativePositionAtUT(now));
				DrawIcon(transformedPosition.x, transformedPosition.y, targetVessel.vesselType, iconColorTargetValue);

				if (vessel.orbit.AscendingNodeExists(targetVessel.orbit)) {
					double anTime = vessel.orbit.TimeOfAscendingNode(targetVessel.orbit, now);
					transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(anTime));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.AN);
				}
				if (vessel.orbit.DescendingNodeExists(targetVessel.orbit)) {
					double anTime = vessel.orbit.TimeOfDescendingNode(targetVessel.orbit, now);
					transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(anTime));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.DN);
				}

				double tClosestApproach;
				double dClosestApproach = JUtil.GetClosestApproach(vessel.orbit, targetVessel.orbit, out tClosestApproach);
				if (tClosestApproach < vessel.orbit.EndUT) {
					transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(tClosestApproach));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorClosestApproachValue, MapIcons.OtherIcon.SHIPATINTERCEPT);

					transformedPosition = screenTransform.MultiplyPoint3x4(targetVessel.orbit.SwappedRelativePositionAtUT(tClosestApproach));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorClosestApproachValue, MapIcons.OtherIcon.TGTATINTERCEPT);
				}
			} else {
				if (vessel.orbit.AscendingNodeEquatorialExists()) {
					double anTime = vessel.orbit.TimeOfAscendingNodeEquatorial(now);
					transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(anTime));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.AN);
				}
				if (vessel.orbit.DescendingNodeEquatorialExists()) {
					double anTime = vessel.orbit.TimeOfDescendingNodeEquatorial(now);
					transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(anTime));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.DN);
				}
			}

			// Draw orbital features
			if (NextPeExists(vessel.orbit)) {
				transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(timeAtPe));
				DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorPEValue, MapIcons.OtherIcon.PE);
			}

			if (NextApExists(vessel.orbit)) {
				transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(vessel.orbit.NextApoapsisTime(vessel.orbit.StartUT)));
				DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorAPValue, MapIcons.OtherIcon.AP);
			}

			if (vessel.orbit.nextPatch != null) {
				transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(vessel.orbit.EndUT));
				DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.EXITSOI);
			}

			if (node != null) {
				double nodePe = node.nextPatch.NextPeriapsisTime(node.nextPatch.StartUT);

				if (NextPeExists(node.nextPatch)) {
					transformedPosition = screenTransform.MultiplyPoint3x4(node.nextPatch.SwappedRelativePositionAtUT(nodePe));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorNextNodeValue, MapIcons.OtherIcon.PE);
				}

				if (NextApExists(node.nextPatch)) {
					transformedPosition = screenTransform.MultiplyPoint3x4(node.nextPatch.SwappedRelativePositionAtUT(node.nextPatch.NextApoapsisTime(node.nextPatch.StartUT)));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorNextNodeValue, MapIcons.OtherIcon.AP);
				}

				transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(node.UT));
				DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorNextNodeValue, MapIcons.OtherIcon.NODE);
			}

			// Draw ownship icon
			transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(now));
			DrawIcon(transformedPosition.x, transformedPosition.y, vessel.vesselType, iconColorSelfValue);

			GL.PopMatrix();
			GL.Viewport(new Rect(0, 0, screen.width, screen.height));

			return true;
		}

		private void DrawIcon(float xPos, float yPos, VesselType vt, Color iconColor, MapIcons.OtherIcon icon = MapIcons.OtherIcon.None)
		{
			var position = new Rect(xPos - iconPixelSize * 0.5f, yPos - iconPixelSize * 0.5f,
				               iconPixelSize, iconPixelSize);

			Rect shadow = position;
			shadow.x += iconShadowShift.x;
			shadow.y += iconShadowShift.y;

			iconMaterial.color = iconColorShadowValue;
			Graphics.DrawTexture(shadow, MapView.OrbitIconsMap, MapIcons.VesselTypeIcon(vt, icon), 0, 0, 0, 0, iconMaterial);

			iconMaterial.color = iconColor;
			Graphics.DrawTexture(position, MapView.OrbitIconsMap, MapIcons.VesselTypeIcon(vt, icon), 0, 0, 0, 0, iconMaterial);
		}

		public void Start()
		{
			if (!string.IsNullOrEmpty(backgroundColor)) {
				backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);
			}
			if (!string.IsNullOrEmpty(iconColorSelf)) {
				iconColorSelfValue = ConfigNode.ParseColor32(iconColorSelf);
			}
			if (!string.IsNullOrEmpty(orbitColorSelf)) {
				orbitColorSelfValue = ConfigNode.ParseColor32(orbitColorSelf);
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
			if (!string.IsNullOrEmpty(orbitColorNextNode)) {
				orbitColorNextNodeValue = ConfigNode.ParseColor32(orbitColorNextNode);
			}
			if (!string.IsNullOrEmpty(iconColorClosestApproach)) {
				iconColorClosestApproachValue = ConfigNode.ParseColor32(iconColorClosestApproach);
			}

			// This mess with shaders has to stop. Maybe we should have a single shader to draw EVERYTHING on the screen...
			iconMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
			iconMaterial.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);

			startupComplete = true;
		}
	}
}
