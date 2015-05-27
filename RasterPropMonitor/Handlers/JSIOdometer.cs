using UnityEngine;
using System;
using System.Collections.Generic;

namespace JSI
{
	/**
	 * JSIOdometer: provides a stripped-down RasterPropMonitor class whose sole
	 * job is to render a numeric value (altitude, speed, climb rate) using an
	 * analog numeric display (like the odometer of a car).
	 */
	public class JSIOdometer : InternalModule
	{
		[KSPField]
		public string screenTransform = "screenTransform";
		[KSPField]
		public string textureLayerID = "_MainTex";
		[KSPField]
		public int screenPixelWidth = 512;
		[KSPField]
		public int screenPixelHeight = 256;
		[KSPField]
		public int refreshDrawRate = 6;
		[KSPField]
		public string variable = string.Empty;
		[KSPField]
		public string altVariable = string.Empty;
		[KSPField]
		public string perPodPersistenceName = string.Empty;
		[KSPField]
		public string digitTexture = string.Empty;
		[KSPField]
		public string characterTexture = string.Empty;
		[KSPField]
		public string overlayTexture = string.Empty;
		[KSPField]
		public Vector2 characterSize = new Vector2(16.0f, 32.0f);
		[KSPField]
		public Vector2 displayPosition = new Vector2(0.0f, 0.0f);
		[KSPField]
		public string backgroundColor = "0,0,0,0";
		private Color32 backgroundColorValue;
		[KSPField]
		public string odometerMode = "LINEAR";
		private OdometerMode oMode = OdometerMode.LINEAR;
		[KSPField]
		public float odometerRotationScalar = 10.0f;

		private float[] goalCoordinate = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
		private float[] currentCoordinate = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
		private float signGoalCoord = 0.75f;
		private float signCurrentCoord = 0.75f;
		private float prefixGoalCoord = 0.125f;
		private float prefixCurrentCoord = 0.125f;
		private Material digitMaterial;
		private Texture digitTex;
		private Texture characterTex;
		private Texture overlayTex;
		private RasterPropMonitorComputer comp;
		private double lastUpdate;
		private RenderTexture screenTexture;
		private Material screenMat;
		private int refreshDrawCountdown;
		private bool startupComplete;

		private readonly Dictionary<string, OdometerMode> modeList = new Dictionary<string, OdometerMode> {
			{ "LINEAR", OdometerMode.LINEAR },
			{ "SI", OdometerMode.SI },
			{ "TIME_HHHMMSS", OdometerMode.TIME_HHHMMSS },
		};

		private enum OdometerMode
		{
			LINEAR,
			SI,
			TIME_HHHMMSS,
		}

