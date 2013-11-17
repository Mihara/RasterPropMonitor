//#define EXAMPLE

#if EXAMPLE

using UnityEngine;

namespace JSI
{
	// Right now this is just an experiment to figure out how
	// to draw directly onto a RenderTexture when I need something more complex
	// than just lots of pasted pre-made pieces.
	// In the future, this will be the basic building block for
	// a Primary Flight Display plugin, I expect.
	public class TestBackgroundHandler: InternalModule
	{
		[KSPField]
		public string texture;
		private Texture2D testTexture;
		private Material expMat;
		private int angle;

		public bool RenderTest(RenderTexture screen)
		{
			if (screen == null)
				return false;
			GL.Clear(true, true, Color.blue);

			DrawSpinner(angle);
			return true;
		}
		// Boy, what a complicated way to do it.
		private void DrawSpinner(float rotAngle)
		{

			Vector3 bottomLeft = Vector3.zero;
			Vector3 topLeft = new Vector3(0f, 1f, 0); 
			Vector3 topRight = new Vector3(1f, 1f, 0);
			Vector3 bottomRight = new Vector3(1f, 0f, 0);

			Vector3 center = new Vector3(0.5f, 0.5f, 0);
			Quaternion angleQuat = Quaternion.Euler(0, 0, rotAngle);

			bottomLeft = RotateAroundPoint(bottomLeft, center, angleQuat);
			topLeft = RotateAroundPoint(topLeft, center, angleQuat);
			bottomRight = RotateAroundPoint(bottomRight, center, angleQuat);
			topRight = RotateAroundPoint(topRight, center, angleQuat);

			GL.PushMatrix();
			GL.LoadOrtho();
			expMat.SetPass(0);

			GL.Begin(GL.QUADS);
			GL.Color(Color.white);
			// Examples seem to do it clockwise.
			GL.TexCoord2(0, 0);
			GL.Vertex(bottomLeft);
			GL.TexCoord2(0, 1);
			GL.Vertex(topLeft);
			GL.TexCoord2(1, 1);
			GL.Vertex(topRight);
			GL.TexCoord2(1, 0);
			GL.Vertex(bottomRight);
			GL.End();

			GL.PopMatrix();

		}

		private static Vector3 RotateAroundPoint(Vector3 point, Vector3 pivot, Quaternion angle)
		{
			return angle * (point - pivot) + pivot;
		}

		public override void OnUpdate()
		{
			angle++;
			if (angle > 359)
				angle = 0;
		}

		public void Start()
		{
			testTexture = GameDatabase.Instance.GetTexture(texture, false);
			expMat = new Material(Shader.Find("KSP/Unlit")); // "KSP/Diffuse"?
			expMat.SetTexture("_MainTex", testTexture);
		}
	}
}

#endif