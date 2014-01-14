using MuMech;

namespace MechJebRPM
{
	public class MechJebRPMButtons: InternalModule
	{
		// Execute Node button is the only special one.
		public void ButtonNodeExecute(bool state)
		{
			MechJebCore activeJeb = vessel.GetMasterMechJeb();
			if (activeJeb == null)
				return;
			MechJebModuleManeuverPlanner mp = activeJeb.GetComputerModule<MechJebModuleManeuverPlanner>();
			if (mp == null)
				return;
			if (state) {
				if (!activeJeb.node.enabled) {
					activeJeb.node.ExecuteOneNode(mp);
				}
			} else {
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
			if (state)
				EnactTargetAction(MechJebModuleSmartASS.Target.NODE, vessel);
			else
				EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
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
		// Prograde button
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
		// Target group buttons additionally require a target to be set to press.
		// TGT+ button
		public void ButtonTargetPlus(bool state)
		{
			if (FlightGlobals.fetch.VesselTarget == null)
				return;
			if (state)
				EnactTargetAction(MechJebModuleSmartASS.Target.TARGET_PLUS, vessel);
			else
				EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
		}

		public bool ButtonTargetPlusState()
		{
			return ReturnTargetState(MechJebModuleSmartASS.Target.TARGET_PLUS, vessel);
		}
		// TGT- button
		public void ButtonTargetMinus(bool state)
		{
			if (FlightGlobals.fetch.VesselTarget == null)
				return;
			if (state)
				EnactTargetAction(MechJebModuleSmartASS.Target.TARGET_MINUS, vessel);
			else
				EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
		}

		public bool ButtonTargetMinusState()
		{
			return ReturnTargetState(MechJebModuleSmartASS.Target.TARGET_MINUS, vessel);
		}
		// RVEL+ button
		public void ButtonRvelPlus(bool state)
		{
			if (FlightGlobals.fetch.VesselTarget == null)
				return;
			if (state)
				EnactTargetAction(MechJebModuleSmartASS.Target.RELATIVE_PLUS, vessel);
			else
				EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
		}

		public bool ButtonRvelPlusState()
		{
			return ReturnTargetState(MechJebModuleSmartASS.Target.RELATIVE_PLUS, vessel);
		}
		// RVEL- button
		public void ButtonRvelMinus(bool state)
		{
			if (FlightGlobals.fetch.VesselTarget == null)
				return;
			if (state)
				EnactTargetAction(MechJebModuleSmartASS.Target.RELATIVE_MINUS, vessel);
			else
				EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
		}

		public bool ButtonRvelMinusState()
		{
			return ReturnTargetState(MechJebModuleSmartASS.Target.RELATIVE_MINUS, vessel);
		}
		// PAR+ button
		public void ButtonParPlus(bool state)
		{
			if (FlightGlobals.fetch.VesselTarget == null)
				return;
			if (state)
				EnactTargetAction(MechJebModuleSmartASS.Target.PARALLEL_PLUS, vessel);
			else
				EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
		}

		public bool ButtonParPlusState()
		{
			return ReturnTargetState(MechJebModuleSmartASS.Target.PARALLEL_PLUS, vessel);
		}
		// PAR- button
		public void ButtonParMinus(bool state)
		{
			if (FlightGlobals.fetch.VesselTarget == null)
				return;
			if (state)
				EnactTargetAction(MechJebModuleSmartASS.Target.PARALLEL_MINUS, vessel);
			else
				EnactTargetAction(MechJebModuleSmartASS.Target.OFF, vessel);
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