		private void UpdateOdometer()
		{
			double thisUpdate = Planetarium.GetUniversalTime();
			float dT = (float)(thisUpdate - lastUpdate) * odometerRotationScalar;

			float value;
			if (!string.IsNullOrEmpty(perPodPersistenceName)) {
				bool state = comp.Persistence.GetBool(perPodPersistenceName, false);
                value = comp.ProcessVariable((state) ? altVariable : variable, internalProp.propID).MassageToFloat();
			} else {
				value = comp.ProcessVariable(variable, internalProp.propID).MassageToFloat();
			}
			// Make sure the value isn't going to be a problem.
			if (float.IsNaN(value)) {
				value = 0.0f;
			}

			if (value < 0.0f) {
				signGoalCoord = 0.625f;
			} else if (value > 0.0f) {
				signGoalCoord = 0.875f;
			} else {
				signGoalCoord = 0.75f;
			}

			signCurrentCoord = JUtil.DualLerp(signCurrentCoord, signGoalCoord, 0.0f, 1.0f, dT);

			value = Mathf.Abs(value);

			if (oMode == OdometerMode.SI) {
				float leadingDigitExponent;
				if (value < 0.001f) {
					leadingDigitExponent = -3.0f;
				} else {
					leadingDigitExponent = Mathf.Floor(Mathf.Log10(value));
				}

				// siExponent is the location relative to the original decimal of
				// the SI prefix.  Is is always the greatest multiple-of-3 less
				// than the leadingDigitExponent.
				int siIndex = (int)Mathf.Floor(leadingDigitExponent / 3.0f);
				if (siIndex > 3) {
					siIndex = 3;
				}
				int siExponent = siIndex * 3;

				prefixGoalCoord = (float)(siIndex + 1) * 0.125f;
				prefixCurrentCoord = JUtil.DualLerp(prefixCurrentCoord, prefixGoalCoord, 0.0f, 1.0f, dT);

				float scaledValue = value / Mathf.Pow(10.0f, (float)(siExponent - 3));
				int intValue = (int)(scaledValue);

				for (int i = 5; i >= 0; --i) {
					float thisCoord = (float)(intValue % 10) / 10.0f;
					if (i == 5) {
						// So we can display fractional values:
						// However, we also quantize it to make it easier to
						// read during the transition from 9 to 0.
						thisCoord = Mathf.Floor((scaledValue % 10.0f) * 2.0f) / 20.0f;
					}
					intValue = intValue / 10;
					goalCoordinate[i] = thisCoord;
				}
			} else if (oMode == OdometerMode.TIME_HHHMMSS) {
				// Clamp the value
				value = Mathf.Min(value, 59.0f + 59.0f * 60.0f + 999.0f * 60.0f * 24.0f);

				// seconds
				float thisCoord = Mathf.Floor((value % 10.0f) * 2.0f) / 20.0f;
				goalCoordinate[6] = thisCoord;
				int intValue = (int)(value) / 10;
				
				// tens of seconds
				thisCoord = (float)(intValue % 6) / 10.0f;
				goalCoordinate[5] = thisCoord;
				intValue /= 6;

				// minutes
				thisCoord = (float)(intValue % 10) / 10.0f;
				goalCoordinate[4] = thisCoord;
				intValue /= 10;

				// tens of minutes
				thisCoord = (float)(intValue % 6) / 10.0f;
				goalCoordinate[3] = thisCoord;
				intValue /= 6;

				for (int i = 2; i >= 0; --i) {
					thisCoord = (float)(intValue % 10) / 10.0f;
					intValue = intValue / 10;
					goalCoordinate[i] = thisCoord;
				}
			} else {
				int intValue = (int)(value);

				for (int i = 7; i >= 0; --i) {
					float thisCoord = (float)(intValue % 10) / 10.0f;
					if (i == 7) {
						thisCoord = Mathf.Floor((value % 10.0f) * 2.0f) / 20.0f;
					}
					intValue = intValue / 10;
					goalCoordinate[i] = thisCoord;
				}
			}

			// Update interpolants
			for (int i = 0; i < 8; ++i) {
				if (currentCoordinate[i] != goalCoordinate[i]) {
					float startingPoint;
					float endingPoint;
					if (Mathf.Abs(currentCoordinate[i] - goalCoordinate[i]) <= 0.5f) {
						startingPoint = currentCoordinate[i];
						endingPoint = goalCoordinate[i];
					} else if (goalCoordinate[i] < currentCoordinate[i]) {
						startingPoint = currentCoordinate[i];
						endingPoint = goalCoordinate[i] + 1.0f;
					} else {
						startingPoint = currentCoordinate[i] + 1.0f;
						endingPoint = goalCoordinate[i];
					}

					// This lerp causes a rotation that starts quickly but
					// slows down close to the goal.  It actually looks
					// pretty good for typical incrementing counts, while the
					// rapid spinning of small values is chaotic enough that
					// you can't really tell what's going on, anyway.
					float goal = JUtil.DualLerp(startingPoint, endingPoint, 0.0f, 1.0f, dT);
					if (goal > 1.0f) {
						goal -= 1.0f;
					}
					currentCoordinate[i] = goal;
				}
			}
			lastUpdate = thisUpdate;
		}

		// Update according to the given refresh rate.
		private bool UpdateCheck()
		{
			refreshDrawCountdown--;

			if (refreshDrawCountdown <= 0) {
				refreshDrawCountdown = refreshDrawRate;
				return true;
			}

			return false;
		}

