using UnityEngine;

namespace JSI
{
	public class JSIInternalEVAHatch: InternalModule
	{
		[KSPField]
		public string hatchTransform = string.Empty;
		[KSPField]
		public string internalAnimation = string.Empty;
		private Kerbal activeKerbal;
		private Animation intAnim;
		private bool intAnimStarted;

		public void Start()
		{
			if (string.IsNullOrEmpty(hatchTransform)) {
				JUtil.LogMessage(this, "Where's my transform?");
				return;
			}
			Transform actualTransform;
			if (internalProp == null) {
				actualTransform = internalModel.FindModelTransform(hatchTransform);
				if (!string.IsNullOrEmpty(internalAnimation)) {
					intAnim = internalModel.FindModelAnimators(internalAnimation)[0];
				}
			} else {
				actualTransform = internalProp.FindModelTransform(hatchTransform);
				if (!string.IsNullOrEmpty(internalAnimation)) {
					intAnim = internalProp.FindModelAnimators(internalAnimation)[0];
				}
			}
			if (!string.IsNullOrEmpty(internalAnimation) && intAnim == null)
				JUtil.LogErrorMessage(this, "Animation name was not found.");
			// Switching to using the stock button class because right now SmarterButton can't correctly handle doubleclick.
			InternalButton.Create(actualTransform.gameObject).OnDoubleTap(new InternalButton.InternalButtonDelegate(EVAClick));

		}

		private void GoEva()
		{
			if (activeKerbal != null) {
				FlightEVA.SpawnEVA(activeKerbal);
				CameraManager.Instance.SetCameraFlight();
				activeKerbal = null;
			}
		}
		// ..I don't feel like using coroutines.
		public override void OnUpdate()
		{
			if (intAnimStarted) {
				if (!intAnim.isPlaying) {
					// The animation completed, so we kick the kerbal out now.
					intAnimStarted = false;
					GoEva();
					// And immediately reset the animation.
					intAnim[internalAnimation].normalizedTime = 0;
					intAnim.Stop();
				}
			}
		}

		public void EVAClick()
		{
			Kerbal thatKerbal = part.FindCurrentKerbal();
			if (thatKerbal != null && HighLogic.CurrentGame.Parameters.Flight.CanEVA) {
				activeKerbal = thatKerbal;
				if (intAnim != null) {
					intAnim.enabled = true;
					intAnim[internalAnimation].speed = 1;
					intAnim.Play();
					intAnimStarted = true;
				} else {
					GoEva();
				}
				JUtil.LogMessage(this, "{0} has opened the internal EVA hatch.", thatKerbal.name);
			} else
				JUtil.LogMessage(this, "Could not open the internal EVA hatch, not sure why.");
		}
	}
}

