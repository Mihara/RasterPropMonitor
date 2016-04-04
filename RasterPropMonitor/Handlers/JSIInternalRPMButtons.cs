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
using KSP.UI;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JSI
{
    /// <summary>
    /// Provides a built-in plugin to execute tasks that can be done in the
    /// core RPM without plugin assistance.
    /// </summary>
    public class JSIInternalRPMButtons : IJSIModule
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
            if (vessel != null)
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
            if (vessel == null)
            {
                return false;
            }

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
            if (vessel != null)
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
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Allows enabling/disabling electric generators (and fuel cells)
        /// </summary>
        /// <param name="state"></param>
        public void ButtonEnableElectricGenerator(bool state)
        {
            if (vessel != null)
            {
                foreach (PartModule pm in ElectricGenerators(vessel))
                {
                    if (pm is ModuleGenerator)
                    {
                        ModuleGenerator gen = pm as ModuleGenerator;
                        if (state)
                        {
                            gen.Activate();
                        }
                        else
                        {
                            gen.Shutdown();
                        }
                    }
                    else if (pm is ModuleResourceConverter)
                    {
                        ModuleResourceConverter gen = pm as ModuleResourceConverter;
                        if (state)
                        {
                            gen.StartResourceConverter();
                        }
                        else
                        {
                            gen.StopResourceConverter();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether any generators or fuel cells are active.
        /// </summary>
        /// <returns></returns>
        public bool ButtonEnableElectricGeneratorState()
        {
            if (vessel != null)
            {
                foreach (PartModule pm in ElectricGenerators(vessel))
                {
                    if (pm is ModuleGenerator && (pm as ModuleGenerator).generatorIsActive == true)
                    {
                        return true;
                    }
                    else if (pm is ModuleResourceConverter && (pm as ModuleResourceConverter).IsActivated == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Toggle Precision Input mode
        /// </summary>
        /// <param name="state"></param>
        public void ButtonPrecisionMode(bool state)
        {
            if (vessel != null)
            {
                FlightInputHandler.fetch.precisionMode = state;

                // Update the UI.
                foreach (UnityEngine.Renderer renderer in FlightInputHandler.fetch.inputGaugeRenderers)
                {
                    renderer.material.color = (state) ? XKCDColors.BrightCyan : XKCDColors.Orange;
                }
            }
        }

        /// <summary>
        /// Returns 'true' if the inputs are in precision mode.
        /// </summary>
        /// <returns></returns>
        public bool ButtonPrecisionModeState()
        {
            return FlightInputHandler.fetch.precisionMode;
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
            UIStateToggleButton[] SASbtns = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>().modeButtons;
            // set our mode, note it takes the mode as an int, generally top to bottom, left to right, as seen on the screen. Maneuver node being the exception, it is 9
            SASbtns.ElementAt<UIStateToggleButton>((int)newMode).SetState(true);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.StabilityAssist);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Prograde);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Retrograde);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Normal);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Antinormal);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.RadialIn);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.RadialOut);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Target);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.AntiTarget);
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
            return ((vessel != null) && vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Maneuver);
        }

        /**
         * Cycle speed modes (between orbital/surface/target)
         */
        public void ButtonSpeedMode(bool ignored)
        {
            FlightGlobals.CycleSpeedModes();
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
            return ((vessel != null) && vessel.ctrlState.mainThrottle < 0.01f);
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
            return ((vessel != null) && vessel.ctrlState.mainThrottle > 0.99f);
        }

        /// <summary>
        /// Undock the current reference part, or the inferred first dock on
        /// the current vessel.
        /// 
        /// The state of the dock appears to be queriable only by reading a
        /// string.  The possible values of that string (that I've discovered)
        /// are:
        /// 
        /// "Disabled", for shielded docking ports that are closed.
        /// "Docked (dockee)", for docks that were docked to (recipient dock).
        /// "Docked (docker)", for docks that initiated the docking.
        /// "PreAttached", for docks that were attached to something in the VAB
        /// "Ready", for docks that are ready.
        /// </summary>
        /// <param name="state">New state - must be 'false' to trigger the undock event</param>
        public void DockUndock(bool state)
        {
            if (vessel == null || state == true)
            {
                return;
            }

            ModuleDockingNode node = InferDockingNode(vessel);
            if (node != null)
            {
                if ((node.state == "Docked (docker)") || (node.state == "Docked (dockee)"))
                {
                    node.Undock();
                }
            }
        }

        /// <summary>
        /// Detach a docking node that was attached in the VAB.
        /// </summary>
        /// <param name="state">New state - must be 'false' to trigger</param>
        public void DockDetach(bool state)
        {
            if (vessel == null || state == true)
            {
                return;
            }

            ModuleDockingNode node = InferDockingNode(vessel);
            if (node != null)
            {
                if (node.state == "PreAttached")
                {
                    node.Decouple();
                }
            }
        }

        /// <summary>
        /// Is the current reference dock pre-attached ("docked" in the VAB)?
        /// </summary>
        /// <returns></returns>
        public bool DockAttached()
        {
            if (vessel == null)
            {
                return false;
            }

            ModuleDockingNode node = InferDockingNode(vessel);
            if (node != null)
            {
                // Urk.  No enums or numerics to test state...
                return (node.state == "PreAttached");
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Is the current reference dock docked to something?
        /// </summary>
        /// <returns></returns>
        public bool DockDocked()
        {
            if (vessel == null)
            {
                return false;
            }

            ModuleDockingNode node = InferDockingNode(vessel);
            if (node != null)
            {
                // Urk.  No enums or numerics to test state...
                return (node.state == "Docked (docker)") || (node.state == "Docked (dockee)");
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Is the current reference dock ready?
        /// </summary>
        /// <returns></returns>
        public bool DockReady()
        {
            if (vessel == null)
            {
                return false;
            }

            ModuleDockingNode node = InferDockingNode(vessel);
            if (node != null)
            {
                // Urk.  No enums or numerics to test state...
                return (node.state == "Ready");
            }
            else
            {
                return false;
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
            bool gimbalLockState = false;

            if (vessel == null)
            {
                return gimbalLockState; // early
            }

            foreach (ModuleGimbal gimbal in FindActiveStageGimbals(vessel))
            {
                if (gimbal.gimbalLock)
                {
                    gimbalLockState = true;
                    break;
                }
            }

            return gimbalLockState;
        }

        public void RadarEnable(bool enabled)
        {
            try
            {
                List<JSIRadar> radars = vessel.FindPartModulesImplementing<JSIRadar>();
                for (int i = 0; i < radars.Count; ++i)
                {
                    radars[i].radarEnabled = enabled;
                }
            }
            catch { }
        }

        public bool RadarEnableState()
        {
            bool enabled = false;

            try
            {
                List<JSIRadar> radars = vessel.FindPartModulesImplementing<JSIRadar>();
                for (int i = 0; i < radars.Count; ++i)
                {
                    if (radars[i].radarEnabled)
                    {
                        enabled = true;
                        break;
                    }
                }
            }
            catch { }

            return enabled;
        }

        public double GetSASMode()
        {
            if (vessel == null)
            {
                return 0.0; // StabilityAssist
            }
            double mode;
            switch (vessel.Autopilot.Mode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    mode = 0.0;
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    mode = 1.0;
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    mode = 2.0;
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    mode = 3.0;
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    mode = 4.0;
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    mode = 5.0;
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    mode = 6.0;
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    mode = 7.0;
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    mode = 8.0;
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    mode = 9.0;
                    break;
                default:
                    mode = 0.0;
                    break;
            }
            return mode;
        }

        public void SetSASMode(double mode)
        {
            int imode = (int)mode;
            VesselAutopilot.AutopilotMode autopilotMode;
            switch (imode)
            {
                case 0:
                    autopilotMode = VesselAutopilot.AutopilotMode.StabilityAssist;
                    break;
                case 1:
                    autopilotMode = VesselAutopilot.AutopilotMode.Prograde;
                    break;
                case 2:
                    autopilotMode = VesselAutopilot.AutopilotMode.Retrograde;
                    break;
                case 3:
                    autopilotMode = VesselAutopilot.AutopilotMode.Normal;
                    break;
                case 4:
                    autopilotMode = VesselAutopilot.AutopilotMode.Antinormal;
                    break;
                case 5:
                    autopilotMode = VesselAutopilot.AutopilotMode.RadialIn;
                    break;
                case 6:
                    autopilotMode = VesselAutopilot.AutopilotMode.RadialOut;
                    break;
                case 7:
                    autopilotMode = VesselAutopilot.AutopilotMode.Target;
                    break;
                case 8:
                    autopilotMode = VesselAutopilot.AutopilotMode.AntiTarget;
                    break;
                case 9:
                    autopilotMode = VesselAutopilot.AutopilotMode.Maneuver;
                    break;
                default:
                    JUtil.LogErrorMessage(this, "SetSASMode: attempt to set a SAS mode with the invalid value {0}", imode);
                    return;
            }

            if (vessel.Autopilot.CanSetMode(autopilotMode))
            {
                vessel.Autopilot.SetMode(autopilotMode);
                ForceUpdateSASModeToggleButtons(autopilotMode);
            }
        }

        /**
         * @returns true if all trim settings are within 1% of neutral.
         */
        public bool TrimNeutralState()
        {
            if (vessel != null && vessel.ctrlState != null)
            {
                return Mathf.Abs(vessel.ctrlState.pitchTrim) < 0.01f && Mathf.Abs(vessel.ctrlState.rollTrim) < 0.01f && Mathf.Abs(vessel.ctrlState.yawTrim) < 0.01f;
            }
            else
            {
                return true;
            }
        }

        /**
         * Resets all trim parameters to neutral
         */
        public void SetAllTrimNeutral(bool state)
        {
            FlightInputHandler.state.ResetTrim();
        }

        /**
         * Resets pitch trim to neutral
         */
        public void SetPitchTrimNeutral(bool state)
        {
            FlightInputHandler.state.pitchTrim = 0.0f;
        }

        /**
         * Sets pitch trim to the desired percent (-100 to 100)
         */
        public void SetPitchTrim(double trimPercent)
        {
            FlightInputHandler.state.pitchTrim = (float)(trimPercent.Clamp(-100.0, 100.0)) / 100.0f;
        }

        /**
         * Resets roll trim to neutral
         */
        public void SetRollTrimNeutral(bool state)
        {
            FlightInputHandler.state.rollTrim = 0.0f;
        }

        /**
         * Sets roll trim to the desired percent (-100 to 100)
         */
        public void SetRollTrim(double trimPercent)
        {
            FlightInputHandler.state.rollTrim = (float)(trimPercent.Clamp(-100.0, 100.0)) / 100.0f;
        }

        /**
         * Resets yaw trim to neutral
         */
        public void SetYawTrimNeutral(bool state)
        {
            FlightInputHandler.state.yawTrim = 0.0f;
        }

        /**
         * Sets yaw trim to the desired percent (-100 to 100)
         */
        public void SetYawTrim(double trimPercent)
        {
            FlightInputHandler.state.yawTrim = (float)(trimPercent.Clamp(-100.0, 100.0)) / 100.0f;
        }

        /// <summary>
        /// Infers the docking node this vessel controls
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private static ModuleDockingNode InferDockingNode(Vessel vessel)
        {
            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            Part compPart = comp.ReferencePart;
            uint launchId;
            if (compPart == null)
            {
                launchId = 0u;
            }
            else
            {
                launchId = compPart.launchID;
            }

            Part referencePart = vessel.GetReferenceTransformPart();
            ModuleDockingNode node = referencePart.FindModuleImplementing<ModuleDockingNode>();
            if (node != null)
            {
                //JUtil.LogMessage(vessel, "InferDockingNode: using reference part {0}", referencePart.name);
                // The current reference part is a docking node.
                return node;
            }

            for (int i = 0; i < vessel.parts.Count; ++i)
            {
                if (vessel.parts[i].launchID == launchId)
                {
                    node = vessel.parts[i].FindModuleImplementing<ModuleDockingNode>();
                    if (node != null)
                    {
                        //JUtil.LogMessage(vessel, "InferDockingNode: found a node on {0}", vessel.parts[i].name);
                        return node;
                    }
                }
            }

            // We did not find a docking node.
            return null;
        }

        /// <summary>
        /// Iterate over the modules in the craft and return all of them that
        /// implement a ModuleGenerator or ModuleResourceConverter that generates 
        /// electricity that can also be shut down.
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private static System.Collections.Generic.IEnumerable<PartModule> ElectricGenerators(Vessel vessel)
        {
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule pm in part.Modules)
                {
                    if (pm is ModuleGenerator)
                    {
                        ModuleGenerator gen = pm as ModuleGenerator;
                        if (gen.isAlwaysActive == false)
                        {
                            for (int i = 0; i < gen.outputList.Count; ++i)
                            {
                                if (gen.outputList[i].name == "ElectricCharge")
                                {
                                    yield return pm;
                                }
                            }
                        }
                    }
                    else if (pm is ModuleResourceConverter)
                    {
                        ModuleResourceConverter gen = pm as ModuleResourceConverter;
                        if (gen.AlwaysActive == false)
                        {
                            ConversionRecipe recipe = gen.Recipe;
                            for (int i = 0; i < recipe.Outputs.Count; ++i)
                            {
                                if (recipe.Outputs[i].ResourceName == "ElectricCharge")
                                {
                                    yield return pm;
                                }
                            }
                        }
                    }
                }
            }
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
                if (thatPart.inverseStage >= StageManager.CurrentStage)
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
