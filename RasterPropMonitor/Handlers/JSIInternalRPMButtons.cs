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
