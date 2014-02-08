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

			double startTA;
			double endTA;
			double now = Planetarium.GetUniversalTime();
			if (o.patchEndTransition != Orbit.PatchTransitionType.FINAL) {
				startTA = o.TrueAnomalyAtUT(o.StartUT);
				endTA = o.TrueAnomalyAtUT(o.EndUT);
				if (endTA < startTA) {
					endTA += 2.0*Math.PI;
				}
			} else {
				startTA = o.GetUTforTrueAnomaly(0.0, now);
				endTA = startTA + 2.0*Math.PI;
			}
			double dTheta = (endTA - startTA) / (double)numSegments;
			double theta = startTA;
			double timeAtTA = o.GetUTforTrueAnomaly(theta, now);
			Vector3 lastVertex = screenTransform.MultiplyPoint3x4(o.getRelativePositionFromTrueAnomaly(theta).xzy + (o.referenceBody.getTruePositionAtUT(timeAtTA)) - (referenceBody.getTruePositionAtUT(timeAtTA)));
			for (int i = 0; i < numSegments; ++i) {
				GL.Vertex3(lastVertex.x, lastVertex.y, 0.0f);
				theta += dTheta;
				timeAtTA = o.GetUTforTrueAnomaly(theta, now);

				Vector3 newVertex = screenTransform.MultiplyPoint3x4(o.getRelativePositionFromTrueAnomaly(theta).xzy + (o.referenceBody.getTruePositionAtUT(timeAtTA)) - (referenceBody.getTruePositionAtUT(timeAtTA)));
				GL.Vertex3(newVertex.x, newVertex.y, 0.0f);

				lastVertex = newVertex;
			}
		}

		// Fallback method: The orbit should be valid, but it's not showing as
		// active.  I've encountered this when targeting a vessel or planet.
		private static void ReallyDrawOrbit(Orbit o, CelestialBody referenceBody, Matrix4x4 screenTransform, int numSegments)
		{
			if (o.eccentricity >= 1.0) {
				Debug.Log("JSIOrbitDisplay.ReallyDrawOrbit(): I can't draw an orbit with e >= 1.0");
				return;
			}

			double dTheta = 2.0 * Math.PI / (double)numSegments;
			double theta = 0.0;
			double now = Planetarium.GetUniversalTime();
			double timeAtTA = o.GetUTforTrueAnomaly(theta, now);
			Vector3 lastVertex = screenTransform.MultiplyPoint3x4(o.getRelativePositionFromTrueAnomaly(theta).xzy + (o.referenceBody.getTruePositionAtUT(timeAtTA)) - (referenceBody.getTruePositionAtUT(timeAtTA)));
			for (int i = 0; i < numSegments; ++i) {
				GL.Vertex3(lastVertex.x, lastVertex.y, 0.0f);
				theta += dTheta;
				timeAtTA = o.GetUTforTrueAnomaly(theta, now);

				Vector3 newVertex = screenTransform.MultiplyPoint3x4(o.getRelativePositionFromTrueAnomaly(theta).xzy + (o.referenceBody.getTruePositionAtUT(timeAtTA)) - (referenceBody.getTruePositionAtUT(timeAtTA)));
				GL.Vertex3(newVertex.x, newVertex.y, 0.0f);

				lastVertex = newVertex;
			}
		}

		private void DrawNextAp(Orbit o, CelestialBody referenceBody, double referenceTime, Color iconColor, Matrix4x4 screenTransform)
		{
			if (o.eccentricity >= 1.0) {
				// Early return: There is no apoapsis on a hyperbolic orbit
				return;
			}
			double nextApTime = o.NextApoapsisTime(referenceTime);

			if (nextApTime < o.EndUT || (o.patchEndTransition == Orbit.PatchTransitionType.FINAL)) {
				Vector3d relativePosition = o.SwappedRelativePositionAtUT(nextApTime) + o.referenceBody.getTruePositionAtUT(nextApTime) - referenceBody.getTruePositionAtUT(nextApTime);
				var transformedPosition = screenTransform.MultiplyPoint3x4(relativePosition);
				DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColor, MapIcons.OtherIcon.AP);
			}
		}

		private void DrawNextPe(Orbit o, CelestialBody referenceBody, double referenceTime, Color iconColor, Matrix4x4 screenTransform)
		{
			/*
			switch (o.patchEndTransition)
			{
				case Orbit.PatchTransitionType.ENCOUNTER:
					Debug.Log("ENCOUNTER patch end type");
					break;
				case Orbit.PatchTransitionType.ESCAPE:
					Debug.Log("ESCAPE patch end type");
					break;
				// FINAL is applied to the active vessel in a stable elliptical
				// orbit.
				case Orbit.PatchTransitionType.FINAL:
					Debug.Log("FINAL patch end type");
					break;
				// INITIAL patchEndTransition appears to be applied to inactive
				// vessels (targeted vessels).
				case Orbit.PatchTransitionType.INITIAL:
					Debug.Log("INITIAL patch end type");
					break;
				case Orbit.PatchTransitionType.MANEUVER:
					Debug.Log("MANEUVER patch end type");
					break;
			}
			 */

			double nextPeTime = o.NextPeriapsisTime(referenceTime);
			if (nextPeTime < o.EndUT || (o.patchEndTransition == Orbit.PatchTransitionType.FINAL)) {
				Vector3d relativePosition = o.SwappedRelativePositionAtUT(nextPeTime) + o.referenceBody.getTruePositionAtUT(nextPeTime) - referenceBody.getTruePositionAtUT(nextPeTime);
				var transformedPosition = screenTransform.MultiplyPoint3x4(relativePosition);
				DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColor, MapIcons.OtherIcon.PE);
			}
		}

		private static Orbit GetPatchAtUT(Orbit startOrbit, double UT)
		{
			Orbit o = startOrbit;
			while (o.patchEndTransition != Orbit.PatchTransitionType.FINAL) {
				if (o.EndUT >= UT) {
					// EndUT for this patch is later than what we're looking for.  Exit.
					break;
				}
				if (o.nextPatch == null || o.nextPatch.activePatch == false) {
					// There is no valid next patch.  Exit.
					break;
				}

				// Check the next one.
				o = o.nextPatch;
			}
			return o;
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
			// fits on the screen.  We define the center of the vessel.mainBody
			// as the origin of our coodinate system.
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
			Vector3 vesselPos;
			// The PeR, ApR, and semiMinorAxis are all one dimensional, so we
			// can just apply them directly to these values.
			maxX = Math.Max(maxX, vessel.orbit.PeR);
			if (vessel.orbit.eccentricity < 1.0) {
				minX = Math.Min(minX, -vessel.orbit.ApR);

				maxY = Math.Max(maxY, vessel.orbit.semiMinorAxis);
				minY = Math.Min(minY, -vessel.orbit.semiMinorAxis);
			} else if(vessel.orbit.EndUT > 0.0) {
				// If we're hyperbolic, let's get the SoI transition
				vesselPos = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(vessel.orbit.EndUT));
				maxX = Math.Max(maxX, vesselPos.x);
				minX = Math.Min(minX, vesselPos.x);
				maxY = Math.Max(maxY, vesselPos.y);
				minY = Math.Min(minY, vesselPos.y);
			}

			// Make sure the vessel shows up on-screen.  Since a hyperbolic
			// orbit doesn't have a meaningful ApR, we use this as a proxy for
			// how far we need to extend the bounds to show the vessel.
			vesselPos = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(now));
			maxX = Math.Max(maxX, vesselPos.x);
			minX = Math.Min(minX, vesselPos.x);
			maxY = Math.Max(maxY, vesselPos.y);
			minY = Math.Min(minY, vesselPos.y);

			// Account for a target vessel
			var targetBody = FlightGlobals.fetch.VesselTarget as CelestialBody;
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

			if (targetBody != null) {
				// Validate some values up front, so we don't need to test them later.
				if (targetBody.GetOrbit() == null) {
					targetBody = null;
				} else if(targetBody.orbit.referenceBody == vessel.orbit.referenceBody) {
					// If the target body orbits our current world, let's at
					// least make sure the body's location is visible.
					vesselPos = screenTransform.MultiplyPoint3x4(targetBody.GetOrbit().SwappedRelativePositionAtUT(now));
					maxX = Math.Max(maxX, vesselPos.x);
					minX = Math.Min(minX, vesselPos.x);
					maxY = Math.Max(maxY, vesselPos.y);
					minY = Math.Min(minY, vesselPos.y);
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
				} else if(node.nextPatch.EndUT > 0.0) {
					// If the next patch is hyperbolic, include the endpoint.
					vesselPos = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(node.nextPatch.EndUT));
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
					ReallyDrawOrbit(targetVessel.orbit, vessel.orbit.referenceBody, screenTransform, orbitPoints);
				} else {
					DrawOrbit(targetVessel.orbit, vessel.orbit.referenceBody, screenTransform, orbitPoints);
				}
			}

			foreach (CelestialBody moon in vessel.orbit.referenceBody.orbitingBodies) {
				if (moon != targetBody) {
					GL.Color(moon.orbitDriver.orbitColor);
					ReallyDrawOrbit(moon.GetOrbit(), vessel.orbit.referenceBody, screenTransform, orbitPoints);
				}
			}

			if (targetBody != null) {
				GL.Color(iconColorTargetValue);
				ReallyDrawOrbit(targetBody.GetOrbit(), vessel.orbit.referenceBody, screenTransform, orbitPoints);
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
			foreach (CelestialBody moon in vessel.orbit.referenceBody.orbitingBodies) {
				if (moon != targetBody) {
					transformedPosition = screenTransform.MultiplyPoint3x4(moon.getTruePositionAtUT(now) - vessel.orbit.referenceBody.getTruePositionAtUT(now));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, moon.orbitDriver.orbitColor, MapIcons.OtherIcon.PLANET);
				}
			}

			if (targetVessel != null || targetBody != null) {
				var orbit = (targetVessel != null) ? targetVessel.GetOrbit() : targetBody.GetOrbit();
				DrawNextPe(orbit, vessel.orbit.referenceBody, now, iconColorTargetValue, screenTransform);

				DrawNextAp(orbit, vessel.orbit.referenceBody, now, iconColorTargetValue, screenTransform);

				if (targetBody != null) {
					transformedPosition = screenTransform.MultiplyPoint3x4(targetBody.getTruePositionAtUT(now) - vessel.orbit.referenceBody.getTruePositionAtUT(now));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorTargetValue, MapIcons.OtherIcon.PLANET);
				} else {
					transformedPosition = screenTransform.MultiplyPoint3x4(orbit.SwappedRelativePositionAtUT(now));
					DrawIcon(transformedPosition.x, transformedPosition.y, targetVessel.vesselType, iconColorTargetValue);
				}

				if (vessel.orbit.AscendingNodeExists(orbit)) {
					double anTime = vessel.orbit.TimeOfAscendingNode(orbit, now);
					if (anTime < vessel.orbit.EndUT || (vessel.orbit.patchEndTransition != Orbit.PatchTransitionType.ESCAPE && vessel.orbit.patchEndTransition != Orbit.PatchTransitionType.ENCOUNTER)) {
						transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(anTime));
						DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.AN);
					}
				}
				if (vessel.orbit.DescendingNodeExists(orbit)) {
					double dnTime = vessel.orbit.TimeOfDescendingNode(orbit, now);
					if (dnTime < vessel.orbit.EndUT || (vessel.orbit.patchEndTransition != Orbit.PatchTransitionType.ESCAPE && vessel.orbit.patchEndTransition != Orbit.PatchTransitionType.ENCOUNTER)) {
						transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(dnTime));
						DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.DN);
					}
				}

				double tClosestApproach;
				double dClosestApproach = JUtil.GetClosestApproach(vessel.orbit, orbit, out tClosestApproach);
				Orbit o = GetPatchAtUT(vessel.orbit, tClosestApproach);
				if (o != null) {
					Vector3d encounterPosition = o.SwappedRelativePositionAtUT(tClosestApproach) + o.referenceBody.getTruePositionAtUT(tClosestApproach) - vessel.orbit.referenceBody.getTruePositionAtUT(tClosestApproach);
					transformedPosition = screenTransform.MultiplyPoint3x4(encounterPosition);
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorClosestApproachValue, MapIcons.OtherIcon.SHIPATINTERCEPT);
				}

				// Unconditionally try to draw the closest approach point on
				// the target orbit.
				transformedPosition = screenTransform.MultiplyPoint3x4(orbit.SwappedRelativePositionAtUT(tClosestApproach));
				DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, iconColorClosestApproachValue, MapIcons.OtherIcon.TGTATINTERCEPT);
			} else {
				if (vessel.orbit.AscendingNodeEquatorialExists()) {
					double anTime = vessel.orbit.TimeOfAscendingNodeEquatorial(now);
					if (anTime < vessel.orbit.EndUT || (vessel.orbit.patchEndTransition != Orbit.PatchTransitionType.ESCAPE && vessel.orbit.patchEndTransition != Orbit.PatchTransitionType.ENCOUNTER)) {
						transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(anTime));
						DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.AN);
					}
				}
				if (vessel.orbit.DescendingNodeEquatorialExists()) {
					double dnTime = vessel.orbit.TimeOfDescendingNodeEquatorial(now);
					if (dnTime < vessel.orbit.EndUT || (vessel.orbit.patchEndTransition != Orbit.PatchTransitionType.ESCAPE && vessel.orbit.patchEndTransition != Orbit.PatchTransitionType.ENCOUNTER)) {
						transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(dnTime));
						DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.DN);
					}
				}
			}

			// Draw orbital features
			DrawNextPe(vessel.orbit, vessel.orbit.referenceBody, now, iconColorPEValue, screenTransform);

			DrawNextAp(vessel.orbit, vessel.orbit.referenceBody, now, iconColorAPValue, screenTransform);

			if (vessel.orbit.nextPatch != null && vessel.orbit.nextPatch.activePatch) {
				transformedPosition = screenTransform.MultiplyPoint3x4(vessel.orbit.SwappedRelativePositionAtUT(vessel.orbit.EndUT));
				DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorSelfValue, MapIcons.OtherIcon.EXITSOI);

				Orbit nextPatch = vessel.orbit.nextPatch.nextPatch;
				if (nextPatch != null && nextPatch.activePatch) {
					transformedPosition = screenTransform.MultiplyPoint3x4(nextPatch.SwappedRelativePositionAtUT(nextPatch.EndUT)+nextPatch.referenceBody.getTruePositionAtUT(nextPatch.EndUT) - vessel.orbit.referenceBody.getTruePositionAtUT(nextPatch.EndUT));
					DrawIcon(transformedPosition.x, transformedPosition.y, VesselType.Unknown, orbitColorNextNodeValue, MapIcons.OtherIcon.EXITSOI);
				}
			}

			if (node != null && node.nextPatch.activePatch) {
				DrawNextPe(node.nextPatch, vessel.orbit.referenceBody, now, orbitColorNextNodeValue, screenTransform);

				DrawNextAp(node.nextPatch, vessel.orbit.referenceBody, now, orbitColorNextNodeValue, screenTransform);

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
