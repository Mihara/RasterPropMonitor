# RasterPropMonitor

This is a showcase mod for RasterPropMonitor which adds a useful information display to certain stock and mod capsules. This is also the canonical source of RasterPropMonitor.dll plugin and associated files.

#INSTALLATION INSTRUCTIONS:

Extract the contents of the GameData folder in the RPM zip file into the GameData folder of your KSP install.  You should see the following folder structure:

```
GameData |
         + JSI |
               + Agencies
               + RasterPropMonitor
               + RPMPodPatches
```

If you do not see GameData/JSI, you have installed this mod incorrectly, and it shall misbehave (missing props, other things not working right).

If the plugin was listed as a dependency by some other mod author, the JSI/RPMPodPatches directory may be safely deleted. Every capsule being modified has its own pair of patch config files -- you can safely delete only those you don't want.

##UPGRADING FROM OLDER VERSIONS:
Some files were removed during v0.19.x and v0.20.0.  If you are upgrading from an older version of RPM, I recommend you delete JSI/RPMPodPatches and JSI/RasterPropMonitor before extracting the new version (or just delete the JSI directory completely).

# LINKS

See [the forum support thread](http://forum.kerbalspaceprogram.com/index.php?/topic/105821-105-rasterpropmonitor-still-putting-the-a-in-iva-v0240-10-november-2015/) for support.

See [the dull^H^H^H^ full documentation](https://github.com/Mihara/RasterPropMonitor/wiki) in the wiki on GitHub.

For the latest release notes, please refer to the wiki at
[Changes in this version](https://github.com/Mihara/RasterPropMonitor/wiki/Changes-in-this-version)

Source code and full license information available at
[GitHub](https://github.com/Mihara/RasterPropMonitor/)

RasterPropMonitor plugin (C) 2013-2016 Mihara, MOARdV, and other contributors.

Code is licensed under GPLv3. Props courtesy of alexustas and other contributors, and available under the terms of CC 3.0 BY-NC-SA. Portions of this package are derived from stock textures by Squad and are distributed according to Squad policy of permitting to distribute stock assets with mods if required.

The ModuleManager plugin included in this distribution to modify stock config files on the fly is available under the terms of CC SA, and obtained from [this thread](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-105-module-manager-2618-january-17th-with-even-more-sha-and-less-bug/#comment-720814)
