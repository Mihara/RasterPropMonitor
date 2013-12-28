using UnityEngine;

namespace JSI
{
	public class JSIInternalEVAHatch: InternalModule
	{
		[KSPField]
		public string hatchTransform = string.Empty;

		public void Start()
		{
			if (string.IsNullOrEmpty(hatchTransform)) {
				JUtil.LogMessage(this, "Where's my transform?");
				return;
			}
			Transform actualTransform;
			if (internalProp == null) {
				actualTransform = internalModel.FindModelTransform(hatchTransform);
			} else {
				actualTransform = internalProp.FindModelTransform(hatchTransform);
			}
			// Switching to using the stock button class because right now SmarterButton can't correctly handle doubleclick.
			InternalButton.Create(actualTransform.gameObject).OnDoubleTap(new InternalButton.InternalButtonDelegate(EVAClick));

		}

		public void EVAClick()
		{
			Kerbal thatKerbal = part.FindCurrentKerbal();
			if (thatKerbal != null && HighLogic.CurrentGame.Parameters.Flight.CanEVA) {
				FlightEVA.SpawnEVA(thatKerbal);
				CameraManager.Instance.SetCameraFlight();
				JUtil.LogMessage(this, "{0} has opened the internal EVA hatch.", thatKerbal.name);
			} else
				JUtil.LogMessage(this, "Could not open the internal EVA hatch, not sure why.");
		}
	}
}

