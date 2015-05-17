using MuMech;

namespace MechJebRPM
{
    public class MechJebRPMButtons : InternalModule
    {
        public void ButtonNodeExecute(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
                return;
            MechJebModuleManeuverPlanner mp = activeJeb.GetComputerModule<MechJebModuleManeuverPlanner>();
            if (mp == null)
                return;
            if (state)
            {
                if (!activeJeb.node.enabled)
                {
                    activeJeb.node.ExecuteOneNode(mp);
                }
            }
            else
            {
                activeJeb.node.Abort();
            }
        }

        public bool ButtonNodeExecuteState()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
                return false;
            MechJebModuleManeuverPlanner mp = activeJeb.GetComputerModule<MechJebModuleManeuverPlanner>();
            return mp != null && activeJeb.node.enabled;
        }

        public void ButtonAscentGuidance(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }

            MechJebModuleAscentAutopilot ap = activeJeb.GetComputerModule<MechJebModuleAscentAutopilot>();
            if (ap == null)
            {
                return;
            }

            var agPilot = activeJeb.GetComputerModule<MechJebModuleAscentGuidance>();
            if (agPilot != null)
            {
                if (ap.enabled)
                {
                    ap.users.Remove(agPilot);
                }
                else
                {
                    ap.users.Add(agPilot);
                }
            }

        }

        public bool ButtonAscentGuidanceState()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }

            MechJebModuleAscentAutopilot ap = activeJeb.GetComputerModule<MechJebModuleAscentAutopilot>();
            if (ap == null)
            {
                return false;
            }

            return ap.enabled;
        }

        /// <summary>
        /// Plot a Hohmann transfer to the current target.  This is an instant
        /// fire-and-forget function, not a toggle switch
        /// </summary>
        /// <param name="state">unused</param>
        public void ButtonPlotHohmannTransfer(bool state)
        {
            if (!ButtonPlotHohmannTransferState())
            {
                // Target is not one MechJeb can successfully plot.
                return;
            }

            MechJebCore activeJeb = vessel.GetMasterMechJeb();

            Orbit o = vessel.orbit;
            Vector3d dV;
            double UT = Planetarium.GetUniversalTime();
            dV = (o.referenceBody == activeJeb.target.TargetOrbit.referenceBody) ?
                OrbitalManeuverCalculator.DeltaVAndTimeForHohmannTransfer(o, activeJeb.target.TargetOrbit, UT, out UT) :
                OrbitalManeuverCalculator.DeltaVAndTimeForInterplanetaryTransferEjection(o, UT, activeJeb.target.TargetOrbit, true, out UT);
            vessel.RemoveAllManeuverNodes();
            vessel.PlaceManeuverNode(o, dV, UT);
        }

        /// <summary>
        /// Indicate whether a Hohmann Transfer Orbit can be plotted
        /// </summary>
        /// <returns>true if a transfer can be plotted, false if not</returns>
        public bool ButtonPlotHohmannTransferState()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }

            // Most of these conditions are directly from MJ, or derived from
            // it.
            if (!activeJeb.target.NormalTargetExists)
            {
                return false;
            }

            Orbit o = vessel.orbit;
            if (o.eccentricity > 0.2)
            {
                // Need fairly circular orbit to plot.
                return false;
            }

            if (o.referenceBody == activeJeb.target.TargetOrbit.referenceBody)
            {
                // Target is in our SoI

                if (activeJeb.target.TargetOrbit.eccentricity >= 1.0)
                {
                    // can't intercept hyperbolic targets
                    return false;
                }

                if (o.RelativeInclination(activeJeb.target.TargetOrbit) > 30.0 && o.RelativeInclination(activeJeb.target.TargetOrbit) < 150.0)
                {
                    // Target is in a drastically different orbital plane.
                    return false;
                }
            }
            else
            {
                // Target is not in our SoI
                if (o.referenceBody.referenceBody == null)
                {
                    // Can't plot a transfer from an orbit around the sun (really?)
                    return false;
                }
                if (o.referenceBody.referenceBody != activeJeb.target.TargetOrbit.referenceBody)
                {
                    return false;
                }
                if (o.referenceBody.orbit.RelativeInclination(activeJeb.target.TargetOrbit) > 30.0)
                {
                    // Can't handle highly inclined targets
                    return false;
                }
            }

            // Did we get through all the tests?  Then we can plot an orbit!
            return true;
        }

        /// <summary>
        /// Enables / disables landing guidance.  When a target is selected and
        /// this mode is enabled, the ship goes into "Land at Target" mode.  If
        /// a target is not selected, it becomes "Land Somewhere".
        /// </summary>
        /// <param name="state"></param>
        public void ButtonLandingGuidance(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }

            var autopilot = activeJeb.GetComputerModule<MechJebModuleLandingAutopilot>();

            if (autopilot == null)
            {
                return;
            }

            if (state != autopilot.enabled)
            {
                if (state)
                {
                    var landingGuidanceAP = activeJeb.GetComputerModule<MechJebModuleLandingGuidance>();
                    if (landingGuidanceAP != null)
                    {
                        if (activeJeb.target.PositionTargetExists)
                        {
                            autopilot.LandAtPositionTarget(landingGuidanceAP);
                        }
                        else
                        {
                            autopilot.LandUntargeted(landingGuidanceAP);
                        }
                    }
                }
                else
                {
                    autopilot.StopLanding();
                }
            }
        }

        /// <summary>
        /// Returns the current state of the landing guidance feature
        /// </summary>
        /// <returns>true if on, false if not</returns>
        public bool ButtonLandingGuidanceState()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }

            var autopilot = activeJeb.GetComputerModule<MechJebModuleLandingAutopilot>();

            if (autopilot == null)
            {
                return false;
            }

            return autopilot.enabled;
        }

        /// <summary>
        /// Toggles SmartASS Force Roll mode.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }

            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            if (activeSmartass != null)
            {
                activeSmartass.forceRol = state;
                activeSmartass.Engage();
            }
        }

        /// <summary>
        /// Indicates whether SmartASS Force Roll is on or off
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRollState()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }

            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            return (activeSmartass != null && activeSmartass.forceRol);
        }

        /// <summary>
        /// Force the roll to zero degrees.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll0(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }

            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            if (activeSmartass != null)
            {
                if (state)
                {
                    activeSmartass.rol = 0.0;
                }
                activeSmartass.forceRol = state;
                activeSmartass.Engage();
            }
        }

        /// <summary>
        /// Returns true when Force Roll is on, and the roll is set to 0.
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRoll0State()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }
            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            return (activeSmartass != null && activeSmartass.forceRol && (double)activeSmartass.rol == 0.0);
        }

        /// <summary>
        /// Force the roll to +90 degrees.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll90(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }

            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            if (activeSmartass != null)
            {
                if (state)
                {
                    activeSmartass.rol = 90.0;
                }
                activeSmartass.forceRol = state;
                activeSmartass.Engage();
            }
        }

        /// <summary>
        /// Returns true when Force Roll is on, and the roll is set to 90.
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRoll90State()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }

            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            return (activeSmartass != null && activeSmartass.forceRol && (double)activeSmartass.rol == 90.0);
        }

        /// <summary>
        /// Force the roll to 180 degrees.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll180(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }

            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            if (activeSmartass != null)
            {
                if (state)
                {
                    activeSmartass.rol = 180.0;
                }
                activeSmartass.forceRol = state;
                activeSmartass.Engage();
            }
        }

        /// <summary>
        /// Returns true when Force Roll is on, and the roll is set to 180.
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRoll180State()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }

            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            return (activeSmartass != null && activeSmartass.forceRol && (double)activeSmartass.rol == 180.0);
        }

        /// <summary>
        /// Force the roll to -90 degrees.
        /// </summary>
        /// <param name="state"></param>
        public void ButtonForceRoll270(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }

            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            if (activeSmartass != null)
            {
                if (state)
                {
                    activeSmartass.rol = -90.0;
                }
                activeSmartass.forceRol = state;
                activeSmartass.Engage();
            }
        }

        /// <summary>
        /// Returns true when Force Roll is on, and the roll is set to -90.
        /// </summary>
        /// <returns></returns>
        public bool ButtonForceRoll270State()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }

            // MOARdV TODO: normalize values before testing.
            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            return (activeSmartass != null && activeSmartass.forceRol && (double)activeSmartass.rol == -90.0);
        }

        /// <summary>
        /// The MechJeb landing prediction simulator runs on a separate thread,
        /// and it may be costly for lower-end computers to leave it running
        /// all the time.  This button allows the player to indicate whether
        /// it needs to be running, or not.
        /// </summary>
        /// <param name="state">Enable/disable</param>
        public void ButtonEnableLandingPrediction(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }
            var predictor = activeJeb.GetComputerModule<MechJebModuleLandingPredictions>();
            if (predictor != null)
            {
                var landingGuidanceAP = activeJeb.GetComputerModule<MechJebModuleLandingGuidance>();
                if (landingGuidanceAP != null)
                {
                    if (state)
                    {
                        predictor.users.Add(landingGuidanceAP);
                    }
                    else
                    {
                        predictor.users.Remove(landingGuidanceAP);
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether the landing prediction simulator is currently
        /// running.
        /// </summary>
        /// <returns></returns>
        public bool ButtonEnableLandingPredictionState()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }
            var predictor = activeJeb.GetComputerModule<MechJebModuleLandingPredictions>();
            return (predictor != null && predictor.enabled);
        }

        /// <summary>
        /// Engages / disengages Rendezvous Autopilot
        /// </summary>
        /// <param name="state"></param>
        public void ButtonRendezvousAutopilot(bool state)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return;
            }
            var autopilot = activeJeb.GetComputerModule<MechJebModuleRendezvousAutopilot>();
            if (autopilot != null && activeJeb.target.NormalTargetExists && activeJeb.target.TargetOrbit.referenceBody == vessel.orbit.referenceBody)
            {
                /*
                var autopilotController = activeJeb.GetComputerModule<MechJebModuleRendezvousAutopilotWindow>();
                if (autopilotController != null && autopilot.enabled != state) {
                    if (state) {
                        autopilot.users.Add(autopilotController);
                    } else {
                        autopilot.users.Remove(autopilotController);
                    }
                }
                 */
            }
        }

        /// <summary>
        /// Indicates whether the Rendezvous Autopilot is engaged.
        /// </summary>
        /// <returns></returns>
        public bool ButtonRendezvousAutopilotState()
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            if (activeJeb == null)
            {
                return false;
            }
            var autopilot = activeJeb.GetComputerModule<MechJebModuleRendezvousAutopilot>();
            return (autopilot != null && autopilot.enabled);
        }

        // All the other buttons are pretty much identical and just use different enum values.
        // Off button
        // Analysis disable once UnusedParameter
        public void ButtonOff(bool state)
        {
            EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
        }

        public bool ButtonOffState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.OFF, vessel);
        }
        // NODE button
        public void ButtonNode(bool state)
        {
            if (vessel.patchedConicSolver != null)
            {
                if (state && vessel.patchedConicSolver.maneuverNodes.Count > 0)
                    EnactTargetAction(MechJebModuleSmartASS.Target.NODE, vessel);
                else if (!state)
                    EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            }
        }

        public bool ButtonNodeState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.NODE, vessel);
        }
        // KillRot button
        public void ButtonKillRot(bool state)
        {
            if (state)
                EnactTargetAction(MechJebModuleSmartASS.Target.KILLROT, vessel);
            else
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
        }

        public bool ButtonKillRotState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.KILLROT, vessel);
        }
        // Prograde button
        public void ButtonPrograde(bool state)
        {
            if (state)
                EnactTargetAction(MechJebModuleSmartASS.Target.PROGRADE, vessel);
            else
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
        }

        public bool ButtonProgradeState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.PROGRADE, vessel);
        }
        // Retrograde button
        public void ButtonRetrograde(bool state)
        {
            if (state)
                EnactTargetAction(MechJebModuleSmartASS.Target.RETROGRADE, vessel);
            else
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
        }

        public bool ButtonRetrogradeState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.RETROGRADE, vessel);
        }
        // NML+ button
        public void ButtonNormalPlus(bool state)
        {
            if (state)
                EnactTargetAction(MechJebModuleSmartASS.Target.NORMAL_PLUS, vessel);
            else
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
        }

        public bool ButtonNormalPlusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.NORMAL_PLUS, vessel);
        }
        // NML- button
        public void ButtonNormalMinus(bool state)
        {
            if (state)
                EnactTargetAction(MechJebModuleSmartASS.Target.NORMAL_MINUS, vessel);
            else
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
        }

        public bool ButtonNormalMinusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.NORMAL_MINUS, vessel);
        }
        // RAD+ button
        public void ButtonRadialPlus(bool state)
        {
            if (state)
                EnactTargetAction(MechJebModuleSmartASS.Target.RADIAL_PLUS, vessel);
            else
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
        }

        public bool ButtonRadialPlusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.RADIAL_PLUS, vessel);
        }
        // RAD- button
        public void ButtonRadialMinus(bool state)
        {
            if (state)
                EnactTargetAction(MechJebModuleSmartASS.Target.RADIAL_MINUS, vessel);
            else
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
        }

        public bool ButtonRadialMinusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.RADIAL_MINUS, vessel);
        }
        // Surface prograde button
        public void ButtonSurfacePrograde(bool state)
        {
            if (state)
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.SURFACE_PROGRADE, vessel);
            }
            else
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            }
        }

        public bool ButtonSurfaceProgradeState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.SURFACE_PROGRADE, vessel);
        }
        // Surface Retrograde button
        public void ButtonSurfaceRetrograde(bool state)
        {
            if (state)
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.SURFACE_RETROGRADE, vessel);
            }
            else
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            }
        }

        public bool ButtonSurfaceRetrogradeState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.SURFACE_RETROGRADE, vessel);
        }
        // Horizontal + button
        public void ButtonHorizontalPlus(bool state)
        {
            if (state)
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.HORIZONTAL_PLUS, vessel);
            }
            else
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            }
        }

        public bool ButtonHorizontalPlusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.HORIZONTAL_PLUS, vessel);
        }
        // Horizontal - button
        public void ButtonHorizontalMinus(bool state)
        {
            if (state)
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.HORIZONTAL_MINUS, vessel);
            }
            else
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            }
        }

        public bool ButtonHorizontalMinusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.HORIZONTAL_MINUS, vessel);
        }
        // Up button
        public void ButtonVerticalPlus(bool state)
        {
            if (state)
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.VERTICAL_PLUS, vessel);
            }
            else
            {
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            }
        }

        public bool ButtonVerticalPlusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.VERTICAL_PLUS, vessel);
        }
        // Target group buttons additionally require a target to be set to press.
        // TGT+ button
        public void ButtonTargetPlus(bool state)
        {
            if (!state)
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            else if (FlightGlobals.fetch.VesselTarget != null)
                EnactTargetAction(MechJebModuleSmartASS.Target.TARGET_PLUS, vessel);
        }

        public bool ButtonTargetPlusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.TARGET_PLUS, vessel);
        }
        // TGT- button
        public void ButtonTargetMinus(bool state)
        {
            if (!state)
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            else if (FlightGlobals.fetch.VesselTarget != null)
                EnactTargetAction(MechJebModuleSmartASS.Target.TARGET_MINUS, vessel);
        }

        public bool ButtonTargetMinusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.TARGET_MINUS, vessel);
        }
        // RVEL+ button
        public void ButtonRvelPlus(bool state)
        {
            if (!state)
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            else if (FlightGlobals.fetch.VesselTarget != null)
                EnactTargetAction(MechJebModuleSmartASS.Target.RELATIVE_PLUS, vessel);
        }

        public bool ButtonRvelPlusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.RELATIVE_PLUS, vessel);
        }
        // RVEL- button
        public void ButtonRvelMinus(bool state)
        {
            if (!state)
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            else if (FlightGlobals.fetch.VesselTarget != null)
                EnactTargetAction(MechJebModuleSmartASS.Target.RELATIVE_MINUS, vessel);
        }

        public bool ButtonRvelMinusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.RELATIVE_MINUS, vessel);
        }
        // PAR+ button
        public void ButtonParPlus(bool state)
        {
            if (!state)
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            else if (FlightGlobals.fetch.VesselTarget != null)
                EnactTargetAction(MechJebModuleSmartASS.Target.PARALLEL_PLUS, vessel);
        }

        public bool ButtonParPlusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.PARALLEL_PLUS, vessel);
        }
        // PAR- button
        public void ButtonParMinus(bool state)
        {
            if (!state)
                EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
            else if (FlightGlobals.fetch.VesselTarget != null)
                EnactTargetAction(MechJebModuleSmartASS.Target.PARALLEL_MINUS, vessel);
        }

        public bool ButtonParMinusState()
        {
            return ReturnTargetState(MechJebModuleSmartASS.Target.PARALLEL_MINUS, vessel);
        }
        // and these are the two functions that actually do the work.
        private static void EnactTargetAction(MechJebModuleSmartASS.Target action, Vessel ourVessel)
        {
            MechJebCore activeJeb = ourVessel.GetMasterMechJeb();
            if (activeJeb == null)
                return;
            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            if (activeSmartass == null)
                return;
            activeSmartass.target = action;
            activeSmartass.Engage();
        }

        private static bool ReturnTargetState(MechJebModuleSmartASS.Target action, Vessel ourVessel)
        {
            MechJebCore activeJeb = ourVessel.GetMasterMechJeb();
            if (activeJeb == null)
                return false;
            MechJebModuleSmartASS activeSmartass = activeJeb.GetComputerModule<MechJebModuleSmartASS>();
            if (activeSmartass == null)
                return false;
            return action == activeSmartass.target;
        }
    }
}

