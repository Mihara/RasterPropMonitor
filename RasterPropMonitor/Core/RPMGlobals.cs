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
using System;
using System.Collections.Generic;

namespace JSI
{
    /// <summary>
    /// The RPMGlobals class contains various statically-defined global values.
    /// These are generally loaded in by the RPMShaderLoader class in the main
    /// menu.
    /// </summary>
    internal class RPMGlobals
    {
        /// <summary>
        /// Should JUtil.LogMessage write to the log?
        /// </summary>
        internal static bool debugLoggingEnabled = false;

        /// <summary>
        /// Should we show the variable call count profiling info?
        /// </summary>
        internal static bool debugShowVariableCallCount = false;

        internal static List<string> debugShowOnly = new List<string>();

        internal static Dictionary<string, IComplexVariable> customVariables = new Dictionary<string, IComplexVariable>();
        internal static List<string> knownLoadedAssemblies = new List<string>();
        internal static SortedDictionary<string, string> systemNamedResources = new SortedDictionary<string, string>();
        internal static List<RasterPropMonitorComputer.TriggeredEventTemplate> triggeredEvents = new List<RasterPropMonitorComputer.TriggeredEventTemplate>();
    }
}
