using System;
using UnityEngine;

namespace JSI
{
	public class InternalCameraTargetHelper: InternalModule
	{
		ITargetable target;

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight ||
			    vessel != FlightGlobals.ActiveVessel)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) {
				target = FlightGlobals.fetch.VesselTarget;
			}
			if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal &&
			    FlightGlobals.fetch.VesselTarget == null && target != null)
				FlightGlobals.fetch.SetVesselTarget(target);

		}
	}
}

