using System;
using System.Collections.Generic;
using System.Text;

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
		private int refreshMenuCountdown;
		private int currentMenu;
		private int currentMenuItem;
		private readonly List<string> rootMenu = new List<string>{ "Celestials", "Vessels" };

		public string ShowMenu(int width, int height)
		{
			if (currentMenu == 0) {
				return FormatMenu(width, height, RootMenu());
			}
			return string.Empty;
		}

		public void ButtonProcessor(int buttonID)
		{
			if (buttonID == buttonUp) {
				currentMenuItem--;
				if (currentMenuItem <= 0)
					currentMenuItem = 0;
			}
			if (buttonID == buttonDown) {
				currentMenuItem++;
				switch (currentMenu) {
					case 0:
						if (currentMenuItem >= rootMenu.Count)
							currentMenuItem = rootMenu.Count - 1;
						break;
				}
			}
			if (buttonID == buttonEnter) {
				if (currentMenu == 0) {
					if (currentMenuItem == 0)
						currentMenu = 1;
					else
						currentMenu = 2;
				}
				currentMenuItem = 0;
			}
			if (buttonID == buttonEsc) {
				currentMenu = 0;
			}
		}

		// Analysis disable once UnusedParameter
		private string FormatMenu(int width, int height, List<string> items)
		{
			List<string> menu = new List<string>();
			for (int i = 0; i < items.Count; i++) {
				menu.Add(((i == currentMenuItem) ? "> " : "  ") + items[i]);
			}
			if (!string.IsNullOrEmpty(pageTitle))
				height--;

			if (menu.Count > height) {
				menu = menu.GetRange(currentMenuItem, height);
			}

			var result = new StringBuilder();
			if (!string.IsNullOrEmpty(pageTitle))
				result.AppendLine(pageTitle);
			foreach (string item in menu)
				result.AppendLine(item);
			return result.ToString();
		}

		private List<string> RootMenu()
		{
			return rootMenu;
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

		}

		public void Start()
		{
			if (!string.IsNullOrEmpty(pageTitle))
				pageTitle = pageTitle.Replace("<=", "{").Replace("=>", "}");
		}
	}
}

