// Analysis disable once RedundantUsingDirective
using System;
using UnityEngine;

namespace JSI
{
	public class FlyingCamera
	{
		private readonly Vessel ourVessel;
		private readonly Part ourPart;
		private GameObject cameraTransform;
		private Part cameraPart;
		private readonly Camera[] cameraObject = { null, null, null };
		private readonly float cameraAspect;
		private bool enabled;
		private readonly RenderTexture screenTexture;
		private readonly bool hasVisualEnhancements;

		public float FOV { get; set; }

		public FlyingCamera(Part thatPart, RenderTexture screen, float aspect)
		{
			ourVessel = thatPart.vessel;
			ourPart = thatPart;
			screenTexture = screen;
			cameraAspect = aspect;

			hasVisualEnhancements = GameDatabase.Instance.ExistsTexture("BoulderCo/Clouds/Textures/particle");
		}

		public void PointCamera(string newCameraName, float initialFOV)
		{
			CleanupCameraObjects();
			if (!string.IsNullOrEmpty(newCameraName)) {
				FOV = initialFOV;
				// First, we search our own part for this camera transform,
				// only then we search all other parts of the vessel.
				if (!LocateCamera(ourPart, newCameraName))
					foreach (Part thatpart in ourVessel.parts) {
						if (LocateCamera(thatpart, newCameraName))
							break;
					}

				if (cameraTransform != null) {
					CameraSetup(0, "Camera ScaledSpace");
					CameraSetup(1, "Camera 01");
					CameraSetup(2, "Camera 00");
					enabled = true;
					JUtil.LogMessage(this, "Switched to camera \"{0}\".", cameraTransform.name);
					return;
				} 
				JUtil.LogMessage(this, "Tried to switch to camera \"{0}\" but camera was not found.", newCameraName);
			}
		}

		private void CleanupCameraObjects()
		{
			if (enabled) {
				for (int i = 0; i < 3; i++)
					if (cameraObject[i].gameObject != null) {
						UnityEngine.Object.Destroy(cameraObject[i].gameObject);
						cameraObject[i] = null;
					}
				enabled = false;
				JUtil.LogMessage(this, "Turning camera off.");
			}
			cameraPart = null;
			cameraTransform = null;
		}

		private bool LocateCamera(Part thatpart, string transformName)
		{
			Transform location = thatpart.FindModelTransform(transformName);
			if (location != null) {
				cameraTransform = location.gameObject;
				cameraPart = thatpart;
				return true;
			}
			return false;
		}

		private void CameraSetup(int index, string sourceName)
		{
			var cameraBody = new GameObject();
			cameraBody.name = typeof(RasterPropMonitor).Name + index + cameraBody.GetInstanceID();
			cameraObject[index] = cameraBody.AddComponent<Camera>();

			Camera sourceCam = null;
			foreach (Camera cam in Camera.allCameras) {
				if (cam.name == sourceName) {
					sourceCam = cam;
					break;
				}
			}

			cameraObject[index].CopyFrom(sourceCam);
			cameraObject[index].enabled = false;
			cameraObject[index].targetTexture = screenTexture;
			cameraObject[index].aspect = cameraAspect;

			// Special handling for Visual Enhancements mod.
			if (hasVisualEnhancements && index == 0) {
				cameraObject[index].cullingMask |= (1 << 3) | (1 << 2);
			}

		}

		public Quaternion CameraRotation(float yawOffset = 0.0f, float pitchOffset = 0.0f)
		{
			Quaternion rotation = cameraTransform.transform.rotation;
			Quaternion offset = Quaternion.Euler(new Vector3(pitchOffset, yawOffset, 0.0f));
			return rotation * offset;
		}

		public bool Render(float yawOffset = 0.0f, float pitchOffset = 0.0f)
		{
			if (!enabled)
				return false;

			if (cameraPart == null || cameraPart.vessel != FlightGlobals.ActiveVessel || cameraTransform == null || cameraTransform.transform == null) {
				CleanupCameraObjects();
				return false;
			}

			Quaternion rotation = cameraTransform.transform.rotation;
			Quaternion offset = Quaternion.Euler(new Vector3(pitchOffset, yawOffset, 0.0f));
			rotation = rotation * offset;

			// ScaledSpace camera is special. :(
			cameraObject[0].transform.rotation = rotation;
			cameraObject[0].fieldOfView = FOV;
			cameraObject[0].Render();
			for (int i = 1; i < 3; i++) {
				cameraObject[i].transform.position = cameraTransform.transform.position;
				cameraObject[i].transform.rotation = rotation;
				cameraObject[i].fieldOfView = FOV;


				cameraObject[i].Render();
			}
			return true;
		}
	}
}

