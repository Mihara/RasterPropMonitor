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
		[KSPField]
		public bool showRay = true;
		[KSPField]
		public Vector3 rotateCamera = Vector3.zero;
		[KSPField]
		public Vector3 translateCamera = Vector3.zero;
		// Internal data storage.
		[KSPField(isPersistant = true)]
		public int current = 1;
		// Fields to handle right-click GUI.
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Camera ID: ")]
		public string visibleCameraName;
		[UI_Toggle(disabledText = "off", enabledText = "on")]
		[KSPField(guiActiveEditor = true, guiName = "FOV marker ", isPersistant = true)]
		public bool showCones = true;
		// The rest of it
		private GameObject lightCone;
		private LineRenderer lightConeRenderer;
		private static readonly Material lightConeMaterial = new Material(Shader.Find("Particles/Additive"));
		private Transform actualCamera;
		private const float endSpan = 15f;
		private const float fovAngle = 60f;

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ID +")]
		public void IdPlus()
		{
			current++;
			if (current > maximum)
				current = 1;
			UpdateName();
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ID -")]
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
			if (containingTransform.childCount > 0) {
				actualCamera = containingTransform.GetChild(0);
			} else {
				actualCamera = new GameObject().transform;
				actualCamera.parent = containingTransform;
			}
			actualCamera.position = containingTransform.position;
			actualCamera.rotation = containingTransform.rotation;

			if (rotateCamera != Vector3.zero)
				actualCamera.transform.Rotate(rotateCamera);
			if (translateCamera != Vector3.zero)
				actualCamera.transform.localPosition = translateCamera;

			visibleCameraName = actualCamera.name = cameraIDPrefix + current;
			if (HighLogic.LoadedSceneIsEditor)
				ColorizeLightCone();
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (state == StartState.Editor) {
				if (part.parent == null) {
					foreach (Part thatPart in EditorLogic.SortedShipList) {
						if (thatPart != part) {
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
				if (showRay)
					part.OnEditorDetach += new Callback(PickupCamera);
				part.OnEditorDestroy += new Callback(DestroyLightCone);
			} else
				DestroyLightCone();
			UpdateName();
		}

		private void PickupCamera()
		{
			showCones = true;
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
			if (lightConeRenderer != null) {
				var newStart = Color32.Lerp(new Color32(0, 0, 255, 178), new Color32(255, 0, 0, 178), 1f / (maximum) * (current - 1));
				lightConeRenderer.SetColors(newStart, new Color32(newStart.r, newStart.g, newStart.b, 0));
			}
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
			showCones = false;
		}

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsEditor)
				return;

			showCones |= GameSettings.HEADLIGHT_TOGGLE.GetKeyDown();
			showCones &= !GameSettings.HEADLIGHT_TOGGLE.GetKeyUp();
							
			if (showCones)
				CreateLightCone();
			else
				DestroyLightCone();

			DrawLightCone();
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
			return 	"Hold down '" + GameSettings.HEADLIGHT_TOGGLE.primary + "' to display all the camera fields of view at once.";
		}
	}
}

