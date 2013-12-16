using System;
using System.Text;
using System.Collections.Generic;
using JSI;
using MuMech;
using UnityEngine;

namespace MechJebRPM
{
	public class MechJebRPM: InternalModule
	{
		[KSPField]
		public string pageTitle = string.Empty;
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
		public Color32 itemColor = Color.white;
		[KSPField]
		public Color32 selectedColor = Color.green;
		[KSPField]
		public Color32 unavailableColor = Color.gray;
		[KSPField]
		public float forceRollStep = 90.0f;
		// KSPFields end here.
		private enum MJMenu
		{
			RootMenu,
			OrbitMenu,
			SurfaceMenu,
			TargetMenu,
			AdvancedMenu,
		};

		private readonly List<MechJebModuleSmartASS.Target> orbitalTargets = new List<MechJebModuleSmartASS.Target> {
			MechJebModuleSmartASS.Target.PROGRADE,
			MechJebModuleSmartASS.Target.RETROGRADE,
			MechJebModuleSmartASS.Target.NORMAL_PLUS,
			MechJebModuleSmartASS.Target.NORMAL_MINUS,
			MechJebModuleSmartASS.Target.RADIAL_PLUS,
			MechJebModuleSmartASS.Target.RADIAL_MINUS,
		};
		private readonly List<MechJebModuleSmartASS.Target> targetTargets = new List<MechJebModuleSmartASS.Target> {
			MechJebModuleSmartASS.Target.TARGET_PLUS,
			MechJebModuleSmartASS.Target.TARGET_MINUS,
			MechJebModuleSmartASS.Target.RELATIVE_PLUS,
			MechJebModuleSmartASS.Target.RELATIVE_MINUS,
			MechJebModuleSmartASS.Target.PARALLEL_PLUS,
			MechJebModuleSmartASS.Target.PARALLEL_MINUS,
		};
		private MJMenu currentMenu = MJMenu.RootMenu;
		private readonly TextMenu topMenu = new TextMenu();
		private TextMenu activeMenu;
		// Actively track some menu items, since their validity can be
		// updated asynchronously.
		private TextMenu.Item nodeMenuItem;
		private TextMenu.Item targetMenuItem;
		private TextMenu.Item forceRollMenuItem;
		private TextMenu.Item executeNodeItem;
		private MechJebCore activeJeb;
		private MechJebModuleSmartASS activeSmartass;
		private bool pageActiveState;

		private string GetActiveMode()
		{
			// It appears the SmartASS module does not know if MJ is in
			// automatic pilot mode (like Ascent Guidance or Landing
			// Guidance) without querying indirectly like this.
			// MOARdV BUG: This doesn't seem to work if any of the
			// attitude settings are active (like "Prograde").
			if (activeJeb.attitude.enabled && !activeJeb.attitude.users.Contains(activeSmartass)) {
				return MechJebModuleSmartASS.TargetTexts[(int)MechJebModuleSmartASS.Target.AUTO].Replace('\n', ' ');
			}
			return MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Target2Mode[(int)activeSmartass.target]] + " " + MechJebModuleSmartASS.TargetTexts[(int)activeSmartass.target].Replace('\n', ' ');
		}

		public string ShowMenu(int width, int height)
		{
			UpdateJebReferences();

			var result = new StringBuilder();
			if (!string.IsNullOrEmpty(pageTitle)) {
				result.AppendLine(pageTitle);
				height--;
			}

			if (activeSmartass != null) {
				switch (currentMenu) {
					case MJMenu.RootMenu:
						UpdateRootMenu();
						break;
					case MJMenu.OrbitMenu:
						UpdateOrbitalMenu();
						break;
					case MJMenu.SurfaceMenu:
						activeMenu.menuTitle = "== " + MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.SURFACE] + " Menu: " + GetActiveMode();
						break;
					case MJMenu.TargetMenu:
						UpdateTargetMenu();
						break;
					case MJMenu.AdvancedMenu:
						activeMenu.menuTitle = "== " + MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.ADVANCED] + " Menu: " + GetActiveMode();
						break;
				}

