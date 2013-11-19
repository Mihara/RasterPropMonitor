using UnityEngine;

namespace JSI
{
	public class JSIPrimaryFlightDisplay: InternalModule
	{
		[KSPField]
		public int drawingLayer = 17;
		[KSPField]
		public string horizonTexture;
		[KSPField]
		public string navBallModel;
		[KSPField]
		public string staticOverlay;
		[KSPField]
		public string headingBar;
		[KSPField]
		public float screenAspect = 1.35f;
		[KSPField]
		public Color backgroundColor = Color.blue;
		[KSPField]
		public float ballOpacity = 1f;
		[KSPField]
		public float markerScale = 0.1f;
		[KSPField]
		public float markerRadius = 0.50f;
		[KSPField] // x,y, width, height
		public Vector4 headingBarPosition = new Vector4(0, 0.8f, 0.8f, 0.1f);
		[KSPField]
		public float headingSpan = 0.25f;
		private Texture2D horizonTex;
		private Material overlayMaterial;
		private Material headingMaterial;
		private Material gizmoMaterial;
		private NavBall stockNavBall;
		private GameObject ballPivot;
		private GameObject navBall;
		private GameObject cameraBody;
		private GameObject overlay;
		private GameObject heading;
		private GameObject markerPrograde;
		private Camera ballCamera;
		private const float markerPlane = 1.4f;

		public bool RenderPFD(RenderTexture screen)
		{
			if (screen == null)
				return false;
			GL.Clear(true, true, backgroundColor);

			ballCamera.targetTexture = screen;


			Vector3d coM = vessel.findWorldCenterOfMass();
			Vector3d up = (coM - vessel.mainBody.position).normalized;
			Vector3d north = Vector3d.Exclude(up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - coM).normalized;
			Quaternion rotationSurface = Quaternion.LookRotation(north, up);
			Quaternion rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);
			Vector3d velocityVesselOrbit = vessel.orbit.GetVel();
			Vector3d velocityVesselSurface = velocityVesselOrbit - vessel.mainBody.getRFrmVel(coM);
			Vector3d velocityVesselSurfaceUnit = velocityVesselSurface.normalized;
			Vector3d velocityVesselOrbitUnit = velocityVesselOrbit.normalized;


			ballPivot.transform.rotation = MirrorX(stockNavBall.navBall.rotation);
			heading.renderer.material.SetTextureOffset("_MainTex",
				new Vector2(
					Mathf.Lerp(0, 1, Mathf.InverseLerp(0, 360, rotationVesselSurface.eulerAngles.y)) - headingSpan / 2
					, 0)
			);

			Quaternion gymbal = stockNavBall.attitudeGymbal;

			markerPrograde.transform.position = FixMarkerPosition(velocityVesselSurfaceUnit, gymbal) * markerRadius;
			markerPrograde.renderer.sharedMaterial.color = new Color(1, 1, 1, (float)(markerPrograde.transform.position.z + 0.5));
			markerPrograde.transform.position = new Vector3(markerPrograde.transform.position.x, markerPrograde.transform.position.y, markerPlane);
			FaceCamera(markerPrograde);

			Show(cameraBody, navBall, overlay, heading);
			ballCamera.Render();
			Hide(cameraBody, navBall, overlay, heading);

			return true;
		}

		private static Vector3 FixMarkerPosition(Vector3 thatVector, Quaternion thatVoodoo)
		{
			Vector3 returnVector = thatVoodoo * thatVector;
			returnVector.x = -returnVector.x;
			return returnVector;
		}

