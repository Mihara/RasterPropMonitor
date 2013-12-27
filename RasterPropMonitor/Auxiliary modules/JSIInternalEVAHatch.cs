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
			if (internalProp == null) {
				SmarterButton.CreateButton(null, hatchTransform, EVAClick, null, internalModel);
			} else {
				SmarterButton.CreateButton(internalProp, hatchTransform, EVAClick);
			}
		}

		public void EVAClick()
		{
			Kerbal thatKerbal = JUtil.FindCurrentKerbal(part);
			if (thatKerbal != null && HighLogic.CurrentGame.Parameters.Flight.CanEVA) {
				FlightEVA.SpawnEVA(thatKerbal);
				CameraManager.Instance.SetCameraFlight();
				JUtil.LogMessage(this, "{0} has opened the internal EVA hatch.", thatKerbal.name);
			} else
				JUtil.LogMessage(this, "Could not open the internal EVA hatch, not sure why.");
		}
	}
}

