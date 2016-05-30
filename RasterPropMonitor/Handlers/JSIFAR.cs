/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;

namespace JSI
{
    class JSIFAR : IJSIModule
    {
        private static readonly bool farFound;

        // FARAPI.ActiveVesselAoA()
        private static readonly MethodInfo farActiveVesselAoA;
        // FARAPI.ActiveVesselDynPres()
        private static readonly MethodInfo farActiveVesselDynPres;
        // FARAPI.ActiveVesselSideslip()
        private static readonly MethodInfo farActiveVesselSideslip;
        // FARAPI.ActiveVesselTermVelEst()
        private static readonly MethodInfo farActiveVesselTermVelEst;
        //public static void VesselIncreaseFlapDeflection(Vessel v)
        private static readonly MethodInfo farIncreaseFlapDeflection;
        //public static void VesselDecreaseFlapDeflection(Vessel v)
        private static readonly MethodInfo farDecreaseFlapDeflection;
        //public static int VesselFlapSetting(Vessel v)
        private static readonly MethodInfo farGetFlapSetting;
        //public static void VesselSetSpoilers(Vessel v, bool spoilerActive)
        private static readonly MethodInfo farSetSpoilers;
        //public static bool VesselSpoilerSetting(Vessel v)
        private static readonly MethodInfo farGetSpoilerSetting;
        //public static FlightGUI VesselFlightInfo(Vessel v)
        private static readonly MethodInfo farGetVesselFlightGUI;
        //public VesselFlightInfo InfoParameters get()
        private static readonly MethodInfo farGetVesselFlightInfo;

        private static readonly FieldInfo flightInfoDragForce;
        private static readonly FieldInfo flightInfoLiftForce;

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

                farIncreaseFlapDeflection = farAPI_t.GetMethod("VesselIncreaseFlapDeflection", BindingFlags.Static | BindingFlags.Public);
                if (farIncreaseFlapDeflection == null)
                {
                    throw new NotImplementedException("farIncreaseFlapDeflection");
                }

                farDecreaseFlapDeflection = farAPI_t.GetMethod("VesselDecreaseFlapDeflection", BindingFlags.Static | BindingFlags.Public);
                if (farDecreaseFlapDeflection == null)
                {
                    throw new NotImplementedException("farDecreaseFlapDeflection");
                }

                farGetFlapSetting = farAPI_t.GetMethod("VesselFlapSetting", BindingFlags.Static | BindingFlags.Public);
                if (farGetFlapSetting == null)
                {
                    throw new NotImplementedException("farGetFlapSetting");
                }

                farSetSpoilers = farAPI_t.GetMethod("VesselSetSpoilers", BindingFlags.Static | BindingFlags.Public);
                if (farSetSpoilers == null)
                {
                    throw new NotImplementedException("farSetSpoilers");
                }

                farGetSpoilerSetting = farAPI_t.GetMethod("VesselSpoilerSetting", BindingFlags.Static | BindingFlags.Public);
                if (farGetSpoilerSetting == null)
                {
                    throw new NotImplementedException("farGetSpoilerSetting");
                }

                farGetVesselFlightGUI = farAPI_t.GetMethod("VesselFlightInfo", BindingFlags.Static | BindingFlags.Public);
                if (farGetVesselFlightGUI == null)
                {
                    throw new NotImplementedException("farGetVesselFlightInfo");
                }

                Type FlightGUI_t = loadedFARAPIAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "FerramAerospaceResearch.FARGUI.FARFlightGUI.FlightGUI");
                if (FlightGUI_t == null)
                {
                    throw new NotImplementedException("FlightGUI_t");
                }
                PropertyInfo fgInfoGui = FlightGUI_t.GetProperty("InfoParameters", BindingFlags.Instance | BindingFlags.Public); ;
                if (fgInfoGui != null)
                {
                    farGetVesselFlightInfo = fgInfoGui.GetGetMethod();
                }
                if (farGetVesselFlightInfo == null)
                {
                    throw new NotImplementedException("farGetVesselFlightInfo");
                }

