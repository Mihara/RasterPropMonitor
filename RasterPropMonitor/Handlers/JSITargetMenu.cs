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
		private MenuList currentMenu;
		private int currentMenuItem;
		private int currentMenuCount = 2;
		private string nameColorTag, distanceColorTag, selectedColorTag;
		private static readonly SIFormatProvider fp = new SIFormatProvider();
		private readonly List<string> rootMenu = new List<string> {
			"Celestials",
			"Vessels"
		};

		private enum MenuList
		{
			Root,
			Celestials,
			Vessels,
			Ports,
		};

		private enum SortMode
		{
			Alphabetic,
			Distance,
		}

		private ITargetable currentTarget;
		private Vessel selectedVessel;
		private ModuleDockingNode selectedPort;
		private CelestialBody selectedCelestial;
		private readonly List<Celestial> celestialsList = new List<Celestial>();
		private readonly List<TargetableVessel> vesselsList = new List<TargetableVessel>();
		private List<ModuleDockingNode> portsList = new List<ModuleDockingNode>();
		private SortMode sortMode;
		private bool pageActiveState;
		// Analysis disable once UnusedParameter
		public string ShowMenu(int width, int height)
		{
			return FormatMenu(height, currentMenu);
		}
		// Analysis disable once UnusedParameter
		public void PageActive(bool active, int pageNumber)
		{
			pageActiveState = active;
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
					case MenuList.Root:
						if (currentMenuItem == 0) {
							currentMenu = MenuList.Celestials;
							if (selectedCelestial != null) {
								currentMenuItem = celestialsList.FindIndex(x => x.body == selectedCelestial);
							}
							UpdateLists();
						} else {
							currentMenu = MenuList.Vessels;
							if (selectedVessel != null) {
								currentMenuItem = vesselsList.FindIndex(x => x.vessel == selectedVessel);
							}
							UpdateLists();
						}
						currentMenuItem = 0;
						break;
					case MenuList.Celestials:
						celestialsList[currentMenuItem].SetTarget();
						selectedVessel = null;
						selectedPort = null;
						break;
					case MenuList.Vessels:
						if (selectedVessel == vesselsList[currentMenuItem].vessel && selectedVessel.loaded) {
							// Vessel already selected and loaded, so we can switch to docking port menu...
							if (UpdatePortsList() > 0) {
								currentMenu = MenuList.Ports;
								currentMenuItem = 0;
								UpdateLists();
							} else
								return;
						} else {
							vesselsList[currentMenuItem].SetTarget();
							selectedVessel = vesselsList[currentMenuItem].vessel;
							selectedPort = null;
						}
						break;
					case MenuList.Ports:
						if (selectedVessel != null && selectedVessel.loaded && portsList[currentMenuItem] != null) {
							FlightGlobals.fetch.SetVesselTarget(portsList[currentMenuItem]);
						}
						break;
				}
			}
			if (buttonID == buttonEsc) {
				if (currentMenu == MenuList.Ports) {
					currentMenu = MenuList.Vessels;
					if (selectedVessel != null) {
						currentMenuItem = vesselsList.FindIndex(x => x.vessel == selectedVessel);
					}
					UpdateLists();
				} else {
					if (currentMenu == MenuList.Vessels)
						currentMenuItem = 1;
					else
						currentMenuItem = 0;
					currentMenu = 0;
					currentMenuCount = rootMenu.Count;
				}
			}
			if (buttonID == buttonHome) {
				if (sortMode == SortMode.Alphabetic)
					sortMode = SortMode.Distance;
				else
					sortMode = SortMode.Alphabetic;
				UpdateLists();
			}
		}

		private string FormatMenu(int height, MenuList current)
		{

			var menu = new List<string>();

			switch (current) {
				case MenuList.Root:
					for (int i = 0; i < rootMenu.Count; i++) {
						menu.Add(FormatItem(rootMenu[i], 0, (currentMenuItem == i), false));
					}
					break;
				case MenuList.Celestials: 
					for (int i = 0; i < celestialsList.Count; i++) {
						menu.Add(FormatItem(celestialsList[i].name, celestialsList[i].distance,
							(currentMenuItem == i), (selectedCelestial == celestialsList[i].body)));

					}
					break;
				case MenuList.Vessels:
					for (int i = 0; i < vesselsList.Count; i++) {
						menu.Add(FormatItem(vesselsList[i].name, vesselsList[i].distance,
							(currentMenuItem == i), (vesselsList[i].vessel == selectedVessel)));

					}
					break;
				case MenuList.Ports:
					if (selectedVessel == null || !selectedVessel.loaded || portsList.Count == 0) {
						currentMenu = MenuList.Vessels;
						currentMenuItem = 0;
						UpdateLists();
						return string.Empty;
					}
					for (int i = 0; i < portsList.Count; i++) {
						menu.Add(FormatItem(portsList[i].GetName(),
							Vector3.Distance(vessel.GetTransform().position, portsList[i].GetTransform().position),
							(currentMenuItem == i), (portsList[i] == selectedPort)));
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

		private static List<ModuleDockingNode> ListAvailablePorts(Vessel thatVessel)
		{
			var unavailableParts = new List<uint>();
			var availablePorts = new List<ModuleDockingNode>();
			for (int i = 0; i < 2; i++) {
				foreach (Part thatPart in thatVessel.parts) {
					foreach (PartModule thatModule in thatPart.Modules) {
						var thatPort = thatModule as ModuleDockingNode;
						if (thatPort != null) {
							if (i == 0) {
								if (thatPort.state.ToLower().Contains("docked")) {
									unavailableParts.Add(thatPort.part.flightID);
									unavailableParts.Add(thatPort.dockedPartUId);
								}
							} else {
								if (!unavailableParts.Contains(thatPort.part.flightID))
									availablePorts.Add(thatPort);
							}
						}
					}
				}
			}
			return availablePorts;
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
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel || !pageActiveState)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (pageActiveState) {
				currentTarget = FlightGlobals.fetch.VesselTarget;
				selectedCelestial = currentTarget as CelestialBody;
				selectedVessel = currentTarget as Vessel;
				selectedPort = currentTarget as ModuleDockingNode;
				if (selectedPort != null)
					selectedVessel = selectedPort.vessel;

			}

			if (!UpdateCheck())
				return;
			UpdateLists();
		}

		private void UpdateLists()
		{

			switch (currentMenu) {
				case MenuList.Celestials: 
					foreach (Celestial body in celestialsList)
						body.UpdateDistance(vessel.transform.position);

					CelestialBody currentBody = celestialsList[currentMenuItem].body;
					switch (sortMode) {
						case SortMode.Alphabetic:
							celestialsList.Sort(CelestialAlphabeticSort);
							break;
						case SortMode.Distance: 
							celestialsList.Sort(CelestialDistanceSort);
							break;
					}
					currentMenuItem = celestialsList.FindIndex(x => x.body == currentBody);
					currentMenuCount = celestialsList.Count;
					break;
				case MenuList.Vessels:
					Vessel currentVessel = null;
					if (vesselsList.Count > 0 && currentMenuItem < vesselsList.Count) {
						if (vesselsList[currentMenuItem].vessel == null)
							currentMenuItem = 0;

						if (vesselsList[currentMenuItem].vessel != null)
							currentVessel = vesselsList[currentMenuItem].vessel;
					} else
						currentMenuItem = 0;

					vesselsList.Clear();
					foreach (Vessel thatVessel in FlightGlobals.fetch.vessels) {
						if (vessel != thatVessel) {
							vesselsList.Add(new TargetableVessel(thatVessel, vessel.transform.position));
						}
					}
					currentMenuCount = vesselsList.Count;

					switch (sortMode) {
						case SortMode.Alphabetic:
							vesselsList.Sort(VesselAlphabeticSort);
							break;
						case SortMode.Distance: 
							vesselsList.Sort(VesselDistanceSort);
							break;
					}
					if (currentVessel != null)
						currentMenuItem = vesselsList.FindIndex(x => x.vessel == currentVessel);

					break;
				case MenuList.Ports:
					UpdatePortsList();
					break;
			}

		}

		private int UpdatePortsList()
		{
			portsList = ListAvailablePorts(selectedVessel);
			if (currentMenu == MenuList.Ports) {
				currentMenuCount = portsList.Count;
				if (currentMenuItem > currentMenuCount)
					currentMenuItem = 0;
			}
			return portsList.Count;
		}

		public void Start()
		{
			if (!HighLogic.LoadedSceneIsFlight)
				return;
			nameColorTag = JUtil.ColorToColorTag(nameColor);
			distanceColorTag = JUtil.ColorToColorTag(distanceColor);
			selectedColorTag = JUtil.ColorToColorTag(selectedColor);
			distanceFormatString = distanceFormatString.UnMangleConfigText();

			if (!string.IsNullOrEmpty(pageTitle))
				pageTitle = pageTitle.Replace("<=", "{").Replace("=>", "}");

			foreach (CelestialBody body in FlightGlobals.Bodies) { 
				if (body.bodyName != "Sun")
					celestialsList.Add(new Celestial(body, vessel.transform.position));
			}
		}

		private static int CelestialDistanceSort(Celestial first, Celestial second)
		{
			return first.distance.CompareTo(second.distance);
		}

		private static int CelestialAlphabeticSort(Celestial first, Celestial second)
		{
			return string.Compare(first.name, second.name, StringComparison.Ordinal);
		}

		private static int VesselDistanceSort(TargetableVessel first, TargetableVessel second)
		{
			if (first.vessel == null || second.vessel == null)
				return 0;
			return first.distance.CompareTo(second.distance);
		}

		private static int VesselAlphabeticSort(TargetableVessel first, TargetableVessel second)
		{
			if (first.vessel == null || second.vessel == null)
				return 0;
			return string.Compare(first.name, second.name, StringComparison.Ordinal);
		}

		private class Celestial
		{
			public string name;
			public readonly CelestialBody body;
			public double distance;

			public Celestial(CelestialBody thisBody, Vector3d position)
			{
				name = thisBody.bodyName;
				body = thisBody;
				UpdateDistance(position);
			}

			public void UpdateDistance(Vector3d position)
			{
				distance = Vector3d.Distance(position, body.GetTransform().position);
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

			public TargetableVessel(Vessel thatVessel, Vector3d position)
			{
				vessel = thatVessel;
				name = thatVessel.vesselName;
				UpdateDistance(position);
			}

			public void UpdateDistance(Vector3d position)
			{
				distance = Vector3d.Distance(position, vessel.transform.position);
			}

			public void SetTarget()
			{
				FlightGlobals.fetch.SetVesselTarget(vessel);
			}
		}
	}
}

