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
using UnityEngine;

namespace JSI
{
    /// <summary>
    /// The JSIThrustReverser is intended to be added to an engine that contains
    /// a thrust reverser animation in its ModuleAnimateGeneric.  It is based
    /// on the assumption that only a single ModuleAnimateGeneric is found on
    /// the part.  It does *not* look for an engine, as well, so it could be
    /// attached to other animated parts such that those parts trigger with the
    /// RPM thrust reverser trigger.
    /// </summary>
    public class JSIThrustReverser : PartModule
    {
        internal ModuleAnimateGeneric thrustReverser;

        /// <summary>
        /// Startup - look for the first ModuleAnimateGeneric.  Keep a
        /// reference to it.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                foreach (var module in part.Modules)
                {
                    if (module is ModuleAnimateGeneric)
                    {
                        thrustReverser = module as ModuleAnimateGeneric;
                        JUtil.LogMessage(this, "Found my thrust reverser");
                        break;
                    }
                }

                if (thrustReverser == null)
                {
                    isEnabled = false;
                }
            }
        }

        /// <summary>
        /// Tear down - null our reference to thrustReverser.
        /// </summary>
        public void OnDestroy()
        {
            thrustReverser = null;
        }
    }
}
