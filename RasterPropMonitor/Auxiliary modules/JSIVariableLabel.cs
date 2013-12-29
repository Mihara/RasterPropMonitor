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
		// Annoying as it is, that is the only font actually available to InternalComponents for some bizarre reason,
		// even though I'm pretty sure there are quite a few other fonts in there.
		private const string fontName = "Arial";
		private string sourceString;

		public void Start()
		{
			comp = RasterPropMonitorComputer.Instantiate(internalProp);
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
			if (!JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
				return;

			textObj.text.Text = StringProcessor.ProcessString(sourceString, comp);
		}
	}
}

