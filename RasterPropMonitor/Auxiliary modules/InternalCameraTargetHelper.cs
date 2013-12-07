namespace JSI
{
	public class InternalCameraTargetHelper: InternalModule
	{
		private ITargetable target;
		private bool needsRestoring;

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (needsRestoring && target != null && FlightGlobals.fetch.VesselTarget == null) {
				FlightGlobals.fetch.SetVesselTarget(target);
				needsRestoring = false;
			}
			target = FlightGlobals.fetch.VesselTarget;
		}

		public void LateUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			needsRestoring |= Mouse.Left.GetDoubleClick();
		}
	}
}

