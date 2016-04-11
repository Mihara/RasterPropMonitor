/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System.Collections.Generic;

namespace JSI
{
    public partial class RPMVesselComputer : VesselModule
    {
        private Dictionary<string, object> persistentVars = new Dictionary<string, object>();

        /// <summary>
        /// Returns the named persistent value, or the default provided if
        /// it's not set.  The persistent value is initialized to the default
        /// if the default is used.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal object GetPersistentVariable(string name, object defaultValue)
        {
            object val;
            try
            {
                val = persistentVars[name];
            }
            catch
            {
                val = defaultValue;
                persistentVars[name] = defaultValue;
            }

            return val;
        }

        /// <summary>
        /// Return the persistent variable, pre-treated as a boolean.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal bool GetPersistentVariable(string name, bool defaultValue)
        {
            object val;
            try
            {
                val = persistentVars[name];
            }
            catch
            {
                val = defaultValue;
                persistentVars[name] = defaultValue;
            }

            return (val.GetType() == typeof(System.Boolean)) ? (bool)val : false;
        }


        /// <summary>
        /// Indicates whether the named persistent variable is present in the
        /// dictionary.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal bool HasPersistentVariable(string name)
        {
            return persistentVars.ContainsKey(name);
        }

        /// <summary>
        /// Set the named persistent variable to the value provided.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        internal void SetPersistentVariable(string name, object value)
        {
            try
            {
                if (name.Trim().Length == 0)
                {
                    JUtil.LogErrorMessage(this, "Trying to set an empty variable name!");
                    return;
                }
                persistentVars[name] = value;
                //JUtil.LogMessage(this, "Setting persistent var {0} to {1}", name, value);
            }
            catch
            {
                // Not needed?  Looks like the assignment will add the value.
                persistentVars.Add(name, value);
                //JUtil.LogMessage(this, "Adding persistent var {0} as {1}", name, value);
            }
        }
    }
}
