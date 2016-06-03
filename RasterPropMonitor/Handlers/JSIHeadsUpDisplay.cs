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
using System.Text;
using UnityEngine;

namespace JSI
{
    /*************************************************************************
     * JSIHeadsUpDisplay provides an alternative to the Primary Flight Display
     * for use in aircraft.  Instead of a spherical nav ball, pitch and roll
     * are displayed with a "ladder" (texture).  Strips also provide heading
     * information.
     ************************************************************************/
    class JSIHeadsUpDisplay : InternalModule
    {
        [KSPField]
        public string cameraTransform = string.Empty;
        private FlyingCamera cameraObject;
        [KSPField]
        public float hudFov = 60.0f;
        [KSPField]
        public int drawingLayer = 17;

        [KSPField]
        public string backgroundColor = "0,0,0,0";
        private Color32 backgroundColorValue;

        // Static overlay
        [KSPField]
        public string staticOverlay = string.Empty;

        // Ladder
        [KSPField]
        public Vector2 horizonSize = new Vector2(64.0f, 32.0f);
        [KSPField]
        public string horizonTexture = string.Empty;
        [KSPField]
        public bool use360horizon = true;
        [KSPField] // Number of texels of the horizon texture to draw (width).
        public Vector2 horizonTextureSize = new Vector2(1f, 1f);
        [KSPField]
        public Vector2 horizonOffset = Vector2.zero;
        private Vector4 cropBound = Vector4.zero;
        private Material ladderMaterial;

        [KSPField]
        public string headingBar = string.Empty;
        [KSPField] // x,y, width, height in pixels
        public Vector4 headingBarPosition = new Vector4(0f, 0f, 0f, 0f);
        [KSPField]
        public float headingBarWidth = 0.0f;
        private float headingBarTextureWidth;

        [KSPField]
        public bool showHeadingBarPrograde = true;
        [KSPField]
        public bool showLadderPrograde = true;
        [KSPField]
        public string progradeColor = string.Empty;
        private Color progradeColorValue = new Color(0.84f, 0.98f, 0);
        [KSPField]
        public float iconPixelSize = 64f;
        [KSPField]
        public string headingBarProgradeTexture = string.Empty;
        [KSPField]
        public string ladderProgradeTexture = string.Empty;

        // Vertical bars
        [KSPField]
        public string verticalBar = string.Empty;
        private List<VerticalBar> verticalBars = new List<VerticalBar>();

        // Horizontal bars
        [KSPField]
        public string horizontalBar = string.Empty;
        private List<HorizontalBar> horizontalBars = new List<HorizontalBar>();

        private GameObject cameraBody;
        private Camera hudCamera;

        private GameObject ladderMesh;
        private GameObject progradeLadderIcon;
        private GameObject overlayMesh;
        private GameObject headingMesh;
        private GameObject progradeHeadingIcon;
        private float progradeHeadingIconOrigin;
        private RasterPropMonitorComputer rpmComp;

        private float lastRoll = 0.0f;

        private bool startupComplete;
        private bool firstRenderComplete;

