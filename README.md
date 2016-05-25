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

If you used CKAN to install this mod, check the file structure.  CKAN has installed this mod incorrectly in the past.  If it is installed incorrectly, remove this mod and install it manually.

If the plugin was listed as a dependency by some other mod author, and you do not want to use modified stock interiors, the JSI/RPMPodPatches directory may be safely deleted. Every capsule being modified has its own pair of patch config files -- you can safely delete only those you don't want.

##UPGRADING FROM OLDER VERSIONS:
v0.25.0 contains significant changes.  You should delete any existing installation.

JSIAdvTransparentPods is now a separate mod maintained by JPLRepo.  It can be found on GitHub at https://github.com/JPLRepo/JSIAdvTransparentPods .

# LINKS

See [the forum support thread](http://forum.kerbalspaceprogram.com/index.php?/topic/105821-105-rasterpropmonitor-still-putting-the-a-in-iva-v0240-10-november-2015/) for support.

See [the dull^H^H^H^H full documentation](https://github.com/Mihara/RasterPropMonitor/wiki) in the wiki on GitHub.

For the latest release notes, please refer to the wiki at
[Changes in this version](https://github.com/Mihara/RasterPropMonitor/wiki/Changes-in-this-version)

Source code and full license information available at
[GitHub](https://github.com/Mihara/RasterPropMonitor/)

RasterPropMonitor plugin (C) 2013-2016 Mihara, MOARdV, and other contributors.

Code is licensed under GPLv3. Props courtesy of alexustas and other contributors, and available under the terms of CC 3.0 BY-NC-SA. Portions of this package are derived from stock textures by Squad and are distributed according to Squad policy of permitting to distribute stock assets with mods if required.

The ModuleManager plugin included in this distribution to modify stock config files on the fly is available under the terms of CC SA, and obtained from [this thread](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-105-module-manager-2618-january-17th-with-even-more-sha-and-less-bug/#comment-720814)

RasterPropMonitor includes embeds the following fonts:

[Repetition Scrolling Font](http://www.1001fonts.com/repetition-scrolling-font.html) by Tepid Monkey Fonts.

[Digital-7](http://www.fontspace.com/style-7/digital-7) by Sizenko Alexander [Style-7](http://www.styleseven.com).

[InconsolataGo](http://www.levien.com/type/myfonts/), released under the [Open Font License](http://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&item_id=OFL&_sc=1).