				result.Append(activeMenu.ShowMenu(width, height));
			} else {
				if (activeJeb == null)
					result.AppendLine("Autopilot not installed.");
				else
					result.AppendLine("Attitude control unavailable.");
			}

			return result.ToString();
		}
		// Analysis disable once UnusedParameter
		public void PageActive(bool active, int pageNumber)
		{
			pageActiveState = active;
		}

		public void ClickProcessor(int buttonID)
		{
			if (buttonID == buttonUp) {
				activeMenu.PreviousItem();
			}
			if (buttonID == buttonDown) {
				activeMenu.NextItem();
			}
			if (buttonID == buttonEnter) {
				activeMenu.SelectItem();
			}
			if (buttonID == buttonEsc) {
				activeMenu = topMenu;
				currentMenu = MJMenu.RootMenu;
			}
			if (buttonID == buttonHome) {
				if (currentMenu == MJMenu.RootMenu && activeMenu.currentSelection == 5) {
					// If Force Roll is highlighted, the Home key will increment the
					// roll value.
					double currentRoll = (double)activeSmartass.rol + forceRollStep;
					if (currentRoll > 180.0) {
						currentRoll -= 360.0;
					} else if (currentRoll < -180.0) {
						currentRoll += 360.0;
					}
					activeSmartass.rol = currentRoll;
				}
			}
		}
		/* Note to self:
		foreach (ThatEnumType item in (ThatEnumType[]) Enum.GetValues(typeof(ThatEnumType)))
		can save a lot of time here.
		*/
		private void UpdateJebReferences()
		{
			activeJeb = vessel.GetMasterMechJeb();
			// Node executor is activeJeb.node
			activeSmartass = activeJeb != null ? activeJeb.GetComputerModule<MechJebModuleSmartASS>() : null;
		}

		public void Start()
		{

			// With the release of 0.23 this should hopefully change to InstallationPathWarning.Warn();
			InstallationPathWarning.Warn("MechJebRPM");

			UpdateJebReferences();

			topMenu.labelColor = JUtil.ColorToColorTag(itemColor);
			topMenu.selectedColor = JUtil.ColorToColorTag(selectedColor);
			topMenu.disabledColor = JUtil.ColorToColorTag(unavailableColor);

			topMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)MechJebModuleSmartASS.Target.OFF], SmartASS_Off));
			topMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)MechJebModuleSmartASS.Target.KILLROT].Replace('\n', ' '), SmartASS_KillRot));
			nodeMenuItem = new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)MechJebModuleSmartASS.Target.NODE], SmartASS_Node);
			topMenu.Add(nodeMenuItem);
			topMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.ORBITAL], OrbitalMenu));
			targetMenuItem = new TextMenu.Item(MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.TARGET], TargetMenu);
			topMenu.Add(targetMenuItem);
			forceRollMenuItem = new TextMenu.Item(String.Format("Force Roll: {0:f0}", activeSmartass.rol), ToggleForceRoll);
			topMenu.Add(forceRollMenuItem);
			executeNodeItem = new TextMenu.Item("Execute Next Node", ExecuteNode);
			topMenu.Add(executeNodeItem);
			// MOARdV: The following two menu items are not implemented.  I removed
			// them to avoid confusion.
			//topMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.SURFACE], null, false, "", true));
			//topMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.ADVANCED], null, false, "", true));

			activeMenu = topMenu;
		}
		//--- ROOT MENU methods
		private void UpdateRootMenu()
		{
			activeMenu.menuTitle = "== Root Menu: " + GetActiveMode();

			targetMenuItem.isDisabled = (FlightGlobals.fetch.VesselTarget == null);
			nodeMenuItem.isDisabled = (vessel.patchedConicSolver.maneuverNodes.Count == 0);
			forceRollMenuItem.labelText = String.Format("Force Roll - {0:f0}", activeSmartass.rol);

			MechJebModuleManeuverPlanner mp = null;
			if (activeJeb != null) {
				mp = activeJeb.GetComputerModule<MechJebModuleManeuverPlanner>();
				executeNodeItem.labelText = (activeJeb.node.enabled) ? "Abort Node Execution" : "Execute Next Node";
			} else {
				executeNodeItem.labelText = "Execute Next Node";
			}
			executeNodeItem.isDisabled = (mp == null || vessel.patchedConicSolver.maneuverNodes.Count == 0);

			// MOARdV:
			// This is a little messy, since SmartASS can be updated
			// asynchronously from our perspective.  It is complicated
			// because some Target values do not have corresponding
			// modes (OFF, for instance).  I am sure there's a cleaner way to
			// manage this, but I want to get basic functionality first, and
			// pretty code later.  Currently, I put the OFF, KILL ROT, and NODE
			// buttons on the root menu, just like they're on the top-level
			// of the SmartASS window in MJ.
			// It is also fragile because I am using hard-coded numbers here
			// and I have to hope that no one rearranges the root menu.  This
			// will be addressed in a future iteration of the code.
			if (activeSmartass.target == MechJebModuleSmartASS.Target.OFF) {
				activeMenu.SetSelected(0, true);
			} else if (activeSmartass.target == MechJebModuleSmartASS.Target.KILLROT) {
				activeMenu.SetSelected(1, true);
			} else if (activeSmartass.target == MechJebModuleSmartASS.Target.NODE) {
				activeMenu.SetSelected(2, true);
			} else if (MechJebModuleSmartASS.Target2Mode[(int)activeSmartass.target] == MechJebModuleSmartASS.Mode.ORBITAL) {
				activeMenu.SetSelected(3, true);
			} else if (MechJebModuleSmartASS.Target2Mode[(int)activeSmartass.target] == MechJebModuleSmartASS.Mode.TARGET) {
				activeMenu.SetSelected(4, true);
			}
			// 5 is Force Roll.  State is controlled below, and is independent
			// of the rest of these
			// 6 is Execute Next Node.
			else if (MechJebModuleSmartASS.Target2Mode[(int)activeSmartass.target] == MechJebModuleSmartASS.Mode.SURFACE) {
				activeMenu.SetSelected(7, true);
			} else if (MechJebModuleSmartASS.Target2Mode[(int)activeSmartass.target] == MechJebModuleSmartASS.Mode.ADVANCED) {
				activeMenu.SetSelected(8, true);
			}

			forceRollMenuItem.isSelected = activeSmartass.forceRol;
		}

		private void SmartASS_Off(int index, TextMenu.Item tmi)
		{
			UpdateJebReferences();

			if (activeSmartass != null) {
				activeSmartass.target = MechJebModuleSmartASS.Target.OFF;
				activeSmartass.Engage();
			}
		}

		private void SmartASS_KillRot(int index, TextMenu.Item tmi)
		{
			UpdateJebReferences();

			if (activeSmartass != null) {
				activeSmartass.target = MechJebModuleSmartASS.Target.KILLROT;
				activeSmartass.Engage();
			}
		}

		private void SmartASS_Node(int index, TextMenu.Item tmi)
		{
			UpdateJebReferences();

			if (activeSmartass != null) {
				activeSmartass.target = MechJebModuleSmartASS.Target.NODE;
				activeSmartass.Engage();
			}
		}

		private void OrbitalMenu(int index, TextMenu.Item tmi)
		{
			currentMenu = MJMenu.OrbitMenu;

			activeMenu = new TextMenu();
			activeMenu.labelColor = JUtil.ColorToColorTag(itemColor);
			activeMenu.selectedColor = JUtil.ColorToColorTag(selectedColor);
			activeMenu.disabledColor = JUtil.ColorToColorTag(unavailableColor);

			foreach (MechJebModuleSmartASS.Target target in orbitalTargets) {
				activeMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)target].Replace('\n', ' '), SelectTarget));
			}
		}

		private void TargetMenu(int index, TextMenu.Item tmi)
		{
			currentMenu = MJMenu.TargetMenu;

			activeMenu = new TextMenu();
			activeMenu.labelColor = JUtil.ColorToColorTag(itemColor);
			activeMenu.selectedColor = JUtil.ColorToColorTag(selectedColor);
			activeMenu.disabledColor = JUtil.ColorToColorTag(unavailableColor);

			foreach (MechJebModuleSmartASS.Target target in targetTargets) {
				activeMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)target].Replace('\n', ' '), SelectTarget));
			}
		}

		private void ExecuteNode(int index, TextMenu.Item tmi)
		{
			UpdateJebReferences();
			if (activeJeb != null) {
				MechJebModuleManeuverPlanner mp = activeJeb.GetComputerModule<MechJebModuleManeuverPlanner>();
				if (mp != null) {
					// We have a valid maneuver planner, which means we can
					// tell MJ to execute a node.  Or abort a node.
					if (activeJeb.node.enabled) {
						activeJeb.node.Abort();
					} else {
						activeJeb.node.ExecuteOneNode(mp);
					}
				}
			}
		}

		private void ToggleForceRoll(int index, TextMenu.Item tmi)
		{
			UpdateJebReferences();
			if (activeSmartass != null) {
				activeSmartass.forceRol = !activeSmartass.forceRol;
				forceRollMenuItem.isSelected = activeSmartass.forceRol;
				activeSmartass.Engage();
			}
		}
		//--- Orbital Menu
		private void UpdateOrbitalMenu()
		{
			activeMenu.menuTitle = "== " + MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.ORBITAL] + " Menu: " + GetActiveMode();

			int idx = orbitalTargets.FindIndex(x => x == activeSmartass.target);
			if (idx >= 0 && idx < orbitalTargets.Count) {
				activeMenu.SetSelected(idx, true);
			}
		}
		//--- Target Menu
		private void UpdateTargetMenu()
		{
			activeMenu.menuTitle = "== " + MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.TARGET] + " Menu: " + GetActiveMode();

			int idx = targetTargets.FindIndex(x => x == activeSmartass.target);
			if (idx >= 0 && idx < targetTargets.Count) {
				activeMenu.SetSelected(idx, true);
			}
		}

		private void SelectTarget(int index, TextMenu.Item tmi)
		{
			List<MechJebModuleSmartASS.Target> activeTargets = null;
			switch (currentMenu) {
				case MJMenu.OrbitMenu:
					activeTargets = orbitalTargets;
					break;
				case MJMenu.SurfaceMenu:
					break;
				case MJMenu.TargetMenu:
					activeTargets = targetTargets;
					break;
				case MJMenu.AdvancedMenu:
					break;
			}

			UpdateJebReferences();

			if (activeSmartass != null && activeTargets != null) {
				activeSmartass.target = activeTargets[index];
				activeSmartass.Engage();
			}
		}
	}
}
