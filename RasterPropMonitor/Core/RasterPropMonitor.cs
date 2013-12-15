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
		public Color32 emptyColor = Color.clear;
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
		public Color32 defaultFontTint = Color.white;
		[KSPField]
		public string noSignalTextureURL = string.Empty;
		// This needs to be public so that pages can point it.
		public FlyingCamera CameraStructure;
		// Some things in life are constant;
		private const int firstCharacter = 32;
		// Internal stuff.
		private Texture2D fontTexture;
		private RenderTexture screenTexture;
		// Page definition syntax.
		private readonly string[] lineSeparator = { Environment.NewLine };
		// Local variables
		private int refreshDrawCountdown;
		private int refreshTextCountdown;
		private int vesselNumParts;
		private bool firstRenderComplete;
		private bool textRefreshRequired;
		private readonly List<MonitorPage> pages = new List<MonitorPage>();
		private MonitorPage activePage;
		// All computations are split into a separate class, because it was getting a mite too big.
		private RasterPropMonitorComputer comp;
		// Persistence for current page variable.
		private PersistenceAccessor persistence;
		private string persistentVarName;
		private string[] screenBuffer;
		private Rect[] fontCharacters;
		private FXGroup audioOutput;
		private double electricChargeReserve;
		public Texture2D noSignalTexture;
		private readonly DefaultableDictionary<int,bool> characterWarnings = new DefaultableDictionary<int, bool>(false);

		public void Start()
		{
			InstallationPathWarning.Warn();

			// Install the calculator module.
			comp = RasterPropMonitorComputer.Instantiate(internalProp);
			comp.UpdateRefreshRates(refreshTextRate, refreshDataRate);

			// Loading the font...
			JUtil.LogMessage(this, "Trying to locate \"{0}\" in GameDatabase...", fontTransform);
			if (GameDatabase.Instance.ExistsTexture(fontTransform.EnforceSlashes())) {
				fontTexture = GameDatabase.Instance.GetTexture(fontTransform.EnforceSlashes(), false);
				JUtil.LogMessage(this, "Loading font texture from URL, \"{0}\"", fontTransform);
			} else {
				fontTexture = (Texture2D)internalProp.FindModelTransform(fontTransform).renderer.material.mainTexture;
				JUtil.LogMessage(this, "Loading font texture from a transform named, \"{0}\"", fontTransform);
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
							JUtil.LogMessage(this, "Warning - {0}", e);
						}
							
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
					SmarterButton.CreateButton(internalProp, tokens[i].Trim(), i, GlobalButtonClick, GlobalButtonRelease);
				}
			}

			audioOutput = JUtil.SetupIVASound(internalProp, buttonClickSound, buttonClickVolume, false);

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
			activePage.GlobalButtonClick(buttonID);
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

		public void PageButtonClick(MonitorPage triggeredPage)
		{
			if (needsElectricCharge && electricChargeReserve < 0.01d)
				return;
			if (triggeredPage != activePage) {
				activePage.Active(false);
				activePage = triggeredPage;
				activePage.Active(true);
				persistence.SetVar(persistentVarName, activePage.pageNumber);
				refreshDrawCountdown = refreshTextCountdown = 0;
				comp.updateForced = true;
				firstRenderComplete = false;
			}
			PlayClickSound(audioOutput);
		}

		private void DrawChar(char letter, int x, int y, Color32 letterColor)
		{
			int charCode = (ushort)letter;
			// Clever bit.
			if (charCode >= 128)
				charCode -= 32;

			charCode -= firstCharacter;

			if ((charCode < 0 || charCode >= fontCharacters.Length) && !characterWarnings[charCode]) {
				JUtil.LogMessage(this, "Warning: Attempted to print a character \"{0}\" not present in the font, raw value \"{1}\"",
					letter.ToString(), Convert.ToUInt16(letter));
				characterWarnings[charCode] = true;
				return;
			}

			// This is complicated.
			// The destination rectangle has coordinates given in pixels, from top left corner of the texture.
			// The source rectangle has coordinates in floats (!) from bottom left corner of the texture!
			// And without the LoadPixelMatrix, DrawTexture produces nonsense anyway.
			Graphics.DrawTexture(
				new Rect(x, y, fontLetterWidth, fontLetterHeight),
				fontTexture,
				fontCharacters[charCode],
				0, 0, 0, 0,
				letterColor
			);

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
				GL.Clear(true, true, emptyColor);
				RenderTexture.active = backupRenderTexture;
				return;
			}

			// Actual rendering of the background is delegated to the page object.
			activePage.RenderBackground(screenTexture);

			// This is the important witchcraft. Without that, DrawTexture does not print where we expect it to.
			// Cameras don't care because they have their own matrices, but DrawTexture does.
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, screenPixelWidth, screenPixelHeight, 0);

			if (!string.IsNullOrEmpty(activePage.Text)) {
				for (int yCursor = 0, lineIndex = 0; lineIndex < screenBuffer.Length; yCursor += fontLetterHeight, lineIndex++) {
					if (!string.IsNullOrEmpty(screenBuffer[lineIndex])) {
						Color32 fontColor = defaultFontTint;
						int xOffset = 0;
						int yOffset = 0;
						for (int charIndex = 0, xCursor = 0; charIndex < screenBuffer[lineIndex].Length; charIndex++, xCursor += fontLetterWidth) {
							bool escapedBracket = false;
							// We will continue parsing bracket pairs until we're out of bracket pairs.
							while (screenBuffer[lineIndex][charIndex] == '[') {
								// If there's no closing bracket, we stop parsing and go on to printing.
								int nextBracket = screenBuffer[lineIndex].IndexOf(']', charIndex) - charIndex;
								if (nextBracket < 0)
									break;

								if (screenBuffer[lineIndex].IndexOf('#', charIndex) - charIndex == 1 && (nextBracket == 10 || nextBracket == 8)) {
									// Valid color tags are [#rrggbbaa] or [#rrggbb].
									// So the conditions for them is that the next character is # and the next bracket 
									// is either 8 or 10 symbols away.
									fontColor = JUtil.HexRGBAToColor(screenBuffer[lineIndex].Substring(charIndex + 2, nextBracket - 2));
									charIndex += nextBracket + 1;
								} else if (screenBuffer[lineIndex].IndexOf('@', charIndex) - charIndex == 1 && nextBracket > 3) {
									// Valid nudge tags are [@x<number>] or [@y<number>] so the conditions for them is that
									// the next symbol is @ and there is at least one symbol before it.
									int coord;
									if (Int32.TryParse(screenBuffer[lineIndex].Substring(charIndex + 3, nextBracket - 3), out coord)) {
										switch (screenBuffer[lineIndex][charIndex + 2]) {
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
								} else if (nextBracket == 2 && screenBuffer[lineIndex][charIndex + 1] == '[') {
									// We got a "[[]" which means an escaped opening bracket.
									// TODO: Trying to escape many bracket pairs in a row will confuse it.
									// I doubt most people would want to do that, but I'm not sure how to deal with that correctly.
									escapedBracket = true;
									charIndex += nextBracket;
								} else
									break;
							}
							int xPos = xCursor + xOffset;
							int yPos = yCursor + yOffset;
							if (charIndex < screenBuffer[lineIndex].Length &&
							    xPos < screenPixelWidth &&
							    xPos > -fontLetterWidth &&
							    yPos < screenPixelHeight &&
							    yPos > -fontLetterHeight)
								DrawChar(escapedBracket ? '[' : screenBuffer[lineIndex][charIndex], xPos, yPos, fontColor);

						}
					}
				}
			}

			GL.PopMatrix();

			RenderTexture.active = backupRenderTexture;
		}

		private void FillScreenBuffer()
		{
			screenBuffer = new string[screenHeight];
			string[] linesArray = activePage.Text.Split(lineSeparator, StringSplitOptions.None);
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
			if (!JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
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
	}
}

