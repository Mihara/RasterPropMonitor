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
using UnityEngine;

namespace JSI
{
    public class JSIExternalCameraSelector : PartModule
    {
        // Actual configuration parameters.
        [KSPField]
        public string cameraContainer;
        [KSPField]
        public string cameraIDPrefix = "ExtCam";
        [KSPField]
        public int maximum = 8;
        [KSPField]
        public bool showRay = true;
        [KSPField]
        public Vector3 rotateCamera = Vector3.zero;
        [KSPField]
        public Vector3 translateCamera = Vector3.zero;
        // Internal data storage.
        [KSPField(isPersistant = true)]
        public int current = 1;
        // Fields to handle right-click GUI.
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Camera ID: ")]
        public string visibleCameraName;
        [UI_Toggle(disabledText = "off", enabledText = "on")]
        [KSPField(guiActiveEditor = true, guiName = "FOV marker ", isPersistant = true)]
        public bool showCones = true;
        
        // The rest of it
        private GameObject lightCone;
        private LineRenderer lightConeRenderer;
        private static readonly Material lightConeMaterial = new Material(Shader.Find("Particles/Additive"));
        private Transform actualCamera;
        private const float endSpan = 15f;
        private const float fovAngle = 60f;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ID +")]
        public void IdPlus()
        {
            current++;
            if (current > maximum)
            {
                current = 1;
            }
            UpdateName();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ID -")]
        public void IdMinus()
        {
            current--;
            if (current <= 0)
            {
                current = maximum;
            }
            UpdateName();
        }

        private void UpdateName()
        {
            visibleCameraName = cameraIDPrefix + current;
            if (actualCamera != null)
            {
                actualCamera.name = visibleCameraName;
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                ColorizeLightCone();
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (string.IsNullOrEmpty(cameraContainer))
            {
                JSI.JUtil.AnnoyUser(this);
                return;
            }

            // Create the camera transform
            Transform containingTransform = part.FindModelTransform(cameraContainer);
            if (containingTransform.childCount > 0)
            {
                actualCamera = containingTransform.GetChild(0);
            }
            else
            {
                actualCamera = new GameObject().transform;
                actualCamera.parent = containingTransform;
            }
            actualCamera.position = containingTransform.position;
            actualCamera.rotation = containingTransform.rotation;

            if (rotateCamera != Vector3.zero)
            {
                actualCamera.transform.Rotate(rotateCamera);
            }
            if (translateCamera != Vector3.zero)
            {
                actualCamera.transform.localPosition = translateCamera;
            }

            if (state == StartState.Editor)
            {
                if (part.parent == null)
                {
                    foreach (Part thatPart in EditorLogic.SortedShipList)
                    {
                        if (thatPart != part)
                        {
                            foreach (PartModule thatModule in thatPart.Modules)
                            {
                                var peerModule = thatModule as JSIExternalCameraSelector;
                                if (peerModule != null && peerModule.cameraIDPrefix == cameraIDPrefix && peerModule.current == current)
                                {
                                    IdPlus();
                                }
                            }
                        }
                    }
                }

                CreateLightCone();

                part.OnEditorAttach += new Callback(HideLightCone);
                if (showRay)
                {
                    part.OnEditorDetach += new Callback(ShowLightCone);
                }
                part.OnEditorDestroy += new Callback(HideLightCone);
            }

            UpdateName();
        }

        private void HideLightCone()
        {
            showCones = false;
        }

        private void ShowLightCone()
        {
            showCones = true;
        }

        private void CreateLightCone()
        {
            if (lightConeRenderer == null)
            {
                lightCone = new GameObject();
                lightConeRenderer = lightCone.AddComponent<LineRenderer>();
                lightConeRenderer.useWorldSpace = true;
                lightConeRenderer.material = lightConeMaterial;
                lightConeRenderer.startWidth = 0.054f;
                lightConeRenderer.endWidth=endSpan;
                lightConeRenderer.positionCount =2;
                lightConeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lightConeRenderer.receiveShadows = false;
                lightConeRenderer.SetPosition(0, Vector3.zero);
                lightConeRenderer.SetPosition(1, Vector3.zero);

                ColorizeLightCone();
            }
        }

        public void OnDestroy()
        {
            if (lightConeRenderer != null)
            {
                Destroy(lightConeRenderer);
                lightConeRenderer = null;
                Destroy(lightCone);
                lightCone = null;
            }
        }

        private void ColorizeLightCone()
        {
            if (lightConeRenderer != null)
            {
                var newStart = Color32.Lerp(new Color32(0, 0, 255, 178), new Color32(255, 0, 0, 178), 1f / (maximum) * (current - 1));
                lightConeRenderer.startColor = newStart;
                lightConeRenderer.endColor = new Color32(newStart.r, newStart.g, newStart.b, 0);
            }
        }

        private void OnGUI()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (showCones && lightConeRenderer != null && actualCamera != null)
                {
                    Vector3 origin = actualCamera.transform.TransformPoint(Vector3.zero);
                    Vector3 direction = actualCamera.transform.TransformDirection(Vector3.forward);
                    lightConeRenderer.SetPosition(0, origin);
                    lightConeRenderer.SetPosition(1, origin + direction * (endSpan / 2 / Mathf.Tan(Mathf.Deg2Rad * fovAngle / 2)));
                }
            }
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            showCones |= GameSettings.HEADLIGHT_TOGGLE.GetKeyDown();
            showCones &= !GameSettings.HEADLIGHT_TOGGLE.GetKeyUp();

            if (lightConeRenderer != null)
            {
                lightConeRenderer.enabled = showCones;
            }
        }

        public override string GetInfo()
        {
            return "Hold down '" + GameSettings.HEADLIGHT_TOGGLE.primary + "' to display all the camera fields of view at once.";
        }
    }
}

