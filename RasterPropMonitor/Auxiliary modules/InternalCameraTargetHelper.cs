namespace JSI
{
	public class InternalCameraTargetHelper: InternalModule
	{
		private ITargetable target;
		private bool needsRestoring;

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel))
				return;

			if (needsRestoring && target != null && FlightGlobals.fetch.VesselTarget == null) {
				FlightGlobals.fetch.SetVesselTarget(target);
			} else
				target = FlightGlobals.fetch.VesselTarget;
			needsRestoring = false;
		}

		public void LateUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel))
				return;

			needsRestoring |= Mouse.Left.GetDoubleClick();
		}
	}
}

