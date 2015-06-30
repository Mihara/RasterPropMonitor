using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;

namespace JSI
{
    class JSIFAR : IJSIModule
    {
        public JSIFAR(Vessel _vessel) : base(_vessel) { }

        private static readonly bool farFound;

        // FARAPI.ActiveVesselAoA()
        private static readonly MethodInfo farActiveVesselAoA;
        // FARAPI.ActiveVesselDynPres()
        private static readonly MethodInfo farActiveVesselDynPres;
        // FARAPI.ActiveVesselSideslip()
        private static readonly MethodInfo farActiveVesselSideslip;
        // FARAPI.ActiveVesselTermVelEst()
        private static readonly MethodInfo farActiveVesselTermVelEst;

        static JSIFAR()
        {
            try
            {
                var loadedFARAPIAssy = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name == "FerramAerospaceResearch");

                if (loadedFARAPIAssy == null)
                {
                    farFound = false;
                    return;
                }

                //--- Process all the reflection info
                // FARAPI
                Type farAPI_t = loadedFARAPIAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "FerramAerospaceResearch.FARAPI");
                if (farAPI_t == null)
                {
                    throw new NotImplementedException("farAPI_t");
                }

                farActiveVesselAoA = farAPI_t.GetMethod("ActiveVesselAoA", BindingFlags.Static | BindingFlags.Public);
                if (farActiveVesselAoA == null)
                {
                    throw new NotImplementedException("farActiveVesselAoA");
                }

                farActiveVesselDynPres = farAPI_t.GetMethod("ActiveVesselDynPres", BindingFlags.Static | BindingFlags.Public);
                if (farActiveVesselDynPres == null)
                {
                    throw new NotImplementedException("farActiveVesselDynPres");
                }

                farActiveVesselSideslip = farAPI_t.GetMethod("ActiveVesselSideslip", BindingFlags.Static | BindingFlags.Public);
                if (farActiveVesselSideslip == null)
                {
                    throw new NotImplementedException("farActiveVesselSideslip");
                }

                farActiveVesselTermVelEst = farAPI_t.GetMethod("ActiveVesselTermVelEst", BindingFlags.Static | BindingFlags.Public);
                if (farActiveVesselTermVelEst == null)
                {
                    throw new NotImplementedException("farActiveVesselTermVelEst");
                }

                farFound = true;
            }
            catch (Exception e)
            {
                farFound = false;
                JUtil.LogMessage(null, "JSIFAR: Exception triggered when configuring: " + e);
            }

            JUtil.LogMessage(null, "JSIFAR: farFound is " + farFound);
        }

        public double GetAngleOfAttack()
        {
            if (farFound)
            {
                return (double)farActiveVesselAoA.Invoke(null, null);
            }
            else
            {
                return double.NaN;
            }
        }

        public double GetDynamicPressure()
        {
            if (farFound)
            {
                return (double)farActiveVesselDynPres.Invoke(null, null);
            }
            else
            {
                return double.NaN;
            }
        }

        public double GetSideSlip()
        {
            if (farFound)
            {
                return (double)farActiveVesselSideslip.Invoke(null, null);
            }
            else
            {
                return double.NaN;
            }
        }

        public double GetTerminalVelocity()
        {
            if (farFound)
            {
                double value = (double)farActiveVesselTermVelEst.Invoke(null, null);
                return (double.IsNaN(value)) ? double.PositiveInfinity : value;
            }
            else
            {
                return double.NaN;
            }
        }
    }
}
