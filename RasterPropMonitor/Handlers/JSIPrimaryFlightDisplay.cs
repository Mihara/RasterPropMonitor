using UnityEngine;
using System;

namespace JSI
{
    public class JSIPrimaryFlightDisplay : InternalModule
    {
        [KSPField]
        public int drawingLayer = 17;
        [KSPField]
        public string horizonTexture = "RasterPropMonitor/Library/Components/NavBall/NavBall000";
        [KSPField]
        public string navBallModel = "RasterPropMonitor/Library/Components/NavBall/NavBall";
        [KSPField]
        public string staticOverlay;
        [KSPField]
        public string headingBar;
        [KSPField]
        public bool ballIsEmissive;
        [KSPField]
        public string backgroundColor = string.Empty;
        private Color backgroundColorValue = Color.black;
        [KSPField]
        public float ballOpacity = 0.8f;
        [KSPField]
        public string ballColor = string.Empty;
        private Color ballColorValue = Color.white;
        [KSPField]
        public float markerScale = 0.1f;
        [KSPField] // x,y, width, height
        public Vector4 headingBarPosition = new Vector4(0, 0.8f, 0.8f, 0.1f);
        [KSPField]
        public float headingSpan = 0.25f;
        [KSPField]
        public bool headingAboveOverlay;
        [KSPField]
        public string progradeColor = string.Empty;
        private Color progradeColorValue = new Color(0.84f, 0.98f, 0);
        [KSPField]
        public string maneuverColor = string.Empty;
        private Color maneuverColorValue = new Color(0, 0.1137f, 1);
        [KSPField]
        public string targetColor = string.Empty;
        private Color targetColorValue = Color.magenta;
        [KSPField]
        public string normalColor = string.Empty;
        private Color normalColorValue = new Color(0.930f, 0, 1);
        [KSPField]
        public string radialColor = string.Empty;
        private Color radialColorValue = new Color(0, 1, 0.958f);
        [KSPField]
        public string dockingColor = string.Empty;
        private Color dockingColorValue = Color.red;
        [KSPField]
        public string waypointColor = string.Empty;
        // MOARdV: Don't know what color and what icon to use.  Haven't received any feedback.
        private Color waypointColorValue = Color.magenta;
        [KSPField]
        public float cameraSpan = 1f;
        [KSPField]
        public Vector2 cameraShift = Vector2.zero;
        [KSPField]
        public int speedModeButton = 4;
        private Texture2D horizonTex;
        private Material overlayMaterial;
        private Material headingMaterial;
        private Texture2D gizmoTexture;
        private NavBall stockNavBall;
        private GameObject navBall;
        private GameObject cameraBody;
        private GameObject overlay;
        private GameObject heading;
        private Camera ballCamera;
        // Markers...
        private GameObject markerPrograde;
        private GameObject markerRetrograde;
        private GameObject markerManeuver;
        private GameObject markerManeuverMinus;
        private GameObject markerTarget;
        private GameObject markerTargetMinus;
        private GameObject markerNormal;
        private GameObject markerNormalMinus;
        private GameObject markerRadial;
        private GameObject markerRadialMinus;
        private GameObject markerDockingAlignment;
        private GameObject markerNavWaypoint;
        // Misc...
        private float cameraAspect;
        private bool startupComplete;
        // This is honestly very badly written code, probably the worst of what I have in this project.
        // Much of it dictated by the fact that I barely, if at all, understand what am I doing in vector mathematics,
        // the rest is because the problem is all built out of special cases.
        // Sorry. :)
        public bool RenderPFD(RenderTexture screen, float aspect)
        {
            if (screen == null || !startupComplete || HighLogic.LoadedSceneIsEditor)
                return false;

            // Analysis disable once CompareOfFloatsByEqualityOperator
            if (aspect != cameraAspect)
            {
                cameraAspect = aspect;
                ballCamera.aspect = cameraAspect;
            }
            GL.Clear(true, true, backgroundColorValue);

            ballCamera.targetTexture = screen;


            Vector3d coM = vessel.findWorldCenterOfMass();
            Vector3d up = (coM - vessel.mainBody.position).normalized;
            Vector3d velocityVesselOrbit = vessel.orbit.GetVel();
            Vector3d velocityVesselOrbitUnit = velocityVesselOrbit.normalized;
            Vector3d velocityVesselSurface = velocityVesselOrbit - vessel.mainBody.getRFrmVel(coM);
            Vector3d velocityVesselSurfaceUnit = velocityVesselSurface.normalized;
            Vector3d radialPlus = Vector3d.Exclude(velocityVesselOrbit, up).normalized;
            Vector3d normalPlus = -Vector3d.Cross(radialPlus, velocityVesselOrbitUnit);

            //Vector3d targetDirection = -FlightGlobals.fetch.vesselTargetDirection.normalized;
            Vector3d targetDirection = FlightGlobals.ship_tgtVelocity.normalized;

            navBall.transform.rotation = MirrorX(stockNavBall.navBall.rotation);

            if (heading != null)
            {
                Vector3d north = Vector3d.Exclude(up, (vessel.mainBody.position + (Vector3d)vessel.mainBody.transform.up * vessel.mainBody.Radius) - coM).normalized;
                Quaternion rotationSurface = Quaternion.LookRotation(north, up);
                Quaternion rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);
                heading.renderer.material.SetTextureOffset("_MainTex",
                    new Vector2(JUtil.DualLerp(0f, 1f, 0f, 360f, rotationVesselSurface.eulerAngles.y) - headingSpan / 2f, 0));
            }

