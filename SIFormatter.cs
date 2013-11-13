using System;

namespace StringFormatter
{
	public class SIFormatProvider : IFormatProvider, ICustomFormatter
	{
		public object GetFormat (Type formatType)
		{
			if (formatType == typeof(ICustomFormatter))
				return this;
			return null;
		}
		// So our format is:
		// SIP05.3
		// Where: SI is the constant prefix indicating format.
		// 5 is the length of the entire string counting the suffix.
		// 3 is the number of digits after the decimal point.
		// 0 means that string is to be right-justified with zeroes
		// otherwise spaces will be used.
		private const string formatPrefix = "SIP";

		public string Format (string format, object arg, IFormatProvider formatProvider)
		{    
			if (format == null || !format.StartsWith (formatPrefix, StringComparison.Ordinal)) {    
				return defaultFormat (format, arg, formatProvider);    
			}

			if (arg is string) {    
				return defaultFormat (format, arg, formatProvider);    
			}

			double inputValue;

			try {    
				inputValue = Convert.ToDouble (arg);    
			} catch (InvalidCastException) {    
				return defaultFormat (format, arg, formatProvider);    
			}

			//int totalDigits = (int)Math.Floor (Math.Log10 (inputValue) + 1);

			string formatData = format.Substring (formatPrefix.Length);

			bool zeroPad = false;
			if (formatData.Length > 0 && formatData [0] == '0') {
				// First character is zero, padding with zeroes
				zeroPad = true;
				formatData = formatData.Substring (1);
			}
			ushort stringLength = 6;
			ushort significantFigures = 2;

			if (formatData.IndexOf ('.') > 0) {
				string[] tokens = formatData.Split ('.');
				UInt16.TryParse (tokens [0], out stringLength);
				UInt16.TryParse (tokens [1], out significantFigures);
			} else {
				UInt16.TryParse (formatData, out stringLength);
			}


			return ConvertToSI (inputValue, (stringLength == 0) ? -1 : stringLength - 1, significantFigures).PadLeft (stringLength, zeroPad ? '0' : ' ');


		}

		private static string defaultFormat (string format, object arg, IFormatProvider formatProvider)
		{
			IFormattable formattableArg = arg as IFormattable;
			if (formattableArg != null) {
				return formattableArg.ToString (format, formatProvider);
			}
			return arg.ToString ();
		}
		// Once again MechJeb code comes to the rescue!
		//Puts numbers into SI format, e.g. 1234 -> "1.234 k", 0.0045678 -> "4.568 m"
		//maxPrecision is the exponent of the smallest place value that will be shown; for example
		//if maxPrecision = -1 and digitsAfterDecimal = 3 then 12.345 will be formatted as "12.3"
		//while 56789 will be formated as "56.789 k"
		private static string ConvertToSI (double d, int maxPrecision = -99, int sigFigs = 4)
		{
			if (d == 0 || double.IsInfinity (d) || double.IsNaN (d))
				return d.ToString () + " ";

			int exponent = (int)Math.Floor (Math.Log10 (Math.Abs (d))); //exponent of d if it were expressed in scientific notation

			string[] units = { "y", "z", "a", "f", "p", "n", "μ", "m", "", "k", "M", "G", "T", "P", "E", "Z", "Y" };
			const int unitIndexOffset = 8; //index of "" in the units array
			int unitIndex = (int)Math.Floor (exponent / 3.0) + unitIndexOffset;
			if (unitIndex < 0)
				unitIndex = 0;
			if (unitIndex >= units.Length)
				unitIndex = units.Length - 1;
			string unit = units [unitIndex];

			int actualExponent = (unitIndex - unitIndexOffset) * 3; //exponent of the unit we will us, e.g. 3 for k.
			d /= Math.Pow (10, actualExponent);

			int digitsAfterDecimal = sigFigs - (int)(Math.Ceiling (Math.Log10 (Math.Abs (d))));

			if (digitsAfterDecimal > actualExponent - maxPrecision)
				digitsAfterDecimal = actualExponent - maxPrecision;
			if (digitsAfterDecimal < 0)
				digitsAfterDecimal = 0;

			string ret = d.ToString ("F" + digitsAfterDecimal) + unit;

			return ret;
		}
	}
}

