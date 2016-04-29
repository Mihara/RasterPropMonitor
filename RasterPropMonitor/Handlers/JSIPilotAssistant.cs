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
     * Interface consists of several categories of methods: Get*Active,
     * Set*Active, Get*Mode, Get*Setting, and mode-specific setters.
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

        // Horizontal control mode & enable
        private static readonly FieldInfo horzMode_t;
        private static readonly FieldInfo horzActive_t;
        private static readonly DynamicFuncDouble GetCurrentHorz;
        private static readonly DynamicMethodDelegate SetHorz;

        // Horizontal control mode & enable
        private static readonly FieldInfo throttleMode_t;
        private static readonly FieldInfo throttleActive_t;
        private static readonly DynamicFuncDouble GetCurrentThrottle;
        private static readonly DynamicMethodDelegate SetThrottle;

        // Speed reference
        private static readonly FieldInfo speedRef_t;
        private static readonly DynamicMethodDelegate SetSpeedRef;

        // Speed units
        private static readonly FieldInfo speedUnits_t;
        private static readonly DynamicMethodDelegate SetSpeedUnits;

        // Pause
        private static readonly FieldInfo pauseState_t;
        private static readonly DynamicAction TogglePause;

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

                horzMode_t = paPilotAssistant_t.GetField("CurrentHrztMode", BindingFlags.Instance | BindingFlags.Public);
                if (horzMode_t == null)
                {
                    throw new NotImplementedException("horzMode_t");
                }
                horzActive_t = paPilotAssistant_t.GetField("HrztActive", BindingFlags.Instance | BindingFlags.Public);
                if (horzActive_t == null)
                {
                    throw new NotImplementedException("horzActive_t");
                }
                MethodInfo horzSetting_t = paPilotAssistant_t.GetMethod("GetCurrentHrzt", BindingFlags.Instance | BindingFlags.Public);
                if (horzSetting_t == null)
                {
                    throw new NotImplementedException("horzSetting_t");
                }
                GetCurrentHorz = DynamicMethodDelegateFactory.CreateFuncDouble(horzSetting_t);
                MethodInfo setHorz_t = paPilotAssistant_t.GetMethod("SetHrzt", BindingFlags.Instance | BindingFlags.Public);
                if (setHorz_t == null)
                {
                    throw new NotImplementedException("setHorz_t");
                }
                SetHorz = DynamicMethodDelegateFactory.Create(setHorz_t);

                throttleMode_t = paPilotAssistant_t.GetField("CurrentThrottleMode", BindingFlags.Instance | BindingFlags.Public);
                if (throttleMode_t == null)
                {
                    throw new NotImplementedException("throttleMode_t");
                }
                throttleActive_t = paPilotAssistant_t.GetField("ThrtActive", BindingFlags.Instance | BindingFlags.Public);
                if (throttleActive_t == null)
                {
                    throw new NotImplementedException("throttleActive_t");
                }
                MethodInfo throttleSetting_t = paPilotAssistant_t.GetMethod("GetCurrentThrottle", BindingFlags.Instance | BindingFlags.Public);
                if (throttleSetting_t == null)
                {
                    throw new NotImplementedException("throttleSetting_t");
                }
                GetCurrentThrottle = DynamicMethodDelegateFactory.CreateFuncDouble(throttleSetting_t);
                MethodInfo setThrottle_t = paPilotAssistant_t.GetMethod("SetThrottle", BindingFlags.Instance | BindingFlags.Public);
                if (setThrottle_t == null)
                {
                    throw new NotImplementedException("setThrottle_t");
                }
                SetThrottle = DynamicMethodDelegateFactory.Create(setThrottle_t);

                speedRef_t = paPilotAssistant_t.GetField("speedRef", BindingFlags.Instance | BindingFlags.NonPublic);
                if (speedRef_t == null)
                {
                    throw new NotImplementedException("PA speedRef_t");
                }
                MethodInfo changeSpeedRef_t = paPilotAssistant_t.GetMethod("ChangeSpeedRef", BindingFlags.Instance | BindingFlags.Public);
                if (changeSpeedRef_t == null)
                {
                    throw new NotImplementedException("PA changeSpeedRef_t");
                }
                SetSpeedRef = DynamicMethodDelegateFactory.Create(changeSpeedRef_t);

                speedUnits_t = paPilotAssistant_t.GetField("units", BindingFlags.Instance | BindingFlags.NonPublic);
                if (speedUnits_t == null)
                {
                    throw new NotImplementedException("PA speedUnits_t");
                }
                MethodInfo changeSpeedUnits_t = paPilotAssistant_t.GetMethod("ChangeSpeedUnit", BindingFlags.Instance | BindingFlags.Public);
                if (changeSpeedUnits_t == null)
                {
                    throw new NotImplementedException("PA changeSpeedUnits_t");
                }
                SetSpeedUnits = DynamicMethodDelegateFactory.Create(changeSpeedUnits_t);

                pauseState_t = paPilotAssistant_t.GetField("bPause", BindingFlags.Instance | BindingFlags.Public);
                if (pauseState_t == null)
                {
                    throw new NotImplementedException("PA pauseState_t");
                }
                MethodInfo TogglePause_t = paPilotAssistant_t.GetMethod("TogglePauseCtrlState", BindingFlags.Instance | BindingFlags.Public);
                if (TogglePause_t == null)
                {
                    throw new NotImplementedException("PA TogglePause_t");
                }
                TogglePause = DynamicMethodDelegateFactory.CreateAction(TogglePause_t);
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

        private enum SpeedRef
        {
            True,
            Indicated,
            Equivalent,
            Mach
        }

        public enum SpeedUnits
        {
            mSec,
            kmph,
            mph,
            knots,
            mach
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

        public void SetVertMode(double dmode)
        {
            if (!paFound)
            {
                return;
            }

            VertMode vertMode;
            switch((int)dmode)
            {
                case 0:
                    vertMode = VertMode.Pitch;
                    break;
                case 1:
                    vertMode = VertMode.VSpeed;
                    break;
                case 2:
                    vertMode = VertMode.Altitude;
                    break;
                case 3:
                    vertMode = VertMode.RadarAltitude;
                    break;
                default:
                    return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();
                object vertActive = vertActive_t.GetValue(pilotAssistant);

                SetVert(pilotAssistant, new object[] { vertActive, false, vertMode, 0.0 });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetVertMode: {0}", e);
            }
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

        //--- Horizontal control modes -----------------------------------------
        public bool GetHorzActive()
        {
            if (!paFound)
            {
                return false;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();

                object horzActive = horzActive_t.GetValue(pilotAssistant);

                return (bool)horzActive;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetHorzActive: {0}", e);
            }
            return false;
        }

        public void SetHorzActive(bool state)
        {
            if (!paFound)
            {
                return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();

                object horzMode = horzMode_t.GetValue(pilotAssistant);

                SetHorz(pilotAssistant, new object[] { state, false, horzMode, 0.0 });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetHorzActive: {0}", e);
            }
        }

        public void SetHorzBank(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                SetHorz(pilotAssistant, new object[] { true, true, HrztMode.Bank, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetHorzBank: {0}", e);
            }
        }

        public void SetHorzDirection(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                SetHorz(pilotAssistant, new object[] { true, true, HrztMode.Heading, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetHorzDirection: {0}", e);
            }
        }

        public void SetHorzHeading(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                SetHorz(pilotAssistant, new object[] { true, true, HrztMode.HeadingNum, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetHorzHeading: {0}", e);
            }
        }

        public double GetHorzMode()
        {
            if (!paFound)
            {
                return 0.0;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                object horzMode = horzMode_t.GetValue(pilotAssistant);

                int hm = (int)horzMode;
                return (double)hm;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetHorzMode: {0}", e);
            }
            return 0.0;
        }

        public void SetHorzMode(double dmode)
        {
            if (!paFound)
            {
                return;
            }

            HrztMode horzMode;
            switch ((int)dmode)
            {
                case 0:
                    horzMode = HrztMode.Bank;
                    break;
                case 1:
                    horzMode = HrztMode.Heading;
                    break;
                case 2:
                    horzMode = HrztMode.HeadingNum;
                    break;
                default:
                    return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();
                object horzActive = horzActive_t.GetValue(pilotAssistant);

                SetHorz(pilotAssistant, new object[] { horzActive, false, horzMode, 0.0 });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetHorzMode: {0}", e);
            }
        }

        public double GetHorzSetting()
        {
            if (!paFound)
            {
                return 0.0;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                return GetCurrentHorz(pilotAssistant);
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetHorzSetting: {0}", e);
            }
            return 0.0;
        }

        //--- Throttle control modes -----------------------------------------
        public bool GetThrottleActive()
        {
            if (!paFound)
            {
                return false;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();

                object throttleActive = throttleActive_t.GetValue(pilotAssistant);

                return (bool)throttleActive;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetThrottleActive: {0}", e);
            }
            return false;
        }

        public void SetThrottleActive(bool state)
        {
            if (!paFound)
            {
                return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();

                object throttleActive = throttleMode_t.GetValue(pilotAssistant);

                SetThrottle(pilotAssistant, new object[] { state, false, throttleActive, 0.0 });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetThrottleActive: {0}", e);
            }
        }

        public void SetThrottleDirect(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                SetThrottle(pilotAssistant, new object[] { true, true, ThrottleMode.Direct, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetThrottleDirect: {0}", e);
            }
        }

        public void SetThrottleAccel(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                SetThrottle(pilotAssistant, new object[] { true, true, ThrottleMode.Acceleration, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetThrottleAccel: {0}", e);
            }
        }

        public void SetThrottleSpeed(double rate)
        {
            if (!paFound)
            {
                return;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                SetThrottle(pilotAssistant, new object[] { true, true, ThrottleMode.Speed, rate });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetThrottleSpeed: {0}", e);
            }
        }

        public double GetThrottleMode()
        {
            if (!paFound)
            {
                return 0.0;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                object throttleMode = throttleMode_t.GetValue(pilotAssistant);

                int tm = (int)throttleMode;
                return (double)tm;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetThrottleMode: {0}", e);
            }
            return 0.0;
        }

        public void SetThrottleMode(double dmode)
        {
            if (!paFound)
            {
                return;
            }

            ThrottleMode throttleMode;
            switch ((int)dmode)
            {
                case 0:
                    throttleMode = ThrottleMode.Direct;
                    break;
                case 1:
                    throttleMode = ThrottleMode.Acceleration;
                    break;
                case 2:
                    throttleMode = ThrottleMode.Speed;
                    break;
                default:
                    return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();
                object throttleActive = throttleActive_t.GetValue(pilotAssistant);

                SetHorz(pilotAssistant, new object[] { throttleActive, false, throttleMode, 0.0 });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetThrottleMode: {0}", e);
            }
        }

        public double GetThrottleSetting()
        {
            if (!paFound)
            {
                return 0.0;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                return GetCurrentThrottle(pilotAssistant);
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetThrottleSetting: {0}", e);
            }
            return 0.0;
        }

        //--- Throttle control modes -----------------------------------------
        public void ChangeSpeedRef(double mode)
        {
            if (!paFound)
            {
                return;
            }

            SpeedRef speedRef;
            switch ((int)mode)
            {
                case 0:
                    speedRef = SpeedRef.True;
                    break;
                case 1:
                    speedRef = SpeedRef.Indicated;
                    break;
                case 2:
                    speedRef = SpeedRef.Equivalent;
                    break;
                case 3:
                    speedRef = SpeedRef.Mach;
                    break;
                default:
                    return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();

                SetSpeedRef(pilotAssistant, new object[] { speedRef });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "ChangeSpeedRef: {0}", e);
            }
        }

        public double GetSpeedRef()
        {
            if (!paFound)
            {
                return 0.0;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                object speedRef = speedRef_t.GetValue(pilotAssistant);

                int sr = (int)speedRef;
                return (double)sr;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetSpeedRef: {0}", e);
            }
            return 0.0;
        }

        public void ChangeSpeedUnits(double mode)
        {
            if (!paFound)
            {
                return;
            }

            SpeedUnits speedUnits;
            switch ((int)mode)
            {
                case 0:
                    speedUnits = SpeedUnits.mSec;
                    break;
                case 1:
                    speedUnits = SpeedUnits.kmph;
                    break;
                case 2:
                    speedUnits = SpeedUnits.mph;
                    break;
                case 3:
                    speedUnits = SpeedUnits.knots;
                    break;
                case 4:
                    speedUnits = SpeedUnits.mach;
                    break;
                default:
                    return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();

                SetSpeedUnits(pilotAssistant, new object[] { speedUnits });
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "ChangeSpeedUnits: {0}", e);
            }
        }

        public double GetSpeedUnits()
        {
            if (!paFound)
            {
                return 0.0;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                object speedUnits = speedUnits_t.GetValue(pilotAssistant);

                int su = (int)speedUnits;
                return (double)su;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetSpeedUnits: {0}", e);
            }
            return 0.0;
        }


        public void SetPauseState(bool newstate)
        {
            if (!paFound)
            {
                return;
            }

            try
            {
                object pilotAssistant = GetPilotAssistant();
                object pauseState = pauseState_t.GetValue(pilotAssistant);

                bool currentState = (bool)pauseState;

                if (newstate != currentState)
                {
                    TogglePause(pilotAssistant);
                }
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "SetPauseState: {0}", e);
            }
        }

        public bool GetPauseState()
        {
            if(!paFound)
            {
                return false;
            }
            try
            {
                object pilotAssistant = GetPilotAssistant();

                object pauseState = pauseState_t.GetValue(pilotAssistant);

                return (bool)pauseState;
            }
            catch (Exception e)
            {
                JUtil.LogMessage(this, "GetPauseState: {0}", e);
            }
            return false;
        }
    }
}
