# 0.10 milestone targets

* Pre-release testing!

# 0.11 milestone targets

## Minor new features

* Page handler to select between different page definition files based on variable values.
* Remote button clicking from other props by directly calling their SmarterButton/OnMouseDown, or another similar arrangement.
* Rearrange code so that the page class gets a pointer to the camera class and can change FOV of the camera with globalButtons if there are no other handlers.

## Major new features

* Graphs using the vector tech from JSISCANsatRPM and variables.
* MechJeb SmartASS interface menu with an 'execute node' option.

# Future targets

## Issues.

* Introduce Visual Enhancements cameras directly into the camera pipeline when (if) they get unique names to find them by.

## Minor new features

* Work out an API setting to direct handler loader to look at another prop?
* Try to make JSISCANsatRPM correctly handle multiple calling props. Might require an API change...
* It should be possible to SetReferenceTransform to a specific docking port while in IVA... It might even be possible to do that to the port we're currently looking out of, and I can use JSIExternalCameraSelector to provide the reference.

## Major new features

* Special shader for printing text with well-defined behaviour?
* Maneuver node creator/editor menu?
* Orbit display a-la Orbiter?