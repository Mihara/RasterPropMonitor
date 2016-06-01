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

namespace JSI
{
    class JSIKAC : IJSIModule
    {
        private static readonly bool kacFound;

        static JSIKAC()
        {
            kacFound = KACWrapper.InitKACWrapper();
        }

        public JSIKAC(Vessel myVessel)
        {
            vessel = myVessel;
            JUtil.LogMessage(this, "A supported version of Kerbal Alarm Clock is {0}", (kacFound) ? "present" : "not available");
        }

        public double ActiveVesselAlarms()
        {
            double count = 0.0;

            if (kacFound)
            {
                var alarms = KACWrapper.KAC.Alarms;
                //alarms.Sort(SortDates);

                string id = vessel.id.ToString();
                int vesselAlarmCount = 0;
                for (int i = 0; i < alarms.Count; ++i)
                {
                    if (alarms[i].VesselID == id)
                    {
                        ++vesselAlarmCount;
                    }
                }

                count = (double)vesselAlarmCount;
            }
            return count;
        }

        public double NextAlarmTime()
        {
            double time = double.MaxValue;

            if (kacFound)
            {
                var alarms = KACWrapper.KAC.Alarms;
                string id = vessel.id.ToString();
                for (int i = 0; i < alarms.Count; ++i)
                {
                    if (alarms[i].VesselID == id && alarms[i].AlarmTime > Planetarium.fetch.time && alarms[i].AlarmTime < time)
                    {
                        time = alarms[i].AlarmTime;
                    }
                }
            }

            return (time < double.MaxValue) ? (time - Planetarium.fetch.time) : 0.0;
        }

        public string NextAlarmType()
        {
            double time = double.MaxValue;
            string type = string.Empty;

            if (kacFound)
            {
                var alarms = KACWrapper.KAC.Alarms;
                string id = vessel.id.ToString();
                for (int i = 0; i < alarms.Count; ++i)
                {
                    if (alarms[i].VesselID == id && alarms[i].AlarmTime > Planetarium.fetch.time && alarms[i].AlarmTime < time)
                    {
                        time = alarms[i].AlarmTime;
                        type = alarms[i].AlarmType.ToString();
                    }
                }
            }

            return type;
        }

        public string NextAlarmName()
        {
            double time = double.MaxValue;
            string name = string.Empty;

            if (kacFound)
            {
                var alarms = KACWrapper.KAC.Alarms;
                string id = vessel.id.ToString();
                for (int i = 0; i < alarms.Count; ++i)
                {
                    if (alarms[i].VesselID == id && alarms[i].AlarmTime > Planetarium.fetch.time && alarms[i].AlarmTime < time)
                    {
                        time = alarms[i].AlarmTime;
                        name = alarms[i].Name;
                    }
                }
            }

            return name;
        }

        private int SortDates(KACWrapper.KACAPI.KACAlarm x, KACWrapper.KACAPI.KACAlarm y)
        {
            return x.AlarmTime.CompareTo(y.AlarmTime);
        }
    }
}
