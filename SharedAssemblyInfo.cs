using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct ("RasterPropMonitor")]
[assembly: AssemblyCopyright ("Copyright ©2013-2014 by Mihara and other contributors, released under the terms of GNU GPLv3")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.

// From now on this is the version-number-for-linking, and is no longer expected to change, for the benefit of people who need to hardlink to RPM anyway.
[assembly: AssemblyVersion("0.17.0.0")]

// Now this is the actual version number with build number.
// As I release newer ones, I'll bump them manually.
// The 8888 is temporary here to distinguish this from the last built dev build packet, 
// as I release 0.17, I'll switch the version to 0.17.
// Build number is altered automatically.
[assembly: AssemblyFileVersion("0.17.8888.14")]

// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.
//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]
