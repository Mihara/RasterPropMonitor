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
		private Camera[] cameraObject = { null, null, null };
		private readonly float cameraAspect;
		private bool enabled;
		private readonly RenderTexture screenTexture;

		public float FOV { get; set; }

		public FlyingCamera(Part thatPart, RenderTexture screen, float aspect)
		{
			ourVessel = thatPart.vessel;
			ourPart = thatPart;
			screenTexture = screen;
			cameraAspect = aspect;
		}

		public void PointCamera(string newCameraName, float initialFOV)
		{
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
					if (!enabled) {
						CameraSetup(0, "Camera ScaledSpace");
						CameraSetup(1, "Camera 01");
						CameraSetup(2, "Camera 00");
						enabled = true;
					}
					Debug.Log(string.Format("Switching to camera \"{0}\".", cameraTransform.name));

				} else {
					CleanupCameraObjects();
					Debug.Log(string.Format("Tried to switch to camera \"{0}\" but camera was not found.", newCameraName));
				}
			} else {
				CleanupCameraObjects();
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
				Debug.Log("Turning camera off.");
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
		}

		public bool Render()
		{
			if (enabled) {
				if (cameraPart.vessel != FlightGlobals.ActiveVessel) {
					CleanupCameraObjects();
				} else {
					// ScaledSpace camera is special. :(
					cameraObject[0].transform.rotation = cameraTransform.transform.rotation;
					cameraObject[0].fieldOfView = FOV;
					cameraObject[0].Render();
					for (int i = 1; i < 3; i++) {
						cameraObject[i].transform.position = cameraTransform.transform.position;
						cameraObject[i].transform.rotation = cameraTransform.transform.rotation;
						cameraObject[i].fieldOfView = FOV;


						cameraObject[i].Render();
					}
					return true;
				}
			}
			return false;
		}
	}
}

