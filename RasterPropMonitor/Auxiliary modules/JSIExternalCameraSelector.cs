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
		private GameObject lightCone;
		private LineRenderer lightConeRenderer;
		private static readonly Material lightConeMaterial = new Material(Shader.Find("Particles/Additive"));
		private static ScreenMessage cameraMessage;
		private Transform actualCamera;
		private const float endSpan = 15f;
		private const float fovAngle = 60f;

		[KSPEvent(guiActive = true, guiName = "ID +")]
		public void IdPlus()
		{
			current++;
			if (current > maximum)
				current = 1;
			UpdateName();
		}

		[KSPEvent(guiActive = true, guiName = "ID -")]
		public void IdMinus()
		{
			current--;
			if (current <= 0)
				current = maximum;
			UpdateName();
		}

		private void UpdateName()
		{
			Transform containingTransform = part.FindModelTransform(cameraContainer);
			foreach (Transform thatTransform in containingTransform.gameObject.GetComponentsInChildren<Transform>()) {
				if (containingTransform != thatTransform) {
					actualCamera = thatTransform;
					break;
				}
			}
			// I'm amused to find that this does appear to work.
			if (actualCamera == null) {
				actualCamera = new GameObject().transform;
				actualCamera.position = containingTransform.position;
				actualCamera.rotation = containingTransform.rotation;
				actualCamera.parent = containingTransform;
			}
			visibleCameraName = actualCamera.name = cameraIDPrefix + current;
			if (HighLogic.LoadedSceneIsEditor) {
				if (cameraMessage != null) {
					ScreenMessages.RemoveMessage(cameraMessage);
					cameraMessage = null;
				}
				cameraMessage = ScreenMessages.PostScreenMessage("Camera ID: " + visibleCameraName, 3, ScreenMessageStyle.UPPER_RIGHT);
				if (lightConeRenderer != null)
					ColorizeLightCone();
			}
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (state == StartState.Editor) {
				if (part.parent == null) {
					foreach (Part thatPart in EditorLogic.SortedShipList) {
						if (thatPart.name == part.name && thatPart != this) {
							foreach (PartModule thatModule in thatPart.Modules) {
								var peerModule = thatModule as JSIExternalCameraSelector;
								if (peerModule != null && peerModule.cameraIDPrefix == cameraIDPrefix && peerModule.current == current)
									IdPlus();
							}
						}
					}
					CreateLightCone();
				}
				part.OnEditorAttach += new Callback(DestroyLightCone);
				part.OnEditorDetach += new Callback(PickupCamera);
				part.OnEditorDestroy += new Callback(DestroyLightCone);
			} else {
				DestroyLightCone();
			}
			UpdateName();
		}

		private void PickupCamera()
		{
			UpdateName();
			CreateLightCone();
		}

		private void CreateLightCone()
		{
			RenderingManager.AddToPostDrawQueue(0, DrawLightCone);
			if (lightConeRenderer == null) {
				lightCone = new GameObject();
				lightConeRenderer = lightCone.AddComponent<LineRenderer>();
				lightConeRenderer.useWorldSpace = true;
				lightConeRenderer.material = lightConeMaterial;
				lightConeRenderer.SetWidth(0.054f, endSpan);
				lightConeRenderer.SetVertexCount(2);
				lightConeRenderer.castShadows = false;
				lightConeRenderer.receiveShadows = false;
				lightConeRenderer.SetPosition(0, Vector3.zero);
				lightConeRenderer.SetPosition(1, Vector3.zero);
				ColorizeLightCone();
			}
		}

		private void ColorizeLightCone()
		{
			var newStart = Color32.Lerp(new Color32(0, 0, 255, 178), new Color32(255, 0, 0, 178), 1f / (maximum) * (current - 1));
			lightConeRenderer.SetColors(newStart, new Color32(newStart.r, newStart.g, newStart.b, 0));
		}

		private void DestroyLightCone()
		{
			RenderingManager.RemoveFromPostDrawQueue(0, DrawLightCone);
			if (lightConeRenderer != null) {
				Destroy(lightConeRenderer);
				lightConeRenderer = null;
				Destroy(lightCone);
				lightCone = null;
			}
		}

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsEditor)
				return;

			if (Input.GetKeyDown(KeyCode.Space)) { 
				RaycastHit whereAmI;
				Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out whereAmI);
				if (Part.FromGO(whereAmI.transform.gameObject) == part) {
					IdPlus();
				}
			}

			if (GameSettings.HEADLIGHT_TOGGLE.GetKeyDown()) {
				CreateLightCone();
			}
			if (GameSettings.HEADLIGHT_TOGGLE.GetKeyUp()) {
				DestroyLightCone();
			}
		}

		public void DrawLightCone()
		{
			if (!HighLogic.LoadedSceneIsEditor) {
				DestroyLightCone();
			}
			if (lightConeRenderer != null && actualCamera != null) {
				Vector3 origin = actualCamera.transform.TransformPoint(Vector3.zero);
				Vector3 direction = actualCamera.transform.TransformDirection(Vector3.forward);
				lightConeRenderer.SetPosition(0, origin);
				lightConeRenderer.SetPosition(1, origin + direction * (endSpan / 2 / Mathf.Tan(Mathf.Deg2Rad * fovAngle / 2)));
			} else
				DestroyLightCone();
		}

		public override string GetInfo()
		{
			return "Look for the ID of the camera in the top right corner of the screen.\n" +
			"Press SPACE while holding the mouse over the part to select the camera's ID number.\n" +
			"Hold down 'U' to display all the camera fields of view.";
		}
	}
}

