using System;

namespace JSI
{
	public class JSIFlightLog: InternalModule
	{
		[KSPField]
		public int screenWidth;
		[KSPField]
		public int screenHeight;
		[KSPField]
		public string pageTitle;
		private string response;
		private int lastCount;

		public string ShowLog()
		{
			if (FlightLogger.eventLog.Count != lastCount) {
				LogToBuffer();
			}
			return response;
			
		}

		public void Start()
		{
			if (!string.IsNullOrEmpty(pageTitle))
				pageTitle = pageTitle.Replace("<=", "{").Replace("=>", "}");
			LogToBuffer();
		}

		private void LogToBuffer()
		{
			// I think I coded this one backwards somehow, but eh, it's a gimmick.
			int activeScreenHeight = screenHeight;
			if (!string.IsNullOrEmpty(pageTitle)) {
				activeScreenHeight--;
			}
			lastCount = FlightLogger.eventLog.Count;
			string fullLog = JUtil.WordWrap(string.Join(Environment.NewLine, FlightLogger.eventLog.ToArray()), screenWidth);
			string[] tempBuffer = fullLog.Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			string[] screenBuffer = new string[activeScreenHeight];
			if (tempBuffer.Length <= activeScreenHeight) {
				screenBuffer = tempBuffer;
			} else {
				for (int i = 0; i < screenBuffer.Length; i++) {
					screenBuffer[i] = tempBuffer[tempBuffer.Length - activeScreenHeight + i];

				}
			}
			response = string.Join(Environment.NewLine, screenBuffer);
			if (!string.IsNullOrEmpty(pageTitle)) {
				response = pageTitle + Environment.NewLine + response;
			}


		}

	}
}
