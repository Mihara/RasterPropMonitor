using UnityEngine;
using System;
using System.Collections.Generic;

namespace JSI
{
	public class JSIVariableAnimator: InternalModule
	{
		[KSPField]
		public int refreshRate = 10;
		private bool startupComplete;
		private int updateCountdown;
		private readonly List<VariableAnimationSet> variableSets = new List<VariableAnimationSet>();

		private bool UpdateCheck()
		{
			if (updateCountdown <= 0) {
				updateCountdown = refreshRate;
				return true;
			}
			updateCountdown--;
			return false;
		}

		public void Start()
		{
			ConfigNode moduleConfig = null;
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("PROP")) {
				if (node.GetValue("name") == internalProp.propName) {

					moduleConfig = node.GetNodes("MODULE")[moduleID];
					ConfigNode[] variableNodes = moduleConfig.GetNodes("VARIABLESET");

					for (int i = 0; i < variableNodes.Length; i++) {
						try {
							variableSets.Add(new VariableAnimationSet(variableNodes[i], internalProp));
						} catch (ArgumentException e) {
							JUtil.LogMessage(this, "Error - {0}", e);
						}
					}
					break;
				}
			}

			// Fallback: If there are no VARIABLESET blocks, we treat the module configuration itself as a variableset block.
			if (variableSets.Count < 1 && moduleConfig != null)
				variableSets.Add(new VariableAnimationSet(moduleConfig, internalProp)); 

			JUtil.LogMessage(this, "Configuration complete, supporting {0} variable indicators.", variableSets.Count);
			startupComplete = true;
		}

		public override void OnUpdate()
		{
			if (!JUtil.VesselIsInIVA(vessel) || !UpdateCheck())
				return;

			foreach (VariableAnimationSet unit in variableSets) {
				unit.Update();
			}
		}

