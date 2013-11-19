using System;
using UnityEngine;
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
		public Color emptyColor = Color.clear;
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
		[KSPField]
		public int refreshDrawRate = 2;
		[KSPField]
		public int refreshTextRate = 5;
		[KSPField]
		public int refreshDataRate = 10;
		// Some things in life are constant;
		private const int firstCharacter = 32;
		private const float defaultFOV = 60f;
		// Internal stuff.
		private Texture2D fontTexture;
		private RenderTexture screenTexture;
		private FlyingCamera cam;
		// Page definition syntax.
		private readonly string[] lineSeparator = { Environment.NewLine };
		private readonly string[] variableListSeparator = { "$&$" };
		private readonly string[] variableSeparator = { };
		// Local variables
		private int refreshDrawCountdown;
		private int refreshTextCountdown;
		private int vesselNumParts;
		private bool firstRenderComplete;
		private bool textRefreshRequired;
		private List<MonitorPage> pages = new List<MonitorPage>();
		private MonitorPage activePage;
		// All computations are split into a separate class, because it was getting a mite too big.
		private RasterPropMonitorComputer comp;
		// Persistence for current page variable.
		private PersistenceAccessor persistence;
		private string persistentVarName;
		private readonly SIFormatProvider fp = new SIFormatProvider();
		private string[] screenBuffer;
		private Rect[] fontCharacters;

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


			// We can pre-compute the rectangles the font characters will be copied from, this seems to make it slightly quicker...
			// although I'm not sure I'm not seeing things by this point.
			int fontLettersX = (fontTexture.width / fontLetterWidth);
			int fontLettersY = (fontTexture.height / fontLetterHeight);
			float letterSpanX = 1f / fontLettersX;
			float letterSpanY = 1f / fontLettersY;
			int lastCharacter = fontLettersX * fontLettersY;

			fontCharacters = new Rect[lastCharacter + 1];
			for (int i = 0; i < lastCharacter; i++) {
				int xSource = i % fontLettersX;
				int ySource = (i - xSource) / fontLettersX;

				fontCharacters[i] = new Rect(letterSpanX * xSource, letterSpanY * (fontLettersY - ySource - 1), letterSpanX, letterSpanY);
			}

			// Now that is done, proceed to setting up the screen.

			screenTexture = new RenderTexture(screenPixelWidth, screenPixelHeight, 24, RenderTextureFormat.ARGB32);
			Material screenMat = internalProp.FindModelTransform(screenTransform).renderer.material;
			screenMat.SetTexture(textureLayerID, screenTexture);
			screenTexture.wrapMode = TextureWrapMode.Clamp;

			// The neat trick. IConfigMode doesn't work. No amount of kicking got it to work.
			// Well, we don't need it. GameDatabase, gimme config nodes for all props!
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("PROP")) {
				// Now, we know our own prop name.
				if (node.GetValue("name") == internalProp.propName) {
					// So this is the configuration of our prop in memory. Nice place.
					// We know it contains at least one MODULE node, us.
					// And we know our moduleID, which is the number in order of being listed in the prop.
					// Therefore the module by that number is our module's own config node.
					ConfigNode[] pageNodes = node.GetNodes("MODULE")[moduleID].GetNodes("PAGE");

					// Which we can now parse for page definitions.
					for (int i = 0; i < pageNodes.Length; i++) {
						// Mwahahaha.
						try {
							var newPage = new MonitorPage(i, pageNodes[i], this);
							activePage = activePage ?? newPage;
							if (newPage.isDefault)
								activePage = newPage;
							pages.Add(newPage);
						} catch (ArgumentException e) {
							LogMessage("Warning - {0}", e);
						}
							
					}
					LogMessage("Done setting up pages, {0} pages ready.", pages.Count);
				}
			}

			// Install the calculator module.
			comp = JUtil.GetComputer(internalProp);
			comp.UpdateRefreshRates(refreshTextRate, refreshDataRate);

			// Load our state from storage...
			persistentVarName = "activePage" + internalProp.propID;
			persistence = new PersistenceAccessor(part);
			int? activePageID = persistence.GetVar(persistentVarName);
			if (activePageID != null) {
				activePage = pages[activePageID.Value];
			}

			// Create and point the camera.
			cam = new FlyingCamera(part, screenTexture, cameraAspect);
			cam.PointCamera(activePage.camera, activePage.cameraFOV);

		}

		public void ButtonClick(MonitorPage callingPage)
		{
			if (callingPage != activePage) {
				activePage.Active(false);
				activePage = callingPage;
				activePage.Active(true);
				persistence.SetVar(persistentVarName, activePage.pageNumber);
				cam.PointCamera(activePage.camera, activePage.cameraFOV);
				refreshDrawCountdown = refreshTextCountdown = 0;
				comp.updateForced = true;
				firstRenderComplete = false;
			}
		}

		private void DrawChar(char letter, int x, int y)
		{
			int charCode = (ushort)letter;
			// Clever bit.
			if (charCode >= 128)
				charCode -= 32;

			charCode -= firstCharacter;

			if (charCode < 0 || charCode >= fontCharacters.Length) {
				LogMessage("Attempted to print a character \"{0}\" not present in the font, raw value {1} ", letter.ToString(), Convert.ToUInt16(letter));
				return;
			}

			// This is complicated.
			// The destination rectangle has coordinates given in pixels, from top left corner of the texture.
			// The source rectangle has coordinates in floats (!) from bottom left corner of the texture!
			// And without the LoadPixelMatrix, DrawTexture produces nonsense anyway.
			Graphics.DrawTexture(
				new Rect(x * fontLetterWidth, y * fontLetterHeight, fontLetterWidth, fontLetterHeight),
				fontTexture,
				fontCharacters[charCode],
				0, 0, 0, 0
			);

		}

		private string ProcessString(string input)
		{
			if (input.IndexOf(variableListSeparator[0], StringComparison.Ordinal) >= 0) {
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
			refreshDrawCountdown--;
			refreshTextCountdown--;
			if (vesselNumParts != vessel.Parts.Count) {
				refreshDrawCountdown = 0;
				refreshTextCountdown = 0;
				vesselNumParts = vessel.Parts.Count;
			}
			if (refreshTextCountdown <= 0) {
				textRefreshRequired = true;
				refreshTextCountdown = refreshTextRate;
			}

			if (refreshDrawCountdown <= 0) {
				refreshDrawCountdown = refreshDrawRate;
				return true;
			}

			return false;
		}

		private void RenderScreen()
		{
			// Technically, I should also check in case RenderTexture is lost when the screensaver got turned on.
			// But I'll wait until anyone complains before doing that.
			RenderTexture backupRenderTexture = RenderTexture.active;

			screenTexture.DiscardContents();
			RenderTexture.active = screenTexture;



			// Draw the background, if any.
			switch (activePage.background) {
				case MonitorPage.BackgroundType.Camera:
					if (!cam.Render())
						GL.Clear(true, true, emptyColor);
					break;
				case MonitorPage.BackgroundType.None:
					GL.Clear(true, true, emptyColor);
					break;
				default:
					if (!activePage.RenderBackground(screenTexture))
						GL.Clear(true, true, emptyColor);
					break;
			}

			// This is the important witchcraft. Without that, DrawTexture does not print where we expect it to.
			// Cameras don't care because they have their own matrices, but DrawTexture does.
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, screenPixelWidth, screenPixelHeight, 0);

			if (!string.IsNullOrEmpty(activePage.Text)) {
				// Draw the text.
				for (int y = 0; y < screenHeight && y < screenBuffer.Length; y++) {
					if (!string.IsNullOrEmpty(screenBuffer[y])) {
						char[] line = screenBuffer[y].ToCharArray();
						for (int x = 0; x < screenWidth && x < line.Length; x++) {
							DrawChar(line[x], x, y);
						}
					}
				}
			}

			GL.PopMatrix();
			RenderTexture.active = backupRenderTexture;
		}

		public void FillScreenBuffer()
		{
			screenBuffer = new string[screenHeight];
			string[] linesArray = activePage.Text.Split(lineSeparator, StringSplitOptions.None);
			for (int i = 0; i < screenHeight; i++)
				screenBuffer[i] = (i < linesArray.Length) ? ProcessString(linesArray[i]).TrimEnd() : string.Empty;
			textRefreshRequired = false;
		}

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (!UpdateCheck())
				return;

			if (!activePage.isMutable) { 
				// In case the page is empty and has no camera, the screen is treated as turned off and blanked once.
				if (!firstRenderComplete) {
					FillScreenBuffer();
					RenderScreen();
					firstRenderComplete = true;
				}
			} else {
				if (textRefreshRequired)
					FillScreenBuffer();
				RenderScreen();
				firstRenderComplete = false;
			}

		}
	}
}

