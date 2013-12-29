using System.Collections.Generic;
using UnityEngine;

namespace JSI
{
	public class JSISetInternalCameraFOV: InternalModule
	{
		private readonly List<SeatCamera> seats = new List<SeatCamera>();
		private int oldSeat = -1;

		private struct SeatCamera
		{
			public float fov;
			public float maxRot;
			public float maxPitch;
			public float minPitch;
		}

		private const float defaultFov = 60f;
		private const float defaultMaxRot = 60f;
		private const float defaultMaxPitch = 60f;
		private const float defaultMinPitch = -30f;

		public void Start()
		{
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("INTERNAL")) {
				if (node.GetValue("name") == internalModel.internalName) {
					foreach (ConfigNode moduleConfig in node.GetNodes("MODULE")) {
						// The order we get should in theory match the order of seats, shouldn't it.
						if (moduleConfig.HasValue("name") && moduleConfig.GetValue("name") == "InternalSeat") {
							var seatData = new SeatCamera();
							seatData.fov = moduleConfig.GetFloat("fov") ?? defaultFov;
							seatData.maxRot = moduleConfig.GetFloat("maxRot") ?? defaultMaxRot;
							seatData.maxPitch = moduleConfig.GetFloat("maxPitch") ?? defaultMaxPitch;
							seatData.minPitch = moduleConfig.GetFloat("minPitch") ?? defaultMinPitch;
							seats.Add(seatData);
							JUtil.LogMessage(this, "Setting per-seat camera parameters for seat {0}: fov {1}, maxRot {2}, maxPitch {3}, minPitch {4}",
								seats.Count - 1, seatData.fov, seatData.maxRot, seatData.maxPitch, seatData.minPitch);
						}
					}
				}
			}
			// Pseudo-seat with default values.
			seats.Add(new SeatCamera {
				fov = defaultFov,
				maxRot = defaultMaxRot,
				maxPitch = defaultMaxPitch,
				minPitch = -defaultMinPitch
			});
		}

		public override void OnUpdate()
		{
			if (JUtil.VesselIsInIVA(vessel) && InternalCamera.Instance != null && InternalCamera.Instance.isActive && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) {
				int seatID = part.CurrentActiveSeat();
				if (seatID < 0)
					seatID = seats.Count - 1;
				if (seatID != oldSeat) {
					InternalCamera.Instance.SetFOV(seats[seatID].fov);
					InternalCamera.Instance.maxRot = seats[seatID].maxRot;
					InternalCamera.Instance.maxPitch = seats[seatID].maxPitch;
					InternalCamera.Instance.minPitch = seats[seatID].minPitch;
				}
				oldSeat = seatID;

				/* Figuring out an appropriate key combination is proving nontrivial.
				if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.Z)) {
					part.FindCurrentKerbal().ReseatKerbalInPart();
				}*/

			}
		}
	}
}

