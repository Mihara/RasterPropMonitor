using System;
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
			if (internalProp == null) {
				SmarterButton.CreateButton(null, hatchTransform, EVAClick, null, internalModel);
			} else {
				SmarterButton.CreateButton(internalProp, hatchTransform, EVAClick);
			}
		}

		public void EVAClick()
		{
			// I wish there were a more sensible way to do it, but looks like there is no other way to get a pointer to it.
			IVACamera currentCamera = (IVACamera)FindObjectOfType(typeof(IVACamera));
			if (currentCamera == null)
				return;
			if (HighLogic.CurrentGame.Parameters.Flight.CanEVA) {
				CameraManager.Instance.SetCameraFlight();
				FlightEVA.SpawnEVA(currentCamera.kerbal);
			}

		}
	}
}

