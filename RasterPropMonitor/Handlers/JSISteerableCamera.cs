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
using UnityEngine;

namespace JSI
{
    /*
     * The JSISteerableCamera provides a background handler that adds yaw and
     * pitch capabilities to a camera (in addition to the zoom of the basic
     * camera).  All movements are smoothly applied while the associated
     * button is held down.  zoom and pan limits, as well as zoom and pan
     * rates, are configurable.  Since multi-press isn't possible in KSP IVA,
     * only a single action can take place at a time (zoom, or yaw, or pitch).
     *
     * Configuration values should be checked to make sure that camera
     * clipping (seeing inside parts) doesn't happen.  This can be especially
     * noticeable with surface-mounted cameras and a negative pitch.
     *
     * Configuration:
     *
     * zoomIn, zoomOut, yawLeft, yawRight, pitchUp, pitchDown -- number of the
     * globalButton that controls each of these modes.  Defaults to 99
     * (disables the behavior).
     *
     * cameraTransform -- the name of the camera that this background handler
     * will use for rendering.
     *
     * fovLimits -- The upper and lower bound of the zoom control, in degrees.
     * Defaults to (60, 60).  Smaller values equate to higher zoom levels.  The
     * camera starts at the largest field of view (lowest zoom).
     *
     * yawLimits -- the upper and lower bound of yaw (side-to-side) camera
     * movement.  Negative values are left, positive values are right.  The
     * values do not have to be symmetrical (-10, 40 is okay, for instance).
     * The lower value must be zero or negative, the upper value must be zero
     * or positive.  The camera starts with a yaw of 0.
     *
     * pitchLimits -- the upper and lower bound of pitch (up-to-down) camera
     * movement.  Positive values are up, negative values are down.  The values
     * do not have to be symmetrical.  The lower values must be zero or
     * negative, the upper value must be zero or positive.  The camera starts
     * with a pitch of 0.
     *
     * zoomRate, yawRate, pitchRate -- controls how quickly the camera will
     * zoom, yaw, or pitch, measured in degrees per second.
     */
    public class JSISteerableCamera : InternalModule
    {
        [KSPField]
        public int zoomIn = -1;
        [KSPField]
        public int zoomOut = -1;
        [KSPField]
        public int yawLeft = -1;
        [KSPField]
        public int yawRight = -1;
        [KSPField]
        public int pitchUp = -1;
        [KSPField]
        public int pitchDown = -1;
        [KSPField]
        public int toggleTargetIcon = -1;
        [KSPField]
        public int nextCamera = -1;
        [KSPField]
        public int prevCamera = -1;
        [KSPField]
        public int seekHome = -1;
        [KSPField]
        public string fovLimits = string.Empty;
        [KSPField]
        public string yawLimits = string.Empty;
        [KSPField]
        public string pitchLimits = string.Empty;
        [KSPField]
        public string zoomRate = string.Empty;
        [KSPField]
        public string yawRate = string.Empty;
        [KSPField]
        public string pitchRate = string.Empty;
        [KSPField]
        public string cameraTransform = string.Empty;
        [KSPField]
        public string targetIconColor = "255, 0, 255, 255";
        // magenta, to match KSP stock
        [KSPField]
        public float iconPixelSize = 8f;
        [KSPField]
        public bool showTargetIcon;
        [KSPField]
        public string homeCrosshairColor = "0,0,0,0";
        // Flicker options to match standard cameras.
        [KSPField]
        public float flickerChance;
        [KSPField]
        public int flickerRange;
        [KSPField]
        public bool skipMissingCameras = false;
        [KSPField]
        public string cameraInfoVarName = string.Empty;

        private Material homeCrosshairMaterial;
        private FlyingCamera cameraObject;
        private float zoomDirection;
        private float yawDirection;
        private float pitchDirection;
        private double lastUpdateTime;
        // Target tracking icon
        private Texture2D gizmoTexture;
        private Material iconMaterial;
        //private Material effect;

        private int currentCamera = 0;
        private List<SteerableCameraParameters> cameras = new List<SteerableCameraParameters>();