        /// <summary>
        /// Initialize the renderable game objects for the HUD.
        /// </summary>
        void InitializeRenderables(RenderTexture screen)
        {
            float screenWidth = (float)screen.width;
            float screenHeight = (float)screen.height;

            Shader displayShader = JUtil.LoadInternalShader("RPM/DisplayShader");

            if (!string.IsNullOrEmpty(cameraTransform))
            {
                cameraObject = new FlyingCamera(part, hudCamera.aspect);
                cameraObject.PointCamera(cameraTransform, hudFov);
            }

            if (!string.IsNullOrEmpty(staticOverlay))
            {
                Material overlayMaterial = new Material(displayShader);
                overlayMaterial.color = Color.white;
                Texture overlayTexture = GameDatabase.Instance.GetTexture(staticOverlay.EnforceSlashes(), false);
                overlayMaterial.mainTexture = overlayTexture;

                overlayMesh = JUtil.CreateSimplePlane("JSIHeadsUpDisplayOverlay" + hudCamera.GetInstanceID(), screenWidth * 0.5f, drawingLayer);
                overlayMesh.transform.position = new Vector3(0, 0, 1.0f);
                overlayMesh.GetComponent<Renderer>().material = overlayMaterial;
                overlayMesh.transform.parent = cameraBody.transform;

                JUtil.ShowHide(false, overlayMesh);
            }

            if (!string.IsNullOrEmpty(horizonTexture))
            {
                Shader ladderShader = JUtil.LoadInternalShader("RPM/CroppedDisplayShader");
                ladderMaterial = new Material(ladderShader);

                // _CropBound is in device normalized coordinates (-1 - +1)
                cropBound = new Vector4((horizonOffset.x - horizonSize.x) / screenWidth, (horizonOffset.y - horizonSize.y) / screenHeight, (horizonOffset.x + horizonSize.x) / screenWidth, (horizonOffset.y + horizonSize.y) / screenHeight);
                ladderMaterial.SetVector("_CropBound", cropBound);
                ladderMaterial.color = Color.white;
                ladderMaterial.mainTexture = GameDatabase.Instance.GetTexture(horizonTexture.EnforceSlashes(), false);
                if (ladderMaterial.mainTexture != null)
                {
                    float diagonal = horizonSize.magnitude / Mathf.Min(horizonSize.x, horizonSize.y) * 0.5f;
                    Vector2 horizonDrawSize = diagonal * horizonSize;
                    horizonTextureSize.x = 0.5f * (horizonTextureSize.x / ladderMaterial.mainTexture.width);
                    horizonTextureSize.y = 0.5f * (horizonTextureSize.y / ladderMaterial.mainTexture.height);

                    ladderMaterial.mainTexture.wrapMode = TextureWrapMode.Clamp;

                    ladderMesh = JUtil.CreateSimplePlane("JSIHeadsUpDisplayLadder" + hudCamera.GetInstanceID(), horizonDrawSize, new Rect(0.0f, 0.0f, 1.0f, 1.0f), drawingLayer);
                    ladderMesh.transform.position = new Vector3(horizonOffset.x, horizonOffset.y, 1.45f);
                    ladderMesh.GetComponent<Renderer>().material = ladderMaterial;
                    ladderMesh.transform.parent = cameraBody.transform;

                    JUtil.ShowHide(false, ladderMesh);

                    if (progradeColorValue.a > 0.0f && showLadderPrograde)
                    {
                        Material progradeIconMaterial = new Material(displayShader);
                        progradeIconMaterial.color = Color.white;
                        Rect texCoord;
                        if (string.IsNullOrEmpty(ladderProgradeTexture))
                        {
                            progradeIconMaterial.mainTexture = JUtil.GetGizmoTexture();
                            texCoord = GizmoIcons.GetIconLocation(GizmoIcons.IconType.PROGRADE);
                        }
                        else
                        {
                            Texture2D progradeTexture = GameDatabase.Instance.GetTexture(ladderProgradeTexture.EnforceSlashes(), false);
                            if (progradeTexture == null)
                            {
                                JUtil.LogErrorMessage(this, "Failed to find ladder prograde texture \"{0}\".", ladderProgradeTexture);
                            }
                            progradeIconMaterial.mainTexture = progradeTexture;
                            texCoord = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
                        }
                        progradeIconMaterial.SetVector("_Color", progradeColorValue);

                        progradeLadderIcon = JUtil.CreateSimplePlane("JSIHeadsUpDisplayLadderProgradeIcon" + hudCamera.GetInstanceID(), new Vector2(iconPixelSize * 0.5f, iconPixelSize * 0.5f), texCoord, drawingLayer);
                        progradeLadderIcon.transform.position = new Vector3(0.0f, 0.0f, 1.41f);
                        progradeLadderIcon.GetComponent<Renderer>().material = progradeIconMaterial;
                        progradeLadderIcon.transform.parent = cameraBody.transform;
                    }
                }
            }

            if (!string.IsNullOrEmpty(headingBar))
            {
                Material headingMaterial = new Material(displayShader);
                headingMaterial.color = Color.white;
                headingMaterial.mainTexture = GameDatabase.Instance.GetTexture(headingBar.EnforceSlashes(), false);
                if (headingMaterial.mainTexture != null)
                {
                    headingBarTextureWidth = 0.5f * (headingBarWidth / (float)headingMaterial.mainTexture.width);

                    headingMaterial.mainTexture.wrapMode = TextureWrapMode.Repeat;

                    headingMesh = JUtil.CreateSimplePlane("JSIHeadsUpDisplayHeading" + hudCamera.GetInstanceID(), new Vector2(headingBarPosition.z * 0.5f, headingBarPosition.w * 0.5f), new Rect(0.0f, 0.0f, 1.0f, 1.0f), drawingLayer);
                    headingMesh.transform.position = new Vector3(headingBarPosition.x + 0.5f * (headingBarPosition.z - screenWidth), 0.5f * (screenHeight - headingBarPosition.w) - headingBarPosition.y, 1.4f);
                    headingMesh.GetComponent<Renderer>().material = headingMaterial;
                    headingMesh.transform.parent = cameraBody.transform;

                    JUtil.ShowHide(false, headingMesh);

                    if (progradeColorValue.a > 0.0f && showHeadingBarPrograde)
                    {
                        Material progradeIconMaterial = new Material(displayShader);
                        progradeIconMaterial.color = Color.white;
                        Rect texCoord;
                        if (string.IsNullOrEmpty(headingBarProgradeTexture))
                        {
                            progradeIconMaterial.mainTexture = JUtil.GetGizmoTexture();
                            texCoord = GizmoIcons.GetIconLocation(GizmoIcons.IconType.PROGRADE);
                        }
                        else
                        {
                            Texture2D progradeTexture = GameDatabase.Instance.GetTexture(headingBarProgradeTexture.EnforceSlashes(), false);
                            if (progradeTexture == null)
                            {
                                JUtil.LogErrorMessage(this, "Failed to find heading bar prograde texture \"{0}\".", headingBarProgradeTexture);
                            }
                            progradeIconMaterial.mainTexture = progradeTexture;
                            texCoord = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
                        }
                        progradeIconMaterial.SetVector("_Color", progradeColorValue);

                        progradeHeadingIconOrigin = headingBarPosition.x + 0.5f * (headingBarPosition.z - screenWidth);

                        progradeHeadingIcon = JUtil.CreateSimplePlane("JSIHeadsUpDisplayHeadingProgradeIcon" + hudCamera.GetInstanceID(), new Vector2(iconPixelSize * 0.5f, iconPixelSize * 0.5f), texCoord, drawingLayer);
                        progradeHeadingIcon.transform.position = new Vector3(progradeHeadingIconOrigin, 0.5f * (screenHeight - headingBarPosition.w) - headingBarPosition.y, 1.35f);
                        progradeHeadingIcon.GetComponent<Renderer>().material = progradeIconMaterial;
                        progradeHeadingIcon.transform.parent = headingMesh.transform;
                    }
                }
            }

            if (!string.IsNullOrEmpty(verticalBar))
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("JSIHUD_VERTICAL_BAR");
                string[] vBars = verticalBar.Split(';');
                for (int i = 0; i < vBars.Length; ++i)
                {
                    for (int j = 0; j < nodes.Length; ++j)
                    {
                        if (nodes[j].HasValue("name") && vBars[i].Trim() == nodes[j].GetValue("name"))
                        {
                            try
                            {
                                VerticalBar vb = new VerticalBar(nodes[j], screenWidth, screenHeight, drawingLayer, displayShader, cameraBody);
                                verticalBars.Add(vb);
                            }
                            catch (Exception e)
                            {
                                JUtil.LogErrorMessage(this, "Error parsing JSIHUD_VERTICAL_BAR: {0}", e);
                            }
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(horizontalBar))
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("JSIHUD_HORIZONTAL_BAR");
                string[] hBars = horizontalBar.Split(';');
                for (int i = 0; i < hBars.Length; ++i)
                {
                    for (int j = 0; j < nodes.Length; ++j)
                    {
                        if (nodes[j].HasValue("name") && hBars[i].Trim() == nodes[j].GetValue("name"))
                        {
                            try
                            {
                                HorizontalBar hb = new HorizontalBar(nodes[j], screenWidth, screenHeight, drawingLayer, displayShader, cameraBody);
                                horizontalBars.Add(hb);
                            }
                            catch (Exception e)
                            {
                                JUtil.LogErrorMessage(this, "Error parsing JSIHUD_HORIZONTAL_BAR: {0}", e);
                            }
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update the ladder's texture UVs so it's drawn correctly
        /// </summary>
        private void UpdateLadder(Quaternion rotationVesselSurface, RPMVesselComputer comp)
        {
            float pitch = 90.0f - Vector3.Angle(comp.Forward, comp.Up);

            float ladderMidpointCoord;
            if (use360horizon)
            {
                // Straight up is texture coord 0.75;
                // Straight down is TC 0.25;
                ladderMidpointCoord = JUtil.DualLerp(0.25f, 0.75f, -90f, 90f, pitch);
            }
            else
            {
                // Straight up is texture coord 1.0;
                // Straight down is TC 0.0;
                ladderMidpointCoord = JUtil.DualLerp(0.0f, 1.0f, -90f, 90f, pitch);
            }

            // MOARdV TODO: These can be done without manually editing the 
            // mesh filter.  I need to look up the game object texture stuff.
            MeshFilter meshFilter = ladderMesh.GetComponent<MeshFilter>();

            meshFilter.mesh.uv = new[] 
            {
                new Vector2(0.5f - horizonTextureSize.x, ladderMidpointCoord - horizonTextureSize.y),
                new Vector2(0.5f + horizonTextureSize.x, ladderMidpointCoord - horizonTextureSize.y),
                new Vector2(0.5f - horizonTextureSize.x, ladderMidpointCoord + horizonTextureSize.y),
                new Vector2(0.5f + horizonTextureSize.x, ladderMidpointCoord + horizonTextureSize.y)
            };

            float roll = rotationVesselSurface.eulerAngles.z;

            ladderMesh.transform.Rotate(new Vector3(0.0f, 0.0f, 1.0f), lastRoll - roll);

            lastRoll = roll;

            if (progradeLadderIcon != null)
            {
                float AoA = rpmComp.AbsoluteAoA;
                AoA = (float)JUtil.ClampDegrees180(AoA);
                if (float.IsNaN(AoA))
                {
                    AoA = 0.0f;
                }

                float AoATC;
                if (use360horizon)
                {
                    // Straight up is texture coord 0.75;
                    // Straight down is TC 0.25;
                    AoATC = JUtil.DualLerp(0.25f, 0.75f, -90f, 90f, AoA);
                }
                else
                {
                    // Straight up is texture coord 1.0;
                    // Straight down is TC 0.0;
                    AoATC = JUtil.DualLerp(0.0f, 1.0f, -90f, 90f, AoA);
                }

                float Ypos = JUtil.DualLerp(
                                 -horizonSize.y * 0.5f, horizonSize.y * 0.5f,
                                 ladderMidpointCoord - horizonTextureSize.y, ladderMidpointCoord + horizonTextureSize.y,
                                 AoATC);

                Vector3 position = progradeLadderIcon.transform.position;
                position.x = Ypos * Mathf.Sin(roll * Mathf.Deg2Rad);
                position.y = Ypos * Mathf.Cos(roll * Mathf.Deg2Rad);
                progradeLadderIcon.transform.position = position;

                JUtil.ShowHide(true, progradeLadderIcon);
            }
        }

        /// <summary>
        /// Update the compass / heading bar
        /// </summary>
        private void UpdateHeading(Quaternion rotationVesselSurface)
        {
            float heading = rotationVesselSurface.eulerAngles.y / 360.0f;

            MeshFilter meshFilter = headingMesh.GetComponent<MeshFilter>();

            meshFilter.mesh.uv = new[] 
            {
                new Vector2(heading - headingBarTextureWidth, 0.0f),
                new Vector2(heading + headingBarTextureWidth, 0.0f),
                new Vector2(heading - headingBarTextureWidth, 1.0f),
                new Vector2(heading + headingBarTextureWidth, 1.0f)
            };

            if (progradeHeadingIcon != null)
            {
                float slipAngle = rpmComp.Sideslip;
                float slipTC = JUtil.DualLerp(0f, 1f, 0f, 360f, rotationVesselSurface.eulerAngles.y + slipAngle);
                float slipIconX = JUtil.DualLerp(progradeHeadingIconOrigin - 0.5f * headingBarPosition.z, progradeHeadingIconOrigin + 0.5f * headingBarPosition.z, heading - headingBarTextureWidth, heading + headingBarTextureWidth, slipTC);

                Vector3 position = progradeHeadingIcon.transform.position;
                position.x = slipIconX;
                progradeHeadingIcon.transform.position = position;

                JUtil.ShowHide(true, progradeHeadingIcon);
            }
        }

        public bool RenderHUD(RenderTexture screen, float cameraAspect)
        {
            if (screen == null || !startupComplete || HighLogic.LoadedSceneIsEditor)
            {
                return false;
            }

            if (!firstRenderComplete)
            {
                firstRenderComplete = true;
                hudCamera.orthographicSize = (float)(screen.height) * 0.5f;
                hudCamera.aspect = (float)screen.width / (float)screen.height;
                InitializeRenderables(screen);
            }

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);

            for (int i = 0; i < verticalBars.Count; ++i)
            {
                verticalBars[i].Update(rpmComp);
            }
            for (int i = 0; i < horizontalBars.Count; ++i)
            {
                horizontalBars[i].Update(rpmComp);
            }

            GL.Clear(true, true, backgroundColorValue);

            // Draw the camera's view, if configured.
            if (cameraObject != null)
            {
                cameraObject.Render(screen, 0.0f, 0.0f);
            }

            hudCamera.targetTexture = screen;

            // MOARdV TODO: I don't think this does anything...
            GL.Color(Color.white);

            Quaternion rotationVesselSurface = comp.RotationVesselSurface;
            if (headingMesh != null)
            {
                UpdateHeading(rotationVesselSurface);
                JUtil.ShowHide(true, headingMesh);
            }

            if (ladderMesh != null)
            {
                // Viewport doesn't work with this, AFAICT.
                // Anyway, these numbers aren't right for the redesigned HUD.
                //JUtil.LogMessage(this, "screen is {0} x {1}, horizon size is {2} x {3}, making a rectangle at {4} x {5} with size of {6} x {7}",
                //    screen.width,screen.height,
                //    horizonSize.x, horizonSize.y,
                //    (screen.width - horizonSize.x) * 0.5f, (screen.height - horizonSize.y) * 0.5f,
                //    horizonSize.x, horizonSize.y);
                //GL.Viewport(new Rect((screen.width - horizonSize.x) * 0.5f, (screen.height - horizonSize.y) * 0.5f, horizonSize.x, horizonSize.y));
                // Fix up UVs, apply rotation.
                UpdateLadder(rotationVesselSurface, comp);
                ladderMaterial.SetVector("_CropBound", cropBound); 
                JUtil.ShowHide(true, ladderMesh);
                //hudCamera.Render();
                //JUtil.ShowHide(false, ladderMesh);
                //GL.Viewport(new Rect(0, 0, screen.width, screen.height));
            }

            if (overlayMesh != null)
            {
                JUtil.ShowHide(true, overlayMesh);
            }

            hudCamera.Render();

            JUtil.ShowHide(false, overlayMesh, ladderMesh, headingMesh, progradeLadderIcon, progradeHeadingIcon);
            for (int i = 0; i < verticalBars.Count; ++i)
            {
                JUtil.ShowHide(false, verticalBars[i].barObject);
            }
            for (int i = 0; i < horizontalBars.Count; ++i)
            {
                JUtil.ShowHide(false, horizontalBars[i].barObject);
            }

            return true;
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }
            try
            {
                rpmComp = RasterPropMonitorComputer.Instantiate(internalProp, true);

                backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);

                cameraBody = new GameObject();
                cameraBody.name = "RPMPFD" + cameraBody.GetInstanceID();
                cameraBody.layer = drawingLayer;
                hudCamera = cameraBody.AddComponent<Camera>();
                hudCamera.enabled = false;
                hudCamera.orthographic = true;
                hudCamera.eventMask = 0;
                hudCamera.farClipPlane = 3f;
                hudCamera.orthographicSize = 1.0f;
                hudCamera.cullingMask = 1 << drawingLayer;
                // does this actually work?
                hudCamera.backgroundColor = backgroundColorValue;
                hudCamera.clearFlags = CameraClearFlags.Depth | CameraClearFlags.Color;
                hudCamera.transparencySortMode = TransparencySortMode.Orthographic;
                hudCamera.transform.position = Vector3.zero;
                hudCamera.transform.LookAt(new Vector3(0.0f, 0.0f, 1.5f), Vector3.up);

                if (!string.IsNullOrEmpty(progradeColor))
                {
                    progradeColorValue = ConfigNode.ParseColor32(progradeColor);
                }
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Start() failed with an exception: {0}", e);
                JUtil.AnnoyUser(this);
                throw;
            }

            startupComplete = true;
        }

        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                // Nothing configured, nothing to destroy.
                return;
            }

            JUtil.DisposeOfGameObjects(new GameObject[] { ladderMesh, progradeLadderIcon, overlayMesh, headingMesh, progradeHeadingIcon });
            for (int i = 0; i < verticalBars.Count; ++i)
            {
                JUtil.DisposeOfGameObjects(new GameObject[] { verticalBars[i].barObject });
                verticalBars[i] = null;
            }
            verticalBars.Clear();
            for (int i = 0; i < horizontalBars.Count; ++i)
            {
                JUtil.DisposeOfGameObjects(new GameObject[] { horizontalBars[i].barObject });
                horizontalBars[i] = null;
            }
            horizontalBars.Clear();
        }
    }

