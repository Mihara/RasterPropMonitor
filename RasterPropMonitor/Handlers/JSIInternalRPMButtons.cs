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
			foreach (Part thatPart in vessel.parts) {
				foreach (PartResource resource in thatPart.Resources) {
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
			foreach (Part thatPart in vessel.parts) {
				foreach (PartResource resource in thatPart.Resources) {
					if (resource.flowState == false) {
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
		public void ButtonClearNodes(bool state)
		{
			while (vessel.patchedConicSolver.maneuverNodes.Count > 0) {
				vessel.patchedConicSolver.RemoveManeuverNode(vessel.patchedConicSolver.maneuverNodes.Last());
			}
		}

		/// <summary>
		/// Indicates whether there are maneuver nodes to clear.
		/// </summary>
		/// <returns></returns>
		public bool ButtonClearNodesState()
		{
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
			foreach (Part thatPart in vessel.parts) {
				// We accept "state == false" to allow engines that are
				// activated outside of the current staging to be shut off by
				// this function.
				if (thatPart.inverseStage == Staging.CurrentStage || state == false) {
					foreach (PartModule pm in thatPart.Modules) {
						var engine = pm as ModuleEngines;
						if (engine != null && engine.EngineIgnited != state) {
							if (state && engine.allowRestart) {
								engine.Activate();
							} else if (engine.allowShutdown) {
								engine.Shutdown();
							}
						}
						var engineFX = pm as ModuleEnginesFX;
						if (engineFX != null && engineFX.EngineIgnited != state) {
							if (state && engineFX.allowRestart) {
								engineFX.Activate();
							} else if(engineFX.allowShutdown) {
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
			foreach (Part thatPart in vessel.parts) {
				foreach (PartModule pm in thatPart.Modules) {
					var engine = pm as ModuleEngines;
					if (engine != null && engine.allowShutdown && engine.getIgnitionState) {
						// early out: at least one engine is enabled.
						return true;
					}
					var engineFX = pm as ModuleEnginesFX;
					if (engineFX != null && engineFX.allowShutdown && engineFX.getIgnitionState) {
						// early out: at least one engine is enabled.
						return true;
					}
				}
			}
			return false;
		}
	}
}
