using System;
using System.Text;
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
		// KSPFields end here.
		private MechJebCore activeJeb;
		private MechJebModuleSmartASS activeSmartass;
		private bool pageActiveState;

		private readonly TextMenu topMenu = new TextMenu();

		// Analysis disable UnusedParameter
		public string ShowMenu(int width, int height)
		// Analysis restore UnusedParameter
		{
			UpdateJebReferences();
			if (activeSmartass == null)
				return pageTitle + (string.IsNullOrEmpty(pageTitle) ? string.Empty : Environment.NewLine) + "Attitude control unavailable.";
			if (activeJeb == null)
				return pageTitle + (string.IsNullOrEmpty(pageTitle) ? string.Empty : Environment.NewLine) + "Autopilot not installed.";

			return string.Empty;
		}
		// Analysis disable once UnusedParameter
		public void PageActive(bool active, int pageNumber)
		{
			pageActiveState = active;
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
			UpdateJebReferences();
			//topMenu.
		}
	}
}

