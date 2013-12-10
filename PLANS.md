# 0.11 milestone targets

## Minor new features

* Page handler to select between different page definition files based on variable values.

## Major new features

* Graphs using the vector tech from JSISCANsatRPM and variables.
* MechJeb SmartASS interface menu with an 'execute node' option.

# Future targets

## Issues.

* Introduce Visual Enhancements cameras directly into the camera pipeline when (if) they get unique names to find them by.

## Minor new features

* Work out an API setting to direct handler loader to look at another prop?
* Try to make JSISCANsatRPM correctly handle multiple calling props. Might require an API change...
* It should be possible to SetReferenceTransform to a specific docking port while in IVA, (ModuleDockingNode.MakeReferenceTransform() *should* work.) but experiments suggest results can be bizarre, and multiple screens can be problematic. *(Which setter takes precedence? What if we are watching multiple port cameras?)* If this can be done, this should be part of the targeting menu...
* Side idea: If this does work, it might be possible to create a variable orientation pod that can switch control direction depending on whether you want to use it for a spaceship or a rover.
* Multiple and-ed conditions in VariableAnimator?...

## Major new features

* Maneuver node creator/editor menu?
* Orbit display a-la Orbiter?
* kOSTER ("Campfire") -- A full keyboard kOS terminal. :)
