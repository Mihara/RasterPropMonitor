## Usage recommendations

Please, if you use this plugin to create a screen of your own, distribute a 
copy of the plugin in this fashion with your mod package:

* GameData\YourGamedataDirectory\Whatever
* GameData\JSI\RasterPropMonitor\Plugins\RasterPropMonitor.dll

and include, in whichever readme file you distribute, the link to this
GitHub repository:

https://github.com/Mihara/RasterPropMonitor/

## Creating a display model for RasterPropMonitor

1. You need a font bitmap. The plugin treats fonts as if they were fixed
   width, so it's best to take a fixed width font to start with.
   [CBFG](http://www.codehead.co.uk/cbfg/) works, and 
   [Fixedsys Excelsior](http://www.fixedsysexcelsior.com/) is a nice font,
   but there are other programs to do the same thing and probably fonts 
   more suitable to your taste.
   
   Every letter is assumed to occupy a block of **fontLetterWidth** by
   fontLetterHeight pixels on the bitmap. Font texture size must be
   evenly divisible by fontLetterWidth/fontLetterHeight respectively.
   For Unity reasons, the font bitmap has to have sizes that are a power
   of 2, but it doesn't have to be square. Characters are read from the font
   left to right, top to bottom, with the bottom left character being
   character number 32 (space). Characters 128-159 are skipped due to
   peculiarities of how KSP treats strings.
   
2. You need a model for your screen. If it's to have buttons, they need to
   be named transforms with isTrigger on them enabled. The screen must be
   a named transform, arranged in such a way that the texture's 0,1
   coordinates are the top left corner of the screen.
   It must already have a texture in the layer ("\_MainTex", "\_Emissive",
   etc) that the plugin will replace. To save memory, that placeholder
   texture should be the minimum size possible, which for KSP appears
   to be 32x32 pixels.
   
## Configuring a monitor

Monitors are created using two modules: **RasterPropMonitor** and
**RasterPropMonitorGenerator** in a prop configuration file.
RasterPropMonitor takes care of the display, while
RasterPropMonitorGenerator feeds it with the data to display. It is perfectly
feasible to write your own plugin to completely replace
RasterPropMonitorGenerator, *(see source for helpful comments)* and do things
your way, but you probably don't want to.

### RasterPropMonitor configuration

* **screenTransform** -- the name of the screen object.
* **textureLayerID** -- Unity name of a texture ID in the object's material
  that the screen will be printed on. Defaults to "_MainTex".
* **fontTransform** -- Where to get the font bitmap. You can either place a
  texture somewhere in GameData and refer to it exactly like you would in a
  MODEL configuration node *(KSP reads in everything that looks like a texture
  and is stored outside of a PluginData directory, and assigns it an URL)*
  or put the texture on a model transform and give it's name. 
* **blankingColor** -- R,G,B,A of a color that will be used to blank out a
  screen between refreshes.
* **screenWidth**/**screenHeight** -- Number of characters in a line and number
  of lines.
* **screenPixelWidth**/**screenPixelHeight** -- Width and height of the texture
  to be generated for the screen.
* **fontLetterWidth**/**fontLetterHeight** -- Width and height of a font cell
  in pixels.

Letters are printed on the screen in pixel-perfect mapping, so one pixel of a
font texture will always correspond to one pixel of the generated screen
texture -- as a result, you can have less characters in a line than would
fit into screenPixelWidth, but can't have more.

### RasterPropMonitorGenerator configuration

* **refreshRate** -- The screen will be redrawn no more often than once this
  number of frames.
* **refreshDataRate** -- Various computationally intensive tasks will be
  performed no more often than once this number of frames.
* **page1,page2...page8** -- Page definitions.
* **button1,button2...button8** -- Button transform names that correspond to
  pages.

You need to have at least one page (page1). Clicking on button2 will cause
page2 to be rendered, etc. If there is a button2 option, but no page2 defined,
the screen will be blanked.

Pages can be defined in one of two ways -- by referencing a text file that
contains a complete screen definition, or directly in the page parameter.
You really want to use the text file, unless your line is particularly short.
Text file reference is just like a texture URL, the only difference is that
it must have a file extension.

If you wish to insert a line break in the screen definition written directly
in a prop config file, you need to replace it with "**$$$**". If you wish to
use the { and } format string characters in such a screen definition, you
need to replace **{** with **<=** and **}** with **=>**, because KSP mangles
them upon reading from prop.cfg files.

### Screen definitions

Screen definitions are normal text files in UTF-8 encoding, lines are
separated by normal line break characters. The real power of screen
definitions comes from String.Format: various pieces of data can be inserted
anywhere into the text. For a quick reference of how String.Format works and
some examples you can see
[this handy blog post](http://blog.stevex.net/string-formatting-in-csharp/).
An example:

    Altitude is {0:##0.00} $&$ ALTITUDE

The special sequence of symbols "**$&$**" separates the text to be printed
from a space-separated list of variables to be inserted into the format
specifiers on the line. It might not be very obvious, but the first character
of the {} format specifier is the index of the variable in the list, starting
with 0.

While debugging your screen definition, it helps to know that the plugin
reloads screen definitions from disk, *(the ones stored in files, at
least)* every time it is instantiated, which is every time the vessel is
loaded -- which happens if you go back to the space center and return, or
even simply switch to an out of range vessel and back.

### Known variables

Boy, this list got long. 

I am warning you that my understanding of the mathematics involved is
practically nonexistent. If any parameter isn't what you expect it should
be, please detail in what way and if possible, what should I do to fix it.

#### Speeds

* **VERTSPEED** -- Vertical speed in m/s.
* **SURFSPEED** -- Surface speed in m/s.
* **ORBTSPEED** -- Orbital speed in m/s.
* **TRGTSPEED** -- Speed relative to target in m/s.
* **HORZVELOCITY** -- Horizontal component of surface velocity in m/s.
* **TGTRELX**, **TGTRELY**, **TGTRELZ**, -- Components of speed relative to
  target, in m/s.

#### Altitudes

* **ALTITUDE** -- Altitude above sea level in meters.
* **RADARALT** -- Altitude above the ground in meters.

#### Masses

* **MASSDRY** -- Dry mass of the ship, i.e. excluding resources.
* **MASSWET** -- Total mass of the ship.

#### Thrust and related parameters

None of these parameters know anything about vectors and orientations, mind.

* **THRUST** -- Total amount of thrust currently produced by the engines.
* **THRUSTMAX** -- Maximum amount of thrust the currently enabled engines
  can produce. 
* **TWR** -- Thrust to weight relative to the body currently being orbited
  calculated from the current throttle level.
* **TWRMAX** -- TWR you would get at full throttle.
* **ACCEL** -- Current acceleration in m/s^2
* **MAXACCEL** -- Maximum acceleration in m/s^2
* **GFORCE** -- G forces being experienced by the vessel in g.

#### Maneuver node

* **MNODETIME** -- time until/after the current maneuver node. Due to
  peculiarities of Kerbal calendar this, as well as other timespans, is
  returned as a pre-formatted string in the vein of
  <sign><number of whole years>:<number of whole days>:<hours>:<minutes>:<seconds>.<10ths of a second>.
  MNODETIME and TIMETOPE/TARGETTIMETOPE are the only ones that come
  with a sign.
* **MNODEDV** -- Delta V remaining in the current maneuver node.

#### Orbit parameters

* **ORBITBODY** -- Name of the body we're orbiting.
* **PERIAPSIS** -- Periapsis of the current orbit in meters.
* **APOAPSIS** -- Periapsis of the current orbit in meters.
* **INCLINATION** -- Inclination of the current orbit in degrees.
* **ECCENTRICITY** -- Eccentricity of the current orbit.
* **ORBPERIOD** -- Period of the current orbit, a formatted timespan.
* **TIMETOAP** -- Time to apoapsis, a formatted timespan.
* **TIMETOPE** -- Time to periapsis, a formatted timespan.

#### Time

* **UT** -- Universal time.
* **MET** -- Mission Elapsed Time.

#### Names

* **NAME** -- Name of the current vessel.
* **CREW_**<*id*>**_**<**FULL**|**FIRST**|**LAST**> -- Names of crewmembers.
  IDs start with 0. I.e. for Jebediah Kerman being the only occupant of a
  capsule, CREW_0_FIRST will produce "Jebediah". An empty string if the seat
  is unoccupied.
* **TARGETNAME** -- Name of the target.

#### Coordinates

* **LATITUDE** -- Latitude of the vessel in degrees. Negative is south.
* **LONGITUDE** -- Longitude of the vessel in degrees. Negative is west.
* **LATITUDE_DMS**,**LONGITUDE_DMS** -- Same, but as a string converted
  to degrees, minutes and seconds.
* **LATITUDETGT**, **LONGITUDETGT**, **LATITUDETGT_DMS**, 
  **LONGITUDETGT_DMS** -- Same as above, but of a target vessel.

#### Orientation

* **HEADING**, **PITCH**, **ROLL** -- should be obvious.

#### Rendezvous and docking

* **TARGETDISTANCE** -- Distance to the target in meters.
* **TARGETDISTANCEX**, **TARGETDISTANCEY**, **TARGETDISTANCEZ** -- Distance
  to the target separated by axis.
* **RELATIVEINCLINATION** -- Relative inclination of the target orbit.
* **TARGETANGLEX**, **TARGETANGLEY**, **TARGETANGLEZ** -- Angles between axes
  of the capsule and a target docking port.
* **TARGETAPOAPSIS**, **TARGETPERIAPSIS**, **TARGETINCLINATION**, 
  **TARGETECCENTRICITY**,  **TARGETORBITALVEL**,  **TARGETIMETOAP**,
  **TARGETORBPERIOD**,  **TARGETTIMETOPE**,  **TARGETTIMETOAP** -- parameters
  of the target's orbit, if one exists. Same considerations as for the
  vessel's own orbital parameters apply.

#### Resources

Notice that resource quantities are rounded down to 0.01, because otherwise
they never become properly zero. If your resource requires a more fine grained
measurement, poke me and we'll talk about it.

* **ELECTRIC**, **ELECTRICMAX** -- Current and maximum quantity
  of ElectricCharge.
* **FUEL**, **FUELMAX** -- Same for LiquidFuel
* **OXIDIZER**, **OXIDIZERMAX** -- Same for Oxidizer
* **MONOPROP**, **MONOPROPMAX** -- Same for MonoPropellant
* **XENON**, **XENONMAX** -- Same for XenonGas

An alphabetically sorted list of all resources present in the craft is
available as well:

* **LISTR_**<*id*>**_**<**NAME**|**VAL**|**MAX**> -- where id's start with 0,
VAL is the current value and MAX is the total storable quantity, so
LISTR_0_NAME is the name of the first resource in an alphabetically
sorted list.

#### Miscellanneous

* **STAGE** -- Number of current stage.
* **SCIENCEDATA** -- Amount of science data in Mits stored in the
  entire vessel.
* **GEAR**, **BRAKES**, **SAS**, **LIGHTS**, **RCS** -- Status of the said
  systems returned as 1 if they are turned on and 0 if they are turned off.
  To format it in a smooth fashion, use a variation on {0:on;;OFF}
  format string.

Whew, that's about all of them.

### InternalCameraTargetHelper

A helper module included in the plugin. 

**Problem**: You wish to use an InternalCameraSwitch for docking. 
To activate this camera, you need to doubleclick on something. Unfortunately, 
doubleclick resets your target, and you can't doubleclick again to re-target,
since the camera switched to by InternalCameraSwitch won't let you.

**Solution**: Insert

    MODULE {
	    name = InternalCameraTargetHelper
	}
	
into your internal.cfg. Problem gone. :)
