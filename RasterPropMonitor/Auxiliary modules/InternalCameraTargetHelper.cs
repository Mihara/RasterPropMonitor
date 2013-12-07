namespace JSI
{
	public class InternalCameraTargetHelper: InternalModule
	{
		private ITargetable target;
		private CameraManager.CameraMode previousCameraMode;

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (CameraManager.Instance.currentCameraMode != previousCameraMode) {
				if (FlightGlobals.fetch.VesselTarget == null && target != null)
					FlightGlobals.fetch.SetVesselTarget(target);

			}

			target = FlightGlobals.fetch.VesselTarget;
			previousCameraMode = CameraManager.Instance.currentCameraMode;
		}
	}
}

