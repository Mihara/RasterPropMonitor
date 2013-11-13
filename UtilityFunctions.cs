using System;
using System.Text;

namespace JSI
{
	static class JUtil
	{
		public static RasterPropMonitorComputer GetComputer(InternalProp thatProp)
		{
			// I hate copypaste, and this is what I'm going to do about it.
			if (thatProp.part != null) {
				foreach (InternalProp prop in thatProp.part.internalModel.props) {
					RasterPropMonitorComputer other = prop.FindModelComponent<RasterPropMonitorComputer>();
					if (other != null) {
						return other;
					}
				}
			}
			thatProp.AddModule(typeof(RasterPropMonitorComputer).Name);
			return thatProp.FindModelComponent<RasterPropMonitorComputer>();
		}

		public static string WordWrap(string text, int maxLineLength)
		{
			StringBuilder sb = new StringBuilder();
			int currentIndex;
			int lastWrap;
			char[] prc = { ' ', ',', '.', '?', '!', ':', ';', '-' };
			char[] ws = { ' ' };

			foreach (string line in text.Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)) {
				currentIndex = 0;
				lastWrap = 0;
				do {
					currentIndex = lastWrap + maxLineLength > line.Length ? line.Length : (line.LastIndexOfAny(prc, Math.Min(line.Length - 1, lastWrap + maxLineLength)) + 1);
					if (currentIndex <= lastWrap)
						currentIndex = Math.Min(lastWrap + maxLineLength, line.Length);
					sb.AppendLine(line.Substring(lastWrap, currentIndex - lastWrap).Trim(ws));
					lastWrap = currentIndex;
				} while(currentIndex < line.Length);
			}
			return sb.ToString();
		}
	}
}

