using System;

namespace JSI
{
	public static class StringProcessor
	{
		private static readonly SIFormatProvider fp = new SIFormatProvider();

		public static string ProcessString(string input, RasterPropMonitorComputer comp, int propId)
		{
			if (input.IndexOf(JUtil.VariableListSeparator[0], StringComparison.Ordinal) >= 0) {
				string[] tokens = input.Split(JUtil.VariableListSeparator, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length != 2) {
					return "FORMAT ERROR";
				} else {
					string[] vars = tokens[1].Split(JUtil.VariableSeparator, StringSplitOptions.RemoveEmptyEntries);

					var variables = new object[vars.Length];
					for (int i = 0; i < vars.Length; i++) {
						variables[i] = comp.ProcessVariable(vars[i], propId);
					}
					string output = string.Format(fp, tokens[0], variables);
					return output.TrimEnd();
				}
			}
			return input.TrimEnd();
		}
	}
}