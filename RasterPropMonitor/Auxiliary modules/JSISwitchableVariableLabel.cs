using UnityEngine;
using System;
using System.Collections.Generic;

namespace JSI
{
	public class JSISwitchableVariableLabel:InternalModule
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
		private readonly List<VariableLabelSet> labelsEx = new List<VariableLabelSet>();
		private int activeLabel;
		private const string fontName = "Arial";
		private InternalText textObj;
		private Transform textObjTransform;
		private RasterPropMonitorComputer comp;
		private int updateCountdown;
		private Renderer colorShiftRenderer;
		private FXGroup audioOutput;

		public void Start()
		{
			try {
				comp = RasterPropMonitorComputer.Instantiate(internalProp);
				textObjTransform = internalProp.FindModelTransform(labelTransform);
				textObj = InternalComponents.Instance.CreateText(fontName, fontSize, textObjTransform, string.Empty);
				activeLabel = 0;

				SmarterButton.CreateButton(internalProp, switchTransform, Click);

				ConfigNode moduleConfig = null;
				foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("PROP")) {
					if (node.GetValue("name") == internalProp.propName) {

						moduleConfig = node.GetNodes("MODULE")[moduleID];
						ConfigNode[] variableNodes = moduleConfig.GetNodes("VARIABLESET");

						for (int i = 0; i < variableNodes.Length; i++) {
							try {
								labelsEx.Add(new VariableLabelSet(variableNodes[i]));
							} catch (ArgumentException e) {
								JUtil.LogMessage(this, "Error in building prop number {1} - {0}", e.Message, internalProp.propID);
							}
						}
						break;
					}
				}

				// Fallback: If there are no VARIABLESET blocks, we treat the module configuration itself as a variableset block.
				if (labelsEx.Count < 1 && moduleConfig != null) {
					try {
						labelsEx.Add(new VariableLabelSet(moduleConfig));
					} catch (ArgumentException e) {
						JUtil.LogMessage(this, "Error in building prop number {1} - {0}", e.Message, internalProp.propID);
					}
				}

				if (labelsEx.Count == 0) {
					JUtil.LogMessage(this, "No labels defined.");
					throw new ArgumentException("No labels defined");
				}

				colorShiftRenderer = internalProp.FindModelComponent<Renderer>(coloredObject);
				if (labelsEx[activeLabel].hasColor) {
					colorShiftRenderer.material.SetColor(colorName, labelsEx[activeLabel].color);
				}
				if (labelsEx[activeLabel].hasText) {
					textObj.text.Text = StringProcessor.ProcessString(labelsEx[activeLabel].labelText, comp);
				}

				audioOutput = JUtil.SetupIVASound(internalProp, switchSound, switchSoundVolume, false);
				JUtil.LogMessage(this, "Configuration complete in prop {1}, supporting {0} variable indicators.", labelsEx.Count, internalProp.propID);
			} catch {
				JUtil.AnnoyUser(this);
				enabled = false;
				throw;
			}
		}

		private bool UpdateCheck()
		{
			if (labelsEx[activeLabel].oneShot) {
				return false;
			}

			if (updateCountdown <= 0) {
				updateCountdown = refreshRate;
				return true;
			}
			updateCountdown--;
			return false;
		}

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
				return;

			textObj.text.Text = StringProcessor.ProcessString(labelsEx[activeLabel].labelText, comp);
		}

		public void Click()
		{
			activeLabel++;

			if (activeLabel == labelsEx.Count) {
				activeLabel = 0;
			}

			if (labelsEx[activeLabel].hasColor) {
				colorShiftRenderer.material.SetColor(colorName, labelsEx[activeLabel].color);
			}

			if (labelsEx[activeLabel].hasText && labelsEx[activeLabel].oneShot) {
				textObj.text.Text = StringProcessor.ProcessString(labelsEx[activeLabel].labelText, comp);
			}

			// Force an update.
			updateCountdown = 0;

			if (audioOutput != null && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal)) {
				audioOutput.audio.Play();
			}
		}
	}

	public class VariableLabelSet
	{
		public readonly string labelText;
		public readonly bool hasText;
		public readonly bool oneShot;
		public readonly Color color;
		public readonly bool hasColor;

		public VariableLabelSet(ConfigNode node)
		{
			if (node.HasValue("labelText")) {
				labelText = node.GetValue("labelText").Trim().UnMangleConfigText();
				hasText = true;
				oneShot = !labelText.Contains("$&$");
			} else {
				hasText = false;
				oneShot = true;
			}

			if (node.HasValue("color")) {
				color = ConfigNode.ParseColor32(node.GetValue("color").Trim());
				hasColor = true;
			} else {
				hasColor = false;
			}

		}
	}
}
