//#define EXAMPLE
#if EXAMPLE

/*

This is an example of getting at screen module and it's parameters using reflection. 
This code snippet remains here for the benefit of others who need to do something like this in
KSP and for possible attempts at integration with RasterPropMonitor.
Although I expect that in the future you will not need to drive a RasterPropMonitor directly,
because a RasterPropMonitorGenerator will accept text and graphical pages for display and take
care of switching pages for you just like it does now.

Since the whole raster screen project started as an attempt to get a Firespitter MFD to show
what I wanted, I tried to duplicate what Snjo's own FSmonitorInterface class did
to address the screen in my own plugin, without actually duplicating the screen
module itself. That did not work -- KSP loads assemblies in an alphabetical
order, and aggressively checks them for using types that are not currently in memory,
so if your assembly referencing classes from a different assembly loads
earlier than the classes being referenced, loading will simply fail.

The solution was getting at the module using reflection. This example encapsulates the whole
messy syntax as it translates into using RasterPropMonitor in a neat little class that you
can just use. If you're trying to get at the guts of something elaborate and complex like
MechJeb, making a class like that would be the only practical way to go, although you obviously
can get away with much less.

If you wish to drive a RasterPropMonitor screen directly, copying this class,
or at least looking at it and replicating what it does is what you need to do.
I shall try to maintain it in working order in the future versions.
*/

using System;
using System.Reflection;
using UnityEngine;

// You obviously want your own namespace here.
namespace ScreenReflectorExample
{
	// I'm assuming you're making an InternalModule.
	// If you aren't, you will need some other way to seacrch the hierarchy
	// for a screen.
	public class MyNewPlugin: InternalModule
	{
		// Our cute little internal class.
		internal class ScreenReflector
		{
			// These are just public fields you might want to read.
			public readonly int Width;
			public readonly int Height;
			// If at any point the constructor fails, Found will remain false.
			public readonly bool Found;
			// This is the pointer to an instance of RasterPropMonitor.
			// We can't say it's name because KSP won't let us,
			// but we can say it's an InternalModule.
			private readonly InternalModule targetModule;
			// These variables allow us to keep direct pointers to methods
			// of the screen.
			// If they were to return a value, we'd use Func<input type,..., output type>
			// This way of addressing them is even wordier than MethodInfo.Invoke, but supposedly much faster,
			// because we avoid certain costly steps incurred by MethodInfo.Invoke.
			private readonly Action <string[]> funcSendPage;
			private readonly Action <string> funcSendCamera;
			private readonly Action <string,float> funcSendCamera2;

			public ScreenReflector(InternalProp thatProp)
			{
				// Just like the RasterPropMonitorGenerator, we search here for an instance of a
				// RasterPropMonitor that lives in the same prop as us.
				try {
					foreach (InternalModule intModule in thatProp.internalModules) {
						// And if we found a class by our name, we can go and shake it down for it's method references.
						if (intModule.ClassName == "RasterPropMonitor") {
							targetModule = intModule;

							// Grab the two variables the screen driver is not supposed to modify anyway.
							Width = (int)intModule.GetType().GetField("screenWidth").GetValue(intModule);
							Height = (int)intModule.GetType().GetField("screenHeight").GetValue(intModule);

							// Now let's get a hold of the methods of this instance.
							foreach (MethodInfo m in intModule.GetType().GetMethods()) {
								switch (m.Name) {
								// There is only one SendPage method.
									case "SendPage":
										funcSendPage = (Action<string[]>)Delegate.CreateDelegate(typeof(Action<string[]>), targetModule, m);
										break;
								// SendCamera method is overloaded, so we tell them apart by the number of arguments.
								// With something like MechJeb you will require more complex logic.
									case "SendCamera":
										if (m.GetParameters().Length == 1)
											funcSendCamera = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), targetModule, m);
										else
											funcSendCamera2 = (Action<string,float>)Delegate.CreateDelegate(typeof(Action<string,float>), targetModule, m);
										break;
								}
							}

							Found = true;
							return;
						}
					}
				} catch {
					Debug.Log("ScreenReflector could not find an instance of RasterPropMonitor. Pretending nothing happened...");
				}
			}
			// If none of the methods we're planning on calling were overloads, we wouldn't even need these proxies,
			// we could call them directly from the method pointers.
			// But this permits us to keep working completely the same regardless of whether we found 
			// a screen to drive or not.
			public void SendPage(string[] page)
			{
				if (Found)
					funcSendPage(page);
			}

			public void SendCamera(string newCameraName)
			{
				if (Found)
					funcSendCamera(newCameraName);
			}

			public void SendCamera(string newCameraName, float newFOV)
			{
				if (Found)
					funcSendCamera2(newCameraName, newFOV);
			}
		}

		ScreenReflector ourScreen;

		public void Start()
		{
			// The important step is instantiating our reflecting class,
			// which will locate the module instance we'll be invoking,
			// and stitch it's own methods to the methods of that instance.
			ourScreen = new ScreenReflector(internalProp);
			if (ourScreen.Found) {
				// We still need some initialisations,
				// but later I'll have the screen just check the array for validity,
				// so you won't have to do this.
				screenData = new string[ourScreen.Height];
				for (int i=0; i<screenData.Length; i++) {
					screenData[i] = string.Empty;
				}
				// Something to show from the start.
				screenData[0] = "ScreenReflector test.";
				// In this example, we'll also request a camera to show.
				// You really don't want to call that one every frame.
				ourScreen.SendCamera("port");
			}

		}

		private int counter = 1;
		private string[] screenData;

		public override void OnUpdate()
		{

			// Only work in the Flight scene.
			if (!HighLogic.LoadedSceneIsFlight)
				return;

			// Do nothing if the player is not inside the IVA or the vessel is not the active vessel.
			if (!((CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal) &&
			    vessel == FlightGlobals.ActiveVessel))
				return;

			// You can, obviously, have a module that only optionally drives the screen if it's present.
			if (ourScreen.Found) {
				screenData[1] = counter.ToString();
				counter++;
				// We're sending a page every frame here. Don't do this in a real plugin.
				ourScreen.SendPage(screenData);
			}
		}
	}
}

#endif