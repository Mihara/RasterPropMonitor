# Frequently Asked Questions about RasterPropMonitor

### Half the buttons don't work!

Half the buttons are reserved for future expansion, which happens pretty rapidly. It may also be that you're expecting the "up" and "down" buttons to switch pages. They don't actually work this way -- in the example package, every page corresponds to a single button, and cursor buttons only work on cursors and other similarly-behaving entities, which only certain pages actually have.

It's sort of an approximation of how actual MFDs work, though they actually typically use interfaces even less similar to what you might expect. 

### The screens don't work at all!

Make sure that there is only one RasterPropMonitor.dll and that it is located at GameData/JSI/RasterPropMonitor/Plugins/RasterPropMonitor.dll -- several earlier third-party capsules distributed old versions incorrectly. They *will* fail to work in 0.23.

### RasterPropMonitor is very heavy on the framerate.

To my knowledge it isn't, and I develop it on a machine that is well over five years old. But it might be a problem I have yet to bump into, and if you help me, I can probably fix it.

I want your KSP_Data/output_log.txt and as precise a description of what you were doing as possible.

### There's a conflict with Firespitter.dll

Update your Firespitter.dll from the [master Firespitter package](http://kerbalspaceport.com/firespitter-propeller-plane-parts/) and the problem will go away.

### How do I get the map to show? It says "No satellite connection"

RasterPropMonitor relies on [SCANsat](http://forum.kerbalspaceprogram.com/threads/55832) for mapping data. These are the precise satellites it means to have a connection to -- install it and launch some, they're quite a lot of fun.

### RasterPropMonitor breaks Kethane map!

This is a Kethane problem that can be triggered by any mod which includes a plugin that fails to load. Unfortunately, sometimes they're *supposed* to fail to load, and that is the case with RasterPropMonitor.

Kethane searches the other plugins for functions implementing it's API, so that the API can actually happen. Unfortunately, the plugin that failed to load also gets found, but then cannot be accessed and Kethane chokes on it. The solution at the moment is looking diligently through the debug log for a plugin that generates an AssemblyLoader exception ("AssemblyLoader: Exception loading *thatplugin*") and making sure it is updated or failing that, removed.

RasterPropMonitor interfaces to other mods for certain functionality and if those mods are not installed, the interface modules will fail to load, because they refer to code that does not exist on the system. If you use RPM but do not use SCANsat, SCANsatRPM.dll will fail to load (as intended) and trigger this bug. If you use RPM but do not use MechJeb, MechJebRPM.dll will fail to load (as intended!) and trigger this bug. The same will happen with any plugins that optionally use the blizzy's [Toolbar](http://forum.kerbalspaceprogram.com/threads/60066) when the toolbar itself is not installed -- this requires them to bundle a separate plugin which they use to communicate with the toolbar, which will fail to load when the toolbar is not installed.

Majir [seems to have fixed it](https://github.com/Majiir/Kethane/commit/e97d806b63cad6921532a612974fd941c9f50209), but he isn't treating releasing the fixed version as a matter of urgency. Until he does, you can either install MechJeb and SCANsat or remove MechJebRPM.dll/SCANsatRPM.dll.

### Can your monitors show X?

RasterPropMonitor is more of an IVA makers toolkit than a standalone product, I spend most of my time programming new things for the monitors to show, but barely have the energy left to arrange it for actual presentation. My own implementation is a few generations behind. Check the [list of variables](https://github.com/Mihara/RasterPropMonitor/wiki/Defined-variables) and the documentation on [page file syntax](https://github.com/Mihara/RasterPropMonitor/wiki/Writing-page-definition-files), it's quite possible what you want is already there, just isn't presented to the user in my own implementation yet. Customize your monitors and share the results -- if you make nice pages, I'll be happy to include them in the distribution.

### How do I show remaining delta V?

You can't. There is a strong technical reason, because otherwise I've been implementing everything including a kitchen sink whenever I could, and plan to continue doing so.

Calculating remaining dV is mathematically simple, but the actual problem is not so much calculating it, but determining the exact amount of fuel that your currently enabled engines have access to, depending on what counts as fuel this season. Paradoxically, even the stock resource tab has problems showing you resources remaining in the current stage. To show you dV per stage, MechJeb requires a separate module which does nothing but that, runs in a separate thread, and needs to be periodically polled until it can come up with results because otherwise it takes too long. Even MechJeb does not always get it right, although it comes up with better numbers than stock resource tab.

I can import the whole chunk of code from MechJeb, though it is fairly big, but then I would end up with a module that is essentially a black box to me which I do not understand particularly well and can't fix when it breaks -- eventually it will -- or I can ask MechJeb for the values if MechJeb is installed and give you a very, very rough figure if it isn't. This is what I plan to do in a future version.

### How do I perform mathematical operations within a page? 

You can't. String.Format is a (rather basic by itself) string layout language, but it is not a mathematical expression processor. Tags are a screen layout language, but they are not a mathematical expression processor either.

While there have been calls for implementing one, that's one of the things I'm very wary of doing at all -- it's not particularly hard to do, but it's also all too easy to produce a [Turing Tarpit](https://en.wikipedia.org/wiki/Turing_tarpit), unwieldy to use and slowing the whole thing down, I've spent much of my life fighting these things and I don't want to create another one. If it's to be a programming language, I can at least make sure it will be a real one and make it suck in modules that will get locally compiled from a domain-specific [Boo](http://boo.codehaus.org/) variant or something to that effect. It will take a while for me to write that, though, but I'm pretty sure eventually I will.

In the meantime, if you want a variable that is derived from existing variables, there's no problem at all just adding one -- give me a list of what you actually want. I'll readily add anything provided it doesn't require writing more than a page of code to do it, and I will seriously consider things that will so require.