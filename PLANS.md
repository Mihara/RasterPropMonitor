# 0.13 milestone targets

## Minor new features

* Undocking menu inside the targeting control menu structure.

## Major new features

* Aviation-style PFD for transparent HUD use. (Well, this is just about done, needs testing and documenting...)

# 0.14 milestone targets

## Minor new features

* A variable to compute the mass of propellants only. Needs asking the engines which propellants do they want...
* ...but if I can get the list of propellants for the currently active engines, I can get a reasonable dV-for-current-stage at least.

## Major new features

* Orbit display a-la Orbiter -- schematic representation of the body as a circle and the orbit as an ellipse viewed from the direction of orbit normal. Needs quite a bit of thinking...
* API to plug extra modules into RPMC so that you could use variables siphoned out of MJ, FAR and other data producers that you otherwise can't. *(Global config blocks loaded through GameData like map vector points, calculator instantiates sub-part-modules when loaded, native variable processor returns some nonsense in case nothing was recognised upon which the results are fed through the chain of processors)*.
* kOSTER ("Campfire") -- A full keyboard kOS terminal. Mostly waiting on the model now before starting.

# 1.0 milestone targets

* Debugging, debugging, debugging. It needs to be as idiot proof as it can possibly be.
* If there are any breaking API changes that are still needed, they should be settled by then.

# Future targets

## Minor new features

* ;-splitter in custom formatters needs to be able to survive quoted and escaped ; when splitting -- but it should do that without regex.
* Resource consumption rates.
* Is it possible to make a menu of all science experiments available on the ship which one could select to bring up their windows and do science from the inside? They're action-buttonable, so it's possible in theory, but it's only worth it if there's a general enough mechanism I could trigger.
* BobCat wants markers on graph lines denoting things like staging events, but I'm not clear on how to set this up nicely.
* Make JSIActionGroupSwitch pluggable and plug MechJeb into it.
* cameraTransform should be a list of prospective transform names to try.
* On high G, place a green polygon in front of the IVA camera to simulate G blackout. Speed of blackin/blackout depends on the courage of the currently active IVA kerbal. :) Possibly other camera effects?
* Spot lights generated within camera structure?
* A key combination to reseat the current kerbal in the same pod so that he can take the pilot's seat if the pilot went out. (This is proving to be difficult...)

## Major new features

* Maneuver node creator/editor menu? I don't even know how to start this one, I don't see how it could be usable yet, even though there's no problem actually doing it.
* Moving-in-IVA:
    * Make an InternalModule that detects doubleclicks on a transform. Place that collider over an internal hatch.
    * When in IVA player clicks on that hatch, search the rest of the ship for a habitable capsule with IVA in the direction of the click (Somewhat non-trivial as it will require intelligently navigating the part tree).
    * Locate a part which has an IVA and a free InternalSeat.
    * Detect which kerbal we're currently looking with. (Well, that is now it's own static function)
    * Move that kerbal to the seat found. (Despawn/spawn should work, but somehow the entire internal is getting borked in the attempt.)
      Voila, we have moving-in-iva, or at least as close as it ever gets.
