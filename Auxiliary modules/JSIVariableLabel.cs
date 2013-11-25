using UnityEngine;

namespace JSI
{
	public class JSIVariableLabel: InternalModule
	{

		[KSPField]
		public string labelText = "<=0=>$&$ALTITUDE";
		[KSPField]
		public string transformName;
		[KSPField]
		public float fontSize = 0.008f;
		[KSPField]
		public int refreshRate = 10;
		private InternalText textObj;
		private Transform textObjTransform;
		private RasterPropMonitorComputer comp;
		private int updateCountdown;
		private const string fontName = "Arial";
		private string sourceString;

		public void Start()
		{
			comp = JUtil.GetComputer(internalProp);
			textObjTransform = internalProp.FindModelTransform(transformName);
			textObj = InternalComponents.Instance.CreateText(fontName, fontSize, textObjTransform, string.Empty);
			sourceString = labelText.UnMangleConfigText();
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

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel != FlightGlobals.ActiveVessel)
				return;

			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
				return;

			if (!UpdateCheck())
				return;

			textObj.text.Text = StringProcessor.ProcessString(sourceString, comp);
		}
	}
}

