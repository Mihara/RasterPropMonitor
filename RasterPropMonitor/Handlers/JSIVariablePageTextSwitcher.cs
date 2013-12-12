using UnityEngine;

namespace JSI
{
	public class JSIVariablePageTextSwitcher: InternalModule
	{
		[KSPField]
		public string variableName;
		[KSPField]
		public string scale;
		[KSPField]
		public Vector2 threshold;
		[KSPField]
		public string definitionOut = string.Empty;
		[KSPField]
		public string definitionIn = string.Empty;
		[KSPField]
		public int refreshRate = 10;
		private string textOut, textIn;
		private readonly float?[] scalePoints = { null, null };
		private readonly string[] varName = { null, null };
		private readonly bool[] warningMade = { false, false, false };
		private bool pageActiveState;
		private bool isInThreshold;
		private int updateCountdown;
		private RasterPropMonitorComputer comp;
		// Analysis disable once UnusedParameter
		public string ShowPage(int width, int height)
		{
			return isInThreshold ? textIn : textOut; 
		}
		// Analysis disable once UnusedParameter
		public void PageActive(bool active, int pageNumber)
		{
			pageActiveState = active;
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
		// I don't like this mess of copypaste, but how can I improve it away?...
		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel) || !pageActiveState || !UpdateCheck())
				return;

			// Well, that looks a little like code reuse now.
			// But maybe I should abstract this away as a proper object later.
			float scaleBottom;
			if (!JSIVariableAnimator.MassageScalePoint(out scaleBottom, scalePoints[0], varName[0], ref warningMade[0], comp, this))
				return;

			float scaleTop;
			if (!JSIVariableAnimator.MassageScalePoint(out scaleTop, scalePoints[1], varName[1], ref warningMade[1], comp, this))
				return;

			float varValue;
			if (!JSIVariableAnimator.MassageScalePoint(out varValue, null, variableName, ref warningMade[2], comp, this))
				return;

			float scaledValue = Mathf.InverseLerp(scaleBottom, scaleTop, varValue);

			isInThreshold = (scaledValue >= threshold.x && scaledValue <= threshold.y);
		}

		public void Start()
		{
			textIn = JUtil.LoadPageDefinition(definitionIn);
			textOut = JUtil.LoadPageDefinition(definitionOut);
			comp = JUtil.GetComputer(internalProp);
		}
	}
}

