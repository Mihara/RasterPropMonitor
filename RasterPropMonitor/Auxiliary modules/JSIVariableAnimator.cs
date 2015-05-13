using UnityEngine;
using System;
using System.Collections.Generic;

namespace JSI
{
    public class JSIVariableAnimator : InternalModule
    {
        [KSPField]
        public int refreshRate = 10;
        private bool startupComplete;
        private int updateCountdown;
        private readonly List<VariableAnimationSet> variableSets = new List<VariableAnimationSet>();
        private bool alwaysActive;

        private bool UpdateCheck()
        {
            if (updateCountdown <= 0)
            {
                updateCountdown = refreshRate;
                return true;
            }
            updateCountdown--;
            return false;
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;

            try
            {
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
                                variableSets.Add(new VariableAnimationSet(variableNodes[i], internalProp));
                            }
                            catch (ArgumentException e)
                            {
                                JUtil.LogMessage(this, "Error in building prop number {1} - {0}", e.Message, internalProp.propID);
                            }
                        }
                        break;
                    }
                }

                // Fallback: If there are no VARIABLESET blocks, we treat the module configuration itself as a variableset block.
                if (variableSets.Count < 1 && moduleConfig != null)
                {
                    try
                    {
                        variableSets.Add(new VariableAnimationSet(moduleConfig, internalProp));
                    }
                    catch (ArgumentException e)
                    {
                        JUtil.LogMessage(this, "Error in building prop number {1} - {0}", e.Message, internalProp.propID);
                    }
                }
                JUtil.LogMessage(this, "Configuration complete in prop {1}, supporting {0} variable indicators.", variableSets.Count, internalProp.propID);
                foreach (VariableAnimationSet thatSet in variableSets)
                {
                    alwaysActive |= thatSet.alwaysActive;
                }
                startupComplete = true;
            }
            catch
            {
                JUtil.AnnoyUser(this);
                enabled = false;
                throw;
            }
        }

        public override void OnUpdate()
        {
            if (!JUtil.IsActiveVessel(vessel))
                return;

            if (!JUtil.VesselIsInIVA(vessel))
            {
                foreach (VariableAnimationSet unit in variableSets)
                {
                    unit.MuteSoundWhileOutOfIVA();
                }
            }

            if ((!alwaysActive && !JUtil.VesselIsInIVA(vessel)) || !UpdateCheck())
                return;

            foreach (VariableAnimationSet unit in variableSets)
            {
                unit.Update();
            }
        }

        public void LateUpdate()
        {
            if (vessel != null && JUtil.VesselIsInIVA(vessel) && !startupComplete)
            {
                JUtil.AnnoyUser(this);
                enabled = false;
            }
        }
    }

    public class VariableAnimationSet
    {
        private readonly VariableOrNumber[] scaleEnds = new VariableOrNumber[3];
        private readonly RasterPropMonitorComputer comp;
        private readonly Animation anim;
        private readonly bool thresholdMode;
        private readonly FXGroup audioOutput;
        private readonly float alarmSoundVolume;
        private readonly Vector2 threshold = Vector2.zero;
        private readonly bool reverse;
        private readonly string animationName;
        private readonly float animationSpeed;
        private readonly bool alarmSoundLooping;
        private readonly bool alarmMustPlayOnce;
        private readonly Color passiveColor, activeColor;
        private readonly Renderer colorShiftRenderer;
        private readonly Transform controlledTransform;
        private readonly Vector3 initialPosition, initialScale, vectorStart, vectorEnd;
        private readonly Quaternion initialRotation, rotationStart, rotationEnd;
        private readonly bool longPath;
        private readonly double flashingDelay;
        private readonly string colorName = "_EmissiveColor";
        private readonly Vector2 textureShiftStart, textureShiftEnd, textureScaleStart, textureScaleEnd;
        private readonly Material affectedMaterial;
        private readonly string textureLayer;
        private readonly Mode mode;
        private readonly float resourceAmount;
        private readonly bool looping;
        // runtime values:
        private bool alarmActive;
        private bool currentState;
        private double lastStateChange;
        private Part part;
        public readonly bool alwaysActive = false;

        private enum Mode
        {
            Animation,
            Color,
            LoopingAnimation,
            Rotation,
            Translation,
            Scale,
            TextureShift,
            TextureScale,
        }

        public VariableAnimationSet(ConfigNode node, InternalProp thisProp)
        {
            part = thisProp.part;

            if (!node.HasData)
            {
                throw new ArgumentException("No data?!");
            }

            comp = RasterPropMonitorComputer.Instantiate(thisProp);

            string[] tokens = { };

            if (node.HasValue("scale"))
            {
                tokens = node.GetValue("scale").Split(',');
            }

            if (tokens.Length != 2)
            {
                throw new ArgumentException("Could not parse 'scale' parameter.");
            }

            string variableName;
            if (node.HasValue("variableName"))
            {
                variableName = node.GetValue("variableName").Trim();
            }
            else
            {
                throw new ArgumentException("Missing variable name.");
            }

            scaleEnds[0] = new VariableOrNumber(tokens[0], comp, this);
            scaleEnds[1] = new VariableOrNumber(tokens[1], comp, this);
            scaleEnds[2] = new VariableOrNumber(variableName, comp, this);

            // That takes care of the scale, now what to do about that scale:

            if (node.HasValue("reverse"))
            {
                if (!bool.TryParse(node.GetValue("reverse"), out reverse))
                {
                    throw new ArgumentException("So is 'reverse' true or false?");
                }
            }

            if (node.HasValue("animationName"))
            {
                animationName = node.GetValue("animationName");
                if (node.HasValue("animationSpeed"))
                {
                    animationSpeed = float.Parse(node.GetValue("animationSpeed"));

                    if (reverse)
                    {
                        animationSpeed = -animationSpeed;
                    }
                }
                else
                {
                    animationSpeed = 0.0f;
                }
                Animation[] anims = node.HasValue("animateExterior") ? thisProp.part.FindModelAnimators(animationName) : thisProp.FindModelAnimators(animationName);
                if (anims.Length > 0)
                {
                    anim = anims[0];
                    anim.enabled = true;
                    anim[animationName].speed = 0;
                    anim[animationName].normalizedTime = reverse ? 1f : 0f;
                    looping = node.HasValue("loopingAnimation");
                    if (looping)
                    {
                        anim[animationName].wrapMode = WrapMode.Loop;
                        anim.wrapMode = WrapMode.Loop;
                        anim[animationName].speed = animationSpeed;
                        mode = Mode.LoopingAnimation;
                    }
                    else
                    {
                        anim[animationName].wrapMode = WrapMode.Once;
                        mode = Mode.Animation;
                    }
                    anim.Play();
                    alwaysActive = node.HasValue("animateExterior");
                }
                else
                {
                    throw new ArgumentException("Animation could not be found.");
                }
            }
            else if (node.HasValue("activeColor") && node.HasValue("passiveColor") && node.HasValue("coloredObject"))
            {
                if (node.HasValue("colorName"))
                    colorName = node.GetValue("colorName");
                passiveColor = ConfigNode.ParseColor32(node.GetValue("passiveColor"));
                activeColor = ConfigNode.ParseColor32(node.GetValue("activeColor"));
                colorShiftRenderer = thisProp.FindModelComponent<Renderer>(node.GetValue("coloredObject"));
                colorShiftRenderer.material.SetColor(colorName, reverse ? activeColor : passiveColor);
                mode = Mode.Color;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("localRotationStart") && node.HasValue("localRotationEnd"))
            {
                controlledTransform = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim());
                initialRotation = controlledTransform.localRotation;
                if (node.HasValue("longPath"))
                {
                    longPath = true;
                    vectorStart = ConfigNode.ParseVector3(node.GetValue("localRotationStart"));
                    vectorEnd = ConfigNode.ParseVector3(node.GetValue("localRotationEnd"));
                }
                else
                {
                    rotationStart = Quaternion.Euler(ConfigNode.ParseVector3(node.GetValue("localRotationStart")));
                    rotationEnd = Quaternion.Euler(ConfigNode.ParseVector3(node.GetValue("localRotationEnd")));
                }
                mode = Mode.Rotation;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("localTranslationStart") && node.HasValue("localTranslationEnd"))
            {
                controlledTransform = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim());
                initialPosition = controlledTransform.localPosition;
                vectorStart = ConfigNode.ParseVector3(node.GetValue("localTranslationStart"));
                vectorEnd = ConfigNode.ParseVector3(node.GetValue("localTranslationEnd"));
                mode = Mode.Translation;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("localScaleStart") && node.HasValue("localScaleEnd"))
            {
                controlledTransform = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim());
                initialScale = controlledTransform.localScale;
                vectorStart = ConfigNode.ParseVector3(node.GetValue("localScaleStart"));
                vectorEnd = ConfigNode.ParseVector3(node.GetValue("localScaleEnd"));
                mode = Mode.Scale;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("textureLayers") && node.HasValue("textureShiftStart") && node.HasValue("textureShiftEnd"))
            {
                affectedMaterial = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim()).renderer.material;
                textureLayer = node.GetValue("textureLayers");
                textureShiftStart = ConfigNode.ParseVector2(node.GetValue("textureShiftStart"));
                textureShiftEnd = ConfigNode.ParseVector2(node.GetValue("textureShiftEnd"));
                mode = Mode.TextureShift;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("textureLayers") && node.HasValue("textureScaleStart") && node.HasValue("textureScaleEnd"))
            {
                affectedMaterial = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim()).renderer.material;
                textureLayer = node.GetValue("textureLayers");
                textureScaleStart = ConfigNode.ParseVector2(node.GetValue("textureScaleStart"));
                textureScaleEnd = ConfigNode.ParseVector2(node.GetValue("textureScaleEnd"));
                mode = Mode.TextureScale;
            }
            else
            {
                throw new ArgumentException("Cannot initiate any of the possible action modes.");
            }

            if (node.HasValue("threshold"))
            {
                threshold = ConfigNode.ParseVector2(node.GetValue("threshold"));
            }

            resourceAmount = 0.0f;
            if (threshold != Vector2.zero)
            {
                thresholdMode = true;

                float min = Mathf.Min(threshold.x, threshold.y);
                float max = Mathf.Max(threshold.x, threshold.y);
                threshold.x = min;
                threshold.y = max;

                if (node.HasValue("flashingDelay"))
                {
                    flashingDelay = double.Parse(node.GetValue("flashingDelay"));
                }

                if (node.HasValue("alarmSound"))
                {
                    alarmSoundVolume = 0.5f;
                    if (node.HasValue("alarmSoundVolume"))
                        alarmSoundVolume = float.Parse(node.GetValue("alarmSoundVolume"));
                    audioOutput = JUtil.SetupIVASound(thisProp, node.GetValue("alarmSound"), alarmSoundVolume, false);
                    if (node.HasValue("alarmMustPlayOnce"))
                    {
                        if (!bool.TryParse(node.GetValue("alarmMustPlayOnce"), out alarmMustPlayOnce))
                            throw new ArgumentException("So is 'alarmMustPlayOnce' true or false?");
                    }
                    if (node.HasValue("alarmShutdownButton"))
                        SmarterButton.CreateButton(thisProp, node.GetValue("alarmShutdownButton"), AlarmShutdown);
                    if (node.HasValue("alarmSoundLooping"))
                    {
                        if (!bool.TryParse(node.GetValue("alarmSoundLooping"), out alarmSoundLooping))
                            throw new ArgumentException("So is 'alarmSoundLooping' true or false?");
                        audioOutput.audio.loop = alarmSoundLooping;
                    }
                }

                if (node.HasValue("resourceAmount"))
                {
                    resourceAmount = float.Parse(node.GetValue("resourceAmount"));
                }

                TurnOff();
            }
        }

        private void TurnOn()
        {
            if (!currentState)
            {
                switch (mode)
                {
                    case Mode.Color:
                        colorShiftRenderer.material.SetColor(colorName, (reverse ? passiveColor : activeColor));
                        break;
                    case Mode.Animation:
                        anim[animationName].normalizedTime = reverse ? 0f : 1f;
                        break;
                    case Mode.LoopingAnimation:
                        anim[animationName].speed = animationSpeed;
                        break;
                    case Mode.Rotation:
                        controlledTransform.localRotation = initialRotation * (reverse ? rotationEnd : rotationStart);
                        break;
                    case Mode.Translation:
                        controlledTransform.localPosition = initialPosition + (reverse ? vectorEnd : vectorStart);
                        break;
                    case Mode.Scale:
                        controlledTransform.localScale = initialScale + (reverse ? vectorEnd : vectorStart);
                        break;
                    case Mode.TextureShift:
                        foreach (string token in textureLayer.Split(','))
                        {
                            affectedMaterial.SetTextureOffset(token.Trim(), reverse ? textureShiftEnd : textureShiftStart);
                        }
                        break;
                    case Mode.TextureScale:
                        foreach (string token in textureLayer.Split(','))
                        {
                            affectedMaterial.SetTextureScale(token.Trim(), reverse ? textureScaleEnd : textureScaleStart);
                        }
                        break;
                }
            }

            if (resourceAmount > 0.0f)
            {
                float requesting = (resourceAmount * TimeWarp.deltaTime);
                float extracted = part.RequestResource("ElectricCharge", requesting);
                if (Mathf.Abs(requesting - extracted) < Mathf.Abs(0.5f * requesting))
                {
                    // Insufficient power - shut down
                    TurnOff();
                    return; // early, so we don't thinl it's on
                }
            }
            currentState = true;
            lastStateChange = Planetarium.GetUniversalTime();
        }

        private void TurnOff()
        {
            if (currentState)
            {
                switch (mode)
                {
                    case Mode.Color:
                        colorShiftRenderer.material.SetColor(colorName, (reverse ? activeColor : passiveColor));
                        break;
                    case Mode.Animation:
                        anim[animationName].normalizedTime = reverse ? 1f : 0f;
                        break;
                    case Mode.LoopingAnimation:
                        anim[animationName].speed = 0.0f;
                        anim[animationName].normalizedTime = reverse ? 1f : 0f;
                        break;
                    case Mode.Rotation:
                        controlledTransform.localRotation = initialRotation * (reverse ? rotationStart : rotationEnd);
                        break;
                    case Mode.Translation:
                        controlledTransform.localPosition = initialPosition + (reverse ? vectorStart : vectorEnd);
                        break;
                    case Mode.Scale:
                        controlledTransform.localScale = initialScale + (reverse ? vectorStart : vectorEnd);
                        break;
                    case Mode.TextureShift:
                        foreach (string token in textureLayer.Split(','))
                        {
                            affectedMaterial.SetTextureOffset(token.Trim(), reverse ? textureShiftStart : textureShiftEnd);
                        }
                        break;
                    case Mode.TextureScale:
                        foreach (string token in textureLayer.Split(','))
                        {
                            affectedMaterial.SetTextureScale(token.Trim(), reverse ? textureScaleStart : textureScaleEnd);
                        }
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
            {
                if (!scaleEnds[i].Get(out scaleResults[i]))
                {
                    return;
                }
            }
            float scaledValue = Mathf.InverseLerp(scaleResults[0], scaleResults[1], scaleResults[2]);
            if (thresholdMode)
            {
                if (scaledValue >= threshold.x && scaledValue <= threshold.y)
                {
                    if (flashingDelay > 0)
                    {
                        if (lastStateChange < Planetarium.GetUniversalTime() - flashingDelay)
                        {
                            if (currentState)
                            {
                                TurnOff();
                            }
                            else
                            {
                                TurnOn();
                            }
                        }
                    }
                    else
                    {
                        TurnOn();
                    }
                    if (audioOutput != null && !alarmActive)
                    {
                        audioOutput.audio.Play();
                        alarmActive = true;
                    }
                }
                else
                {
                    TurnOff();
                    if (audioOutput != null)
                    {
                        if (!alarmMustPlayOnce)
                            audioOutput.audio.Stop();
                        alarmActive = false;
                    }
                }
                // Resetting the audio volume in case it was muted while the ship was out of IVA.
                if (alarmActive && audioOutput != null)
                {
                    audioOutput.audio.volume = alarmSoundVolume * GameSettings.SHIP_VOLUME;
                }
            }
            else
            {
                switch (mode)
                {
                    case Mode.Rotation:
                        Quaternion newRotation = longPath ? Quaternion.Euler(Vector3.Lerp(reverse ? vectorEnd : vectorStart, reverse ? vectorStart : vectorEnd, scaledValue)) :
                                                 Quaternion.Slerp(reverse ? rotationEnd : rotationStart, reverse ? rotationStart : rotationEnd, scaledValue);
                        controlledTransform.localRotation = initialRotation * newRotation;
                        break;
                    case Mode.Translation:
                        controlledTransform.localPosition = initialPosition + Vector3.Lerp(reverse ? vectorEnd : vectorStart, reverse ? vectorStart : vectorEnd, scaledValue);
                        break;
                    case Mode.Scale:
                        controlledTransform.localScale = initialScale + Vector3.Lerp(reverse ? vectorEnd : vectorStart, reverse ? vectorStart : vectorEnd, scaledValue);
                        break;
                    case Mode.Color:
                        colorShiftRenderer.material.SetColor(colorName, Color.Lerp(reverse ? activeColor : passiveColor, reverse ? passiveColor : activeColor, scaledValue));
                        break;
                    case Mode.TextureShift:
                        foreach (string token in textureLayer.Split(','))
                        {
                            affectedMaterial.SetTextureOffset(token.Trim(),
                                Vector2.Lerp(reverse ? textureShiftEnd : textureShiftStart, reverse ? textureShiftStart : textureShiftEnd, scaledValue));
                        }
                        break;
                    case Mode.TextureScale:
                        foreach (string token in textureLayer.Split(','))
                        {
                            affectedMaterial.SetTextureScale(token.Trim(),
                                Vector2.Lerp(reverse ? textureScaleEnd : textureScaleStart, reverse ? textureScaleStart : textureScaleEnd, scaledValue));
                        }
                        break;
                    case Mode.LoopingAnimation:
                    // MOARdV TODO: Define what this actually does
                    case Mode.Animation:
                        float lerp = JUtil.DualLerp(reverse ? 1f : 0f, reverse ? 0f : 1f, scaleResults[0], scaleResults[1], scaleResults[2]);
                        if (float.IsNaN(lerp) || float.IsInfinity(lerp))
                        {
                            lerp = reverse ? 1f : 0f;
                        }
                        anim[animationName].normalizedTime = lerp;
                        break;
                }
            }

        }

        public void MuteSoundWhileOutOfIVA()
        {
            if (audioOutput != null && alarmActive)
            {
                audioOutput.audio.volume = 0;
            }
        }

        public void AlarmShutdown()
        {
            if (audioOutput != null && alarmActive)
                audioOutput.audio.Stop();
        }
    }
}