                Type FlightInfo_t = loadedFARAPIAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "FerramAerospaceResearch.FARGUI.FARFlightGUI.VesselFlightInfo");
                if (FlightInfo_t == null)
                {
                    throw new NotImplementedException("FlightInfo_t");
                }
                flightInfoLiftForce = FlightInfo_t.GetField("liftForce");
                if (flightInfoLiftForce == null)
                {
                    throw new NotImplementedException("flightInfoLiftForce");
                }
                flightInfoDragForce = FlightInfo_t.GetField("dragForce");
                if (flightInfoDragForce == null)
                {
                    throw new NotImplementedException("flightInfoDragForce");
                }

                farFound = true;
            }
            catch (Exception e)
            {
                farFound = false;
                JUtil.LogMessage(null, "JSIFAR: Exception triggered when configuring: {0}", e);
            }

        }

        public JSIFAR(Vessel myVessel)
        {
            vessel = myVessel;
            JUtil.LogMessage(this, "A supported version of FAR is {0}", (farFound) ? "present" : "not available");
        }

        #region Private Methods
        private void SetFlaps(int newSetting)
        {
            int currentSetting = (int)GetFlapSetting();
            if (currentSetting >= 0)
            {
                int delta = newSetting - currentSetting;
                if (delta < 0 && farDecreaseFlapDeflection != null)
                {
                    for (int i = 0; i > delta; --i)
                    {
                        farDecreaseFlapDeflection.Invoke(null, new object[] { vessel });
                    }
                }
                else if (delta > 0 && farIncreaseFlapDeflection != null)
                {
                    for (int i = 0; i < delta; ++i)
                    {
                        farIncreaseFlapDeflection.Invoke(null, new object[] { vessel });
                    }
                }
            }
        }
        #endregion

        #region Information Queries
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

        public double GetDragForce()
        {
            if (farFound)
            {
                object flightGUI = farGetVesselFlightGUI.Invoke(null, new object[] { vessel });
                if (flightGUI != null)
                {
                    object flightInfo = farGetVesselFlightInfo.Invoke(flightGUI, null);
                    if (flightInfo != null)
                    {
                        object dragForce = flightInfoDragForce.GetValue(flightInfo);
                        if (dragForce != null)
                        {
                            return (double)dragForce;
                        }
                    }
                }
            }

            return double.NaN;
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

        public double GetLiftForce()
        {
            if (farFound)
            {
                object flightGUI = farGetVesselFlightGUI.Invoke(null, new object[] { vessel });
                if (flightGUI != null)
                {
                    object flightInfo = farGetVesselFlightInfo.Invoke(flightGUI, null);
                    if (flightInfo != null)
                    {
                        object liftForce = flightInfoLiftForce.GetValue(flightInfo);
                        if (liftForce != null)
                        {
                            return (double)liftForce;
                        }
                    }
                }
            }

            return double.NaN;
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
                double termVel = (double)farActiveVesselTermVelEst.Invoke(null, null);
                return termVel;
            }
            else
            {
                return -1.0;
            }
        }

        public double GetFlapSetting()
        {
            if (farFound)
            {
                // MOARdV: Temporary until next FAR release
                if (farGetFlapSetting != null)
                {
                    int setting = (int)farGetFlapSetting.Invoke(null, new object[] { vessel });
                    return (double)setting;
                }
            }

            return -1.0;
        }
        #endregion

        #region Control Interface
        public void IncreaseFlapSetting(bool state)
        {
            // If flaps can be increased, IncreaseFlapSettingState returns
            // true, which JSIActionGroupSwitch negates.
            if (farFound && !state)
            {
                // MOARdV: Temporary until next FAR release
                if (farIncreaseFlapDeflection != null)
                {
                    farIncreaseFlapDeflection.Invoke(null, new object[] { vessel });
                }
            }
        }

        public bool IncreaseFlapSettingState()
        {
            if (farFound)
            {
                int flapSetting = (int)GetFlapSetting();
                // MOARdV: Hardcoded magic numbers = bad.
                return (flapSetting < 3 && flapSetting >= 0);
            }

            return false;
        }

        public void DecreaseFlapSetting(bool state)
        {
            // If flaps can be increased, DecreaseFlapSettingState returns
            // true, which JSIActionGroupSwitch negates.
            if (farFound && !state)
            {
                // MOARdV: Temporary until next FAR release
                if (farDecreaseFlapDeflection != null)
                {
                    farDecreaseFlapDeflection.Invoke(null, new object[] { vessel });
                }
            }
        }

        public bool DecreaseFlapSettingState()
        {
            if (farFound)
            {
                int flapSetting = (int)GetFlapSetting();
                // MOARdV: Hardcoded magic numbers = bad.
                return (flapSetting > 0);
            }

            return false;
        }

        public void SetFlaps0(bool unused)
        {
            SetFlaps(0);
        }

        public void SetFlaps1(bool unused)
        {
            SetFlaps(1);
        }

        public void SetFlaps2(bool unused)
        {
            SetFlaps(2);
        }

        public void SetFlaps3(bool unused)
        {
            SetFlaps(3);
        }

        public void SetSpoiler(bool state)
        {
            if (farFound)
            {
                // MOARdV: Temporary until next FAR release
                if (farSetSpoilers != null)
                {
                    farSetSpoilers.Invoke(null, new object[] { vessel, state });
                }
            }
        }

        public bool GetSpoilerState()
        {
            if (farFound)
            {
                // MOARdV: Temporary until next FAR release
                if (farGetSpoilerSetting != null)
                {
                    return (bool)farGetSpoilerSetting.Invoke(null, new object[] { vessel });
                }
            }

            return false;
        }
        #endregion
    }
}
