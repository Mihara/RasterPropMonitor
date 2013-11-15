using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace JSI
{
	public class RasterPropMonitorGenerator: InternalModule
	{
		[KSPField]
		public int refreshRate = 5;
		[KSPField]
		public int refreshDataRate = 10;
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
		private RasterPropMonitor ourScreen;
		private readonly SIFormatProvider fp = new SIFormatProvider();

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
					if (pages.Count == 0) {
						LogMessage("Argh, no pages were defined!");
					}
				}
			}

			// Maybe I need an extra parameter to set the initially active page.

			textArray = new string[ourScreen.screenHeight];

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

			if ((CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal) &&
			    vessel == FlightGlobals.ActiveVessel) {

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
						textArray = new string[ourScreen.screenHeight];
						ourScreen.SendPage(textArray);
						screenWasBlanked = true;
					}
				} else {
					if (!currentPageFirstPassComplete || currentPageIsMutable) {
						string[] linesArray = activePage.Text.Split(lineSeparator, StringSplitOptions.None);
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