        private readonly Vector2 defaultFovLimits = new Vector2(60.0f, 60.0f);
        private readonly Vector2 defaultYawLimits = new Vector2(0.0f, 0.0f);
        private readonly Vector2 defaultPitchLimits = new Vector2(0.0f, 0.0f);

        private static Vector2 ClampToEdge(Vector2 position)
        {
            return position / (Math.Abs(position.x) > Math.Abs(position.y) ? Math.Abs(position.x) : Math.Abs(position.y));
        }

        private Vector2 GetNormalizedScreenPosition(SteerableCameraParameters activeCamera, Vector3 directionVector, float cameraAspect)
        {
            // Transform direction using the active camera's rotation.
            var targetTransformed = cameraObject.CameraRotation(activeCamera.currentYaw, -activeCamera.currentPitch).Inverse() * directionVector;

            // (x, y) provided the lateral displacement.  (z) provides the "in front of / behind"
            var targetDisp = new Vector2(targetTransformed.x, -targetTransformed.y);

            // I want to scale the displacement such that 1.0
            // represents the edge of the viewport. And my math is too
            // rusty to remember the right way to get that scalar.
            // Both of these are off by just a bit at wider zooms
            // (tan scales a little too much, sin a little too
            // little).  It may simply be an artifact of the camera
            // perspective divide.
            var fovScale = new Vector2(cameraAspect * Mathf.Tan(Mathf.Deg2Rad * activeCamera.currentFoV * 0.5f), Mathf.Tan(Mathf.Deg2Rad * activeCamera.currentFoV * 0.5f));
            //Vector2 fovScale = new Vector2(cameraAspect * Mathf.Sin(Mathf.Deg2Rad * currentFoV * 0.5f), Mathf.Sin(Mathf.Deg2Rad * currentFoV * 0.5f));

            // MOARdV: Are there no overloaded operators for vector math?
            // Normalize to a [-1,+1] range on both axes
            targetDisp.x = targetDisp.x / fovScale.x;
            targetDisp.y = targetDisp.y / fovScale.y;

            // If the target is behind the camera, or outside the
            // bounds of the viewport, the icon needs to be clamped
            // to the edge.
            if (targetTransformed.z < 0.0f || Math.Max(Math.Abs(targetDisp.x), Math.Abs(targetDisp.y)) > 1.0f)
            {
                targetDisp = ClampToEdge(targetDisp);
            }

            targetDisp.x = targetDisp.x * 0.5f + 0.5f;
            targetDisp.y = targetDisp.y * 0.5f + 0.5f;

            return targetDisp;
        }

        public void PageActive(bool state, int pageID)
        {
            if (cameraObject == null)
                return;
            if (state)
                cameraObject.SetFlicker(flickerChance, flickerRange);
            else
                cameraObject.SetFlicker(0, 0);
        }

