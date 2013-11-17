using UnityEngine;

namespace JSI
{
	public class JSIPrimaryFlightDisplay: InternalModule
	{
		[KSPField]
		public string horizonTexture;
		[KSPField]
		public string staticOverlay;
		[KSPField]
		public string headingBar;
		private Material horizonMaterial;
		private Material overlayMaterial;
		private Material headingMaterial;

		public bool RenderPFD(RenderTexture screen)
		{
			if (screen == null)
				return false;
			GL.Clear(true, true, Color.blue);

			Vector3d coM = vessel.findWorldCenterOfMass();
			Vector3d up = (coM - vessel.mainBody.position).normalized;
			Vector3d north = Vector3d.Exclude(up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - coM).normalized;
			Quaternion rotationSurface = Quaternion.LookRotation(north, up);
			Quaternion rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);

			GL.PushMatrix();
			GL.LoadOrtho();

			DrawHorizon(rotationVesselSurface.eulerAngles.z, rotationVesselSurface.eulerAngles.x);
			DrawHeadingBar(rotationVesselSurface.eulerAngles.y);
			DrawOverlay();

			GL.PopMatrix();
			return true;

		}

		// It all went low-level and downhill from there.

		private void DrawHeadingBar(float heading)
		{

			float xShift = Mathf.Lerp(0, 1, Mathf.InverseLerp(0, 360, (float)JUtil.ClampDegrees360(heading)));
			const float span = 0.25f;
			xShift -= span / 2f;
			Vector3 bottomLeft = new Vector3(0.2f, 0.8f);
			Vector3 topLeft = new Vector3(0.2f, 0.9f);
			Vector3 bottomRight = new Vector3(0.8f, 0.8f);
			Vector3 topRight = new Vector3(0.8f, 0.9f);

			headingMaterial.SetPass(0);
			GL.Begin(GL.QUADS);
			GL.Color(Color.white);
			// Examples seem to do it clockwise.
			GL.TexCoord2(0 + xShift, 0);
			GL.Vertex(bottomLeft);
			GL.TexCoord2(0 + xShift, 1);
			GL.Vertex(topLeft);
			GL.TexCoord2(span + xShift, 1);
			GL.Vertex(topRight);
			GL.TexCoord2(span + xShift, 0);
			GL.Vertex(bottomRight);
			GL.End();
		}

		private void DrawOverlay()
		{
			overlayMaterial.SetPass(0);
			DrawQuad(new Vector3(0.25f, 0.25f), new Vector3(0.25f, 0.75f), new Vector3(0.75f, 0.75f), new Vector3(0.75f, 0.25f), 0, 0);
		}

		private static void DrawQuad(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, float xShift, float yShift)
		{
			GL.Begin(GL.QUADS);
			GL.Color(Color.white);
			// Examples seem to do it clockwise.
			GL.TexCoord2(0 + xShift, 0 + yShift);
			GL.Vertex(bottomLeft);
			GL.TexCoord2(0 + xShift, 1 + yShift);
			GL.Vertex(topLeft);
			GL.TexCoord2(1 + xShift, 1 + yShift);
			GL.Vertex(topRight);
			GL.TexCoord2(1 + xShift, 0 + yShift);
			GL.Vertex(bottomRight);
			GL.End();
		}

		private void DrawHorizon(float rollAngle, float pitchAngle)
		{
			Vector3[] corners = {
				new Vector3(-0.5f, -0.5f),
				new Vector3(-0.5f, 1.5f),
				new Vector3(1.5f, 1.5f),
				new Vector3(1.5f, -0.5f)
			};

			Vector3 center = new Vector3(0.5f, 0.5f);
			Quaternion angleQuat = Quaternion.Euler(0, 0, -rollAngle);
			for (int i = 0; i < corners.Length; i++)
				corners[i] = RotateAroundPoint(corners[i], center, angleQuat);

			horizonMaterial.SetPass(0);
			DrawQuad(corners[0], corners[1], corners[2], corners[3],
				0,
				Mathf.Lerp(1, 0, Mathf.InverseLerp(0, 360, pitchAngle))
			);
		}

		private static Vector3 RotateAroundPoint(Vector3 point, Vector3 pivot, Quaternion angle)
		{
			return angle * (point - pivot) + pivot;
		}

		public override void OnUpdate()
		{
		}

		public void Start()
		{
			Shader unlit = Shader.Find("Unlit/Transparent");
			horizonMaterial = new Material(unlit);
			horizonMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(horizonTexture, false));
			overlayMaterial = new Material(unlit);
			overlayMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(staticOverlay, false));
			headingMaterial = new Material(unlit);
			headingMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(headingBar, false));
		}
	}
}

