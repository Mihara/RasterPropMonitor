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
using System.Linq;
using System.Text;

namespace JSI
{
    public class JSIEngine:IJSIModule
    {
        public JSIEngine()
        {

        }

        public bool GetRunningPrimary()
        {
            // TODO: select which engine
            bool runningPrimary = true;
            foreach (JSIEngineMonitor mon in FindEngineMonitorIn(vessel))
            {
                if(!mon.GetRunningPrimary())
                {
                    runningPrimary = false;
                }
            }

            return runningPrimary;
        }

        public void SetRunningPrimary(bool state)
        {
            foreach(JSIEngineMonitor mon in FindEngineMonitorIn(vessel))
            {
                mon.SetRunningPrimary(state);
            }
        }

        private static IEnumerable<JSIEngineMonitor> FindEngineMonitorIn(Vessel vessel)
        {
            for (int i = 0; i < vessel.Parts.Count; ++i )
            {
                for (int j = 0; j < vessel.Parts[i].Modules.Count; ++j )
                {
                    if (vessel.Parts[i].Modules[j] is JSIEngineMonitor)
                    {
                        yield return vessel.Parts[i].Modules[j] as JSIEngineMonitor;
                    }
                }
            }
        }
    }
}
