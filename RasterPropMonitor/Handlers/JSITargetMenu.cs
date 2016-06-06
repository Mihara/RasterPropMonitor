/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JSI
{
    public class JSITargetMenu : InternalModule
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
        public string nameColor = string.Empty;
        private Color nameColorValue = Color.white;
        [KSPField]
        public string distanceColor = string.Empty;
        private Color distanceColorValue = Color.cyan;
        [KSPField]
        public string selectedColor = string.Empty;
        private Color selectedColorValue = Color.green;
        [KSPField]
        public string unavailableColor = string.Empty;
        private Color unavailableColorValue = Color.gray;
        [KSPField]
        public int distanceColumn = 30;
        [KSPField]
        public string distanceFormatString = " <=0:SIP_6=>m";
        [KSPField]
        public int distanceColumnWidth = 8;
        [KSPField]
        public string menuTitleFormatString = "== {0}";
        [KSPField]
        public int defaultFilter = 15;
        // MOARdV: Really, there is no reason to instantiate the topMenu and
        // keep it around.  If anything, it is less expensive to construct than
        // the other menus.  Although leaving it here means the "current item"
        // in the main menu will always be correct when navigating up from a
        // submenu.
        private readonly TextMenu topMenu = new TextMenu();
        private TextMenu activeMenu;
        private TextMenu.Item clearTarget;
        private TextMenu.Item undockMenuItem;
        private TextMenu.Item grappleMenuItem;
        private int refreshMenuCountdown;
        private MenuList currentMenu;
        private string nameColorTag, distanceColorTag, selectedColorTag, unavailableColorTag;
        private static readonly SIFormatProvider fp = new SIFormatProvider();
        private const string clearTargetItemText = "Clear target";
        private const string undockItemText = "Undock";
        private const string armGrappleText = "Arm Grapple";
        private const string crewEvaText = "Crew EVA";
        private readonly List<string> rootMenu = new List<string> {
            "Celestials",
            "Vessels",
            "Space Objects",
            "Reference part",
            undockItemText,
            armGrappleText,
            "Filters",
            clearTargetItemText,
            crewEvaText,
        };
        private readonly Dictionary<VesselType, bool> vesselFilter = new Dictionary<VesselType, bool> {
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

        private enum MenuList
        {
            Root,
            Celestials,
            Vessels,
            SpaceObjects,
            Reference,
            Ports,
            Undock,
            Filters,
            CrewEVA,
        };

        private enum SortMode
        {
            Alphabetic,
            Distance,
        }

        private ITargetable currentTarget;
        private Vessel selectedVessel;
        private ModuleDockingNode selectedPort;
        private ModuleGrappleNode selectedClaw;
        private CelestialBody selectedCelestial;
        private readonly List<Celestial> celestialsList = new List<Celestial>();
        private readonly List<TargetableVessel> spaceObjectsList = new List<TargetableVessel>();
        private readonly List<TargetableVessel> vesselsList = new List<TargetableVessel>();
        private readonly List<PartModule> undockablesList = new List<PartModule>();
        private List<ModuleDockingNode> portsList = new List<ModuleDockingNode>();
        private readonly List<PartModule> referencePoints = new List<PartModule>();
        private readonly List<uint> unavailablePorts = new List<uint>();
        private int partCount;
        private SortMode sortMode;
        private bool pageActiveState;
        private string persistentVarName;
        private RasterPropMonitorComputer rpmComp;

        // DPAI linkage
        private static readonly Type dpaiModuleDockingNodeNamed;
        private static readonly FieldInfo dpaiPortName;
        private static readonly bool dpaiFound;

        static JSITargetMenu()
        {
            try
            {
                dpaiModuleDockingNodeNamed = AssemblyLoader.loadedAssemblies.SelectMany(
                    a => a.assembly.GetExportedTypes())
                    .SingleOrDefault(t => t.FullName == "NavyFish.ModuleDockingNodeNamed");

                dpaiPortName = dpaiModuleDockingNodeNamed.GetField("portName", BindingFlags.Instance | BindingFlags.Public);
            }
            catch (Exception)
            {
                dpaiModuleDockingNodeNamed = null;
                dpaiPortName = null;
            }

            if (dpaiModuleDockingNodeNamed != null && dpaiPortName != null)
            {
                dpaiFound = true;
            }
            else
            {
                dpaiFound = false;
            }
        }

        public string ShowMenu(int width, int height)
        {
            switch (currentMenu)
            {
                case MenuList.Undock:
                    activeMenu.menuTitle = string.Format(fp, menuTitleFormatString, "Decouple ports");
                    break;
                case MenuList.Root:
                    activeMenu.menuTitle = MakeMenuTitle("Root menu");
                    grappleMenuItem.isDisabled = UpdateReferencePartAsClaw();
                    clearTarget.isDisabled = (currentTarget == null);
                    undockMenuItem.isDisabled = (undockablesList.Count == 0);
                    break;
                case MenuList.Filters:
                    activeMenu.menuTitle = string.Format(fp, menuTitleFormatString, "Vessel filtering");
                    break;
                case MenuList.Celestials:
                    activeMenu.menuTitle = MakeMenuTitle("Celestial bodies");
                    break;
                case MenuList.SpaceObjects:
                    activeMenu.menuTitle = MakeMenuTitle("Space Objects");
                    break;
                case MenuList.Vessels:
                    activeMenu.menuTitle = MakeMenuTitle("Vessels");
                    break;
                case MenuList.Reference:
                    activeMenu.menuTitle = string.Format(fp, menuTitleFormatString, "Select reference");
                    break;
                case MenuList.Ports:
                    // sanity check:
                    if (selectedVessel == null || !selectedVessel.loaded || portsList.Count == 0)
                    {
                        ShowVesselMenu(0, null);
                        return string.Empty;
                    }
                    activeMenu.menuTitle = MakeMenuTitle(selectedVessel.GetName());
                    break;
                case MenuList.CrewEVA:
                    activeMenu.menuTitle = MakeMenuTitle("Crew EVA");
                    break;
            }

            if (string.IsNullOrEmpty(pageTitle))
            {
                return activeMenu.ShowMenu(width, height);
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine(pageTitle);
                sb.Append(activeMenu.ShowMenu(width, height - 1));
                return sb.ToString();
            }
        }
        // Analysis disable once UnusedParameter
        public void PageActive(bool active, int pageNumber)
        {
            pageActiveState = active;
        }

        public void ButtonProcessor(int buttonID)
        {
            if (buttonID == buttonUp)
            {
                activeMenu.PreviousItem();
            }
            if (buttonID == buttonDown)
            {
                activeMenu.NextItem();
            }
            if (buttonID == buttonEnter)
            {
                activeMenu.SelectItem();
            }
            if (buttonID == buttonEsc)
            {
                if (currentMenu == MenuList.Ports)
                {
                    // Take advantage of the fact that ShowVesselMenu does not
                    // care about the parameters.
                    ShowVesselMenu(0, null);
                }
                else
                {
                    activeMenu = topMenu;
                    currentMenu = MenuList.Root;
                }
            }
            if (buttonID == buttonHome)
            {
                sortMode = sortMode == SortMode.Alphabetic ? SortMode.Distance : SortMode.Alphabetic;
                UpdateLists();
            }
        }

        private string MakeMenuTitle(string titleString)
        {
            string targetName = string.Empty;
            if (selectedCelestial != null)
            {
                targetName = selectedCelestial.GetName();
            }
            else if (selectedVessel != null)
            {
                targetName = selectedVessel.GetName();
            }
            return currentTarget != null ? string.Format(fp, menuTitleFormatString, "Current: " + targetName) : string.Format(fp, menuTitleFormatString, titleString);
        }

        private bool UpdateCheck()
        {
            refreshMenuCountdown--;
            if (refreshMenuCountdown <= 0)
            {
                refreshMenuCountdown = refreshMenuRate;

                // Mihara: Apparently, number of parts changes much later when a claw grapples something,
                // so this cycles too late.
                // I don't particularly like it, but this is the quickest way to get claws into undock menu for now.
                // FIXME: Find out what exactly happens to vessel structure when a claw grapples a part.
                UpdateUndockablesList();

                return true;
            }
            else
            {
                return false;
            }
        }

        public override void OnUpdate()
        {
            if (!pageActiveState || !JUtil.VesselIsInIVA(vessel))
            {
                return;
            }

            currentTarget = FlightGlobals.fetch.VesselTarget;
            selectedCelestial = currentTarget as CelestialBody;
            selectedVessel = currentTarget as Vessel;
            selectedPort = currentTarget as ModuleDockingNode;
            selectedClaw = currentTarget as ModuleGrappleNode;
            if (selectedPort != null)
            {
                selectedVessel = selectedPort.vessel;
            }
            if (selectedClaw != null)
            {
                selectedVessel = selectedClaw.vessel;
            }
            if (vessel.parts.Count != partCount)
            {
                FindReferencePoints();
                //UpdateUndockablesList();
            }
            if (!UpdateCheck())
                return;
            UpdateLists();
        }

        private static string PortOrientationText(Transform reference, Transform subject)
        {
            // Docking ports reference transform 'up' is the actual direction the port is pointing in, so.
            Vector3 portDirection = subject.up;
            const float deviation = 5f;
            if (Vector3.Angle(reference.up, portDirection) < deviation)
            {
                return "(front)";
            }
            else if (Vector3.Angle(-reference.up, portDirection) < deviation)
            {
                return "(back)";
            }
            else if (Vector3.Angle(reference.right, portDirection) < deviation)
            {
                return "(right)";
            }
            else if (Vector3.Angle(-reference.right, portDirection) < deviation)
            {
                return "(left)";
            }
            else if (Vector3.Angle(reference.forward, portDirection) < deviation)
            {
                return "(bottom)";
            }
            else if (Vector3.Angle(-reference.forward, portDirection) < deviation)
            {
                return "(top)";
            }

            return "??";
        }

        // Decouple port menu...
        private void DecouplePort(int index, TextMenu.Item ti)
        {
            var thatPort = undockablesList[index] as ModuleDockingNode;
            var thatClaw = undockablesList[index] as ModuleGrappleNode;
            if (thatPort != null)
            {
                switch (thatPort.state)
                {
                    case "Docked (docker)":
                        thatPort.Undock();
                        break;
                    case "PreAttached":
                        thatPort.Decouple();
                        break;
                }
            }
            // Mihara: Claws require multiple different calls depending on state --
            // Release releases the claw that grabbed something else, while Decouple releases the claw that grabbed your own vessel.
            // FIXME: This needs further research.
            if (thatClaw != null)
            {
                thatClaw.Release();
            }
            UpdateUndockablesList();
            activeMenu = topMenu;
            currentMenu = MenuList.Root;
            UpdateLists();
        }

        private void UpdateUndockablesList()
        {
            undockablesList.Clear();
            foreach (PartModule thatModule in GetUndockableNodes(vessel.parts))
            {
                undockablesList.Add(thatModule);
            }
        }

        private void UpdateLists()
        {

            switch (currentMenu)
            {
                case MenuList.Undock:
                    UpdateUndockablesList();

                    activeMenu.Clear();
                    foreach (PartModule thatUndockable in undockablesList)
                    {
                        var tmi = new TextMenu.Item();
                        tmi.action = DecouplePort;
                        if (thatUndockable is ModuleDockingNode)
                        {
                            var thatPort = thatUndockable as ModuleDockingNode;
                            switch (thatPort.state)
                            {
                                case "Docked (docker)":
                                    tmi.labelText = "Undock " + thatPort.vesselInfo.name;
                                    break;
                                case "PreAttached":
                                    tmi.labelText = string.Format("Detach {0} {1}", GetPortName(thatPort),
                                        PortOrientationText(part.GetReferenceTransform(), thatPort.controlTransform));
                                    break;
                            }
                        }
                        if (thatUndockable is ModuleGrappleNode)
                        {
                            var thatClaw = thatUndockable as ModuleGrappleNode;
                            tmi.labelText = "Claw (" + thatClaw.otherVesselInfo.name + ")";
                        }
                        activeMenu.Add(tmi);
                    }

                    break;
                case MenuList.Celestials:
                    foreach (Celestial body in celestialsList)
                        body.UpdateDistance(vessel.transform.position);

                    CelestialBody currentBody = celestialsList[activeMenu.GetCurrentIndex()].body;
                    switch (sortMode)
                    {
                        case SortMode.Alphabetic:
                            celestialsList.Sort(Celestial.AlphabeticSort);
                            break;
                        case SortMode.Distance:
                            celestialsList.Sort(Celestial.DistanceSort);
                            break;
                    }

                    activeMenu.Clear();

                    foreach (Celestial celestial in celestialsList)
                    {
                        var tmi = new TextMenu.Item();
                        tmi.action = TargetCelestial;
                        tmi.labelText = celestial.name;
                        tmi.rightText = String.Format(fp, distanceFormatString.UnMangleConfigText(), celestial.distance);
                        tmi.isSelected = (selectedCelestial == celestial.body);
                        tmi.isDisabled = (vessel.mainBody == celestial.body);
                        activeMenu.Add(tmi);
                    }

                    break;
                case MenuList.Reference:
                    FindReferencePoints();
                    activeMenu.Clear();

                    Part currentReference = vessel.GetReferenceTransformPart();
                    foreach (PartModule referencePoint in referencePoints)
                    {
                        var tmi = new TextMenu.Item();
                        tmi.action = SetReferencePoint;

                        if (referencePoint is ModuleDockingNode)
                        {
                            var thatPort = referencePoint as ModuleDockingNode;
                            tmi.labelText = string.Format("{0}. {1} {2}", activeMenu.Count + 1, GetPortName(thatPort),
                                PortOrientationText(part.GetReferenceTransform(), thatPort.controlTransform));
                        }
                        else if (referencePoint is ModuleGrappleNode)
                        {
                            var thatClaw = referencePoint as ModuleGrappleNode;
                            tmi.labelText = string.Format("{0}. {1} {2}", activeMenu.Count + 1, referencePoint.part.partInfo.title,
                                PortOrientationText(part.GetReferenceTransform(), thatClaw.controlTransform));
                        }
                        else
                        {
                            tmi.labelText = string.Format("{0}. {1}", activeMenu.Count + 1, referencePoint.part.partInfo.title);
                        }

                        tmi.isSelected = (currentReference == referencePoint.part);
                        activeMenu.Add(tmi);
                    }
                    break;
                case MenuList.Vessels:
                    vesselsList.Clear();
                    foreach (Vessel thatVessel in FlightGlobals.fetch.vessels)
                    {
                        if (vessel != thatVessel)
                        {
                            foreach (var filter in vesselFilter)
                            {
                                if (thatVessel.vesselType == filter.Key && filter.Value)
                                {
                                    vesselsList.Add(new TargetableVessel(thatVessel, vessel.transform.position));
                                    break;
                                }
                            }
                        }
                    }

                    switch (sortMode)
                    {
                        case SortMode.Alphabetic:
                            vesselsList.Sort(TargetableVessel.AlphabeticSort);
                            break;
                        case SortMode.Distance:
                            vesselsList.Sort(TargetableVessel.DistanceSort);
                            break;
                    }
                    activeMenu.Clear();

                    foreach (TargetableVessel targetableVessel in vesselsList)
                    {
                        var tmi = new TextMenu.Item();
                        tmi.action = TargetVessel;
                        tmi.labelText = targetableVessel.name;
                        tmi.rightText = String.Format(fp, distanceFormatString.UnMangleConfigText(), targetableVessel.distance);
                        tmi.isSelected = (selectedVessel == targetableVessel.vessel);
                        activeMenu.Add(tmi);
                    }
                    break;
                case MenuList.SpaceObjects:
                    spaceObjectsList.Clear();
                    foreach (Vessel thatVessel in FlightGlobals.fetch.vessels)
                    {
                        if (vessel != thatVessel)
                        {
                            // MOARdV:
                            // Assuming DiscoveryLevels.StateVectors = "tracked object"
                            // DL.Name is set true as well, but I suspect it
                            // remains true after tracking ceases (not confirmed).
                            // Note that all asteroids are VesselType.SpaceObject.
                            if (thatVessel.vesselType == VesselType.SpaceObject && thatVessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.StateVectors))
                            {
                                spaceObjectsList.Add(new TargetableVessel(thatVessel, vessel.transform.position));
                            }
                        }
                    }

                    switch (sortMode)
                    {
                        case SortMode.Alphabetic:
                            spaceObjectsList.Sort(TargetableVessel.AlphabeticSort);
                            break;
                        case SortMode.Distance:
                            spaceObjectsList.Sort(TargetableVessel.DistanceSort);
                            break;
                    }
                    activeMenu.Clear();

                    foreach (TargetableVessel targetableVessel in spaceObjectsList)
                    {
                        var tmi = new TextMenu.Item();
                        tmi.action = TargetSpaceObject;
                        tmi.labelText = targetableVessel.name;
                        tmi.rightText = String.Format(fp, distanceFormatString.UnMangleConfigText(), targetableVessel.distance);
                        tmi.isSelected = (selectedVessel == targetableVessel.vessel);
                        activeMenu.Add(tmi);
                    }
                    break;
                case MenuList.Ports:
                    UpdatePortsList();

                    activeMenu.Clear();
                    foreach (ModuleDockingNode port in portsList)
                    {
                        var tmi = new TextMenu.Item();
                        tmi.action = TargetPort;
                        tmi.labelText = GetPortName(port);
                        tmi.rightText = PortOrientationText(port.vessel.ReferenceTransform, port.controlTransform);
                        tmi.isSelected = (selectedPort == port);
                        activeMenu.Add(tmi);
                    }
                    break;
                case MenuList.CrewEVA:
                    activeMenu.Clear();
                    var vesselCrew = vessel.GetVesselCrew();
                    for (int crewIdx = 0; crewIdx < vesselCrew.Count; ++crewIdx)
                    {
                        if (vesselCrew[crewIdx] != null)
                        {
                            var tmi = new TextMenu.Item();
                            tmi.action = CrewEVA;
                            tmi.labelText = vesselCrew[crewIdx].name;
                            tmi.rightText = vesselCrew[crewIdx].experienceTrait.Title;
                            tmi.isSelected = false;
                            tmi.id = crewIdx;
                            activeMenu.Add(tmi);
                        }
                    }
                    break;
            }

        }

        private static string GetPortName(ModuleDockingNode port)
        {
            if (dpaiFound)
            {
                PartModule dockingNode = null;
                for (int i = 0; i < port.part.Modules.Count; i++)
                {
                    var module = port.part.Modules[i];
                    if (module.GetType() == dpaiModuleDockingNodeNamed)
                    {
                        dockingNode = module;
                        break;
                    }
                }

                if (dockingNode != null)
                {
                    return (string)dpaiPortName.GetValue(dockingNode);
                }
            }

            return port.part.partInfo.title;
        }

        private void UpdatePortsList()
        {
            unavailablePorts.Clear();
            portsList.Clear();

            // Can be null during docking/undocking?
            if (selectedVessel != null)
            {
                if (!selectedVessel.packed)
                {
                    foreach (uint id in FindUnavailablePorts(selectedVessel))
                    {
                        unavailablePorts.Add(id);
                    }
                    foreach (ModuleDockingNode thatPort in FindAvailablePorts(selectedVessel, unavailablePorts))
                    {
                        portsList.Add(thatPort);
                    }
                }
            }
        }

        private void FindReferencePoints()
        {
            referencePoints.Clear();
            foreach (PartModule thatModule in FindReferenceModules(vessel.Parts))
            {
                referencePoints.Add(thatModule);
            }
            partCount = vessel.parts.Count;
        }

        public void Start()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            rpmComp = RasterPropMonitorComputer.Instantiate(internalProp, true);

            // Grrrrrr.
            if (!string.IsNullOrEmpty(nameColor))
                nameColorValue = ConfigNode.ParseColor32(nameColor);
            if (!string.IsNullOrEmpty(distanceColor))
                distanceColorValue = ConfigNode.ParseColor32(distanceColor);
            if (!string.IsNullOrEmpty(selectedColor))
                selectedColorValue = ConfigNode.ParseColor32(selectedColor);
            if (!string.IsNullOrEmpty(unavailableColor))
                unavailableColorValue = ConfigNode.ParseColor32(unavailableColor);

            persistentVarName = "targetfilter" + internalProp.propID;

            // 7 is the bitmask for ship-station-probe;
            VesselFilterFromBitmask(rpmComp.GetPersistentVariable(persistentVarName, defaultFilter).MassageToInt());

            nameColorTag = JUtil.ColorToColorTag(nameColorValue);
            distanceColorTag = JUtil.ColorToColorTag(distanceColorValue);
            selectedColorTag = JUtil.ColorToColorTag(selectedColorValue);
            unavailableColorTag = JUtil.ColorToColorTag(unavailableColorValue);
            distanceFormatString = distanceFormatString.UnMangleConfigText();
            menuTitleFormatString = menuTitleFormatString.UnMangleConfigText();

            topMenu.labelColor = nameColorTag;
            topMenu.selectedColor = selectedColorTag;
            topMenu.disabledColor = unavailableColorTag;

            if (!string.IsNullOrEmpty(pageTitle))
                pageTitle = pageTitle.UnMangleConfigText();

            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                celestialsList.Add(new Celestial(body, vessel.transform.position));
            }

            FindReferencePoints();
            UpdateUndockablesList();

            var menuActions = new List<Action<int, TextMenu.Item>>();
            menuActions.Add(ShowCelestialMenu);
            menuActions.Add(ShowVesselMenu);
            menuActions.Add(ShowSpaceObjectMenu);
            menuActions.Add(ShowReferenceMenu);
            menuActions.Add(ShowUndockMenu);
            menuActions.Add(ArmGrapple);
            menuActions.Add(ShowFiltersMenu);
            menuActions.Add(ClearTarget);
            menuActions.Add(ShowCrewEVA);

            for (int i = 0; i < rootMenu.Count; ++i)
            {
                var menuitem = new TextMenu.Item();
                menuitem.labelText = rootMenu[i];
                menuitem.action = menuActions[i];
                topMenu.Add(menuitem);
                switch (menuitem.labelText)
                {
                    case clearTargetItemText:
                        clearTarget = topMenu[i];
                        break;
                    case undockItemText:
                        undockMenuItem = topMenu[i];
                        break;
                    case armGrappleText:
                        grappleMenuItem = topMenu[i];
                        break;
                    case crewEvaText:
                        float acLevel = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
                        bool evaUnlocked = GameVariables.Instance.UnlockedEVA(acLevel);
                        menuitem.isDisabled = !GameVariables.Instance.EVAIsPossible(evaUnlocked, vessel);
                        break;
                }
            }

            activeMenu = topMenu;
        }

        private static int VesselFilterToBitmask(Dictionary<VesselType, bool> filterList)
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

        // Returns true if the reference part is a claw and the part can be
        // toggled (state is Ready or state is Disabled).  Also updates the
        // top-menu state text.  Returns whether or not to disable the menu
        // item.
        private bool UpdateReferencePartAsClaw()
        {
            ModuleGrappleNode thatClaw = null;
            Part referencePart = vessel.GetReferenceTransformPart();
            if (referencePart != null)
            {
                foreach (PartModule thatModule in referencePart.Modules)
                {
                    thatClaw = thatModule as ModuleGrappleNode;
                    if (thatClaw != null)
                        break;
                }
            }

            if (thatClaw != null && (thatClaw.state == "Ready" || thatClaw.state == "Disabled"))
            {
                grappleMenuItem.labelText = (thatClaw.state == "Disabled") ? "Arm Grapple" : "Disarm Grapple";

                return false;
            }
            else
            {
                return true;
            }
        }

        //--- Menu item callbacks
        // Root menu:
        private void ShowCelestialMenu(int index, TextMenu.Item ti)
        {
            currentMenu = MenuList.Celestials;

            // MOARdV: Bug warning: rightColumnWidth for ShowCelestialMenu and
            // ShowVesselMenu is hard-coded to 8, which fits the default format
            // string.  It really needs to be sized appropriately, which isn't
            // easy if the format string is configured with non-fixed size.
            // Maybe the format string should be non-configurable?
            //
            // Mihara: Well, why not make it another module parameter then and
            // let the modder who uses it worry about that? Most won't change it.
            activeMenu = new TextMenu();
            activeMenu.rightColumnWidth = distanceColumnWidth;

            activeMenu.labelColor = nameColorTag;
            activeMenu.selectedColor = selectedColorTag;
            activeMenu.disabledColor = unavailableColorTag;
            activeMenu.rightTextColor = distanceColorTag;

            UpdateLists();

            if (selectedCelestial != null)
            {
                int idx = celestialsList.FindIndex(x => x.body == selectedCelestial);
                activeMenu.currentSelection = idx;
            }
        }

        private void ShowVesselMenu(int index, TextMenu.Item ti)
        {
            currentMenu = MenuList.Vessels;

            activeMenu = new TextMenu();
            activeMenu.rightColumnWidth = distanceColumnWidth;

            activeMenu.labelColor = nameColorTag;
            activeMenu.selectedColor = selectedColorTag;
            activeMenu.disabledColor = unavailableColorTag;
            activeMenu.rightTextColor = distanceColorTag;

            UpdateLists();

            if (selectedVessel != null)
            {
                int idx = vesselsList.FindIndex(x => x.vessel == selectedVessel);
                activeMenu.currentSelection = idx;
            }
        }

        private void ShowSpaceObjectMenu(int index, TextMenu.Item ti)
        {
            currentMenu = MenuList.SpaceObjects;

            activeMenu = new TextMenu();
            activeMenu.rightColumnWidth = distanceColumnWidth;

            activeMenu.labelColor = nameColorTag;
            activeMenu.selectedColor = selectedColorTag;
            activeMenu.disabledColor = unavailableColorTag;
            activeMenu.rightTextColor = distanceColorTag;

            UpdateLists();

            if (selectedVessel != null)
            {
                int idx = spaceObjectsList.FindIndex(x => x.vessel == selectedVessel);
                activeMenu.currentSelection = idx;
            }
        }

        private void ShowReferenceMenu(int index, TextMenu.Item ti)
        {
            currentMenu = MenuList.Reference;

            activeMenu = new TextMenu();

            activeMenu.labelColor = nameColorTag;
            activeMenu.selectedColor = selectedColorTag;
            activeMenu.disabledColor = unavailableColorTag;
            activeMenu.rightTextColor = distanceColorTag;

            UpdateLists();

            activeMenu.currentSelection = referencePoints.FindIndex(x => x.part == vessel.GetReferenceTransformPart());
        }

        private void ShowUndockMenu(int index, TextMenu.Item ti)
        {
            currentMenu = MenuList.Undock;

            activeMenu = new TextMenu();

            activeMenu.labelColor = nameColorTag;
            activeMenu.selectedColor = selectedColorTag;
            activeMenu.disabledColor = unavailableColorTag;
            activeMenu.rightTextColor = distanceColorTag;
            UpdateLists();
        }

        private void ShowFiltersMenu(int index, TextMenu.Item ti)
        {
            currentMenu = MenuList.Filters;

            activeMenu = new TextMenu();

            activeMenu.labelColor = nameColorTag;
            activeMenu.selectedColor = selectedColorTag;
            activeMenu.disabledColor = unavailableColorTag;
            activeMenu.rightTextColor = distanceColorTag;
            for (int i = 0; i < vesselFilter.Count; i++)
            {
                var filter = vesselFilter.ElementAt(i);
                var tmi = new TextMenu.Item();
                tmi.labelText = filter.Key.ToString().PadRight(9) + (filter.Value ? "- On" : "- Off");
                tmi.isSelected = filter.Value;
                tmi.action = ToggleFilter;
                activeMenu.Add(tmi);
            }
        }

        private void ShowCrewEVA(int index, TextMenu.Item ti)
        {
            currentMenu = MenuList.CrewEVA;

            activeMenu = new TextMenu();
            activeMenu.labelColor = nameColorTag;
            activeMenu.selectedColor = selectedColorTag;
            activeMenu.disabledColor = unavailableColorTag;
            activeMenu.rightTextColor = distanceColorTag;

            var vesselCrew = vessel.GetVesselCrew();
            for (int crewIdx = 0; crewIdx < vesselCrew.Count; ++crewIdx)
            {
                if (vesselCrew[crewIdx] != null)
                {
                    var tmi = new TextMenu.Item();
                    tmi.action = CrewEVA;
                    tmi.labelText = vesselCrew[crewIdx].name;
                    tmi.rightText = vesselCrew[crewIdx].experienceTrait.Title;
                    tmi.isSelected = false;
                    tmi.id = crewIdx;
                    activeMenu.Add(tmi);
                }
            }

        }

        private static void ClearTarget(int index, TextMenu.Item ti)
        {
            FlightGlobals.fetch.SetVesselTarget((ITargetable)null);
        }

        private void ArmGrapple(int index, TextMenu.Item ti)
        {
            ModuleGrappleNode thatClaw = null;
            Part referencePart = vessel.GetReferenceTransformPart();
            if (referencePart != null)
            {
                foreach (PartModule thatModule in referencePart.Modules)
                {
                    thatClaw = thatModule as ModuleGrappleNode;
                    if (thatClaw != null)
                    {
                        break;
                    }
                }
            }

            if (thatClaw != null)
            {
                try
                {
                    ModuleAnimateGeneric clawAnimation = (referencePart.Modules[thatClaw.deployAnimationController] as ModuleAnimateGeneric);
                    if (clawAnimation != null)
                    {
                        clawAnimation.Toggle();
                    }
                }
                catch (Exception e)
                {
                    JUtil.LogErrorMessage(this, "Exception trying to arm/disarm Grapple Node: {0}", e.Message);
                }

            }
        }

        // Celestial Menu
        private void TargetCelestial(int index, TextMenu.Item ti)
        {
            celestialsList[index].SetTarget();
            selectedVessel = null;
            selectedPort = null;

            activeMenu.SetSelected(index, true);
        }
        // Vessel Menu
        private void TargetVessel(int index, TextMenu.Item ti)
        {
            if (selectedVessel == vesselsList[index].vessel)
            {
                // Already selected.  Are there ports?
                UpdatePortsList();
                if (portsList.Count > 0)
                {
                    currentMenu = MenuList.Ports;

                    activeMenu = new TextMenu();
                    activeMenu.rightColumnWidth = 8;

                    activeMenu.labelColor = nameColorTag;
                    activeMenu.selectedColor = selectedColorTag;
                    activeMenu.disabledColor = unavailableColorTag;
                    activeMenu.rightTextColor = distanceColorTag;

                    UpdateLists();

                    if (selectedPort != null)
                    {
                        int idx = portsList.FindIndex(x => x == selectedPort);
                        activeMenu.currentSelection = idx;
                    }
                }
            }
            else
            {
                vesselsList[index].SetTarget();
                selectedCelestial = null;
                selectedPort = null;

                activeMenu.SetSelected(index, true);
            }
        }
        // Space Object Menu
        private void TargetSpaceObject(int index, TextMenu.Item ti)
        {
            spaceObjectsList[index].SetTarget();
            selectedCelestial = null;
            selectedPort = null;

            activeMenu.SetSelected(index, true);
        }
        // Reference Menu
        private void SetReferencePoint(int index, TextMenu.Item ti)
        {
            // This is going to get complicated...
            if (referencePoints[index].part != vessel.GetReferenceTransformPart())
            {
                referencePoints[index].part.MakeReferencePart();
            }
            activeMenu.SetSelected(index, true);

        }
        // Port Menu
        private void TargetPort(int index, TextMenu.Item ti)
        {
            if (selectedVessel != null && selectedVessel.loaded && portsList[index] != null)
            {
                FlightGlobals.fetch.SetVesselTarget(portsList[index]);
            }
            selectedCelestial = null;

            activeMenu.SetSelected(index, true);
        }
        // Filters Menu
        private void ToggleFilter(int index, TextMenu.Item ti)
        {
            vesselFilter[vesselFilter.ElementAt(index).Key] = !vesselFilter[vesselFilter.ElementAt(index).Key];
            rpmComp.SetPersistentVariable(persistentVarName, VesselFilterToBitmask(vesselFilter));
            ti.isSelected = !ti.isSelected;
            ti.labelText = vesselFilter.ElementAt(index).Key.ToString().PadRight(9) + (ti.isSelected ? "- On" : "- Off");
        }
        // EVA Menu
        private void CrewEVA(int index, TextMenu.Item ti)
        {
            var vesselCrew = vessel.GetVesselCrew();
            float acLevel = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
            bool evaUnlocked = GameVariables.Instance.UnlockedEVA(acLevel);
            bool evaPossible = GameVariables.Instance.EVAIsPossible(evaUnlocked, vessel);
            if (evaPossible && ti.id < vesselCrew.Count && vesselCrew[ti.id] != null && HighLogic.CurrentGame.Parameters.Flight.CanEVA)
            {
                FlightEVA.SpawnEVA(vesselCrew[ti.id].KerbalRef);
                CameraManager.Instance.SetCameraFlight();
            }
        }

        // Iterator helpers
        private static IEnumerable<ModuleDockingNode> FindAvailablePorts(Vessel selectedVessel, List<uint> unavailablePorts)
        {
            foreach (Part thatPart in selectedVessel.parts)
            {
                foreach (PartModule thatModule in thatPart.Modules)
                {
                    if (thatModule is ModuleDockingNode && !unavailablePorts.Contains(thatModule.part.flightID))
                    {
                        yield return (thatModule as ModuleDockingNode);
                    }
                }
            }
        }

        private static IEnumerable<PartModule> FindReferenceModules(List<Part> parts)
        {
            foreach (Part thatPart in parts)
            {
                foreach (PartModule thatModule in thatPart.Modules)
                {
                    if (thatModule is ModuleDockingNode || thatModule is ModuleCommand || thatModule is ModuleGrappleNode)
                    {
                        yield return thatModule;
                    }
                }
            }
        }

        private static IEnumerable<uint> FindUnavailablePorts(Vessel selectedVessel)
        {
            foreach (Part thatPart in selectedVessel.parts)
            {
                foreach (PartModule thatModule in thatPart.Modules)
                {
                    if (thatModule is ModuleDockingNode)
                    {
                        var thatPort = thatModule as ModuleDockingNode;
                        if (thatPort.state.ToLower().Contains("docked"))
                        {
                            yield return thatPort.part.flightID;
                            yield return thatPort.dockedPartUId;
                        }
                    }
                }
            }
        }

        private static IEnumerable<PartModule> GetUndockableNodes(List<Part> parts)
        {
            foreach (Part thatPart in parts)
            {
                foreach (PartModule thatModule in thatPart.Modules)
                {
                    if (thatModule is ModuleDockingNode)
                    {
                        var thatPort = thatModule as ModuleDockingNode;
                        if (thatPort.state == "Docked (docker)" || thatPort.state == "PreAttached")
                        {
                            yield return thatModule;
                        }
                    }
                    else if (thatModule is ModuleGrappleNode)
                    {
                        var thatClaw = thatModule as ModuleGrappleNode;
                        if (thatClaw.state == "Grappled")
                        {
                            yield return thatModule;
                        }

                    }
                }
            }
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

            public static int DistanceSort(Celestial first, Celestial second)
            {
                return first.distance.CompareTo(second.distance);
            }

            public static int AlphabeticSort(Celestial first, Celestial second)
            {
                return string.Compare(first.name, second.name, StringComparison.Ordinal);
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

            public static int DistanceSort(TargetableVessel first, TargetableVessel second)
            {
                if (first.vessel == null || second.vessel == null)
                    return 0;
                return first.distance.CompareTo(second.distance);
            }

            public static int AlphabeticSort(TargetableVessel first, TargetableVessel second)
            {
                if (first.vessel == null || second.vessel == null)
                    return 0;
                return string.Compare(first.name, second.name, StringComparison.Ordinal);
            }
        }
    }
}

