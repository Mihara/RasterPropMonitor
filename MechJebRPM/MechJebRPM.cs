using System;
using System.Text;
using System.Collections.Generic;
using MuMech;

// I had to add these as explicit "using" instead of pulling in all of JSI
// because JSI.OrbitExtensions clashes with MuMech.OrbitExtensions, and this
// resulted in the least disruptive way to get around that.
using TextMenu = JSI.TextMenu;
using JUtil = JSI.JUtil;
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
		public string itemColor = string.Empty;
		private Color itemColorValue = Color.white;
		[KSPField]
		public string selectedColor = string.Empty;
		private Color selectedColorValue = Color.green;
		[KSPField]
		public string unavailableColor = string.Empty;
		private Color unavailableColorValue = Color.gray;
		[KSPField]
		public float forceRollStep = 90.0f;
		// KSPFields end here.
		private enum MJMenu
		{
			RootMenu,
			OrbitMenu,
			SurfaceMenu,
			TargetMenu,
			ExecuteNodeMenu,
			AscentGuidanceMenu,
			LandingGuidanceMenu,
			DockingGuidanceMenu,
			CircularizeMenu,
			//SpacePlaneMenu,
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
		private MechJebCore activeJeb;
		private MechJebModuleSmartASS activeSmartass;
		private bool pageActiveState;

		private string GetActiveMode()
		{
			if (activeSmartass != null) {
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
			return MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Target.OFF];
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
					case MJMenu.CircularizeMenu:
						UpdateCircularizeMenu();
						break;
				}

				result.Append(activeMenu.ShowMenu(width, height));
			} else {
				if (activeJeb == null)
					result.AppendLine("Autopilot not found.");
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
				if (currentMenu == MJMenu.RootMenu && activeMenu.currentSelection == 5 && activeSmartass != null) {
					// If Force Roll is highlighted, the Home key will increment the
					// roll value.
					double currentRoll = (double)activeSmartass.rol + forceRollStep;
					if (currentRoll > 180.0) {
						currentRoll -= 360.0;
					} else if (currentRoll < -180.0) {
						currentRoll += 360.0;
					}
					activeSmartass.rol = currentRoll;
					if (forceRollMenuItem.isSelected) {
						activeSmartass.Engage();
					}
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

			// I guess I shouldn't have expected Squad to actually do something nice for a modder like that.
			// In 0.23, loading in non-alphabetical order is still broken.

			// But now we have KSPAssembly and KSPAssemblyDependency, which actually sidestep the issue, and finally
			// Mu told someone about it and now I can avoid this path hardlinking.
			// Actually, better yet. Let it check for the new canonical location instead. Because fuck installation problems.
			if (!JSI.InstallationPathWarning.Warn())
				return;

			if (!string.IsNullOrEmpty(itemColor))
				itemColorValue = ConfigNode.ParseColor32(itemColor);
			if (!string.IsNullOrEmpty(selectedColor))
				selectedColorValue = ConfigNode.ParseColor32(selectedColor);
			if (!string.IsNullOrEmpty(unavailableColor))
				unavailableColorValue = ConfigNode.ParseColor32(unavailableColor);

			UpdateJebReferences();

			topMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
			topMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
			topMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);

			// If MechJeb is installed, but not found on the craft, menu options can't be populated correctly.
			if (activeJeb != null) {
				topMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)MechJebModuleSmartASS.Target.OFF], SmartASS_Off));
				topMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)MechJebModuleSmartASS.Target.KILLROT].Replace('\n', ' '), SmartASS_KillRot));
				nodeMenuItem = new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)MechJebModuleSmartASS.Target.NODE], SmartASS_Node);
				topMenu.Add(nodeMenuItem);
				topMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.ORBITAL], OrbitalMenu));
				targetMenuItem = new TextMenu.Item(MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.TARGET], TargetMenu);
				topMenu.Add(targetMenuItem);
				// Analysis disable once RedundantCast
				forceRollMenuItem = new TextMenu.Item(String.Format("Force Roll: {0:f0}", (activeSmartass != null) ? (double)activeSmartass.rol : 0.0), ToggleForceRoll);
				topMenu.Add(forceRollMenuItem);
				topMenu.Add(new TextMenu.Item("Execute Next Node", ExecuteNode, (int)MJMenu.ExecuteNodeMenu));
				topMenu.Add(new TextMenu.Item("Ascent Guidance", AscentGuidance, (int)MJMenu.AscentGuidanceMenu));
				topMenu.Add(new TextMenu.Item("Land Somewhere", LandingGuidance, (int)MJMenu.LandingGuidanceMenu));
				topMenu.Add(new TextMenu.Item("Docking Guidance", DockingGuidance, (int)MJMenu.DockingGuidanceMenu));
				//topMenu.Add(new TextMenu.Item("Hold Alt & Heading", SpaceplaneGuidance, (int)MJMenu.SpacePlaneMenu));
				topMenu.Add(new TextMenu.Item("Circularize", CircularizeMenu, (int)MJMenu.CircularizeMenu));
			}
			activeMenu = topMenu;
		}
		//--- ROOT MENU methods
		private void UpdateRootMenu()
		{
			activeMenu.menuTitle = "== Root Menu: " + GetActiveMode();

			targetMenuItem.isDisabled = (FlightGlobals.fetch.VesselTarget == null);
			if (vessel.patchedConicSolver != null) {
				nodeMenuItem.isDisabled = (vessel.patchedConicSolver.maneuverNodes.Count == 0);
			} else {
				nodeMenuItem.isDisabled = true;
			}
			// Analysis disable once RedundantCast
			forceRollMenuItem.labelText = String.Format("Force Roll: {0:+0;-0;0}", (activeSmartass != null) ? (double)activeSmartass.rol : 0.0);

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
			if (activeSmartass != null) {
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
			} else {
				activeMenu.SetSelected(0, true);
				forceRollMenuItem.isSelected = false;
			}

			TextMenu.Item item;

			item = activeMenu.Find(x => x.id == (int)MJMenu.ExecuteNodeMenu);
			if (item != null) {
				var mp = activeJeb.GetComputerModule<MechJebModuleManeuverPlanner>();
				if (mp != null) {
					item.isSelected = false;
					item.labelText = (activeJeb.node.enabled) ? "Abort Node Execution" : "Execute Next Node";
					if (vessel.patchedConicSolver != null) {
						item.isDisabled = (vessel.patchedConicSolver.maneuverNodes.Count == 0);
					} else {
						item.isDisabled = true;
					}
				} else {
					item.isSelected = false;
					item.labelText = "Execute Next Node";
					item.isDisabled = true;
				}
			}

			item = activeMenu.Find(x => x.id == (int)MJMenu.AscentGuidanceMenu);
			if (item != null) {
				var ascentAP = activeJeb.GetComputerModule<MechJebModuleAscentAutopilot>();
				if (ascentAP == null) {
					item.isSelected = false;
					item.isDisabled = true;
				} else {
					item.isSelected = ascentAP.enabled;
					item.isDisabled = false;
				}
			}

			item = activeMenu.Find(x => x.id == (int)MJMenu.LandingGuidanceMenu);
			if (item != null) {
				var landingAP = activeJeb.GetComputerModule<MechJebModuleLandingAutopilot>();
				if (landingAP == null) {
					item.isSelected = false;
					item.isDisabled = true;
				} else {
					item.labelText = (activeJeb.target.PositionTargetExists) ? "Land at Target" : "Land Somewhere";
					item.isSelected = landingAP.enabled;
					item.isDisabled = false;
				}
			}

			item = activeMenu.Find(x => x.id == (int)MJMenu.DockingGuidanceMenu);
			if (item != null) {
				var dockingAP = activeJeb.GetComputerModule<MechJebModuleDockingAutopilot>();
				if (dockingAP == null) {
					item.isSelected = false;
					item.isDisabled = true;
				} else {
					item.isSelected = dockingAP.enabled;
					item.isDisabled = !(activeJeb.target.Target is ModuleDockingNode);
				}
			}

			//item = activeMenu.Find(x => x.id == (int)MJMenu.SpacePlaneMenu);
			//if (item != null) {
			//	var headingAP = activeJeb.GetComputerModule<MechJebModuleSpaceplaneAutopilot>();
			//	if (headingAP == null) {
			//		item.isSelected = false;
			//		item.isDisabled = true;
			//	} else {
			//		item.isSelected = (headingAP.enabled && headingAP.mode == MechJebModuleSpaceplaneAutopilot.Mode.HOLD);
			//		item.isDisabled = (headingAP.mode == MechJebModuleSpaceplaneAutopilot.Mode.AUTOLAND);
			//	}
			//}

			item = activeMenu.Find(x => x.id == (int)MJMenu.CircularizeMenu);
			if (item != null) {
				item.isDisabled = vessel.LandedOrSplashed;
			}
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
			activeMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
			activeMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
			activeMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);

			foreach (MechJebModuleSmartASS.Target target in orbitalTargets) {
				activeMenu.Add(new TextMenu.Item(MechJebModuleSmartASS.TargetTexts[(int)target].Replace('\n', ' '), SelectTarget));
			}
		}

		private void TargetMenu(int index, TextMenu.Item tmi)
		{
			currentMenu = MJMenu.TargetMenu;

			activeMenu = new TextMenu();
			activeMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
			activeMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
			activeMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);

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

		private void AscentGuidance(int index, TextMenu.Item tmi)
		{
			UpdateJebReferences();
			if (activeJeb != null) {
				MechJebModuleAscentAutopilot ap = activeJeb.GetComputerModule<MechJebModuleAscentAutopilot>();
				if (ap == null) {
					return;
				}

				var agPilot = activeJeb.GetComputerModule<MechJebModuleAscentGuidance>();
				if (agPilot != null) {
					if (ap.enabled) {
						ap.users.Remove(agPilot);
					} else {
						ap.users.Add(agPilot);
					}
				}
			}
		}

		private void LandingGuidance(int index, TextMenu.Item tmi)
		{
			UpdateJebReferences();
			if (activeJeb != null) {
				var autopilot = activeJeb.GetComputerModule<MechJebModuleLandingAutopilot>();

				if (autopilot == null) {
					return;
				}

				if (autopilot.enabled) {
					autopilot.StopLanding();
				} else {
					var landingGuidanceAP = activeJeb.GetComputerModule<MechJebModuleLandingGuidance>();
					if (landingGuidanceAP != null) {
						if (activeJeb.target.PositionTargetExists) {
							autopilot.LandAtPositionTarget(landingGuidanceAP);
						} else {
							autopilot.LandUntargeted(landingGuidanceAP);
						}
					}
				}
			}
		}

		private void DockingGuidance(int index, TextMenu.Item tmi)
		{
			UpdateJebReferences();
			if (activeJeb != null) {
				var autopilot = activeJeb.GetComputerModule<MechJebModuleDockingAutopilot>();
				if (autopilot != null) {
					var autopilotController = activeJeb.GetComputerModule<MechJebModuleDockingGuidance>();
					if (autopilotController != null) {
						if (autopilot.enabled) {
							autopilot.users.Remove(autopilotController);
						} else if (activeJeb.target.Target is ModuleDockingNode) {
							if (autopilot.speedLimit < 0) {
								autopilot.speedLimit = 0;
							}
							autopilot.users.Add(autopilotController);
						}
					}
				}
			}
		}

		// MOARdV: Spaceplane Guidance can not be implemented cleanly, because
		// MJ's MechJebModuleSpaceplaneGuidance is missing the 'public'
		// keyword.  We could use another controller (like ourself), but that
		// means one is forced to use our menu to turn it off (the MJ GUI is
		// not able to change the setting), and vice versa.  Since every other
		// place where we interface with MJ, we use MJ's objects as the
		// controller, this breaks our design model.  If/when MJ makes the
		// module public, all of the commented code here related to it can be
		// uncommented, and this missive can be deleted.
		//private void SpaceplaneGuidance(int index, TextMenu.Item tmi)
		//{
		//	UpdateJebReferences();
		//	if (activeJeb != null) {
		//		var autopilot = activeJeb.GetComputerModule<MechJebModuleSpaceplaneAutopilot>();
		//		if (autopilot != null) {
		//			MechJebModuleSpaceplaneGuidance is not currently public.  Can't use it.
		//			var autopilotController = activeJeb.GetComputerModule<MechJebModuleSpaceplaneGuidance>();
		//			if (autopilotController != null) {
		//				if (autopilot.enabled && autopilot.mode == MechJebModuleSpaceplaneAutopilot.Mode.HOLD) {
		//					autopilot.AutopilotOff();
		//				} else if (!autopilot.enabled) {
		//					autopilot.HoldHeadingAndAltitude(autopilotController);
		//				}
		//			}
		//		}
		//	}
		//}

		private void CircularizeMenu(int index, TextMenu.Item tmi)
		{
			currentMenu = MJMenu.CircularizeMenu;

			activeMenu = new TextMenu();
			activeMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
			activeMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
			activeMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);
			activeMenu.menuTitle = "== Circularize Menu:";

			activeMenu.Add(new TextMenu.Item("At Next Ap", DoCircularize, (int)TimeReference.APOAPSIS));
			activeMenu.Add(new TextMenu.Item("At Next Pe", DoCircularize, (int)TimeReference.PERIAPSIS));
			activeMenu.Add(new TextMenu.Item("In 15s", DoCircularize, (int)TimeReference.X_FROM_NOW));
		}

		//--- Orbital Menu
		private void UpdateOrbitalMenu()
		{
			activeMenu.menuTitle = "== " + MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.ORBITAL] + " Menu: " + GetActiveMode();

			if (activeSmartass != null) {
				int idx = orbitalTargets.FindIndex(x => x == activeSmartass.target);
				if (idx >= 0 && idx < orbitalTargets.Count) {
					activeMenu.SetSelected(idx, true);
				}
			}
		}
		//--- Target Menu
		private void UpdateTargetMenu()
		{
			activeMenu.menuTitle = "== " + MechJebModuleSmartASS.ModeTexts[(int)MechJebModuleSmartASS.Mode.TARGET] + " Menu: " + GetActiveMode();

			if (activeSmartass != null) {
				int idx = targetTargets.FindIndex(x => x == activeSmartass.target);
				if (idx >= 0 && idx < targetTargets.Count) {
					activeMenu.SetSelected(idx, true);
				}
			}
		}
		//--- Circularize Menu
		private void UpdateCircularizeMenu()
		{
			// If the menu works, the only thing we won't allow is
			// "circularize at Ap" when we're hyperbolic.

			var AtAp = activeMenu.Find(x => x.id == (int)TimeReference.APOAPSIS);
			if (AtAp != null) {
				AtAp.isDisabled = (vessel.orbit.eccentricity >= 1.0);
			}
		}

		private void DoCircularize(int index, TextMenu.Item tmi)
		{
			double UT = 0.0;
			Vector3d dV = Vector3d.zero;
			Orbit o = vessel.orbit;

			switch (tmi.id) {
				case (int)TimeReference.APOAPSIS:
					UT = o.NextApoapsisTime(Planetarium.GetUniversalTime());
					dV = OrbitalManeuverCalculator.DeltaVToCircularize(o, UT);
					break;
				case (int)TimeReference.PERIAPSIS:
					UT = o.NextPeriapsisTime(Planetarium.GetUniversalTime());
					dV = OrbitalManeuverCalculator.DeltaVToCircularize(o, UT);
					break;
				case (int)TimeReference.X_FROM_NOW:
					UT = Planetarium.GetUniversalTime() + 15.0;
					dV = OrbitalManeuverCalculator.DeltaVToCircularize(o, UT);
					break;
			}

			if (UT > 0.0) {
				vessel.PlaceManeuverNode(o, dV, UT);
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
			}

			UpdateJebReferences();

			if (activeSmartass != null && activeTargets != null) {
				activeSmartass.target = activeTargets[index];
				activeSmartass.Engage();
			}
		}
	}
}
