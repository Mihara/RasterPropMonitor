using System;
using System.Collections.Generic;
using UnityEngine;

namespace JSI
{
    // So this is my attempt at reconstructing sfr's transparent internals plugin from first principles.
    // Because I really don't understand some of the things he's doing, why are they breaking, and
    // I'm going to attempt to recreate this module from scratch.

    public class JSITransparentPod : PartModule
    {
        [KSPField]
        public string transparentTransforms = string.Empty;

        [KSPField]
        public string transparentShaderName = "Transparent/Specular";

        [KSPField]
        public string opaqueShaderName = string.Empty;

        [KSPField]
        public bool restoreShadersOnIVA = true;

        [KSPField]
        public bool disableLoadingInEditor = false;

        [KSPField]
        public float distanceToCameraThreshold = 100f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "TransparentPod")] //ON = transparentpod on, OFF = transparentpod off, AUTO = on when focused.
        public string transparentPodSetting = "ON";

        [KSPEvent(active = true, guiActive = true, guiActiveUnfocused = true, guiActiveEditor = true, unfocusedRange = 5f, name = "eventToggleTransparency", guiName = "TransparentPod")]
        public void eventToggleTransparency()
        {
            switch (transparentPodSetting)
            {
                case "ON":
                    transparentPodSetting = "OFF";
                    break;

                case "OFF":
                    transparentPodSetting = "AUTO";
                    break;

                default:
                    transparentPodSetting = "ON";
                    break;
            }
        }

        // I would love to know what exactly possessed Squad to have the IVA space use it's own coordinate system.
        // This rotation adjusts IVA space to match the 'real' space...
        private Quaternion MagicalVoodooRotation = new Quaternion(0, 0.7f, -0.7f, 0);

        private Part knownRootPart;
        private Vessel lastActiveVessel;
        private Quaternion originalRotation;
        private Transform originalParent;
        private Vector3 originalPosition;
        private Vector3 originalScale;

        private Shader transparentShader, opaqueShader;
        private bool hasOpaqueShader;
        private readonly Dictionary<Transform, Shader> shadersBackup = new Dictionary<Transform, Shader>();

        private bool mouseOver = false;

        private float distanceToCamera = 0f;

        public override string GetInfo()
        {
            return "The windows of this capsule have been carefully cleaned.";
        }

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor && disableLoadingInEditor == true)
            {
                // Early out for people who want to disable transparency in
                // the editor due to low-spec computers.
                return;
            }
            GameEvents.onGameSceneSwitchRequested.Add(this.OnGameSceneSwitch);
            GameEvents.onCrewTransferred.Add(this.OnCrewTransferred);
            GameEvents.onVesselChange.Add(this.OnVesselChange);
            GameEvents.onCrewBoardVessel.Add(this.OnCrewboardVessel);

            JUtil.LogMessage(this, "Cleaning pod windows...");

            // Apply shaders to transforms on startup.
            if (!string.IsNullOrEmpty(transparentTransforms))
            {
                transparentShader = Shader.Find(transparentShaderName);

                foreach (string transformName in transparentTransforms.Split('|'))
                {
                    try
                    {
                        Transform tr = part.FindModelTransform(transformName.Trim());
                        if (tr != null)
                        {
                            // We both change the shader and backup the original shader so we can undo it later.
                            Shader backupShader = tr.renderer.material.shader;
                            tr.renderer.material.shader = transparentShader;
                            shadersBackup.Add(tr, backupShader);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, this);
                    }
                }
            }

            if (!string.IsNullOrEmpty(opaqueShaderName))
            {
                opaqueShader = Shader.Find(opaqueShaderName);
                hasOpaqueShader = true;
            }

            // In Editor, the camera we want to change is called "Main Camera". In flight, the camera to change is
            // "Camera 00", i.e. close range camera.

            if (state == StartState.Editor)
            {
                // I'm not sure if this change is actually needed, even. Main Camera's culling mask seems to already include IVA objects,
                // they just don't normally spawn them.
                JUtil.SetCameraCullingMaskForIVA("Main Camera", true);
            }

            // If the internal model has not yet been created, try creating it and log the exception if we fail.
            if (part.internalModel == null)
            {
                try
                {
                    part.CreateInternalModel();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            if (part.internalModel == null && part.partInfo != null)
            {
                // KSP 1.0.x introduced a new feature where it doesn't appear
                // to fully load parts if they're not the root.  In particular,
                // that CreateInternalModel() call above here returns null for
                // non-root parts until one exits the VAB and returns.
                // If the internalModel doesn't exist yet, I find the config
                // for this part, extract the INTERNAL node, and try to create
                // the model myself. Awfully roundabout.

                JUtil.LogMessage(this, "Let's see if anyone included parts so I can assemble the interior");
                ConfigNode ipNameNode = null;
                foreach (UrlDir.UrlConfig cfg in GameDatabase.Instance.GetConfigs("PART"))
                {
                    if (cfg.url == part.partInfo.partUrl)
                    {
                        ipNameNode = cfg.config.GetNode("INTERNAL");
                        break;
                    }
                }

                if (ipNameNode != null)
                {
                    part.internalModel = part.AddInternalPart(ipNameNode);
                }
            }

            // If we ended up with an existing internal model, rotate it now, so that it is shown correctly in the editor.
            if (part.internalModel != null)
            {
                if (state == StartState.Editor)
                {
                    // Just rotating the internal is sufficient in this case.
                    part.internalModel.transform.localRotation = MagicalVoodooRotation;
                }
                else
                {
                    // Else this is our first startup in flight scene, so we reset the IVA.
                    ResetIVA();
                }
            }
            else
            {
                // Some error-proofing. I won't bother doing this every frame, because one error message
                // should suffice, this module is not supposed to be attached to parts that have no internals in the first place.
                JUtil.LogErrorMessage(this, "Wait, where's my internal model?");
            }
        }

        private void SetShaders(bool state)
        {
            if (restoreShadersOnIVA)
            {
                if (state)
                {
                    foreach (KeyValuePair<Transform, Shader> backup in shadersBackup)
                    {
                        backup.Key.renderer.material.shader = transparentShader;
                    }
                }
                else
                {
                    foreach (KeyValuePair<Transform, Shader> backup in shadersBackup)
                    {
                        backup.Key.renderer.material.shader = hasOpaqueShader ? opaqueShader : backup.Value;
                    }
                }
            }
        }

        private void ResetIVA()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                JUtil.LogMessage(this, "Need to reset IVA in part ", part.partName);

                // Now the cruical bit.
                // If the root part changed, we actually need to recreate the IVA forcibly even if it still exists.
                if (vessel.rootPart != knownRootPart)
                {
                    // In this case we also need to kick the user out of IVA if they're currently in our pod,
                    // otherwise lots of things screw up in a bizarre fashion.
                    if (JUtil.UserIsInPod(part))
                    {
                        JUtil.LogMessage(this, "The user is in pod {0} and I need to kick them out.", part.partName);
                        CameraManager.Instance.SetCameraFlight();
                    }
                    // This call not just reinitialises the IVA, but also destroys the existing one, if any,
                    // and reloads all the props and modules.
                    JUtil.LogMessage(this, "Need to actually respawn the IVA model in part {0}", part.partName);
                    part.CreateInternalModel();
                }
                // But otherwise the existing one will serve.

                // If the internal model doesn't yet exist, this call will implicitly create it anyway.
                // It will also initialise it, which in this case implies moving it into the correct location in internal space
                // and populate it with crew, which is what we want.
                part.SpawnCrew();

                // Once that happens, the internal will have the correct location for viewing from IVA relative to the
                // current active vessel. (Yeah, internal space is bizarre like that.) So we make note of it.
                originalParent = part.internalModel.transform.parent;
                originalPosition = part.internalModel.transform.localPosition;
                originalRotation = part.internalModel.transform.localRotation;
                originalScale = part.internalModel.transform.localScale;

                // And then we remember the root part and the active vessel these coordinates refer to.
                knownRootPart = vessel.rootPart;
                lastActiveVessel = FlightGlobals.ActiveVessel;

                //Finally we check for Stowaways on the PortraitCams
                CheckStowaways();
            }
        }

        // When our part is destroyed, we need to be sure to undo the culling mask change before we leave.
        // But 2 out of 10 times it seems OnDestroy is being called AFTER Camera 00 is Disabled which means it's too late.
        public void OnDestroy()
        {
            JUtil.SetMainCameraCullingMaskForIVA(false);
            GameEvents.onGameSceneSwitchRequested.Remove(this.OnGameSceneSwitch);
            GameEvents.onCrewTransferred.Remove(this.OnCrewTransferred);
            GameEvents.onVesselChange.Remove(this.OnVesselChange);
            GameEvents.onCrewBoardVessel.Remove(this.OnCrewboardVessel);
        }

        // So, we also add a GameEvent to fire when the GameScene is about to switch.
        // When user switches from Flight mode, we need to be sure to undo the culling mask change before we leave.
        // But 2 out of 10 times it seems even this is being called AFTER Camera 00 is Disabled which means it's too late.
        public void OnGameSceneSwitch(GameEvents.FromToAction<GameScenes, GameScenes> fromtoAction)
        {
            if (fromtoAction.from == GameScenes.FLIGHT)
            {
                JUtil.SetMainCameraCullingMaskForIVA(false);
            }
        }

        // So these next three methods are called when crew transfers occur or vessel change occurs or crew board vessel
        // In all three cases we check the portrait cams for stowaways.
        public void OnCrewTransferred(GameEvents.HostedFromToAction<ProtoCrewMember, Part> fromToAction)
        {
            CheckStowaways();
        }

        public void OnVesselChange(Vessel vessel)
        {
            CheckStowaways();
        }

        public void OnCrewboardVessel(GameEvents.FromToAction<Part, Part> fromToAction)
        {
            CheckStowaways();
        }

        public void CheckStowaways()
        {
            // Now we need to make sure that the list of portraits in the GUI conforms to what actually is in the active vessel.
            // This is important because IVA/EVA buttons clicked on kerbals that are not in the active vessel cause problems
            // that I can't readily debug, and it shouldn't happen anyway.

            // First, every pod should check through the list of portaits and remove everyone who is not from the active vessel, or NO vessel.
            var stowaways = new List<Kerbal>();
            foreach (Kerbal thatKerbal in KerbalGUIManager.ActiveCrew)
            {
                if (thatKerbal.InPart == null)
                {
                    stowaways.Add(thatKerbal);
                }
                else
                {
                    if (thatKerbal.InVessel != FlightGlobals.ActiveVessel)
                    {
                        stowaways.Add(thatKerbal);
                    }
                }
            }
            foreach (Kerbal thatKerbal in stowaways)
            {
                KerbalGUIManager.RemoveActiveCrew(thatKerbal);
            }
            // Only the pods in the active vessel should be doing this since the list refers to them.
            if (vessel.isActiveVessel)
            {
                // Then, every pod should check the list of seats in itself and see if anyone is missing who should be present.
                foreach (InternalSeat seat in part.internalModel.seats)
                {
                    if (seat.kerbalRef != null && !KerbalGUIManager.ActiveCrew.Contains(seat.kerbalRef))
                    {
                        if (seat.kerbalRef.protoCrewMember.rosterStatus != ProtoCrewMember.RosterStatus.Dead || seat.kerbalRef.protoCrewMember.type != ProtoCrewMember.KerbalType.Unowned)
                        {
                            KerbalGUIManager.AddActiveCrew(seat.kerbalRef);
                        }
                    }
                }
            }
        }

        // We also do the same if the part is packed, just in case.
        public virtual void OnPartPack()
        {
            JUtil.SetMainCameraCullingMaskForIVA(false);
        }

        public override void OnUpdate()
        {
            // In the editor, none of this logic should matter, even though the IVA probably exists already.
            if (HighLogic.LoadedSceneIsEditor)
                return;

            // If the root part changed, or the IVA is mysteriously missing, we reset it and take note of where it ended up.
            if (vessel.rootPart != knownRootPart || lastActiveVessel != FlightGlobals.ActiveVessel || part.internalModel == null)
            {
                ResetIVA();
            }

            // So we do have an internal model, right?
            if (part.internalModel != null)
            {
                if (JUtil.IsInIVA())
                {
                    // If the user is IVA, we move the internals to the original position,
                    // so that they show up correctly on InternalCamera. This particularly concerns
                    // the pod the user is inside of.
                    part.internalModel.transform.parent = originalParent;
                    part.internalModel.transform.localRotation = originalRotation;
                    part.internalModel.transform.localPosition = originalPosition;
                    part.internalModel.transform.localScale = originalScale;

                    if (!JUtil.UserIsInPod(part))
                    {
                        // If the user is in some other pod than this one, we also hide our IVA to prevent them from being drawn above
                        // everything else.
                        part.internalModel.SetVisible(false);
                    }

                    // Unfortunately even if I do that, it means that at least one kerbal on the ship will see his own IVA twice in two different orientations,
                    // one time through the InternalCamera (which I can't modify) and another through the Camera 00.
                    // So we have to also undo the culling mask change as well.
                    JUtil.SetMainCameraCullingMaskForIVA(false);

                    // So once everything is hidden again, we undo the change in shaders to conceal the fact that you can't see other internals.
                    SetShaders(false);
                }
                else
                {
                    // If the current part is not part of the active vessel, we calculate the distance from the part to the flight camera.
                    // If this distance is > 500m we turn off transparency for the part.
                    // Uses Maths calcs intead of built in Unity functions as this is up to 5 times faster.
                    if (!vessel.isActiveVessel)
                    {
                        Vector3 heading;
                        float distanceSquared;
                        Transform thisPart = this.part.transform;
                        Transform flightCamera = FlightCamera.fetch.transform;
                        heading.x = thisPart.position.x - flightCamera.position.x;
                        heading.y = thisPart.position.y - flightCamera.position.y;
                        heading.z = thisPart.position.z - flightCamera.position.z;
                        distanceSquared = heading.x * heading.x + heading.y * heading.y + heading.z * heading.z;
                        distanceToCamera = Mathf.Sqrt(distanceSquared);

                        if (distanceToCamera > distanceToCameraThreshold)
                        {
                            SetShaders(false);
                            JUtil.SetMainCameraCullingMaskForIVA(true);
                            return;
                        }
                    }
                    distanceToCamera = 0f;

                    // If transparentPodSetting = OFF or AUTO and not the focused active part we treat the part like a non-transparent part.
                    // and we turn off the shaders (if set) and exit OnUpdate. onGUI and LateUpdate will do the rest.
                    if (transparentPodSetting == "OFF" || ((transparentPodSetting == "AUTO" && FlightGlobals.ActiveVessel.referenceTransformId != this.part.flightID)
                        && (transparentPodSetting == "AUTO" && !mouseOver)))
                    {
                        SetShaders(false);
                        JUtil.SetMainCameraCullingMaskForIVA(true);
                        return;
                    }
                    // Otherwise, we're out of IVA, so we can proceed with setting up the pods for exterior view.
                    JUtil.SetMainCameraCullingMaskForIVA(true);

                    // Make the internal model visible...
                    part.internalModel.SetVisible(true);

                    // And for a good measure we make sure the shader change has been applied.
                    SetShaders(true);

                    // Now we attach the restored IVA directly into the pod at zero local coordinates and rotate it,
                    // so that it shows up on the main outer view camera in the correct location.
                    part.internalModel.transform.parent = part.transform;
                    part.internalModel.transform.localRotation = MagicalVoodooRotation;
                    part.internalModel.transform.localPosition = Vector3.zero;
                    part.internalModel.transform.localScale = Vector3.one;
                }
            }
        }

        // During the drawing of the GUI, when the portraits are to be drawn, if the internal exists, it should be visible,
        // so that portraits show up correctly. This is only checked when transparentPodSetting is "OFF" or on "AUTO"
        // and this part is part of the active vessel.
        public void OnGUI()
        {
            if (HighLogic.LoadedSceneIsEditor || JUtil.IsInIVA())
                return;
            if ((transparentPodSetting == "OFF" || transparentPodSetting == "AUTO") && vessel.isActiveVessel && part.internalModel != null)
            {
                part.internalModel.SetVisible(true);
            }
        }

        // Before the rest of the world is to be drawn, in the editor or flight mode we turn off the internalModel if transparentPodSetting is "OFF" or on "AUTO" and the mouse is not over this part.
        // If in IVA we do nothing. If the distance to the camera is other the threshold (for not active vessel) we also turn off the internal.
        public void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (transparentPodSetting == "OFF" || (transparentPodSetting == "AUTO" && !mouseOver))
                {
                    SetShaders(false);
                    JUtil.SetCameraCullingMaskForIVA("Main Camera", true);
                    if (part.internalModel != null)
                        part.internalModel.SetVisible(false);
                }
                else
                {
                    SetShaders(true);
                    JUtil.SetCameraCullingMaskForIVA("Main Camera", true);
                    if (part.internalModel != null)
                        part.internalModel.SetVisible(true);
                }
                mouseOver = false;
                return;
            }

            if (JUtil.IsInIVA())
                return;

            if (transparentPodSetting == "OFF" || ((transparentPodSetting == "AUTO" && FlightGlobals.ActiveVessel.referenceTransformId != this.part.flightID)
                        && (transparentPodSetting == "AUTO" && !mouseOver)))
            {
                if (JUtil.cameraMaskShowsIVA && part.internalModel != null && !JUtil.UserIsInPod(part))
                {
                    part.internalModel.SetVisible(false);
                }
            }

            if (distanceToCamera > distanceToCameraThreshold && part.internalModel != null)
            {
                part.internalModel.SetVisible(false);
            }
            mouseOver = false;
        }

        // When mouse is over this part set a flag for the transparentPodSetting = "AUTO" setting.
        private void OnMouseOver()
        {
            mouseOver = true;
        }
    }

    // And this is a stop gap measure.
    // In the particular case where the user is controlling a vessel where the root pod is not
    // a transparent pod, but a transparent pod is within physics range,
    // the IVA of the non-transparent pod will be visible while the user is out of it.
    // And it won't be in the correct position either.

    // Which is why this module need to be added to every non-transparent pod with IVA
    // that does not have a JSITransparentPod module on it with ModuleManager to hide the IVA
    // in the case that happens.

    public class JSINonTransparentPod : PartModule
    {
        /* Correction. You can actually do this, so this method is unnecessary.
         * So much the better.

        // Since apparently, current versions of ModuleManager do not allow multiple
        // "HAS" directives, the easier course of action to only apply this module to
        // pods that are not transparent is to apply it to every pod,
        // and then make it self-destruct if the pod is in fact transparent.
        public override void OnStart(StartState state)
        {
            if (state != StartState.Editor) {
                foreach (PartModule thatModule in part.Modules) {
                    if (thatModule is JSITransparentPod) {
                        Destroy(this);
                    }
                }
            }
        }

        */

        // During Startup we need to reset the var JUtil.cameraMaskShowsIVA, as sometimes it gets out of sync with the actual Camera Mask.
        // JUtil.cameraMaskShowsIVA is used in the following two methods to correctly draw non Transparent pods.
        // So this will check the culling mask of 'Camera 00' and reset that var once at OnStart.
        public override void OnStart(StartState state)
        {
            if (state != StartState.Editor)
            {
                Camera sourceCam = JUtil.GetCameraByName("Camera 00");
                if (sourceCam != null)
                {
                    JUtil.cameraMaskShowsIVA = ((sourceCam.cullingMask & (1 << 16)) != 0) && ((sourceCam.cullingMask & (1 << 20)) != 0);
                }
            }
        }

        // During the drawing of the GUI, when the portraits are to be drawn, if the internal exists, it should be visible,
        // so that portraits show up correctly.
        public void OnGUI()
        {
            if (JUtil.cameraMaskShowsIVA && vessel.isActiveVessel && part.internalModel != null)
            {
                part.internalModel.SetVisible(true);
            }
        }

        // But before the rest of the world is to be drawn, if the internal exists and is the active internal,
        // it should become invisible.
        public void LateUpdate()
        {
            if (JUtil.cameraMaskShowsIVA && vessel.isActiveVessel && part.internalModel != null && !JUtil.UserIsInPod(part))
            {
                part.internalModel.SetVisible(false);
            }
        }
    }
}