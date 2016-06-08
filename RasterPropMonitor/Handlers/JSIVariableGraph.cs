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
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

namespace JSI
{
    public class JSIVariableGraph : InternalModule
    {
        [KSPField]
        public string graphSet;
        [KSPField]
        public Vector4 graphRect = new Vector4(32, 32, 608, 608);
        [KSPField]
        public float xSpan;
        [KSPField]
        public Vector2 ySpan;
        [KSPField]
        public string borderColor = string.Empty;
        private Color borderColorValue = Color.white;
        [KSPField]
        public int borders = 2;
        [KSPField]
        public float secondsBetweenSamples = 0.5f;
        [KSPField]
        public string backgroundColor = string.Empty;
        private Color backgroundColorValue = Color.black;
        [KSPField]
        public string backgroundTextureURL = string.Empty;
        private readonly List<GraphLine> graphs = new List<GraphLine>();
        public static Material lineMaterial = JUtil.DrawLineMaterial();
        private Rect graphSpace;
        private double lastDataPoint;
        private Texture2D backgroundTexture;
        // Because KSPField can't handle double. :E
        private double xGraphSpan, interval;
        private readonly List<Vector2> borderVertices = new List<Vector2>();
        private bool startupComplete;
        private RasterPropMonitorComputer rpmComp;

