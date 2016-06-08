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
    public class JSISwitchableVariableLabel : InternalModule
    {
        [KSPField]
        public string labelTransform = string.Empty;
        [KSPField]
        public float fontSize = 0.008f;
        [KSPField]
        public int refreshRate = 10;
        [KSPField]
        public string switchTransform = string.Empty;
        [KSPField]
        public string switchSound = "Squad/Sounds/sound_click_flick";
        [KSPField]
        public float switchSoundVolume = 0.5f;
        [KSPField]
        public string coloredObject = string.Empty;
        [KSPField]
        public string colorName = "_EmissiveColor";
        private int colorNameId = -1;
        private readonly List<VariableLabelSet> labelsEx = new List<VariableLabelSet>();
        private int activeLabel;
        private const string fontName = "Arial";
        private InternalText textObj;
        private Transform textObjTransform;
        private int updateCountdown;
        private Renderer colorShiftRenderer;
        private FXGroup audioOutput;
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

                textObjTransform = internalProp.FindModelTransform(labelTransform);
                textObj = InternalComponents.Instance.CreateText(fontName, fontSize, textObjTransform, string.Empty);
                activeLabel = 0;

                SmarterButton.CreateButton(internalProp, switchTransform, Click);

                ConfigNode moduleConfig = null;
                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("PROP"))
                {
                    if (node.GetValue("name") == internalProp.propName)
                    {

                        moduleConfig = node.GetNodes("MODULE")[moduleID];
                        ConfigNode[] variableNodes = moduleConfig.GetNodes("VARIABLESET");

                        for (int i = 0; i < variableNodes.Length; i++)
                        {
                            try
                            {
                                labelsEx.Add(new VariableLabelSet(variableNodes[i], part));
                            }
                            catch (ArgumentException e)
                            {
                                JUtil.LogErrorMessage(this, "Error in building prop number {1} - {0}", e.Message, internalProp.propID);
                            }
                        }
                        break;
                    }
                }

                // Fallback: If there are no VARIABLESET blocks, we treat the module configuration itself as a variableset block.
                if (labelsEx.Count < 1 && moduleConfig != null)
                {
                    try
                    {
                        labelsEx.Add(new VariableLabelSet(moduleConfig, part));
                    }
                    catch (ArgumentException e)
                    {
                        JUtil.LogErrorMessage(this, "Error in building prop number {1} - {0}", e.Message, internalProp.propID);
                    }
                }

                if (labelsEx.Count == 0)
                {
                    JUtil.LogErrorMessage(this, "No labels defined.");
                    throw new ArgumentException("No labels defined");
                }

                colorShiftRenderer = internalProp.FindModelComponent<Renderer>(coloredObject);
                if (labelsEx[activeLabel].hasColor)
                {
                    colorNameId = Shader.PropertyToID(colorName);
                    colorShiftRenderer.material.SetColor(colorNameId, labelsEx[activeLabel].color);
                }
                if (labelsEx[activeLabel].hasText)
                {
                    if (labelsEx[activeLabel].oneShot)
                    {
                        // Fetching formatString directly is notionally bad
                        // because there may be formatting stuff, but if
                        // oneShot is true, we already know that this is a
                        // constant string with no formatting.
                        textObj.text.Text = labelsEx[activeLabel].label.formatString;
                    }
                    else
                    {
                        textObj.text.Text = "";
                    }
                }

                audioOutput = JUtil.SetupIVASound(internalProp, switchSound, switchSoundVolume, false);
                JUtil.LogMessage(this, "Configuration complete in prop {1}, supporting {0} variable indicators.", labelsEx.Count, internalProp.propID);
            }
            catch
            {
                JUtil.AnnoyUser(this);
                enabled = false;
                throw;
            }
        }

        public void OnDestroy()
        {
            //JUtil.LogMessage(this, "OnDestroy()");
        }

        private bool UpdateCheck()
        {
            // Saw an out-of-range exception in the next if clause once as a
            // side effect of docking.  Not sure if OnUpdate was called after
            // onDestroy, or before Start.
            if (activeLabel > labelsEx.Count)
            {
                activeLabel = labelsEx.Count - 1;
                if(activeLabel < 0)
                {
                    return false;
                }
            }

            if (labelsEx[activeLabel].oneShot)
            {
                return false;
            }

            if (updateCountdown <= 0)
            {
                updateCountdown = refreshRate;
                return true;
            }
            updateCountdown--;
            return false;
        }

        public override void OnUpdate()
        {
            if (JUtil.RasterPropMonitorShouldUpdate(vessel) && UpdateCheck())
            {
                textObj.text.Text = StringProcessor.ProcessString(labelsEx[activeLabel].label, rpmComp);
            }
        }

        public void Click()
        {
            activeLabel++;

            if (activeLabel == labelsEx.Count)
            {
                activeLabel = 0;
            }

            if (labelsEx[activeLabel].hasColor)
            {
                colorShiftRenderer.material.SetColor(colorNameId, labelsEx[activeLabel].color);
            }

            if (labelsEx[activeLabel].hasText)
            {
                textObj.text.Text = StringProcessor.ProcessString(labelsEx[activeLabel].label, rpmComp);
            }

            // Force an update.
            updateCountdown = 0;

            if (audioOutput != null && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
                CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
            {
                audioOutput.audio.Play();
            }
        }
    }

    public class VariableLabelSet
    {
        public readonly StringProcessorFormatter label;
        public readonly bool hasText;
        public readonly bool oneShot;
        public readonly Color color;
        public readonly bool hasColor;

        public VariableLabelSet(ConfigNode node, Part part)
        {
            RasterPropMonitorComputer rpmComp = null;
            if (node.HasValue("labelText"))
            {
                string labelText = node.GetValue("labelText").Trim().UnMangleConfigText();
                hasText = true;
                oneShot = !labelText.Contains("$&$");
                rpmComp = RasterPropMonitorComputer.Instantiate(part, true);
                label = new StringProcessorFormatter(labelText, rpmComp);
            }
            else
            {
                hasText = false;
                oneShot = true;
            }

            if (node.HasValue("color"))
            {
                color = JUtil.ParseColor32(node.GetValue("color").Trim(), part, ref rpmComp);
                hasColor = true;
            }
            else
            {
                hasColor = false;
            }
        }
    }
}
