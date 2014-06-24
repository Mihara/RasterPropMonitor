using System.Reflection;
using System.Runtime.CompilerServices;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.
[assembly: AssemblyTitle("MechJebRPM")]
[assembly: AssemblyDescription("RasterPropMonitor / MechJeb2 interface plugin for Kerbal Space Program")]

// For KSP purposes we are MechJebRPM version 0.17...
[assembly: KSPAssembly("MechJebRPM", 0, 17)]
// Depends on RPM 0.17 and MechJeb 2.2.
// Supposedly these help avoid the problems of plugin loading order.
[assembly: KSPAssemblyDependency("RasterPropMonitor", 0, 17)]
[assembly: KSPAssemblyDependency("MechJeb2", 2, 2)]