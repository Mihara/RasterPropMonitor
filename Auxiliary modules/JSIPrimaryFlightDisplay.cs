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

		private Texture2D horizonTex;
		private Material overlayMaterial;
		private Material headingMaterial;

		private NavBall stockNavBall;

		private GameObject navBall;
		private Camera ballCamera;

		public bool RenderPFD(RenderTexture screen)
		{
			if (screen == null)
				return false;
			GL.Clear(true, true, Color.blue);

			/*
			Vector3d coM = vessel.findWorldCenterOfMass();
			Vector3d up = (coM - vessel.mainBody.position).normalized;
			Vector3d north = Vector3d.Exclude(up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - coM).normalized;
			Quaternion rotationSurface = Quaternion.LookRotation(north, up);
			Quaternion rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);
			*/

			ballCamera.targetTexture = screen;
			navBall.SetActive(true);
			navBall.renderer.enabled = true;

			navBall.transform.rotation = MirrorX(stockNavBall.navBall.rotation);

			ballCamera.Render();
			navBall.renderer.enabled = false;
			navBall.SetActive(false);

			//GL.PushMatrix();
			//GL.LoadOrtho();
			//GL.PopMatrix();
			return true;

		}

		private static Quaternion MirrorX(Quaternion input){
			// Witchcraft: It's called mirroring the X axis of the quaternion's conjugate.
			return new Quaternion(input.x,-input.y,-input.z,input.w);
		}


		public override void OnUpdate()
		{
		}

		public void Start()
		{
			Shader unlit = Shader.Find("KSP/Alpha/Unlit Transparent");
			overlayMaterial = new Material(unlit);
			overlayMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(staticOverlay, false));
			headingMaterial = new Material(unlit);
			headingMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(headingBar, false));

			horizonTex = GameDatabase.Instance.GetTexture(horizonTexture, false);
			navBall = GameDatabase.Instance.GetModel(navBallModel);
			navBall.SetActive(false);
			navBall.name = "RPMNB"+navBall.GetInstanceID();
			navBall.layer = drawingLayer;
			navBall.transform.position = new Vector3(0,0,0);
			navBall.transform.rotation = Quaternion.identity;
			navBall.transform.localRotation = Quaternion.identity;
			navBall.renderer.material.SetTexture("_MainTex",horizonTex);
			// We need to get rid of that coded offset later.
			//navBall.renderer.material.SetTextureOffset("_MainTex",new Vector2(navBall.renderer.material.GetTextureOffset("_MainTex").x-0.25f,0));
			navBall.renderer.enabled = false;


			GameObject cameraBody = new GameObject();
			cameraBody.name = "RPMPFD"+cameraBody.GetInstanceID();
			cameraBody.layer = drawingLayer;
			ballCamera = cameraBody.AddComponent<Camera>();
			ballCamera.enabled = false;
			ballCamera.orthographic = true;
			ballCamera.aspect = screenAspect;
			ballCamera.orthographicSize = 0.7f;
			ballCamera.cullingMask = 1 << drawingLayer;
			// -2,0,0 seems to get the orientation exactly as the ship.
			// But logically, forward is Z+, right?
			// Which means that 
			ballCamera.transform.position = new Vector3(0, 0, 2);
			ballCamera.transform.LookAt(Vector3.zero,new Vector3(0,2,0));

			// Ahaha, that's clever, does it work?
			stockNavBall = GameObject.Find("NavBall").GetComponent<NavBall>();
			// ...well, it does, but the result is bizarre,
			// apparently, because the stock BALL ITSELF IS MIRRORED.


		}

	}
}

