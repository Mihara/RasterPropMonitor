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
using System.Text;

namespace JSI
{
    /// <summary>
    /// JSIGimbal provides an interface for gimbals
    /// </summary>
    class JSIGimbal : IJSIModule
    {
        /// <summary>
        /// Locks / unlocks gimbals on the currently-active stage.
        /// </summary>
        /// <param name="state"></param>
        public void GimbalLock(bool state)
        {
            foreach (ModuleGimbal gimbal in FindActiveStageGimbals(vessel))
            {
                gimbal.gimbalLock = state;
            }
        }

        /// <summary>
        /// Returns true if at least one gimbal on the active stage is locked.
        /// </summary>
        /// <returns></returns>
        public bool GimbalLockState()
        {
            bool gimbalLockState = false;

            if (vessel == null)
            {
                return gimbalLockState; // early
            }

            foreach (ModuleGimbal gimbal in FindActiveStageGimbals(vessel))
            {
                if (gimbal.gimbalLock)
                {
                    gimbalLockState = true;
                    break;
                }
            }

            return gimbalLockState;
        }

        /// <summary>
        /// Iterator to find gimbals on active stages
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private static IEnumerable<ModuleGimbal> FindActiveStageGimbals(Vessel vessel)
        {
            foreach (Part thatPart in vessel.parts)
            {
                // MOARdV: I'm not sure inverseStage is ever > CurrentStage,
                // but there's no harm in >= vs ==.
                if (thatPart.inverseStage >= Staging.CurrentStage)
                {
                    foreach (PartModule pm in thatPart.Modules)
                    {
                        if (pm is ModuleGimbal)
                        {
                            yield return pm as ModuleGimbal;
                        }
                    }
                }
            }
        }
    }
}
