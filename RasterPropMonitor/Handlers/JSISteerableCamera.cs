using System;
using UnityEngine;

namespace JSI
{
	/*
	 * The JSISteerableCamera provides a background handler that adds yaw and
	 * pitch capabilities to a camera (in addition to the zoom of the basic
	 * camera).  All movements are smoothly applied while the associated
	 * button is held down.  zoom and pan limits, as well as zoom and pan
	 * rates, are configurable.  Since multi-press isn't possible in KSP IVA,
	 * only a single action can take place at a time (zoom, or yaw, or pitch).
	 *
	 * Configuration values should be checked to make sure that camera
	 * clipping (seeing inside parts) doesn't happen.  This can be especially
	 * noticeable with surface-mounted cameras and a negative pitch.
	 *
	 * Configuration:
	 *
	 * zoomIn, zoomOut, yawLeft, yawRight, pitchUp, pitchDown -- number of the
	 * globalButton that controls each of these modes.  Defaults to 99
	 * (disables the behavior).
	 *
	 * cameraTransform -- the name of the camera that this background handler
	 * will use for rendering.
	 *
	 * fovLimits -- The upper and lower bound of the zoom control, in degrees.
	 * Defaults to (60, 60).  Smaller values equate to higher zoom levels.  The
	 * camera starts at the largest field of view (lowest zoom).
	 *
	 * yawLimits -- the upper and lower bound of yaw (side-to-side) camera
	 * movement.  Negative values are left, positive values are right.  The
	 * values do not have to be symmetrical (-10, 40 is okay, for instance).
	 * The lower value must be zero or negative, the upper value must be zero
	 * or positive.  The camera starts with a yaw of 0.
	 *
	 * pitchLimits -- the upper and lower bound of pitch (up-to-down) camera
	 * movement.  Positive values are up, negative values are down.  The values
	 * do not have to be symmetrical.  The lower values must be zero or
	 * negative, the upper value must be zero or positive.  The camera starts
	 * with a pitch of 0.
	 *
	 * zoomRate, yawRate, pitchRate -- controls how quickly the camera will
	 * zoom, yaw, or pitch, measured in degrees per second.
	 */
	public class JSISteerableCamera:InternalModule
	{
		[KSPField]
		public int zoomIn = -1;
		[KSPField]
		public int zoomOut = -1;
		[KSPField]
		public int yawLeft = -1;
		[KSPField]
		public int yawRight = -1;
		[KSPField]
		public int pitchUp = -1;
		[KSPField]
		public int pitchDown = -1;
		[KSPField]
		public int toggleTargetIcon = -1;
		[KSPField]
		public Vector2 fovLimits = new Vector2(60.0f, 60.0f);
		[KSPField]
		public Vector2 yawLimits = new Vector2(0.0f, 0.0f);
		[KSPField]
		public Vector2 pitchLimits = new Vector2(0.0f, 0.0f);
		[KSPField]
		public float zoomRate;
		[KSPField]
		public float yawRate;
		[KSPField]
		public float pitchRate;
		[KSPField]
		public string cameraTransform = string.Empty;
		[KSPField]
		public string targetIconColor = "255, 0, 255, 255";
		// magenta, to match KSP stock
		[KSPField]
		public float iconPixelSize = 8f;
		[KSPField]
		public bool showTargetIcon;
		[KSPField]
		public string homeCrosshairColor = "0,0,0,0";
		private Material homeCrosshairMaterial;
		private FlyingCamera cameraObject;
		private float currentFoV;
		private float currentYaw;
		private float currentPitch;
		private float zoomDirection;
		private float yawDirection;
		private float pitchDirection;
		private double lastUpdateTime;
		// Target tracking icon
		private Texture2D gizmoTexture;
		private Material iconMaterial;

		private static Vector2 ClampToEdge(Vector2 position)
		{
			return position / (Math.Abs(position.x) > Math.Abs(position.y) ? Math.Abs(position.x) : Math.Abs(position.y));
		}

