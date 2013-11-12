using System;
using UnityEngine;

namespace JSI
{
	public class PersistenceAccessor
	{
		private JSIInternalPersistence persistenceStorage;

		public PersistenceAccessor(Part thatPart)
		{
			for (int i=0; i<thatPart.Modules.Count; i++)
				if (thatPart.Modules[i].ClassName == typeof(JSIInternalPersistence).Name)
					persistenceStorage = thatPart.Modules[i] as JSIInternalPersistence;
		}

		private static void LogWarning(Exception e)
		{
			Debug.Log(String.Format("Warning: RasterPropMonitor components want JSIInternalPersistence to be loaded by the pod they're in. {0}", e.Message));
		}

		public int? GetVar(string persistentVarName)
		{
			try {
				return persistenceStorage.GetVar(persistentVarName);
			} catch (NullReferenceException e) {
				LogWarning(e);
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
				LogWarning(e);
			}
		}
		public void SetVar(string persistentVarName, bool varvalue){
			SetVar(persistentVarName, varvalue ? 1 : 0);
		}
	}
}