		public void LateUpdate()
		{
			if (JUtil.VesselIsInIVA(vessel) && !startupComplete)
				JUtil.AnnoyUser(this);
		}
	}

	public class VariableAnimationSet
	{
		private readonly VariableOrNumber[] scaleEnds = new VariableOrNumber[3];
		private readonly RasterPropMonitorComputer comp;
		private readonly Animation anim;
		private readonly bool thresholdMode;
		private readonly FXGroup audioOutput;
		private readonly Vector2 threshold = Vector2.zero;
		private readonly bool reverse;
		private readonly string animationName;
		private readonly bool alarmSoundLooping;
		private readonly Color passiveColor, activeColor;
		private readonly Renderer colorShiftRenderer;
		private readonly Transform controlledTransform;
		private readonly Vector3 initialPosition, vectorStart, vectorEnd;
		private readonly Quaternion initialRotation, rotationStart, rotationEnd;
		private readonly bool longPath;
		private readonly double flashingDelay;
		private readonly string colorName = "_EmissiveColor";
		private readonly Mode mode;
		// runtime values:
		private bool alarmActive;
		private bool currentState;
		private double lastStateChange;

		private enum Mode
		{
			Animation,
			Color,
			Rotation,
			Translation
		}

		public VariableAnimationSet(ConfigNode node, InternalProp thisProp)
		{
			if (!node.HasData)
				throw new ArgumentException("No data?!");

			comp = RasterPropMonitorComputer.Instantiate(thisProp);

			string[] tokens = { };

			if (node.HasValue("scale"))
				tokens = node.GetValue("scale").Split(',');

			if (tokens.Length != 2)
				throw new ArgumentException("Could not parse 'scale' parameter.");

			string variableName;
			if (node.HasValue("variableName"))
				variableName = node.GetValue("variableName").Trim();
			else
				throw new ArgumentException("Missing variable name.");

			scaleEnds[0] = new VariableOrNumber(tokens[0], comp, this);
			scaleEnds[1] = new VariableOrNumber(tokens[1], comp, this);
			scaleEnds[2] = new VariableOrNumber(variableName, comp, this);

			// That takes care of the scale, now what to do about that scale:
				
			if (node.HasValue("reverse")) {
				if (!bool.TryParse(node.GetValue("reverse"), out reverse))
					throw new ArgumentException("So is 'reverse' true or false?");
			}

			if (node.HasValue("animationName")) {
				animationName = node.GetValue("animationName");
				anim = thisProp.FindModelAnimators(animationName)[0];
				anim.enabled = true;
				anim[animationName].speed = 0;
				anim[animationName].normalizedTime = reverse ? 1f : 0f;
				anim.Play();
				JUtil.LogMessage(this, "Using animation mode with animation {0}.", animationName);
				mode = Mode.Animation;
			} else if (node.HasValue("activeColor") && node.HasValue("passiveColor") && node.HasValue("coloredObject")) {
				if (node.HasValue("colorName"))
					colorName = node.GetValue("colorName");
				passiveColor = ConfigNode.ParseColor32(node.GetValue("passiveColor"));
				activeColor = ConfigNode.ParseColor32(node.GetValue("activeColor"));
				colorShiftRenderer = thisProp.FindModelComponent<Renderer>(node.GetValue("coloredObject"));
				colorShiftRenderer.material.SetColor(colorName, reverse ? activeColor : passiveColor);
				JUtil.LogMessage(this, "Using color shift mode with object {0}.", node.GetValue("coloredObject"));
				mode = Mode.Color;
			} else if (node.HasValue("controlledTransform") && node.HasValue("localRotationStart") && node.HasValue("localRotationEnd")) {
				controlledTransform = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim());
				initialRotation = controlledTransform.localRotation;
				if (node.HasValue("longPath")) {
					longPath = true;
					vectorStart = ConfigNode.ParseVector3(node.GetValue("localRotationStart"));
					vectorEnd = ConfigNode.ParseVector3(node.GetValue("localRotationEnd"));
				} else {
					rotationStart = Quaternion.Euler(ConfigNode.ParseVector3(node.GetValue("localRotationStart")));
					rotationEnd = Quaternion.Euler(ConfigNode.ParseVector3(node.GetValue("localRotationEnd")));
				}
				JUtil.LogMessage(this, "Using rotation mode with object {0}.", node.GetValue("controlledTransform"));
				mode = Mode.Rotation;
			} else if (node.HasValue("controlledTransform") && node.HasValue("localTranslationStart") && node.HasValue("localTranslationEnd")) {
				controlledTransform = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim());
				initialPosition = controlledTransform.localPosition;
				vectorStart = ConfigNode.ParseVector3(node.GetValue("localTranslationStart"));
				vectorEnd = ConfigNode.ParseVector3(node.GetValue("localTranslationEnd"));
				JUtil.LogMessage(this, "Using translation mode with object {0}.", node.GetValue("controlledTransform"));
				mode = Mode.Translation;
			} else
				throw new ArgumentException("Cannot initiate any of the possible action modes.");

			if (node.HasValue("threshold"))
				threshold = ConfigNode.ParseVector2(node.GetValue("threshold"));

			if (threshold != Vector2.zero) {
				thresholdMode = true;

				float min = Mathf.Min(threshold.x, threshold.y);
				float max = Mathf.Max(threshold.x, threshold.y);
				threshold.x = min;
				threshold.y = max;

				if (node.HasValue("flashingDelay")) {
					flashingDelay = double.Parse(node.GetValue("flashingDelay"));
				}

				if (node.HasValue("alarmSound")) {
					float alarmSoundVolume = 0.5f;
					if (node.HasValue("alarmSoundVolume"))
						alarmSoundVolume = float.Parse(node.GetValue("alarmSoundVolume"));
					audioOutput = JUtil.SetupIVASound(thisProp, node.GetValue("alarmSound"), alarmSoundVolume, false);
					if (node.HasValue("alarmShutdownButton"))
						SmarterButton.CreateButton(thisProp, node.GetValue("alarmShutdownButton"), AlarmShutdown);
					if (node.HasValue("alarmSoundLooping")) {
						if (!bool.TryParse(node.GetValue("alarmSoundLooping"), out alarmSoundLooping))
							throw new ArgumentException("So is 'alarmSoundLooping' true or false?");
					}
				}
				TurnOff();
			}

		}

		private void TurnOn()
		{
			if (!currentState) {
				switch (mode) {
					case Mode.Color:
						colorShiftRenderer.material.SetColor(colorName, (reverse ? passiveColor : activeColor));
						break;
					case Mode.Animation:
						anim[animationName].normalizedTime = reverse ? 0f : 1f;
						break;
					case Mode.Rotation:
						controlledTransform.localRotation = initialRotation * (reverse ? rotationEnd : rotationStart);
						break;
					case Mode.Translation:
						controlledTransform.localPosition = initialPosition + (reverse ? vectorEnd : vectorStart);
						break;
				}
			}
			currentState = true;
			lastStateChange = Planetarium.GetUniversalTime();
		}

		private void TurnOff()
		{
			if (currentState) {
				switch (mode) {
					case Mode.Color:
						colorShiftRenderer.material.SetColor(colorName, (reverse ? activeColor : passiveColor));
						break;
					case Mode.Animation:
						anim[animationName].normalizedTime = reverse ? 1f : 0f;
						break;
					case Mode.Rotation:
						controlledTransform.localRotation = initialRotation * (reverse ? rotationStart : rotationEnd);
						break;
					case Mode.Translation:
						controlledTransform.localPosition = initialPosition + (reverse ? vectorStart : vectorEnd);
						break;
				}
			}
			currentState = false;
			lastStateChange = Planetarium.GetUniversalTime();
		}

		public void Update()
		{
			var scaleResults = new float[3];
			for (int i = 0; i < 3; i++)
				if (!scaleEnds[i].Get(out scaleResults[i]))
					return;
			float scaledValue = Mathf.InverseLerp(scaleResults[0], scaleResults[1], scaleResults[2]);
			if (thresholdMode) {
				if (scaledValue >= threshold.x && scaledValue <= threshold.y) {
					if (flashingDelay > 0) {
						if (lastStateChange < Planetarium.GetUniversalTime() - flashingDelay) {
							if (currentState)
								TurnOff();
							else
								TurnOn();
						}
					} else
						TurnOn();
					if (audioOutput != null && !alarmActive) {
						audioOutput.audio.Play();
						alarmActive = true;
					}
				} else {
					TurnOff();
					if (audioOutput != null) {
						audioOutput.audio.Stop();
						alarmActive = false;
					}
				}

			} else {
				switch (mode) {
					case Mode.Rotation:
						Quaternion newRotation = longPath ? Quaternion.Euler(Vector3.Lerp(reverse ? vectorEnd : vectorStart, reverse ? vectorStart : vectorEnd, scaledValue)) :
						                         Quaternion.Slerp(reverse ? rotationEnd : rotationStart, reverse ? rotationStart : rotationEnd, scaledValue);
						controlledTransform.localRotation = initialRotation * newRotation;
						break;
					case Mode.Translation:
						controlledTransform.localPosition = initialPosition + Vector3.Lerp(reverse ? vectorEnd : vectorStart, reverse ? vectorStart : vectorEnd, scaledValue);
						break;
					case Mode.Color:
						colorShiftRenderer.material.SetColor(colorName, Color.Lerp(reverse ? activeColor : passiveColor, reverse ? passiveColor : activeColor, scaledValue));
						break;
					case Mode.Animation:
						float lerp = JUtil.DualLerp(reverse ? 1f : 0f, reverse ? 0f : 1f, scaleResults[0], scaleResults[1], scaleResults[2]);
						if (float.IsNaN(lerp) || float.IsInfinity(lerp)) {
							lerp = reverse ? 1f : 0f;
						}
						anim[animationName].normalizedTime = lerp;
						break;
				}
			}

		}

		public void AlarmShutdown()
		{
			if (audioOutput != null && alarmActive)
				audioOutput.audio.Stop();
		}
	}
}

