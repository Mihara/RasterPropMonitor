using System;
using UnityEngine;

namespace JSI
{
	/*************************************************************************
	 * JSIHeadsUpDisplay provides an alternative to the Primary Flight Display
	 * for use in aircraft.  Instead of a spherical nav ball, pitch and roll
	 * are displayed with a "ladder" (texture).  Strips also provide heading
	 * information.
	 * As an experiment, I'll see if we can handle drawing a camera view
	 * beneath the HUD, too.
	 ************************************************************************/
	class JSIHeadsUpDisplay:InternalModule
	{
		[KSPField]
		public string cameraTransform = string.Empty;
		private FlyingCamera cameraObject;
		[KSPField]
		public string backgroundColor = "0,0,0,0";
		private Color32 backgroundColorValue;
		// MOARdV: Assuming (for now) the camera will not zoom.  FoV is needed
		// for both the camera render, and to know how to scale the HUD ladder.
		[KSPField]
		public float hudFov = 60.0f;
		[KSPField]
		public Vector2 ladderSize = new Vector2( 64, 32 );
		[KSPField]
		public string ladderTexture = string.Empty;
		private Material ladderMaterial;

		private bool startupComplete = false;

		public bool RenderHUD(RenderTexture screen, float cameraAspect)
		{
			if (screen == null)
				return false;

			if (!startupComplete)
				JUtil.AnnoyUser(this);

			// Clear the background, if configured.
			if (backgroundColorValue.a > 0) {
				GL.Clear(true, true, backgroundColorValue);
			}

			// Configure the camera, if configured.
			// MOARdV: Might be worthwhile to refactor the flying camera so
			// it is created in Start (like new FlyingCamera(part, cameraTransform)),
			// and pass the screen, FoV, and aspect ratio (or just screen and
			// FoV) as Render parameters, so there's no need to test if the
			// camera's been created every render call.
			if (cameraObject == null && !string.IsNullOrEmpty(cameraTransform)) {
				cameraObject = new FlyingCamera(part, screen, cameraAspect);
				cameraObject.PointCamera(cameraTransform, hudFov);
			}

			// Draw the camera's view, if configured.
			if (cameraObject != null && !cameraObject.Render()) {
				return false;
			}

			// Configure the matrix so that the origin is the center of the screen.
			GL.PushMatrix();

			// Draw the HUD ladder
			GL.LoadPixelMatrix(-ladderSize.x, ladderSize.x, -ladderSize.y, ladderSize.y);
			GL.Viewport(new Rect(screen.width / 2 - ladderSize.x, screen.height / 2 - ladderSize.y, 2.0f * ladderSize.x, 2.0f * ladderSize.y));

			Vector3 coM = vessel.findWorldCenterOfMass();
			Vector3 up = (coM - vessel.mainBody.position).normalized;
			Vector3 forward = vessel.GetTransform().up;
			Vector3 right = vessel.GetTransform().right;
			Vector3 top = Vector3.Cross(right, forward);

			float cosUp = Vector3.Dot(forward, up);
			float cosRoll = Vector3.Dot(top, up);
			float sinRoll = Vector3.Dot(right, up);

			ladderMaterial.SetPass(0);
			GL.Begin(GL.QUADS);
			// transform -x -y
			GL.TexCoord2(0.0f, 1.0f);
			GL.Vertex3(cosRoll * ladderSize.x + sinRoll * ladderSize.y, sinRoll * ladderSize.x - cosRoll * ladderSize.y, 0.0f);
			// transform +x -y
			GL.TexCoord2(1.0f, 1.0f);
			GL.Vertex3(-cosRoll * ladderSize.x + sinRoll * ladderSize.y, -sinRoll * ladderSize.x - cosRoll * ladderSize.y, 0.0f);
			// transform +x +y
			GL.TexCoord2(1.0f, 0.0f);
			GL.Vertex3(-cosRoll * ladderSize.x - sinRoll * ladderSize.y, -sinRoll * ladderSize.x + cosRoll * ladderSize.y, 0.0f);
			// transform -x +y
			GL.TexCoord2(0.0f, 0.0f);
			GL.Vertex3(cosRoll * ladderSize.x - sinRoll * ladderSize.y, sinRoll * ladderSize.x + cosRoll * ladderSize.y, 0.0f);
			GL.End();

			// Draw the rest of the HUD stuff (0,0) is the center of the screen, right handed coordinate system.
			GL.LoadPixelMatrix(-screen.width / 2, screen.width / 2, -screen.height / 2, screen.height / 2);
			GL.Viewport(new Rect(0, 0, screen.width, screen.height));

			GL.PopMatrix();

			return true;
		}

		public void Start()
		{
			backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);
			startupComplete = true;

			ladderMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
			ladderMaterial.color = new Color(0.0f, 1.0f, 0.0f, 0.75f);
			if (!String.IsNullOrEmpty(ladderTexture)) {
				ladderMaterial.mainTexture = GameDatabase.Instance.GetTexture(ladderTexture.EnforceSlashes(), false);
			}

			// We rescale these values by 1/2 because we always work
			// with 1/2 the values.  No sense remultiplying them every single
			// frame.
			ladderSize.x *= 0.5f;
			ladderSize.y *= 0.5f;
		}
	}
}
