using System;
using System.Collections.Generic;

namespace JSI
{

    // Just a helper class to encapsulate this mess.
    public class PersistenceAccessor
    {
        private readonly RasterPropMonitorComputer comp;
        private Dictionary<string, int> persistentVars = new Dictionary<string, int>();

        public PersistenceAccessor(RasterPropMonitorComputer comp)
        {
            this.comp = comp;
            ParseData();
        }

        private void ParseData()
        {
            persistentVars.Clear();
            if (!string.IsNullOrEmpty(comp.data))
            {
                string[] varstring = comp.data.Split('|');
                for (int i = 0; i < varstring.Length; ++i)
                {
                    string[] tokens = varstring[i].Split('$');
                    int value;
                    if (tokens.Length == 2 && int.TryParse(tokens[1], out value))
                    {
                        persistentVars.Add(tokens[0], value);
                    }
                }
            }
            JUtil.LogMessage(this, "Parsed persistence string 'data' into {0} entries", persistentVars.Count);
        }

        private void StoreData()
        {
            var tokens = new List<string>();
            foreach (KeyValuePair<string, int> item in persistentVars)
            {
                tokens.Add(item.Key + "$" + item.Value);
            }

            comp.data = string.Join("|", tokens.ToArray());
        }

        public bool GetBool(string persistentVarName, bool defaultValue)
        {
            if (persistentVars.ContainsKey(persistentVarName))
            {
                int value = GetVar(persistentVarName);
                return (value > 0);
            }
            else
            {
                return defaultValue;
            }
        }

        public int GetVar(string persistentVarName, int defaultValue)
        {
            if (persistentVars.ContainsKey(persistentVarName))
            {
                return persistentVars[persistentVarName];
            }
            else
            {
                return defaultValue;
            }
        }

        public int GetVar(string persistentVarName)
        {
            try
            {
                return persistentVars[persistentVarName];
            }
            catch
            {
                JUtil.LogErrorMessage(this, "Someone called GetVar({0}) without making sure the value existed", persistentVarName);
            }

            return int.MinValue;
        }

        public bool HasVar(string persistentVarName)
        {
            return persistentVars.ContainsKey(persistentVarName);
        }

        public void SetVar(string persistentVarName, int varvalue)
        {
            if (persistentVars.ContainsKey(persistentVarName))
            {
                persistentVars[persistentVarName] = varvalue;
            }
            else
            {
                persistentVars.Add(persistentVarName, varvalue);
            }

            StoreData();
        }

        public void SetVar(string persistentVarName, bool varvalue)
        {
            SetVar(persistentVarName, varvalue ? 1 : 0);
        }
    }
}