        public bool RenderCamera(RenderTexture screen, float cameraAspect)
        {
            // Just in case.
            if (HighLogic.LoadedSceneIsEditor)
            {
                return false;
            }

            if (cameras.Count < 1)
            {
                return false;
            }

            var activeCamera = cameras[currentCamera];
            if (string.IsNullOrEmpty(activeCamera.cameraTransform))
            {
                return false;
            }

            if (cameraObject == null)
            {
                cameraObject = new FlyingCamera(part, cameraAspect);
                cameraObject.PointCamera(activeCamera.cameraTransform, activeCamera.currentFoV);
            }

            cameraObject.FOV = activeCamera.currentFoV;

            //RenderTexture rt = RenderTexture.GetTemporary(screen.width, screen.height, screen.depth, screen.format);

            // Negate pitch - the camera object treats a negative pitch as "up"
            if (cameraObject.Render(screen, activeCamera.currentYaw, -activeCamera.currentPitch))
            {
                //Graphics.Blit(rt, screen, effect);
                //RenderTexture.ReleaseTemporary(rt);

                ITargetable target = FlightGlobals.fetch.VesselTarget;

                bool drawSomething = ((gizmoTexture != null && target != null && showTargetIcon) || homeCrosshairMaterial.color.a > 0);

                if (drawSomething)
                {
                    GL.PushMatrix();
                    GL.LoadPixelMatrix(0, screen.width, screen.height, 0);
                }

                if (gizmoTexture != null && target != null && showTargetIcon)
                {
                    // Figure out which direction the target is.
                    Vector3 targetDisplacement = target.GetTransform().position - cameraObject.GetTransform().position;
                    targetDisplacement.Normalize();

                    // Transform it using the active camera's rotation.
                    var targetDisp = GetNormalizedScreenPosition(activeCamera, targetDisplacement, cameraAspect);

                    var iconCenter = new Vector2(screen.width * targetDisp.x, screen.height * targetDisp.y);

                    // Apply some clamping values to force the icon to stay on screen
                    iconCenter.x = Math.Max(iconPixelSize * 0.5f, iconCenter.x);
                    iconCenter.x = Math.Min(screen.width - iconPixelSize * 0.5f, iconCenter.x);
                    iconCenter.y = Math.Max(iconPixelSize * 0.5f, iconCenter.y);
                    iconCenter.y = Math.Min(screen.height - iconPixelSize * 0.5f, iconCenter.y);

                    var position = new Rect(iconCenter.x - iconPixelSize * 0.5f, iconCenter.y - iconPixelSize * 0.5f, iconPixelSize, iconPixelSize);

                    Graphics.DrawTexture(position, gizmoTexture, GizmoIcons.GetIconLocation(GizmoIcons.IconType.TARGETPLUS), 0, 0, 0, 0, iconMaterial);
                }

                if (homeCrosshairMaterial.color.a > 0)
                {
                    // Mihara: Reference point cameras are different enough to warrant it.
                    var cameraForward = cameraObject.GetTransformForward();
                    var crossHairCenter = GetNormalizedScreenPosition(activeCamera, cameraForward, cameraAspect);
                    crossHairCenter.x *= screen.width;
                    crossHairCenter.y *= screen.height;
                    crossHairCenter.x = Math.Max(iconPixelSize * 0.5f, crossHairCenter.x);
                    crossHairCenter.x = Math.Min(screen.width - iconPixelSize * 0.5f, crossHairCenter.x);
                    crossHairCenter.y = Math.Max(iconPixelSize * 0.5f, crossHairCenter.y);
                    crossHairCenter.y = Math.Min(screen.height - iconPixelSize * 0.5f, crossHairCenter.y);

                    float zoomAdjustedIconSize = iconPixelSize * Mathf.Tan(Mathf.Deg2Rad * activeCamera.fovLimits.y * 0.5f) / Mathf.Tan(Mathf.Deg2Rad * activeCamera.currentFoV * 0.5f);

                    homeCrosshairMaterial.SetPass(0);
                    GL.Begin(GL.LINES);
                    GL.Vertex3(crossHairCenter.x - zoomAdjustedIconSize * 0.5f, crossHairCenter.y, 0.0f);
                    GL.Vertex3(crossHairCenter.x + zoomAdjustedIconSize * 0.5f, crossHairCenter.y, 0.0f);
                    GL.Vertex3(crossHairCenter.x, crossHairCenter.y - zoomAdjustedIconSize * 0.5f, 0.0f);
                    GL.Vertex3(crossHairCenter.x, crossHairCenter.y + zoomAdjustedIconSize * 0.5f, 0.0f);
                    GL.End();
                }

                if (drawSomething)
                {
                    GL.PopMatrix();
                }

                return true;
            }
            else if (skipMissingCameras)
            {
                // This will handle cameras getting ejected while in use.
                SelectNextCamera();
            }

            //RenderTexture.ReleaseTemporary(rt);
            return false;
        }

