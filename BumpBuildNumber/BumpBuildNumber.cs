using System.IO;
using System.Text.RegularExpressions;

// This is a neat little idea I found of how to deal with the problem of non-incrementing file version on StackOverflow
// It has the advantage of not relying on a particular build system and being completely optional.

namespace BumpBuildNumber
{
	class BumpBuildNumber
	{
		public static void Main(string[] args)
		{
			try {
				string FILE = @"SharedAssemblyInfo.cs";
				string text = File.ReadAllText(FILE);
				var regex = new Regex(@"(?<STATIC>\[assembly: AssemblyFileVersion\(""\d+\.\d+\.\d+\.)(?<BUILD>\d+)(?<TRAILER>""\)\])");
				var match = regex.Match(text);
				int buildNumber = int.Parse(match.Groups["BUILD"].Value) + 1;
				string newText = regex.Replace(text, "${STATIC}" + buildNumber + "${TRAILER}", 1);
				File.WriteAllText(FILE, newText);
			} catch {
			}
		}
	}
}
