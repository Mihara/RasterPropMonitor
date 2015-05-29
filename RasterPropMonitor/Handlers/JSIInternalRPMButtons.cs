using System;
using System.Linq;

namespace JSI
{
    /// <summary>
    /// Provides a built-in plugin to execute tasks that can be done in the
    /// core RPM without plugin assistance.
    /// </summary>
    public class JSIInternalRPMButtons : InternalModule
    {
        /// <summary>
        /// Turns on the flowState for all resources on the ship.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonActivateReserves(bool state)
        {
            foreach (Part thatPart in vessel.parts)
            {
                foreach (PartResource resource in thatPart.Resources)
                {
                    resource.flowState = true;
                }
            }
        }

        /// <summary>
        /// Indicates whether any onboard resources have been flagged as
        /// reserves by switching them to "not usable".
        /// </summary>
        /// <returns></returns>
        public bool ButtonActivateReservesState()
        {
            foreach (Part thatPart in vessel.parts)
            {
                foreach (PartResource resource in thatPart.Resources)
                {
                    if (!resource.flowState)
                    {
                        // Early return: At least one resource is flagged as a
                        // reserve.
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Clear all maneuver nodes
        /// </summary>
        /// <param name="state"></param>
        // Analysis disable once UnusedParameter
        public void ButtonClearNodes(bool state)
        {
            // patchedConicSolver can be null in early career mode.
            if (vessel.patchedConicSolver != null)
            {
                while (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                {
                    vessel.patchedConicSolver.RemoveManeuverNode(vessel.patchedConicSolver.maneuverNodes.Last());
                }
            }
        }

        /// <summary>
        /// Indicates whether there are maneuver nodes to clear.
        /// </summary>
        /// <returns></returns>
        public bool ButtonClearNodesState()
        {
            if (vessel.patchedConicSolver == null)
            {
                // patchedConicSolver can be null in early career mode.
                return false;
            }

            return (vessel.patchedConicSolver.maneuverNodes.Count > 0);
        }

        /// <summary>
        /// Clear the current target.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonClearTarget(bool state)
        {
            FlightGlobals.fetch.SetVesselTarget((ITargetable)null);
        }

        /// <summary>
        /// Returns whether there are any targets to clear.
        /// </summary>
        /// <returns></returns>
        public bool ButtonClearTargetState()
        {
            return (FlightGlobals.fetch.VesselTarget != null);
        }

        /// <summary>
        /// Toggles engines on the current stage (on/off)
        /// </summary>
        /// <param name="state">"true" for on, "false" for off</param>
        public void ButtonEnableEngines(bool state)
        {
            foreach (Part thatPart in vessel.parts)
            {
                // We accept "state == false" to allow engines that are
                // activated outside of the current staging to be shut off by
                // this function.
                if (thatPart.inverseStage == Staging.CurrentStage || !state)
                {
                    foreach (PartModule pm in thatPart.Modules)
                    {
                        var engine = pm as ModuleEngines;
                        if (engine != null && engine.EngineIgnited != state)
                        {
                            if (state && engine.allowRestart)
                            {
                                engine.Activate();
                            }
                            else if (engine.allowShutdown)
                            {
                                engine.Shutdown();
                            }
                        }
                        var engineFX = pm as ModuleEnginesFX;
                        if (engineFX != null && engineFX.EngineIgnited != state)
                        {
                            if (state && engineFX.allowRestart)
                            {
                                engineFX.Activate();
                            }
                            else if (engineFX.allowShutdown)
                            {
                                engineFX.Shutdown();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Indicates whether at least one engine is enabled.
        /// </summary>
        /// <returns></returns>
        public bool ButtonEnableEnginesState()
        {
            foreach (Part thatPart in vessel.parts)
            {
                foreach (PartModule pm in thatPart.Modules)
                {
                    var engine = pm as ModuleEngines;
                    if (engine != null && engine.allowShutdown && engine.getIgnitionState)
                    {
                        // early out: at least one engine is enabled.
                        return true;
                    }
                    var engineFX = pm as ModuleEnginesFX;
                    if (engineFX != null && engineFX.allowShutdown && engineFX.getIgnitionState)
                    {
                        // early out: at least one engine is enabled.
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Force the SAS mode buttons on the flight view to update when we
        /// update modes under the hood.  Code from
        /// http://forum.kerbalspaceprogram.com/threads/105074-Updating-the-auto-pilot-UI?p=1633958&viewfull=1#post1633958
        /// </summary>
        /// <param name="newMode">The new autopilot mode</param>
        private void ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode newMode)
        {
            // find the UI object on screen
            RUIToggleButton[] SASbtns = FindObjectOfType<VesselAutopilotUI>().modeButtons;
            // set our mode, note it takes the mode as an int, generally top to bottom, left to right, as seen on the screen. Maneuver node being the exception, it is 9
            SASbtns.ElementAt<RUIToggleButton>((int)newMode).SetTrue(true, true);
        }

        /// <summary>
        /// Sets SAS to stability assist mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeStabilityAssist(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.StabilityAssist))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.StabilityAssist);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for Stability Assist</returns>
        public bool ButtonSASModeStabilityAssistState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.StabilityAssist);
        }

        /// <summary>
        /// Sets SAS to prograde mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModePrograde(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Prograde))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Prograde);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Prograde);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for Prograde</returns>
        public bool ButtonSASModeProgradeState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Prograde);
        }

        /// <summary>
        /// Sets SAS to retrograde mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeRetrograde(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Retrograde))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Retrograde);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Retrograde);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for Retrograde</returns>
        public bool ButtonSASModeRetrogradeState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Retrograde);
        }

        /// <summary>
        /// Sets SAS to normal mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeNormal(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Normal))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Normal);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Normal);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for Normal</returns>
        public bool ButtonSASModeNormalState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Normal);
        }

        /// <summary>
        /// Sets SAS to anti normal mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeAntiNormal(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Antinormal))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Antinormal);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Antinormal);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for Antinormal</returns>
        public bool ButtonSASModeAntiNormalState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Antinormal);
        }

        /// <summary>
        /// Sets SAS to radial in mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeRadialIn(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.RadialIn))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.RadialIn);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.RadialIn);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for RadialIn</returns>
        public bool ButtonSASModeRadialInState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.RadialIn);
        }

        /// <summary>
        /// Sets SAS to radial out mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeRadialOut(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.RadialOut))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.RadialOut);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.RadialOut);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for RadialOut</returns>
        public bool ButtonSASModeRadialOutState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.RadialOut);
        }

