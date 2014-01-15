# Frequently Asked Questions about RasterPropMonitor

### Half the buttons don't work!

Certain buttons are reserved for future expansion, which happens pretty rapidly -- there's nothing assigned to them. It may also be that you're expecting the "up" and "down" buttons to switch pages. They don't actually work this way -- in the example package, every page corresponds to a single button, and cursor buttons only work on cursors and other similarly-behaving entities, which only certain pages actually have.

It's sort of an approximation of how actual MFDs work, though they typically use interfaces even less similar to what you might expect. Version 0.13 lets you switch pages in much more creative ways, but the default package does not make use of them.

### The screens don't work at all!

Make sure that there is only one RasterPropMonitor.dll and that it is located at GameData/JSI/RasterPropMonitor/Plugins/RasterPropMonitor.dll -- several earlier third-party capsules distributed old versions incorrectly. They *will* fail to work in 0.23.

### I get lots of exclamation signs and gibberish all over my screens!

Update Active Texture Management to latest version and remember, for the future, that not all textures can be safely resized -- with any capsule that uses RasterPropMonitor, the problem may recur if you don't manage your Active Texture Management settings intelligently. "Aggressive" settings are called that for a reason.

### RasterPropMonitor is very heavy on the framerate.

To my knowledge it isn't -- I started developing it on a machine that is well over five years old and was very conscious of the framerate the whole time. But it might be a problem I have yet to bump into, and if you help me, I can probably fix it.

I want your KSP_Data/output_log.txt and as precise a description of what you were doing as possible.

### How do I get the map to show? It says "No satellite connection"

RasterPropMonitor relies on [SCANsat](http://forum.kerbalspaceprogram.com/threads/55832) for mapping data. These are the precise satellites it means to have a connection to -- install it and launch some, they're quite a lot of fun. It's one of those things that makes probes more than just a practice launch.

### RasterPropMonitor breaks Kethane map!

This is a Kethane problem. Update to Kethane 0.8.4 or newer.

### What are those ☊ and ☋ symbols?

They are the traditional symbols for ascending node and descending node respectively. [See Wikipedia page](https://en.wikipedia.org/wiki/Orbital%20node) which also nicely explains what those node things are. :)

Basically, they're points in which you want to burn to change your orbit inclination optimally.

### Can your monitors show X?

RasterPropMonitor is more of an IVA makers toolkit than a standalone product, I spend most of my time programming new things for the monitors to show, but barely have the energy left to arrange it for actual presentation. My own implementation is a few generations behind. Check the [list of variables](https://github.com/Mihara/RasterPropMonitor/wiki/Defined-variables) and the documentation on [page file syntax](https://github.com/Mihara/RasterPropMonitor/wiki/Writing-page-definition-files), it's quite possible what you want is already there, just isn't presented to the user in my own implementation yet. Customize your monitors and share the results -- if you make nice pages, I'll be happy to include them in the distribution.

### Can I make a maneuver node while in IVA?

Not *yet*. While it is in the plans, there is the considerable question of how to make a menu-based maneuver node editor with an interface that doesn't suck.

### How do I show remaining delta V?

Wait for version 0.14.

### How do I perform mathematical operations within a page? 

You can't. String.Format is a (rather basic by itself) string layout language, but it is not a mathematical expression processor. Tags are a screen layout language, but they are not a mathematical expression processor either.

While there have been calls for implementing one, that's one of the things I'm very wary of doing at all -- it's not particularly hard to do, but it's also all too easy to produce a [Turing Tarpit](https://en.wikipedia.org/wiki/Turing%20tarpit), unwieldy to use and slowing the whole thing down, I've spent much of my life fighting these things and I don't want to create another one. If it's to be a programming language, I can at least make sure it will be a real one and make it suck in modules that will get locally compiled from a domain-specific [Boo](http://boo.codehaus.org/) variant or something to that effect. It will take a while for me to write that, though, but I'm pretty sure eventually I will.

In the meantime, if you want a variable that is derived from existing variables, there's no problem at all just adding one -- give me a list of what you actually want. I'll readily add anything provided it doesn't require writing more than a page of code to do it, and I will seriously consider things that will so require.
