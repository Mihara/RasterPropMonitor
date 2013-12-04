using System;
using System.Text;

namespace JSI
{
	public class SIFormatProvider: IFormatProvider, ICustomFormatter
	{
		public object GetFormat(Type formatType)
		{
			return formatType == typeof(ICustomFormatter) ? this : null;
		}

		private const string formatPrefixSIP = "SIP";
		private const string formatPrefixDMS = "DMS";
		private const string formatPrefixKDT = "KDT";

		public string Format(string format, object arg, IFormatProvider formatProvider)
		{    

			if (format == null || arg is string) {    
				return DefaultFormat(format, arg, formatProvider);    
			}

			// But otherwise we're dealing with something we can cast to a double.
			// And if we can't, we aren't dealing with it.
			double inputValue;

			try {    
				inputValue = Convert.ToDouble(arg);
			} catch (InvalidCastException) {    
				return DefaultFormat(format, arg, formatProvider);    
			}

			// This way we can chain prefixes for other KSP-specific formats,
			// like Kerbal Time or degrees-minutes-seconds, which are internally
			// also doubles, right.
			if (format.StartsWith(formatPrefixSIP, StringComparison.Ordinal))
				return SIPFormat(format, inputValue);
			if (format.StartsWith(formatPrefixDMS, StringComparison.Ordinal))
				return DMSFormat(format, inputValue);
			if (format.StartsWith(formatPrefixKDT, StringComparison.Ordinal))
				return KDTFormat(format, inputValue);


			return DefaultFormat(format, arg, formatProvider);
		}
		// KDT -- Kerbal Date/Time format.
		// y - years
		// d - days
		// D - whole days.
		// h - hours
		// H - whole hours.
		// m - minutes
		// M - whole minutes.
		// s - seconds
		// S - whole seconds
		// f - fractional seconds.
		// Repeat of a character means 'pad to this number of characters with zeros.'
		// - - sign of the date/time span, space if the span is positive.
		// + - sign of the date/time span, plus if the span is positive
		private static string KDTFormat(string format, double seconds)
		{

			if (double.IsNaN(seconds) || double.IsInfinity(seconds))
				return string.Empty;

			bool positive = true && seconds >= 0;
			seconds = Math.Abs(seconds);

			const int minuteLength = 60;
			const int hourLength = 60 * minuteLength;
			const int dayLength = 24 * hourLength;
			const int yearLength = 365 * dayLength;

			int years = (int)Math.Floor(seconds / yearLength);
			int wholeDays = (int)Math.Floor(seconds / dayLength);
			int wholeHours = (int)Math.Floor(seconds / hourLength);
			int wholeMinutes = (int)Math.Floor(seconds / minuteLength);
			int wholeSeconds = (int)Math.Floor(seconds);
			double fracSeconds = seconds - wholeSeconds;

			seconds = wholeSeconds - years * yearLength;
			int days = (int)Math.Floor(seconds / dayLength);
			seconds -= days * dayLength;
			int hours = (int)Math.Floor(seconds / hourLength);
			seconds -= hours * hourLength;
			int minutes = (int)Math.Floor(seconds / minuteLength);
			seconds -= minutes * minuteLength;
			// So now we should have years, wholeDays/days, wholeHours/hours, wholeMinutes/minutes, wholeSeconds/seconds and fracSeconds....

			var result = new StringBuilder();
			var formatChars = format.ToCharArray();
			for (int i = formatPrefixKDT.Length; i < formatChars.Length; i++) {
				switch (formatChars[i]) {
					case '+':
						if (positive)
							result.Append('+');
						else
							result.Append('-');
						break;
					case '-':
						if (positive)
							result.Append(' ');
						else
							result.Append('-');
						break;
					case 'y':
						i += AppendRepeated(formatChars, result, years, 'y', i);
						break;
					case 'd':
						i += AppendRepeated(formatChars, result, days, 'd', i);
						break;
					case 'D':
						i += AppendRepeated(formatChars, result, wholeDays, 'D', i);
						break;
					case 'h':
						i += AppendRepeated(formatChars, result, hours, 'h', i);
						break;
					case 'H':
						i += AppendRepeated(formatChars, result, wholeHours, 'H', i);
						break;
					case 'm':
						i += AppendRepeated(formatChars, result, minutes, 'm', i);
						break;
					case 'M':
						i += AppendRepeated(formatChars, result, wholeMinutes, 'M', i);
						break;
					case 's':
						i += AppendRepeated(formatChars, result, (int)seconds, 's', i);
						break;
					case 'S':
						i += AppendRepeated(formatChars, result, wholeSeconds, 'S', i);
						break;
					case 'f':
						int count = CountRepeats(formatChars, i, 'f');
						result.Append(fracSeconds.ToString(".".PadRight(20,'0')).Substring(1,count));
						i += count - 1;
						break;
					default:
						result.Append(formatChars[i]);
						break;
				}
			}

			return result.ToString();
		}

