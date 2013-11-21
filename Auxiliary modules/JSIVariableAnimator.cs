using System;
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
		public string alarmShutdownButton;
		private float?[] scalePoints = { null, null };
		private string[] varName = { null, null };
		private RasterPropMonitorComputer comp;
		private int updateCountdown;
		private Animation anim;
		private bool thresholdMode;
		private FXGroup audioOutput;
		private bool alarmActive;

		private static void LogMessage(string line, params object[] list)
		{
			Debug.Log(String.Format(typeof(JSIVariableAnimator).Name + ": " + line, list));
		}

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

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight ||
			    vessel != FlightGlobals.ActiveVessel)
				return;

			// Let's see if it works that way first.
			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (!UpdateCheck())
				return;

			float scaleBottom = 0;
			float scaleTop = 0;
			float varValue = 0;
			try {
				scaleBottom = scalePoints[0] ?? (float)(double)comp.ProcessVariable(varName[0]);
				scaleTop = scalePoints[1] ?? (float)(double)comp.ProcessVariable(varName[1]);
				varValue = (float)(double)comp.ProcessVariable(variableName);
			} catch (InvalidCastException e) {
				LogMessage("Error, one of the variables failed to produce a usable number. {0}", e);
			}

			float scaledValue = Mathf.InverseLerp(scaleBottom, scaleTop, varValue);
			if (thresholdMode) {
				if (scaledValue >= threshold.x && scaledValue <= threshold.y) {
					if (audioOutput != null && !alarmActive) {
						audioOutput.audio.Play();
						alarmActive = true;
					}
					anim[animationName].normalizedTime = 1;
				} else {
					anim[animationName].normalizedTime = 0;
					if (audioOutput != null) {
						audioOutput.audio.Stop();
						alarmActive = false;
					}
				}

			} else {
				anim[animationName].normalizedTime = Mathf.Lerp(0, 1f, scaledValue);
			}

		}
	}
}

