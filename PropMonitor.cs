using System;
using UnityEngine;

namespace RasterPropMonitor
{
	public class RasterPropMonitor: InternalModule
	{
		//[KSPField]
		//public string screenUnitID = "Raster1"; // Pout.
		[KSPField]
		public string screenTransform = "screenTransform";
		[KSPField]
		public string fontTransform = "fontTransform";
		[KSPField]
		public string textureLayerID = "_MainTex";
		[KSPField]
		public string blankingColor = "0,0,0,255";
		[KSPField]
		public int screenWidth = 32;
		[KSPField]
		public int screenHeight = 8;
		[KSPField]
		public int screenPixelWidth = 512;
		[KSPField]
		public int screenPixelHeight = 256;
		[KSPField]
		public int fontLetterWidth = 16;
		[KSPField]
		public int fontLetterHeight = 32;
		[KSPField]
		public string[] screenText;
		[KSPField]
		public bool screenUpdateRequired = false;
		private Texture2D fontTexture;
		private RenderTexture screenTexture;
		private int firstCharacter = 32;
		private int fontLettersX = 16;
		private int fontLettersY = 8;
		private int lastCharacter = 255;
		private float letterSpanX = 1f;
		private float letterSpanY = 1f;
		private Color emptyColor = new Color (0, 0, 0, 255);

		private void Start ()
		{

			Debug.Log ("RasterPropMonitor: Trying to locate " + fontTransform + " in GameDatabase...");
			if (GameDatabase.Instance.ExistsTexture (fontTransform)) {
				fontTexture = GameDatabase.Instance.GetTexture (fontTransform, false);
				Debug.Log (String.Format ("RasterPropMonitor: Loading font texture from URL, \"{0}\"", fontTransform));
			} else {
				fontTexture = (Texture2D)base.internalProp.FindModelTransform (fontTransform).renderer.material.mainTexture;
				Debug.Log (String.Format ("RasterPropMonitor: Loading font texture from a transform named, \"{0}\"", fontTransform));
			}

			fontLettersX = (int)(fontTexture.width / fontLetterWidth);
			fontLettersY = (int)(fontTexture.height / fontLetterHeight);

			letterSpanX = 1f / fontLettersX;
			letterSpanY = 1f / fontLettersY;

			lastCharacter = fontLettersX * fontLettersY;

			screenText = new string[screenHeight];
			for (int i = 0; i < screenText.Length; i++)
				screenText [i] = "";

			string[] tokens = blankingColor.Split (',');
			if (tokens.Length != 4) {
				Debug.LogWarning ("RasterPropMonitor: Blanking color does not make sense, ignoring.");
			} else {
				emptyColor = new Color (
					Convert.ToInt16 (tokens [0]),
					Convert.ToInt16 (tokens [1]),
					Convert.ToInt16 (tokens [2]),
					Convert.ToInt16 (tokens [3])
				);
			}

			screenText [0] = "RasterMonitor initializing...";
			screenUpdateRequired = true;

			screenTexture = new RenderTexture (screenPixelWidth, screenPixelHeight, 0, RenderTextureFormat.ARGB32);

			Material screen = base.internalProp.FindModelTransform (screenTransform).renderer.material;
			screen.SetTexture (textureLayerID, screenTexture);

			Debug.Log (String.Format ("RasterMonitor initialised. fontLettersX: {0}, fontLettersY: {1}, letterSpanX: {2}, letterSpanY: {3}.", fontLettersX, fontLettersY, letterSpanX, letterSpanY));
		}

		private void drawChar (char letter, int x, int y)
		{
			int charCode = (ushort)letter;
			// Clever bit.
			if (charCode >= 128)
				charCode -= 32;

			charCode -= firstCharacter;

			if (charCode < 0 || charCode > lastCharacter) {
				Debug.Log (String.Format ("RasterMonitor: Attempted to print a character \"{0}\" not present in the font, raw value {1} ", letter, (ushort)letter));
				return;
			}
			int xSource = charCode % fontLettersX;
			int ySource = (charCode - xSource) / fontLettersX;

			// This is complicated.
			// The destination rectangle has coordinates given in pixels, from top left corner of the texture.
			// The source rectangle has coordinates in floats (!) from bottom left corner of the texture!
			// And without the LoadPixelMatrix, DrawTexture produces nonsense anyway.
			Graphics.DrawTexture (
				new Rect (x * fontLetterWidth, y * fontLetterHeight, fontLetterWidth, fontLetterHeight),
				fontTexture,
				new Rect (letterSpanX * xSource, letterSpanY * (fontLettersY - ySource - 1), letterSpanX, letterSpanY),
				0, 0, 0, 0
			);

		}

		private void updateScreen ()
		{
			// Technically, I should also check in case RenderTexture is lost when the screensaver got turned on.
			// But I'll wait until anyone complains before doing that.
			RenderTexture.active = screenTexture;

			// This is the important witchcraft. Without that, DrawTexture does not print correctly.
			GL.PushMatrix ();
			GL.LoadPixelMatrix (0, screenPixelWidth, screenPixelHeight, 0);

			// Clear the texture now. It saves computrons compared to printing spaces.
			GL.Clear (true, true, emptyColor);

			for (int y=0; y<screenHeight; y++) {
				char[] line = screenText [y].ToCharArray ();
				for (int x=0; x<screenWidth && x<line.Length; x++) {
					drawChar (line [x], x, y);
				}
			}
			GL.PopMatrix ();
			RenderTexture.active = null;
		}

		public override void OnUpdate ()
		{

			if (!HighLogic.LoadedSceneIsFlight ||
			    !(vessel == FlightGlobals.ActiveVessel &&
			    (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal)
			    ))
				return;

			if (screenUpdateRequired) {
				updateScreen ();
				screenUpdateRequired = false;
			}
		}
	}
}

