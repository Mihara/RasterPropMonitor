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

