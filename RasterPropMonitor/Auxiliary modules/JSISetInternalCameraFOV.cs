/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System.Collections.Generic;
using UnityEngine;

namespace JSI
{
    public class JSISetInternalCameraFOV : InternalModule
    {
        public enum HideKerbal
        {
            none,
            head,
            all
        };

        private readonly List<SeatCamera> seats = new List<SeatCamera>();
        private int oldSeat = -1;

        private struct SeatCamera
        {
            public float fov;
            public float maxRot;
            public float maxPitch;
            public float minPitch;
            public HideKerbal hideKerbal;
        }

        private const float defaultFov = 60f;
        private const float defaultMaxRot = 60f;
        private const float defaultMaxPitch = 60f;
        private const float defaultMinPitch = -30f;
        private const HideKerbal defaultHideKerbal = HideKerbal.none;

        public void Start()
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("INTERNAL"))
            {
                if (node.GetValue("name") == internalModel.internalName)
                {
                    foreach (ConfigNode moduleConfig in node.GetNodes("MODULE"))
                    {
                        // The order we get should in theory match the order of seats, shouldn't it.
                        if (moduleConfig.HasValue("name") && moduleConfig.GetValue("name") == "InternalSeat")
                        {
                            var seatData = new SeatCamera();
                            seatData.fov = moduleConfig.GetFloat("fov") ?? defaultFov;
                            seatData.maxRot = moduleConfig.GetFloat("maxRot") ?? defaultMaxRot;
                            seatData.maxPitch = moduleConfig.GetFloat("maxPitch") ?? defaultMaxPitch;
                            seatData.minPitch = moduleConfig.GetFloat("minPitch") ?? defaultMinPitch;
                            seatData.hideKerbal = HideKerbal.none;

                            if (moduleConfig.HasValue("hideKerbal"))
                            {
                                string hideKerbalVal = moduleConfig.GetValue("hideKerbal");
                                if (hideKerbalVal == HideKerbal.head.ToString())
                                {
                                    seatData.hideKerbal = HideKerbal.head;
                                }
                                else if (hideKerbalVal == HideKerbal.all.ToString())
                                {
                                    seatData.hideKerbal = HideKerbal.all;
                                }
                            }

                            seats.Add(seatData);
                            JUtil.LogMessage(this, "Setting per-seat camera parameters for seat {0}: fov {1}, maxRot {2}, maxPitch {3}, minPitch {4}, hideKerbal {5}",
                                seats.Count - 1, seatData.fov, seatData.maxRot, seatData.maxPitch, seatData.minPitch, seatData.hideKerbal.ToString());
                        }
                    }
                }
            }
            GameEvents.OnCameraChange.Add(OnCameraChange);
            GameEvents.OnIVACameraKerbalChange.Add(OnIVACameraChange);
            // Pseudo-seat with default values.
            seats.Add(new SeatCamera
            {
                fov = defaultFov,
                maxRot = defaultMaxRot,
                maxPitch = defaultMaxPitch,
                minPitch = defaultMinPitch,
                hideKerbal = HideKerbal.none
            });

            // If (somehow) we start in IVA, make sure we initialize here.
            if (JUtil.UserIsInPod(part) && InternalCamera.Instance != null && InternalCamera.Instance.isActive && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
            {
                Kerbal activeKerbal = part.FindCurrentKerbal();
                int seatID;
                if (activeKerbal == null)
                {
                    seatID = -1;
                }
                else
                {
                    seatID = activeKerbal.protoCrewMember.seatIdx;
                }

                UpdateCameras(seatID, activeKerbal);
            }
        }

        /// <summary>
        /// Unregister those callbacks
        /// </summary>
        public void OnDestroy()
        {
            GameEvents.OnIVACameraKerbalChange.Remove(OnIVACameraChange);
            GameEvents.OnCameraChange.Remove(OnCameraChange);
        }

        /// <summary>
        /// If the camera mode changes, we need to reset our local cache.
        /// </summary>
        /// <param name="newMode"></param>
        private void OnCameraChange(CameraManager.CameraMode newMode)
        {
            if (newMode == CameraManager.CameraMode.IVA)
            {
                Kerbal activeKerbal = part.FindCurrentKerbal();
                if (activeKerbal != null)
                {
                    int seatID = activeKerbal.protoCrewMember.seatIdx;
                    if (seatID != oldSeat)
                    {
                        UpdateCameras(seatID, activeKerbal);
                    }
                }
            }
            else
            {
                oldSeat = -1;
            }
        }

        /// <summary>
        /// Take care of updating everything.
        /// </summary>
        /// <param name="seatID"></param>
        /// <param name="activeKerbal"></param>
        private void UpdateCameras(int seatID, Kerbal activeKerbal)
        {
            InternalCamera.Instance.SetFOV(seats[seatID].fov);
            InternalCamera.Instance.maxRot = seats[seatID].maxRot;
            InternalCamera.Instance.maxPitch = seats[seatID].maxPitch;
            InternalCamera.Instance.minPitch = seats[seatID].minPitch;

            RPMVesselComputer comp = null;
            if (RPMVesselComputer.TryGetInstance(vessel, ref comp))
            {
                comp.SetKerbalVisible(activeKerbal, seats[seatID].hideKerbal);
            }

            oldSeat = seatID;
        }

        /// <summary>
        /// Callback when the player switches IVA camera.
        /// 
        /// BUG: The callback's parameter tells me who the
        /// previous Kerbal was, not who the new Kerbal is.
        /// </summary>
        /// <param name="newKerbal"></param>
        private void OnIVACameraChange(Kerbal newKerbal)
        {
            // Unfortunately, the callback is telling me who the previous Kerbal was,
            // not who the new Kerbal is.
            Kerbal activeKerbal = part.FindCurrentKerbal();
            if (activeKerbal != null)
            {
                int seatID = activeKerbal.protoCrewMember.seatIdx;
                if (seatID != oldSeat)
                {
                    UpdateCameras(seatID, activeKerbal);
                }
            }
        }
    }
}
