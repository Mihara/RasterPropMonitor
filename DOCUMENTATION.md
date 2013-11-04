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

Monitors are created using two modules: **RasterPropMonitor** and **RasterPropMonitorGenerator** in a prop configuration file. RasterPropMonitor 
takes care of the display, while RasterPropMonitorGenerator feeds it with the data to display.

### RasterPropMonitor configuration

* **screenTransform** -- the name of the screen object.
* **textureLayerID** -- Unity name of a texture ID in the object's material that the screen will be printed on. Defaults to "_MainTex".
* **fontTransform** -- Where to get the font bitmap. You can either place a texture somewhere in GameData and refer to it exactly like 
  you would in a MODEL configuration node *(KSP reads everything that looks like a texture and is stored outside of a PluginData directory)*
  or put the texture on a model transform and give it's name. 
* **blankingColor** -- R,G,B,A of a color that will be used to blank out a screen between refreshes.
* **screenWidth**/**screenHeight** -- Number of characters in a line and number of lines.
* **screenPixelWidth**/**screenPixelHeight** -- Width and height of the texture to be generated for the screen.
* **fontLetterWidth**/**fontLetterHeight** -- Width and height of a font cell in pixels.

Letters are printed on the screen in pixel-perfect mapping, so one pixel of a font texture will always correspond to one pixel of the generated screen texture -- as a result, you can have less characters in a line than would fit into screenPixelWidth, but can't have more.

### RasterPropMonitorGenerator configuration

