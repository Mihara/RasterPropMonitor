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
	}
}
