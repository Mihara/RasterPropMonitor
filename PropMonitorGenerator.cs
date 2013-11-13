using System;
using System.IO;
using UnityEngine;
using System.Reflection;

namespace JSI
{
	public class RasterPropMonitorGenerator: InternalModule
	{
		[KSPField]
		public int refreshRate = 5;
		[KSPField]
		public int refreshDataRate = 10;
		// I wish I could get rid of this particular mess of fields, because in theory I can support an unlimited number of pages.
		// I could also parse a Very Long String, but that would make using ModuleManager cumbersome.
		// Explanations how to correctly read config nodes directly in OnLoad would be welcome.
		[KSPField]
		public string page1 = "Display$$$ not$$$  configured.";
		[KSPField]
		public string button1 = string.Empty;
		[KSPField]
		public string page2 = string.Empty;
		[KSPField]
		public string button2 = string.Empty;
		[KSPField]
		public string page3 = string.Empty;
		[KSPField]
		public string button3 = string.Empty;
		[KSPField]
		public string page4 = string.Empty;
		[KSPField]
		public string button4 = string.Empty;
		[KSPField]
		public string page5 = string.Empty;
		[KSPField]
		public string button5 = string.Empty;
		[KSPField]
		public string page6 = string.Empty;
		[KSPField]
		public string button6 = string.Empty;
		[KSPField]
		public string page7 = string.Empty;
		[KSPField]
		public string button7 = string.Empty;
		[KSPField]
		public string page8 = string.Empty;
		[KSPField]
		public string button8 = string.Empty;
		[KSPField]
		public int activePage = 0;
		[KSPField]
		public string camera1 = null;
		[KSPField]
		public string camera2 = null;
		[KSPField]
		public string camera3 = null;
		[KSPField]
		public string camera4 = null;
		[KSPField]
		public string camera5 = null;
		[KSPField]
		public string camera6 = null;
		[KSPField]
		public string camera7 = null;
		[KSPField]
		public string camera8 = null;
		// Config syntax.
		private readonly string[] lineSeparator = { Environment.NewLine };
		private readonly string[] variableListSeparator = { "$&$" };
		private readonly string[] variableSeparator = { };
		// Local variables
		private string[] textArray;
		private string[] pages = { "", "", "", "", "", "", "", "" };
		private Func<string>[] pageHandlers = {null,null,null,null,null,null,null,null};
		private string[] cameras;
		private int updateCountdown = 0;
		private bool updateForced = false;
		private bool screenWasBlanked = false;
		private bool currentPageIsMutable = false;
		private bool currentPageFirstPassComplete = false;
		// All computations are split into a separate class, because it was getting a mite too big.
		private RasterPropMonitorComputer comp;
		// Persistence for current page variable.
		private PersistenceAccessor persistence;
		private string persistentVarName;
		private RasterPropMonitor ourScreen;
		private SIFormatProvider fp = new SIFormatProvider();

		private static void LogMessage(string line, params object[] list)
		{
			Debug.Log(String.Format(typeof(RasterPropMonitorGenerator).Name + ": " + line, list));
		}

		public void Start()
		{
			ourScreen = internalProp.FindModelComponent<RasterPropMonitor>();
			if (ourScreen == null) {
				LogMessage("Could not find a screen module in my prop. Expect errors.");
			}

			string[] pageData = { page1, page2, page3, page4, page5, page6, page7, page8 };
			string[] buttonName = {	button1, button2, button3, button4, button5, button6, button7, button8 };


			for (int i = 0; i < 8; i++) {
				if (!string.IsNullOrEmpty(buttonName[i])) 
					SmarterButton.CreateButton(internalProp, buttonName[i], i, ButtonClick);

				try {
					pages[i] = String.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + pageData[i], System.Text.Encoding.UTF8));
				} catch {
					// Notice that this will also happen if the referenced file is not found.

					// Now we check for a page handler.
					string[] tokens = pageData[i].Split(',');
					if (tokens.Length == 2) {
						foreach (InternalModule thatModule in internalProp.internalModules) {
							if (thatModule.ClassName == tokens[0].Trim()) {
								foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
									if (m.Name == tokens[1].Trim()) {
										// We'll assume whoever wrote it is not being an idiot today.
										LogMessage("Found page handler {0}, using method {1}.",tokens[0].Trim(),tokens[1].Trim());
										pageHandlers[i] = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), thatModule, m);
										break;
									}
								}
								break;
							}
						}
					}

					// But regardless of whether we found a page handler, it won't matter if we populate the page data or not.
					pages[i] = pageData[i].Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
				}
			}

			textArray = new string[ourScreen.screenHeight];

			comp = JUtil.GetComputer(internalProp);

			comp.UpdateRefreshRates(refreshRate, refreshDataRate);

			// Load our state from storage...
			persistentVarName = "activePage" + internalProp.propID;
			persistence = new PersistenceAccessor(part);
			activePage = persistence.GetVar(persistentVarName) ?? activePage;

			// So camera support.
			cameras = new [] { camera1, camera2, camera3, camera4, camera5,	camera6, camera7, camera8 };
			SetCamera(cameras[activePage]);
		}

		private void SetCamera(string cameraTransform)
		{
			if (!string.IsNullOrEmpty(cameraTransform)) {
				string[] tokens = cameraTransform.Split(',');
				if (tokens.Length == 2) {
					float fov;
					float.TryParse(tokens[1], out fov);
					ourScreen.SendCamera(tokens[0].Trim(), fov);
				} else
					ourScreen.SendCamera(cameraTransform);
			} else {
				ourScreen.SendCamera(null);
			}
		}

		public void ButtonClick(int buttonID)
		{
			if (buttonID != activePage) {
				activePage = buttonID;
				persistence.SetVar(persistentVarName, activePage);
				SetCamera(cameras[activePage]);
				updateForced = true;
				comp.updateForced = true;
				if (!string.IsNullOrEmpty(cameras[activePage]))
					currentPageIsMutable = true;
				else
					currentPageIsMutable = false;
				currentPageFirstPassComplete = false;
			}
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

					object[] variables = new object[vars.Length];
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

			if ((CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal) &&
			    vessel == FlightGlobals.ActiveVessel) {

				if (!UpdateCheck())
					return;

				// Check if we have a page handler for this page.
				// If we do, now we ask it for the page text.
				if (pageHandlers[activePage] != null) {
					pages[activePage] = pageHandlers[activePage]();
					currentPageFirstPassComplete = false;
				}

				if (pages[activePage] == string.Empty && !currentPageIsMutable) { 
					// In case the page is empty and has no camera, the screen is treated as turned off and blanked once.
					if (!screenWasBlanked) {
						textArray = new string[ourScreen.screenHeight];
						ourScreen.SendPage(textArray);
						screenWasBlanked = true;
					}
				} else {
					if (!currentPageFirstPassComplete || currentPageIsMutable) {
						string[] linesArray = pages[activePage].Split(lineSeparator, StringSplitOptions.None);
						for (int i = 0; i < ourScreen.screenHeight; i++) {
							textArray[i] = (i < linesArray.Length) ? ProcessString(linesArray[i]).TrimEnd() : string.Empty;
						}
						ourScreen.SendPage(textArray);
						screenWasBlanked = false;
						currentPageFirstPassComplete = true;
					}
				}

			}
		}
	}


}

