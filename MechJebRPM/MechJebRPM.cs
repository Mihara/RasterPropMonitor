using MuMech;

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
			if (activeSmartass != null) {
				return activeSmartass.mode.ToString();
			}
			return "Not found.";
		}
		// Analysis disable once UnusedParameter
		public void PageActive(bool active, int pageNumber)
		{
			pageActiveState = active;
		}

		public void Start()
		{
			activeJeb = vessel.GetMasterMechJeb();
			if (activeJeb != null) {
				activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
			}
		}
	}
}

