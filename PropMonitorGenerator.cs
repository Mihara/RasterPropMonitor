using System;
using System.IO;
using System.Reflection;
using UnityEngine;

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
		private string[] lineSeparator = { Environment.NewLine };
		private string[] variableListSeparator = { "$&$" };
		private string[] variableSeparator = { };
		// Local variables
		private string[] textArray;
		private string[] pages = { "", "", "", "", "", "", "", "" };
		private string[] cameras;
		private int updateCountdown = 0;
		private bool updateForced = false;
		private bool screenWasBlanked = false;
		private bool currentPageIsMutable = false;
		private bool currentPageFirstPassComplete = false;
		// All computations are split into a separate class, because it was getting a mite too big.
		public RasterPropMonitorComputer comp;
		// Persistence for current page variable.
		private JSIInternalPersistence persistence = null;
		private string persistentVarName;

		private RasterPropMonitor ourScreen;

		public void Start()
		{


			ourScreen = internalProp.FindModelComponent<RasterPropMonitor>();

			string[] pageData = new string[] { page1, page2, page3, page4, page5, page6, page7, page8 };
			string[] buttonName = new string[] { button1, button2, button3, button4, button5, button6, button7, button8 };

			for (int i=0; i<8; i++) {
				if (!string.IsNullOrEmpty(buttonName[i])) {
					GameObject buttonObject = base.internalProp.FindModelTransform(buttonName[i]).gameObject;
					ButtonHandler pageButton = buttonObject.AddComponent<ButtonHandler>();
					pageButton.ID = i;
					pageButton.handlerFunction = ButtonClick;
				}

				try {
					pages[i] = String.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + pageData[i], System.Text.Encoding.UTF8));
				} catch {
					// Notice that this will also happen if the referenced file is not found.
					pages[i] = pageData[i].Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
				}
			}


			textArray = new string[ourScreen.screenHeight];
			for (int i = 0; i < textArray.Length; i++) {
				textArray[i] = string.Empty;
			}

			// The semi-clever bit: Recycling computational module.
			if (part != null) {
				foreach (InternalProp prop in part.internalModel.props) {
					RasterPropMonitorComputer other = prop.FindModelComponent<RasterPropMonitorComputer>();
					if (other != null) {
						Debug.Log("RasterPropMonitorGenerator: Found an existing calculator instance, using that.");
						comp = other;
						break;
					}
				}
			}

			if (comp == null) {
				Debug.Log("RasterPropMonitorGenerator: Instantiating a new calculator.");
				base.internalProp.AddModule("RasterPropMonitorComputer");
				comp = base.internalProp.FindModelComponent<RasterPropMonitorComputer>();
				if (comp == null) {
					Debug.Log("RasterPropMonitorGenerator: Failed to instantiate a calculator, wtf?");
				}
			}

			comp.UpdateRefreshRates(refreshRate, refreshDataRate);

			// Load our state from storage...
			persistentVarName = "activePage" + internalProp.propID.ToString();
			if (persistence == null)
				for (int i=0; i<part.Modules.Count; i++)
					if (part.Modules[i].ClassName == typeof(JSIInternalPersistence).Name)
						persistence = part.Modules[i] as JSIInternalPersistence;
			int retval = persistence.GetVar(persistentVarName);

			if (retval != int.MaxValue)
				activePage = retval;

			// So camera support.
			cameras = new string[] { camera1, camera2, camera3, camera4, camera5, camera6, camera7, camera8 };
			SetCamera(cameras[activePage]);
		}

		private void SetCamera(string cameraTransform)
		{
			if (!string.IsNullOrEmpty(cameraTransform)) {
				string[] tokens = cameraTransform.Split(',');
				if (tokens.Length == 2) {
					float fov;
					float.TryParse(tokens[1], out fov);
					ourScreen.SendCamera(tokens[0].Trim(),fov);
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

				if (persistence != null) {
					persistence.SetVar(persistentVarName, activePage);
				}

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
					for (int i=0; i<vars.Length; i++) {
						variables[i] = comp.ProcessVariable(vars[i]);
					}
					return String.Format(tokens[0], variables);
				}
			} else
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

				if (pages[activePage] == string.Empty && !currentPageIsMutable) { // In case the page is empty and has no camera, the screen is treated as turned off and blanked once.
					if (!screenWasBlanked) {
						for (int i = 0; i < textArray.Length; i++)
							textArray[i] = string.Empty;
						screenWasBlanked = true;
						ourScreen.SendPage(textArray);
					}
				} else {
					if (!currentPageFirstPassComplete || currentPageIsMutable) {
						string[] linesArray = pages[activePage].Split(lineSeparator, StringSplitOptions.None);
						for (int i=0; i<ourScreen.screenHeight; i++) {
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

	public class ButtonHandler:MonoBehaviour
	{
		public delegate void HandlerFunction(int ID);

		public HandlerFunction handlerFunction;
		public int ID;

		public void OnMouseDown()
		{
			handlerFunction(ID);
		}
	}
}

