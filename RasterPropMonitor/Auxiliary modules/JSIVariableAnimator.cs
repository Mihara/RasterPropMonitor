using UnityEngine;

namespace JSI
{
	public class JSIVariableAnimator: InternalModule
	{
		[KSPField]
		public string animationName = string.Empty;
		[KSPField]
		public string variableName = string.Empty;
		[KSPField]
		public int refreshRate = 10;
		[KSPField]
		public string scale = string.Empty;
		[KSPField]
		public Vector2 threshold;
		[KSPField]
		public string alarmSound;
		[KSPField]
		public float alarmSoundVolume = 0.5f;
		[KSPField]
		public bool alarmSoundLooping;
		[KSPField]
		public bool reverse;
		[KSPField]
		public string alarmShutdownButton;
		private readonly VariableOrNumber[] scaleEnds = new VariableOrNumber[3];
		private readonly float[] scaleResults = new float[3];
		private RasterPropMonitorComputer comp;
		private int updateCountdown;
		private Animation anim;
		private bool thresholdMode;
		private FXGroup audioOutput;
		private bool alarmActive;

		private bool UpdateCheck()
		{
			if (updateCountdown <= 0) {
				updateCountdown = refreshRate;
				return true;
			}
			updateCountdown--;
			return false;
		}

		public void Start()
		{
			string[] tokens = scale.Split(',');
			if (tokens.Length != 2)
				JUtil.LogMessage(this, "Could not parse the 'scale' parameter: {0}", scale);
			else {

				comp = RasterPropMonitorComputer.Instantiate(internalProp);
				scaleEnds[0] = new VariableOrNumber(tokens[0], comp, this);
				scaleEnds[1] = new VariableOrNumber(tokens[1], comp, this);
				scaleEnds[2] = new VariableOrNumber(variableName, comp, this);

				if (threshold != Vector2.zero) {
					thresholdMode = true;

					float min = Mathf.Min(threshold.x, threshold.y);
					float max = Mathf.Max(threshold.x, threshold.y);
					threshold.x = min;
					threshold.y = max;

					audioOutput = JUtil.SetupIVASound(internalProp, alarmSound, alarmSoundVolume, false);
					if (!string.IsNullOrEmpty(alarmShutdownButton))
						SmarterButton.CreateButton(internalProp, alarmShutdownButton, AlarmShutdown);
				}

				anim = internalProp.FindModelAnimators(animationName)[0];
				anim.enabled = true;
				anim[animationName].speed = 0;
				anim.Play();
			}
		}

		public void AlarmShutdown()
		{
			if (audioOutput != null && alarmActive)
				audioOutput.audio.Stop();
		}

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
				return;

			for (int i = 0; i < 3; i++)
				if (!scaleEnds[i].Get(out scaleResults[i]))
					return;


			if (thresholdMode) {
				float scaledValue = Mathf.InverseLerp(scaleResults[0], scaleResults[1], scaleResults[2]);
				if (scaledValue >= threshold.x && scaledValue <= threshold.y) {
					if (audioOutput != null && !alarmActive) {
						audioOutput.audio.Play();
						alarmActive = true;
					}
					anim[animationName].normalizedTime = reverse ? 0f : 1f;
				} else {
					anim[animationName].normalizedTime = reverse ? 1f : 0f;
					if (audioOutput != null) {
						audioOutput.audio.Stop();
						alarmActive = false;
					}
				}

			} else
				anim[animationName].normalizedTime = JUtil.DualLerp(reverse ? 1f : 0f, reverse ? 0f : 1f, scaleResults[0], scaleResults[1], scaleResults[2]);

		}
	}
}

