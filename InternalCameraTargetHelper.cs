using System;
using UnityEngine;

namespace RasterPropMonitor
{
	public class InternalCameraTargetHelper: InternalModule
	{
		ITargetable target;

		// Problem: You wish to use an InternalCameraSwitch for docking.
		// To activate this camera, you need to doubleclick on something.
		// Unfortunately, doubleclick resets your camera, and you can't doubleclick again
		// to re-target.
		// Solution:
		// Insert MODULE {name = InternalCameraTargetHelper} into your internal.cfg
		// Problem gone. :)

		public override void OnUpdate ()
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
				FlightGlobals.fetch.SetVesselTarget (target);

		}
	}
}

