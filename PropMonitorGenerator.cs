using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RasterPropMonitorGenerator
{
	public class RasterPropMonitorGenerator: InternalModule
	{
		[KSPField]
		public int refreshRate = 20;
		//[KSPField]
		//public int refreshResourceRate = 20;
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
		// Config syntax.
		//private string[] lineSeparator = { "$$$" };
		private string[] lineSeparator = { Environment.NewLine };
		private string[] variableListSeparator = { "###" };
		private string[] variableSeparator = { "|" };
		private InternalModule targetScript;
		private string[] textArray;
		// Important pointers to the screen's data structures.
		FieldInfo remoteArray;
		FieldInfo remoteFlag;
		// Local variables
		private string[] pages = { "", "", "", "", "", "", "", "" };
		private int activePage = 0;
		private int charPerLine = 23;
		private int linesPerPage = 17;
		private string spacebuffer;
		private int vesselNumParts;
		private int updateCountdown;
		private bool updateForced = false;
		private bool screenWasBlanked = false;

		public void Start ()
		{
			// Mihara: We're getting at the screen module and it's parameters using reflection here.
			// While I would prefer to use some message passing mechanism instead,
			// it does not look like I can use KSPEvent.
			// I could directly lock at the parameters, seeing as how these two modules
			// are in the same assembly, but instead I'm leaving the reflection-based mechanism here
			// so that you could make your own screen driver module
			// by simply copy-pasting the relevant sections.
			foreach (InternalModule intModule in base.internalProp.internalModules) {
				if (intModule.ClassName == "RasterPropMonitor") {
					targetScript = intModule;
					remoteArray = intModule.GetType ().GetField ("screenText");
					remoteFlag = intModule.GetType ().GetField ("screenUpdateRequired");

					charPerLine = (int)intModule.GetType ().GetField ("screenWidth").GetValue (intModule);
					linesPerPage = (int)intModule.GetType ().GetField ("screenHeight").GetValue (intModule);

					break;
				}
			}

			spacebuffer = new String (' ', charPerLine);

			string[] pageData = new string[] { page1, page2, page3, page4, page5, page6, page7, page8 };
			string[] buttonName = new string[] { button1, button2, button3, button4, button5, button6, button7, button8 };
			for (int i=0; i<8; i++) {
				//Debug.Log ("RasterMonitor: Page " + i.ToString () + " data is \"" + pageData [i] + "\" button name is " + buttonName [i]);
				if (buttonName [i] != "") {
					GameObject buttonObject = base.internalProp.FindModelTransform (buttonName [i]).gameObject;
					buttonHandler pageButton = buttonObject.AddComponent<buttonHandler> ();
					pageButton.ID = i;
					pageButton.handlerFunction = buttonClick;
				}

				try {
					pages [i] = String.Join (Environment.NewLine, File.ReadAllLines (KSPUtil.ApplicationRootPath + "GameData/" + pageData [i], System.Text.Encoding.ASCII));
				} catch {
					pages [i] = pageData [i].Replace ("<=", "{").Replace ("=>", "}").Replace ("$$$", Environment.NewLine);
				}
			}


			textArray = new string[linesPerPage];
			for (int i = 0; i < textArray.Length; i++) {
				textArray [i] = "";
			}

		}

		public void buttonClick (int buttonID)
		{
			activePage = buttonID;
			updateForced = true;
		}

		private object processVariable (string input)
		{
			switch (input) {

			// It's a bit crude, but it's simple enough to populate.
			// Would be a bit smoother if I had eval() :)
			case "ALTITUDE":
				return Math.Floor (FlightGlobals.ship_altitude);
			case "RADARALT":
				return Math.Floor (vessel.altitude - Math.Max (vessel.pqsAltitude, 0D));
			case "VERTSPEED":
				return Math.Round (FlightGlobals.ship_verticalSpeed, 1);
			case "SURFSPEED":
				return Math.Round (FlightGlobals.ship_srfSpeed, 1);
			case "ORBTSPEED":
				return Math.Round (FlightGlobals.ship_obtSpeed, 1);
			case "TRGTSPEED":
				return Math.Round (FlightGlobals.ship_tgtSpeed, 1);
			case "PERIAPSIS":
				return Math.Round (FlightGlobals.ship_orbit.PeA, 1);
			case "APOAPSIS":
				return Math.Round (FlightGlobals.ship_orbit.ApA, 1);
			case "INCLINATION":
				return Math.Round (FlightGlobals.ship_orbit.inclination, 1);

			}
			return "!??";
		}

		private string processString (string input)
		{
			// Each separate output line is delimited by "$$$".
			// Within each line, if it contains any variables, it looks like this:
			// "Insert <=0:0.0=> variables <=0:0.0=> into this string###VARIABLE|VARIABLE"
			// 
			// <= will be replaced by {.
			// => will be replaced by }.
			// A more readable string format reference detailing where each variable is to be inserted and 
			// what it should look like can be found here: http://blog.stevex.net/string-formatting-in-csharp/

			if (input.IndexOf (variableListSeparator [0]) >= 0) {

				string[] tokens = input.Split (variableListSeparator, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length != 2) {
					return "FORMAT ERROR";
				} else {
					string[] vars = tokens [1].Split (variableSeparator, StringSplitOptions.RemoveEmptyEntries);
					//Debug.Log ("PropMonitorGenerator: So we got " + vars.Length.ToString () + " variables...");

					object[] variables = new object[vars.Length];
					for (int i=0; i<vars.Length; i++) {
						//Debug.Log ("PropMonitorGenerator: Processing " + vars[i]);
						variables [i] = processVariable (vars [i]);
					}
					return String.Format (tokens [0], variables);
				}
			} else
				return input;
		}
		// Update according to the given refresh rate or when number of parts changes.
		private bool updateCheck ()
		{
			if (vesselNumParts != vessel.Parts.Count || updateCountdown <= 0 || updateForced) {
				updateCountdown = refreshRate;
				vesselNumParts = vessel.Parts.Count;
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

			if (!updateCheck ())
				return;

			if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA && vessel == FlightGlobals.ActiveVessel) {

				for (int i = 0; i < textArray.Length; i++)
					textArray [i] = spacebuffer;
				// And here we actually populate the array with data by an undisclosed method.

				if (pages [activePage] == "") { // In case the page is empty, the screen is treated as turned off and blanked once.
					if (!screenWasBlanked) {
						screenWasBlanked = true;
						remoteArray.SetValue (targetScript, textArray);
						remoteFlag.SetValue (targetScript, true);
					}
				} else {
					string[] linesArray = pages [activePage].Split (lineSeparator, StringSplitOptions.None);
					for (int i=0; i<linesArray.Length && i<linesPerPage; i++) {
						textArray [i] = processString (linesArray [i]) + spacebuffer;
					}
					remoteArray.SetValue (targetScript, textArray);
					remoteFlag.SetValue (targetScript, true);
					screenWasBlanked = false;
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