            Quaternion gymbal = stockNavBall.attitudeGymbal;
            switch (FlightUIController.speedDisplayMode)
            {
                case FlightUIController.SpeedDisplayModes.Surface:
                    MoveMarker(markerPrograde, velocityVesselSurfaceUnit, progradeColorValue, gymbal);
                    MoveMarker(markerRetrograde, -velocityVesselSurfaceUnit, progradeColorValue, gymbal);
                    break;
                case FlightUIController.SpeedDisplayModes.Target:
                    MoveMarker(markerPrograde, targetDirection, progradeColorValue, gymbal);
                    MoveMarker(markerRetrograde, -targetDirection, progradeColorValue, gymbal);
                    break;
                case FlightUIController.SpeedDisplayModes.Orbit:
                    MoveMarker(markerPrograde, velocityVesselOrbitUnit, progradeColorValue, gymbal);
                    MoveMarker(markerRetrograde, -velocityVesselOrbitUnit, progradeColorValue, gymbal);
                    break;
            }
            MoveMarker(markerNormal, normalPlus, normalColorValue, gymbal);
            MoveMarker(markerNormalMinus, -normalPlus, normalColorValue, gymbal);

            MoveMarker(markerRadial, radialPlus, radialColorValue, gymbal);
            MoveMarker(markerRadialMinus, -radialPlus, radialColorValue, gymbal);

            if (vessel.patchedConicSolver != null && vessel.patchedConicSolver.maneuverNodes.Count > 0)
            {
                Vector3d burnVector = vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit);
                MoveMarker(markerManeuver, burnVector.normalized, maneuverColorValue, gymbal);
                MoveMarker(markerManeuverMinus, -burnVector.normalized, maneuverColorValue, gymbal);
                ShowHide(true, markerManeuver, markerManeuverMinus);
            }

            if (FinePrint.WaypointManager.navIsActive() == true)
            {
                Vector3d waypointPosition = vessel.mainBody.GetWorldSurfacePosition(FinePrint.WaypointManager.navWaypoint.latitude, FinePrint.WaypointManager.navWaypoint.longitude, FinePrint.WaypointManager.navWaypoint.altitude);
                Vector3d waypointDirection = (waypointPosition - coM).normalized;
                MoveMarker(markerNavWaypoint, waypointDirection, waypointColorValue, gymbal);
                ShowHide(true, markerNavWaypoint);
            }

