/*****************************************************************************
 * JSIAdvTransparentPod
 * ====================
 * Plugin for Kerbal Space Program
 *
 * Re-Written by JPLRepo (Jamie Leighton).
 * Based on original JSITransparentPod by Mihara (Eugene Medvedev), 
 * MOARdV, and other contributors
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
 
 using KSP.UI.Screens.Flight;
using System;
using System.Collections.Generic;
using System.Linq;
 using UnityEngine;

namespace JSIAdvTransparentPods
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class JSIAdvTransparentPods : MonoBehaviour
    {
        public static JSIAdvTransparentPods Instance;
        internal List<Part> PartstoFilterfromIVADict;
        private GameObject baseGO;
        internal Camera IVAcamera;
        internal Transform IVAcameraTransform;
        private Camera Maincamera;
        private Transform MaincameraTransform;
        private Component IVACamJSICameraCuller;

        public void Awake()
        {
            JSIAdvTPodsUtil.Log_Debug("OnAwake in {0}", HighLogic.LoadedScene);
            if (Instance != null) Destroy(this);
            //DontDestroyOnLoad(this);
            Instance = this;
            GameEvents.OnMapEntered.Add(TurnoffIVACamera);
            GameEvents.OnMapExited.Add(TurnonIVACamera);
            GameEvents.onVesselChange.Add(onVesselChange);
            GameEvents.onVesselSwitching.Add(onVesselSwitching);
            PartstoFilterfromIVADict = new List<Part>();
        }

        public void onLevelWasLoaded(GameScenes scene)
        {
            JSIAdvTPodsUtil.Log_Debug("OnLevelWasLoaded {0}", scene);
        }

        public void onVesselChange(Vessel vessel)
        {
            JSIAdvTPodsUtil.Log_Debug("OnVesselChange {0} ({1})" , vessel.vesselName , vessel.id);
            //CheckStowaways(); 
        }

        public void onVesselSwitching(Vessel oldvessel, Vessel newvessel)
        {
            JSIAdvTPodsUtil.Log_Debug("OnVesselSwitching");
            if (oldvessel != null)
                JSIAdvTPodsUtil.Log_Debug("From: {0} ({1})" , oldvessel.vesselName , oldvessel.id);
            if (newvessel != null)
                JSIAdvTPodsUtil.Log_Debug("To: {0} ({1}) " , newvessel.vesselName , newvessel.id);
            CheckStowaways();  
        }
        
        public void Update()
        {
            if (Time.timeSinceLevelLoad < 2f)
                return;

            //If Stock Overlay Cam is On or we are NOT in Flight camera mode (IE. Map or IVA mode), turn OFF our camera.
            if (StockOverlayCamIsOn || CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Flight)
            {
                TurnoffIVACamera();
                return;
            }
                
            //So we are in flight cam mode, the stock camera overlay is not on.
            //Check our IVACamera exists, if it doesn't, create one.
            if (IVAcamera == null)
                CreateIVACamera();
            TurnonIVACamera();
        }

        public void LateUpdate()
        {
            if (Time.timeSinceLevelLoad < 2f || CameraManager.Instance == null)
                return;
            
            //If the Stock Overlay camera is not on or we are in flight cam mode.
            if (!StockOverlayCamIsOn && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Flight)
            {
                //This is a bit of a performance hit, we are checking ALL loaded vessels to filter out NON JSIAdvTransparentPods.
                //PartstoFilterFromIVADict will contain all loaded vessels that are not JSIAdvTransparentPods, as well as any that are but are too far from the 
                //camera or are set to auto or OFF.
                foreach (Vessel vsl in FlightGlobals.Vessels.Where(p => p.loaded))
                {
                    foreach (Part part in vsl.parts)
                    {
                        if (part.internalModel != null)
                        {
                            if (!part.Modules.Contains("JSIAdvTransparentPod"))
                            {
                                if (!PartstoFilterfromIVADict.Contains(part))
                                    PartstoFilterfromIVADict.Add(part);
                            }
                        }
                    }
                }

                //If the IVA and Main camera transforms are not null (should't be) position and rotate the IVACamera correctly.
                if (IVAcameraTransform != null && MaincameraTransform != null && InternalSpace.Instance != null)
                {
                    IVAcameraTransform.position = InternalSpace.WorldToInternal(MaincameraTransform.position);
                    IVAcameraTransform.rotation = InternalSpace.WorldToInternal(MaincameraTransform.rotation);
                    IVAcamera.fieldOfView = Maincamera.fieldOfView;
                    
                }
            }
            
        }

        public void OnDestroy()
        {
            JSIAdvTPodsUtil.Log_Debug("OnDestroy");
            DestroyIVACamera();
            GameEvents.OnMapEntered.Remove(TurnoffIVACamera);
            GameEvents.OnMapExited.Remove(TurnonIVACamera);
            GameEvents.onVesselChange.Remove(onVesselChange);
            GameEvents.onLevelWasLoaded.Remove(onLevelWasLoaded);
            GameEvents.onVesselSwitching.Remove(onVesselSwitching);
        }

        public bool StockOverlayCamIsOn
        {
            get
            {
                Camera StockOverlayCamera = JSIAdvTPodsUtil.GetCameraByName("InternalSpaceOverlay Host");
                if (StockOverlayCamera != null) return true;
                else return false;
            }
        }

        internal void CreateIVACamera()
        {
            JSIAdvTPodsUtil.Log_Debug("CreateIVACamera");
            //Create a new IVA camera if one does not exist.
            if (IVAcamera == null)
            {
                //Create a new Gameobject to attach everything to.
                //Attach the IVA Camera to it.
                if (baseGO == null)
                    baseGO = new GameObject("JSIAdvTransparentPods");
                IVAcamera = baseGO.gameObject.AddComponent<Camera>();
                IVAcamera.clearFlags = CameraClearFlags.Depth;
                IVAcameraTransform = IVAcamera.transform;
                //Get the Main Flight camera.
                Maincamera = JSIAdvTPodsUtil.GetCameraByName("Camera 00");
                //The IVA cmaera Transform needs to be parented to the InternalSpace Transform.
                IVAcameraTransform.parent = InternalSpace.Instance.transform;
                MaincameraTransform = Maincamera.transform;
                //Depth of 3 is above the Main Cameras.
                IVAcamera.depth = 3f;
                IVAcamera.fieldOfView = Maincamera.fieldOfView;
                //Show Only Kerbals and Internal Space layers.
                IVAcamera.cullingMask = 1114112;
                //Attach a Culler class to the camera to cull objects we don't want rendered.
                if (IVACamJSICameraCuller == null && HighLogic.LoadedSceneIsFlight)
                    IVACamJSICameraCuller = IVAcamera.gameObject.AddComponent<JSIIVACameraEvents>();
            }
            //Finally turn the new camera on.
            TurnonIVACamera();
        }

        internal void DestroyIVACamera()
        {
            if (IVAcamera != null)
            {
                JSIAdvTPodsUtil.Log_Debug("DestroyIVACamera");
                Destroy(IVACamJSICameraCuller);
                Destroy(IVAcamera);
                baseGO.DestroyGameObject();
            }
        }

        internal void TurnoffIVACamera()
        {
            if (IVAcamera != null)
            {
                if (IVAcamera.enabled)
                    IVAcamera.enabled = false;
                if (IVACamJSICameraCuller != null)
                    IVACamJSICameraCuller.gameObject.SetActive(false);
            }
        }

        internal void TurnonIVACamera()
        {
            if (IVAcamera != null)
            {
                if (!IVAcamera.enabled)
                    IVAcamera.enabled = true;
                if (IVACamJSICameraCuller != null)
                    IVACamJSICameraCuller.gameObject.SetActive(true);
            }
        }

        //This will Unregister all crew on all active/loaded vessels. As there is no PUBLIC access anymore to the portraits list.
        //Kludgy, slow, horrendous. But no other choice. This is only done ONVesselChange and OnVesselSwitch.
        public void CheckStowaways()
        {
            /*
            List <ProtoCrewMember> vslcrew = new List<ProtoCrewMember>();
            //First we do all loaded vessels that are NOT active vessel.
            foreach (
                    Vessel fgVessel in
                        FlightGlobals.Vessels.Where(p => p.loaded && p.isActiveVessel == false))
                {
                    try
                    {
                        vslcrew.AddRange(fgVessel.GetVesselCrew());
                    }
                    catch (Exception)
                    {
                        JSIAdvTPodsUtil.Log_Debug("Failed to build VesselCrew list in CheckStowaways");
                    }
                    
                }
            foreach (ProtoCrewMember crewmbr in vslcrew)
            {
                try
                {
                    crewmbr.KerbalRef.SetVisibleInPortrait(false);
                    KerbalPortraitGallery.Instance.UnregisterActiveCrew(crewmbr.KerbalRef);
                }
                catch (Exception)
                {
                    JSIAdvTPodsUtil.Log_Debug("Failed to UnregisterActiveCrew from PortraitGallery : {0}" , crewmbr.name);
                }
            }*/
            //Now we do the Activevessel - but we also REGISTER the activevessel crew back to the PortraitGallery.
            if (FlightGlobals.ActiveVessel != null)
            {
                foreach (ProtoCrewMember crewmbr in FlightGlobals.ActiveVessel.GetVesselCrew())
                {
                    try
                    {
                        crewmbr.KerbalRef.SetVisibleInPortrait(true);
                        KerbalPortraitGallery.Instance.UnregisterActiveCrew(crewmbr.KerbalRef);
                        KerbalPortraitGallery.Instance.RegisterActiveCrew(crewmbr.KerbalRef);
                    }
                    catch (Exception)
                    {
                        JSIAdvTPodsUtil.Log_Debug("Failed to Un/registerActiveCrew from PortraitGallery : {0}" , crewmbr.name);
                    }
                }
            }
        }
    }

    //This Class is Attached to the JSIIVA Camera object to trigger PreCull and PostRender events on the camera.
    //It will filter/cull out any Internals from the Camera that are in the list: JSIAdvTransparentPods.Instance.PartstoFilterfromIVADict
    //This list is populated with all NON JSIAdvTransparentPods and any filtered JSIAdvTransparentPods (eg: Mode turned to OFF or AUTO).
    public class JSIIVACameraEvents : MonoBehaviour
    {
        //private float overlayFrame = 0;
        private Camera camera;
        private int precullMsgCount = 0;
        void Start()
        {
            camera = GetComponent<Camera>();
            JSIAdvTPodsUtil.Log_Debug("Object attached to Camera {0}" , camera.name);
        }

        public void OnPreCull()
        {
            for (int i = 0; i < JSIAdvTransparentPods.Instance.PartstoFilterfromIVADict.Count; i++)
            {
                try
                {
                    JSIAdvTransparentPods.Instance.PartstoFilterfromIVADict[i].internalModel.SetVisible(false);
                }
                catch (Exception ex)
                {
                    if (precullMsgCount < 10)
                    {
                        JSIAdvTPodsUtil.Log_Debug("Unable to Precull internalModel for part {0}", JSIAdvTransparentPods.Instance.PartstoFilterfromIVADict[i].craftID);
                        JSIAdvTPodsUtil.Log_Debug("Err : {0}", ex.Message);
                        precullMsgCount++;
                    }
                }
            }
        }
        
        public void OnPostRender()
        {
            for (int i = 0; i < JSIAdvTransparentPods.Instance.PartstoFilterfromIVADict.Count; i++)
            {
                try
                {
                    JSIAdvTransparentPods.Instance.PartstoFilterfromIVADict[i].internalModel.SetVisible(true);
                }
                catch (Exception)
                {
                    //JSIAdvTPodsUtil.Log_Debug("Unable to PostRender internalModel for part {0}" , JSIAdvTransparentPods.Instance.PartstoFilterfromIVADict[i].craftID);
                }
            }
        }
    }
}
