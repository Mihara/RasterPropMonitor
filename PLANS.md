# 0.11 milestone targets

* This probably comes out right after the KSP 0.23 release as soon as I can ensure nothing was broken.

## Major new features

* MechJeb SmartASS interface menu with an 'execute node' option. *(Due to the rumoured fix of module-loading-order problem in KSP 0.23, pushing this down to 0.11 means 0.11 can't come out earlier than 0.23 comes out, since it introduces yet another floating assembly regardless.)*

# 0.12 milestone targets

## Major new features

* Orbit display a-la Orbiter -- schematic representation of the body as a circle and the orbit as an ellipse viewed from the direction of orbit normal. Needs quite a bit of thinking...
* kOSTER ("Campfire") -- A full keyboard kOS terminal. I'm pretty sure it's possible now, mostly waiting on the model to test it with and KSP 0.23 release before starting.

# Future targets

## Issues.

* Introduce Visual Enhancements cameras directly into the camera pipeline when (if) they get unique names to find them by.

## Minor new features

* Work out an API setting to direct handler loader to look at another prop?
* Try to make JSISCANsatRPM correctly handle multiple calling props. Might require an API change...
* It might be possible to create a variable orientation pod that can switch control direction depending on whether you want to use it for a spaceship or a rover.
* Multiple and-ed conditions in VariableAnimator? Not sure if it's practical, would need a significant rewrite and I don't want a Turing tarpit... On the other hand, that might call for actually figuring out a DSL in Boo.

## Major new features

* Maneuver node creator/editor menu? I don't even know how to start this one, I don't see how it could be usable yet, even though there's no problem actually doing it.
