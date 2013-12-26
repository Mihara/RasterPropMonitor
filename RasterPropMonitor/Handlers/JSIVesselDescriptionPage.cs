
namespace JSI
{
	public class JSIVesselDescriptionPage: PartModule
	{
		[KSPField(isPersistant=true)]
		public string vesselDescription = string.Empty;

		private readonly string editorNewline = ((char)0x0a).ToString();

		// Analysis disable UnusedParameter
		public string RawScreen(int screenWidth, int screenHeight) {
		// Analysis restore UnusedParameter
			return vesselDescription.UnMangleConfigText();
		}

		// Analysis disable UnusedParameter
		public string WrappedScreen(int screenWidth, int screenHeight) {
		// Analysis restore UnusedParameter
			return JUtil.WordWrap(vesselDescription.UnMangleConfigText(),screenWidth);
		}

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsEditor)
				return;
			// I think it can't be null. But for some unclear reason, the newline in this case is always 0A, rather than Environment.NewLine.
			vesselDescription = EditorLogic.fetch.shipDescriptionField.Text.Replace(editorNewline,"$$$");
		}


	}
}

