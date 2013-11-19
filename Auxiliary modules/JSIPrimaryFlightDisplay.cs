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
		private Texture2D horizonTex;
		private Material overlayMaterial;
		private Material headingMaterial;
		private NavBall stockNavBall;
		private GameObject navBall;
		private GameObject cameraBody;
		private GameObject overlay;
		private GameObject heading;
		private Camera ballCamera;
		private const float headingSpan = 0.25f;

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



			navBall.transform.rotation = MirrorX(stockNavBall.navBall.rotation);
			heading.renderer.material.SetTextureOffset("_MainTex",
				new Vector2(
					Mathf.Lerp(0, 1, Mathf.InverseLerp(0, 360, rotationVesselSurface.eulerAngles.y)) - headingSpan / 2
					, 0)
			);

			Show(cameraBody, navBall, overlay, heading);
			ballCamera.Render();
			Hide(cameraBody, navBall, overlay, heading);

			return true;
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
			overlayMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(staticOverlay, false));
			headingMaterial = new Material(unlit);
			headingMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(headingBar, false));

			horizonTex = GameDatabase.Instance.GetTexture(horizonTexture, false);
			navBall = GameDatabase.Instance.GetModel(navBallModel);

			// Ahaha, that's clever, does it work?
			stockNavBall = GameObject.Find("NavBall").GetComponent<NavBall>();
			// ...well, it does, but the result is bizarre,
			// apparently, because the stock BALL ITSELF IS MIRRORED.

			navBall.name = "RPMNB" + navBall.GetInstanceID();
			navBall.layer = drawingLayer;
			navBall.transform.position = new Vector3(0, 0, 0);
			navBall.transform.rotation = Quaternion.identity;
			navBall.transform.localRotation = Quaternion.identity;
			navBall.renderer.material.SetTexture("_MainTex", horizonTex);

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

			overlay = CreateSimplePlane("RPMPFDOverlay", 1f);
			overlay.layer = drawingLayer;
			// first turn, THEN move.
			overlay.transform.LookAt(Vector3.down,Vector3.back);
			overlay.transform.position = new Vector3(0, 0, 1.5f);
			overlay.renderer.material = overlayMaterial;
			overlay.transform.parent = cameraBody.transform;

			heading = CreateSimplePlane("RPMPFDHeading", 1f);
			heading.layer = drawingLayer;
			heading.transform.LookAt(Vector3.down,Vector3.back);
			heading.transform.position = new Vector3(0, 0.8f, 1.45f);
			heading.transform.parent = cameraBody.transform;
			heading.transform.localScale = new Vector3(0.8f, 0, 0.1f);
			heading.renderer.material = headingMaterial;
			heading.renderer.material.SetTextureScale("_MainTex", new Vector2(headingSpan, 1f));

			Hide(navBall, cameraBody, overlay, heading);
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
			float vectorSize)
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

			return obj;
		}
	}
}

