using System;
using System.Collections.Generic;
using System.Text;

namespace JSI
{
    /// <summary>
    /// JSIGimbal provides an interface for gimbals
    /// </summary>
    class JSIGimbal : IJSIModule
    {
        bool gimbalLockState = false;

        public JSIGimbal(Vessel _vessel) : base(_vessel) { }

        /// <summary>
        /// Refresh the current state of gimbal locks
        /// </summary>
        private void UpdateGimbalLockState()
        {
            moduleInvalidated = false;
            gimbalLockState = false;

            if (vessel == null)
            {
                return; // early
            }

            foreach (ModuleGimbal gimbal in FindActiveStageGimbals(vessel))
            {
                if (gimbal.gimbalLock)
                {
                    gimbalLockState = true;
                    break;
                }
            }

        }

        /// <summary>
        /// Locks / unlocks gimbals on the currently-active stage.
        /// </summary>
        /// <param name="state"></param>
        public void GimbalLock(bool state)
        {
            foreach (ModuleGimbal gimbal in FindActiveStageGimbals(vessel))
            {
                gimbal.gimbalLock = state;
            }
        }

        /// <summary>
        /// Returns true if at least one gimbal on the active stage is locked.
        /// </summary>
        /// <returns></returns>
        public bool GimbalLockState()
        {
            if (moduleInvalidated)
            {
                UpdateGimbalLockState();
            }

            return gimbalLockState;
        }

        /// <summary>
        /// Iterator to find gimbals on active stages
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private static IEnumerable<ModuleGimbal> FindActiveStageGimbals(Vessel vessel)
        {
            foreach (Part thatPart in vessel.parts)
            {
                // MOARdV: I'm not sure inverseStage is ever > CurrentStage,
                // but there's no harm in >= vs ==.
                if (thatPart.inverseStage >= Staging.CurrentStage)
                {
                    foreach (PartModule pm in thatPart.Modules)
                    {
                        if (pm is ModuleGimbal)
                        {
                            yield return pm as ModuleGimbal;
                        }
                    }
                }
            }
        }
    }
}