        public override void OnUpdate()
        {
            if (!JUtil.VesselIsInIVA(vessel) || cameraObject == null)
            {
                return;
            }

            if (cameras.Count < 1)
            {
                return;
            }

            var activeCamera = cameras[currentCamera];

            double thisUpdateTime = Planetarium.GetUniversalTime();

            // Just to be safe, never allow negative values.
            float dT = Math.Max(0.0f, (float)(thisUpdateTime - lastUpdateTime));

            activeCamera.currentFoV = Math.Max(activeCamera.fovLimits.x, Math.Min(activeCamera.fovLimits.y, activeCamera.currentFoV + dT * activeCamera.zoomRate * zoomDirection));
            if (activeCamera.seekHome)
            {
                float deltaYaw = Math.Min(Math.Abs(activeCamera.currentYaw), dT * activeCamera.yawRate);
                float deltaPitch = Math.Min(Math.Abs(activeCamera.currentPitch), dT * activeCamera.pitchRate);
                activeCamera.currentYaw -= deltaYaw * Math.Sign(activeCamera.currentYaw);
                activeCamera.currentPitch -= deltaPitch * Math.Sign(activeCamera.currentPitch);
            }
            else
            {
                activeCamera.currentYaw = Math.Max(activeCamera.yawLimits.x, Math.Min(activeCamera.yawLimits.y, activeCamera.currentYaw + dT * activeCamera.yawRate * yawDirection));
                activeCamera.currentPitch = Math.Max(activeCamera.pitchLimits.x, Math.Min(activeCamera.pitchLimits.y, activeCamera.currentPitch + dT * activeCamera.pitchRate * pitchDirection));
            }

            lastUpdateTime = thisUpdateTime;
        }

        public void ClickProcessor(int buttonID)
        {
            if (cameraObject == null)
            {
                return;
            }

            if (cameras.Count < 1)
            {
                return;
            }

            if (buttonID == zoomIn)
            {
                zoomDirection = -1.0f;
                yawDirection = 0.0f;
                pitchDirection = 0.0f;
            }
            else if (buttonID == zoomOut)
            {
                zoomDirection = 1.0f;
                yawDirection = 0.0f;
                pitchDirection = 0.0f;
            }
            else if (buttonID == yawLeft)
            {
                zoomDirection = 0.0f;
                yawDirection = -1.0f;
                pitchDirection = 0.0f;
                cameras[currentCamera].seekHome = false;
            }
            else if (buttonID == yawRight)
            {
                zoomDirection = 0.0f;
                yawDirection = 1.0f;
                pitchDirection = 0.0f;
                cameras[currentCamera].seekHome = false;
            }
            else if (buttonID == pitchUp)
            {
                zoomDirection = 0.0f;
                yawDirection = 0.0f;
                pitchDirection = 1.0f;
                cameras[currentCamera].seekHome = false;
            }
            else if (buttonID == pitchDown)
            {
                zoomDirection = 0.0f;
                yawDirection = 0.0f;
                pitchDirection = -1.0f;
                cameras[currentCamera].seekHome = false;
            }
            else if (buttonID == toggleTargetIcon)
            {
                showTargetIcon = !showTargetIcon;
            }
            else if (buttonID == nextCamera)
            {
                // Stop current camera motion, since we're not going to update it.
                cameras[currentCamera].seekHome = false;

                SelectNextCamera();
            }
            else if (buttonID == prevCamera)
            {
                // Stop current camera motion, since we're not going to update it.
                cameras[currentCamera].seekHome = false;

                SelectPreviousCamera();
            }
            else if (buttonID == seekHome)
            {
                cameras[currentCamera].seekHome = true;
            }

            // Always reset the lastUpdateTime on a button click, in case it
            // has been a while since the last click.
            lastUpdateTime = Planetarium.GetUniversalTime();
        }

