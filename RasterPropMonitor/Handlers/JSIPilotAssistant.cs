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
using System.Linq;
using System.Reflection;
using System.Text;

namespace JSI
{
    /**
     * Interace with the Pilot Assistant mod:
     * http://forum.kerbalspaceprogram.com/threads/100073
     * 
     */
    class JSIPilotAssistant : IJSIModule
    {
        static private readonly bool paFound;
        // AsstVesselModule.vesselAsst
        private static readonly FieldInfo vesselAsst_t;

        // PilotAssistant fields
        // Vertical control mode & enable
        private static readonly FieldInfo vertMode_t;
        private static readonly FieldInfo vertActive_t;
        private static readonly DynamicFuncDouble GetCurrentVert;
        private static readonly DynamicMethodDelegate SetVert;

        static JSIPilotAssistant()
        {
            try
            {
                var loadedPAAssy = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name == "PilotAssistant");
                if (loadedPAAssy == null)
                {
                    //JUtil.LogMessage(null, "Did not load PilotAssistant");
                    //var list = AssemblyLoader.loadedAssemblies;
                    //foreach(var a in list)
                    //{
                    //    JUtil.LogMessage(null, "-- {0}", a.name);
                    //}
                    return;
                }
                //else
                //{
                //    JUtil.LogMessage(null, "Did find PilotAssistant");
                //}

                Type paAsstVesselModule_t = loadedPAAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "PilotAssistant.FlightModules.AsstVesselModule");
                if (paAsstVesselModule_t == null)
                {
                    throw new NotImplementedException("paAsstVesselModule_t");
                }

                vesselAsst_t = paAsstVesselModule_t.GetField("vesselAsst", BindingFlags.Instance | BindingFlags.Public);
                if (vesselAsst_t == null)
                {
                    throw new NotImplementedException("vesselAsst_t");
                }

                Type paPilotAssistant_t = loadedPAAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "PilotAssistant.FlightModules.PilotAssistant");
                if (paPilotAssistant_t == null)
                {
                    throw new NotImplementedException("paPilotAssistant_t");
                }

                vertMode_t = paPilotAssistant_t.GetField("CurrentVertMode", BindingFlags.Instance | BindingFlags.Public);
                if (vertMode_t == null)
                {
                    throw new NotImplementedException("currentVertMode_t");
                }
                vertActive_t = paPilotAssistant_t.GetField("VertActive", BindingFlags.Instance | BindingFlags.Public);
                if (vertActive_t == null)
                {
                    throw new NotImplementedException("vertActive_t");
                }
                MethodInfo vertSetting_t = paPilotAssistant_t.GetMethod("GetCurrentVert", BindingFlags.Instance | BindingFlags.Public);
                if (vertSetting_t == null)
                {
                    throw new NotImplementedException("vertSetting_t");
                }
                GetCurrentVert = DynamicMethodDelegateFactory.CreateFuncDouble(vertSetting_t);
                MethodInfo setVert_t = paPilotAssistant_t.GetMethod("SetVert", BindingFlags.Instance | BindingFlags.Public);
                if (setVert_t == null)
                {
                    throw new NotImplementedException("setVert_t");
                }
                SetVert = DynamicMethodDelegateFactory.Create(setVert_t);
            }
            catch (Exception e)
            {
                JUtil.LogMessage(null, "Exception tripped: {0}", e);
                paFound = false;
                return;
            }

            paFound = true;
        }

        public JSIPilotAssistant()
        {
            JUtil.LogMessage(this, "A supported version of Pilot Assistant is {0}", (paFound) ? "present" : "not available");
        }

        // Defines taken directly from PilotAssistant for readability sake.
        public enum VertMode
        {
            ToggleOn = -1,
            Pitch = 0,
            VSpeed = 1,
            Altitude = 2,
            RadarAltitude = 3
        }

        public enum HrztMode
        {
            ToggleOn = -1,
            Bank = 0,
            Heading = 1,
            HeadingNum = 2
        }

        public enum ThrottleMode
        {
            ToggleOn = -1,
            Direct = 0,
            Acceleration = 1,
            Speed = 2
        }

        private object GetPilotAssistant()
        {
            object asstVesselModule = vessel.GetComponent("AsstVesselModule");
            object pilotAssistant = vesselAsst_t.GetValue(asstVesselModule);
            if (pilotAssistant == null)
            {
                throw new NullReferenceException("pilotAssistant");
            }

            return pilotAssistant;
        }

        //--- Vertical control modes -----------------------------------------
        public bool GetVertActive()
        {
            if(!paFound)
            {
                return false;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();

                object vertActive = vertActive_t.GetValue(pilotAssistant);

                return (bool)vertActive;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetVertActive: {0}", e);
            }
            return false;
        }

        public void SetVertActive(bool state)
        {
            if (!paFound)
            {
                return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();

                object vertMode = vertMode_t.GetValue(pilotAssistant);

                SetVert(pilotAssistant, new object[] { state, false, vertMode, 0.0 });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetVertActive: {0}", e);
            }
        }

        public void SetVertPitch(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
            object pilotAssistant = GetPilotAssistant();

            SetVert(pilotAssistant, new object[] { true, true, VertMode.Pitch, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetVertPitch: {0}", e);
            }
        }

        public void SetVertRate(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
            object pilotAssistant = GetPilotAssistant();

            SetVert(pilotAssistant, new object[] { true, true, VertMode.VSpeed, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetVertRate: {0}", e);
            }
        }

        public void SetVertAltitude(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
            object pilotAssistant = GetPilotAssistant();

            SetVert(pilotAssistant, new object[] { true, true, VertMode.Altitude, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetVertAltitude: {0}", e);
            }
        }

        public void SetVertRadarAltitude(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
            object pilotAssistant = GetPilotAssistant();

            SetVert(pilotAssistant, new object[] { true, true, VertMode.RadarAltitude, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetVertRadarAltitude: {0}", e);
            }
        }

        public double GetVertMode()
        {
            if (!paFound)
            {
                return 0.0;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                object vertMode = vertMode_t.GetValue(pilotAssistant);

                int vm = (int)vertMode;
                return (double)vm;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetVertMode: {0}", e);
            }
            return 0.0;
        }

        public double GetVertSetting()
        {
            if (!paFound)
            {
                return 0.0;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                return GetCurrentVert(pilotAssistant);
            }
            catch(Exception e)
            {
                JUtil.LogMessage(this, "GetVertSetting: {0}", e);
            }
            return 0.0;
        }
    }
}
