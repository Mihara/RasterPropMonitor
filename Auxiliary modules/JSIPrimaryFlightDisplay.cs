using UnityEngine;

namespace JSI
{
	public class JSIPrimaryFlightDisplay: InternalModule
	{
		[KSPField]
		public string horizonTexture;
		[KSPField]
		public string staticOverlay;

		private Material horizonMaterial;
		private Material overlayMaterial;

		public bool RenderPFD(RenderTexture screen)
		{
			if (screen == null)
				return false;
			GL.Clear(true, true, Color.blue);

			Vector3d coM = vessel.findWorldCenterOfMass();
			Vector3d up = (coM - vessel.mainBody.position).normalized;
			Vector3d forward = vessel.GetTransform().up;
			Vector3d right = vessel.GetTransform().right;
			Vector3d north = Vector3d.Exclude(up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - coM).normalized;
			Quaternion rotationSurface = Quaternion.LookRotation(north, up);
			Quaternion rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);
			// Orientation
			//case "HEADING":
			//return rotationVesselSurface.eulerAngles.y;
			//case "PITCH":
			//return (rotationVesselSurface.eulerAngles.x > 180) ? (360.0 - rotationVesselSurface.eulerAngles.x) : -rotationVesselSurface.eulerAngles.x;
			//case "ROLL":
			//return (rotationVesselSurface.eulerAngles.z > 180) ? (rotationVesselSurface.eulerAngles.z - 360.0) : rotationVesselSurface.eulerAngles.z;


			float heading = rotationVesselSurface.eulerAngles.y;
			GL.PushMatrix();
			GL.LoadOrtho();

			DrawHorizon(rotationVesselSurface.eulerAngles.z, rotationVesselSurface.eulerAngles.x);
			DrawOverlay();

			GL.PopMatrix();
			return true;
		}

		private void DrawOverlay() {
			overlayMaterial.SetPass(0);
			DrawQuad(Vector3.zero,new Vector3(0,1,0),new Vector3(1,1,0),new Vector3(1,0,0),0);
		}

		private static void DrawQuad(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, float yShift){
			GL.Begin(GL.QUADS);
			GL.Color(Color.white);
			// Examples seem to do it clockwise.
			GL.TexCoord2(0, 0 + yShift);
			GL.Vertex(bottomLeft);
			GL.TexCoord2(0, 1 + yShift);
			GL.Vertex(topLeft);
			GL.TexCoord2(1, 1 + yShift);
			GL.Vertex(topRight);
			GL.TexCoord2(1, 0 + yShift);
			GL.Vertex(bottomRight);
			GL.End();
		}

		// Boy, what a complicated way to do it.
		private void DrawHorizon(float rollAngle, float pitchAngle)
		{

			Vector3 bottomLeft = new Vector3(-0.25f,-0.25f,0);
			Vector3 topLeft = new Vector3(-0.25f, 1.25f, 0); 
			Vector3 topRight = new Vector3(1.25f, 1.25f, 0);
			Vector3 bottomRight = new Vector3(1.25f, -0.25f, 0);

			Vector3 center = new Vector3(0.5f, 0.5f, 0);
			Quaternion angleQuat = Quaternion.Euler(0, 0, -rollAngle);

			float pitchShift = Mathf.Lerp(1, 0, Mathf.InverseLerp(0, 360, pitchAngle));

			bottomLeft = RotateAroundPoint(bottomLeft, center, angleQuat);
			topLeft = RotateAroundPoint(topLeft, center, angleQuat);
			bottomRight = RotateAroundPoint(bottomRight, center, angleQuat);
			topRight = RotateAroundPoint(topRight, center, angleQuat);

			horizonMaterial.SetPass(0);
			DrawQuad(bottomLeft, topLeft, topRight, bottomRight, pitchShift);
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
			horizonMaterial = new Material(Shader.Find("Unlit/Transparent"));
			horizonMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(horizonTexture, false));
			overlayMaterial = new Material(Shader.Find("Unlit/Transparent"));
			overlayMaterial.SetTexture("_MainTex", GameDatabase.Instance.GetTexture(staticOverlay, false));
		}
	}
}

