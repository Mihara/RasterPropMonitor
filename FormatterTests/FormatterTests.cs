/* This is a crude imitation of a formal testing methodology,
 * but it might suffice.
*/
using System;
using JSI;

namespace FormatterTests
{
	class MainClass
	{
		private static readonly SIFormatProvider fp = new SIFormatProvider();

		public static void Main(string[] args)
		{
			double[] values = {
				1d,
				1000000d,
			};

			foreach (double value in values) {
				Console.WriteLine(string.Format(fp, "{0:SIP_6}", value));
			}
		}
	}
}
