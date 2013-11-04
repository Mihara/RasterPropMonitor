## Creating a display model for RasterPropMonitor

1. You need a font bitmap. The plugin treats fonts as if they were fixed width, so it's best to take a fixed width font to start with.
   We used [CBFG](http://www.codehead.co.uk/cbfg/) to generate ours from [Fixedsys Excelsior](http://www.fixedsysexcelsior.com/),
   but there are other programs to do the same thing.
   
   Every letter is assumed to occupy a block of fontLetterWidth by fontLetterHeight pixels on the bitmap. Font texture size must be
   evenly divisible by fontLetterWidth/fontLetterHeight respectively. For Unity reasons,
   the bitmap has to have sizes that are a power of 2. Characters are read left to right, top to bottom, with the bottom
   left character being character number 32 (space). Characters 128-159 are skipped due to pecularities of how KSP treats strings.
   
2. You need a model for your screen. If it's to have buttons, they need to be named transforms with isTrigger on them enabled.
   The screen must be a named transform, arranged in such a way that the texture's 0,1 coordinates are the top left corner of the screen.
   It must already have a texture in the layer ("_MainTex", "_Emissive", etc) that the plugin will replace. To save memory, that
   placeholder texture should be the minimum size possible, which for KSP appears to be 32x32 pixels.
   
## Configuring a monitor

Monitors are created using two modules: RasterPropMonitor and RasterPropMonitorGenerator in a prop configuration file. RasterPropMonitor 
takes care of the display