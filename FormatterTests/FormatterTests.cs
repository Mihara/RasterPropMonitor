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
				0.1234567,
				123456.7,
				1234567890.3,
				11234567890.2,
				90148.3,
				-123.45,
				-123456.7,
				-0.0001234,
				0.0123,
				-0.0123,
				0,
			};

			foreach (double value in values) {
				// If the formatter is working correctly, all of the '|'
				// characters will line up under the 'v'.
				Console.WriteLine("\nUnformatted: {0}\nAlignment Mark      v", value);
				Console.WriteLine(string.Format(fp, "SIP_6   :>{0:SIP_6}<   |", value));
				Console.WriteLine(string.Format(fp, "SIP_06  :>{0:SIP_06}<   |", value));
				Console.WriteLine(string.Format(fp, "SIP_6.3 :>{0:SIP_6.3}<   |", value));
				Console.WriteLine(string.Format(fp, "SIP_6.2 :>{0:SIP_6.2}<   |", value));
				Console.WriteLine(string.Format(fp, "SIP6    :>{0:SIP6}<   |", value));
				Console.WriteLine(string.Format(fp, "SIP6.3  :>{0:SIP6.3}<   |", value));
				Console.WriteLine(string.Format(fp, "SIP_9   :>{0:SIP_9}<|", value));
				Console.WriteLine(string.Format(fp, "SIP_9.3 :>{0:SIP_9.3}<|", value));
				Console.WriteLine(string.Format(fp, "SIP9.3  :>{0:SIP9.3}<|", value));
				Console.WriteLine(string.Format(fp, "SIP09.3 :>{0:SIP09.3}<|", value));
				Console.WriteLine(string.Format(fp, "SIP_10  :>{0:SIP_10}|", value));
			}
			Console.WriteLine("SIP tests done, press any key to start DMS tests");
			Console.ReadKey();
			Console.WriteLine();

			double[] degrees = {
				123.45,
				-123.45,
				1.234,
				0.1234,
				0,
			};


			foreach (double value in degrees) {
				Console.WriteLine(string.Format(fp,"DMSd°m's\":       {0:DMSd°m's\"} ({0})",value));
				Console.WriteLine(string.Format(fp,"DMSdd+m+s+E:     {0:DMSdd+m+s+E} ({0})",value));
				Console.WriteLine(string.Format(fp,"DMSdd+mm+ss+E:   {0:DMSdd+mm+ss+E} ({0})",value));
				Console.WriteLine(string.Format(fp,"DMSddNm+s+:      {0:DMSddNm+s+} ({0})",value));
				// The string as it was previously returned is...
				Console.WriteLine(string.Format(fp,"DMSd+ mm+ ss+ N: {0:DMSd+ mm+ ss+ N} ({0})",value));
			}
			Console.WriteLine("DMS tests done, press any key to start KDT tests");
			Console.ReadKey();
			Console.WriteLine();

			double[] seconds = {
				0,
				12,
				-12,
				1234,
				-1234,
				123456,
				-123456,
				12345.6789,
				-12345.6789,
			};

			foreach (double value in seconds) {
				// The full time format is...
				Console.WriteLine(string.Format(fp,"KDT+y:ddd:hh:mm:ss >{0:KDT+y:ddd:hh:mm:ss} ({0})",value));
				Console.WriteLine(string.Format(fp,"KDT-y:ddd:hh:mm:ss >{0:KDT-y:ddd:hh:mm:ss} ({0})",value));
				Console.WriteLine(string.Format(fp,"KDTy:d:h:m:s >{0:KDTy:d:h:m:s} ({0})",value));
				Console.WriteLine(string.Format(fp,"KDTS.f >{0:KDTS.f} ({0})",value));
				Console.WriteLine(string.Format(fp,"KDTS.fff >{0:KDTS.fff} ({0})",value));
			}


		}
	}
}
