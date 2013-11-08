using System;
using System.Collections.Generic;
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
		public float cameraAspect = 2f;
		// Public variables that other modules can reflect into and send us data.
		public string[] screenText;
		public bool screenUpdateRequired = false;
		public string cameraName = null;
		public float fov = 60f;
		public bool setCamera = false;
		// Other internal stuff...
		private Texture2D fontTexture;
		private RenderTexture screenTexture;
		private int firstCharacter = 32;
		private int fontLettersX = 16;
		private int fontLettersY = 8;
		private int lastCharacter = 255;
		private float letterSpanX = 1f;
		private float letterSpanY = 1f;
		private Color emptyColor = new Color (0, 0, 0, 255);
		// Camera support.
		private bool cameraEnabled = false;
		private GameObject cameraTransform;
		private Part cameraPart = null;
		private Part myHomePart = null;
		private Camera[] cameraObject = { null, null, null };

		private void logMessage (string line, params object[] list)
		{
			Debug.Log (String.Format (this.ClassName + ": " + line, list));
		}

		private void Start ()
		{

			logMessage ("Trying to locate \"{0}\" in GameDatabase...", fontTransform);
			if (GameDatabase.Instance.ExistsTexture (fontTransform)) {
				fontTexture = GameDatabase.Instance.GetTexture (fontTransform, false);
				logMessage ("Loading font texture from URL, \"{0}\"", fontTransform);
			} else {
				fontTexture = (Texture2D)base.internalProp.FindModelTransform (fontTransform).renderer.material.mainTexture;
				logMessage ("Loading font texture from a transform named, \"{0}\"", fontTransform);
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
				logMessage ("Blanking color \"{0}\" does not make sense, ignoring.", blankingColor);
			} else {
				emptyColor = new Color (
					Convert.ToInt16 (tokens [0]),
					Convert.ToInt16 (tokens [1]),
					Convert.ToInt16 (tokens [2]),
					Convert.ToInt16 (tokens [3])
				);
			}

			screenText [0] = "Monitor initializing...";

			screenTexture = new RenderTexture (screenPixelWidth, screenPixelHeight, 24, RenderTextureFormat.ARGB32);

			Material screen = base.internalProp.FindModelTransform (screenTransform).renderer.material;
			screen.SetTexture (textureLayerID, screenTexture);

			logMessage ("Initialised. fontLettersX: {0}, fontLettersY: {1}, letterSpanX: {2}, letterSpanY: {3}.", fontLettersX, fontLettersY, letterSpanX, letterSpanY);

			// That leaves us with a clear reference to the part which our internal belongs to, which the class apparently lacks.
			foreach (Part thatpart in vessel.parts) {
				if (thatpart.internalModel != null) {
					// I'm not sure I'm not doing something radically silly here.
					foreach (InternalProp prop in thatpart.internalModel.props) {
						RasterPropMonitor myself = prop.FindModelComponent<RasterPropMonitor> ();
						if (myself != null && myself == this) {
							myHomePart = thatpart;
							break;
						}
					}
				}
				if (myHomePart != null)
					break;
			}

			screenUpdateRequired = true;
		}

		private void cameraSetup (int index, string sourceName)
		{
			GameObject cameraBody = new GameObject ();
			cameraBody.name = "RPMC" + index.ToString () + " " + cameraBody.GetInstanceID ();
			cameraObject [index] = cameraBody.AddComponent<Camera> ();

			Camera sourceCam = null;
			foreach (Camera cam in Camera.allCameras) {
				if (cam.name == sourceName) {
					sourceCam = cam;
					break;
				}
			}

			cameraObject [index].CopyFrom (sourceCam);
			cameraObject [index].enabled = false;
			cameraObject [index].aspect = cameraAspect;
			cameraObject [index].targetTexture = screenTexture;
		}

		private void createCameraObjects ()
		{
			cameraSetup (0, "Camera ScaledSpace");
			cameraSetup (1, "Camera 01");
			cameraSetup (2, "Camera 00");
			cameraEnabled = true;
			cameraName = null;
		}

		private void cleanupCameraObjects ()
		{
			for (int i=0; i<3; i++)
				if (cameraObject [i].gameObject != null) {
					GameObject.Destroy (cameraObject [i].gameObject);
					cameraObject [i] = null;
				}
			cameraName = null;
			cameraEnabled = false;
			cameraPart = null;
		}

		private void drawChar (char letter, int x, int y)
		{
			int charCode = (ushort)letter;
			// Clever bit.
			if (charCode >= 128)
				charCode -= 32;

			charCode -= firstCharacter;

			if (charCode < 0 || charCode > lastCharacter) {
				logMessage ("Attempted to print a character \"{0}\" not present in the font, raw value {1} ", letter, (ushort)letter);
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

		private bool locateCamera (Part thatpart, string name)
		{
			Transform location = thatpart.FindModelTransform (cameraName);
			if (location != null) {
				cameraTransform = location.gameObject;
				cameraPart = thatpart;
				return true;
			}
			return false;
		}

		public override void OnUpdate ()
		{

			if (!HighLogic.LoadedSceneIsFlight ||
			    !(vessel == FlightGlobals.ActiveVessel &&
			    (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal)
			    ))
				return;

			if (setCamera) {
				if (cameraName != null) {

					// First, we search our own part for this camera transform,
					// only then we search all other parts of the vessel.
					if (!locateCamera (myHomePart, cameraName))
						foreach (Part thatpart in vessel.parts) {
							if (locateCamera (thatpart, cameraName))
								break;
						}

					if (cameraTransform != null) {
						logMessage ("Switching to camera \"{0}\".", cameraTransform.name);
						createCameraObjects ();
					} else {
						logMessage ("Tried to switch to camera \"{0}\" but camera was not found.", cameraName);
						cleanupCameraObjects ();
					}
				} else {
					if (cameraEnabled) {
						logMessage ("Turning camera off...");
						cleanupCameraObjects ();
					}
				}
				setCamera = false;
			}

			if (screenUpdateRequired) {

				// Technically, I should also check in case RenderTexture is lost when the screensaver got turned on.
				// But I'll wait until anyone complains before doing that.
				RenderTexture backupRenderTexture = RenderTexture.active;

				screenTexture.DiscardContents ();
				RenderTexture.active = screenTexture;

				// This is the important witchcraft. Without that, DrawTexture does not print correctly.
				GL.PushMatrix ();
				GL.LoadPixelMatrix (0, screenPixelWidth, screenPixelHeight, 0);

				if (cameraEnabled) {
					if (cameraPart.vessel != FlightGlobals.ActiveVessel) {
						cleanupCameraObjects ();
					} else {

						// ScaledSpace camera is special. :(
						cameraObject [0].transform.rotation = cameraTransform.transform.rotation;
						cameraObject [0].fieldOfView = fov;
						cameraObject [0].Render ();
						for (int i=1; i<3; i++) {
							cameraObject [i].transform.position = cameraTransform.transform.position;
							cameraObject [i].transform.rotation = cameraTransform.transform.rotation;
							cameraObject [i].fieldOfView = fov;

							cameraObject [i].Render ();
						}
					}
				} else {
					GL.Clear (true, true, emptyColor);
				}

				for (int y=0; y<screenHeight; y++) {
					char[] line = screenText [y].ToCharArray ();
					for (int x=0; x<screenWidth && x<line.Length; x++) {
						drawChar (line [x], x, y);
					}
				}

				GL.PopMatrix ();
				RenderTexture.active = backupRenderTexture;


				screenUpdateRequired = false;
			}
		}
	}
}

