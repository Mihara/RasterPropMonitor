using UnityEngine;

namespace JSI
{
	public class JSIVariableAnimator: InternalModule
	{
		[KSPField]
		public string animationName = "";
		[KSPField]
		public string variableName = "";
		[KSPField]
		public int refreshRate = 10;
		[KSPField]
		public string scale;
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

			comp = JUtil.GetComputer(internalProp);
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
				if (!string.IsNullOrEmpty(alarmShutdownButton)) {
					SmarterButton.CreateButton(internalProp, alarmShutdownButton, AlarmShutdown);
				}
			}

			anim = internalProp.FindModelAnimators(animationName)[0];
			anim.enabled = true;
			anim[animationName].speed = 0;
			anim.Play();
		}

		public void AlarmShutdown()
		{
			if (audioOutput != null && alarmActive) {
				audioOutput.audio.Stop();
			}
		}

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
				return;

			float scaleBottom;
			if (!scaleEnds[0].Get(out scaleBottom))
				return;
			float scaleTop;
			if (!scaleEnds[1].Get(out scaleTop))
				return;
			float varValue;
			if (!scaleEnds[2].Get(out varValue))
				return;

			if (thresholdMode) {
				float scaledValue = Mathf.InverseLerp(scaleBottom, scaleTop, varValue);
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

			} else {
				anim[animationName].normalizedTime = JUtil.DualLerp(reverse ? 1f : 0f, reverse ? 0f : 1f, scaleBottom, scaleTop, varValue);
			}

		}
	}
}

