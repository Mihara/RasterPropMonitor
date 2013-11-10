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
		// Apparently, IConfigNode is one more thing that doesn't quite work for InternalModule,
		// or if it does, I can't tell how to make it work.
		// This is discrimination, I say!
		[KSPField]
		public string page1 = "Display$$$ not$$$  configured.";
		[KSPField]
		public string button1 = "";
		[KSPField]
		public string page2 = "";
		[KSPField]
		public string button2 = "";
		[KSPField]
		public string page3 = "";
		[KSPField]
		public string button3 = "";
		[KSPField]
		public string page4 = "";
		[KSPField]
		public string button4 = "";
		[KSPField]
		public string page5 = "";
		[KSPField]
		public string button5 = "";
		[KSPField]
		public string page6 = "";
		[KSPField]
		public string button6 = "";
		[KSPField]
		public string page7 = "";
		[KSPField]
		public string button7 = "";
		[KSPField]
		public string page8 = "";
		[KSPField]
		public string button8 = "";
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
		// Important pointers to the screen's data structures.
		private InternalModule targetScript;
		FieldInfo remoteArray;
		FieldInfo remoteFlag;
		FieldInfo remoteCameraName;
		FieldInfo remoteCameraSet;
		FieldInfo remoteCameraFov;
		// Local variables
		private string[] textArray;
		private string[] pages = { "", "", "", "", "", "", "", "" };
		private string[] cameras;
		private int charPerLine = 23;
		private int linesPerPage = 17;
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

		public void Start ()
		{
			// Mihara: We're getting at the screen module and it's parameters using reflection here.
			// While I would prefer to use some message passing mechanism instead,
			// it does not look like I can use KSPEvent.
			// I could directly lock at the parameters, seeing as how these two modules
			// are in the same assembly, but instead I'm leaving the reflection-based mechanism here
			// so that you could make your own screen driver module
			// by simply copy-pasting the relevant sections.
			//
			// Once you have that you're golden -- you can populate the array of lines,
			// and trigger the screen update by writing a boolean when it needs updating.
			//
			// Cameras are part of the monitor class and are controlled in a very similar way --
			// you send a transform name, a float, and a boolean, and the rest is the problem for some
			// other class.
			foreach (InternalModule intModule in base.internalProp.internalModules) {
				if (intModule.ClassName == "RasterPropMonitor") {
					targetScript = intModule;
					// These are for text.
					remoteArray = intModule.GetType ().GetField ("screenText");
					remoteFlag = intModule.GetType ().GetField ("screenUpdateRequired");
					// And these are to tell which camera to show on the background!
					remoteCameraName = intModule.GetType ().GetField ("cameraName");
					remoteCameraSet = intModule.GetType ().GetField ("setCamera");
					remoteCameraFov = intModule.GetType ().GetField ("fov");

					charPerLine = (int)intModule.GetType ().GetField ("screenWidth").GetValue (intModule);
					linesPerPage = (int)intModule.GetType ().GetField ("screenHeight").GetValue (intModule);

					break;
				}
			}

			// Everything from there on is just my idea of doing it and can be done in a myriad different ways.
			// If InternalModule class wasn't such an odd entity, I could probably even name some of them.

			string[] pageData = new string[] { page1, page2, page3, page4, page5, page6, page7, page8 };
			string[] buttonName = new string[] { button1, button2, button3, button4, button5, button6, button7, button8 };

			for (int i=0; i<8; i++) {
				if (buttonName [i] != "") {
					GameObject buttonObject = base.internalProp.FindModelTransform (buttonName [i]).gameObject;
					buttonHandler pageButton = buttonObject.AddComponent<buttonHandler> ();
					pageButton.ID = i;
					pageButton.handlerFunction = ButtonClick;
				}

				try {
					pages [i] = String.Join (Environment.NewLine, File.ReadAllLines (KSPUtil.ApplicationRootPath + "GameData/" + pageData [i], System.Text.Encoding.UTF8));
				} catch {
					// Notice that this will also happen if the referenced file is not found.
					pages [i] = pageData [i].Replace ("<=", "{").Replace ("=>", "}").Replace ("$$$", Environment.NewLine);
				}
			}


			textArray = new string[linesPerPage];
			for (int i = 0; i < textArray.Length; i++) {
				textArray [i] = "";
			}

			// The semi-clever bit: Recycling computational module.
			if (part != null) {
				foreach (InternalProp prop in part.internalModel.props) {
					RasterPropMonitorComputer other = prop.FindModelComponent<RasterPropMonitorComputer> ();
					if (other != null) {
						Debug.Log ("RasterPropMonitorGenerator: Found an existing calculator instance, using that.");
						comp = other;
						break;
					}
				}
			}

			if (comp == null) {
				Debug.Log ("RasterPropMonitorGenerator: Instantiating a new calculator.");
				base.internalProp.AddModule ("RasterPropMonitorComputer");
				comp = base.internalProp.FindModelComponent<RasterPropMonitorComputer> ();
				if (comp == null) {
					Debug.Log ("RasterPropMonitorGenerator: Failed to instantiate a calculator, wtf?");
				}
			}

			comp.UpdateRefreshRates (refreshRate, refreshDataRate);

			// Load our state from storage...
			persistentVarName = "activePage" + internalProp.propID.ToString ();
			if (persistence == null)
				for (int i=0; i<part.Modules.Count; i++)
					if (part.Modules [i].ClassName == typeof(JSIInternalPersistence).Name)
						persistence = part.Modules [i] as JSIInternalPersistence;
			int retval = persistence.GetVar (persistentVarName);

			if (retval != int.MaxValue)
				activePage = retval;

			// So camera support.
			cameras = new string[] { camera1, camera2, camera3, camera4, camera5, camera6, camera7, camera8 };
			SetCamera (cameras [activePage]);
		}

		private void SetCamera (string name)
		{
			if (name != "" && name != null) {
				string[] tokens = name.Split (',');
				if (tokens.Length == 2) {
					float fov;
					if (!float.TryParse (tokens [1], out fov))
						fov = 60;
					remoteCameraFov.SetValue (targetScript, fov);
					name = tokens [0].Trim ();
				}
				remoteCameraName.SetValue (targetScript, name);
				remoteCameraSet.SetValue (targetScript, true);
			} else {
				remoteCameraName.SetValue (targetScript, null);
				remoteCameraSet.SetValue (targetScript, true);
			}
		}

		public void ButtonClick (int buttonID)
		{
			activePage = buttonID;

			if (persistence != null) {
				persistence.SetVar (persistentVarName,activePage);
			}

			SetCamera (cameras [activePage]);
			updateForced = true;
			comp.updateForced = true;
			if (cameras [activePage] != "" && cameras [activePage] != null)
				currentPageIsMutable = true;
			else
				currentPageIsMutable = false;
			currentPageFirstPassComplete = false;
		}

		private string ProcessString (string input)
		{
			// Each separate output line is delimited by Environment.NewLine.
			// When loading from a config file, you can't have newlines in it, so they're represented by "$$$".
			// I didn't expect this, but Linux newlines work just as well as Windows ones.
			//
			// You can read a full description of this mess in DOCUMENTATION.md

			if (input.IndexOf (variableListSeparator [0]) >= 0) {
				currentPageIsMutable = true;

				string[] tokens = input.Split (variableListSeparator, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length != 2) {
					return "FORMAT ERROR";
				} else {
					string[] vars = tokens [1].Split (variableSeparator, StringSplitOptions.RemoveEmptyEntries);

					object[] variables = new object[vars.Length];
					for (int i=0; i<vars.Length; i++) {
						variables [i] = comp.ProcessVariable (vars [i]);
					}
					return String.Format (tokens [0], variables);
				}
			} else
				return input;
		}
		// Update according to the given refresh rate.
		private bool UpdateCheck ()
		{
			if (updateCountdown <= 0 || updateForced) {
				updateForced = false;
				return true;
			} else {
				updateCountdown--;
				return false;
			}
		}

		public override void OnUpdate ()
		{
			if (!HighLogic.LoadedSceneIsFlight)
				return;

			if ((CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal) &&
			    vessel == FlightGlobals.ActiveVessel) {

				if (!UpdateCheck ())
					return;

				if (pages [activePage] == "" && !currentPageIsMutable) { // In case the page is empty and has no camera, the screen is treated as turned off and blanked once.
					if (!screenWasBlanked) {
						for (int i = 0; i < textArray.Length; i++)
							textArray [i] = "";
						screenWasBlanked = true;
						remoteArray.SetValue (targetScript, textArray);
						remoteFlag.SetValue (targetScript, true);
					}
				} else {
					if (!currentPageFirstPassComplete || currentPageIsMutable) {
						string[] linesArray = pages [activePage].Split (lineSeparator, StringSplitOptions.None);
						for (int i=0; i<linesPerPage; i++) {
							if (i < linesArray.Length) {
								textArray [i] = ProcessString (linesArray [i]).TrimEnd ();
							} else
								textArray [i] = "";
						}
						remoteArray.SetValue (targetScript, textArray);
						remoteFlag.SetValue (targetScript, true);
						screenWasBlanked = false;
						currentPageFirstPassComplete = true;
					}
				}

			}
		}
	}

	public class buttonHandler:MonoBehaviour
	{
		public delegate void HandlerFunction (int ID);

		public HandlerFunction handlerFunction;
		public int ID;

		public void OnMouseDown ()
		{
			handlerFunction (ID);
		}
	}
}

