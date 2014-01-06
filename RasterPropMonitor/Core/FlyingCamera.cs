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
		private readonly Camera[] cameraObject = { null, null, null, null, null };
		private readonly float cameraAspect;
		private bool enabled;
		private readonly RenderTexture screenTexture;
		private bool isReferenceCamera;
		private const string referenceCamera = "CurrentReferenceDockingPortCamera";
		private readonly Quaternion referencePointRotation = Quaternion.Euler(-90, 0, 0);
		private float flickerChance;
		private int flickerMaxTime;
		private int flickerCounter;

		public float FOV { get; set; }

		public FlyingCamera(Part thatPart, RenderTexture screen, float aspect)
		{
			ourVessel = thatPart.vessel;
			ourPart = thatPart;
			screenTexture = screen;
			cameraAspect = aspect;
		}

		public void SetFlicker(float flicker, int flickerTime) {
			flickerChance = flicker;
			flickerMaxTime = flickerTime;
			flickerCounter = 0;
		}

		public void PointCamera(string newCameraName, float initialFOV)
		{
			CleanupCameraObjects();
			if (!string.IsNullOrEmpty(newCameraName)) {
				FOV = initialFOV;

				if (newCameraName == referenceCamera) {
					PointToReferenceCamera();
					JUtil.LogMessage(this, "Tracking reference point docking port camera.");
				} else {
					CreateCameraObjects(newCameraName);
				}
			}
		}

		private void PointToReferenceCamera()
		{
			isReferenceCamera = true;
			ModuleDockingNode thatPort = null;
			foreach (PartModule thatModule in ourVessel.GetReferenceTransformPart().Modules) {
				thatPort = thatModule as ModuleDockingNode;
				if (thatPort != null)
					break;
			}
			if (thatPort != null) {
				cameraPart = thatPort.part;
				cameraTransform = ourVessel.ReferenceTransform.gameObject;
				CreateCameraObjects();
			}
		}

		private void CreateCameraObjects(string newCameraName = null)
		{

			if (!string.IsNullOrEmpty(newCameraName)) {
				isReferenceCamera = false;
				// First, we search our own part for this camera transform,
				// only then we search all other parts of the vessel.
				if (!LocateCamera(ourPart, newCameraName)) {
					foreach (Part thatpart in ourVessel.parts) {
						if (LocateCamera(thatpart, newCameraName))
							break;
					}
				}
			}
			if (cameraTransform != null) {
				CameraSetup(0, "Camera ScaledSpace");
				// These two cameras are created by Visual Enhancements mod.
				// I'm still not completely satisfied with the look, but it's definitely an improvement.
				CameraSetup(1, "Camera VE Underlay");
				CameraSetup(2, "Camera VE Overlay");
				CameraSetup(3, "Camera 01");
				CameraSetup(4, "Camera 00");
				enabled = true;
				JUtil.LogMessage(this, "Switched to camera \"{0}\".", cameraTransform.name);
				return;
			} 
			JUtil.LogMessage(this, "Tried to switch to camera \"{0}\" but camera was not found.", newCameraName);

		}

		private void CleanupCameraObjects()
		{
			if (enabled) {
				for (int i = 0; i < cameraObject.Length; i++) {
					try {
						UnityEngine.Object.Destroy(cameraObject[i]);
						// Analysis disable once EmptyGeneralCatchClause
					} catch {
						// Yes, that's really what it's supposed to be doing.
					} finally {
						cameraObject[i] = null;
					}
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
			Camera sourceCam = null;
			foreach (Camera cam in Camera.allCameras) {
				if (cam.name == sourceName) {
					sourceCam = cam;
					break;
				}
			}

			if (sourceCam != null) {
				var cameraBody = new GameObject();
				cameraBody.name = typeof(RasterPropMonitor).Name + index + cameraBody.GetInstanceID();
				cameraObject[index] = cameraBody.AddComponent<Camera>();

				cameraObject[index].CopyFrom(sourceCam);
				cameraObject[index].enabled = false;
				cameraObject[index].targetTexture = screenTexture;
				cameraObject[index].aspect = cameraAspect;
			}
		}

		public Quaternion CameraRotation(float yawOffset = 0.0f, float pitchOffset = 0.0f)
		{
			Quaternion rotation = cameraTransform.transform.rotation;
			if (isReferenceCamera)
				rotation *= referencePointRotation;
			Quaternion offset = Quaternion.Euler(new Vector3(pitchOffset, yawOffset, 0.0f));
			return rotation * offset;
		}

		public Transform GetTransform()
		{
			return cameraTransform.transform;
		}

		public Vector3 GetTransformForward()
		{
			return isReferenceCamera ? cameraTransform.transform.up : cameraTransform.transform.forward;
		}

		public bool Render(float yawOffset = 0.0f, float pitchOffset = 0.0f)
		{

			if (isReferenceCamera) {
				if (cameraTransform != ourVessel.ReferenceTransform.gameObject) {
					CleanupCameraObjects();
					PointToReferenceCamera();
				}
			}

			if (!enabled)
				return false;

			if (cameraPart == null || cameraPart.vessel != FlightGlobals.ActiveVessel || cameraTransform == null || cameraTransform.transform == null) {
				CleanupCameraObjects();
				return false;
			}

			// Randomized camera flicker.
			if (flickerChance > 0 && flickerCounter == 0) {
				if (flickerChance > UnityEngine.Random.Range(0f, 1000f)) {
					flickerCounter = UnityEngine.Random.Range(1, flickerMaxTime);
				}
			}
			if (flickerCounter > 0) {
				flickerCounter--;
				return false;
			}

			Quaternion rotation = cameraTransform.transform.rotation;

			if (isReferenceCamera) {
				// Reference transforms of docking ports have the wrong orientation, so need an extra rotation applied before that.
				rotation *= referencePointRotation;
			}

			Quaternion offset = Quaternion.Euler(new Vector3(pitchOffset, yawOffset, 0.0f));
			rotation = rotation * offset;


			for (int i = 0; i < cameraObject.Length; i++) {
				if (cameraObject[i] != null) {
					// ScaledSpace camera and it's derived cameras from Visual Enhancements mod are special - they don't move.
					if (i >= 3)
						cameraObject[i].transform.position = cameraTransform.transform.position;
					cameraObject[i].transform.rotation = rotation;
					cameraObject[i].fieldOfView = FOV;
					cameraObject[i].Render();
				}
			}
			return true;
		}
	}
}