    class VerticalBar
    {
        private VariableOrNumber variable;
        private Vector2 scale;
        private Vector2 textureLimit;
        public readonly GameObject barObject;
        private float textureSize;
        private bool useLog10;

        private VariableOrNumberRange enablingVariable;

        internal VerticalBar(ConfigNode node, float screenWidth, float screenHeight, int drawingLayer, Shader displayShader, GameObject cameraBody)
        {
            JUtil.LogMessage(this, "Configuring for {0}", node.GetValue("name"));
            if (!node.HasValue("variableName"))
            {
                throw new Exception("VerticalBar " + node.GetValue("name") + " missing variableName");
            }
            variable = VariableOrNumber.Instantiate(node.GetValue("variableName"));

            if (!node.HasValue("texture"))
            {
                throw new Exception("VerticalBar " + node.GetValue("name") + " missing texture");
            }

            Texture2D tex = GameDatabase.Instance.GetTexture(node.GetValue("texture"), false);
            if (tex == null)
            {
                throw new Exception("VerticalBar " + node.GetValue("name") + " texture " + node.GetValue("texture") + " can't be loaded.");
            }
            tex.wrapMode = TextureWrapMode.Clamp;

            if (node.HasValue("useLog10") && bool.TryParse(node.GetValue("useLog10"), out useLog10) == false)
            {
                // I think this is redundant
                useLog10 = false;
            }

            if (!node.HasValue("scale"))
            {
                throw new Exception("VerticalBar " + node.GetValue("name") + " missing scale");
            }

            scale = ConfigNode.ParseVector2(node.GetValue("scale"));
            if (useLog10)
            {
                scale.x = JUtil.PseudoLog10(scale.x);
                scale.y = JUtil.PseudoLog10(scale.y);
            }

            if (!node.HasValue("textureSize"))
            {
                throw new Exception("VerticalBar " + node.GetValue("name") + " missing textureSize");
            }

            if (!float.TryParse(node.GetValue("textureSize"), out textureSize))
            {
                throw new Exception("VerticalBar " + node.GetValue("name") + " failed parsing textureSize");
            }

            textureSize = 0.5f * textureSize / (float)tex.height;

            if (!node.HasValue("textureLimit"))
            {
                throw new Exception("VerticalBar " + node.GetValue("name") + " missing textureLimit");
            }

            textureLimit = ConfigNode.ParseVector2(node.GetValue("textureLimit"));
            textureLimit.x = 1.0f - textureLimit.x / (float)tex.height;
            textureLimit.y = 1.0f - textureLimit.y / (float)tex.height;

            if (!node.HasValue("position"))
            {
                throw new Exception("VerticalBar " + node.GetValue("name") + " missing position");
            }

            Vector4 position = ConfigNode.ParseVector4(node.GetValue("position"));

            if (node.HasValue("enablingVariable") && node.HasValue("enablingVariableRange"))
            {
                string[] range = node.GetValue("enablingVariableRange").Split(',');
                if (range.Length != 2)
                {
                    throw new Exception("VerticalBar " + node.GetValue("name") + " has an invalid enablingVariableRange");
                }

                enablingVariable = new VariableOrNumberRange(node.GetValue("enablingVariable").Trim(), range[0].Trim(), range[1].Trim());
            }

            barObject = JUtil.CreateSimplePlane("VerticalBar" + node.GetValue("name"), new Vector2(0.5f * position.z, 0.5f * position.w), new Rect(0.0f, 0.0f, 1.0f, 1.0f), drawingLayer);

            Material barMaterial = new Material(displayShader);
            barMaterial.color = Color.white;
            barMaterial.mainTexture = tex;

            // Position in camera space has (0, 0) in the center, so we need to
            // translate everything appropriately.  Y is odd since the coordinates
            // supplied are Left-Handed (0Y on top, growing down), not RH.
            barObject.transform.position = new Vector3(position.x + 0.5f * (position.z - screenWidth), 0.5f * (screenHeight - position.w) - position.y, 1.4f);
            barObject.GetComponent<Renderer>().material = barMaterial;
            barObject.transform.parent = cameraBody.transform;

            JUtil.ShowHide(true, barObject);
        }

