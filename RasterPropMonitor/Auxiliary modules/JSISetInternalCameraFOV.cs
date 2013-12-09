
namespace JSI
{
	public class JSISetInternalCameraFOV: InternalModule
	{
		[KSPField]
		public float fov = 60f;
		private CameraManager.CameraMode oldCameraMode;

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;
			if (CameraManager.Instance.currentCameraMode != oldCameraMode && InternalCamera.Instance.isActive) {
				InternalCamera.Instance.SetFOV(fov);
				oldCameraMode = CameraManager.Instance.currentCameraMode;
			}
		}
	}
}