        private void SelectNextCamera()
        {
            if (cameras.Count < 2)
            {
                return;
            }

            ++currentCamera;
            if (currentCamera == cameras.Count)
            {
                currentCamera = 0;
            }

            bool gotCamera = cameraObject.PointCamera(cameras[currentCamera].cameraTransform, cameras[currentCamera].currentFoV);

            if (!skipMissingCameras)
            {
                //if (rpmComp != null)
                //{
                //    rpmComp.SetPropVar(cameraInfoVarName + "_ID", internalProp.propID, currentCamera + 1);
                //}
                return;
            }

            int camerasTested = 1;

            while (!gotCamera && camerasTested < cameras.Count)
            {
                ++camerasTested;
                ++currentCamera;
                if (currentCamera == cameras.Count)
                {
                    currentCamera = 0;
                }

                gotCamera = cameraObject.PointCamera(cameras[currentCamera].cameraTransform, cameras[currentCamera].currentFoV);
            }

            //if (rpmComp != null)
            //{
            //    rpmComp.SetPropVar(cameraInfoVarName + "_ID", internalProp.propID, currentCamera + 1);
            //}
        }

        private void SelectPreviousCamera()
        {
            if (cameras.Count < 2)
            {
                return;
            }

            --currentCamera;
            if (currentCamera < 0)
            {
                currentCamera = cameras.Count - 1;
            }

            bool gotCamera = cameraObject.PointCamera(cameras[currentCamera].cameraTransform, cameras[currentCamera].currentFoV);

            if (!skipMissingCameras)
            {
                //if (rpmComp != null)
                //{
                //    rpmComp.SetPropVar(cameraInfoVarName + "_ID", internalProp.propID, currentCamera + 1);
                //}
                return;
            }

            int camerasTested = 1;

            while (!gotCamera && camerasTested < cameras.Count)
            {
                ++camerasTested;
                --currentCamera;
                if (currentCamera < 0)
                {
                    currentCamera = cameras.Count - 1;
                }

                gotCamera = cameraObject.PointCamera(cameras[currentCamera].cameraTransform, cameras[currentCamera].currentFoV);
            }
            //if (rpmComp != null)
            //{
            //    rpmComp.SetPropVar(cameraInfoVarName + "_ID", internalProp.propID, currentCamera + 1);
            //}
        }

        // Analysis disable once UnusedParameter
        public void ReleaseProcessor(int buttonID)
        {
            // Always clear all movements here.  We don't support multi-click :)
            zoomDirection = 0.0f;
            pitchDirection = 0.0f;
            yawDirection = 0.0f;
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;

            if (string.IsNullOrEmpty(cameraTransform))
            {
                // Nothing to do if there're no camera transforms.
                return;
            }

            string[] cameraTransformList = cameraTransform.Split('|');

            // I'm sure this and the loop can be done a little differently to
            // make it clearer, but this works.
            string[] fovLimitsList = (string.IsNullOrEmpty(fovLimits)) ? null : fovLimits.Split('|');
            string[] yawLimitsList = (string.IsNullOrEmpty(yawLimits)) ? null : yawLimits.Split('|');
            string[] pitchLimitsList = (string.IsNullOrEmpty(pitchLimits)) ? null : pitchLimits.Split('|');
            string[] zoomRateList = (string.IsNullOrEmpty(zoomRate)) ? null : zoomRate.Split('|');
            string[] yawRateList = (string.IsNullOrEmpty(yawRate)) ? null : yawRate.Split('|');
            string[] pitchRateList = (string.IsNullOrEmpty(pitchRate)) ? null : pitchRate.Split('|');

            // cameraTransformList controls the number of cameras instantiated.
            // Every other value has a default, so if it's not specified, we
            // will use that default.
            for (int i = 0; i < cameraTransformList.Length; ++i)
            {
                Vector2 thisFovLimit = (fovLimitsList != null && i < fovLimitsList.Length) ? (Vector2)ConfigNode.ParseVector2(fovLimitsList[i]) : defaultFovLimits;
                Vector2 thisYawLimit = (yawLimitsList != null && i < yawLimitsList.Length) ? (Vector2)ConfigNode.ParseVector2(yawLimitsList[i]) : defaultYawLimits;
                Vector2 thisPitchLimit = (pitchLimitsList != null && i < pitchLimitsList.Length) ? (Vector2)ConfigNode.ParseVector2(pitchLimitsList[i]) : defaultPitchLimits;
                float thisZoomRate = (zoomRateList != null && i < zoomRateList.Length) ? JUtil.GetFloat(zoomRateList[i]) ?? 0.0f : 0.0f;
                float thisYawRate = (yawRateList != null && i < yawRateList.Length) ? JUtil.GetFloat(yawRateList[i]) ?? 0.0f : 0.0f;
                float thisPitchRate = (pitchRateList != null && i < pitchRateList.Length) ? JUtil.GetFloat(pitchRateList[i]) ?? 0.0f : 0.0f;

                var thatCamera = new SteerableCameraParameters(cameraTransformList[i],
                    thisFovLimit, thisYawLimit, thisPitchLimit,
                    thisZoomRate, thisYawRate, thisPitchRate, i + 1);
                cameras.Add(thatCamera);
            }

            gizmoTexture = JUtil.GetGizmoTexture();

            iconMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));

