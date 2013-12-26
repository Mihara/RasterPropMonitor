# 0.13 milestone targets

## Minor new features

* A variable to compute the mass of propellants only. Needs asking the engines which propellants do they want...
* JSISteerableCamera needs some graphical way of indicating how far did it actually get offset. Some kind of crosshair icon that keeps pointing in the pre-offset direction? Maybe even changes size with zoom?

## Major new features

* Aviation-style PFD for transparent HUD use.
* Orbit display a-la Orbiter -- schematic representation of the body as a circle and the orbit as an ellipse viewed from the direction of orbit normal. Needs quite a bit of thinking...
* API to plug extra modules into RPMC so that you could use variables siphoned out of MJ, FAR and other data producers that you otherwise can't. *(Global config blocks loaded through GameData like map vector points, calculator instantiates sub-part-modules when loaded, native variable processor returns some nonsense in case nothing was recognised upon which the results are fed through the chain of processors)*.

# 1.0 milestone targets

* Debugging, debugging, debugging. It needs to be as idiot proof as it can possibly be.
* If there are any breaking API changes that are still needed, they should be settled by then.

# Future targets

## Minor new features

* Should I introduce TGT+/TGT- markers into PFD instead of mode switching? Come to think of it, can I do REL+/REL- markers too?
* Is it possible to make a menu of all science experiments available on the ship which one could select to bring up their windows and do science from the inside? They're action-buttonable, so it's possible in theory, but it's only worth it if there's a general enough mechanism I could trigger.
* BobCat wants markers on graph lines denoting things like staging events, but I'm not clear on how to set this up nicely.
* Make JSIActionGroupSwitch pluggable and plug MechJeb into it.
* Undocking menu.
* cameraTransform should be a list of prospective transform names to try.
* On high G, place a green polygon in front of the IVA camera to simulate G blackout. Speed of blackin/blackout depends on the courage of the currently active IVA kerbal. :)

## Major new features

* Analogue of Firespitter's infoitem module -- A page handler that is a part module which presents the user with a text editor. One clever idea is to use the vessel description, which requires getting at it before launch, since it doesn't exist after. EditorLogic.fetch.shipDescriptionField.Text should do it.
* kOSTER ("Campfire") -- A full keyboard kOS terminal. Mostly waiting on the model now before starting.
* Maneuver node creator/editor menu? I don't even know how to start this one, I don't see how it could be usable yet, even though there's no problem actually doing it.
