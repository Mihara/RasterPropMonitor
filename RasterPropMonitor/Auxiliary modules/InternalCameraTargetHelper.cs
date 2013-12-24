namespace JSI
{
	public class InternalCameraTargetHelper: InternalModule
	{
		private ITargetable target;
		private bool needsRestoring;
		private static Part referencePart;
		private bool wasOutsideOnPreviousFrame = true;

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel)) {
				wasOutsideOnPreviousFrame = true;
				return;
			}

			// Restoring target.
			if (needsRestoring && target != null && FlightGlobals.fetch.VesselTarget == null) {
				FlightGlobals.fetch.SetVesselTarget(target);
			} else {
				target = FlightGlobals.fetch.VesselTarget;
			}
			needsRestoring = false;

			// Restoring reference.
			if (wasOutsideOnPreviousFrame && referencePart != null && referencePart.vessel == vessel) {
				referencePart.MakeReferencePart();
			}
			referencePart = vessel.GetReferenceTransformPart();
			wasOutsideOnPreviousFrame = false;
		}

		public void LateUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel))
				return;

			needsRestoring |= Mouse.Left.GetDoubleClick();
		}
	}
}