		private void RenderScreen()
		{
			RenderTexture backupRenderTexture = RenderTexture.active;

			if (!screenTexture.IsCreated()) {
				screenTexture.Create();
			}

			screenTexture.DiscardContents();
			RenderTexture.active = screenTexture;

			if (!startupComplete) {
				return;
			}

			UpdateOdometer();

			GL.PushMatrix();
			GL.LoadPixelMatrix(0, screenPixelWidth, screenPixelHeight, 0);

			GL.Clear(true, true, backgroundColorValue);

			// draw ...
			Rect digitTexCoord = new Rect(0.0f, 0.0f, 1.0f, 0.1f);
			Rect characterTexCoord = new Rect(0.0f, 0.0f, 1.0f, 0.125f);
			Rect texPosition = new Rect(displayPosition.x, displayPosition.y, characterSize.x, characterSize.y);

			// Draw the sign.
			if (oMode != OdometerMode.TIME_HHHMMSS) {
				if (characterTex != null) {
					characterTexCoord.y = signCurrentCoord;
					Graphics.DrawTexture(texPosition, characterTex, characterTexCoord, 0, 0, 0, 0, digitMaterial);
				}

				texPosition.x += characterSize.x;
			}
			digitTexCoord.y = currentCoordinate[0];
			Graphics.DrawTexture(texPosition, digitTex, digitTexCoord, 0, 0, 0, 0, digitMaterial);

			texPosition.x += characterSize.x;
			digitTexCoord.y = currentCoordinate[1];
			Graphics.DrawTexture(texPosition, digitTex, digitTexCoord, 0, 0, 0, 0, digitMaterial);

			texPosition.x += characterSize.x;
			digitTexCoord.y = currentCoordinate[2];
			Graphics.DrawTexture(texPosition, digitTex, digitTexCoord, 0, 0, 0, 0, digitMaterial);

			if (oMode == OdometerMode.SI || oMode == OdometerMode.TIME_HHHMMSS) {
				texPosition.x += characterSize.x;
				// SI Mode decimal goes here.
				// TIME_HHH_MM_SS mode, first colon goes here.
			}

			texPosition.x += characterSize.x;
			digitTexCoord.y = currentCoordinate[3];
			Graphics.DrawTexture(texPosition, digitTex, digitTexCoord, 0, 0, 0, 0, digitMaterial);

			texPosition.x += characterSize.x;
			digitTexCoord.y = currentCoordinate[4];
			Graphics.DrawTexture(texPosition, digitTex, digitTexCoord, 0, 0, 0, 0, digitMaterial);

			if (oMode == OdometerMode.TIME_HHHMMSS) {
				texPosition.x += characterSize.x;
				// TIME_HHH_MM_SS mode, second colon goes here.
			}

			texPosition.x += characterSize.x;
			digitTexCoord.y = currentCoordinate[5];
			Graphics.DrawTexture(texPosition, digitTex, digitTexCoord, 0, 0, 0, 0, digitMaterial);

			if (oMode == OdometerMode.SI) {
				texPosition.x += characterSize.x;
				characterTexCoord.y = prefixCurrentCoord;
				Graphics.DrawTexture(texPosition, characterTex, characterTexCoord, 0, 0, 0, 0, digitMaterial);
			} else {
				texPosition.x += characterSize.x;
				digitTexCoord.y = currentCoordinate[6];
				Graphics.DrawTexture(texPosition, digitTex, digitTexCoord, 0, 0, 0, 0, digitMaterial);

				if (oMode == OdometerMode.LINEAR) {
					texPosition.x += characterSize.x;
					digitTexCoord.y = currentCoordinate[7];
					Graphics.DrawTexture(texPosition, digitTex, digitTexCoord, 0, 0, 0, 0, digitMaterial);
				}
			}

			if (overlayTex != null) {
				Graphics.DrawTexture(new Rect(0, 0, screenTexture.width, screenTexture.height), overlayTex);
			}
			GL.PopMatrix();

			RenderTexture.active = backupRenderTexture;
		}

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel)) {
				return;
			}

			if (!UpdateCheck()) {
				return;
			}

			RenderScreen();
		}

		public void Start()
		{

			if (HighLogic.LoadedSceneIsEditor)
				return;

			try {
				if (!string.IsNullOrEmpty(odometerMode) && modeList.ContainsKey(odometerMode)) {
					oMode = modeList[odometerMode];
				}
				//else if (!string.IsNullOrEmpty(odometerMode)) {
				//	JUtil.LogMessage(this, "found odometerMode {0}, but it's not in the dictionary", odometerMode);
				//}
				//else {
				//	JUtil.LogMessage(this, "Did not find odometerMode");
				//}

				if (string.IsNullOrEmpty(characterTexture) && oMode == OdometerMode.SI) {
					JUtil.LogErrorMessage(this, "Prop configured as SI scaled, but there is no characterTexture");
					return;
				}

				if (string.IsNullOrEmpty(digitTexture)) {
					// We can't do anything without the digit texture
					JUtil.LogErrorMessage(this, "Prop can not function without a digitTexture");
					return;
				}

				digitTex = GameDatabase.Instance.GetTexture(digitTexture.EnforceSlashes(), false);
				if (digitTex == null) {
					JUtil.LogErrorMessage(this, "Failed to load digitTexture {0}", digitTexture);
					return;
				}

				if (!string.IsNullOrEmpty(characterTexture)) {
					characterTex = GameDatabase.Instance.GetTexture(characterTexture.EnforceSlashes(), false);
					if (characterTex == null) {
						JUtil.LogErrorMessage(this, "Failed to load characterTexture {0}", characterTexture);
						return;
					}
				}

				if (!string.IsNullOrEmpty(overlayTexture)) {
					overlayTex = GameDatabase.Instance.GetTexture(overlayTexture.EnforceSlashes(), false);
					if (overlayTex == null) {
						JUtil.LogErrorMessage(this, "Failed to load overlayTexture {0}", overlayTexture);
						return;
					}
				}

				if (string.IsNullOrEmpty(altVariable) != string.IsNullOrEmpty(perPodPersistenceName)) {
					JUtil.LogErrorMessage(this, "Both altVariable and perPodPeristenceName must be defined, or neither");
					return;
				}

				// MOARdV: Which one are we using?  HUD uses the latter, OrbitDisplay, the former.
				Shader unlit = Shader.Find("KSP/Alpha/Unlit Transparent");
				//Shader unlit = Shader.Find("Hidden/Internal-GUITexture");
				digitMaterial = new Material(unlit);
				comp = RasterPropMonitorComputer.Instantiate(internalProp);

				backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);

				lastUpdate = Planetarium.GetUniversalTime();

				screenTexture = new RenderTexture(screenPixelWidth, screenPixelHeight, 24, RenderTextureFormat.ARGB32);
				screenMat = internalProp.FindModelTransform(screenTransform).renderer.material;

				foreach (string layerID in textureLayerID.Split()) {
					screenMat.SetTexture(layerID.Trim(), screenTexture);
				}

				startupComplete = true;
			} catch {
				JUtil.AnnoyUser(this);
				throw;
			}
		}
	}
}
