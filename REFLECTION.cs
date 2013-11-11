/*

		internal class ScreenReflector
		{
			// This is an example of getting at screen module and it's parameters using reflection.
			// While I'm no longer doing this myself, (I needed to simplify things and it's 
			// self-educational purpose has been served) this code snippet remains here for
			// the benefit of others who go this way and for possible attempts at integration.
			//
			// Since the whole screen project started as an attempt to get a Firespitter MFD to show
			// what I wanted, I tried to duplicate what Snjo's own FSmonitorInterface class did
			// to address the screen in my own plugin, without actually duplicating the screen
			// module itself. That did not work -- KSP loads assemblies in an alphabetical
			// order, so if your assembly referencing classes from a different assembly loads
			// earlier than the classes being referenced, loading will simply fail.
			//
			// The solution was getting at the required fields (and I really should convert this mess
			// to methods, which I'll be doing over time) by using reflection.
			// This internal class is a neat encapsulation of the whole mechanism.
			// It will eventually grow out of date, but by then I'll have explanations how to call
			// methods in this manner instead, these should be no trouble to keep unchanging.
			//
			// If you wish to drive a RasterPropMonitor screen directly, copying out this class,
			// or at least looking at it and replicating what it does, is what you need to do.

			private readonly InternalModule targetModule;
			private FieldInfo rmArray;
			private FieldInfo rmFlag;
			private FieldInfo rmCameraName;
			private FieldInfo rmCameraFlag;
			private FieldInfo rmCameraFov;
			private const float defaultFOV = 60f;

			public int width { get; set; }

			public int height { get; set; }

			public ScreenReflector(InternalProp thatProp)
			{
				foreach (InternalModule intModule in thatProp.internalModules) {
					if (intModule.ClassName == "RasterPropMonitor") {
						targetModule = intModule;
						// These are for text.
						rmArray = intModule.GetType().GetField("screenText");
						rmFlag = intModule.GetType().GetField("screenUpdateRequired");
						// And these are to tell which camera to show on the background.
						rmCameraName = intModule.GetType().GetField("cameraName");
						rmCameraFlag = intModule.GetType().GetField("setCamera");
						rmCameraFov = intModule.GetType().GetField("fov");

						width = (int)intModule.GetType().GetField("screenWidth").GetValue(intModule);
						height = (int)intModule.GetType().GetField("screenHeight").GetValue(intModule);
						break;
					}
				}
				if (targetModule == null)
					throw new ArgumentException("RasterPorpMonitor module was not found.");
			}

			public void SendPage(string[] screenData)
			{
				rmArray.SetValue(targetModule, screenData);
				rmFlag.SetValue(targetModule, true);
			}

			public void SendCamera(string cameraName)
			{
				SendCamera(cameraName, defaultFOV);
			}

			public void SendCamera(string cameraName, float fov)
			{
				if (fov == 0)
					fov = defaultFOV;
				rmCameraFov.SetValue(targetModule, fov);
				rmCameraName.SetValue(targetModule, cameraName);
				rmCameraFlag.SetValue(targetModule, true);
			}
		}

*/