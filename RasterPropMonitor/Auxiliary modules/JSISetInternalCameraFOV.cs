
namespace JSI
{
	public class JSISetInternalCameraFOV: InternalModule
	{
		[KSPField]
		public float fov = 60f;
		private CameraManager.CameraMode oldCameraMode;

		public override void OnUpdate()
		{
			if (JUtil.VesselIsInIVA(vessel) && CameraManager.Instance.currentCameraMode != oldCameraMode && InternalCamera.Instance.isActive) {
				InternalCamera.Instance.SetFOV(fov);
				oldCameraMode = CameraManager.Instance.currentCameraMode;
			}
		}
	}
}

