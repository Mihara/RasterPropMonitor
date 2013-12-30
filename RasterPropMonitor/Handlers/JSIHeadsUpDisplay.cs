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
		public Vector2 horizonSize = new Vector2(64.0f, 32.0f);
		[KSPField]
		public string horizonTexture = string.Empty;
		[KSPField]
		public bool use360horizon = false;

		[KSPField]
		public string headingBar = string.Empty;
		[KSPField] // x,y, width, height in pixels
		public Vector4 headingBarPosition = new Vector4(0f, 0f, 64f, 32f);

		[KSPField] // Texture to use
		public string vertBar1Texture = string.Empty;
		[KSPField] // Position and size of the bar, in pixels
		public Vector4 vertBar1Position = new Vector4(0f, 0f, 64f, 320f);
		[KSPField] // minimum and maximum values
		public Vector2 vertBar1Limit = new Vector2(0f, 10000f);
		[KSPField] // lower and upper bound of the texture, in texture coordinates
		public Vector2 vertBar1TextureLimit = new Vector2(0.0f, 1.0f);
		[KSPField] // Amount of boundary on the texture (offset added to the current value's texture coordinate, to limit how much of the strip is visible)
		public float vertBar1TextureBoundary = 0.25f;
		[KSPField]
		public string vertBar1Variable = string.Empty;

		[KSPField] // Texture to use
		public string vertBar2Texture = string.Empty;
		[KSPField] // Position and size of the bar, in pixels
		public Vector4 vertBar2Position = new Vector4(0f, 0f, 64f, 320f);
		[KSPField] // minimum and maximum values
		public Vector2 vertBar2Limit = new Vector2(-10000f, 10000f);
		[KSPField] // lower and upper bound of the texture, in texture coordinates
		public Vector2 vertBar2TextureLimit = new Vector2(0.0f, 1.0f);
		[KSPField] // Amount of boundary on the texture (offset added to the current value's texture coordinate, to limit how much of the strip is visible)
		public float vertBar2TextureBoundary = 0.25f;
		[KSPField]
		public string vertBar2Variable = string.Empty;

		[KSPField]
		public string staticOverlay = string.Empty;

		private Material ladderMaterial = null;
		private Material headingMaterial = null;
		private Material overlayMaterial = null;
		private Material altBarMaterial = null;
		private Material vertBar2Material = null;
		private RasterPropMonitorComputer comp;

		private bool startupComplete = false;

		public bool RenderHUD(RenderTexture screen, float cameraAspect)
		{
			if (screen == null)
				return false;

			if (!startupComplete)
				JUtil.AnnoyUser(this);

			// Clear the background, if configured.
			GL.Clear(true, true, backgroundColorValue);

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

			// Figure out the texture coordinate scaling for the ladder.
			float ladderTextureOffset;
			float ladderHeightRatio = horizonSize.y / screen.height;
			float ladderHalfHeightDegrees = hudFov * 0.5f * ladderHeightRatio;
			if (use360horizon) {
				ladderTextureOffset = ladderHalfHeightDegrees / 180.0f;
			} else {
				ladderTextureOffset = ladderHalfHeightDegrees / 90.0f;
			}

			// Configure the matrix so that the origin is the center of the screen.
			GL.PushMatrix();

			// Draw the HUD ladder
			GL.LoadPixelMatrix(-horizonSize.x * 0.5f, horizonSize.x * 0.5f, -horizonSize.y * 0.5f, horizonSize.y * 0.5f);
			GL.Viewport(new Rect((screen.width - horizonSize.x) * 0.5f, (screen.height - horizonSize.y) * 0.5f, horizonSize.x, horizonSize.y));

			Vector3 coM = vessel.findWorldCenterOfMass();
			Vector3 up = (coM - vessel.mainBody.position).normalized;
			Vector3 forward = vessel.GetTransform().up;
			Vector3 right = vessel.GetTransform().right;
			Vector3 top = Vector3.Cross(right, forward);
			Vector3 north = Vector3.Exclude(up, (vessel.mainBody.position + (Vector3d)vessel.mainBody.transform.up * vessel.mainBody.Radius) - coM).normalized;

			if (ladderMaterial) {
				float cosUp = Vector3.Dot(forward, up);
				float cosRoll = Vector3.Dot(top, up);
				float sinRoll = Vector3.Dot(right, up);

				var normalizedRoll = new Vector2(cosRoll, sinRoll);
				normalizedRoll.Normalize();
				cosRoll = normalizedRoll.x;
				sinRoll = normalizedRoll.y;

				float pitch = Mathf.Asin(cosUp) * Mathf.Rad2Deg;

				float ladderMidpointCoord;
				if (use360horizon) {
					// Straight up is texture coord 0.75;
					// Straight down is TC 0.25;
					ladderMidpointCoord = JUtil.DualLerp(0.25f, 0.75f, -90f, 90f, pitch);
				}
				else {
					// Straight up is texture coord 1.0;
					// Straight down is TC 0.0;
					ladderMidpointCoord = JUtil.DualLerp(0.0f, 1.0f, -90f, 90f, pitch);
				}

				ladderMaterial.SetPass(0);
				GL.Begin(GL.QUADS);

				// transform -x -y
				GL.TexCoord2(0, ladderMidpointCoord + ladderTextureOffset);
				GL.Vertex3(cosRoll * horizonSize.x + sinRoll * horizonSize.y, sinRoll * horizonSize.x - cosRoll * horizonSize.y, 0.0f);

				// transform +x -y
				GL.TexCoord2(1.0f, ladderMidpointCoord + ladderTextureOffset);
				GL.Vertex3(-cosRoll * horizonSize.x + sinRoll * horizonSize.y, -sinRoll * horizonSize.x - cosRoll * horizonSize.y, 0.0f);

				// transform +x +y
				GL.TexCoord2(1.0f, ladderMidpointCoord - ladderTextureOffset);
				GL.Vertex3(-cosRoll * horizonSize.x - sinRoll * horizonSize.y, -sinRoll * horizonSize.x + cosRoll * horizonSize.y, 0.0f);

				// transform -x +y
				GL.TexCoord2(0.0f, ladderMidpointCoord - ladderTextureOffset);
				GL.Vertex3(cosRoll * horizonSize.x - sinRoll * horizonSize.y, sinRoll * horizonSize.x + cosRoll * horizonSize.y, 0.0f);
				GL.End();
			}

			// Draw the rest of the HUD stuff (0,0) is the top left corner of the screen.
			GL.LoadPixelMatrix(0, screen.width, screen.height , 0);
			GL.Viewport(new Rect(0, 0, screen.width, screen.height));

			if (headingMaterial != null) {
				Quaternion rotationSurface = Quaternion.LookRotation(north, up);
				Quaternion rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);
				float headingTexture = JUtil.DualLerp(0f, 1f, 0f, 360f, rotationVesselSurface.eulerAngles.y);
				// MOARdV: While we can use the comp to get these values, the
				// HUD update stutters if the computation refresh rate is too
				// low.  We can switch this back with a caveat to implementers
				// that they must keep the refresh rate high for smooth
				// performance.
				//float heading = comp.ProcessVariable("HEADING").MassageToFloat();
				//float headingTexture = JUtil.DualLerp(0f, 1f, 0f, 360f, heading);

				float headingTextureOffset;
				float headingHeightRatio = headingBarPosition.z / screen.width;
				float headingHalfHeightDegrees = hudFov * 0.5f * headingHeightRatio;
				headingTextureOffset = headingHalfHeightDegrees / 180.0f;

				headingMaterial.SetPass(0);
				GL.Begin(GL.QUADS);
				GL.TexCoord2(headingTexture - headingTextureOffset, 1.0f);
				GL.Vertex3(headingBarPosition.x, headingBarPosition.y,  0.0f);
				GL.TexCoord2(headingTexture + headingTextureOffset, 1.0f);
				GL.Vertex3(headingBarPosition.x + headingBarPosition.z, headingBarPosition.y, 0.0f);
				GL.TexCoord2(headingTexture + headingTextureOffset, 0.0f);
				GL.Vertex3(headingBarPosition.x + headingBarPosition.z, headingBarPosition.y + headingBarPosition.w, 0.0f);
				GL.TexCoord2(headingTexture - headingTextureOffset, 0.0f);
				GL.Vertex3(headingBarPosition.x, headingBarPosition.y + headingBarPosition.w, 0.0f);
				GL.End();
			}

			if (altBarMaterial != null) {
				float value = comp.ProcessVariable(vertBar1Variable).MassageToFloat();
				if (float.IsNaN(value)) {
					value = 0.0f;
				}

				float vertBar1TexCoord = JUtil.DualLerp(vertBar1TextureLimit.x, vertBar1TextureLimit.y, vertBar1Limit.x, vertBar1Limit.y, value);

				altBarMaterial.SetPass(0);
				GL.Begin(GL.QUADS);
				GL.TexCoord2(0.0f, vertBar1TexCoord + vertBar1TextureBoundary);
				GL.Vertex3(vertBar1Position.x, vertBar1Position.y, 0.0f);
				GL.TexCoord2(1.0f, vertBar1TexCoord + vertBar1TextureBoundary);
				GL.Vertex3(vertBar1Position.x + vertBar1Position.z, vertBar1Position.y, 0.0f);
				GL.TexCoord2(1.0f, vertBar1TexCoord - vertBar1TextureBoundary);
				GL.Vertex3(vertBar1Position.x + vertBar1Position.z, vertBar1Position.y + vertBar1Position.w, 0.0f);
				GL.TexCoord2(0.0f, vertBar1TexCoord - vertBar1TextureBoundary);
				GL.Vertex3(vertBar1Position.x, vertBar1Position.y + vertBar1Position.w, 0.0f);
				GL.End();
			}

			if (vertBar2Material != null) {
				float value = comp.ProcessVariable(vertBar2Variable).MassageToFloat();
				if (float.IsNaN(value)) {
					value = 0.0f;
				}

				float vertBar2TexCoord = JUtil.DualLerp(vertBar2TextureLimit.x, vertBar2TextureLimit.y, vertBar2Limit.x, vertBar2Limit.y, value);

				vertBar2Material.SetPass(0);
				GL.Begin(GL.QUADS);
				GL.TexCoord2(0.0f, vertBar2TexCoord + vertBar2TextureBoundary);
				GL.Vertex3(vertBar2Position.x, vertBar2Position.y, 0.0f);
				GL.TexCoord2(1.0f, vertBar2TexCoord + vertBar2TextureBoundary);
				GL.Vertex3(vertBar2Position.x + vertBar2Position.z, vertBar2Position.y, 0.0f);
				GL.TexCoord2(1.0f, vertBar2TexCoord - vertBar2TextureBoundary);
				GL.Vertex3(vertBar2Position.x + vertBar2Position.z, vertBar2Position.y + vertBar2Position.w, 0.0f);
				GL.TexCoord2(0.0f, vertBar2TexCoord - vertBar2TextureBoundary);
				GL.Vertex3(vertBar2Position.x, vertBar2Position.y + vertBar2Position.w, 0.0f);
				GL.End();
			}

			if (overlayMaterial != null) {
				overlayMaterial.SetPass(0);
				GL.Begin(GL.QUADS);
				GL.TexCoord2(0.0f, 1.0f);
				GL.Vertex3(0.0f, 0.0f, 0.0f);
				GL.TexCoord2(1.0f, 1.0f);
				GL.Vertex3(screen.width, 0.0f, 0.0f);
				GL.TexCoord2(1.0f, 0.0f);
				GL.Vertex3(screen.width, screen.height, 0.0f);
				GL.TexCoord2(0.0f, 0.0f);
				GL.Vertex3(0.0f, screen.height, 0.0f);
				GL.End();
			}

			GL.PopMatrix();

			return true;
		}

		public void Start()
		{
			backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);

			// MOARdV: Not sure this is the right one to use - I see some
			// lighting artifacts, like interior lights are affecting the
			// stuff I'm rendering.
			Shader unlit = Shader.Find("KSP/Alpha/Unlit Transparent");
			ladderMaterial = new Material(unlit);
			ladderMaterial.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
			if (!String.IsNullOrEmpty(horizonTexture)) {
				ladderMaterial.mainTexture = GameDatabase.Instance.GetTexture(horizonTexture.EnforceSlashes(), false);
			}

			if (!String.IsNullOrEmpty(headingBar)) {
				headingMaterial = new Material(unlit);
				headingMaterial.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
				headingMaterial.mainTexture = GameDatabase.Instance.GetTexture(headingBar.EnforceSlashes(), false);
			}

			if (!String.IsNullOrEmpty(staticOverlay)) {
				overlayMaterial = new Material(unlit);
				overlayMaterial.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
				overlayMaterial.mainTexture = GameDatabase.Instance.GetTexture(staticOverlay.EnforceSlashes(), false);
			}

			if (!String.IsNullOrEmpty(vertBar1Texture) && !String.IsNullOrEmpty(vertBar1Variable)) {
				altBarMaterial = new Material(unlit);
				altBarMaterial.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
				altBarMaterial.mainTexture = GameDatabase.Instance.GetTexture(vertBar1Texture.EnforceSlashes(), false);
			}

			if (!String.IsNullOrEmpty(vertBar2Texture) && !String.IsNullOrEmpty(vertBar2Variable)) {
				vertBar2Material = new Material(unlit);
				vertBar2Material.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
				vertBar2Material.mainTexture = GameDatabase.Instance.GetTexture(vertBar2Texture.EnforceSlashes(), false);
			}

			comp = RasterPropMonitorComputer.Instantiate(internalProp);

			startupComplete = true;
		}
	}
}
