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
   It must already have a texture in the layer ("\_MainTex", "\_Emissive", etc) that the plugin will replace. To save memory, that
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

* **refreshRate** -- The screen will be redrawn no more often than once this number of frames.
* **refreshDataRate** -- Various computationally intensive tasks will be performed no more often than once this number of frames.
* **page1,page2...page8** -- Page definitions.
* **button1,button2...button8** -- Button transform names that correspond to pages.

You need to have at least one page (page1). Clicking on button2 will cause page2 to be rendered, etc. If there is a button2 option, but no page2 defined, the screen will be blanked.

Pages can be defined in one of two ways -- by referencing a text file that contains a complete screen definition, or directly in the page parameter.
Text file reference is just like a texture URL, the only difference is that it must have a file extension.

If you wish to insert a line break in the page definition written directly in a prop config file, you need to replace it with "**$$$**". If you wish to use the { and } format string characters in such a screen definition, you need to replace **{** with **<=** and **}** with **=>**, because KSP mangles them upon reading from prop.cfg files.

### Screen definitions

Screen definitions are normal text files in UTF-8 encoding, lines are separated by normal line break characters.
The real power of screen definitions comes from String.Format: various pieces of data can be inserted anywhere into the text. For a quick reference of 
how String.Format works and some examples you can see [this handy blog post](http://blog.stevex.net/string-formatting-in-csharp/). An example:

    Altitude is {0:##0.00} $&$ ALTITUDE

The special sequence of symbols "*$&$*" separates the text to be printed from a space-separated list of variables to be inserted into the format specifiers on the line.

### Known variables

Boy, this list got long.