        /// <summary>
        /// Sets SAS to target mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeTarget(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Target))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Target);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Target);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for Target</returns>
        public bool ButtonSASModeTargetState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Target);
        }

        /// <summary>
        /// Sets SAS to anti target mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeAntiTarget(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.AntiTarget))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.AntiTarget);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.AntiTarget);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for AntiTarget</returns>
        public bool ButtonSASModeAntiTargetState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.AntiTarget);
        }

        /// <summary>
        /// Sets SAS to maneuver mode mode
        /// </summary>
        /// <param name="ignored">Unused</param>
        // Analysis disable once UnusedParameter
        public void ButtonSASModeManeuver(bool ignored)
        {
            if (vessel.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Maneuver))
            {
                vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Maneuver);
                ForceUpdateSASModeToggleButtons(VesselAutopilot.AutopilotMode.Maneuver);
            }
        }

        /// <summary>
        /// Used to check SAS mode.
        /// </summary>
        /// <returns>true if SAS is currently set for Maneuver</returns>
        public bool ButtonSASModeManeuverState()
        {
            return (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Maneuver);
        }

        /**
         * Cycle speed modes (between orbital/surface/target)
         */
        public void ButtonSpeedMode(bool ignored)
        {
            FlightUIController.fetch.cycleSpdModes();
        }

        /**
         * Returns true (really, nothing makes sense for the return value).
         */
        public bool ButtonSpeedModeState()
        {
            return true;
        }

        /**
         * Toggles the staging lock.
         *
         * WARNING: We are using the same string as KSP, so that our lock will
         * interact with the game's lock (alt-L); if an update to KSP changes
         * the name they use, we will have to be updated.
         */
        public void ButtonStageLock(bool state)
        {
            if (state)
            {
                InputLockManager.SetControlLock(ControlTypes.STAGING, "manualStageLock");
            }
            else
            {
                InputLockManager.RemoveControlLock("manualStageLock");
            }
        }

        /**
         * Returns whether staging is locked (disabled).
         */
        public bool ButtonStageLockState()
        {
            return InputLockManager.IsLocked(ControlTypes.STAGING);
        }

        /// <summary>
        /// Cuts throttle (set it to 0) when state is true.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonCutThrottle(bool state)
        {
            if (state)
            {
                float throttle = vessel.ctrlState.mainThrottle;
                try
                {
                    FlightInputHandler.state.mainThrottle = 0.0f;
                }
                catch (Exception)
                {
                    FlightInputHandler.state.mainThrottle = throttle;
                }
            }
        }

        /// <summary>
        /// Returns true when the throttle is at or near 0.
        /// </summary>
        /// <returns></returns>
        public bool ButtonCutThrottleState()
        {
            return (vessel.ctrlState.mainThrottle < 0.01f);
        }

        /// <summary>
        /// Sets the throttle to maximum (1.0) when state is true.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonFullThrottle(bool state)
        {
            if (state)
            {
                float throttle = vessel.ctrlState.mainThrottle;
                try
                {
                    FlightInputHandler.state.mainThrottle = 1.0f;
                }
                catch (Exception)
                {
                    FlightInputHandler.state.mainThrottle = throttle;
                }
            }
        }

        /// <summary>
        /// Returns when the throttle is at or near maximum.
        /// </summary>
        /// <returns></returns>
        public bool ButtonFullThrottleState()
        {
            return (vessel.ctrlState.mainThrottle > 0.99f);
        }
    }
}
