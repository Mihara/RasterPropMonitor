
namespace JSI
{
	public class JSISetInternalCameraFOV: InternalModule
	{
		[KSPField]
		public float fov = 60f;
		[KSPField]
		public float maxRot = 60f;
		[KSPField]
		public float maxPitch = 60f;
		[KSPField]
		public float minPitch = -30f;
		private CameraManager.CameraMode oldCameraMode;

		public override void OnUpdate()
		{
			if (JUtil.VesselIsInIVA(vessel) && CameraManager.Instance.currentCameraMode != oldCameraMode && InternalCamera.Instance.isActive) {
				InternalCamera.Instance.SetFOV(fov);
				InternalCamera.Instance.maxRot = maxRot;
				InternalCamera.Instance.maxPitch = maxPitch;
				InternalCamera.Instance.minPitch = minPitch;
				oldCameraMode = CameraManager.Instance.currentCameraMode;
			}
		}
	}
}

