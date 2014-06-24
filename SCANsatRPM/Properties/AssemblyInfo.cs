using System.Reflection;
using System.Runtime.CompilerServices;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.
[assembly: AssemblyTitle("SCANsatRPM")]
[assembly: AssemblyDescription ("RasterPropMonitor / SCANsat interface plugin for Kerbal Space Program")]

// For KSP purposes we are SCANsatRPM version 0.17...
[assembly: KSPAssembly("SCANsatRPM", 0, 17)]

// This depends on RPM 0.17...
[assembly: KSPAssemblyDependency("RasterPropMonitor", 0, 17)]
// And it depends on SCANsat, but the current version does not have a KSPAssembly statement...
