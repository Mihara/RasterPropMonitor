using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
		public string emptyColor = string.Empty;
		public Color emptyColorValue = Color.clear;
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
		[KSPField]
		public string globalButtons;
		[KSPField]
		public string buttonClickSound;
		[KSPField]
		public float buttonClickVolume = 0.5f;
		[KSPField]
		public bool needsElectricCharge = true;
		[KSPField]
		public string defaultFontTint = string.Empty;
		public Color defaultFontTintValue = Color.white;
		[KSPField]
		public string noSignalTextureURL = string.Empty;
		[KSPField]
		public string fontDefinition = string.Empty;
		// This needs to be public so that pages can point it.
		public FlyingCamera CameraStructure;
		// Internal stuff.
		private readonly List<Texture2D> fontTexture = new List<Texture2D>();
		private RenderTexture screenTexture;
		// Local variables
		private int refreshDrawCountdown;
		private int refreshTextCountdown;
		private int vesselNumParts;
		private bool firstRenderComplete;
		private bool textRefreshRequired;
		private readonly List<MonitorPage> pages = new List<MonitorPage>();
		private int fontTextureIndex;
		private MonitorPage activePage;
		// All computations are split into a separate class, because it was getting a mite too big.
		private RasterPropMonitorComputer comp;
		// Persistence for current page variable.
		private PersistenceAccessor persistence;
		private string persistentVarName;
		private string[] screenBuffer;
		private readonly Dictionary<char,Rect> fontCharacters = new Dictionary<char,Rect>();
		private FXGroup audioOutput;
		private double electricChargeReserve;
		public Texture2D noSignalTexture;
		private readonly DefaultableDictionary<char,bool> characterWarnings = new DefaultableDictionary<char, bool>(false);
		private float fontLetterHalfHeight;
		private float fontLetterHalfWidth;
		private float fontLetterDoubleWidth;
		private bool startupComplete;
		private string fontDefinitionString = @" !""#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~Δ☊¡¢£¤¥¦§¨©ª«¬☋®¯°±²³´µ¶·¸¹º»¼½¾¿";

		private enum Script
		{
			Normal,
			Subscript,
			Superscript,
		}

		private enum Width
		{
			Normal,
			Half,
			Double,
		}

		private static Texture2D LoadFont(object caller, InternalProp thisProp, string location, bool extra)
		{
			Texture2D font = null;
			if (!string.IsNullOrEmpty(location)) {
				JUtil.LogMessage(caller, "Trying to locate \"{0}\" in GameDatabase...", location);
				if (GameDatabase.Instance.ExistsTexture(location.EnforceSlashes())) {
					font = GameDatabase.Instance.GetTexture(location.EnforceSlashes(), false);
					JUtil.LogMessage(caller, "Loading{1} font texture from URL, \"{0}\"", location, extra ? " extra" : string.Empty);
				} else {
					font = (Texture2D)thisProp.FindModelTransform(location).renderer.material.mainTexture;
					JUtil.LogMessage(caller, "Loading{1} font texture from a transform named, \"{0}\"", location, extra ? " extra" : string.Empty);
				}
			}
			return font;
		}

		public void Start()
		{
			InstallationPathWarning.Warn();

			// Install the calculator module.
			comp = RasterPropMonitorComputer.Instantiate(internalProp);
			comp.UpdateRefreshRates(refreshTextRate, refreshDataRate);

			// Loading the font...
			fontTexture.Add(LoadFont(this, internalProp, fontTransform, false));

			// Damn KSP's config parser!!!
			if (!string.IsNullOrEmpty(emptyColor))
				emptyColorValue = ConfigNode.ParseColor32(emptyColor);
			if (!string.IsNullOrEmpty(defaultFontTint))
				defaultFontTintValue = ConfigNode.ParseColor32(defaultFontTint);

			if (!string.IsNullOrEmpty(fontDefinition)) {
				JUtil.LogMessage(this, "Loading font definition from {0}", fontDefinition);
				fontDefinitionString = File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + fontDefinition.EnforceSlashes(), Encoding.UTF8)[0];
			}

			// We can pre-compute the rectangles the font characters will be copied from, this seems to make it slightly quicker...
			// although I'm not sure I'm not seeing things by this point.
			int fontLettersX = (fontTexture[0].width / fontLetterWidth);
			int fontLettersY = (fontTexture[0].height / fontLetterHeight);
			float letterSpanX = 1f / fontLettersX;
			float letterSpanY = 1f / fontLettersY;
			int lastCharacter = fontLettersX * fontLettersY;

			if (lastCharacter != fontDefinitionString.Length) {
				JUtil.LogMessage(this, "Warning, number of letters in the font definition does not match font bitmap size.");
			}

			for (int i = 0; i < lastCharacter && i < fontDefinitionString.Length; i++) {
				int xSource = i % fontLettersX;
				int ySource = (i - xSource) / fontLettersX;
				if (!fontCharacters.ContainsKey(fontDefinitionString[i]))
					fontCharacters[fontDefinitionString[i]] = new Rect(letterSpanX * xSource, letterSpanY * (fontLettersY - ySource - 1), letterSpanX, letterSpanY);
			}

			// And a little optimisation for superscript/subscript:
			fontLetterHalfHeight = fontLetterHeight / 2f;
			fontLetterHalfWidth = fontLetterWidth / 2f;
			fontLetterDoubleWidth = fontLetterWidth * 2f;

			// Now that is done, proceed to setting up the screen.

			screenTexture = new RenderTexture(screenPixelWidth, screenPixelHeight, 24, RenderTextureFormat.ARGB32);
			Material screenMat = internalProp.FindModelTransform(screenTransform).renderer.material;
			foreach (string layerID in textureLayerID.Split())
				screenMat.SetTexture(layerID.Trim(), screenTexture);

			if (GameDatabase.Instance.ExistsTexture(noSignalTextureURL.EnforceSlashes())) {
				noSignalTexture = GameDatabase.Instance.GetTexture(noSignalTextureURL.EnforceSlashes(), false);
			}

			// Create camera instance...
			CameraStructure = new FlyingCamera(part, screenTexture, cameraAspect);

			// The neat trick. IConfigNode doesn't work. No amount of kicking got it to work.
			// Well, we don't need it. GameDatabase, gimme config nodes for all props!
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("PROP")) {
				// Now, we know our own prop name.
				if (node.GetValue("name") == internalProp.propName) {
					// So this is the configuration of our prop in memory. Nice place.
					// We know it contains at least one MODULE node, us.
					// And we know our moduleID, which is the number in order of being listed in the prop.
					// Therefore the module by that number is our module's own config node.

					ConfigNode moduleConfig = node.GetNodes("MODULE")[moduleID];
					ConfigNode[] pageNodes = moduleConfig.GetNodes("PAGE");

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
							JUtil.LogMessage(this, "Warning - {0}", e);
						}
							
					}

					// Now that all pages are loaded, we can use the moment in the loop to suck in all the extra fonts.
					foreach (string value in moduleConfig.GetValues("extraFont")) {
						fontTexture.Add(LoadFont(this, internalProp, value, true));
					}

					break;
				}
			}
			JUtil.LogMessage(this, "Done setting up pages, {0} pages ready.", pages.Count);

			// Load our state from storage...
			persistentVarName = "activePage" + internalProp.propID;
			persistence = new PersistenceAccessor(part);
			int? activePageID = persistence.GetVar(persistentVarName);
			if (activePageID != null && activePageID.Value < pages.Count) {
				activePage = pages[activePageID.Value];
			}
			activePage.Active(true);

			// If we have global buttons, set them up.
			if (!string.IsNullOrEmpty(globalButtons)) {
				string[] tokens = globalButtons.Split(',');
				for (int i = 0; i < tokens.Length; i++) {
					string buttonName = tokens[i].Trim();
					// Notice that holes in the global button list ARE legal.
					if (!string.IsNullOrEmpty(buttonName))
						SmarterButton.CreateButton(internalProp, buttonName, i, GlobalButtonClick, GlobalButtonRelease);
				}
			}

			audioOutput = JUtil.SetupIVASound(internalProp, buttonClickSound, buttonClickVolume, false);
			startupComplete = true;
		}

		private static void PlayClickSound(FXGroup audioOutput)
		{
			if (audioOutput != null) {
				audioOutput.audio.Play();
			}
		}

		public void GlobalButtonClick(int buttonID)
		{
			if (needsElectricCharge && electricChargeReserve < 0.01d)
				return;
			if (activePage.GlobalButtonClick(buttonID))
				PlayClickSound(audioOutput);
		}

		public void GlobalButtonRelease(int buttonID)
		{
			// Or do we allow a button release to have effects?
			/* Mihara: Yes, I think we should. Otherwise if the charge
			 * manages to run out in the middle of a pressed button, it will never stop.
			if (needsElectricCharge && electricChargeReserve < 0.01d)
				return;
			*/
			activePage.GlobalButtonRelease(buttonID);
		}

		private MonitorPage FindPageByName(string pageName)
		{
			if (!string.IsNullOrEmpty(pageName)) {
				foreach (MonitorPage page in pages) {
					if (page.name == pageName)
						return page;
				}
			}
			return null;
		}

		public void PageButtonClick(MonitorPage triggeredPage)
		{
			if (needsElectricCharge && electricChargeReserve < 0.01d)
				return;
			// Apply page redirect like this:
			triggeredPage = FindPageByName(activePage.ContextRedirect(triggeredPage.name)) ?? triggeredPage;
			if (triggeredPage != activePage && (activePage.SwitchingPermitted(triggeredPage.name) || triggeredPage.unlocker)) {
				activePage.Active(false);
				activePage = triggeredPage;
				activePage.Active(true);
				persistence.SetVar(persistentVarName, activePage.pageNumber);
				refreshDrawCountdown = refreshTextCountdown = 0;
				comp.updateForced = true;
				firstRenderComplete = false;
				PlayClickSound(audioOutput);
			}
		}

		private void DrawChar(char letter, float x, float y, Color letterColor, Script scriptType, Width fontWidth)
		{

			if (fontCharacters.ContainsKey(letter)) {
				// This is complicated.
				// The destination rectangle has coordinates given in pixels, from top left corner of the texture.
				// The source rectangle has coordinates in normalised texture coordinates (!) from bottom left corner of the texture!
				// And without the LoadPixelMatrix, DrawTexture produces nonsense anyway.
				Graphics.DrawTexture(
					new Rect(x, (scriptType == Script.Subscript) ? y + fontLetterHalfHeight : y, 
						(fontWidth == Width.Normal ? fontLetterWidth : (fontWidth == Width.Half ? fontLetterHalfWidth : fontLetterDoubleWidth)),
						(scriptType != Script.Normal) ? fontLetterHalfHeight : fontLetterHeight),
					fontTexture[fontTextureIndex],
					fontCharacters[letter],
					0, 0, 0, 0,
					letterColor
				);
			} else {
				if (!characterWarnings[letter]) {
					JUtil.LogMessage(this, "Warning: Attempted to print a character \"{0}\" not present in the font.",
						letter.ToString());
					characterWarnings[letter] = true;
				}
			}
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
			RenderTexture backupRenderTexture = RenderTexture.active;

			if (!screenTexture.IsCreated())
				screenTexture.Create();
			screenTexture.DiscardContents();
			RenderTexture.active = screenTexture;

			if (needsElectricCharge && electricChargeReserve < 0.01d) {
				// If we're out of electric charge, we're drawing a blank screen.
				GL.Clear(true, true, emptyColorValue);
				RenderTexture.active = backupRenderTexture;
				return;
			}

			// This is the important witchcraft. Without that, DrawTexture does not print where we expect it to.
			// Cameras don't care because they have their own matrices, but DrawTexture does.
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, screenPixelWidth, screenPixelHeight, 0);

			// Actual rendering of the background is delegated to the page object.
			activePage.RenderBackground(screenTexture);

			if (!string.IsNullOrEmpty(activePage.Text)) {
				float yCursor = activePage.screenYMin * fontLetterHeight;
				for (int lineIndex = 0; lineIndex < screenBuffer.Length; yCursor += fontLetterHeight, lineIndex++) {
					if (!string.IsNullOrEmpty(screenBuffer[lineIndex])) {
						Color fontColor = activePage.defaultColor;
						float xOffset = 0;
						float yOffset = 0;
						Script scriptType = Script.Normal;
						Width fontWidth = Width.Normal;
						fontTextureIndex = 0;
						if (activePage.pageFont < fontTexture.Count)
							fontTextureIndex = activePage.pageFont;
						float xCursor = activePage.screenXMin * fontLetterWidth;
						for (int charIndex = 0; charIndex < screenBuffer[lineIndex].Length; charIndex++) {
							bool escapedBracket = false;
							// We will continue parsing bracket pairs until we're out of bracket pairs,
							// since all of them -- except the escaped bracket tag --
							// consume characters and change state without actually generating any output.
							while (charIndex < screenBuffer[lineIndex].Length && screenBuffer[lineIndex][charIndex] == '[') {
								// If there's no closing bracket, we stop parsing and go on to printing.
								int nextBracket = screenBuffer[lineIndex].IndexOf(']', charIndex) - charIndex;
								if (nextBracket < 1)
									break;
								// Much easier to parse it this way, although I suppose more expensive.
								string tagText = screenBuffer[lineIndex].Substring(charIndex + 1, nextBracket - 1);
								if ((tagText.Length == 9 || tagText.Length == 7) && tagText[0] == '#') {
									// Valid color tags are [#rrggbbaa] or [#rrggbb].
									fontColor = JUtil.HexRGBAToColor(tagText.Substring(1));
									charIndex += nextBracket + 1;
								} else if (tagText.Length > 2 && tagText[0] == '@') {
									// Valid nudge tags are [@x<number>] or [@y<number>] so the conditions for them is that
									// the next symbol is @ and there are at least three, one designating the axis.
									float coord;
									if (float.TryParse(tagText.Substring(2), out coord)) {
										switch (tagText[1]) {
											case 'X':
											case 'x':
												xOffset = coord;
												break;
											case 'Y':
											case 'y':
												yOffset = coord;
												break;
										}
										// We only consume the symbols if they did parse correctly.
										charIndex += nextBracket + 1;
									} else //If it didn't parse, skip over it.
										break;
								} else if (tagText == "sup") {
									// Superscript!
									scriptType = Script.Superscript;
									charIndex += nextBracket + 1;
								} else if (tagText == "sub") {
									// Subscript!
									scriptType = Script.Subscript;
									charIndex += nextBracket + 1;
								} else if (tagText == "/sup" || tagText == "/sub") {
									// And back...
									scriptType = Script.Normal;
									charIndex += nextBracket + 1;
								} else if (tagText == "hw") {
									fontWidth = Width.Half;
									charIndex += nextBracket + 1;
								} else if (tagText == "dw") {
									fontWidth = Width.Double;
									charIndex += nextBracket + 1;
								} else if (tagText == "/hw" || tagText == "/dw") {
									// And back...
									fontWidth = Width.Normal;
									charIndex += nextBracket + 1;
								} else if (tagText.StartsWith("font", StringComparison.Ordinal)) {
									uint newFontID;
									if (uint.TryParse(tagText.Substring(4), out newFontID) && newFontID < fontTexture.Count) {
										fontTextureIndex = (int)newFontID;
									}
									charIndex += nextBracket + 1;
								} else if (tagText == "[") {
									// We got a "[[]" which means an escaped opening bracket.
									escapedBracket = true;
									charIndex += nextBracket;
									break;
								} else // Else we didn't recognise anything so it's not a tag.
									break;
							}
							float xPos = xCursor + xOffset;
							float yPos = yCursor + yOffset;
							if (charIndex < screenBuffer[lineIndex].Length &&
							    xPos < screenPixelWidth &&
							    xPos > -(fontWidth == Width.Normal ? fontLetterWidth : (fontWidth == Width.Half ? fontLetterHalfWidth : fontLetterDoubleWidth)) &&
							    yPos < screenPixelHeight &&
							    yPos > -fontLetterHeight)
								DrawChar(escapedBracket ? '[' : screenBuffer[lineIndex][charIndex], xPos, yPos, fontColor, scriptType, fontWidth);
							switch (fontWidth) {
								case Width.Normal:
									xCursor += fontLetterWidth;
									break;
								case Width.Half:
									xCursor += fontLetterHalfWidth;
									break;
								case Width.Double:
									xCursor += fontLetterDoubleWidth;
									break;

							}
						}
					}
				}
			}

			activePage.RenderOverlay(screenTexture);
			GL.PopMatrix();

			RenderTexture.active = backupRenderTexture;
		}

		private void FillScreenBuffer()
		{
			screenBuffer = new string[screenHeight];
			string[] linesArray = activePage.Text.Split(JUtil.lineSeparator, StringSplitOptions.None);
			for (int i = 0; i < screenHeight; i++)
				screenBuffer[i] = (i < linesArray.Length) ? StringProcessor.ProcessString(linesArray[i], comp) : string.Empty;
			textRefreshRequired = false;

			// This is where we request electric charge reserve. And if we don't have any, well... :)
			CheckForElectricCharge();
		}

		private void CheckForElectricCharge()
		{
			if (needsElectricCharge)
				electricChargeReserve = (double)comp.ProcessVariable("ELECTRIC");
		}

		public override void OnUpdate()
		{

			if (!JUtil.VesselIsInIVA(vessel))
				return; 

			// Screenshots need to happen in at this moment, because otherwise they may miss.
			if (GameSettings.TAKE_SCREENSHOT.GetKeyDown() && part.ActiveKerbalIsLocal()) {
				// Let's try to save a screenshot.
				JUtil.LogMessage(this, "SCREENSHOT!");

				string screenshotName = string.Format("{0}{1}{2:yyyy-MM-dd_HH-mm-ss}_{4}_{3}.png",
					                        KSPUtil.ApplicationRootPath, "Screenshots/monitor", DateTime.Now, internalProp.propID, part.uid);
				var screenshot = new Texture2D(screenTexture.width, screenTexture.height);
				RenderTexture backupRenderTexture = RenderTexture.active;
				RenderTexture.active = screenTexture;
				screenshot.ReadPixels(new Rect(0, 0, screenTexture.width, screenTexture.height), 0, 0);
				RenderTexture.active = backupRenderTexture;
				var bytes = screenshot.EncodeToPNG();
				Destroy(screenshot);
				File.WriteAllBytes(screenshotName, bytes);
			}

			if (!UpdateCheck())
				return;

			if (!activePage.isMutable) { 
				// In case the page is empty and has no camera, the screen is treated as turned off and blanked once.
				if (!firstRenderComplete) {
					FillScreenBuffer();
					RenderScreen();
					firstRenderComplete = true;
				} else {
					CheckForElectricCharge();
					if (needsElectricCharge && electricChargeReserve < 0.01d)
						RenderScreen();
				}
			} else {
				if (textRefreshRequired)
					FillScreenBuffer();
				RenderScreen();
				firstRenderComplete = false;
			}

		}

		public void LateUpdate()
		{
			if (JUtil.VesselIsInIVA(vessel) && !startupComplete)
				JUtil.AnnoyUser(this);
		}
	}
}

