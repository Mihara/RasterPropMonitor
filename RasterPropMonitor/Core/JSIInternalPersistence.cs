using System;
using System.Collections.Generic;
using UnityEngine;

namespace JSI
{
	public class JSIInternalPersistence:PartModule
	{
		[KSPField(isPersistant = true)]
		public string data = "";
		// Yes, it's a really braindead way of doing it, but I ran out of elegant ones,
		// because nothing appears to work as documented -- IF it's documented.
		// This one is sure to work and isn't THAT much of a performance drain, really.
		// Pull requests welcome
		public void SetVar(string varname, int value)
		{
			var variables = ParseData(data);
			try {
				variables.Add(varname, value);
			} catch (ArgumentException) {
				variables[varname] = value;
			}
			data = UnparseData(variables);
		}

		public int? GetVar(string varname)
		{
			var variables = ParseData(data);
			if (variables.ContainsKey(varname))
				return variables[varname];
			return null;
		}

		private static string UnparseData(Dictionary<string,int> variables)
		{
			var tokens = new List<string>();
			foreach (KeyValuePair<string,int> item in variables) {
				tokens.Add(item.Key + "$" + item.Value);
			}
			return String.Join("|", tokens.ToArray());
		}

		private static Dictionary<string,int> ParseData(string dataString)
		{
			var variables = new Dictionary<string,int>();
			if (!string.IsNullOrEmpty(dataString))
				foreach (string varstring in dataString.Split ('|')) {
					string[] tokens = varstring.Split('$');
					int value;
					int.TryParse(tokens[1], out value);
					variables.Add(tokens[0], value);
				}

			return variables;
			
		}
	}
	// Just a helper class to encapsulate this mess.
	public class PersistenceAccessor
	{
		private readonly JSIInternalPersistence persistenceStorage;
		private const string errorMessage = "Warning: RasterPropMonitor components want JSIInternalPersistence to be loaded by the pod they're in. {0}";

		public PersistenceAccessor(Part thatPart)
		{
			for (int i = 0; i < thatPart.Modules.Count; i++)
				if (thatPart.Modules[i].ClassName == typeof(JSIInternalPersistence).Name)
					persistenceStorage = thatPart.Modules[i] as JSIInternalPersistence;
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

