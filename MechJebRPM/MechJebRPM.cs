using MuMech;
using System;
using System.Text;

namespace MechJebRPM
{
	public class MechJebRPM: InternalModule
	{
		MechJebCore activeJeb;
		MechJebModuleSmartASS activeSmartass;
		bool pageActiveState;
		// Analysis disable UnusedParameter
		public string ShowMenu(int width, int height)
		// Analysis restore UnusedParameter
		{
			UpdateJebReferences();
			var result = new StringBuilder();
			if (activeSmartass != null) {
				result.AppendLine(activeSmartass.target.ToString());
				result.AppendLine(activeSmartass.mode.ToString());
			} else {
				result.AppendLine("Not found.");
			}
			return result.ToString();
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
			if (activeJeb != null) {
				activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
			} else {
				activeSmartass = null;
			}
		}

		public void Start()
		{
			UpdateJebReferences();
		}
	}
}