        internal void Update(RasterPropMonitorComputer rpmComp)
        {
            float value;
            if (enablingVariable != null)
            {
                if (!enablingVariable.IsInRange(rpmComp))
                {
                    return;
                }
            }

            if (variable.Get(out value, rpmComp))
            {
                if (useLog10)
                {
                    value = JUtil.PseudoLog10(value);
                }
                float yOffset = JUtil.DualLerp(textureLimit, scale, value);

                MeshFilter meshFilter = barObject.GetComponent<MeshFilter>();

                meshFilter.mesh.uv = new[] 
                {
                    new Vector2(0.0f, yOffset - textureSize),
                    new Vector2(1.0f, yOffset - textureSize),
                    new Vector2(0.0f, yOffset + textureSize),
                    new Vector2(1.0f, yOffset + textureSize)
                };

                JUtil.ShowHide(true, barObject);
            }
        }
    }

    class HorizontalBar
    {
        private VariableOrNumber variable;
        private Vector2 scale;
        private Vector2 textureLimit;
        public readonly GameObject barObject;
        private float textureSize;
        private bool useLog10;

        private VariableOrNumberRange enablingVariable;

        internal HorizontalBar(ConfigNode node, float screenWidth, float screenHeight, int drawingLayer, Shader displayShader, GameObject cameraBody)
        {
            JUtil.LogMessage(this, "Configuring for {0}", node.GetValue("name"));
            if (!node.HasValue("variableName"))
            {
                throw new Exception("HorizontalBar " + node.GetValue("name") + " missing variableName");
            }
            variable = VariableOrNumber.Instantiate(node.GetValue("variableName"));

            if (!node.HasValue("texture"))
            {
                throw new Exception("HorizontalBar " + node.GetValue("name") + " missing texture");
            }

            Texture2D tex = GameDatabase.Instance.GetTexture(node.GetValue("texture"), false);
            if (tex == null)
            {
                throw new Exception("HorizontalBar " + node.GetValue("name") + " texture " + node.GetValue("texture") + " can't be loaded.");
            }
            tex.wrapMode = TextureWrapMode.Clamp;

            if (node.HasValue("useLog10") && bool.TryParse(node.GetValue("useLog10"), out useLog10) == false)
            {
                // I think this is redundant
                useLog10 = false;
            }

            if (!node.HasValue("scale"))
            {
                throw new Exception("HorizontalBar " + node.GetValue("name") + " missing scale");
            }

            scale = ConfigNode.ParseVector2(node.GetValue("scale"));
            if (useLog10)
            {
                scale.x = JUtil.PseudoLog10(scale.x);
                scale.y = JUtil.PseudoLog10(scale.y);
            }

            if (!node.HasValue("textureSize"))
            {
                throw new Exception("HorizontalBar " + node.GetValue("name") + " missing textureSize");
            }

            if (!float.TryParse(node.GetValue("textureSize"), out textureSize))
            {
                throw new Exception("HorizontalBar " + node.GetValue("name") + " failed parsing textureSize");
            }

            textureSize = 0.5f * textureSize / (float)tex.width;

            if (!node.HasValue("textureLimit"))
            {
                throw new Exception("HorizontalBar " + node.GetValue("name") + " missing textureLimit");
            }

            textureLimit = ConfigNode.ParseVector2(node.GetValue("textureLimit"));
            textureLimit.x = 1.0f - textureLimit.x / (float)tex.width;
            textureLimit.y = 1.0f - textureLimit.y / (float)tex.width;

            if (!node.HasValue("position"))
            {
                throw new Exception("HorizontalBar " + node.GetValue("name") + " missing position");
            }

            Vector4 position = ConfigNode.ParseVector4(node.GetValue("position"));

            if (node.HasValue("enablingVariable") && node.HasValue("enablingVariableRange"))
            {
                string[] range = node.GetValue("enablingVariableRange").Split(',');
                if (range.Length != 2)
                {
                    throw new Exception("HorizontalBar " + node.GetValue("name") + " has an invalid enablingVariableRange");
                }

                enablingVariable = new VariableOrNumberRange(node.GetValue("enablingVariable").Trim(), range[0].Trim(), range[1].Trim());
            }

            barObject = JUtil.CreateSimplePlane("HorizontalBar" + node.GetValue("name"), new Vector2(0.5f * position.z, 0.5f * position.w), new Rect(0.0f, 0.0f, 1.0f, 1.0f), drawingLayer);

            Material barMaterial = new Material(displayShader);
            barMaterial.color = Color.white;
            barMaterial.mainTexture = tex;

            // Position in camera space has (0, 0) in the center, so we need to
            // translate everything appropriately.  Y is odd since the coordinates
            // supplied are Left-Handed (0Y on top, growing down), not RH.
            barObject.transform.position = new Vector3(position.x + 0.5f * (position.z - screenWidth), 0.5f * (screenHeight - position.w) - position.y, 1.4f);
            barObject.GetComponent<Renderer>().material = barMaterial;
            barObject.transform.parent = cameraBody.transform;

            JUtil.ShowHide(true, barObject);
        }

        internal void Update(RasterPropMonitorComputer rpmComp)
        {
            float value;
            if (enablingVariable != null)
            {
                if (!enablingVariable.IsInRange(rpmComp))
                {
                    return;
                }
            }

            if (variable.Get(out value, rpmComp))
            {
                if (useLog10)
                {
                    value = JUtil.PseudoLog10(value);
                }
                float xOffset = JUtil.DualLerp(textureLimit, scale, value);

                MeshFilter meshFilter = barObject.GetComponent<MeshFilter>();

                meshFilter.mesh.uv = new[] 
                {
                    new Vector2(xOffset - textureSize, 0.0f),
                    new Vector2(xOffset + textureSize, 0.0f),
                    new Vector2(xOffset - textureSize, 1.0f),
                    new Vector2(xOffset + textureSize, 1.0f)
                };

                JUtil.ShowHide(true, barObject);
            }
        }
    }
}
