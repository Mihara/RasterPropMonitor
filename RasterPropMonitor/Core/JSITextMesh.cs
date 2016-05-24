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
    /// <summary>
    /// JSITextMesh is designed as a drop-in replacement for Unity's TextMesh
    /// with two key differences:
    /// 1) the Material and Mesh are both directly visible from the class, and
    /// 2) the generated mesh includes normals and tangents, making this class
    /// suitable for in-scene lighting.
    /// </summary>
    public class JSITextMesh : MonoBehaviour
    {
        private TextAlignment alignment_;
        public TextAlignment alignment
        {
            get
            {
                return alignment_;
            }
            set
            {
                if (value != alignment_)
                {
                    invalidated = true;
                    alignment_ = value;
                }
            }
        }

        private TextAnchor anchor_;
        public TextAnchor anchor
        {
            get
            {
                return anchor_;
            }
            set
            {
                if (value != anchor_)
                {
                    invalidated = true;
                    anchor_ = value;
                }
            }
        }

        private float characterSize_ = 1.0f;
        public float characterSize
        {
            get
            {
                return characterSize_;
            }
            set
            {
                if (value != characterSize_)
                {
                    invalidated = true;
                    characterSize_ = value;
                }
            }
        }

        private Color32 color_;
        public Color32 color
        {
            get
            {
                return color_;
            }
            set
            {
                if (value.r != color_.r || value.g != color_.g || value.b != color_.b || value.a != color_.a)
                {
                    invalidatedColor = true;
                    color_ = value;
                }
            }
        }

        private Font font_;
        public Font font
        {
            get
            {
                return font_;
            }
            set
            {
                if (value != font_)
                {
                    invalidated = true;
                    font_ = value;
                }
            }
        }

        private int fontSize_;
        public int fontSize
        {
            get
            {
                return fontSize_;
            }
            set
            {
                if (value != fontSize_)
                {
                    invalidated = true;
                    fontSize_ = value;
                }
            }
        }

        private FontStyle fontStyle_;
        public FontStyle fontStyle
        {
            get
            {
                return fontStyle_;
            }
            set
            {
                if (value != fontStyle_)
                {
                    invalidated = true;
                    fontStyle_ = value;
                }
            }
        }

        private float lineSpacing_;
        public float lineSpacing
        {
            get
            {
                return lineSpacing_;
            }
            set
            {
                if (value != lineSpacing_)
                {
                    invalidated = true;
                    lineSpacing_ = value;
                }
            }
        }

        private MeshRenderer meshRenderer_;
        private MeshFilter meshFilter_;
        public Material material
        {
            get
            {
                CreateComponents();
                return meshRenderer_.material;
            }
            set
            {
                invalidated = true;
                meshRenderer_.material = value;
            }
        }

        //private Mesh mesh_;
        public Mesh mesh
        {
            get
            {
                CreateComponents();
                return meshFilter_.mesh;
            }
        }

        private bool richText_;
        public bool richText
        {
            get
            {
                return richText_;
            }
            set
            {
                if (value != richText_)
                {
                    invalidated = true;
                    richText_ = value;
                }
            }
        }

        private string text_;
        public string text
        {
            get
            {
                return text_;
            }
            set
            {
                if (value != text_)
                {
                    invalidated = true;
                    text_ = value;
                }
            }
        }

        //public float offsetZ { get; set; }
        //public float tabSize { get; set; }

        private bool invalidated = false;
        private bool invalidatedColor = false;

        private void CreateComponents()
        {
            if (meshRenderer_ == null)
            {
                meshFilter_ = gameObject.AddComponent<MeshFilter>();
                meshRenderer_ = gameObject.AddComponent<MeshRenderer>();
                meshRenderer_.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer_.receiveShadows = true; // not working?
                meshRenderer_.material = new Material(JUtil.LoadInternalShader("RPM/JSILabel"));
            }
        }

        public void Start()
        {
            CreateComponents();
        }

        public void Update()
        {
            if (invalidated)
            {
                //JUtil.LogMessage(this, "Update() - invalidated = {0}", invalidated);
                if (richText_)
                {
                    GenerateRichText();
                }
                else
                {
                    GenerateText();
                }
                invalidated = false;
                invalidatedColor = false;
            }
            else if (invalidatedColor)
            {
                //JUtil.LogMessage(this, "Update() - invalidatedColor = {0}", invalidatedColor);
                if (meshFilter_.mesh.colors32.Length > 0)
                {
                    Color32[] newColor = new Color32[meshFilter_.mesh.colors32.Length];
                    for (int idx = 0; idx < newColor.Length; ++idx)
                    {
                        newColor[idx] = color_;
                    }
                    meshFilter_.mesh.colors32 = newColor;
                    meshFilter_.mesh.UploadMeshData(false);
                }
                invalidatedColor = false;
            }
        }

        private void GenerateRichText()
        {
            if (font_ == null)
            {
                JUtil.LogErrorMessage(this, "Font was not initialized");
                return;
            }

            if (string.IsNullOrEmpty(text_))
            {
                meshRenderer_.enabled = false;
                return;
            }

            meshRenderer_.enabled = true;

            GenerateText();
        }

        private void GenerateText()
        {
            if (font_ == null)
            {
                JUtil.LogErrorMessage(this, "Font was not initialized");
                return;
            }

            if (string.IsNullOrEmpty(text_))
            {
                meshRenderer_.gameObject.SetActive(false);
                return;
            }

            // Break the text into lines
            string[] textLines = text_.Split(JUtil.LineSeparator, StringSplitOptions.None);

            // Determine text length
            int[] textLength = new int[textLines.Length];
            int maxTextLength = 0;
            int maxVerts = 0;
            for (int line = 0; line < textLines.Length; ++line)
            {
                textLength[line] = 0;
                font_.RequestCharactersInTexture(textLines[line]);
                maxVerts += Font.GetMaxVertsForString(textLines[line]);

                for (int ch = 0; ch < textLines[line].Length; ++ch)
                {
                    CharacterInfo charInfo;
                    if (font_.GetCharacterInfo(textLines[line][ch], out charInfo))
                    {
                        textLength[line] += charInfo.advance;
                    }
                }
                if (textLength[line] > maxTextLength)
                {
                    maxTextLength = textLength[line];
                }
            }

            if (maxVerts == 0)
            {
                meshRenderer_.gameObject.SetActive(false);
                return;
            }

            meshRenderer_.gameObject.SetActive(true);

            Vector3[] vertices = new Vector3[maxVerts];
            Color32[] colors32 = new Color32[maxVerts];
            Vector4[] tangents = new Vector4[maxVerts];
            Vector2[] uv = new Vector2[maxVerts];

            int[] triangles = new int[maxVerts + maxVerts / 2];
            for (int idx = 0; idx < triangles.Length; ++idx)
            {
                triangles.SetValue(0, idx);
            }

            int charWritten = 0;
            int arrayIndex = 0;
            int yPos = 0;
            int xAnchor = 0;
            switch (anchor_)
            {
                case TextAnchor.LowerCenter:
                    yPos = font_.lineHeight * textLines.Length - font_.ascent;
                    break;
                case TextAnchor.LowerLeft:
                    //xAnchor = 0;
                    yPos = font_.lineHeight * textLines.Length - font_.ascent;
                    break;
                case TextAnchor.LowerRight:
                    yPos = font_.lineHeight * textLines.Length - font_.ascent;
                    break;
                case TextAnchor.MiddleCenter:
                    yPos = (font_.lineHeight * textLines.Length) / 2 - font_.ascent;
                    break;
                case TextAnchor.MiddleLeft:
                    //xAnchor = 0;
                    yPos = (font_.lineHeight * textLines.Length) / 2 - font_.ascent;
                    break;
                case TextAnchor.MiddleRight:
                    yPos = (font_.lineHeight * textLines.Length) / 2 - font_.ascent;
                    break;
                case TextAnchor.UpperCenter:
                    yPos = -font_.ascent;
                    break;
                case TextAnchor.UpperLeft:
                    //xAnchor = 0;
                    yPos = -font_.ascent;
                    break;
                case TextAnchor.UpperRight:
                    yPos = -font_.ascent;
                    break;
            }

            for (int line = 0; line < textLines.Length; ++line)
            {
                int xPos = 0;
                if (alignment_ == TextAlignment.Center)
                {
                    xPos = -(textLength[line]) / 2;
                }
                else if (alignment_ == TextAlignment.Right)
                {
                    xPos = -textLength[line];
                }
                xPos += xAnchor;

                for (int ch = 0; ch < textLines[line].Length; ++ch)
                {
                    CharacterInfo charInfo;
                    if (font_.GetCharacterInfo(textLines[line][ch], out charInfo))
                    {
                        triangles[charWritten * 6 + 0] = arrayIndex + 0;
                        triangles[charWritten * 6 + 1] = arrayIndex + 3;
                        triangles[charWritten * 6 + 2] = arrayIndex + 2;
                        triangles[charWritten * 6 + 3] = arrayIndex + 0;
                        triangles[charWritten * 6 + 4] = arrayIndex + 1;
                        triangles[charWritten * 6 + 5] = arrayIndex + 3;

                        vertices[arrayIndex] = new Vector3(characterSize_ * (float)(xPos + charInfo.minX), characterSize_ * (float)(yPos + charInfo.maxY), 0.0f);
                        colors32[arrayIndex] = color_;
                        tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                        uv[arrayIndex] = charInfo.uvTopLeft;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterSize_ * (float)(xPos + charInfo.maxX), characterSize_ * (float)(yPos + charInfo.maxY), 0.0f);
                        colors32[arrayIndex] = color_;
                        tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                        uv[arrayIndex] = charInfo.uvTopRight;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterSize_ * (float)(xPos + charInfo.minX), characterSize_ * (float)(yPos + charInfo.minY), 0.0f);
                        colors32[arrayIndex] = color_;
                        tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                        uv[arrayIndex] = charInfo.uvBottomLeft;

                        ++arrayIndex;

                        vertices[arrayIndex] = new Vector3(characterSize_ * (float)(xPos + charInfo.maxX), characterSize_ * (float)(yPos + charInfo.minY), 0.0f);
                        colors32[arrayIndex] = color_;
                        tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                        uv[arrayIndex] = charInfo.uvBottomRight;

                        ++arrayIndex;

                        xPos += charInfo.advance;
                        ++charWritten;
                    }
                }

                yPos -= font_.lineHeight;
            }

            meshFilter_.mesh.vertices = vertices;
            meshFilter_.mesh.colors32 = colors32;
            meshFilter_.mesh.tangents = tangents;
            meshFilter_.mesh.uv = uv;
            meshFilter_.mesh.triangles = triangles;
            meshFilter_.mesh.RecalculateNormals();
            meshFilter_.mesh.Optimize();
            // Can't hide mesh with (true), or we can't edit colors later.
            meshFilter_.mesh.UploadMeshData(false);
        }
    }
}
