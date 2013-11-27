using System;
using System.Text;

namespace JSI
{
	public class SIFormatProvider : IFormatProvider, ICustomFormatter
	{
		public object GetFormat(Type formatType)
		{
			return formatType == typeof(ICustomFormatter) ? this : null;
		}
		// So our format is:
		// SIP_05.3
		// Where: SI is the constant prefix indicating format.
		// 5 is the length of the entire string counting the suffix.
		// 3 is the number of digits after the decimal point.
		// 0 means that string is to be right-justified with zeroes
		// otherwise spaces will be used.
		// If there is an underscore, a space will be inserted
		// before the SI suffix, otherwise, no space.
		// This space will count towards the full length of the string.
		private const string formatPrefix = "SIP";

		public string Format(string format, object arg, IFormatProvider formatProvider)
		{    
			if (format == null || !format.StartsWith(formatPrefix, StringComparison.Ordinal)) {    
				return DefaultFormat(format, arg, formatProvider);    
			}

			if (arg is string) {    
				return DefaultFormat(format, arg, formatProvider);    
			}

			// This is approaching a dangerous mess and needs a rewrite.

			double inputValue;

			try {    
				inputValue = Convert.ToDouble(arg);    
			} catch (InvalidCastException) {    
				return DefaultFormat(format, arg, formatProvider);    
			}

			string formatData = format.Substring(formatPrefix.Length);

			// We always lose one significant figure, not sure why.
			int stringLengthModifier = 1;

			// If we're using a space between the number and the prefix,
			// we lose one significant figure.
			if (formatData.Length > 0 && formatData[0] == '_') {
				// First character is underscore, so we need a space.
				stringLengthModifier++;
				formatData = formatData.Substring(1);
			}

			// If there's a zero, pad with zeros -- otherwise spaces.
			bool zeroPad = false;
			if (formatData.Length > 0 && formatData[0] == '0') {
				// First character is zero, padding with zeroes
				zeroPad = true;
				formatData = formatData.Substring(1);
			}

			ushort stringLength;
			ushort postDecimal = 0;

			if (formatData.IndexOf('.') > 0) {
				string[] tokens = formatData.Split('.');
				UInt16.TryParse(tokens[0], out stringLength);
				UInt16.TryParse(tokens[1], out postDecimal);
			} else {
				UInt16.TryParse(formatData, out stringLength);
			}

			// We lose one significant figure to negative sign.
			if (inputValue < 0)
				stringLengthModifier+=2;

			// If we have more digits than the string length as the result,
			// we're going to be getting a prefix, so we lose one more
			// significant figure.
			if (Math.Floor(Math.Log10(inputValue) + 1) > stringLength)
				stringLengthModifier++;

			return ConvertToSI(inputValue, 
				-postDecimal, stringLength - stringLengthModifier).PadLeft(stringLength, zeroPad ? '0' : ' ');


		}

		private static string DefaultFormat(string format, object arg, IFormatProvider formatProvider)
		{
			var formattableArg = arg as IFormattable;
			return formattableArg != null ? formattableArg.ToString(format, formatProvider) : arg.ToString();
		}
		// Once again MechJeb code comes to the rescue with a function I very tenuously understand!
		//Puts numbers into SI format, e.g. 1234 -> "1.234 k", 0.0045678 -> "4.568 m"
		//maxPrecision is the exponent of the smallest place value that will be shown; for example
		//if maxPrecision = -1 and digitsAfterDecimal = 3 then 12.345 will be formatted as "12.3"
		//while 56789 will be formated as "56.789 k"
		private static string ConvertToSI(double d, int maxPrecision = -99, int sigFigs = 4, bool needSpace = false)
		{
			// Analysis disable once CompareOfFloatsByEqualityOperator
			if (d == 0 || double.IsInfinity(d) || double.IsNaN(d))
				return d + " ";

			int exponent = (int)Math.Floor(Math.Log10(Math.Abs(d))); //exponent of d if it were expressed in scientific notation

			string[] units = {
				"y",
				"z",
				"a",
				"f",
				"p",
				"n",
				"\u00B5",//"μ", //Because unicode.
				"m",
				"",
				"k",
				"M",
				"G",
				"T",
				"P",
				"E",
				"Z",
				"Y"
			};
			const int unitIndexOffset = 8; //index of "" in the units array
			int unitIndex = (int)Math.Floor(exponent / 3.0) + unitIndexOffset;
			if (unitIndex < 0)
				unitIndex = 0;
			if (unitIndex >= units.Length)
				unitIndex = units.Length - 1;
			string unit = units[unitIndex];

			int actualExponent = (unitIndex - unitIndexOffset) * 3; //exponent of the unit we will us, e.g. 3 for k.
			d /= Math.Pow(10, actualExponent);

			int digitsAfterDecimal = sigFigs - (int)(Math.Ceiling(Math.Log10(Math.Abs(d))));

			if (digitsAfterDecimal > actualExponent - maxPrecision)
				digitsAfterDecimal = actualExponent - maxPrecision;
			if (digitsAfterDecimal < 0)
				digitsAfterDecimal = 0;

			var result = new StringBuilder(d.ToString("F" + digitsAfterDecimal));
			if (needSpace)
				result.Append(" ");
			result.Append(unit);

			return result.ToString();
		}
	}
}