            ITargetable target = FlightGlobals.fetch.VesselTarget;
            if (target != null)
            {
                Vector3 targetSeparation = (vessel.GetTransform().position - target.GetTransform().position).normalized;
                MoveMarker(markerTarget, targetSeparation, targetColorValue, gymbal);
                MoveMarker(markerTargetMinus, -targetSeparation, targetColorValue, gymbal);
                var targetPort = target as ModuleDockingNode;
                if (targetPort != null)
                {
                    // Thanks to Michael Enßlin 
                    Transform targetTransform = targetPort.transform;
                    Transform selfTransform = vessel.ReferenceTransform;
                    Vector3 targetOrientationVector = -targetTransform.up.normalized;

                    Vector3 v1 = Vector3.Cross(selfTransform.up, targetTransform.forward);
                    Vector3 v2 = Vector3.Cross(selfTransform.up, selfTransform.forward);
                    float angle = Vector3.Angle(v1, v2);
                    if (Vector3.Dot(selfTransform.up, Vector3.Cross(v1, v2)) < 0)
                        angle = -angle;
                    MoveMarker(markerDockingAlignment, targetOrientationVector, dockingColorValue, gymbal);
                    markerDockingAlignment.transform.Rotate(Vector3.up, -angle);
                    ShowHide(true, markerDockingAlignment);
                }
                ShowHide(true, markerTarget, markerTargetMinus);
            }


            // This dirty hack reduces the chance that the ball might get affected by internal cabin lighting.
            int backupQuality = QualitySettings.pixelLightCount;
            QualitySettings.pixelLightCount = 0;

            ShowHide(true,
                cameraBody, navBall, overlay, heading, markerPrograde, markerRetrograde,
                markerNormal, markerNormalMinus, markerRadial, markerRadialMinus);
            ballCamera.Render();
            QualitySettings.pixelLightCount = backupQuality;
            ShowHide(false,
                cameraBody, navBall, overlay, heading, markerPrograde, markerRetrograde,
                markerManeuver, markerManeuverMinus, markerTarget, markerTargetMinus,
                markerNormal, markerNormalMinus, markerRadial, markerRadialMinus, markerDockingAlignment, markerNavWaypoint);