            // MOARdV: The maneuver gizmo texture is white. Unity's DrawTexture
            // expects a (0.5, 0.5, 0.5, 0.5) texture to be neutral for coloring
            // purposes.  Multiplying the desired alpha by 1/2 gets around the
            // gizmo texture's color, and gets correct alpha effects.
            Color32 iconColor = ConfigNode.ParseColor32(targetIconColor);
            iconColor.a /= 2;
            iconMaterial.color = iconColor;

            homeCrosshairMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
            homeCrosshairMaterial.color = ConfigNode.ParseColor32(homeCrosshairColor);

            if (!string.IsNullOrEmpty(cameraInfoVarName))
            {
                //rpmComp = RasterPropMonitorComputer.Instantiate(internalProp);
                //if (rpmComp.HasPropVar(cameraInfoVarName + "_ID", internalProp.propID))
                //{
                //    currentCamera = rpmComp.GetPropVar(cameraInfoVarName + "_ID", internalProp.propID) - 1;
                //}
                //else
                //{
                //    rpmComp.SetPropVar(cameraInfoVarName + "_ID", internalProp.propID, currentCamera + 1);
                //}
            }
        }
    }

    // Prep for switchable steerable cameras.
    public class SteerableCameraParameters
    {
        public readonly string cameraTransform;
        public readonly Vector2 fovLimits;
        public readonly Vector2 yawLimits;
        public readonly Vector2 pitchLimits;
        public readonly float zoomRate;
        public readonly float yawRate;
        public readonly float pitchRate;
        public readonly int cameraId;
        public float currentFoV;
        public float currentYaw;
        public float currentPitch;
        public bool seekHome;

        public SteerableCameraParameters(string _cameraTransform,
            Vector2 _fovLimits, Vector2 _yawLimits, Vector2 _pitchLimits,
            float _zoomRate, float _yawRate, float _pitchRate, int _cameraId)
        {
            cameraTransform = _cameraTransform;
            cameraId = _cameraId;

            // canonicalize the limits
            fovLimits = _fovLimits;
            if (fovLimits.x > fovLimits.y)
            {
                float f = fovLimits.x;
                fovLimits.x = fovLimits.y;
                fovLimits.y = f;
            }

            yawLimits = _yawLimits;
            if (yawLimits.x > yawLimits.y)
            {
                float f = yawLimits.x;
                yawLimits.x = yawLimits.y;
                yawLimits.y = f;
            }

            pitchLimits = _pitchLimits;
            if (pitchLimits.x > pitchLimits.y)
            {
                float f = pitchLimits.x;
                pitchLimits.x = pitchLimits.y;
                pitchLimits.y = f;
            }

            // Always requiure 0.0 to be within the legal range of yuaw
            // and pitch.
            yawLimits.x = Math.Min(0.0f, yawLimits.x);
            yawLimits.y = Math.Max(0.0f, yawLimits.y);
            pitchLimits.x = Math.Min(0.0f, pitchLimits.x);
            pitchLimits.y = Math.Max(0.0f, pitchLimits.y);

            zoomRate = _zoomRate;
            yawRate = _yawRate;
            pitchRate = _pitchRate;

            currentFoV = fovLimits.y;
            currentYaw = 0.0f;
            currentPitch = 0.0f;
            seekHome = false;
        }
    }
}
