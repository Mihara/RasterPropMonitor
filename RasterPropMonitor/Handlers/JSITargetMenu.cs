using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;

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
		public Color32 unavailableColor = Color.gray;
		[KSPField]
		public int distanceColumn = 30;
		[KSPField]
		public string distanceFormatString = " <=0:SIP_6=>m";
		private int refreshMenuCountdown;
		private MenuList currentMenu;
		private int currentMenuItem;
		private string nameColorTag, distanceColorTag, selectedColorTag, unavailableColorTag;
		private static readonly SIFormatProvider fp = new SIFormatProvider();
		private readonly List<string> rootMenu = new List<string> {
			"Celestials",
			"Vessels",
			"Filters",
			"Clear target",
		};
		private readonly Dictionary<VesselType,bool> vesselFilter = new Dictionary<VesselType,bool> {
			{ VesselType.Ship,true },
			{ VesselType.Station,true },
			{ VesselType.Probe,false },
			{ VesselType.Lander,false },
			{ VesselType.Rover,false },
			{ VesselType.EVA,false },
			{ VesselType.Flag,false },
			{ VesselType.Base,false },
			{ VesselType.Debris,false },
			{ VesselType.Unknown,false },
		};
		private int currentMenuCount;

		private enum MenuList
		{
			Root,
			Celestials,
			Vessels,
			Ports,
			Filters,
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
		private PersistenceAccessor persistence;
		private string persistentVarName;

		public string ShowMenu(int width, int height)
		{
			return FormatMenu(width, height, currentMenu);
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
						switch (rootMenu[currentMenuItem]) {
							case "Celestials":
								currentMenu = MenuList.Celestials;
								currentMenuItem = selectedCelestial != null ? celestialsList.FindIndex(x => x.body == selectedCelestial) : 0;
								UpdateLists();
								break;
							case "Vessels":
								currentMenu = MenuList.Vessels;
								currentMenuItem = selectedVessel != null ? vesselsList.FindIndex(x => x.vessel == selectedVessel) : 0;
								UpdateLists();
								break;
							case "Filters":
								currentMenu = MenuList.Filters;
								currentMenuCount = vesselFilter.Count;
								currentMenuItem = 0;
								break;
							case "Clear target":
								FlightGlobals.fetch.SetVesselTarget((ITargetable)null);
								break;
						}
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
					case MenuList.Filters:
						vesselFilter[vesselFilter.ElementAt(currentMenuItem).Key] = !vesselFilter[vesselFilter.ElementAt(currentMenuItem).Key];
						persistence.SetVar(persistentVarName, VesselFilterToBitmask(vesselFilter));
						break;
				}
			}
			if (buttonID == buttonEsc) {
				switch (currentMenu) {
					case MenuList.Celestials:
						currentMenuItem = 0;
						currentMenu = MenuList.Root;
						currentMenuCount = rootMenu.Count;
						break;
					case MenuList.Vessels:
						currentMenuItem = 1;
						currentMenu = MenuList.Root;
						currentMenuCount = rootMenu.Count;
						break;
					case MenuList.Filters:
						currentMenuItem = 2;
						currentMenu = MenuList.Root;
						currentMenuCount = rootMenu.Count;
						break;
					case MenuList.Ports:
						currentMenu = MenuList.Vessels;
						if (selectedVessel != null) {
							currentMenuItem = vesselsList.FindIndex(x => x.vessel == selectedVessel);
						}
						UpdateLists();
						break;
				}
			}
			if (buttonID == buttonHome) {
				sortMode = sortMode == SortMode.Alphabetic ? SortMode.Distance : SortMode.Alphabetic;
				UpdateLists();
			}
		}
		// Analysis disable once UnusedParameter
		private string MakeMenuTitle(string titleString, int width)
		{
			string targetName = string.Empty;
			if (selectedCelestial != null)
				targetName = selectedCelestial.GetName();
			if (selectedVessel != null)
				targetName = selectedVessel.GetName();
			return currentTarget != null ? "== Current: " + targetName : "== " + titleString;
		}

		private string FormatMenu(int width, int height, MenuList current)
		{

			var menu = new List<string>();

			string menuTitle = string.Empty;

			switch (current) {
				case MenuList.Root:
					if (currentTarget != null)
						menuTitle = MakeMenuTitle("Root menu", width);
					for (int i = 0; i < rootMenu.Count; i++) {
						menu.Add(FormatItem(rootMenu[i], 0, (currentMenuItem == i), false, false));
					}
					break;
				case MenuList.Filters:
					menuTitle = "== Vessel filtering:";
					for (int i = 0; i < vesselFilter.Count; i++) {
						var filter = vesselFilter.ElementAt(i);
						menu.Add(FormatItem(
							filter.Key.ToString().PadRight(9) + (filter.Value ? "- On" : "- Off"), 0,
							(currentMenuItem == i), filter.Value, false));
					}
					break;
				case MenuList.Celestials: 
					menuTitle = MakeMenuTitle("Celestial bodies", width);
					for (int i = 0; i < celestialsList.Count; i++) {
						menu.Add(FormatItem(celestialsList[i].name, celestialsList[i].distance,
							(currentMenuItem == i), (selectedCelestial == celestialsList[i].body),
							(vessel.mainBody == celestialsList[i].body)));

					}
					break;
				case MenuList.Vessels:
					menuTitle = MakeMenuTitle("Vessels", width);
					for (int i = 0; i < vesselsList.Count; i++) {
						menu.Add(FormatItem(vesselsList[i].name, vesselsList[i].distance,
							(currentMenuItem == i), (vesselsList[i].vessel == selectedVessel),
							(vesselsList[i].vessel.mainBody != vessel.mainBody)));

					}
					break;
				case MenuList.Ports:
					if (selectedVessel == null || !selectedVessel.loaded || portsList.Count == 0) {
						currentMenu = MenuList.Vessels;
						currentMenuItem = 0;
						UpdateLists();
						return string.Empty;
					}
					menuTitle = MakeMenuTitle(selectedVessel.GetName(), width);
					for (int i = 0; i < portsList.Count; i++) {
						menu.Add(FormatItem(portsList[i].GetName(),
							Vector3.Distance(vessel.GetTransform().position, portsList[i].GetTransform().position),
							(currentMenuItem == i), (portsList[i] == selectedPort),
							false));
					}
					break;
			}
			if (!string.IsNullOrEmpty(pageTitle))
				height--;
			if (!string.IsNullOrEmpty(menuTitle))
				height--;

			if (menu.Count > height) {
				menu = menu.GetRange(Math.Min(currentMenuItem, menu.Count - height), height);
			}
			var result = new StringBuilder();

			if (!string.IsNullOrEmpty(pageTitle))
				result.AppendLine(pageTitle);

			if (!string.IsNullOrEmpty(menuTitle))
				result.AppendLine(menuTitle);

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

		private string FormatItem(string itemText, double distance, bool current, bool selected, bool unavailable)
		{
			var result = new StringBuilder();
			result.Append(current ? "> " : "  ");
			if (selected)
				result.Append(selectedColorTag);
			else if (unavailable)
				result.Append(unavailableColorTag);
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
							foreach (var filter in vesselFilter) {
								if (thatVessel.vesselType == filter.Key && filter.Value) {
									vesselsList.Add(new TargetableVessel(thatVessel, vessel.transform.position));
									break;
								}
							}
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
					if (currentMenuItem < 0)
						currentMenuItem = 0;
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

			persistentVarName = "targetfilter" + internalProp.propID;
			persistence = new PersistenceAccessor(part);
			// 7 is the bitmask for ship-station-probe;
			VesselFilterFromBitmask(persistence.GetVar(persistentVarName) ?? 7);

			nameColorTag = JUtil.ColorToColorTag(nameColor);
			distanceColorTag = JUtil.ColorToColorTag(distanceColor);
			selectedColorTag = JUtil.ColorToColorTag(selectedColor);
			unavailableColorTag = JUtil.ColorToColorTag(unavailableColor);
			distanceFormatString = distanceFormatString.UnMangleConfigText();

			if (!string.IsNullOrEmpty(pageTitle))
				pageTitle = pageTitle.Replace("<=", "{").Replace("=>", "}");

			foreach (CelestialBody body in FlightGlobals.Bodies) { 
				celestialsList.Add(new Celestial(body, vessel.transform.position));
			}
			currentMenuCount = rootMenu.Count;
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

		private static int VesselFilterToBitmask(Dictionary<VesselType,bool> filterList)
		{
			// Because VesselType is not [Flags]. Gweh.
			int mask = 0;
			if (filterList[VesselType.Ship])
				mask |= 1 << 0;
			if (filterList[VesselType.Station])
				mask |= 1 << 1;
			if (filterList[VesselType.Probe])
				mask |= 1 << 2;
			if (filterList[VesselType.Lander])
				mask |= 1 << 3;
			if (filterList[VesselType.Rover])
				mask |= 1 << 4;
			if (filterList[VesselType.EVA])
				mask |= 1 << 5;
			if (filterList[VesselType.Flag])
				mask |= 1 << 6;
			if (filterList[VesselType.Base])
				mask |= 1 << 7;
			if (filterList[VesselType.Debris])
				mask |= 1 << 8;
			if (filterList[VesselType.Unknown])
				mask |= 1 << 9;
			return mask;
		}

		private void VesselFilterFromBitmask(int mask)
		{
			vesselFilter[VesselType.Ship] = (mask & (1 << 0)) > 0;
			vesselFilter[VesselType.Station] = (mask & (1 << 1)) > 0;
			vesselFilter[VesselType.Probe] = (mask & (1 << 2)) > 0;
			vesselFilter[VesselType.Lander] = (mask & (1 << 3)) > 0;
			vesselFilter[VesselType.Rover] = (mask & (1 << 4)) > 0;
			vesselFilter[VesselType.EVA] = (mask & (1 << 5)) > 0;
			vesselFilter[VesselType.Flag] = (mask & (1 << 6)) > 0;
			vesselFilter[VesselType.Base] = (mask & (1 << 7)) > 0;
			vesselFilter[VesselType.Debris] = (mask & (1 << 8)) > 0;
			vesselFilter[VesselType.Unknown] = (mask & (1 << 9)) > 0;
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