		private static Quaternion MirrorX(Quaternion input)
		{
			// Witchcraft: It's called mirroring the X axis of the quaternion's conjugate.
			return new Quaternion(input.x, -input.y, -input.z, input.w);
		}
		/*
		public override void OnUpdate()
		{
		}
		*/
		public void Start()
		{
			Shader unlit = Shader.Find("KSP/Alpha/Unlit Transparent");
			overlayMaterial = new Material(unlit);
			overlayMaterial.mainTexture = GameDatabase.Instance.GetTexture(staticOverlay, false);
			headingMaterial = new Material(unlit);
			headingMaterial.mainTexture = GameDatabase.Instance.GetTexture(headingBar, false);

			horizonTex = GameDatabase.Instance.GetTexture(horizonTexture, false);
			navBall = GameDatabase.Instance.GetModel(navBallModel);

			// Cute!
			ManeuverGizmo maneuverGizmo = MapView.ManeuverNodePrefab.GetComponent<ManeuverGizmo>();
			ManeuverGizmoHandle maneuverGizmoHandle = maneuverGizmo.handleNormal;
			Transform gizmoTransform = maneuverGizmoHandle.flag;
			Renderer gizmoRenderer = gizmoTransform.renderer;
			gizmoMaterial = gizmoRenderer.sharedMaterial;

			// Ahaha, that's clever, does it work?
			stockNavBall = GameObject.Find("NavBall").GetComponent<NavBall>();
			// ...well, it does, but the result is bizarre,
			// apparently, because the stock BALL ITSELF IS MIRRORED.

			// Moving parts...
			ballPivot = new GameObject();
			ballPivot.layer = drawingLayer;
			ballPivot.transform.position = Vector3.zero;
			ballPivot.transform.rotation = Quaternion.identity;
			ballPivot.transform.localRotation = Quaternion.identity;

			navBall.name = "RPMNB" + navBall.GetInstanceID();
			navBall.layer = drawingLayer;
			navBall.transform.position = Vector3.zero;
			navBall.transform.rotation = Quaternion.identity;
			navBall.transform.localRotation = Quaternion.identity;
			navBall.transform.parent = ballPivot.transform;


			navBall.renderer.material.mainTexture = horizonTex;
			navBall.renderer.material.SetFloat("_Opacity", ballOpacity);

			markerPrograde = CreateSimplePlane("RPMPFDPrograde", markerScale, drawingLayer);
			markerPrograde.renderer.sharedMaterial = gizmoMaterial;
			markerPrograde.renderer.sharedMaterial.mainTextureScale = Vector2.one / 3f;
			markerPrograde.renderer.sharedMaterial.mainTextureOffset = new Vector2(0, 2f / 3f);
			markerPrograde.renderer.sharedMaterial.color = Color.white;
			markerPrograde.transform.position = Vector3.zero;
			markerPrograde.transform.parent = ballPivot.transform;


			// Non-moving parts...
			cameraBody = new GameObject();
			cameraBody.name = "RPMPFD" + cameraBody.GetInstanceID();
			cameraBody.layer = drawingLayer;
			ballCamera = cameraBody.AddComponent<Camera>();
			ballCamera.enabled = false;
			ballCamera.orthographic = true;
			ballCamera.clearFlags = CameraClearFlags.Nothing;
			ballCamera.aspect = screenAspect;
			ballCamera.orthographicSize = 1f;
			ballCamera.cullingMask = 1 << drawingLayer;
			// -2,0,0 seems to get the orientation exactly as the ship.
			// But logically, forward is Z+, right?
			// Which means that 
			ballCamera.transform.position = new Vector3(0, 0, 2);
			ballCamera.transform.LookAt(Vector3.zero, Vector3.up);

			overlay = CreateSimplePlane("RPMPFDOverlay", 1f, drawingLayer);
			overlay.layer = drawingLayer;
			overlay.transform.position = new Vector3(0, 0, 1.5f);
			overlay.renderer.material = overlayMaterial;
			overlay.transform.parent = cameraBody.transform;
			FaceCamera(overlay);

			heading = CreateSimplePlane("RPMPFDHeading", 1f, drawingLayer);
			heading.layer = drawingLayer;
			heading.transform.position = new Vector3(headingBarPosition.x, headingBarPosition.y, 1.45f);
			heading.transform.parent = cameraBody.transform;
			heading.transform.localScale = new Vector3(headingBarPosition.z, 0, headingBarPosition.w);
			heading.renderer.material = headingMaterial;
			heading.renderer.material.SetTextureScale("_MainTex", new Vector2(headingSpan, 1f));
			FaceCamera(heading);

			Hide(navBall, cameraBody, overlay, heading);
		}

		private static void FaceCamera(GameObject thatObject)
		{
			if (thatObject == null)
				throw new System.ArgumentNullException("thatObject");
			// This is known to rotate correctly, so I'll keep it around.
			/*
			Vector3 originalPosition = thatObject.transform.position;
			thatObject.transform.position = Vector3.zero;
			thatObject.transform.LookAt(Vector3.down,Vector3.back);
			thatObject.transform.position = originalPosition;
			*/
			thatObject.transform.rotation = Quaternion.Euler(new Vector3(90, 180, 0));
		}

		private static void Hide(params GameObject[] objects)
		{
			foreach (GameObject thatObject in objects) {
				thatObject.SetActive(false);
				if (thatObject.renderer != null)
					thatObject.renderer.enabled = false;
			}
		}

		private static void Show(params GameObject[] objects)
		{
			foreach (GameObject thatObject in objects) {
				thatObject.SetActive(true);
				if (thatObject.renderer != null)
					thatObject.renderer.enabled = true;
			}
		}
		// This function courtesy of EnhancedNavBall.
		private static GameObject CreateSimplePlane(
			string name,
			float vectorSize,
			int drawingLayer)
		{
			Mesh mesh = new Mesh();

			GameObject obj = new GameObject(name);
			MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
			obj.AddComponent<MeshRenderer>();

			const float uvize = 1f;

			Vector3 p0 = new Vector3(-vectorSize, 0, vectorSize);
			Vector3 p1 = new Vector3(vectorSize, 0, vectorSize);
			Vector3 p2 = new Vector3(-vectorSize, 0, -vectorSize);
			Vector3 p3 = new Vector3(vectorSize, 0, -vectorSize);

			mesh.vertices = new[] {
				p0, p1, p2,
				p1, p3, p2
			};

			mesh.triangles = new[] {
				0, 1, 2,
				3, 4, 5
			};

			Vector2 uv1 = new Vector2(0, 0);
			Vector2 uv2 = new Vector2(uvize, uvize);
			Vector2 uv3 = new Vector2(0, uvize);
			Vector2 uv4 = new Vector2(uvize, 0);

			mesh.uv = new[] {
				uv1, uv4, uv3,
				uv4, uv2, uv3
			};

			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			mesh.Optimize();

			meshFilter.mesh = mesh;

			obj.layer = drawingLayer;

			return obj;
		}
	}
}

