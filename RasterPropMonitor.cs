using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace JSI
{
	public class RasterPropMonitor: InternalModule
	{
		[KSPField]
		public string screenTransform = "screenTransform";
		[KSPField]
		public string fontTransform = "fontTransform";
		[KSPField]
		public string textureLayerID = "_MainTex";
		[KSPField]
		public Color emptyColor = Color.black;
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
		// Some things in life are constant;
		private const int firstCharacter = 32;
		private const float defaultFOV = 60f;
		[KSPField]
		public int refreshRate = 5;
		[KSPField]
		public int refreshDataRate = 10;
		// Internal stuff.
		private bool screenUpdateRequired;
		private Texture2D fontTexture;
		private RenderTexture screenTexture;
		private int fontLettersX = 16;
		private int fontLettersY = 8;
		private int lastCharacter = 255;
		private Vector2 letterSpan;
		// Camera support.
		private bool cameraEnabled;
		private GameObject cameraTransform;
		private Part cameraPart;
		private Camera[] cameraObject = { null, null, null };
		// Config syntax.
		private readonly string[] lineSeparator = { Environment.NewLine };
		private readonly string[] variableListSeparator = { "$&$" };
		private readonly string[] variableSeparator = { };
		// Local variables
		private string[] textArray;
		private int updateCountdown = 0;
		private bool updateForced = false;
		private bool screenWasBlanked = false;
		private bool currentPageIsMutable = false;
		private bool currentPageFirstPassComplete = false;
		private List<MonitorPage> pages = new List<MonitorPage>();
		private MonitorPage activePage;
		// All computations are split into a separate class, because it was getting a mite too big.
		private RasterPropMonitorComputer comp;
		// Persistence for current page variable.
		private PersistenceAccessor persistence;
		private string persistentVarName;
		private readonly SIFormatProvider fp = new SIFormatProvider();

		private static void LogMessage(string line, params object[] list)
		{
			Debug.Log(String.Format(typeof(RasterPropMonitor).Name + ": " + line, list));
		}

		public void Start()
		{
			// Loading the font...
			LogMessage("Trying to locate \"{0}\" in GameDatabase...", fontTransform);
			if (GameDatabase.Instance.ExistsTexture(fontTransform)) {
				fontTexture = GameDatabase.Instance.GetTexture(fontTransform, false);
				LogMessage("Loading font texture from URL, \"{0}\"", fontTransform);
			} else {
				fontTexture = (Texture2D)internalProp.FindModelTransform(fontTransform).renderer.material.mainTexture;
				LogMessage("Loading font texture from a transform named, \"{0}\"", fontTransform);
			}

			fontLettersX = (fontTexture.width / fontLetterWidth);
			fontLettersY = (fontTexture.height / fontLetterHeight);

			letterSpan.x = 1f / fontLettersX;
			letterSpan.y = 1f / fontLettersY;

			lastCharacter = fontLettersX * fontLettersY;

			textArray = new string[screenHeight];

			textArray[0] = "Monitor initializing...";

			screenTexture = new RenderTexture(screenPixelWidth, screenPixelHeight, 24, RenderTextureFormat.ARGB32);

			Material screen = internalProp.FindModelTransform(screenTransform).renderer.material;
			screen.SetTexture(textureLayerID, screenTexture);

			screenUpdateRequired = true;


			// The neat trick. IConfigMode doesn't work. No amount of kicking got it to work.
			// Well, we don't need it. GameDatabase, gimme config nodes for all props!
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("PROP")) {
				// Now, we know our own prop name.
				if (node.GetValue("name") == internalProp.propName) {
					// So this is the configuration of our prop in memory. Nice place.
					// We know it contains at least one MODULE node, us.
					// And we know our moduleID, which is the number in order of being listed in the prop.
					// Therefore the module by that number is our module's own config node.
					ConfigNode[] pageNodes = node.GetNodes("MODULE")[moduleID].GetNode("PAGEDEFINITIONS").GetNodes("PAGE");
					for (int i = 0; i < pageNodes.Length; i++) {
						// Mwahahaha.
						try {
							var newPage = new MonitorPage(i, pageNodes[i], this);
							pages.Add(newPage);
						} catch (ArgumentException e) {
							LogMessage("Warning - {0}", e);
						}
							
					}
					LogMessage("Done setting up pages, {0} pages ready.", pages.Count);
				}
			}

			// Maybe I need an extra parameter to set the initially active page.

			comp = JUtil.GetComputer(internalProp);

			comp.UpdateRefreshRates(refreshRate, refreshDataRate);

			// Load our state from storage...

			persistentVarName = "activePage" + internalProp.propID;
			persistence = new PersistenceAccessor(part);
			int? activePageID = persistence.GetVar(persistentVarName);
			if (activePageID != null) {
				activePage = (from x in pages
				              where x.PageNumber == activePageID
				              select x).First();
			} else {
				activePage = (from x in pages
				              where x.IsDefault
				              select x).FirstOrDefault();
				if (activePage == null)
					activePage = pages[0];
			}

			// So camera support.

			SetCamera(activePage.Camera);
		}

		private void CleanupCameraObjects()
		{
			for (int i = 0; i < 3; i++)
				if (cameraObject[i].gameObject != null) {
					Destroy(cameraObject[i].gameObject);
					cameraObject[i] = null;
				}
			cameraEnabled = false;
			cameraPart = null;
		}

		private bool LocateCamera(Part thatpart, string transformName)
		{
			Transform location = thatpart.FindModelTransform(transformName);
			if (location != null) {
				cameraTransform = location.gameObject;
				cameraPart = thatpart;
				return true;
			}
			return false;
		}

		private void SetCamera(string transformName)
		{
			if (!string.IsNullOrEmpty(transformName)) {
				string[] tokens = transformName.Split(',');
				if (tokens.Length == 2) {
					float fov;
					float.TryParse(tokens[1], out fov);
					SendCamera(tokens[0].Trim(), fov);
				} else
					SendCamera(transformName);
			} else {
				SendCamera(null);
			}
		}

		private void CameraSetup(int index, string sourceName, float fov)
		{
			var cameraBody = new GameObject();
			cameraBody.name = typeof(RasterPropMonitor).Name + index + cameraBody.GetInstanceID();
			cameraObject[index] = cameraBody.AddComponent<Camera>();

			Camera sourceCam = null;
			foreach (Camera cam in Camera.allCameras) {
				if (cam.name == sourceName) {
					sourceCam = cam;
					break;
				}
			}

			cameraObject[index].CopyFrom(sourceCam);
			cameraObject[index].enabled = false;
			cameraObject[index].aspect = cameraAspect;
			cameraObject[index].fieldOfView = fov;
			cameraObject[index].targetTexture = screenTexture;
		}

		public void ButtonClick(MonitorPage callingPage)
		{
			if (callingPage != activePage) {
				activePage = callingPage;
				persistence.SetVar(persistentVarName, activePage.PageNumber);
				SetCamera(activePage.Camera);
				updateForced = true;
				comp.updateForced = true;
				currentPageIsMutable = !string.IsNullOrEmpty(activePage.Camera);
				currentPageFirstPassComplete = false;
			}
		}

		public void SendCamera(string newCameraName)
		{
			SendCamera(newCameraName, defaultFOV);
		}

		public void SendCamera(string newCameraName, float newFOV)
		{
			if (newCameraName != null) {

				// First, we search our own part for this camera transform,
				// only then we search all other parts of the vessel.
				if (!LocateCamera(part, newCameraName))
					foreach (Part thatpart in vessel.parts) {
						if (LocateCamera(thatpart, newCameraName))
							break;
					}

				if (cameraTransform != null) {
					LogMessage("Switching to camera \"{0}\".", cameraTransform.name);

					float fov = (newFOV > 0) ? newFOV : defaultFOV;
					CameraSetup(0, "Camera ScaledSpace", fov);
					CameraSetup(1, "Camera 01", fov);
					CameraSetup(2, "Camera 00", fov);

					cameraEnabled = true;
					screenUpdateRequired = true;

				} else {
					LogMessage("Tried to switch to camera \"{0}\" but camera was not found.", newCameraName);
					if (cameraEnabled)
						CleanupCameraObjects();
					else {
						cameraPart = null;
					}
				}
			} else {
				if (cameraEnabled) {
					LogMessage("Turning camera off...");
					CleanupCameraObjects();
				}
			}
		}

		private void DrawChar(char letter, int x, int y)
		{
			int charCode = (ushort)letter;
			// Clever bit.
			if (charCode >= 128)
				charCode -= 32;

			charCode -= firstCharacter;

			if (charCode < 0 || charCode > lastCharacter) {
				LogMessage("Attempted to print a character \"{0}\" not present in the font, raw value {1} ", letter.ToString(), Convert.ToUInt16(letter));
				return;
			}
			int xSource = charCode % fontLettersX;
			int ySource = (charCode - xSource) / fontLettersX;

			// This is complicated.
			// The destination rectangle has coordinates given in pixels, from top left corner of the texture.
			// The source rectangle has coordinates in floats (!) from bottom left corner of the texture!
			// And without the LoadPixelMatrix, DrawTexture produces nonsense anyway.
			Graphics.DrawTexture(
				new Rect(x * fontLetterWidth, y * fontLetterHeight, fontLetterWidth, fontLetterHeight),
				fontTexture,
				new Rect(letterSpan.x * xSource, letterSpan.y * (fontLettersY - ySource - 1), letterSpan.x, letterSpan.y),
				0, 0, 0, 0
			);

		}

		private string ProcessString(string input)
		{
			// Each separate output line is delimited by Environment.NewLine.
			// When loading from a config file, you can't have newlines in it, so they're represented by "$$$".
			// I didn't expect this, but Linux newlines work just as well as Windows ones.
			//
			// You can read a full description of this mess in DOCUMENTATION.md

			if (input.IndexOf(variableListSeparator[0], StringComparison.Ordinal) >= 0) {
				currentPageIsMutable = true;

				string[] tokens = input.Split(variableListSeparator, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length != 2) {
					return "FORMAT ERROR";
				} else {
					string[] vars = tokens[1].Split(variableSeparator, StringSplitOptions.RemoveEmptyEntries);

					var variables = new object[vars.Length];
					for (int i = 0; i < vars.Length; i++) {
						variables[i] = comp.ProcessVariable(vars[i]);
					}
					return String.Format(fp, tokens[0], variables);
				}
			}
			return input;
		}
		// Update according to the given refresh rate.
		private bool UpdateCheck()
		{
			if (updateCountdown <= 0 || updateForced) {
				updateForced = false;
				return true;
			}
			updateCountdown--;
			return false;
		}

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight)
				return;

			if (!((CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal) &&
			    vessel == FlightGlobals.ActiveVessel))
				return;

			if (!UpdateCheck())
				return;

			// This way we don't check for that here, but how do we deal with firstpasscomplete?

			/*
				if (activePage.handler != null) {
					activePage.text = activePage.handler();
					currentPageFirstPassComplete = false;
				}
				*/
			if (string.IsNullOrEmpty(activePage.Text) && !currentPageIsMutable) { 
				// In case the page is empty and has no camera, the screen is treated as turned off and blanked once.
				if (!screenWasBlanked) {
					textArray = new string[screenHeight];
					screenUpdateRequired = true;
					screenWasBlanked = true;
				}
			} else {
				if (!currentPageFirstPassComplete || currentPageIsMutable) {
					string[] linesArray = activePage.Text.Split(lineSeparator, StringSplitOptions.None);
					for (int i = 0; i < screenHeight; i++) {
						textArray[i] = (i < linesArray.Length) ? ProcessString(linesArray[i]).TrimEnd() : string.Empty;
					}
					screenWasBlanked = false;
					screenUpdateRequired = true;
					currentPageFirstPassComplete = true;
				}
			}

			if (screenUpdateRequired) {

				// Technically, I should also check in case RenderTexture is lost when the screensaver got turned on.
				// But I'll wait until anyone complains before doing that.
				RenderTexture backupRenderTexture = RenderTexture.active;

				screenTexture.DiscardContents();
				RenderTexture.active = screenTexture;

				// This is the important witchcraft. Without that, DrawTexture does not print correctly.
				GL.PushMatrix();
				GL.LoadPixelMatrix(0, screenPixelWidth, screenPixelHeight, 0);

				if (cameraEnabled) {
					if (cameraPart.vessel != FlightGlobals.ActiveVessel) {
						CleanupCameraObjects();
					} else {

						// ScaledSpace camera is special. :(
						cameraObject[0].transform.rotation = cameraTransform.transform.rotation;
						cameraObject[0].Render();
						for (int i = 1; i < 3; i++) {
							cameraObject[i].transform.position = cameraTransform.transform.position;
							cameraObject[i].transform.rotation = cameraTransform.transform.rotation;

							cameraObject[i].Render();
						}
					}
				} else {
					GL.Clear(true, true, emptyColor);
				}

				for (int y = 0; y < screenHeight && y < textArray.Length; y++) {
					if (!string.IsNullOrEmpty(textArray[y])) {
						char[] line = textArray[y].ToCharArray();
						for (int x = 0; x < screenWidth && x < line.Length; x++) {
							DrawChar(line[x], x, y);
						}
					}
				}

				GL.PopMatrix();
				RenderTexture.active = backupRenderTexture;
				screenUpdateRequired = false;
			}

		}
	}
}