        public void Start()
        {

            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            try
            {
                rpmComp = RasterPropMonitorComputer.Instantiate(internalProp, true);

                if (!string.IsNullOrEmpty(borderColor))
                {
                    borderColorValue = ConfigNode.ParseColor32(borderColor);
                }
                if (!string.IsNullOrEmpty(backgroundColor))
                {
                    backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);
                }

                //comp = RasterPropMonitorComputer.Instantiate(internalProp);
                graphSpace = new Rect();
                graphSpace.xMin = graphRect.x;
                graphSpace.yMin = graphRect.y;
                graphSpace.xMax = graphRect.z;
                graphSpace.yMax = graphRect.w;
                xGraphSpan = xSpan;
                interval = secondsBetweenSamples;
                if (GameDatabase.Instance.ExistsTexture(backgroundTextureURL.EnforceSlashes()))
                {
                    backgroundTexture = GameDatabase.Instance.GetTexture(backgroundTextureURL.EnforceSlashes(), false);
                }

                var bottomLeft = new Vector2(graphSpace.xMin, graphSpace.yMin);
                var bottomRight = new Vector2(graphSpace.xMax, graphSpace.yMin);
                var topLeft = new Vector2(graphSpace.xMin, graphSpace.yMax);
                var topRight = new Vector2(graphSpace.xMax, graphSpace.yMax);


                switch (borders)
                {
                    case 2:
                        borderVertices.Add(bottomRight);
                        borderVertices.Add(bottomLeft);
                        borderVertices.Add(topLeft);
                        break;
                    case 4:
                        borderVertices.Add(bottomLeft);
                        borderVertices.Add(topLeft);
                        borderVertices.Add(topRight);
                        borderVertices.Add(bottomRight);
                        borderVertices.Add(bottomLeft);
                        break;
                }

                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("JSIGRAPHSET"))
                {
                    if (node.HasValue("name") && node.GetValue("name") == graphSet)
                    {
                        foreach (ConfigNode graphNode in node.GetNodes("GRAPH"))
                        {
                            graphs.Add(new GraphLine(graphNode, rpmComp, xGraphSpan, ySpan, interval));
                        }
                    }
                }
                JUtil.LogMessage(this, "Graphing {0} values.", graphs.Count);
                startupComplete = true;
            }
            catch
            {
                JUtil.AnnoyUser(this);
                throw;
            }
        }

        public void OnDestroy()
        {
            //JUtil.LogMessage(this, "OnDestroy()");
        }

        // Analysis disable once UnusedParameter
        public bool RenderGraphs(RenderTexture screen, float cameraAspect)
        {
            if (!startupComplete)
                return false;

            if (backgroundTexture != null)
                Graphics.Blit(backgroundTexture, screen);
            GL.Clear(true, (backgroundTexture == null), backgroundColorValue);

            GL.PushMatrix();
            // This way 0,0 is in bottom left corner, which is what we want this time.
            GL.LoadPixelMatrix(0, screen.width, 0, screen.height);
            double time = Planetarium.GetUniversalTime();
            foreach (GraphLine graph in graphs)
                graph.Draw(graphSpace, time);
            if (borders > 0)
                GraphLine.DrawVector(borderVertices, borderColorValue);

            GL.PopMatrix();
            return true;
        }

        public override void OnUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            double time = Planetarium.GetUniversalTime();
            if (lastDataPoint + (double)secondsBetweenSamples < time)
            {
                foreach (GraphLine graph in graphs)
                {
                    graph.Update(time);
                }
                lastDataPoint = time;
            }
        }

        private class GraphLine
        {
            private readonly Color32 lineColor;
            private readonly List<Vector2d> points = new List<Vector2d>();
            private readonly int maxPoints;
            private readonly VariableOrNumber variable;
            private readonly double horizontalSpan;
            // Analysis disable once FieldCanBeMadeReadOnly.Local
            private Vector2 verticalSpan;
            private bool floatingMax, floatingMin;

            public GraphLine(ConfigNode node, RasterPropMonitorComputer rpmComp, double xSpan, Vector2 ySpan, double secondsBetweenSamples)
            {
                maxPoints = (int)(xSpan / secondsBetweenSamples);
                horizontalSpan = xSpan;
                verticalSpan = ySpan;
                if (!node.HasData)
                    throw new ArgumentException("Graph block with no data?");
                string variableName = string.Empty;
                if (node.HasValue("variableName"))
                {
                    variableName = node.GetValue("variableName").Trim();
                    variable = rpmComp.InstantiateVariableOrNumber(variableName);
                }
                else
                {
                    throw new ArgumentException("Draw a graph of what?");
                }

                lineColor = Color.white;
                if (node.HasValue("color"))
                    lineColor = ConfigNode.ParseColor32(node.GetValue("color"));

                floatingMax = node.HasValue("floatingMaximum");
                floatingMin = node.HasValue("floatingMinimum");

                JUtil.LogMessage(this, "Graphing {0} in color {1}", variableName, lineColor);
            }

            public void Draw(Rect screenRect, double time)
            {
                double mintime = time - horizontalSpan;
                if (floatingMin && points.Count > 0)
                {
                    verticalSpan.x = (float)points[0].y;
                    foreach (Vector2d dataPoint in points)
                    {
                        verticalSpan.x = (float)Math.Min(dataPoint.y, verticalSpan.x);
                    }
                }
                if (floatingMax && points.Count > 0)
                {
                    verticalSpan.y = (float)points[0].y;
                    foreach (Vector2d dataPoint in points)
                    {
                        verticalSpan.y = (float)Math.Max(dataPoint.y, verticalSpan.y);
                    }
                }
                var actualXY = new List<Vector2>();
                foreach (Vector2d dataPoint in points)
                {
                    if (dataPoint.x > mintime)
                        actualXY.Add(new Vector2(
                            (float)JUtil.DualLerp(screenRect.xMin, screenRect.xMax, mintime, time, dataPoint.x),
                            (float)JUtil.DualLerp(screenRect.yMin, screenRect.yMax, verticalSpan.x, verticalSpan.y, dataPoint.y)
                        ));
                }
                DrawVector(actualXY, lineColor);
            }

            public void Update(double time)
            {
                double value = variable.AsDouble();
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    return;
                }
                points.Add(new Vector2d(time, value));
                if (points.Count > maxPoints)
                {
                    points.RemoveRange(0, points.Count - maxPoints);
                }
            }

            public static void DrawVector(List<Vector2> points, Color32 lineColor)
            {
                if (points.Count < 2)
                    return;
                GL.Begin(GL.LINES);
                lineMaterial.SetPass(0);
                GL.Color(lineColor);

                Vector2 start, end;
                start = points[0];
                for (int i = 1; i < points.Count; i++)
                {
                    end = points[i];
                    GL.Vertex(start);
                    GL.Vertex(end);
                    start = end;
                }
                // Reset color to white.
                GL.Color(Color.white);
                GL.End();
            }
        }
    }
}

