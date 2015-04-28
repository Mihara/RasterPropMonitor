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
		public bool use360horizon = true;
		[KSPField] // Number of texels of the horizon texture to draw (width).
		public Vector2 horizonTextureSize = new Vector2(1f, 1f);
		[KSPField]
		public string headingBar = string.Empty;
		[KSPField] // x,y, width, height in pixels
		public Vector4 headingBarPosition = new Vector4(0f, 0f, 64f, 32f);
		[KSPField]
		public bool showHeadingBarPrograde = true;
		[KSPField]
		public float headingBarWidth = 64;
		[KSPField] // Texture to use
		public string vertBar1Texture = string.Empty;
		[KSPField] // Position and size of the bar, in pixels
		public Vector4 vertBar1Position = new Vector4(0f, 0f, 64f, 320f);
		[KSPField] // minimum and maximum values
		public Vector2 vertBar1Limit = new Vector2(0f, 10000f);
		[KSPField] // lower and upper bound of the texture, in pixels.  Defaults are useless...
		public Vector2 vertBar1TextureLimit = new Vector2(0.0f, 1.0f);
		[KSPField] // Number of texels to draw (vertically) for this bar.
		public float vertBar1TextureSize = 0.5f;
		[KSPField]
		public string vertBar1Variable = string.Empty;
		[KSPField]
		public bool vertBar1UseLog10 = true;
		[KSPField] // Texture to use
		public string vertBar2Texture = string.Empty;
		[KSPField] // Position and size of the bar, in pixels
		public Vector4 vertBar2Position = new Vector4(0f, 0f, 64f, 320f);
		[KSPField] // minimum and maximum values
		public Vector2 vertBar2Limit = new Vector2(-10000f, 10000f);
		[KSPField] // lower and upper bound of the texture, in pixels.  Defaults are useless...
		public Vector2 vertBar2TextureLimit = new Vector2(0.0f, 1.0f);
		[KSPField] // Amount of boundary on the texture (offset added to the current value's texture coordinate, to limit how much of the strip is visible)
		public float vertBar2TextureSize = 0.5f;
		[KSPField]
		public string vertBar2Variable = string.Empty;
		[KSPField]
		public bool vertBar2UseLog10 = true;
		[KSPField]
		public string staticOverlay = string.Empty;
		[KSPField]
		public string progradeColor = string.Empty;
		private Color progradeColorValue = new Color(0.84f, 0.98f, 0);
		[KSPField]
		public float iconPixelSize = 64f;

		private Material ladderMaterial;
		private Material headingMaterial;
		private Material overlayMaterial;
		private Material vertBar1Material;
		private Material vertBar2Material;
		private Material iconMaterial;
		private Texture2D gizmoTexture;
		private RasterPropMonitorComputer comp;
		private bool startupComplete;

		public bool RenderHUD(RenderTexture screen, float cameraAspect)
		{
			if (screen == null || !startupComplete || HighLogic.LoadedSceneIsEditor)
				return false;

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
			if (cameraObject != null) {
				cameraObject.Render();
			}

			// Configure the matrix so that the origin is the center of the screen.
			GL.PushMatrix();

			// Draw the HUD ladder
			// MOARdV note, 2014/03/19: swapping the y values, to invert the
			// coordinates so the prograde icon is right-side up.
			GL.LoadPixelMatrix(-horizonSize.x * 0.5f, horizonSize.x * 0.5f, horizonSize.y * 0.5f, -horizonSize.y * 0.5f);
			GL.Viewport(new Rect((screen.width - horizonSize.x) * 0.5f, (screen.height - horizonSize.y) * 0.5f, horizonSize.x, horizonSize.y));

			Vector3 coM = vessel.findWorldCenterOfMass();
			Vector3 up = comp.Up;
			Vector3 forward = vessel.GetTransform().up;
			Vector3 right = vessel.GetTransform().right;
			Vector3 top = Vector3.Cross(right, forward);
			Vector3 north = comp.North;

			Vector3d velocityVesselSurface = comp.VelocityVesselSurface;
			Vector3 velocityVesselSurfaceUnit = velocityVesselSurface.normalized;

			if (ladderMaterial) {
				// Figure out the texture coordinate scaling for the ladder.
				float ladderTextureOffset = horizonTextureSize.y / ladderMaterial.mainTexture.height;

				float cosUp = Vector3.Dot(forward, up);
				float cosRoll = Vector3.Dot(top, up);
				float sinRoll = Vector3.Dot(right, up);

				var normalizedRoll = new Vector2(cosRoll, sinRoll);
				normalizedRoll.Normalize();
				if (normalizedRoll.magnitude < 0.99f) {
					// If we're hitting +/- 90 nearly perfectly, the sin and cos will
					// be too far out of whack to normalize.  Arbitrarily pick
					// a roll of 0.0.
					normalizedRoll.x = 1.0f;
					normalizedRoll.y = 0.0f;
				}
				cosRoll = normalizedRoll.x;
				sinRoll = normalizedRoll.y;

				// Mihara: I'm pretty sure this was negative of what it should actually be, at least according to my mockup.
				float pitch = -(Mathf.Asin(cosUp) * Mathf.Rad2Deg);

				float ladderMidpointCoord;
				if (use360horizon) {
					// Straight up is texture coord 0.75;
					// Straight down is TC 0.25;
					ladderMidpointCoord = JUtil.DualLerp(0.25f, 0.75f, -90f, 90f, pitch);
				} else {
					// Straight up is texture coord 1.0;
					// Straight down is TC 0.0;
					ladderMidpointCoord = JUtil.DualLerp(0.0f, 1.0f, -90f, 90f, pitch);
				}

				ladderMaterial.SetPass(0);
				GL.Begin(GL.QUADS);

				// transform -x -y
				GL.TexCoord2(0.5f + horizonTextureSize.x, ladderMidpointCoord - ladderTextureOffset);
				GL.Vertex3(cosRoll * horizonSize.x + sinRoll * horizonSize.y, -sinRoll * horizonSize.x + cosRoll * horizonSize.y, 0.0f);

				// transform +x -y
				GL.TexCoord2(0.5f - horizonTextureSize.x, ladderMidpointCoord - ladderTextureOffset);
				GL.Vertex3(-cosRoll * horizonSize.x + sinRoll * horizonSize.y, sinRoll * horizonSize.x + cosRoll * horizonSize.y, 0.0f);

				// transform +x +y
				GL.TexCoord2(0.5f - horizonTextureSize.x, ladderMidpointCoord + ladderTextureOffset);
				GL.Vertex3(-cosRoll * horizonSize.x - sinRoll * horizonSize.y, sinRoll * horizonSize.x - cosRoll * horizonSize.y, 0.0f);

				// transform -x +y
				GL.TexCoord2(0.5f + horizonTextureSize.x, ladderMidpointCoord + ladderTextureOffset);
				GL.Vertex3(cosRoll * horizonSize.x - sinRoll * horizonSize.y, -sinRoll * horizonSize.x - cosRoll * horizonSize.y, 0.0f);
				GL.End();

				float AoA = velocityVesselSurfaceUnit.AngleInPlane(right, forward);
				float AoATC;
				if (use360horizon) {
					// Straight up is texture coord 0.75;
					// Straight down is TC 0.25;
					AoATC = JUtil.DualLerp(0.25f, 0.75f, -90f, 90f, pitch + AoA);
				} else {
					// Straight up is texture coord 1.0;
					// Straight down is TC 0.0;
					AoATC = JUtil.DualLerp(0.0f, 1.0f, -90f, 90f, pitch + AoA);
				}

				float Ypos = JUtil.DualLerp(
					             -horizonSize.y, horizonSize.y,
					             ladderMidpointCoord - ladderTextureOffset, ladderMidpointCoord + ladderTextureOffset, 
					             AoATC);

				// Placing the icon on the (0, Ypos) location, so simplify the transform.
				DrawIcon(-sinRoll * Ypos, -cosRoll * Ypos, GizmoIcons.GetIconLocation(GizmoIcons.IconType.PROGRADE), progradeColorValue);
			}

			// Draw the rest of the HUD stuff (0,0) is the top left corner of the screen.
			GL.LoadPixelMatrix(0, screen.width, screen.height, 0);
			GL.Viewport(new Rect(0, 0, screen.width, screen.height));

			if (headingMaterial != null) {
				Quaternion rotationSurface = Quaternion.LookRotation(north, up);
				Quaternion rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);
				float headingTexture = JUtil.DualLerp(0f, 1f, 0f, 360f, rotationVesselSurface.eulerAngles.y);
				float headingTextureOffset = (headingBarWidth / headingMaterial.mainTexture.width) / 2;

				headingMaterial.SetPass(0);
				GL.Begin(GL.QUADS);
				GL.TexCoord2(headingTexture - headingTextureOffset, 1.0f);
				GL.Vertex3(headingBarPosition.x, headingBarPosition.y, 0.0f);
				GL.TexCoord2(headingTexture + headingTextureOffset, 1.0f);
				GL.Vertex3(headingBarPosition.x + headingBarPosition.z, headingBarPosition.y, 0.0f);
				GL.TexCoord2(headingTexture + headingTextureOffset, 0.0f);
				GL.Vertex3(headingBarPosition.x + headingBarPosition.z, headingBarPosition.y + headingBarPosition.w, 0.0f);
				GL.TexCoord2(headingTexture - headingTextureOffset, 0.0f);
				GL.Vertex3(headingBarPosition.x, headingBarPosition.y + headingBarPosition.w, 0.0f);
				GL.End();

				if (showHeadingBarPrograde) {
					float slipAngle = velocityVesselSurfaceUnit.AngleInPlane(up, forward);
					float slipTC = JUtil.DualLerp(0f, 1f, 0f, 360f, rotationVesselSurface.eulerAngles.y + slipAngle);
					float slipIconX = JUtil.DualLerp(headingBarPosition.x, headingBarPosition.x + headingBarPosition.z, headingTexture - headingTextureOffset, headingTexture + headingTextureOffset, slipTC);
					DrawIcon(slipIconX, headingBarPosition.y + headingBarPosition.w * 0.5f, GizmoIcons.GetIconLocation(GizmoIcons.IconType.PROGRADE), progradeColorValue);
				}
			}

			if (vertBar1Material != null) {
				float value = comp.ProcessVariable(vertBar1Variable).MassageToFloat();
				if (float.IsNaN(value)) {
					value = 0.0f;
				}

				if (vertBar1UseLog10) {
					value = JUtil.PseudoLog10(value);
				}

				float vertBar1TexCoord = JUtil.DualLerp(vertBar1TextureLimit.x, vertBar1TextureLimit.y, vertBar1Limit.x, vertBar1Limit.y, value);

				vertBar1Material.SetPass(0);
				GL.Begin(GL.QUADS);
				GL.TexCoord2(0.0f, vertBar1TexCoord + vertBar1TextureSize);
				GL.Vertex3(vertBar1Position.x, vertBar1Position.y, 0.0f);
				GL.TexCoord2(1.0f, vertBar1TexCoord + vertBar1TextureSize);
				GL.Vertex3(vertBar1Position.x + vertBar1Position.z, vertBar1Position.y, 0.0f);
				GL.TexCoord2(1.0f, vertBar1TexCoord - vertBar1TextureSize);
				GL.Vertex3(vertBar1Position.x + vertBar1Position.z, vertBar1Position.y + vertBar1Position.w, 0.0f);
				GL.TexCoord2(0.0f, vertBar1TexCoord - vertBar1TextureSize);
				GL.Vertex3(vertBar1Position.x, vertBar1Position.y + vertBar1Position.w, 0.0f);
				GL.End();
			}

			if (vertBar2Material != null) {
				float value = comp.ProcessVariable(vertBar2Variable).MassageToFloat();
				if (float.IsNaN(value)) {
					value = 0.0f;
				}

				if (vertBar2UseLog10) {
					value = JUtil.PseudoLog10(value);
				}

				float vertBar2TexCoord = JUtil.DualLerp(vertBar2TextureLimit.x, vertBar2TextureLimit.y, vertBar2Limit.x, vertBar2Limit.y, value);

				vertBar2Material.SetPass(0);
				GL.Begin(GL.QUADS);
				GL.TexCoord2(0.0f, vertBar2TexCoord + vertBar2TextureSize);
				GL.Vertex3(vertBar2Position.x, vertBar2Position.y, 0.0f);
				GL.TexCoord2(1.0f, vertBar2TexCoord + vertBar2TextureSize);
				GL.Vertex3(vertBar2Position.x + vertBar2Position.z, vertBar2Position.y, 0.0f);
				GL.TexCoord2(1.0f, vertBar2TexCoord - vertBar2TextureSize);
				GL.Vertex3(vertBar2Position.x + vertBar2Position.z, vertBar2Position.y + vertBar2Position.w, 0.0f);
				GL.TexCoord2(0.0f, vertBar2TexCoord - vertBar2TextureSize);
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

			if (HighLogic.LoadedSceneIsEditor)
				return;
			try {
				backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);

				Shader unlit = Shader.Find("Hidden/Internal-GUITexture");
				ladderMaterial = new Material(unlit);
				ladderMaterial.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
				if (!String.IsNullOrEmpty(horizonTexture)) {
					ladderMaterial.mainTexture = GameDatabase.Instance.GetTexture(horizonTexture.EnforceSlashes(), false);
					if (ladderMaterial.mainTexture != null) {
						horizonTextureSize.x = horizonTextureSize.x / ladderMaterial.mainTexture.width;
						ladderMaterial.mainTexture.wrapMode = TextureWrapMode.Clamp;
					}
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
					vertBar1Material = new Material(unlit);
					vertBar1Material.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
					vertBar1Material.mainTexture = GameDatabase.Instance.GetTexture(vertBar1Texture.EnforceSlashes(), false);
					if (vertBar1Material.mainTexture != null) {
						float height = (float)vertBar1Material.mainTexture.height;
						vertBar1TextureLimit.x = 1.0f - (vertBar1TextureLimit.x / height);
						vertBar1TextureLimit.y = 1.0f - (vertBar1TextureLimit.y / height);
						vertBar1TextureSize = 0.5f * (vertBar1TextureSize / height);
						vertBar1Material.mainTexture.wrapMode = TextureWrapMode.Clamp;
					}
				}

				if (!String.IsNullOrEmpty(vertBar2Texture) && !String.IsNullOrEmpty(vertBar2Variable)) {
					vertBar2Material = new Material(unlit);
					vertBar2Material.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
					vertBar2Material.mainTexture = GameDatabase.Instance.GetTexture(vertBar2Texture.EnforceSlashes(), false);
					if (vertBar2Material.mainTexture != null) {
						float height = (float)vertBar2Material.mainTexture.height;
						vertBar2TextureLimit.x = 1.0f - (vertBar2TextureLimit.x / height);
						vertBar2TextureLimit.y = 1.0f - (vertBar2TextureLimit.y / height);
						vertBar2TextureSize = 0.5f * (vertBar2TextureSize / height);
						vertBar2Material.mainTexture.wrapMode = TextureWrapMode.Clamp;
					}
				}

				if (vertBar1UseLog10) {
					vertBar1Limit.x = JUtil.PseudoLog10(vertBar1Limit.x);
					vertBar1Limit.y = JUtil.PseudoLog10(vertBar1Limit.y);
				}

				if (vertBar2UseLog10) {
					vertBar2Limit.x = JUtil.PseudoLog10(vertBar2Limit.x);
					vertBar2Limit.y = JUtil.PseudoLog10(vertBar2Limit.y);
				}

				if (!string.IsNullOrEmpty(progradeColor)) {
					progradeColorValue = ConfigNode.ParseColor32(progradeColor);
				}

				comp = RasterPropMonitorComputer.Instantiate(internalProp);

				iconMaterial = new Material(unlit);
				iconMaterial.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
				gizmoTexture = JUtil.GetGizmoTexture();

				startupComplete = true;
			} catch {
				JUtil.AnnoyUser(this);
				throw;
			}
		}

		private void DrawIcon(float xPos, float yPos, Rect texCoord, Color iconColor)
		{
			var position = new Rect(xPos - iconPixelSize * 0.5f, yPos - iconPixelSize * 0.5f,
				               iconPixelSize, iconPixelSize);

			Graphics.DrawTexture(position, gizmoTexture, texCoord, 0, 0, 0, 0, iconColor, iconMaterial);
		}
	}
}
