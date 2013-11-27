// Mental note:
// It is not entirely clear which .NET version should KSP plugins be compiled for,
// but the consensus is that 3.5 is the most appropriate because types introduced
// in 4.0 can be verified not to work. It is a fact that you can use C#4 itself
// with it with no ill effects, though -- at least all the features which rely
// on the compiler, rather than on the libraries.
// SCANsat is compiled for .NET 4.0 for some reason, which means that
// this assembly also needs to be compiled for 4.0 to link to it. Which can and probably will
// cause problems.
// I wish there were some clarity on the subject.
using SCANsat;
using UnityEngine;

namespace SCANsatRPM
{
	public class JSISCANsatRPM: InternalModule
	{

		[KSPField]
		public float screenAspect = 1;
		[KSPField]
		public int screenWidth;
		[KSPField]
		public int screenHeight;
		[KSPField]
		public int refreshRate = 3600;

		private const int mapMode = 1;

		private int refreshCountdown;

		private SCANmap mapGenerator;
		private CelestialBody mapBody;

		public bool MapRenderer(RenderTexture screen)
		{
			// Just in case.
			if (!HighLogic.LoadedSceneIsFlight)
				return false;

			Graphics.Blit(mapGenerator.map, screen);
			return true;
		}

		private void CheckOrbitingBody(){
			if (mapBody != vessel.mainBody) {
				mapBody = vessel.mainBody;
				mapGenerator.resetMap(mapMode);
				mapGenerator.setBody(mapBody);
			}
		}

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!mapGenerator.isMapComplete())
				mapGenerator.getPartialMap();

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
				CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (UpdateCheck()) {
				mapGenerator.resetMap(mapMode);
			}
		}

		private bool UpdateCheck()
		{
			refreshCountdown--;
			if (refreshCountdown <= 0) {
				refreshCountdown = refreshRate;
				return true;
			}
			return false;
		}

		public void Start()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			mapGenerator = new SCANmap();
			mapGenerator.setSize(screenWidth, screenHeight);
			mapGenerator.setProjection(SCANmap.MapProjection.Rectangular);
			CheckOrbitingBody();
		}
	}
}

