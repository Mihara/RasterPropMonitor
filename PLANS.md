# Potential future features currently in research phase

For things already in the pipeline, see [project issues page](https://github.com/Mihara/RasterPropMonitor/issues)

## Minor new features

* BobCat wants markers on graph lines denoting things like staging events, but I'm not clear on how to set this up nicely.
* A key combination to reseat the current kerbal in the same pod so that he can take the pilot's seat if the pilot went out. (This is proving to be difficult...)

## Major new features

* Moving-in-IVA:
    * Make an InternalModule that detects doubleclicks on a transform. Place that collider over an internal hatch.
    * When in IVA player clicks on that hatch, search the rest of the ship for a habitable capsule with IVA in the direction of the click (Somewhat non-trivial as it will require intelligently navigating the part tree).
    * Locate a part which has an IVA and a free InternalSeat.
    * Detect which kerbal we're currently looking with. (Well, that is now it's own static function)
    * Move that kerbal to the seat found. (Despawn/spawn should work, but somehow the entire internal is getting borked in the attempt.)
      Voila, we have moving-in-iva, or at least as close as it ever gets.
