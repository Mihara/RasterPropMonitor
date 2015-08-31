using System;

namespace JSI
{
    public static class StringProcessor
    {
        private static readonly SIFormatProvider fp = new SIFormatProvider();

        public static string ProcessString(string input, RPMVesselComputer comp, int propID = -1)
        {
            try
            {
                if (input.IndexOf(JUtil.VariableListSeparator[0], StringComparison.Ordinal) >= 0)
                {
                    string[] tokens = input.Split(JUtil.VariableListSeparator, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length != 2)
                    {
                        return "FORMAT ERROR";
                    }
                    else
                    {
                        string[] vars = tokens[1].Split(JUtil.VariableSeparator, StringSplitOptions.RemoveEmptyEntries);

                        var variables = new object[vars.Length];
                        for (int i = 0; i < vars.Length; i++)
                        {
                            variables[i] = comp.ProcessVariable(vars[i]);
                        }
                        string output = string.Format(fp, tokens[0], variables);
                        return output.TrimEnd();
                    }
                }
            }
            catch (Exception e)
            {
                JUtil.LogMessage(comp, "Bad format on string {0}", input);
                throw e;
            }
            return input.TrimEnd();
        }
    }
}
