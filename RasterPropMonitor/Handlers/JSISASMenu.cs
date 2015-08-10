using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace JSI
{
    class JSISASMenu : InternalModule
    {
        [KSPField]
        public string pageTitle = string.Empty;
        [KSPField]
        public string menuTitleFormatString = "== SAS Menu == {0}";
        [KSPField]
        public int buttonUp = 0;
        [KSPField]
        public int buttonDown = 1;
        [KSPField]
        public int buttonEnter = 2;
        [KSPField]
        public string itemColor = string.Empty;
        private Color itemColorValue = Color.white;
        [KSPField]
        public string selectedColor = string.Empty;
        private Color selectedColorValue = Color.green;
        [KSPField]
        public string unavailableColor = string.Empty;
        private Color unavailableColorValue = Color.gray;

        private readonly TextMenu topMenu = new TextMenu();
        private static readonly SIFormatProvider fp = new SIFormatProvider();
        private static readonly int sasGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.SAS);
        private int activeMode;

        private static readonly VesselAutopilot.AutopilotMode[] modes = new VesselAutopilot.AutopilotMode[]
        {
            VesselAutopilot.AutopilotMode.StabilityAssist, // Not actually used
            VesselAutopilot.AutopilotMode.StabilityAssist,
            VesselAutopilot.AutopilotMode.Maneuver,
            VesselAutopilot.AutopilotMode.Prograde,
            VesselAutopilot.AutopilotMode.Retrograde,
            VesselAutopilot.AutopilotMode.RadialIn,
            VesselAutopilot.AutopilotMode.RadialOut,
            VesselAutopilot.AutopilotMode.Normal,
            VesselAutopilot.AutopilotMode.Antinormal,
            VesselAutopilot.AutopilotMode.Target,
            VesselAutopilot.AutopilotMode.AntiTarget,
        };

        private static readonly string[] modeStrings = new string[]
        {
            "SAS Off",
            "Stability Assist",
            "Maneuver Node",
            "Prograde",
            "Retrograde",
            "Radial In",
            "Radial Out",
            "Normal",
            "Antinormal",
            "Target",
            "Antitarget",
        };

        public void Start()
        {
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

            topMenu.menuTitle = "== SAS Menu ==";
            topMenu.labelColor = JUtil.ColorToColorTag(itemColorValue);
            topMenu.selectedColor = JUtil.ColorToColorTag(selectedColorValue);
            topMenu.disabledColor = JUtil.ColorToColorTag(unavailableColorValue);

            topMenu.Add(new TextMenu.Item("SAS Off", SAS_Toggle, 0));
            topMenu.Add(new TextMenu.Item("Stability Assist", SAS_Mode, 1));
            topMenu.Add(new TextMenu.Item("Maneuver Node", SAS_Mode, 2));
            topMenu.Add(new TextMenu.Item("Prograde", SAS_Mode, 3));
            topMenu.Add(new TextMenu.Item("Retrograde", SAS_Mode, 4));
            topMenu.Add(new TextMenu.Item("Radial In", SAS_Mode, 5));
            topMenu.Add(new TextMenu.Item("Radial Out", SAS_Mode, 6));
            topMenu.Add(new TextMenu.Item("Normal", SAS_Mode, 7));
            topMenu.Add(new TextMenu.Item("Antinormal", SAS_Mode, 8));
            topMenu.Add(new TextMenu.Item("Target", SAS_Mode, 9));
            topMenu.Add(new TextMenu.Item("Antitarget", SAS_Mode, 10));
        }

        private void SAS_Mode(int arg1, TextMenu.Item arg2)
        {
            vessel.Autopilot.SetMode(modes[arg1]);
            // find the UI object on screen
            RUIToggleButton[] SASbtns = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>().modeButtons;
            // set our mode, note it takes the mode as an int, generally top to bottom, left to right, as seen on the screen. Maneuver node being the exception, it is 9
            SASbtns.ElementAt<RUIToggleButton>((int)modes[arg1]).SetTrue(true, true);
        }

        private void SAS_Toggle(int arg1, TextMenu.Item arg2)
        {
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
        }

        public string ShowMenu(int width, int height)
        {
            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(pageTitle))
            {
                result.AppendLine(pageTitle);
                height--;
            }

            topMenu[0].isSelected = vessel.ActionGroups.groups[sasGroupNumber];
            topMenu[0].labelText = (vessel.ActionGroups.groups[sasGroupNumber]) ? "Disable SAS" : "Enable SAS";

            for (int i = 1; i < topMenu.Count; ++i)
            {
                if (vessel.Autopilot.CanSetMode(modes[i]))
                {
                    topMenu[i].isDisabled = false;
                    topMenu[i].isSelected = (vessel.Autopilot.Mode == modes[i]);
                    if (topMenu[i].isSelected)
                    {
                        activeMode = i;
                    }
                }
                else
                {
                    topMenu[i].isDisabled = true;
                    topMenu[i].isSelected = false;
                }
            }

            if (vessel.ActionGroups.groups[sasGroupNumber] == false)
            {
                activeMode = 0;
            }

            topMenu.menuTitle = string.Format(fp, menuTitleFormatString, modeStrings[activeMode]);

            result.Append(topMenu.ShowMenu(width, height));

            return result.ToString();
        }

        // Analysis disable once UnusedParameter
        public void PageActive(bool active, int pageNumber)
        {
        }

        public void ClickProcessor(int buttonID)
        {
            if (buttonID == buttonUp)
            {
                topMenu.PreviousItem();
            }
            else if (buttonID == buttonDown)
            {
                topMenu.NextItem();
            }
            else if (buttonID == buttonEnter)
            {
                topMenu.SelectItem();
            }
        }
    }
}
