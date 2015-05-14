using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using UnityEngine;

namespace JSI
{
    // JSIGraphingBackground provides an editable / configurable way to render
    // one or more data in a graphical manner.
    class JSIGraphingBackground : InternalModule
    {
        [KSPField]
        public string layout = null;

        private Color32 backgroundColorValue;
        private List<DataSet> dataSets = new List<DataSet>();
        private RasterPropMonitorComputer comp;
        private bool startupComplete = false;
        private Material lineMaterial = JUtil.DrawLineMaterial();
        private Material graphMaterial;

    
        public bool RenderBackground(RenderTexture screen, float cameraAspect)
        {
            if (!enabled)
            {
                return false;
            }

            //JUtil.LogMessage(this, "RenderBackground ({0}, {1})", screen.width, screen.height);
            GL.Clear(true, true, backgroundColorValue);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0.0f, screen.width, screen.height, 0.0f);
            GL.Viewport(new Rect(0, 0, screen.width, screen.height));
            lineMaterial.SetPass(0);

            // Render background - eventually, squirrel this away onto a render tex
            for (int i = 0; i < dataSets.Count; ++i)
            {
                dataSets[i].RenderBackground(screen);
            }

            // Render data
            for (int i = 0; i < dataSets.Count; ++i)
            {
                dataSets[i].RenderData(screen, comp);
            }

            GL.PopMatrix();
            GL.Viewport(new Rect(0, 0, screen.width, screen.height));

            return true;
        }

        public void LateUpdate()
        {
            if (vessel != null && JUtil.VesselIsInIVA(vessel) && !startupComplete)
            {
                JUtil.AnnoyUser(this);
                enabled = false;
            }
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }
            try
            {
                if (string.IsNullOrEmpty(layout))
                {
                    throw new ArgumentNullException("layout");
                }

                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("JSI_GRAPHING_BACKGROUND"))
                {
                    if (node.GetValue("layout") == layout)
                    {
                        JUtil.LogMessage(this, "node {0} has {1} nodes and {2} values", layout, node.CountNodes, node.CountValues);
                        string[] vals = node.GetValues();
                        for (int i = 0; i < vals.Length; ++i)
                        {
                            JUtil.LogMessage(this, "vals[{0}] = {1}", i, vals[i]);
                        }
                        ConfigNode[] nodess = node.GetNodes();
                        for (int i = 0; i < nodess.Length; ++i)
                        {
                            JUtil.LogMessage(this, "nodess[{0}] = {1}", i, nodess[i].name);
                        }
                        if (!node.HasValue("backgroundColor"))
                        {
                            JUtil.LogErrorMessage(this, "?!? no backgroundColor");
                        }
                        string s = node.GetValue("backgroundColor");
                        if(string.IsNullOrEmpty(s))
                        {
                            JUtil.LogErrorMessage(this, "backgroundColor is missing?");
                        }
                        backgroundColorValue = ConfigNode.ParseColor32(node.GetValue("backgroundColor"));

                        ConfigNode[] dataNodes = node.GetNodes("DATA_SET");
                        //JUtil.LogMessage(this, "Found my config with {0} DATA_SET nodes", dataNodes.Length);

                        for (int i = 0; i < dataNodes.Length; i++)
                        {
                            try
                            {
                                dataSets.Add(new DataSet(dataNodes[i]));
                            }
                            catch (ArgumentException e)
                            {
                                JUtil.LogErrorMessage(this, "Error in building prop number {1} - {0}", e.Message, internalProp.propID);
                            }
                        }
                        break;
                    }
                }

                graphMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
                comp = RasterPropMonitorComputer.Instantiate(internalProp);
                startupComplete = true;
            } 

            catch 
            {
				JUtil.AnnoyUser(this);
				throw;
			}
        }
    }

    class DataSet
    {
        //--- Static data
        private readonly Vector2 position;
        private readonly Vector2 size;
        private readonly Color32 color;
        private readonly int lineWidth;

        private readonly GraphType graphType;
        private readonly string variableName;
        private readonly Color32 passiveColor;
        private readonly Color32 activeColor;
        private readonly VariableOrNumber[] scale = new VariableOrNumber[2];

        private bool warned = false;

        private enum GraphType
        {
            VerticalUp,
            HorizontalRight,
        };

        public DataSet(ConfigNode node)
        {
            //JUtil.LogMessage(this, "Initializing...");
            Vector4 packedPosition = ConfigNode.ParseVector4(node.GetValue("borderPosition"));
            position.x = packedPosition.x;
            position.y = packedPosition.y;
            size.x = packedPosition.z;
            size.y = packedPosition.w;

            color = ConfigNode.ParseColor32(node.GetValue("borderColor"));
            lineWidth = int.Parse(node.GetValue("borderWidth"));

            string graphTypeStr = node.GetValue("graphType").Trim();
            if (graphTypeStr == GraphType.VerticalUp.ToString())
            {
                graphType = GraphType.VerticalUp;
            }
            else if(graphTypeStr == GraphType.HorizontalRight.ToString())
            {
                graphType = GraphType.HorizontalRight;
            }
            else
            {
                throw new ArgumentException("Unknown 'graphType' in DATA_SET");
            }

            passiveColor = ConfigNode.ParseColor32(node.GetValue("passiveColor"));
            activeColor = ConfigNode.ParseColor32(node.GetValue("activeColor"));
            string[] token = node.GetValue("scale").Split(',');
            scale[0] = new VariableOrNumber(token[0].Trim(), this);
            scale[1] = new VariableOrNumber(token[1].Trim(), this);
            variableName = node.GetValue("variableName").Trim();
        }

        public void RenderBackground(RenderTexture screen)
        {
            if(lineWidth > 0)
            {
                DrawBorder(screen);
            }
        }

        public void RenderData(RenderTexture screen, RasterPropMonitorComputer comp)
        {
            float leftVal, rightVal;
            if (!scale[0].Get(out leftVal, comp) || !scale[1].Get(out rightVal, comp))
            {
                return; // bad values - can't render
            }

            float eval = comp.ProcessVariable(variableName).MassageToFloat();
            if (float.IsInfinity(eval) || float.IsNaN(eval))
            {
                if (!warned)
                {
                    warned = true;
                    JUtil.LogErrorMessage(this, "Variable {0} can produce bad values", variableName);
                }
                return; // bad value - can't render
            }

            float position = JUtil.DualLerp(0.0f, 1.0f, leftVal, rightVal, eval);

            switch (graphType)
            {
                case GraphType.VerticalUp:
                    DrawVerticalUp(position);
                    break;
                case GraphType.HorizontalRight:
                    DrawHorizontalRight(position);
                    break;
                default:
                    throw new NotImplementedException("Unimplemented graphType " + graphType.ToString());
            }
        }

        private void DrawHorizontalRight(float fillRatio)
        {
            if (fillRatio <= 0.0f)
            {
                return; // early return - empty graph
            }

            Vector2 topLeft = position + new Vector2((float)lineWidth, (float)lineWidth);
            Vector2 fillSize = (size - new Vector2((float)(2 * lineWidth), (float)(2 * lineWidth)));

            Color fillColor = Color.Lerp(passiveColor, activeColor, fillRatio);

            GL.Color(fillColor);
            GL.Begin(GL.QUADS);
            GL.Vertex3(topLeft.x, topLeft.y + fillSize.y, 0.0f);
            GL.Vertex3(topLeft.x + fillSize.x * fillRatio, topLeft.y + fillSize.y, 0.0f);
            GL.Vertex3(topLeft.x + fillSize.x * fillRatio, topLeft.y, 0.0f);
            GL.Vertex3(topLeft.x, topLeft.y, 0.0f);
            GL.End();
        }

        private void DrawVerticalUp(float fillRatio)
        {
            if (fillRatio <= 0.0f)
            {
                return; // early return - empty graph
            }

            Vector2 topLeft = position + new Vector2((float)lineWidth, (float)lineWidth);
            Vector2 fillSize = (size - new Vector2((float)(2 * lineWidth), (float)(2 * lineWidth)));

            Color fillColor = Color.Lerp(passiveColor, activeColor, fillRatio);

            GL.Color(fillColor);
            GL.Begin(GL.QUADS);
            GL.Vertex3(topLeft.x, topLeft.y + fillSize.y, 0.0f);
            GL.Vertex3(topLeft.x + fillSize.x, topLeft.y + fillSize.y, 0.0f);
            GL.Vertex3(topLeft.x + fillSize.x, topLeft.y + fillSize.y * (1.0f - fillRatio), 0.0f);
            GL.Vertex3(topLeft.x, topLeft.y + fillSize.y * (1.0f - fillRatio), 0.0f);
            GL.End();
        }

        private void DrawBorder(RenderTexture screen)
        {
            GL.Color(color);
            GL.Begin(GL.LINES);
            for (int i = 0; i < lineWidth; ++i)
            {
                float offset = (float)i;
                GL.Vertex3(position.x + offset, position.y + offset, 0.0f);
                GL.Vertex3(position.x + offset, position.y + size.y - offset, 0.0f);
                GL.Vertex3(position.x + offset, position.y + size.y - offset, 0.0f);
                GL.Vertex3(position.x + size.x - offset, position.y + size.y - offset, 0.0f);
                GL.Vertex3(position.x + size.x - offset, position.y + size.y - offset, 0.0f);
                GL.Vertex3(position.x + size.x - offset, position.y + offset, 0.0f);
                GL.Vertex3(position.x + size.x - offset, position.y + offset, 0.0f);
                GL.Vertex3(position.x + offset, position.y + offset, 0.0f);
            }
            GL.End();
        }
    }
}