		private static int AppendRepeated(char[] thatArray, StringBuilder result, int value, char formatChar, int start) {
			int count = CountRepeats(thatArray, start, formatChar);
			if (count == 1) {
				result.Append(value.ToString());
			} else {
				result.Append(value.ToString().PadLeft(count, '0'));
			}
			return count-1;
		}

		private static int CountRepeats(char[] thatArray, int start, char thatChar)
		{
			int i;
			for (i = start; i < thatArray.Length; i++) {
				if (thatArray[i] != thatChar)
					break;
			}
			return i - start;
		}
		// Mihara: So we define format like this:
		// DMS -- format prefix, signifies this is a degrees-minutes-seconds value.
		// N,S,E,W -- will be replaced by the correct sign character.
		//            N and S will mean latitude is used, E and W will mean longitude is used.
		// + is replaced by the appropriate degree-minute-second symbol
		// when used after d,m and s.
		// d - degrees, no padding.
		// dd - degrees, zero padding.
		// m - minutes, no padding
		// mm - minutes, zero padding.
		// s - seconds, no padding.
		// ss - seconds, zero padding.
		// All other symbols seen are copied into the string as is.
		// Format string that duplicated prior behaviour for longitude is DMSd+ mm+ ss+ E
		// Format string that duplicated prior behaviour for latitude is DMSd+ mm+ ss+ N
		private static string DMSFormat(string format, double angle)
		{

			if (double.IsInfinity(angle) || double.IsNaN(angle))
				return string.Empty;

			// First calculate our values, then go in order.

			int degrees = (int)Math.Floor(Math.Abs(angle));
			int minutes = (int)Math.Floor(60 * (Math.Abs(angle) - degrees));
			int seconds = (int)Math.Floor(3600 * (Math.Abs(angle) - degrees - minutes / 60d));
			var result = new StringBuilder();
			var formatChars = format.ToCharArray();


			for (int i = formatPrefixDMS.Length; i < formatChars.Length; i++) {
				switch (formatChars[i]) {
					case 'd':
						if (i < formatChars.Length - 1 && formatChars[i + 1] == 'd') {
							i++;
							result.Append(degrees.ToString().PadLeft(3, '0'));
						} else
							result.Append(degrees);
						break;
					case 'm':
						if (i < formatChars.Length - 1 && formatChars[i + 1] == 'm') {
							i++;
							result.Append(minutes.ToString().PadLeft(2, '0'));
						} else
							result.Append(minutes);
						break;
					case 's':
						if (i < formatChars.Length - 1 && formatChars[i + 1] == 's') {
							i++;
							result.Append(seconds.ToString().PadLeft(2, '0'));
						} else
							result.Append(seconds);
						break;
					case '+':
						if (i > formatPrefixDMS.Length) {
							switch (formatChars[i - 1]) {
								case 'd':
									result.Append('°');
									break;
								case 'm':
									result.Append("'");
									break;
								case 's':
									result.Append('"');
									break;
								default:
									result.Append(formatChars[i]);
									break;
							}
						} else {
							result.Append(formatChars[i]);
						}
						break;
					case 'N':
					case 'S':
						result.Append((angle >= 0) ? 'N' : 'S');
						break;
					case 'E':
					case 'W':
						result.Append((angle >= 0) ? 'E' : 'W');
						break;
					default:
						result.Append(formatChars[i]);
						break;
				}
			}

			return result.ToString();
		}
		// Mihara: SIP format spec is:
		// SIP_05.3
		// Where: SI is the constant prefix indicating format.
		// 5 is the length of the entire string counting the suffix.
		// 3 is the number of digits after the decimal point.
		// 0 means that string is to be right-justified with zeroes
		// otherwise spaces will be used.
		// If there is an underscore, a space will be inserted
		// before the SI suffix, otherwise, no space.
		// This space will count towards the full length of the string.
		// MOARdV rewrite of Format():
		// SIPFormat is based on the MechJeb SI formatting function.  It diverges
		// from MechJeb in that it allows for specified precision after the
		// decimal.  The formatting behavior is as follows:
		//
		// If no decimal specifier is provided, Format will set the decimal
		// specifier to the number of digits to the left of the decimal place
		// in the value we are formatting after accounting for the SI
		// adjustment.  That is, 123456 will be formatted as 123.456 k, and so
		// will 123456.2.  On the other hand, 0.0123 will be 12 m.  This
		// behavior mimics MechJeb.
		//
		// If we figure out that the number will not fit in the space requested,
		// we truncate decimal places.  After truncating, if we still can't
		// fit, well, too bad.
		//
		// For a non-negative value, the smallest format that will fit in the
		// requested format is SIP4.0.  For a value that can be negative, the
		// smallest format is SIP5.0.
		public string SIPFormat(string format, double inputValue)
		{

			// Handle degenerate values (NaN, INF)
			if (double.IsInfinity(inputValue) || double.IsNaN(inputValue)) {
				return inputValue + " ";
			}

			// Get some metrics on the number we are formatting:

			// leadingDigitExponent is the location relative to the original
			// decimal place for the leading digit.
			int leadingDigitExponent = (int)Math.Floor(Math.Log10(Math.Abs(inputValue)));
			// Analysis disable once CompareOfFloatsByEqualityOperator
			if (inputValue == 0d) {
				// special case: can't take log(0).
				leadingDigitExponent = 0;
			}
			// siExponent is the location relative to the original decimal of
			// the SI prefix.  Is is always the greatest multiple-of-3 less
			// than the leadingDigitExponent.
			int siExponent = ((int)Math.Floor(leadingDigitExponent / 3.0)) * 3;

			bool isNegative = (inputValue < 0.0);

			string formatData = format.Substring(formatPrefixSIP.Length);

			bool spaceBeforePrefix = false;
			if (formatData.Length > 0 && formatData[0] == '_') {
				// First character is underscore, so we need a space.
				formatData = formatData.Substring(1);
				spaceBeforePrefix = true;
			}

			// If there's a zero, pad with zeros -- otherwise spaces.
			bool zeroPad = false;
			if (formatData.Length > 0 && formatData[0] == '0') {
				// First character is zero, padding with zeroes
				zeroPad = true;
				formatData = formatData.Substring(1);
			}

			int stringLength;
			int postDecimal;

			if (formatData.IndexOf('.') > 0) {
				string[] tokens = formatData.Split('.');
				Int32.TryParse(tokens[0], out stringLength);
				Int32.TryParse(tokens[1], out postDecimal);
			} else {
				Int32.TryParse(formatData, out stringLength);
				// Don't care postDecimal: mimic the MJ SI formatter by setting
				// it to the number of digits to the left of the original decimal.
				postDecimal = Math.Max(0, siExponent);
			}

			// Figure out our character budget:
			// The number of digits to the left of the decimal (1 + leadingDigitExponent-siExponent)
			// Plus the sign character (if present)
			// Plus the space before the suffix (if present)
			// Plus the siExponent (if present)
			int charactersRequired = 1 + (leadingDigitExponent - siExponent) + (isNegative ? 1 : 0) + ((siExponent != 0) ? 1 : 0) + (spaceBeforePrefix ? 1 : 0);

			if (charactersRequired >= stringLength) {
				// We can't fit in the required space, so we will overflow and
				// drop and decimal values.
				postDecimal = 0;
			} else if (postDecimal > 0) {
				// We will prioritize fitting into the overall character budget:
				// -1 to account for the '.'
				int decimalCharactersAvailable = (stringLength - charactersRequired) - 1;

				postDecimal = Math.Min(postDecimal, decimalCharactersAvailable);
			}
			//charactersRequired += (postDecimal > 0) ? (postDecimal + 1) : 0;

			double scaledInputValue = inputValue / Math.Pow(10.0, siExponent);

			var result = new StringBuilder(scaledInputValue.ToString("F" + postDecimal));

			if (spaceBeforePrefix) {
				result.Append(" ");
			}
			if (siExponent != 0) {
				result.Append(GetSIPrefix(siExponent));
			}

			var resultStr = result.ToString();
			// MOARdV: This feels kind-of hacky, but I don't know C# formatting
			// tricks to find a cleaner way to do this.
			if (zeroPad && isNegative) {
				String zeros = "";
				// I have to add an extra '0' if there is no siExponent character
				zeros = zeros.PadRight(stringLength - resultStr.Length, '0');
				return resultStr.Insert(1, zeros);
			}
			return resultStr.PadLeft(stringLength, zeroPad ? '0' : ' ');
		}

		private static string GetSIPrefix(int siExponent)
		{
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

			int index = siExponent / 3 + unitIndexOffset;

			index = Math.Max(index, 0);
			index = Math.Min(index, units.Length - 1);

			return units[index];
		}

		private static string DefaultFormat(string format, object arg, IFormatProvider formatProvider)
		{
			var formattableArg = arg as IFormattable;
			return formattableArg != null ? formattableArg.ToString(format, formatProvider) : arg.ToString();
		}
	}
}

