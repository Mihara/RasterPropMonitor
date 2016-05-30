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

        private int fontSize_ = 32;
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

        private float lineSpacing_ = 1.0f;
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
                CreateComponents();
                meshRenderer_.material = value;
            }
        }

        public Mesh mesh
        {
            get
            {
                CreateComponents();
                return meshFilter_.mesh;
            }
        }

        private string text_;
        private bool richText = false;
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

                    if (meshRenderer_ != null)
                    {
                        if (string.IsNullOrEmpty(text_))
                        {
                            meshRenderer_.gameObject.SetActive(false);
                        }
                        else
                        {
                            meshRenderer_.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        private bool invalidated = false;
        private bool invalidatedColor = false;
        private bool fontNag = false;

        /// <summary>
        /// Set up rendering components.
        /// </summary>
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

        /// <summary>
        /// Set up the JSITextMesh components if they haven't been set up yet.
        /// </summary>
        public void Start()
        {
            CreateComponents();
        }

        /// <summary>
        /// Update the text mesh if it's changed.
        /// </summary>
        public void Update()
        {
            if (!string.IsNullOrEmpty(text_))
            {
                if (invalidated)
                {
                    if (font_ == null)
                    {
                        if (!fontNag)
                        {
                            JUtil.LogErrorMessage(this, "Font was not initialized");
                            JUtil.AnnoyUser(this);
                            fontNag = true;
                        }
                        return;
                    }

                    if (text_.Contains("["))
                    {
                        richText = true;
                        GenerateRichText();
                    }
                    else
                    {
                        richText = false;
                        GenerateText();
                    }

                    invalidated = false;
                    invalidatedColor = false;
                }
                else if (invalidatedColor)
                {
                    if (richText)
                    {
                        GenerateRichText();
                    }
                    else
                    {
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
                    }

                    invalidatedColor = false;
                }
            }
        }

        /// <summary>
        /// Convert a text using control sequences ([b], [i], [#rrggbb(aa)], [size]).
        /// </summary>
        private void GenerateRichText()
        {
            // Break the text into lines
            string[] textLines = text_.Split(JUtil.LineSeparator, StringSplitOptions.None);

            // State tracking
            bool bold = false;
            bool italic = false;
            //size = something.

            // Determine text length
            int[] textLength = new int[textLines.Length];
            int maxTextLength = 0;
            int maxVerts = 0;
            for (int line = 0; line < textLines.Length; ++line)
            {
                textLength[line] = 0;

                for (int charIndex = 0; charIndex < textLines[line].Length; charIndex++)
                {
                    bool escapedBracket = false;
                    // We will continue parsing bracket pairs until we're out of bracket pairs,
                    // since all of them -- except the escaped bracket tag --
                    // consume characters and change state without actually generating any output.
                    while (charIndex < textLines[line].Length && textLines[line][charIndex] == '[')
                    {
                        // If there's no closing bracket, we stop parsing and go on to printing.
                        int nextBracket = textLines[line].IndexOf(']', charIndex) - charIndex;
                        if (nextBracket < 1)
                            break;
                        // Much easier to parse it this way, although I suppose more expensive.
                        string tagText = textLines[line].Substring(charIndex + 1, nextBracket - 1).Trim();
                        if ((tagText.Length == 9 || tagText.Length == 7) && tagText[0] == '#')
                        {
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "[")
                        {
                            // We got a "[[]" which means an escaped opening bracket.
                            escapedBracket = true;
                            charIndex += nextBracket;
                            break;
                        }
                        else if (tagText == "b")
                        {
                            bold = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "i")
                        {
                            italic = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/b")
                        {
                            bold = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/i")
                        {
                            italic = false;
                            charIndex += nextBracket + 1;
                        }
                        else // Else we didn't recognise anything so it's not a tag.
                        {
                            break;
                        }
                    }

                    if (charIndex < textLines[line].Length)
                    {
                        FontStyle style = GetFontStyle(bold, italic);
                        font_.RequestCharactersInTexture(escapedBracket ? "[" : textLines[line][charIndex].ToString(), fontSize_, style);
                        CharacterInfo charInfo;
                        if (font_.GetCharacterInfo(textLines[line][charIndex], out charInfo, 0, style))
                        {
                            textLength[line] += charInfo.advance;
                            maxVerts += 4;
                        }
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
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) - font_.ascent;
                    break;
                case TextAnchor.LowerLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) - font_.ascent;
                    break;
                case TextAnchor.LowerRight:
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) - font_.ascent;
                    break;
                case TextAnchor.MiddleCenter:
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) / 2 - font_.ascent;
                    break;
                case TextAnchor.MiddleLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) / 2 - font_.ascent;
                    break;
                case TextAnchor.MiddleRight:
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) / 2 - font_.ascent;
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

            int lineAdvance = (int)(lineSpacing_ * font_.lineHeight);
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

                Color32 fontColor = color_;

                for (int charIndex = 0; charIndex < textLines[line].Length; charIndex++)
                {
                    bool escapedBracket = false;
                    // We will continue parsing bracket pairs until we're out of bracket pairs,
                    // since all of them -- except the escaped bracket tag --
                    // consume characters and change state without actually generating any output.
                    while (charIndex < textLines[line].Length && textLines[line][charIndex] == '[')
                    {
                        // If there's no closing bracket, we stop parsing and go on to printing.
                        int nextBracket = textLines[line].IndexOf(']', charIndex) - charIndex;
                        if (nextBracket < 1)
                            break;
                        // Much easier to parse it this way, although I suppose more expensive.
                        string tagText = textLines[line].Substring(charIndex + 1, nextBracket - 1).Trim();
                        if ((tagText.Length == 9 || tagText.Length == 7) && tagText[0] == '#')
                        {
                            // Valid color tags are [#rrggbbaa] or [#rrggbb].
                            fontColor = XKCDColors.ColorTranslator.FromHtml(tagText);
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "[")
                        {
                            // We got a "[[]" which means an escaped opening bracket.
                            escapedBracket = true;
                            charIndex += nextBracket;
                            break;
                        }
                        else if (tagText == "b")
                        {
                            bold = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "i")
                        {
                            italic = true;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/b")
                        {
                            bold = false;
                            charIndex += nextBracket + 1;
                        }
                        else if (tagText == "/i")
                        {
                            italic = false;
                            charIndex += nextBracket + 1;
                        }
                        else // Else we didn't recognise anything so it's not a tag.
                        {
                            break;
                        }
                    }

                    if (charIndex < textLines[line].Length)
                    {
                        FontStyle style = GetFontStyle(bold, italic);
                        CharacterInfo charInfo;
                        if (font_.GetCharacterInfo(escapedBracket ? '[' : textLines[line][charIndex], out charInfo, 0, style))
                        {
                            if (charInfo.minX != charInfo.maxX && charInfo.minY != charInfo.maxY)
                            {
                                triangles[charWritten * 6 + 0] = arrayIndex + 0;
                                triangles[charWritten * 6 + 1] = arrayIndex + 3;
                                triangles[charWritten * 6 + 2] = arrayIndex + 2;
                                triangles[charWritten * 6 + 3] = arrayIndex + 0;
                                triangles[charWritten * 6 + 4] = arrayIndex + 1;
                                triangles[charWritten * 6 + 5] = arrayIndex + 3;

                                vertices[arrayIndex] = new Vector3(characterSize_ * (float)(xPos + charInfo.minX), characterSize_ * (float)(yPos + charInfo.maxY), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvTopLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize_ * (float)(xPos + charInfo.maxX), characterSize_ * (float)(yPos + charInfo.maxY), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvTopRight;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize_ * (float)(xPos + charInfo.minX), characterSize_ * (float)(yPos + charInfo.minY), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvBottomLeft;

                                ++arrayIndex;

                                vertices[arrayIndex] = new Vector3(characterSize_ * (float)(xPos + charInfo.maxX), characterSize_ * (float)(yPos + charInfo.minY), 0.0f);
                                colors32[arrayIndex] = fontColor;
                                tangents[arrayIndex] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                                uv[arrayIndex] = charInfo.uvBottomRight;

                                ++arrayIndex;
                                ++charWritten;
                            }
                            xPos += charInfo.advance;
                        }
                    }
                }

                yPos -= lineAdvance;
            }

            meshFilter_.mesh.Clear();
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

        /// <summary>
        /// Convert a simple text string into displayable quads with no
        /// additional processing (untagged text).
        /// </summary>
        private void GenerateText()
        {
            // Break the text into lines
            string[] textLines = text_.Split(JUtil.LineSeparator, StringSplitOptions.None);

            // Determine text length
            int[] textLength = new int[textLines.Length];
            int maxTextLength = 0;
            int maxVerts = 0;
            for (int line = 0; line < textLines.Length; ++line)
            {
                textLength[line] = 0;
                font_.RequestCharactersInTexture(textLines[line], fontSize_);
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
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) - font_.ascent;
                    break;
                case TextAnchor.LowerLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) - font_.ascent;
                    break;
                case TextAnchor.LowerRight:
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) - font_.ascent;
                    break;
                case TextAnchor.MiddleCenter:
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) / 2 - font_.ascent;
                    break;
                case TextAnchor.MiddleLeft:
                    //xAnchor = 0;
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) / 2 - font_.ascent;
                    break;
                case TextAnchor.MiddleRight:
                    yPos = (int)(lineSpacing_ * font_.lineHeight * textLines.Length) / 2 - font_.ascent;
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

            int lineAdvance = (int)(lineSpacing_ * font_.lineHeight);
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

                yPos -= lineAdvance;
            }

            meshFilter_.mesh.Clear();
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

        /// <summary>
        /// Convert the booleans for bold and italic text into a FontStyle.
        /// </summary>
        /// <param name="bold">Is the style bold?</param>
        /// <param name="italic">Is the style italic?</param>
        /// <returns></returns>
        public static FontStyle GetFontStyle(bool bold, bool italic)
        {
            if (bold)
            {
                return (italic) ? FontStyle.BoldAndItalic : FontStyle.Bold;
            }
            else if (italic)
            {
                return FontStyle.Italic;
            }
            else
            {
                return FontStyle.Normal;
            }
        }
    }
}
