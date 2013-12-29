using System;

namespace JSI
{

	// Just a helper class to encapsulate this mess.
	public class PersistenceAccessor
	{
		private readonly RasterPropMonitorComputer persistenceStorage;
		private const string errorMessage = "Warning: RasterPropMonitor components want RasterPropMonitorComputer PartModule to be loaded by the pod they're in. {0}";

		public PersistenceAccessor(Part thatPart)
		{
			for (int i = 0; i < thatPart.Modules.Count; i++)
				if (thatPart.Modules[i].ClassName == typeof(RasterPropMonitorComputer).Name)
					persistenceStorage = thatPart.Modules[i] as RasterPropMonitorComputer;
		}

		public int? GetVar(string persistentVarName)
		{
			try {
				return persistenceStorage.GetVar(persistentVarName);
			} catch (NullReferenceException e) {
				JUtil.LogMessage(this,errorMessage, e.Message);
			}
			return null;
		}

		public bool? GetBool(string persistentVarName)
		{

			int? value;
			if ((value = GetVar(persistentVarName)) > 0)
				return true;
			if (value == 0)
				return false;
			return null;
		}

		public void SetVar(string persistentVarName, int varvalue)
		{
			try {
				persistenceStorage.SetVar(persistentVarName, varvalue);
			} catch (NullReferenceException e) {
				JUtil.LogMessage(this,errorMessage, e.Message);
			}
		}

		public void SetVar(string persistentVarName, bool varvalue)
		{
			SetVar(persistentVarName, varvalue ? 1 : 0);
		}
	}
}

