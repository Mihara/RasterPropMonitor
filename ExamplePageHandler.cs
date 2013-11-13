//#define EXAMPLE
#if EXAMPLE
// This is a better mechanism to drive a PropMonitor from a foreign plugin,
// because it's what I'm probably going to use myself if I decide to make an onscreen menu.
// It has the advantage of letting you make use of the variable processing and you don't
// need to copy a page of boilerplate either -- instead, you just need an InternalModule
// with a method that returns a string. That's it.
//
// RasterPropMonitorGenerator module can, beyond the usual types of page read from a text file,
// and defined directly in the config file, request a page text from an InternalModule
// living in the same prop as it does. To do this, configure the page like this:
//
// page1 = ExamplePageHandler,ExamplePage
//
// and then make the prop load the module:
// MODULE {
//     name = ExamplePageHandler
// }
//
// With that configuration, RasterPropMonitorGenerator will look inside the prop for a 
// module named "ExamplePageHandler" and attempt to delegate a function call to
// it's "ExamplePage" method. That method must return a string and take no parameters.
// Environment.NewLine is the linebreak. The return string will be processed
// exactly like RasterPropMonitorGenerator's own page definition file,
// so you can use every variable and String.Format to place the data where you want it.

using System;
using UnityEngine;

// You obviously want your own namespace.
namespace YourFavouriteNamespace
{
	// It needs to be InternalModule because that's what the monitor is looking for and because of where it lives.
	public class ExamplePageHandler: InternalModule
	{
		// Your screen buffer. Technically you don't have to have one.
		private string[] screen = new string[10];

		// This method will be found by RasterPropMonitorGenerator and called to provide a page.
		// You must return a string. Environment.Newline is the carriage return, nothing fancy.
		public string ExamplePage()
		{
			return string.Join(Environment.NewLine, screen);
		}

		public void Start()
		{
			// I honestly have no clue why InternalModules need to be initialised
			// like this, and not with a proper constructor or an OnAwake, but that works.
			// Even a very simple OnAwake apparently doesn't and causes the entire IVA to choke.
			screen[0] = "This is our first line.";
			screen[1] = "This is our second line.";
			screen[2] = "This calls for a variable: {0} $&$ ALTITUDE"; 
		}

		public override void OnUpdate()
		{
			// In case of a menu handler, you won't need to do anything here, 
			// since your button handlers will take care of it.
			// But who knows what you might want to cook up...
		}
	}
}
#endif