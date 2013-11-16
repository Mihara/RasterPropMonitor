using UnityEngine;

namespace JSI
{
	public class JSIExternalCameraSelector: PartModule
	{
		// Actual configuration parameters.
		[KSPField]
		public string cameraContainer;
		[KSPField]
		public string cameraIDPrefix = "ExtCam";
		[KSPField]
		public int maximum = 8;
		// Internal data storage.
		[KSPField(isPersistant = true)]
		public int current = 1;
		// Fields to handle right-click GUI.
		[KSPField(guiActive = true, guiName = "Camera ID: ")]
		public string visibleCameraName;

		[KSPEvent(guiActive = true, guiName = "ID+")]
		public void IdPlus()
		{
			current++;
			if (current > maximum)
				current = 1;
			updateName();
		}

		[KSPEvent(guiActive = true, guiName = "ID-")]
		public void IdMinus()
		{
			current--;
			if (current <= 0)
				current = maximum;
			updateName();
		}

		private void updateName()
		{
			Transform containingTransform = part.FindModelTransform(cameraContainer);
			Transform actualCamera = null;
			foreach (Transform thatTransform in containingTransform.gameObject.GetComponentsInChildren<Transform>()) {
				if (containingTransform != thatTransform) {
					actualCamera = thatTransform;
					break;
				}
			}
			// Is that even supposed to work?...
			if (actualCamera == null) {
				actualCamera = new GameObject().transform;
				actualCamera.parent = containingTransform;
				actualCamera.localPosition = Vector3.zero;
				actualCamera.localRotation = Quaternion.identity;
			}
			visibleCameraName = actualCamera.name = cameraIDPrefix + current;
		}

		public override void OnStart(PartModule.StartState state)
		{
			updateName();
		}
	}
}

