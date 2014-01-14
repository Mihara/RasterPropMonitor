using MuMech;
using System.Linq;

namespace MechJebRPM
{
	public class MechJebRPMVariables: PartModule
	{
		private MechJebCore activeJeb;

		public object ProcessVariable(string variable)
		{
			activeJeb = vessel.GetMasterMechJeb();
			switch (variable) {
				case "MECHJEBAVAILABLE":
					if (activeJeb != null)
						return 1;
					return -1;
				case "DELTAV":
					if (activeJeb != null) {
						MechJebModuleStageStats stats = activeJeb.GetComputerModule<MechJebModuleStageStats>();
						stats.RequestUpdate(this);
						return stats.vacStats.Sum(s => s.deltaV);
					}
					return null;
				case "DELTAVSTAGE":
					if (activeJeb != null) {
						MechJebModuleStageStats stats = activeJeb.GetComputerModule<MechJebModuleStageStats>();
						stats.RequestUpdate(this);
						return stats.vacStats[stats.vacStats.Length - 1].deltaV;
					}
					return null;
			}
			return null;
		}
	}
}

