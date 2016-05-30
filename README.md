# RasterPropMonitor

RasterPropMonitor (RPM) is a toolkit and plugin that provides drastically-increased functionality to the IVA
mode in Kerbal Space Program.  Using RPM-enabled props, a player can control almost any aspect of spacecraft
or spaceplane operations.

RPM can interface with some mods, incorporating those mods' behaviors seamlessly into the RPM IVA.  A list of
actively supported and known working mods is available in the [release notes](https://github.com/Mihara/RasterPropMonitor/wiki/Changes-in-this-version).

Included in the RPM distribution are example props for use in enhancing the IVA experience.  Most of these
props were created by using stock KSP prop models.  There is also a MFD model by alexustas for use in glass cockpit designs.

The RPM distribution also contains [Module Manager](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-112-module-manager-2625-may-19th-where-the-singularity-started/) configs to override some of the stock IVAs with the example props to give you an idea of what is possible with RPM.

**NOTE:** This mod by itself is not intended to be a comprehensive IVA experience.  While basic IVAs are
included, they are intended to be examples of what is possible.  Because creating a good IVA takes a significant
amount of time (I've put more than 80 hours into each of the IVAs I've made), these example IVAs are not frequently updated, and they only scratch the surface of what can be
accomplished using RasterPropMonitor.

##INSTALLATION INSTRUCTIONS:

Extract the contents of the GameData folder in the RPM zip file into the GameData folder of your KSP install.  You should see the following folder structure:

```
GameData |
         + JSI |
               + Agencies
               + RasterPropMonitor
               + RPMPodPatches
```

If you do not see GameData/JSI, you have installed this mod incorrectly, and it shall misbehave (missing props, other things not working right).

If you used CKAN to install this mod, check the file structure.  CKAN has installed this mod incorrectly in the past.  If CKAN installed it incorrectly, remove this mod and install it manually.  I do not provide support for CKAN installations.

If the plugin was listed as a dependency by some other mod author, and you do not want to use the modified stock interiors included in this package, the JSI/RPMPodPatches directory may be safely deleted. Every capsule being modified has its own pair of patch config files -- you can safely delete only those you don't want.

###UPGRADING FROM OLDER VERSIONS:
v0.26.1 contains significant changes.  You should delete any existing installation.  v0.26.1 removed JSITransparentPod and its
corresponding JSINonTransparentPod, so legacy parts should be upgraded to use JSIAdvTransparentPods.

JSIAdvTransparentPods is a separate mod created by JPLRepo.  It can be found on GitHub at https://github.com/JPLRepo/JSIAdvTransparentPods and on the [KSP forum](http://forum.kerbalspaceprogram.com/index.php?/topic/138433-111-jsi-advanced-transparent-pods-v0160-previously-part-of-rasterpropmonitor-14th-may-2016/).

## MOAR IVAs?

If the basic IVA experience included in this package is not enough, take a look at some of these:

* [Aerokerbin Industries](http://forum.kerbalspaceprogram.com/index.php?/topic/86692-v50-rc1-released-aerokerbin-industries-modified-ivas/) Modified IVAs by MasseFlieger
* [ALCOR](http://forum.kerbalspaceprogram.com/index.php?/topic/50272-104alcorquotadvanced-landing-capsule-for-orbital-rendezvousquot-by-aset-21072015/) lander capsule by alexustas
* Yarbrough [Mk1. 1-1 A2](http://forum.kerbalspaceprogram.com/index.php?/topic/60681-10511-flight-systems-redux-aset-props-and-rpm-iva-8-april-2016/) IVA replacement by MOARdV
* [Mk1-2 Pod](http://forum.kerbalspaceprogram.com/index.php?/topic/116440-iva104-mk1-2-pod-iva-replacement-by-aset-wip/) by alexustas
* [Mk3 Pod IVA](http://forum.kerbalspaceprogram.com/index.php?/topic/119612-iva11-mk3-pod-iva-replacement-by-apex-wip/) Replacement by Apex

Take a look around the forum, and you'll find other mods with RasterPropMonitor IVAs.

### Even MOAR IVAs!
Do you want to try making your own IVA?  You'll need to download Unity and KSP's PartTools (look on the forum for more information).

While you can use the props included with this distribution to make some basic IVAs, you really should use the
following prop packs to make something exceptional:

* [ASET Avionics](forum.kerbalspaceprogram.com/index.php?/topic/116479-ivaprops-aset-avionics-pack-v-10-for-the-modders-who-create-ivaã¢â‚¬â„¢s/) - primarily aircraft-oriented props.
* [ASET Props](forum.kerbalspaceprogram.com/index.php?/topic/116430-ivaprops-aset-props-pack-v13-for-the-modders-who-create-ivaã¢â‚¬â„¢s/) - a mix of spacecraft and spaceplane props, including several MFD designs.

**NOTE 2:** As of 28 May 2016, the ASET props and avionics packs *are* being updated.  Until alexustas is ready to release them, there are links in the ASET Props thread to the current edition of those props packs.

## LINKS

See [the forum support thread](http://forum.kerbalspaceprogram.com/index.php?/topic/105821-105-rasterpropmonitor-still-putting-the-a-in-iva-v0240-10-november-2015/) for support.

See [the dull^H^H^H^H full documentation](https://github.com/Mihara/RasterPropMonitor/wiki) in the wiki on GitHub.

For the latest release notes, please refer to the wiki at
[Changes in this version](https://github.com/Mihara/RasterPropMonitor/wiki/Changes-in-this-version)

Source code and full license information available at
[GitHub](https://github.com/Mihara/RasterPropMonitor/)

## LICENSES

RasterPropMonitor plugin (C) 2013-2016 Mihara, MOARdV, and other contributors.

Code and shaders are licensed under GPLv3.

Props courtesy of alexustas and other contributors, and available under the terms of CC 3.0 BY-NC-SA. Portions of this package are derived from stock textures by Squad and are distributed according to Squad policy of permitting to distribute stock assets with mods if required.

The ModuleManager plugin included in this distribution to modify stock config files on the fly is available under the terms of CC SA, and obtained from [this thread](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-105-module-manager-2618-january-17th-with-even-more-sha-and-less-bug/#comment-720814)

RasterPropMonitor includes the following fonts in its Asset Bundle:

[Repetition Scrolling Font](http://www.1001fonts.com/repetition-scrolling-font.html) by Tepid Monkey Fonts.

[Digital-7](http://www.fontspace.com/style-7/digital-7) by Sizenko Alexander [Style-7](http://www.styleseven.com).

[InconsolataGo](http://www.levien.com/type/myfonts/), released under the [Open Font License](http://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&item_id=OFL&_sc=1).
