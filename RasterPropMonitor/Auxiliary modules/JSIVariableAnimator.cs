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
		private readonly float?[] scalePoints = { null, null };
		private readonly string[] varName = { null, null };
		private RasterPropMonitorComputer comp;
		private int updateCountdown;
		private Animation anim;
		private bool thresholdMode;
		private FXGroup audioOutput;
		private bool alarmActive;
		private readonly bool[] warningMade = { false, false, false };

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

			for (int i = 0; i < tokens.Length; i++) {
				float realValue;
				if (float.TryParse(tokens[i], out realValue)) {
					scalePoints[i] = realValue;
				} else {
					varName[i] = tokens[i].Trim();
				}

			}

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

			comp = JUtil.GetComputer(internalProp);

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
		// It's messy but it's static and it abstracts some of the mess away.
		public static bool MassageScalePoint(out float destination, float? source, string variableName, ref bool warningFlag, RasterPropMonitorComputer compReference, InternalModule caller)
		{
			destination = source ?? JUtil.MassageObjectToFloat(compReference.ProcessVariable(variableName));
			if (float.IsNaN(destination) || float.IsInfinity(destination)) {
				if (!warningFlag) {
					JUtil.LogMessage(caller, "Warning, {0} can fail to produce a usable number.", variableName);
					warningFlag = true;
					return false;
				}

			}
			return true;
		}

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
				return;

			float scaleBottom;
			if (!MassageScalePoint(out scaleBottom, scalePoints[0], varName[0], ref warningMade[0], comp, this))
				return;

			float scaleTop;
			if (!MassageScalePoint(out scaleTop, scalePoints[1], varName[1], ref warningMade[1], comp, this))
				return;

			float varValue;
			if (!MassageScalePoint(out varValue, null, variableName, ref warningMade[2], comp, this))
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

