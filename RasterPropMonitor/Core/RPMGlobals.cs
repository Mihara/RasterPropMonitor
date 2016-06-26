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
    internal static class RPMGlobals
    {
        internal static readonly string configFileName = "GameData/JSI/RasterPropMonitor/Plugins/PluginData/rpm-config.cfg";
        internal const float KelvinToCelsius = -273.15f;
        internal const float MetersToFeet = 3.2808399f;
        internal const float MetersPerSecondToKnots = 1.94384449f;
        internal const float MetersPerSecondToFeetPerMinute = 196.850394f;
        internal const float gee = 9.81f;
        internal static readonly double upperAtmosphereLimit = Math.Log(100000.0);

        /// <summary>
        /// Should JUtil.LogMessage write to the log?
        /// </summary>
        internal static bool debugLoggingEnabled = false;

        /// <summary>
        /// Should we show the variable call count profiling info?
        /// </summary>
        internal static bool debugShowVariableCallCount = false;

        internal static bool useNewVariableAnimator = false;

        /// <summary>
        /// What is the minimum setting we want to allow for our variable
        /// refresh?
        /// </summary>
        internal static int minimumRefreshRate = 1;

        /// <summary>
        /// What should the initial refresh rate be?
        /// </summary>
        internal static int defaultRefreshRate = 10;

        internal static List<string> debugShowOnly = new List<string>();

        internal static Dictionary<string, ConfigNode> customVariables = new Dictionary<string, ConfigNode>();
        internal static List<string> knownLoadedAssemblies = new List<string>();
        internal static SortedDictionary<string, string> systemNamedResources = new SortedDictionary<string, string>();
        internal static List<RasterPropMonitorComputer.TriggeredEventTemplate> triggeredEvents = new List<RasterPropMonitorComputer.TriggeredEventTemplate>();
    }
}