		private Vector2 GetNormalizedScreenPosition(Vector3 directionVector, float yawOffset, float pitchOffset, float cameraAspect)
		{
			// Transform direction using the active camera's rotation.
			var targetTransformed = cameraObject.CameraRotation(currentYaw, -currentPitch).Inverse() * directionVector;

			// (x, y) provided the lateral displacement.  (z) provides the "in front of / behind"
			var targetDisp = new Vector2(targetTransformed.x, -targetTransformed.y);

			// I want to scale the displacement such that 1.0
			// represents the edge of the viewport. And my math is too
			// rusty to remember the right way to get that scalar.
			// Both of these are off by just a bit at wider zooms
			// (tan scales a little too much, sin a little too
			// little).  It may simply be an artifact of the camera
			// perspective divide.
			var fovScale = new Vector2(cameraAspect * Mathf.Tan(Mathf.Deg2Rad * currentFoV * 0.5f), Mathf.Tan(Mathf.Deg2Rad * currentFoV * 0.5f));
			//Vector2 fovScale = new Vector2(cameraAspect * Mathf.Sin(Mathf.Deg2Rad * currentFoV * 0.5f), Mathf.Sin(Mathf.Deg2Rad * currentFoV * 0.5f));

			// MOARdV: Are there no overloaded operators for vector math?
			// Normalize to a [-1,+1] range on both axes
			targetDisp.x = targetDisp.x / fovScale.x;
			targetDisp.y = targetDisp.y / fovScale.y;

			// If the target is behind the camera, or outside the
			// bounds of the viewport, the icon needs to be clamped
			// to the edge.
			if (targetTransformed.z < 0.0f || Math.Max(Math.Abs(targetDisp.x), Math.Abs(targetDisp.y)) > 1.0f) {
				targetDisp = ClampToEdge(targetDisp);
			}

			targetDisp.x = targetDisp.x * 0.5f + 0.5f;
			targetDisp.y = targetDisp.y * 0.5f + 0.5f;

			return targetDisp;
		}

