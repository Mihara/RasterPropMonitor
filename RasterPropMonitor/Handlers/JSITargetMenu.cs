using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JSI
{
	public class JSITargetMenu: InternalModule
	{
		[KSPField]
		public string pageTitle;
		[KSPField]
		public int refreshMenuRate = 60;
		[KSPField]
		public int buttonUp;
		[KSPField]
		public int buttonDown = 1;
		[KSPField]
		public int buttonEnter = 2;
		[KSPField]
		public int buttonEsc = 3;
		[KSPField]
		public int buttonHome = 4;
		[KSPField]
		public Color32 nameColor = Color.white;
		[KSPField]
		public Color32 distanceColor = Color.cyan;
		[KSPField]
		public Color32 selectedColor = Color.green;
		[KSPField]
		public int distanceColumn = 30;
		[KSPField]
		public string distanceFormatString = " <=0:SIP_6=>m";
		private int refreshMenuCountdown;
		private int currentMenu;
		private int currentMenuItem;
		private int currentMenuCount = 2;
		private string nameColorTag, distanceColorTag, selectedColorTag;
		private static readonly SIFormatProvider fp = new SIFormatProvider();
		private readonly List<string> rootMenu = new List<string> {
			"Celestials",
			"Vessels"
		};
		private ITargetable currentTarget;
		private readonly List<Celestial> celestialsList = new List<Celestial>();
		private readonly List<TargetableVessel> vesselsList = new List<TargetableVessel>();
		// Analysis disable once UnusedParameter
		public string ShowMenu(int width, int height)
		{
			currentTarget = FlightGlobals.fetch.VesselTarget;
			return FormatMenu(height, currentMenu);
		}

		public void ButtonProcessor(int buttonID)
		{
			if (buttonID == buttonUp) {
				currentMenuItem--;
				if (currentMenuItem < 0)
					currentMenuItem = 0;
			}
			if (buttonID == buttonDown) {
				currentMenuItem++;
				if (currentMenuItem >= currentMenuCount - 1)
					currentMenuItem = currentMenuCount - 1;
			}
			if (buttonID == buttonEnter) {
				switch (currentMenu) {
					case 0:
						if (currentMenuItem == 0) {
							currentMenu = 1;
							currentMenuCount = celestialsList.Count;
						} else {
							currentMenu = 2;
							currentMenuCount = vesselsList.Count;
						}
						currentMenuItem = 0;
						break;
					case 1:
						celestialsList[currentMenuItem].SetTarget();
						break;
					case 2:
						vesselsList[currentMenuItem].SetTarget();
						break;
				}
			}
			if (buttonID == buttonEsc) {
				currentMenu = 0;
				currentMenuCount = rootMenu.Count;
				currentMenuItem = 0;
			}
			if (buttonID == buttonHome) {
				// Sort mode.
			}
		}

		private string FormatMenu(int height, int current)
		{

			var menu = new List<string>();

			switch (current) {
				case 0:
					for (int i = 0; i < rootMenu.Count; i++) {
						menu.Add(FormatItem(rootMenu[i], 0, (currentMenuItem == i), false));
					}
					break;
				case 1: 
					for (int i = 0; i < celestialsList.Count; i++) {
						menu.Add(FormatItem(celestialsList[i].name, celestialsList[i].distance,
							(currentMenuItem == i), (currentTarget as CelestialBody == celestialsList[i].body)));

					}
					break;
				case 2:
					if (currentMenuItem > vesselsList.Count)
						currentMenuItem = vesselsList.Count - 1;
					var selectedVessel = currentTarget as Vessel;
					var targetNode = currentTarget as ModuleDockingNode;
					if (targetNode != null)
						selectedVessel = targetNode.vessel;
					for (int i = 0; i < vesselsList.Count; i++) {
						menu.Add(FormatItem(vesselsList[i].name, vesselsList[i].distance,
							(currentMenuItem == i), (vesselsList[i].vessel == selectedVessel)));

					}
					break;
			}
			if (!string.IsNullOrEmpty(pageTitle))
				height--;

			if (menu.Count > height) {
				menu = menu.GetRange(Math.Min(currentMenuItem, menu.Count - height), height);
			}

			var result = new StringBuilder();
			if (!string.IsNullOrEmpty(pageTitle))
				result.AppendLine(pageTitle);
			foreach (string item in menu)
				result.AppendLine(item);
			return result.ToString();
		}

		private string FormatItem(string itemText, double distance, bool current, bool selected)
		{
			var result = new StringBuilder();
			result.Append(current ? "> " : "  ");
			if (selected)
				result.Append(selectedColorTag);
			else
				result.Append(nameColorTag);
			result.Append(itemText.PadRight(distanceColumn, ' ').Substring(0, distanceColumn - 2));
			if (distance > 0) {
				result.Append(distanceColorTag);
				result.AppendFormat(fp, distanceFormatString, distance);
			}
			return result.ToString();
		}

		private bool UpdateCheck()
		{
			refreshMenuCountdown--;
			if (refreshMenuCountdown <= 0) {
				refreshMenuCountdown = refreshMenuRate;
				return true;
			}

			return false;
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
			foreach (Celestial body in celestialsList)
				body.UpdateDistance();

			vesselsList.Clear();
			foreach (Vessel thatVessel in FlightGlobals.fetch.vessels) {
				if (vessel != thatVessel) {
					vesselsList.Add(new TargetableVessel(vessel, thatVessel));
				}
			}

		}

		public void Start()
		{
			nameColorTag = JUtil.ColorToColorTag(nameColor);
			distanceColorTag = JUtil.ColorToColorTag(distanceColor);
			selectedColorTag = JUtil.ColorToColorTag(selectedColor);
			distanceFormatString = distanceFormatString.UnMangleConfigText();

			if (!string.IsNullOrEmpty(pageTitle))
				pageTitle = pageTitle.Replace("<=", "{").Replace("=>", "}");

			foreach (CelestialBody body in FlightGlobals.Bodies) { 
				if (body.bodyName != "Sun")
					celestialsList.Add(new Celestial(vessel, body));
			}
		}

		private class Celestial
		{
			public string name;
			public readonly CelestialBody body;
			public double distance;
			private readonly Vessel ourVessel;

			public Celestial(Vessel thisVessel, CelestialBody thisBody)
			{
				name = thisBody.bodyName;
				body = thisBody;
				ourVessel = thisVessel;
				UpdateDistance();

			}

			public void UpdateDistance()
			{
				distance = Vector3d.Distance(ourVessel.transform.position, body.GetTransform().position);
			}

			public void SetTarget()
			{
				FlightGlobals.fetch.SetVesselTarget(body);
			}
		}

		private class TargetableVessel
		{
			public string name;
			public readonly Vessel vessel;
			public double distance;
			private readonly Vessel ourVessel;

			public TargetableVessel(Vessel thisVessel, Vessel thatVessel)
			{
				ourVessel = thisVessel;
				vessel = thatVessel;
				name = thatVessel.vesselName;
				UpdateDistance();
			}

			public void UpdateDistance()
			{
				distance = Vector3d.Distance(ourVessel.transform.position, vessel.transform.position);
			}

			public void SetTarget()
			{
				FlightGlobals.fetch.SetVesselTarget(vessel);
			}
		}
	}
}

