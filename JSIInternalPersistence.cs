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
			var variables = ParseData();
			try {
				variables.Add(varname, value);
			} catch (ArgumentException) {
				variables[varname] = value;
			}
			data = UnparseData(variables);
		}

		private string UnparseData(Dictionary<string,int> variables)
		{
			List<string> tokens = new List<string>();
			foreach (KeyValuePair<string,int> item in variables) {
				tokens.Add(item.Key + "$" + item.Value.ToString());
			}
			return String.Join("|", tokens.ToArray());
		}

		private Dictionary<string,int> ParseData()
		{
			var variables = new Dictionary<string,int>();
			if (!string.IsNullOrEmpty(data))
				foreach (string varstring in data.Split ('|')) {
					string[] tokens = varstring.Split('$');
					int value;
					int.TryParse(tokens[1], out value);
					variables.Add(tokens[0], value);
				}

			return variables;
			
		}

		public int GetVar(string varname)
		{
			var variables = ParseData();
			if (variables.ContainsKey(varname))
				return variables[varname];
			else
				return int.MaxValue;
		}
	}
}