		public bool RenderCamera(RenderTexture screen, float cameraAspect)
		{
			// Just in case.
			if (!HighLogic.LoadedSceneIsFlight) {
				return false;
			}

			if (string.IsNullOrEmpty(cameraTransform)) {
				return false;
			}

			if (cameraObject == null) {
				cameraObject = new FlyingCamera(part, screen, cameraAspect);
				currentFoV = fovLimits.y;
				cameraObject.PointCamera(cameraTransform, currentFoV);
			}

			cameraObject.FOV = currentFoV;

			// Negate pitch - the camera object treats a negative pitch as "up"
			if (cameraObject.Render(currentYaw, -currentPitch)) {
				ITargetable target = FlightGlobals.fetch.VesselTarget;

				bool drawSomething = ((gizmoTexture != null && target != null && showTargetIcon) || homeCrosshairMaterial.color.a > 0);

				if (drawSomething) {
					GL.PushMatrix();
					GL.LoadPixelMatrix(0, screen.width, screen.height, 0);
				}

				if (gizmoTexture != null && target != null && showTargetIcon) {
					// Figure out which direction the target is.
					Vector3 targetDisplacement = target.GetTransform().position - cameraObject.GetTransform().position;
					targetDisplacement.Normalize();

					// Transform it using the active camera's rotation.
					var targetDisp = GetNormalizedScreenPosition(targetDisplacement, currentYaw, -currentPitch, cameraAspect);

					var iconCenter = new Vector2(screen.width * targetDisp.x, screen.height * targetDisp.y);

					// Apply some clamping values to force the icon to stay on screen
					iconCenter.x = Math.Max(iconPixelSize * 0.5f, iconCenter.x);
					iconCenter.x = Math.Min(screen.width - iconPixelSize * 0.5f, iconCenter.x);
					iconCenter.y = Math.Max(iconPixelSize * 0.5f, iconCenter.y);
					iconCenter.y = Math.Min(screen.height - iconPixelSize * 0.5f, iconCenter.y);

					var position = new Rect(iconCenter.x - iconPixelSize * 0.5f, iconCenter.y - iconPixelSize * 0.5f, iconPixelSize, iconPixelSize);
					// TGT+ is at (2/3, 2/3).
					var srcRect = new Rect(2.0f / 3.0f, 2.0f / 3.0f, 1.0f / 3.0f, 1.0f / 3.0f);

					Graphics.DrawTexture(position, gizmoTexture, srcRect, 0, 0, 0, 0, iconMaterial);
				}

				if (homeCrosshairMaterial.color.a > 0) {
					// Mihara: Reference point cameras are different enough to warrant it.
					var cameraForward = cameraObject.GetTransformForward();
					var crossHairCenter = GetNormalizedScreenPosition(cameraForward, 0.0f, 0.0f, cameraAspect);
					crossHairCenter.x *= screen.width;
					crossHairCenter.y *= screen.height;
					crossHairCenter.x = Math.Max(iconPixelSize * 0.5f, crossHairCenter.x);
					crossHairCenter.x = Math.Min(screen.width - iconPixelSize * 0.5f, crossHairCenter.x);
					crossHairCenter.y = Math.Max(iconPixelSize * 0.5f, crossHairCenter.y);
					crossHairCenter.y = Math.Min(screen.height - iconPixelSize * 0.5f, crossHairCenter.y);

					float zoomAdjustedIconSize = iconPixelSize * Mathf.Tan(Mathf.Deg2Rad * fovLimits.y * 0.5f) / Mathf.Tan(Mathf.Deg2Rad * currentFoV * 0.5f); 

					homeCrosshairMaterial.SetPass(0);
					GL.Begin(GL.LINES);
					GL.Vertex3(crossHairCenter.x - zoomAdjustedIconSize * 0.5f, crossHairCenter.y, 0.0f);
					GL.Vertex3(crossHairCenter.x + zoomAdjustedIconSize * 0.5f, crossHairCenter.y, 0.0f);
					GL.Vertex3(crossHairCenter.x, crossHairCenter.y - zoomAdjustedIconSize * 0.5f, 0.0f);
					GL.Vertex3(crossHairCenter.x, crossHairCenter.y + zoomAdjustedIconSize * 0.5f, 0.0f);
					GL.End();
				}

				if (drawSomething) {
					GL.PopMatrix();
				}

				return true;
			}
			return false;
		}

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel) || cameraObject == null) {
				return;
			}

			double thisUpdateTime = Planetarium.GetUniversalTime();

			// Just to be safe, never allow negative values.
			float dT = Math.Max(0.0f, (float)(thisUpdateTime - lastUpdateTime));

			currentFoV = Math.Max(fovLimits.x, Math.Min(fovLimits.y, currentFoV + dT * zoomRate * zoomDirection));
			currentYaw = Math.Max(yawLimits.x, Math.Min(yawLimits.y, currentYaw + dT * yawRate * yawDirection));
			currentPitch = Math.Max(pitchLimits.x, Math.Min(pitchLimits.y, currentPitch + dT * pitchRate * pitchDirection));

			lastUpdateTime = thisUpdateTime;
		}

		public void ClickProcessor(int buttonID)
		{
			if (cameraObject == null) {
				return;
			}

			if (buttonID == zoomIn) {
				zoomDirection = -1.0f;
				yawDirection = 0.0f;
				pitchDirection = 0.0f;
			} else if (buttonID == zoomOut) {
				zoomDirection = 1.0f;
				yawDirection = 0.0f;
				pitchDirection = 0.0f;
			} else if (buttonID == yawLeft) {
				zoomDirection = 0.0f;
				yawDirection = -1.0f;
				pitchDirection = 0.0f;
			} else if (buttonID == yawRight) {
				zoomDirection = 0.0f;
				yawDirection = 1.0f;
				pitchDirection = 0.0f;
			} else if (buttonID == pitchUp) {
				zoomDirection = 0.0f;
				yawDirection = 0.0f;
				pitchDirection = 1.0f;
			} else if (buttonID == pitchDown) {
				zoomDirection = 0.0f;
				yawDirection = 0.0f;
				pitchDirection = -1.0f;
			} else if (buttonID == toggleTargetIcon) {
				showTargetIcon = !showTargetIcon;
			}

			// Always reset the lastUpdateTime on a button click, in case it
			// has been a while since the last click.
			lastUpdateTime = Planetarium.GetUniversalTime();
		}
		// Analysis disable once UnusedParameter
		public void ReleaseProcessor(int buttonID)
		{
			// Always clear all movements here.  We don't support multi-click :)
			zoomDirection = 0.0f;
			pitchDirection = 0.0f;
			yawDirection = 0.0f;
		}

		public void Start()
		{
			// canonicalize the limits
			if (fovLimits.x > fovLimits.y) {
				//std::swap(fovLimits.x, fovLimits.y);
				float f = fovLimits.x;
				fovLimits.x = fovLimits.y;
				fovLimits.y = f;
			}

			if (yawLimits.x > yawLimits.y) {
				//std::swap(yawLimits.x, yawLimits.y);
				float f = yawLimits.x;
				yawLimits.x = yawLimits.y;
				yawLimits.y = f;
			}

			if (pitchLimits.x > pitchLimits.y) {
				//std::swap(pitchLimits.x, pitchLimits.y);
				float f = pitchLimits.x;
				pitchLimits.x = pitchLimits.y;
				pitchLimits.y = f;
			}

			// Always requiure 0.0 to be within the legal range of yuaw
			// and pitch.
			yawLimits.x = Math.Min(0.0f, yawLimits.x);
			yawLimits.y = Math.Max(0.0f, yawLimits.y);
			pitchLimits.x = Math.Min(0.0f, pitchLimits.x);
			pitchLimits.y = Math.Max(0.0f, pitchLimits.y);

			gizmoTexture = JUtil.GetGizmoTexture();

			iconMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));

			// MOARdV: The maneuver gizmo texture is white. Unity's DrawTexture
			// expects a (0.5, 0.5, 0.5, 0.5) texture to be neutral for coloring
			// purposes.  Multiplying the desired alpha by 1/2 gets around the
			// gizmo texture's color, and gets correct alpha effects.
			Color32 iconColor = ConfigNode.ParseColor32(targetIconColor);
			iconColor.a /= 2;
			iconMaterial.color = iconColor;

			homeCrosshairMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
			homeCrosshairMaterial.color = ConfigNode.ParseColor32(homeCrosshairColor);
		}
	}
}
