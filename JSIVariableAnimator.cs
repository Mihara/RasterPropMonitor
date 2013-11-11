using System;
using UnityEngine;
using System.Linq;

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
		public Vector2 scale;
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
			if (scale == null)
				LogMessage("Configuration error -- please check your scale setting.");
			// I hate copypaste, but what can you do.
			if (part != null) {
				foreach (InternalProp prop in part.internalModel.props) {
					RasterPropMonitorComputer other = prop.FindModelComponent<RasterPropMonitorComputer>();
					if (other != null) {
						LogMessage("Found an existing calculator instance, using that.");
						comp = other;
						break;
					}
				}
			}

			if (comp == null) {
				LogMessage("Instantiating a new calculator.");
				base.internalProp.AddModule(typeof(RasterPropMonitorComputer).Name);
				comp = base.internalProp.FindModelComponent<RasterPropMonitorComputer>();
				if (comp == null) {
					LogMessage("Failed to instantiate a calculator, wtf?");
				}
			}

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
				anim[animationName].normalizedTime = Mathf.Lerp(0,1f,Mathf.InverseLerp(scale.x,scale.y,(float)comp.ProcessVariable(variableName)));
			} catch (InvalidCastException e) {
				LogMessage("ERROR - variable returned is not a usable number! Exception: {0}", e);
			}

		}
	}
}