            return true;
        }

        public void ButtonProcessor(int buttonID)
        {
            if (buttonID == speedModeButton)
                FlightUIController.fetch.cycleSpdModes();
        }

        private static void MoveMarker(GameObject marker, Vector3 position, Color nativeColor, Quaternion voodooGymbal)
        {
            const float markerRadius = 0.5f;
            const float markerPlane = 1.4f;
            marker.transform.position = FixMarkerPosition(position, voodooGymbal) * markerRadius;
            marker.renderer.material.color = new Color(nativeColor.r, nativeColor.g, nativeColor.b, (float)(marker.transform.position.z + 0.5));
            marker.transform.position = new Vector3(marker.transform.position.x, marker.transform.position.y, markerPlane);
            FaceCamera(marker);
        }

        private static Vector3 FixMarkerPosition(Vector3 thatVector, Quaternion thatVoodoo)
        {
            Vector3 returnVector = thatVoodoo * thatVector;
            returnVector.x = -returnVector.x;
            return returnVector;
        }

        private static Quaternion MirrorX(Quaternion input)
        {
            // Witchcraft: It's called mirroring the X axis of the quaternion's conjugate.
            return new Quaternion(input.x, -input.y, -input.z, input.w);
        }

        public GameObject BuildMarker(int iconX, int iconY, Color nativeColor)
        {

            GameObject marker = CreateSimplePlane("RPMPFDMarker" + iconX + iconY + internalProp.propID, markerScale, drawingLayer);
            marker.renderer.material = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
            marker.renderer.material.mainTexture = gizmoTexture;
            marker.renderer.material.mainTextureScale = Vector2.one / 3f;
            marker.renderer.material.mainTextureOffset = new Vector2(iconX * (1f / 3f), iconY * (1f / 3f));
            marker.renderer.material.color = nativeColor;
            marker.transform.position = Vector3.zero;
            ShowHide(false, marker);
            return marker;
        }

        public void Start()
        {

            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            try
            {
                // Parse bloody KSPField colors.
                if (!string.IsNullOrEmpty(backgroundColor))
                {
                    backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);
                }
                if (!string.IsNullOrEmpty(ballColor))
                {
                    ballColorValue = ConfigNode.ParseColor32(ballColor);
                }
                if (!string.IsNullOrEmpty(progradeColor))
                {
                    progradeColorValue = ConfigNode.ParseColor32(progradeColor);
                }
                if (!string.IsNullOrEmpty(maneuverColor))
                {
                    maneuverColorValue = ConfigNode.ParseColor32(maneuverColor);
                }
                if (!string.IsNullOrEmpty(targetColor))
                {
                    targetColorValue = ConfigNode.ParseColor32(targetColor);
                }
                if (!string.IsNullOrEmpty(normalColor))
                {
                    normalColorValue = ConfigNode.ParseColor32(normalColor);
                }
                if (!string.IsNullOrEmpty(radialColor))
                {
                    radialColorValue = ConfigNode.ParseColor32(radialColor);
                }
                if (!string.IsNullOrEmpty(dockingColor))
                {
                    dockingColorValue = ConfigNode.ParseColor32(dockingColor);
                }
                if (!string.IsNullOrEmpty(waypointColor))
                {
                    waypointColorValue = ConfigNode.ParseColor32(waypointColor);
                }


                Shader unlit = Shader.Find("KSP/Alpha/Unlit Transparent");
                overlayMaterial = new Material(unlit);
                overlayMaterial.mainTexture = GameDatabase.Instance.GetTexture(staticOverlay.EnforceSlashes(), false);

                if (!string.IsNullOrEmpty(headingBar))
                {
                    headingMaterial = new Material(unlit);
                    headingMaterial.mainTexture = GameDatabase.Instance.GetTexture(headingBar.EnforceSlashes(), false);
                }
                horizonTex = GameDatabase.Instance.GetTexture(horizonTexture.EnforceSlashes(), false);

                gizmoTexture = JUtil.GetGizmoTexture();

                // Ahaha, that's clever, does it work?
                stockNavBall = GameObject.Find("NavBall").GetComponent<NavBall>();
                // ...well, it does, but the result is bizarre,
                // apparently, because the stock BALL ITSELF IS MIRRORED.

                navBall = GameDatabase.Instance.GetModel(navBallModel.EnforceSlashes());
                Destroy(navBall.collider);
                navBall.name = "RPMNB" + navBall.GetInstanceID();
                navBall.layer = drawingLayer;
                navBall.transform.position = Vector3.zero;
                navBall.transform.rotation = Quaternion.identity;
                navBall.transform.localRotation = Quaternion.identity;

                if (ballIsEmissive)
                {
                    navBall.renderer.material.shader = Shader.Find("KSP/Emissive/Diffuse");
                    navBall.renderer.material.SetTexture("_MainTex", horizonTex);
                    navBall.renderer.material.SetTextureOffset("_Emissive", navBall.renderer.material.GetTextureOffset("_MainTex"));
                    navBall.renderer.material.SetTexture("_Emissive", horizonTex);
                    navBall.renderer.material.SetColor("_EmissiveColor", ballColorValue);
                }
                else
                {
                    navBall.renderer.material.shader = Shader.Find("KSP/Unlit");
                    navBall.renderer.material.mainTexture = horizonTex;
                    navBall.renderer.material.color = ballColorValue;
                }
                navBall.renderer.material.SetFloat("_Opacity", ballOpacity);

                markerPrograde = BuildMarker(0, 2, progradeColorValue);
                markerRetrograde = BuildMarker(1, 2, progradeColorValue);
                markerManeuver = BuildMarker(2, 0, maneuverColorValue);
                markerManeuverMinus = BuildMarker(1, 2, maneuverColorValue);
                markerTarget = BuildMarker(2, 1, targetColorValue);
                markerTargetMinus = BuildMarker(2, 2, targetColorValue);
                markerNormal = BuildMarker(0, 0, normalColorValue);
                markerNormalMinus = BuildMarker(1, 0, normalColorValue);
                markerRadial = BuildMarker(1, 1, radialColorValue);
                markerRadialMinus = BuildMarker(0, 1, radialColorValue);

                markerDockingAlignment = BuildMarker(0, 2, dockingColorValue);
                markerNavWaypoint = BuildMarker(0, 2, waypointColorValue);

                // Non-moving parts...
                cameraBody = new GameObject();
                cameraBody.name = "RPMPFD" + cameraBody.GetInstanceID();
                cameraBody.layer = drawingLayer;
                ballCamera = cameraBody.AddComponent<Camera>();
                ballCamera.enabled = false;
                ballCamera.orthographic = true;
                ballCamera.clearFlags = CameraClearFlags.Nothing;
                ballCamera.eventMask = 0;
                ballCamera.farClipPlane = 3f;
                ballCamera.orthographicSize = cameraSpan;
                ballCamera.cullingMask = 1 << drawingLayer;
                ballCamera.clearFlags = CameraClearFlags.Depth;
                // -2,0,0 seems to get the orientation exactly as the ship.
                // But logically, forward is Z+, right?
                // Which means that 
                ballCamera.transform.position = new Vector3(0, 0, 2);
                ballCamera.transform.LookAt(Vector3.zero, Vector3.up);
                ballCamera.transform.position = new Vector3(cameraShift.x, cameraShift.y, 2);

                overlay = CreateSimplePlane("RPMPFDOverlay" + internalProp.propID, 1f, drawingLayer);
                overlay.layer = drawingLayer;
                overlay.transform.position = new Vector3(0, 0, 1.5f);
                overlay.renderer.material = overlayMaterial;
                overlay.transform.parent = cameraBody.transform;
                FaceCamera(overlay);

                if (headingMaterial != null)
                {
                    heading = CreateSimplePlane("RPMPFDHeading" + internalProp.propID, 1f, drawingLayer);
                    heading.layer = drawingLayer;
                    heading.transform.position = new Vector3(headingBarPosition.x, headingBarPosition.y, headingAboveOverlay ? 1.55f : 1.45f);
                    heading.transform.parent = cameraBody.transform;
                    heading.transform.localScale = new Vector3(headingBarPosition.z, 0, headingBarPosition.w);
                    heading.renderer.material = headingMaterial;
                    heading.renderer.material.SetTextureScale("_MainTex", new Vector2(headingSpan, 1f));
                    FaceCamera(heading);
                }

                ShowHide(false, navBall, cameraBody, overlay, heading);
                startupComplete = true;
            }
            catch
            {
                JUtil.AnnoyUser(this);
                throw;
            }
        }

        private static void FaceCamera(GameObject thatObject)
        {
            if (thatObject == null)
                throw new ArgumentNullException("thatObject");
            // This is known to rotate correctly, so I'll keep it around.
            /*
            Vector3 originalPosition = thatObject.transform.position;
            thatObject.transform.position = Vector3.zero;
            thatObject.transform.LookAt(Vector3.down,Vector3.back);
            thatObject.transform.position = originalPosition;
            */
            thatObject.transform.rotation = Quaternion.Euler(90, 180, 0);
        }

        private static void ShowHide(bool status, params GameObject[] objects)
        {
            foreach (GameObject thatObject in objects)
            {
                if (thatObject != null)
                {
                    thatObject.SetActive(status);
                    if (thatObject.renderer != null)
                        thatObject.renderer.enabled = status;
                }
            }
        }
        // This function courtesy of EnhancedNavBall.
        private static GameObject CreateSimplePlane(
            string name,
            float vectorSize,
            int drawingLayer)
        {
            var mesh = new Mesh();

            var obj = new GameObject(name);
            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            obj.AddComponent<MeshRenderer>();

            const float uvize = 1f;

            var p0 = new Vector3(-vectorSize, 0, vectorSize);
            var p1 = new Vector3(vectorSize, 0, vectorSize);
            var p2 = new Vector3(-vectorSize, 0, -vectorSize);
            var p3 = new Vector3(vectorSize, 0, -vectorSize);

            mesh.vertices = new[] {
                p0, p1, p2,
                p1, p3, p2
            };

            mesh.triangles = new[] {
                0, 1, 2,
                3, 4, 5
            };

            var uv1 = new Vector2(0, 0);
            var uv2 = new Vector2(uvize, uvize);
            var uv3 = new Vector2(0, uvize);
            var uv4 = new Vector2(uvize, 0);

            mesh.uv = new[] {
                uv1, uv4, uv3,
                uv4, uv2, uv3
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();

            meshFilter.mesh = mesh;

            obj.layer = drawingLayer;

            Destroy(obj.collider);

            return obj;
        }
    }
}
