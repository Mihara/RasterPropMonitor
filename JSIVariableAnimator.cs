using System;
using UnityEngine;
using System.Linq;


// Once again, GitHub application, stop being silly.

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
		private float?[] scalePoints = { null, null };
		private string[] varName = { null, null };
		private RasterPropMonitorComputer comp;
		private int updateCountdown;
		private Animation anim;

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

			for (int i=0; i<tokens.Length; i++) {
				float realValue;
				if (float.TryParse(tokens[i], out realValue)) {
					scalePoints[i] = realValue;
				} else {
					varName[i] = tokens[i].Trim();
				}

			}

			comp = JUtil.GetComputer(internalProp);

			anim = internalProp.FindModelAnimators(animationName).FirstOrDefault();
			anim.enabled = true;
			anim[animationName].speed = 0;
			anim.Play();
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
			try {
				anim[animationName].normalizedTime = Mathf.Lerp(0, 1f, Mathf.InverseLerp(
					scalePoints[0] ?? (float)(double)comp.ProcessVariable(varName[0]),
					scalePoints[1] ?? (float)(double)comp.ProcessVariable(varName[1]),
					(float)(double)comp.ProcessVariable(variableName)));
			} catch (InvalidCastException e) {
				LogMessage("Error, one of the variables failed to produce a usable number. {0}", e);
			}

		}
	}
}

