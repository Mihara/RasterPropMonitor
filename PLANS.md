# 0.12 milestone targets

## Minor new features

* A "follow reference" mode for cameras: A special option for PAGE that, instead of looking for a specific camera transform, finds the reference part, determines if it's a docking node, and if it is, places the camera on it's reference transform, otherwise shows no camera -- so that particular page always shows the view from the currently active docking port.
* Halfwidth/doublewidth fonts.

## Major new features

* Orbit display a-la Orbiter -- schematic representation of the body as a circle and the orbit as an ellipse viewed from the direction of orbit normal. Needs quite a bit of thinking...
* kOSTER ("Campfire") -- A full keyboard kOS terminal. I'm pretty sure it's possible now, mostly waiting on the model to test it with and KSP 0.23 release before starting.

# Future targets

## Issues.

* Introduce Visual Enhancements cameras directly into the camera pipeline when (if) they get unique names to find them by.

## Minor new features

* Should I introduce TGT+/TGT- markers into PFD instead of mode switching? Come to think of it, can I do REL+/REL- markers too?
* Work out an API setting to direct handler loader to look at another prop?
* Is it possible to make a menu of all science experiments available on the ship which one could select to bring up their windows and do science from the inside? They're action-buttonable, so it's possible in theory, but it's only worth it if there's a general enough mechanism I could trigger.
* Try to make JSISCANsatRPM correctly handle multiple calling props. Might require an API change...
* Multiple and-ed conditions in VariableAnimator? Not sure if it's practical, would need a significant rewrite and I don't want a Turing tarpit... On the other hand, that might call for actually figuring out a DSL in Boo.
* Multiple fonts? It's easy enough to do tag-based font switching now, so something like FONTS{ name = value } would be trivial provided all the fonts have the same pixel size.
* Textline-based font contents definition?

## Major new features

* API to plug extra modules into RPMC so that you could use variables siphoned out of MJ, FAR and other data producers that you otherwise can't. *(Global config blocks loaded through GameData like map vector points, calculator instantiates sub-part-modules when loaded, native variable processor returns some nonsense in case nothing was recognised upon which the results are fed through the chain of processors)*.
* Maneuver node creator/editor menu? I don't even know how to start this one, I don't see how it could be usable yet, even though there's no problem actually doing it.
