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
		public bool ballIsEmissive;
		[KSPField]
		public Color backgroundColor = Color.black;
		[KSPField]
		public float ballOpacity = 0.8f;
		[KSPField]
		public Color ballColor = Color.white;
		[KSPField]
		public float markerScale = 0.1f;
		[KSPField] // x,y, width, height
		public Vector4 headingBarPosition = new Vector4(0, 0.8f, 0.8f, 0.1f);
		[KSPField]
		public float headingSpan = 0.25f;
		[KSPField]
		public bool headingAboveOverlay = false;
		[KSPField]
		public Color progradeColor = new Color(0.84f, 0.98f, 0);
		[KSPField]
		public Color maneuverColor = new Color(0, 0.1137f, 1);
		[KSPField]
		public Color targetColor = Color.magenta;
		[KSPField]
		public Color normalColor = new Color(0.930f, 0, 1);
		[KSPField]
		public Color radialColor = new Color(0, 1, 0.958f);
		[KSPField]
		public float cameraSpan = 1f;
		[KSPField]
		public Vector2 cameraShift = Vector2.zero;
		private Texture2D horizonTex;
		private Material overlayMaterial;
		private Material headingMaterial;
		private Texture2D gizmoTexture;
		private NavBall stockNavBall;
		private GameObject navBall;
		private GameObject cameraBody;
		private GameObject overlay;
		private GameObject heading;
		private Camera ballCamera;
		// Markers...
		private GameObject markerPrograde;
		private GameObject markerRetrograde;
		private GameObject markerManeuver;
		private GameObject markerManeuverMinus;
		private GameObject markerTarget;
		private GameObject markerTargetMinus;
		private GameObject markerNormal;
		private GameObject markerNormalMinus;
		private GameObject markerRadial;
		private GameObject markerRadialMinus;
		// This is honestly very badly written code, probably the worst of what I have in this project.
		// Much of it dictated by the fact that I barely, if at all, understand what am I doing in vector mathematics,
		// the rest is because the problem is all built out of special cases.
		// Sorry. :)
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
			Vector3d velocityVesselOrbitUnit = velocityVesselOrbit.normalized;
			Vector3d velocityVesselSurface = velocityVesselOrbit - vessel.mainBody.getRFrmVel(coM);
			Vector3d velocityVesselSurfaceUnit = velocityVesselSurface.normalized;
			Vector3d radialPlus = Vector3d.Exclude(velocityVesselOrbit, up).normalized;
			Vector3d normalPlus = -Vector3d.Cross(radialPlus, velocityVesselOrbitUnit);

			navBall.transform.rotation = MirrorX(stockNavBall.navBall.rotation);

			if (heading != null)
				heading.renderer.material.SetTextureOffset("_MainTex", new Vector2(Mathf.Lerp(0, 1, Mathf.InverseLerp(0, 360, rotationVesselSurface.eulerAngles.y)) - headingSpan / 2, 0));

			Quaternion gymbal = stockNavBall.attitudeGymbal;

			MoveMarker(markerPrograde, velocityVesselSurfaceUnit, progradeColor, gymbal);
			MoveMarker(markerRetrograde, -velocityVesselSurfaceUnit, progradeColor, gymbal);

			MoveMarker(markerNormal, normalPlus, normalColor, gymbal);
			MoveMarker(markerNormalMinus, -normalPlus, normalColor, gymbal);

			MoveMarker(markerRadial, radialPlus, radialColor, gymbal);
			MoveMarker(markerRadialMinus, -radialPlus, radialColor, gymbal);

			if (vessel.patchedConicSolver.maneuverNodes.Count > 0) {
				Vector3d burnVector = vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit);
				MoveMarker(markerManeuver, burnVector.normalized, maneuverColor, gymbal);
				MoveMarker(markerManeuverMinus, -burnVector.normalized, maneuverColor, gymbal);
				ShowHide(true, markerManeuver, markerManeuverMinus);
			}

			ITargetable target = FlightGlobals.fetch.VesselTarget;
			if (target != null) {
				Vector3 targetSeparation = (vessel.GetTransform().position - target.GetTransform().position).normalized;
				MoveMarker(markerTarget, targetSeparation, targetColor, gymbal);
				MoveMarker(markerTargetMinus, -targetSeparation, targetColor, gymbal);
				ShowHide(true, markerTarget, markerTargetMinus);
			}


			// This dirty hack reduces the chance that the ball might get affected by internal cabin lighting.
			int backupQuality = QualitySettings.pixelLightCount;
			QualitySettings.pixelLightCount = 0;

			ShowHide(true,
				cameraBody, navBall, overlay, heading, markerPrograde, markerRetrograde,
				markerNormal, markerNormalMinus, markerRadial, markerRadialMinus);
			ballCamera.Render();
			QualitySettings.pixelLightCount = backupQuality;
			ShowHide(false,
				cameraBody, navBall, overlay, heading, markerPrograde, markerRetrograde,
				markerManeuver, markerManeuverMinus, markerTarget, markerTargetMinus,
				markerNormal, markerNormalMinus, markerRadial, markerRadialMinus);

			return true;
		}

		private static void MoveMarker(GameObject marker, Vector3 position, Color nativeColor, Quaternion voodooGymbal)
		{
			const float markerRadius = 0.5f;
			const float markerPlane = 1.4f;
			marker.transform.position = FixMarkerPosition(position, voodooGymbal) * markerRadius;
			marker.renderer.material.color = new Color(nativeColor.r, nativeColor.g, nativeColor.b, (float)(marker.transform.position.z + 0.5));
			marker.transform.position = new Vector3(marker.transform.position.x, marker.transform.position.y, markerPlane);
			FaceCamera(marker);
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

		public GameObject BuildMarker(int iconX, int iconY, Color nativeColor)
		{

			GameObject marker = CreateSimplePlane("RPMPFDMarker" + iconX + iconY + internalProp.propID, markerScale, drawingLayer);
			marker.renderer.material = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
			marker.renderer.material.mainTexture = gizmoTexture;
			marker.renderer.material.mainTextureScale = Vector2.one / 3f;
			marker.renderer.material.mainTextureOffset = new Vector2(iconX * (1f / 3f), iconY * (1f / 3f));
			marker.renderer.material.color = nativeColor;
			marker.transform.position = Vector3.zero;
			ShowHide(false, marker);
			return marker;
		}

		public void Start()
		{
			Shader unlit = Shader.Find("KSP/Alpha/Unlit Transparent");
			overlayMaterial = new Material(unlit);
			overlayMaterial.mainTexture = GameDatabase.Instance.GetTexture(staticOverlay, false);

			if (!string.IsNullOrEmpty(headingBar)) {
				headingMaterial = new Material(unlit);
				headingMaterial.mainTexture = GameDatabase.Instance.GetTexture(headingBar, false);
			}
			horizonTex = GameDatabase.Instance.GetTexture(horizonTexture, false);
			navBall = GameDatabase.Instance.GetModel(navBallModel);

			// Cute!
			ManeuverGizmo maneuverGizmo = MapView.ManeuverNodePrefab.GetComponent<ManeuverGizmo>();
			ManeuverGizmoHandle maneuverGizmoHandle = maneuverGizmo.handleNormal;
			Transform gizmoTransform = maneuverGizmoHandle.flag;
			Renderer gizmoRenderer = gizmoTransform.renderer;
			gizmoTexture = (Texture2D)gizmoRenderer.sharedMaterial.mainTexture;

			// Ahaha, that's clever, does it work?
			stockNavBall = GameObject.Find("NavBall").GetComponent<NavBall>();
			// ...well, it does, but the result is bizarre,
			// apparently, because the stock BALL ITSELF IS MIRRORED.

			navBall.name = "RPMNB" + navBall.GetInstanceID();
			navBall.layer = drawingLayer;
			navBall.transform.position = Vector3.zero;
			navBall.transform.rotation = Quaternion.identity;
			navBall.transform.localRotation = Quaternion.identity;

			if (ballIsEmissive) {
				navBall.renderer.material.shader = Shader.Find("KSP/Emissive/Diffuse");
				navBall.renderer.material.SetTexture("_MainTex", horizonTex);
				navBall.renderer.material.SetTextureOffset("_Emissive", navBall.renderer.material.GetTextureOffset("_MainTex"));
				navBall.renderer.material.SetTexture("_Emissive", horizonTex);
				navBall.renderer.material.SetColor("_EmissiveColor", ballColor);
			} else {
				navBall.renderer.material.shader = Shader.Find("KSP/Unlit");
				navBall.renderer.material.mainTexture = horizonTex;
				navBall.renderer.material.color = ballColor;
			}
			navBall.renderer.material.SetFloat("_Opacity", ballOpacity);

			markerPrograde = BuildMarker(0, 2, progradeColor);
			markerRetrograde = BuildMarker(1, 2, progradeColor);
			markerManeuver = BuildMarker(2, 0, maneuverColor);
			markerManeuverMinus = BuildMarker(1, 2, maneuverColor);
			markerTarget = BuildMarker(2, 1, targetColor);
			markerTargetMinus = BuildMarker(2, 2, targetColor);
			markerNormal = BuildMarker(0, 0, normalColor);
			markerNormalMinus = BuildMarker(1, 0, normalColor);
			markerRadial = BuildMarker(0, 1, radialColor);
			markerRadialMinus = BuildMarker(1, 1, radialColor);

			// Non-moving parts...
			cameraBody = new GameObject();
			cameraBody.name = "RPMPFD" + cameraBody.GetInstanceID();
			cameraBody.layer = drawingLayer;
			ballCamera = cameraBody.AddComponent<Camera>();
			ballCamera.enabled = false;
			ballCamera.orthographic = true;
			ballCamera.clearFlags = CameraClearFlags.Nothing;
			ballCamera.eventMask = 0;
			ballCamera.farClipPlane = 3f;
			ballCamera.aspect = screenAspect;
			ballCamera.orthographicSize = cameraSpan;
			ballCamera.cullingMask = 1 << drawingLayer;
			ballCamera.clearFlags = CameraClearFlags.Depth;
			// -2,0,0 seems to get the orientation exactly as the ship.
			// But logically, forward is Z+, right?
			// Which means that 
			ballCamera.transform.position = new Vector3(0, 0, 2);
			ballCamera.transform.LookAt(Vector3.zero, Vector3.up);
			ballCamera.transform.position = new Vector3(cameraShift.x, cameraShift.y, 2);

			overlay = CreateSimplePlane("RPMPFDOverlay" + internalProp.propID, 1f, drawingLayer);
			overlay.layer = drawingLayer;
			overlay.transform.position = new Vector3(0, 0, 1.5f);
			overlay.renderer.material = overlayMaterial;
			overlay.transform.parent = cameraBody.transform;
			FaceCamera(overlay);

			if (headingMaterial != null) {
				heading = CreateSimplePlane("RPMPFDHeading" + internalProp.propID, 1f, drawingLayer);
				heading.layer = drawingLayer;
				heading.transform.position = new Vector3(headingBarPosition.x, headingBarPosition.y, headingAboveOverlay ? 1.55f : 1.45f);
				heading.transform.parent = cameraBody.transform;
				heading.transform.localScale = new Vector3(headingBarPosition.z, 0, headingBarPosition.w);
				heading.renderer.material = headingMaterial;
				heading.renderer.material.SetTextureScale("_MainTex", new Vector2(headingSpan, 1f));
				FaceCamera(heading);
			}

			ShowHide(false, navBall, cameraBody, overlay, heading);
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

		private static void ShowHide(bool status, params GameObject[] objects)
		{
			foreach (GameObject thatObject in objects) {
				if (thatObject != null) {
					thatObject.SetActive(status);
					if (thatObject.renderer != null)
						thatObject.renderer.enabled = status;
				}
			}
		}
		// This function courtesy of EnhancedNavBall.
		private static GameObject CreateSimplePlane(
			string name,
			float vectorSize,
			int drawingLayer)
		{
			var mesh = new Mesh();

			var obj = new GameObject(name);
			MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
			obj.AddComponent<MeshRenderer>();

			const float uvize = 1f;

			var p0 = new Vector3(-vectorSize, 0, vectorSize);
			var p1 = new Vector3(vectorSize, 0, vectorSize);
			var p2 = new Vector3(-vectorSize, 0, -vectorSize);
			var p3 = new Vector3(vectorSize, 0, -vectorSize);

			mesh.vertices = new[] {
				p0, p1, p2,
				p1, p3, p2
			};

			mesh.triangles = new[] {
				0, 1, 2,
				3, 4, 5
			};

			var uv1 = new Vector2(0, 0);
			var uv2 = new Vector2(uvize, uvize);
			var uv3 = new Vector2(0, uvize);
			var uv4 = new Vector2(uvize, 0);

			mesh.uv = new[] {
				uv1, uv4, uv3,
				uv4, uv2, uv3
			};

			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			mesh.Optimize();

			meshFilter.mesh = mesh;

			obj.layer = drawingLayer;

			Destroy(obj.collider);

			return obj;
		}
	}
}

