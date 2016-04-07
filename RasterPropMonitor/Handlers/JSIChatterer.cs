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
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace JSI
{
    public class JSIChatterer : IJSIModule
    {
        private static readonly Type chatterer_t;
        private static readonly DynamicFuncBool chattererTx;
        private static readonly DynamicFuncBool chattererRx;
        private static readonly DynamicAction chattererStartTalking;

        private static readonly bool chattererFound;

        private Guid lastVessel;
        private UnityEngine.Object chatterer;

        static JSIChatterer()
        {
            try
            {
                var loadedChattererAssy = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name == "Chatterer");

                if (loadedChattererAssy == null)
                {
                    chattererFound = false;

                    return;
                }

                //--- Process all the reflection info
                // MechJebCore
                chatterer_t = loadedChattererAssy.assembly.GetExportedTypes()
                    .SingleOrDefault(t => t.FullName == "Chatterer.chatterer");
                if (chatterer_t == null)
                {
                    JUtil.LogErrorMessage(null, "Did not find Chatterer.chatterer");
                    return;
                }

                MethodInfo txMethod = chatterer_t.GetMethod("VesselIsTransmitting", BindingFlags.Instance | BindingFlags.Public);
                if (txMethod == null)
                {
                    throw new NotImplementedException("txMethod");
                }
                chattererTx = DynamicMethodDelegateFactory.CreateFuncBool(txMethod);

                MethodInfo rxMethod = chatterer_t.GetMethod("VesselIsReceiving", BindingFlags.Instance | BindingFlags.Public);
                if (rxMethod == null)
                {
                    throw new NotImplementedException("rxMethod");
                }
                chattererRx = DynamicMethodDelegateFactory.CreateFuncBool(rxMethod);

                MethodInfo chatterMethod = chatterer_t.GetMethod("InitiateChatter", BindingFlags.Instance | BindingFlags.Public);
                if (chatterMethod == null)
                {
                    throw new NotImplementedException("chatterMethod");
                }
                chattererStartTalking = DynamicMethodDelegateFactory.CreateAction(chatterMethod);
            }
            catch (Exception e)
            {
                chatterer_t = null;
                JUtil.LogMessage(null, "Exception initializing JSIChatterer: {0}", e);
            }

            if (chatterer_t != null && chattererStartTalking != null)
            {
                chattererFound = true;
            }
            else
            {
                chattererFound = false;
            }
        }

        public JSIChatterer()
        {
            JUtil.LogMessage(this, "A supported version of Chatterer is {0}", (chattererFound) ? "present" : "not available");
            lastVessel = Guid.Empty;
        }

        private void updateChatterer()
        {
            // Is this needed?
            if (lastVessel != vessel.id)
            {
                lastVessel = vessel.id;
                chatterer = UnityEngine.Object.FindObjectOfType(chatterer_t);
            }
        }

        public bool VesselXmit()
        {
            if (chattererFound)
            {
                updateChatterer();
                return chattererTx(chatterer);
            }
            else
            {
                return false;
            }
        }

        public bool VesselRecv()
        {
            if (chattererFound)
            {
                updateChatterer();
                return chattererRx(chatterer);
            }
            else
            {
                return false;
            }
        }

        public bool RadioIdle()
        {
            if (chattererFound)
            {
                updateChatterer();
                return !(chattererRx(chatterer) || chattererTx(chatterer));
            }
            else
            {
                return true;
            }
        }

        public void InitiateChatter(bool state)
        {
            if (chattererFound)
            {
                updateChatterer();
                chattererStartTalking(chatterer);
            }
        }
    }
}
