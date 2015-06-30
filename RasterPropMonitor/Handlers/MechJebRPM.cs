using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JSI
{
    class MechJebRPM : InternalModule
    {
        [KSPField]
        public string pageTitle = string.Empty;
        [KSPField]
        public int buttonUp = 0;
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

        private readonly JSIMechJeb.Target[] orbitalTargets = new JSIMechJeb.Target[] {
			JSIMechJeb.Target.PROGRADE,
			JSIMechJeb.Target.RETROGRADE,
			JSIMechJeb.Target.NORMAL_PLUS,
			JSIMechJeb.Target.NORMAL_MINUS,
			JSIMechJeb.Target.RADIAL_PLUS,
			JSIMechJeb.Target.RADIAL_MINUS,
		};
        private readonly JSIMechJeb.Target[] surfaceTargets = new JSIMechJeb.Target[] {
			JSIMechJeb.Target.SURFACE_PROGRADE,
			JSIMechJeb.Target.SURFACE_RETROGRADE,
			JSIMechJeb.Target.HORIZONTAL_PLUS,
			JSIMechJeb.Target.HORIZONTAL_MINUS,
			JSIMechJeb.Target.VERTICAL_PLUS,
		};
        private readonly JSIMechJeb.Target[] targetTargets = new JSIMechJeb.Target[] {
			JSIMechJeb.Target.TARGET_PLUS,
			JSIMechJeb.Target.TARGET_MINUS,
			JSIMechJeb.Target.RELATIVE_PLUS,
			JSIMechJeb.Target.RELATIVE_MINUS,
			JSIMechJeb.Target.PARALLEL_PLUS,
			JSIMechJeb.Target.PARALLEL_MINUS,
		};
        private MJMenu currentMenu = MJMenu.RootMenu;
        private readonly TextMenu topMenu = new TextMenu();
        private TextMenu activeMenu;
        // Actively track some menu items, since their validity can be
        // updated asynchronously.
        private TextMenu.Item nodeMenuItem;
        private TextMenu.Item targetMenuItem;
        private TextMenu.Item forceRollMenuItem;
        private bool pageActiveState;

        private Func<double> GetForceRollAngle;
        private Func<int> GetSmartassMode;
        private Action<JSIMechJeb.Target> SetSmartassMode;
        private Action<bool, double> SetForceRoll;
        private Func<string, bool> GetModuleExists;
        private Action<double> CircularizeAt;
        private Func<bool> PositionTargetExists;
        private Func<bool> AutopilotEnabled;

        private Action<bool> AscentAP;
        private Func<bool> AscentAPState;

        private Action<bool> LandingAP;
        private Func<bool> LandingAPState;

        private Action<bool> DockingAP;
        private Func<bool> DockingAPState;

        private Action<bool> ForceRoll;
        private Func<bool> ForceRollState;

        private Action<bool> ExecuteNextNode;
        private Func<bool> ExecuteNextNodeState;

        private bool mjAvailable, smartassAvailable;

        private string GetActiveMode()
        {
            if (smartassAvailable)
            {
                if (AutopilotEnabled())
                {
                    return JSIMechJeb.TargetTexts[(int)JSIMechJeb.Target.AUTO].Replace('\n', ' ');
                }
                else
                {
                    int target = GetSmartassMode();
                    return JSIMechJeb.ModeTexts[(int)JSIMechJeb.Target2Mode[target]] + " " + JSIMechJeb.TargetTexts[target].Replace('\n', ' ');
                }
            }
            else
            {
                return JSIMechJeb.ModeTexts[(int)JSIMechJeb.Target.OFF];
            }
        }

        public string ShowMenu(int width, int height)
        {
            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(pageTitle))
            {
                result.AppendLine(pageTitle);
                height--;
            }

            if (smartassAvailable)
            {
                switch (currentMenu)
                {
                    case MJMenu.RootMenu:
                        UpdateRootMenu();
                        break;
                    case MJMenu.OrbitMenu:
                        UpdateOrbitalMenu();
                        break;
                    case MJMenu.SurfaceMenu:
                        UpdateSurfaceMenu();
                        break;
                    case MJMenu.TargetMenu:
                        UpdateTargetMenu();
                        break;
                    case MJMenu.CircularizeMenu:
                        UpdateCircularizeMenu();
                        break;
                }

                result.Append(activeMenu.ShowMenu(width, height));
            }
            else
            {
                if (mjAvailable == false)
                {
                    result.AppendLine("MechJeb Autopilot not found.");
                }
                else
                {
                    result.AppendLine("Attitude control unavailable.");
                }
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
            if (buttonID == buttonUp)
            {
                activeMenu.PreviousItem();
            }
            else if (buttonID == buttonDown)
            {
                activeMenu.NextItem();
            }
            else if (buttonID == buttonEnter)
            {
                activeMenu.SelectItem();
            }
            else if (buttonID == buttonEsc)
            {
                activeMenu = topMenu;
                currentMenu = MJMenu.RootMenu;
            }
            else if (buttonID == buttonHome)
            {
                if (currentMenu == MJMenu.RootMenu && activeMenu.currentSelection == 5 && smartassAvailable)
                {
                    // If Force Roll is highlighted, the Home key will increment the
                    // roll value.
                    double currentRoll = GetForceRollAngle() + forceRollStep;
                    if (currentRoll > 180.0)
                    {
                        currentRoll -= 360.0;
                    }
                    else if (currentRoll < -180.0)
                    {
                        currentRoll += 360.0;
                    }
                    SetForceRoll(true, currentRoll);
                    forceRollMenuItem.isSelected = true;
                    //activeSmartass.rol = currentRoll;
                    //if (forceRollMenuItem.isSelected)
                    //{
                    //    activeSmartass.Engage();
                    //}
                }
            }
        }

        public void Start()
        {

            // I guess I shouldn't have expected Squad to actually do something nice for a modder like that.
            // In 0.23, loading in non-alphabetical order is still broken.

            // But now we have KSPAssembly and KSPAssemblyDependency, which actually sidestep the issue, and finally
            // Mu told someone about it and now I can avoid this path hardlinking.
            // Actually, better yet. Let it check for the new canonical location instead. Because fuck installation problems.
            if (!JSI.InstallationPathWarning.Warn())
            {
                return;
            }

            if (!string.IsNullOrEmpty(itemColor))
            {
                itemColorValue = ConfigNode.ParseColor32(itemColor);
            }
            if (!string.IsNullOrEmpty(selectedColor))
            {
                selectedColorValue = ConfigNode.ParseColor32(selectedColor);
            }
            if (!string.IsNullOrEmpty(unavailableColor))
            {
                unavailableColorValue = ConfigNode.ParseColor32(unavailableColor);
            }

            topMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
            topMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
            topMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);

            RasterPropMonitorComputer comp = RasterPropMonitorComputer.Instantiate(internalProp);
            if (comp == null)
            {
                throw new NotImplementedException("comp");
            }
            Func<bool> isMjAvailable = (Func<bool>)comp.GetMethod("JSIMechJeb:GetMechJebAvailable", internalProp, typeof(Func<bool>));
            if (isMjAvailable == null)
            {
                throw new NotImplementedException("isMjAvailable");
            }
            GetSmartassMode = (Func<int>)comp.GetMethod("JSIMechJeb:GetSmartassMode", internalProp, typeof(Func<int>));
            SetSmartassMode = (Action<JSIMechJeb.Target>)comp.GetMethod("JSIMechJeb:SetSmartassMode", internalProp, typeof(Action<JSIMechJeb.Target>));
            SetForceRoll = (Action<bool, double>)comp.GetMethod("JSIMechJeb:ForceRoll", internalProp, typeof(Action<bool, double>));
            GetModuleExists = (Func<string, bool>)comp.GetMethod("JSIMechJeb:GetModuleExists", internalProp, typeof(Func<string, bool>));
            CircularizeAt = (Action<double>)comp.GetMethod("JSIMechJeb:CircularizeAt", internalProp, typeof(Action<double>));
            PositionTargetExists = (Func<bool>)comp.GetMethod("JSIMechJeb:PositionTargetExists", internalProp, typeof(Func<bool>));
            AutopilotEnabled = (Func<bool>)comp.GetMethod("JSIMechJeb:AutopilotEnabled", internalProp, typeof(Func<bool>));
            GetForceRollAngle = (Func<double>)comp.GetMethod("JSIMechJeb:GetForceRollAngle", internalProp, typeof(Func<double>));

            AscentAP = (Action<bool>)comp.GetMethod("JSIMechJeb:ButtonAscentGuidance", internalProp, typeof(Action<bool>));
            AscentAPState = (Func<bool>)comp.GetMethod("JSIMechJeb:ButtonAscentGuidanceState", internalProp, typeof(Func<bool>));

            LandingAP = (Action<bool>)comp.GetMethod("JSIMechJeb:ButtonLandingGuidance", internalProp, typeof(Action<bool>));
            LandingAPState = (Func<bool>)comp.GetMethod("JSIMechJeb:ButtonLandingGuidanceState", internalProp, typeof(Func<bool>));

            DockingAP = (Action<bool>)comp.GetMethod("JSIMechJeb:ButtonDockingGuidance", internalProp, typeof(Action<bool>));
            DockingAPState = (Func<bool>)comp.GetMethod("JSIMechJeb:ButtonDockingGuidanceState", internalProp, typeof(Func<bool>));

            ForceRoll = (Action<bool>)comp.GetMethod("JSIMechJeb:ButtonForceRoll", internalProp, typeof(Action<bool>));
            ForceRollState = (Func<bool>)comp.GetMethod("JSIMechJeb:ButtonForceRollState", internalProp, typeof(Func<bool>));

            ExecuteNextNode = (Action<bool>)comp.GetMethod("JSIMechJeb:ButtonNodeExecute", internalProp, typeof(Action<bool>));
            ExecuteNextNodeState = (Func<bool>)comp.GetMethod("JSIMechJeb:ButtonNodeExecuteState", internalProp, typeof(Func<bool>));

            // If MechJeb is installed, but not found on the craft, menu options can't be populated correctly.
            if (isMjAvailable())
            {
                mjAvailable = true;
                smartassAvailable = GetModuleExists("MechJebModuleSmartASS");

                topMenu.Add(new TextMenu.Item(JSIMechJeb.TargetTexts[(int)JSIMechJeb.Target.OFF], SmartASS_Off));
                topMenu.Add(new TextMenu.Item(JSIMechJeb.TargetTexts[(int)JSIMechJeb.Target.KILLROT].Replace('\n', ' '), SmartASS_KillRot));
                nodeMenuItem = new TextMenu.Item(JSIMechJeb.TargetTexts[(int)JSIMechJeb.Target.NODE], SmartASS_Node);
                topMenu.Add(nodeMenuItem);
                topMenu.Add(new TextMenu.Item(JSIMechJeb.ModeTexts[(int)JSIMechJeb.Mode.ORBITAL], OrbitalMenu));
                topMenu.Add(new TextMenu.Item(JSIMechJeb.ModeTexts[(int)JSIMechJeb.Mode.SURFACE], SurfaceMenu));
                targetMenuItem = new TextMenu.Item(JSIMechJeb.ModeTexts[(int)JSIMechJeb.Mode.TARGET], TargetMenu);
                topMenu.Add(targetMenuItem);
                forceRollMenuItem = new TextMenu.Item(String.Format("Force Roll: {0:f0}", GetForceRollAngle()), ToggleForceRoll);
                topMenu.Add(forceRollMenuItem);
                topMenu.Add(new TextMenu.Item("Execute Next Node", ExecuteNode, (int)MJMenu.ExecuteNodeMenu));
                topMenu.Add(new TextMenu.Item("Ascent Guidance", AscentGuidance, (int)MJMenu.AscentGuidanceMenu));
                topMenu.Add(new TextMenu.Item("Land Somewhere", LandingGuidance, (int)MJMenu.LandingGuidanceMenu));
                topMenu.Add(new TextMenu.Item("Docking Guidance", DockingGuidance, (int)MJMenu.DockingGuidanceMenu));
                //topMenu.Add(new TextMenu.Item("Hold Alt & Heading", SpaceplaneGuidance, (int)MJMenu.SpacePlaneMenu));
                topMenu.Add(new TextMenu.Item("Circularize", CircularizeMenu, (int)MJMenu.CircularizeMenu));
            }
            else
            {
                mjAvailable = false;
                smartassAvailable = false;
            }
            activeMenu = topMenu;
        }

        //--- ROOT MENU methods
        private void UpdateRootMenu()
        {
            activeMenu.menuTitle = "== Root Menu: " + GetActiveMode();

            targetMenuItem.isDisabled = (FlightGlobals.fetch.VesselTarget == null);
            if (vessel.patchedConicSolver != null)
            {
                nodeMenuItem.isDisabled = (vessel.patchedConicSolver.maneuverNodes.Count == 0);
            }
            else
            {
                nodeMenuItem.isDisabled = true;
            }
            // Analysis disable once RedundantCast
            forceRollMenuItem.labelText = String.Format("Force Roll: {0:+0;-0;0}", GetForceRollAngle());

            if (smartassAvailable)
            {
                int target = GetSmartassMode();
                if (target == (int)JSIMechJeb.Target.OFF)
                {
                    activeMenu.SetSelected(0, true);
                }
                else if (target == (int)JSIMechJeb.Target.KILLROT)
                {
                    activeMenu.SetSelected(1, true);
                }
                else if (target == (int)JSIMechJeb.Target.NODE)
                {
                    activeMenu.SetSelected(2, true);
                }
                else if ((int)JSIMechJeb.Target2Mode[target] == (int)JSIMechJeb.Mode.ORBITAL)
                {
                    activeMenu.SetSelected(3, true);
                }
                else if ((int)JSIMechJeb.Target2Mode[target] == (int)JSIMechJeb.Mode.TARGET)
                {
                    activeMenu.SetSelected(4, true);
                }
                // 5 is Force Roll.  State is controlled below, and is independent
                // of the rest of these
                // 6 is Execute Next Node.
                else if ((int)JSIMechJeb.Target2Mode[target] == (int)JSIMechJeb.Mode.SURFACE)
                {
                    activeMenu.SetSelected(7, true);
                }
                else if ((int)JSIMechJeb.Target2Mode[target] == (int)JSIMechJeb.Mode.ADVANCED)
                {
                    activeMenu.SetSelected(8, true);
                }

                forceRollMenuItem.isSelected = ForceRollState();
            }
            else
            {
                activeMenu.SetSelected(0, true);
                forceRollMenuItem.isSelected = false;
            }

            TextMenu.Item item = activeMenu.Find(x => x.id == (int)MJMenu.ExecuteNodeMenu);
            if (item != null)
            {
                if (GetModuleExists("MechJebModuleManeuverPlanner"))
                {
                    item.isSelected = false;
                    item.labelText = (ExecuteNextNodeState()) ? "Abort Node Execution" : "Execute Next Node";
                    if (vessel.patchedConicSolver != null)
                    {
                        item.isDisabled = (vessel.patchedConicSolver.maneuverNodes.Count == 0);
                    }
                    else
                    {
                        item.isDisabled = true;
                    }
                }
                else
                {
                    item.isSelected = false;
                    item.labelText = "Execute Next Node";
                    item.isDisabled = true;
                }
            }

            item = activeMenu.Find(x => x.id == (int)MJMenu.AscentGuidanceMenu);
            if (item != null)
            {
                if (!GetModuleExists("MechJebModuleAscentAutopilot"))
                {
                    item.isSelected = false;
                    item.isDisabled = true;
                }
                else
                {
                    item.isSelected = AscentAPState();
                    item.isDisabled = false;
                }
            }

            item = activeMenu.Find(x => x.id == (int)MJMenu.LandingGuidanceMenu);
            if (item != null)
            {
                if (!GetModuleExists("MechJebModuleLandingAutopilot"))
                {
                    item.isSelected = false;
                    item.isDisabled = true;
                }
                else
                {
                    item.labelText = (PositionTargetExists()) ? "Land at Target" : "Land Somewhere";
                    item.isSelected = LandingAPState();
                    item.isDisabled = false;
                }
            }

            item = activeMenu.Find(x => x.id == (int)MJMenu.DockingGuidanceMenu);
            if (item != null)
            {
                if (!GetModuleExists("MechJebModuleDockingAutopilot"))
                {
                    item.isSelected = false;
                    item.isDisabled = true;
                }
                else
                {
                    item.isSelected = DockingAPState();
                    item.isDisabled = !(FlightGlobals.fetch.VesselTarget is ModuleDockingNode);
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
            if (item != null)
            {
                item.isDisabled = vessel.LandedOrSplashed;
            }
        }

        private void SmartASS_Off(int index, TextMenu.Item tmi)
        {
            SetSmartassMode(JSIMechJeb.Target.OFF);
        }

        private void SmartASS_KillRot(int index, TextMenu.Item tmi)
        {
            SetSmartassMode(JSIMechJeb.Target.KILLROT);
        }

        private void SmartASS_Node(int index, TextMenu.Item tmi)
        {
            SetSmartassMode(JSIMechJeb.Target.NODE);
        }

        private void OrbitalMenu(int index, TextMenu.Item tmi)
        {
            currentMenu = MJMenu.OrbitMenu;

            activeMenu = new TextMenu();
            activeMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
            activeMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
            activeMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);

            foreach (JSIMechJeb.Target target in orbitalTargets)
            {
                activeMenu.Add(new TextMenu.Item(JSIMechJeb.TargetTexts[(int)target].Replace('\n', ' '), SelectTarget));
            }
        }

        private void SurfaceMenu(int index, TextMenu.Item tmi)
        {
            currentMenu = MJMenu.SurfaceMenu;

            activeMenu = new TextMenu();
            activeMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
            activeMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
            activeMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);

            foreach (JSIMechJeb.Target target in surfaceTargets)
            {
                activeMenu.Add(new TextMenu.Item(JSIMechJeb.TargetTexts[(int)target].Replace('\n', ' '), SelectTarget));
            }
        }

        private void TargetMenu(int index, TextMenu.Item tmi)
        {
            currentMenu = MJMenu.TargetMenu;

            activeMenu = new TextMenu();
            activeMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
            activeMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
            activeMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);

            foreach (JSIMechJeb.Target target in targetTargets)
            {
                activeMenu.Add(new TextMenu.Item(JSIMechJeb.TargetTexts[(int)target].Replace('\n', ' '), SelectTarget));
            }
        }

        private void ExecuteNode(int index, TextMenu.Item tmi)
        {
            bool state = ExecuteNextNodeState();
            ExecuteNextNode(!state);
        }

        private void ToggleForceRoll(int index, TextMenu.Item tmi)
        {
            bool newForceRollState = !ForceRollState();
            forceRollMenuItem.isSelected = newForceRollState;
            ForceRoll(newForceRollState);
        }

        private void AscentGuidance(int index, TextMenu.Item tmi)
        {
            bool state = AscentAPState();
            AscentAP(!state);
        }

        private void LandingGuidance(int index, TextMenu.Item tmi)
        {
            bool state = LandingAPState();
            LandingAP(!state);
        }

        private void DockingGuidance(int index, TextMenu.Item tmi)
        {
            bool state = DockingAPState();
            DockingAP(!state);
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

            activeMenu.Add(new TextMenu.Item("At Next Ap", DoCircularize, (int)JSIMechJeb.TimeReference.APOAPSIS));
            activeMenu.Add(new TextMenu.Item("At Next Pe", DoCircularize, (int)JSIMechJeb.TimeReference.PERIAPSIS));
            activeMenu.Add(new TextMenu.Item("In 15s", DoCircularize, (int)JSIMechJeb.TimeReference.X_FROM_NOW));
        }

        //--- Orbital Menu
        private void UpdateOrbitalMenu()
        {
            activeMenu.menuTitle = "== " + JSIMechJeb.ModeTexts[(int)JSIMechJeb.Mode.ORBITAL] + " Menu: " + GetActiveMode();

            int target = GetSmartassMode();
            int idx = -1;
            for (int i = 0; i < orbitalTargets.Length; ++i)
            {
                if ((int)orbitalTargets[i] == target)
                {
                    idx = i;
                    break;
                }
            }

            if (idx != -1)
            {
                activeMenu.SetSelected(idx, true);
            }
        }
        //--- Surface Menu
        private void UpdateSurfaceMenu()
        {
            activeMenu.menuTitle = "== " + JSIMechJeb.ModeTexts[(int)JSIMechJeb.Mode.SURFACE] + " Menu: " + GetActiveMode();

            int target = GetSmartassMode();
            int idx = -1;
            for (int i = 0; i < surfaceTargets.Length; ++i)
            {
                if ((int)surfaceTargets[i] == target)
                {
                    idx = i;
                    break;
                }
            }

            if (idx != -1)
            {
                activeMenu.SetSelected(idx, true);
            }
        }
        //--- Target Menu
        private void UpdateTargetMenu()
        {
            activeMenu.menuTitle = "== " + JSIMechJeb.ModeTexts[(int)JSIMechJeb.Mode.TARGET] + " Menu: " + GetActiveMode();

            int target = GetSmartassMode();
            int idx = -1;
            for (int i = 0; i < targetTargets.Length; ++i)
            {
                if ((int)targetTargets[i] == target)
                {
                    idx = i;
                    break;
                }
            }

            if (idx != -1)
            {
                activeMenu.SetSelected(idx, true);
            }
        }
        //--- Circularize Menu
        private void UpdateCircularizeMenu()
        {
            // If the menu works, the only thing we won't allow is
            // "circularize at Ap" when we're hyperbolic.
            for (int i = 0; i < activeMenu.Count; ++i)
            {
                if (activeMenu[i].id == (int)JSIMechJeb.TimeReference.APOAPSIS)
                {
                    activeMenu[i].isDisabled = (vessel.orbit.eccentricity >= 1.0);
                    break;
                }
            }
        }

        private void DoCircularize(int index, TextMenu.Item tmi)
        {
            double UT = 0.0;

            switch (tmi.id)
            {
                case (int)JSIMechJeb.TimeReference.APOAPSIS:
                    UT = vessel.orbit.NextApoapsisTime(Planetarium.GetUniversalTime());
                    break;
                case (int)JSIMechJeb.TimeReference.PERIAPSIS:
                    UT = vessel.orbit.NextPeriapsisTime(Planetarium.GetUniversalTime());
                    break;
                case (int)JSIMechJeb.TimeReference.X_FROM_NOW:
                    UT = Planetarium.GetUniversalTime() + 15.0;
                    break;
            }

            if (UT > Planetarium.GetUniversalTime())
            {
                CircularizeAt(UT);
            }
        }

        private void SelectTarget(int index, TextMenu.Item tmi)
        {
            JSIMechJeb.Target[] activeTargets = null;
            switch (currentMenu)
            {
                case MJMenu.OrbitMenu:
                    activeTargets = orbitalTargets;
                    break;
                case MJMenu.SurfaceMenu:
                    activeTargets = surfaceTargets;
                    break;
                case MJMenu.TargetMenu:
                    activeTargets = targetTargets;
                    break;
            }

            if (activeTargets != null)
            {
                SetSmartassMode(activeTargets[index]);
            }
        }
    }
}
