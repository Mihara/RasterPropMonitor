using System;

namespace JSI
{
	public static class StringProcessor
	{
		private static readonly SIFormatProvider fp = new SIFormatProvider();
		private static readonly string[] variableListSeparator = { "$&$" };
		private static readonly string[] variableSeparator = { };

		public static string ProcessString(string input, RasterPropMonitorComputer comp)
		{
			if (input.IndexOf(variableListSeparator[0], StringComparison.Ordinal) >= 0) {
				string[] tokens = input.Split(variableListSeparator, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length != 2) {
					return "FORMAT ERROR";
				} else {
					string[] vars = tokens[1].Split(variableSeparator, StringSplitOptions.RemoveEmptyEntries);

					var variables = new object[vars.Length];
					for (int i = 0; i < vars.Length; i++) {
						variables[i] = comp.ProcessVariable(vars[i]);
					}
					return String.Format(fp, tokens[0], variables).TrimEnd();
				}
			}
			return input.TrimEnd();
		}
	}
}