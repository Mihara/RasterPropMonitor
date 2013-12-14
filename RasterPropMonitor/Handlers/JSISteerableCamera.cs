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
		public string cameraTransform;
		private FlyingCamera cameraObject;
		private float currentFoV;
		private float currentYaw;
		private float currentPitch;
		private float zoomDirection;
		private float yawDirection;
		private float pitchDirection;
		private double lastUpdateTime;

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

				cameraObject = new FlyingCamera(part, screen, cameraAspect);
				currentFoV = fovLimits.y;
				cameraObject.PointCamera(cameraTransform, currentFoV);
			}

			cameraObject.FOV = currentFoV;

			// Negate pitch - the camera object treats a negative pitch as "up"
			return cameraObject.Render(currentYaw, -currentPitch);
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
	}
}
