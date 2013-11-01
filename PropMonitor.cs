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
		private Texture2D screenTexture;
		private int firstCharacter = 32;
		private int fontLettersX = 16;
		private int fontLettersY = 8;

		private void Start ()
		{
			// With that we should be able to get a texture by URL instead of asking for a transform, needs testing.
			if (GameDatabase.Instance.ExistsTexture (fontTransform)) {
				fontTexture = GameDatabase.Instance.GetTexture (fontTransform,false);
				Debug.Log ("RasterPropMonitor: Loading font texture from URL, " + fontTransform);
			} else {
				fontTexture = (Texture2D)base.internalProp.FindModelTransform (fontTransform).renderer.material.mainTexture;
				Debug.Log ("RasterPropMonitor: Loading font texture from transform, " + fontTransform);
			}

			fontLettersX = (int)(fontTexture.width / fontLetterWidth);
			fontLettersY = (int)(fontTexture.height / fontLetterHeight);

			screenText = new string[screenHeight];
			for (int i = 0; i < screenText.Length; i++)
				screenText [i] = "";

			screenText [0] = "RasterMonitor initializing...";
			screenUpdateRequired = true;

			screenTexture = new Texture2D (screenPixelWidth, screenPixelHeight, TextureFormat.RGB24, false);

			Material screen = base.internalProp.FindModelTransform (screenTransform).renderer.material;
			screen.SetTexture (textureLayerID, screenTexture);

			//screen.SetTextureScale ("_MainTex", new Vector2 (1f, 1f));
			//screen.SetTextureOffset ("_MainTex", new Vector2 (0f, 0f));

			Debug.Log ("RasterMonitor initialised.");
			Debug.Log ("fontLettersX: " + fontLettersX.ToString ()+" fontLettersY: " + fontLettersY.ToString ());
		}

		private void drawChar (char letter, int x, int y)
		{
			int charCode = ((int)letter) - firstCharacter;
			if (charCode < 0) {
				Debug.Log ("RasterMonitor: Attempted to print an illegal character " + letter);
				return;
			}
			int xSource = charCode % fontLettersX;
			int ySource = (charCode - xSource) / fontLettersX;
			//Debug.Log ("RasterMonitor char plate ID:" + charCode.ToString () + "X " + xSource.ToString () + " Y " + ySource.ToString ());

			Color[] pixelBlock = fontTexture.GetPixels (xSource * fontLetterWidth, fontTexture.height - ((ySource + 1) * fontLetterHeight), fontLetterWidth, fontLetterHeight);
			//Debug.Log ("RasterMonitor: copying from " + (xSource * fontLetterWidth).ToString () + "," + (fontTexture.height - ((ySource + 1) * fontLetterHeight)).ToString ());
			//Debug.Log ("RasterMonitor: Pasting char '" + letter.ToString () + "' at pos " + x.ToString () + "," + y.ToString () + " at " + (x * fontLetterWidth).ToString () + "," + (fontLetterHeight * (screenHeight - 1) - y * fontLetterHeight).ToString ());
			screenTexture.SetPixels (x * fontLetterWidth, fontLetterHeight * (screenHeight - 1) - y * fontLetterHeight, fontLetterWidth, fontLetterHeight, pixelBlock);
			//Debug.Log ("RasterMonitor: Pasted.");

		}

		private void updateScreen ()
		{
			for (int y=0; y<screenHeight; y++) {
				char[] line = screenText [y].ToCharArray ();
				for (int x=0; x<screenWidth && x<line.Length; x++) {
					drawChar (line [x], x, y);
				}
			}
			screenTexture.Apply ();
		}

		public override void OnUpdate ()
		{

			if (!HighLogic.LoadedSceneIsFlight || !(vessel == FlightGlobals.ActiveVessel && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA))
				return;

			if (screenUpdateRequired) {
				updateScreen ();
				screenUpdateRequired = false;
			}
		}
	}
}

