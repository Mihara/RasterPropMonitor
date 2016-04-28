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
using UnityEngine;

namespace JSI
{
    // These two modules are now deprecated and replaced by JSIAdvTransparentPod module.
    // This is a shell module now that logs a message to the user to have advise mod authors to update to the new module.

    public class JSITransparentPod : PartModule
    {
        public override void OnLoad(ConfigNode node)
        {
            JUtil.LogErrorMessage(this,
                "This module has been deprecated and you should contact the Mod author of {0}:{1} to upgrade their Mod to the new JSIAdvTransparentPods mod.", part.partName, part.name);
            Destroy(this);
        }
    }

    // This is a deprecated module and replaced by JSIAdvTransparentPod module.
    // This is a shell module now that logs a message to the user to have advise mod authors to update to the new module.

    public class JSINonTransparentPod : PartModule
    {
        public override void OnLoad(ConfigNode node)
        {
            JUtil.LogErrorMessage(this, "This module has been deprecated and you should remove any Module Manager config files referencing it.");
            Destroy(this);
        }
    }
}
