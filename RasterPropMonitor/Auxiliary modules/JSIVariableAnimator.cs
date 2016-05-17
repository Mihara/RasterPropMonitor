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
        private bool muted = false;

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
            {
                return;
            }

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

                JUtil.LogMessage(this, "Configuration complete in prop {1} ({2}), supporting {0} variable indicators.", variableSets.Count, internalProp.propID, internalProp.propName);

                foreach (VariableAnimationSet thatSet in variableSets)
                {
                    alwaysActive |= thatSet.alwaysActive;
                }
                RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
                comp.UpdateDataRefreshRate(refreshRate);
                startupComplete = true;
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
            for (int i = 0; i < variableSets.Count; ++i)
            {
                variableSets[i].TearDown();
            }
            variableSets.Clear();
        }

        public void Update()
        {
            if (!JUtil.IsActiveVessel(vessel))
            {
                return;
            }

            if (!JUtil.VesselIsInIVA(vessel))
            {
                if (!muted)
                {
                    for (int unit = 0; unit < variableSets.Count; ++unit)
                    {
                        variableSets[unit].MuteSoundWhileOutOfIVA();
                    }
                }
                muted = true;
            }
            else if (muted)
            {
                for (int unit = 0; unit < variableSets.Count; ++unit)
                {
                    variableSets[unit].UnmuteSoundWhileInIVA();
                }
                muted = false;
            }

            if ((!alwaysActive && !JUtil.RasterPropMonitorShouldUpdate(vessel)) || !UpdateCheck())
            {
                return;
            }

            RPMVesselComputer comp = RPMVesselComputer.Instance(vessel);
            double universalTime = Planetarium.GetUniversalTime();
            for (int unit = 0; unit < variableSets.Count; ++unit)
            {
                variableSets[unit].Update(comp, universalTime);
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
        private readonly VariableOrNumberRange variable;
        private readonly Animation onAnim;
        private readonly Animation offAnim;
        private readonly bool thresholdMode;
        private readonly FXGroup audioOutput;
        private readonly float alarmSoundVolume;
        private readonly Vector2 threshold = Vector2.zero;
        private readonly bool reverse;
        private readonly bool usesModulo;
        private readonly string animationName;
        private readonly string stopAnimationName;
        private readonly float animationSpeed;
        private readonly bool alarmSoundLooping;
        private readonly bool alarmMustPlayOnce;
        private readonly Color passiveColor, activeColor;
        private Transform controlledTransform;
        private readonly Vector3 initialPosition, initialScale, vectorStart, vectorEnd;
        private readonly Quaternion initialRotation, rotationStart, rotationEnd;
        private readonly bool longPath;
        private readonly double flashingDelay;
        private readonly string colorName = "_EmissiveColor";
        private readonly Vector2 textureShiftStart, textureShiftEnd, textureScaleStart, textureScaleEnd;
        private Material affectedMaterial;
        private List<string> textureLayer = new List<string>();
        private readonly Mode mode;
        private readonly float resourceAmount;
        private readonly string resourceName;
        private readonly bool looping;
        private readonly float maxRateChange;
        // runtime values:
        private bool alarmActive;
        private bool currentState;
        private double lastStateChange;
        private double lastAnimUpdate;
        private Part part;
        private float lastScaledValue = -1.0f;
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

        // MOARdV TODO: If I understand the Unity docs correctly, we are leaking
        // some things here (material .get methods make copies, for instance).
        // I haven't seen conclusive signs of destructors working in child
        // objects like this, so do I need a manual method?  Or make it a MonoBehavior
        // with only the OnDestroy implemented?
        public VariableAnimationSet(ConfigNode node, InternalProp thisProp)
        {
            part = thisProp.part;

            if (!node.HasData)
            {
                throw new ArgumentException("No data?!");
            }

            string[] tokens = { };

            if (node.HasValue("scale"))
            {
                tokens = node.GetValue("scale").Split(',');
            }

            if (tokens.Length != 2)
            {
                throw new ArgumentException("Could not parse 'scale' parameter.");
            }

            string variableName = string.Empty;
            if (node.HasValue("variableName"))
            {
                variableName = node.GetValue("variableName").Trim();
            }
            else if (node.HasValue("stateMethod"))
            {
                RPMVesselComputer comp = RPMVesselComputer.Instance(part.vessel);
                string stateMethod = node.GetValue("stateMethod").Trim();
                // Verify the state method actually exists
                Func<bool> stateFunction = (Func<bool>)comp.GetMethod(stateMethod, thisProp, typeof(Func<bool>));
                if (stateFunction != null)
                {
                    variableName = "PLUGIN_" + stateMethod;
                }
                else
                {
                    throw new ArgumentException("Unrecognized stateMethod");
                }
            }
            else
            {
                throw new ArgumentException("Missing variable name.");
            }

            if (node.HasValue("modulo"))
            {
                variable = new VariableOrNumberRange(variableName, tokens[0], tokens[1], node.GetValue("modulo"));
                usesModulo = true;
            }
            else
            {
                variable = new VariableOrNumberRange(variableName, tokens[0], tokens[1]);
                usesModulo = false;
            }

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
                    onAnim = anims[0];
                    onAnim.enabled = true;
                    onAnim[animationName].speed = 0;
                    onAnim[animationName].normalizedTime = reverse ? 1f : 0f;
                    looping = node.HasValue("loopingAnimation");
                    if (looping)
                    {
                        onAnim[animationName].wrapMode = WrapMode.Loop;
                        onAnim.wrapMode = WrapMode.Loop;
                        onAnim[animationName].speed = animationSpeed;
                        mode = Mode.LoopingAnimation;
                    }
                    else
                    {
                        onAnim[animationName].wrapMode = WrapMode.Once;
                        mode = Mode.Animation;
                    }
                    onAnim.Play();
                    alwaysActive = node.HasValue("animateExterior");
                }
                else
                {
                    throw new ArgumentException("Animation could not be found.");
                }

                if (node.HasValue("stopAnimationName"))
                {
                    stopAnimationName = node.GetValue("stopAnimationName");
                    anims = node.HasValue("animateExterior") ? thisProp.part.FindModelAnimators(stopAnimationName) : thisProp.FindModelAnimators(stopAnimationName);
                    if (anims.Length > 0)
                    {
                        offAnim = anims[0];
                        offAnim.enabled = true;
                        offAnim[stopAnimationName].speed = 0;
                        offAnim[stopAnimationName].normalizedTime = reverse ? 1f : 0f;
                        if (looping)
                        {
                            offAnim[stopAnimationName].wrapMode = WrapMode.Loop;
                            offAnim.wrapMode = WrapMode.Loop;
                            offAnim[stopAnimationName].speed = animationSpeed;
                            mode = Mode.LoopingAnimation;
                        }
                        else
                        {
                            offAnim[stopAnimationName].wrapMode = WrapMode.Once;
                            mode = Mode.Animation;
                        }
                    }
                }
            }
            else if (node.HasValue("activeColor") && node.HasValue("passiveColor") && node.HasValue("coloredObject"))
            {
                if (node.HasValue("colorName"))
                {
                    colorName = node.GetValue("colorName");
                }
                if (reverse)
                {
                    activeColor = ConfigNode.ParseColor32(node.GetValue("passiveColor"));
                    passiveColor = ConfigNode.ParseColor32(node.GetValue("activeColor"));
                }
                else
                {
                    passiveColor = ConfigNode.ParseColor32(node.GetValue("passiveColor"));
                    activeColor = ConfigNode.ParseColor32(node.GetValue("activeColor"));
                }
                Renderer colorShiftRenderer = thisProp.FindModelComponent<Renderer>(node.GetValue("coloredObject"));
                affectedMaterial = colorShiftRenderer.material;
                affectedMaterial.SetColor(colorName, passiveColor);
                mode = Mode.Color;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("localRotationStart") && node.HasValue("localRotationEnd"))
            {
                controlledTransform = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim());
                initialRotation = controlledTransform.localRotation;
                if (node.HasValue("longPath"))
                {
                    longPath = true;
                    if (reverse)
                    {
                        vectorEnd = ConfigNode.ParseVector3(node.GetValue("localRotationStart"));
                        vectorStart = ConfigNode.ParseVector3(node.GetValue("localRotationEnd"));
                    }
                    else
                    {
                        vectorStart = ConfigNode.ParseVector3(node.GetValue("localRotationStart"));
                        vectorEnd = ConfigNode.ParseVector3(node.GetValue("localRotationEnd"));
                    }
                }
                else
                {
                    if (reverse)
                    {
                        rotationEnd = Quaternion.Euler(ConfigNode.ParseVector3(node.GetValue("localRotationStart")));
                        rotationStart = Quaternion.Euler(ConfigNode.ParseVector3(node.GetValue("localRotationEnd")));
                    }
                    else
                    {
                        rotationStart = Quaternion.Euler(ConfigNode.ParseVector3(node.GetValue("localRotationStart")));
                        rotationEnd = Quaternion.Euler(ConfigNode.ParseVector3(node.GetValue("localRotationEnd")));
                    }
                }
                mode = Mode.Rotation;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("localTranslationStart") && node.HasValue("localTranslationEnd"))
            {
                controlledTransform = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim());
                initialPosition = controlledTransform.localPosition;
                if (reverse)
                {
                    vectorEnd = ConfigNode.ParseVector3(node.GetValue("localTranslationStart"));
                    vectorStart = ConfigNode.ParseVector3(node.GetValue("localTranslationEnd"));
                }
                else
                {
                    vectorStart = ConfigNode.ParseVector3(node.GetValue("localTranslationStart"));
                    vectorEnd = ConfigNode.ParseVector3(node.GetValue("localTranslationEnd"));
                }
                mode = Mode.Translation;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("localScaleStart") && node.HasValue("localScaleEnd"))
            {
                controlledTransform = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim());
                initialScale = controlledTransform.localScale;
                if (reverse)
                {
                    vectorEnd = ConfigNode.ParseVector3(node.GetValue("localScaleStart"));
                    vectorStart = ConfigNode.ParseVector3(node.GetValue("localScaleEnd"));
                }
                else
                {
                    vectorStart = ConfigNode.ParseVector3(node.GetValue("localScaleStart"));
                    vectorEnd = ConfigNode.ParseVector3(node.GetValue("localScaleEnd"));
                }
                mode = Mode.Scale;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("textureLayers") && node.HasValue("textureShiftStart") && node.HasValue("textureShiftEnd"))
            {
                affectedMaterial = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim()).GetComponent<Renderer>().material;
                var textureLayers = node.GetValue("textureLayers").Split(',');
                for (int i = 0; i < textureLayers.Length; ++i)
                {
                    textureLayer.Add(textureLayers[i].Trim());
                }

                if (reverse)
                {
                    textureShiftEnd = ConfigNode.ParseVector2(node.GetValue("textureShiftStart"));
                    textureShiftStart = ConfigNode.ParseVector2(node.GetValue("textureShiftEnd"));
                }
                else
                {
                    textureShiftStart = ConfigNode.ParseVector2(node.GetValue("textureShiftStart"));
                    textureShiftEnd = ConfigNode.ParseVector2(node.GetValue("textureShiftEnd"));
                }
                mode = Mode.TextureShift;
            }
            else if (node.HasValue("controlledTransform") && node.HasValue("textureLayers") && node.HasValue("textureScaleStart") && node.HasValue("textureScaleEnd"))
            {
                affectedMaterial = thisProp.FindModelTransform(node.GetValue("controlledTransform").Trim()).GetComponent<Renderer>().material;
                var textureLayers = node.GetValue("textureLayers").Split(',');
                for (int i = 0; i < textureLayers.Length; ++i)
                {
                    textureLayer.Add(textureLayers[i].Trim());
                }

                if (reverse)
                {
                    textureScaleEnd = ConfigNode.ParseVector2(node.GetValue("textureScaleStart"));
                    textureScaleStart = ConfigNode.ParseVector2(node.GetValue("textureScaleEnd"));
                }
                else
                {
                    textureScaleStart = ConfigNode.ParseVector2(node.GetValue("textureScaleStart"));
                    textureScaleEnd = ConfigNode.ParseVector2(node.GetValue("textureScaleEnd"));
                }
                mode = Mode.TextureScale;
            }
            else
            {
                throw new ArgumentException("Cannot initiate any of the possible action modes.");
            }

            if (!(node.HasValue("maxRateChange") && float.TryParse(node.GetValue("maxRateChange"), out maxRateChange)))
            {
                maxRateChange = 0.0f;
            }
            if (maxRateChange >= 60.0f)
            {
                // Animation rate is too fast to even notice @60Hz
                maxRateChange = 0.0f;
            }
            else
            {
                lastAnimUpdate = Planetarium.GetUniversalTime();
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
                    {
                        alarmSoundVolume = float.Parse(node.GetValue("alarmSoundVolume"));
                    }
                    audioOutput = JUtil.SetupIVASound(thisProp, node.GetValue("alarmSound"), alarmSoundVolume, false);
                    if (node.HasValue("alarmMustPlayOnce"))
                    {
                        if (!bool.TryParse(node.GetValue("alarmMustPlayOnce"), out alarmMustPlayOnce))
                        {
                            throw new ArgumentException("So is 'alarmMustPlayOnce' true or false?");
                        }
                    }
                    if (node.HasValue("alarmShutdownButton"))
                    {
                        SmarterButton.CreateButton(thisProp, node.GetValue("alarmShutdownButton"), AlarmShutdown);
                    }
                    if (node.HasValue("alarmSoundLooping"))
                    {
                        if (!bool.TryParse(node.GetValue("alarmSoundLooping"), out alarmSoundLooping))
                        {
                            throw new ArgumentException("So is 'alarmSoundLooping' true or false?");
                        }
                        audioOutput.audio.loop = alarmSoundLooping;
                    }
                }

                if (node.HasValue("resourceAmount"))
                {
                    resourceAmount = float.Parse(node.GetValue("resourceAmount"));

                    if (node.HasValue("resourceName"))
                    {
                        resourceName = node.GetValue("resourceName");
                    }
                    else
                    {
                        resourceName = "ElectricCharge";
                    }
                }

                TurnOff(Planetarium.GetUniversalTime());
            }
        }

        // Some things need to be explicitly destroyed due to Unity quirks.
        internal void TearDown()
        {
            if (affectedMaterial != null)
            {
                UnityEngine.Object.Destroy(affectedMaterial);
                affectedMaterial = null;
            }
            textureLayer = null;
            controlledTransform = null;
            part = null;
        }

        private void TurnOn(double universalTime)
        {
            if (!currentState)
            {
                switch (mode)
                {
                    case Mode.Color:
                        affectedMaterial.SetColor(colorName, activeColor);
                        break;
                    case Mode.Animation:
                        onAnim[animationName].normalizedTime = reverse ? 0f : 1f;
                        break;
                    case Mode.LoopingAnimation:
                        onAnim[animationName].speed = animationSpeed;
                        if (!onAnim.IsPlaying(animationName))
                        {
                            onAnim.Play(animationName);
                        }
                        break;
                    case Mode.Rotation:
                        controlledTransform.localRotation = initialRotation * (longPath ? Quaternion.Euler(vectorStart) : rotationStart);
                        break;
                    case Mode.Translation:
                        controlledTransform.localPosition = initialPosition + vectorStart;
                        break;
                    case Mode.Scale:
                        controlledTransform.localScale = initialScale + vectorStart;
                        break;
                    case Mode.TextureShift:
                        for (int i = 0; i < textureLayer.Count; ++i)
                        {
                            affectedMaterial.SetTextureOffset(textureLayer[i], textureShiftStart);
                        }
                        break;
                    case Mode.TextureScale:
                        for (int i = 0; i < textureLayer.Count; ++i)
                        {
                            affectedMaterial.SetTextureScale(textureLayer[i], textureScaleStart);
                        }
                        break;
                }
            }

            if (resourceAmount > 0.0f)
            {
                float requesting = (resourceAmount * TimeWarp.deltaTime);
                if (requesting > 0.0f)
                {
                    float extracted = part.RequestResource(resourceName, requesting);
                    if (extracted < 0.5f * requesting)
                    {
                        // Insufficient power - shut down
                        TurnOff(universalTime);
                        return; // early, so we don't think it's on
                    }
                }
            }
            currentState = true;
            lastStateChange = universalTime;
        }

        private void TurnOff(double universalTime)
        {
            if (currentState)
            {
                switch (mode)
                {
                    case Mode.Color:
                        affectedMaterial.SetColor(colorName, passiveColor);
                        break;
                    case Mode.Animation:
                        onAnim[animationName].normalizedTime = reverse ? 1f : 0f;
                        break;
                    case Mode.LoopingAnimation:
                        if (offAnim != null)
                        {
                            offAnim[stopAnimationName].speed = animationSpeed;
                            if (!offAnim.IsPlaying(stopAnimationName))
                            {
                                offAnim.Play(stopAnimationName);
                            }
                        }
                        else
                        {
                            onAnim[animationName].speed = 0.0f;
                            onAnim[animationName].normalizedTime = reverse ? 1f : 0f;
                        }
                        break;
                    case Mode.Rotation:
                        controlledTransform.localRotation = initialRotation * (longPath ? Quaternion.Euler(vectorEnd) : rotationEnd);
                        break;
                    case Mode.Translation:
                        controlledTransform.localPosition = initialPosition + vectorEnd;
                        break;
                    case Mode.Scale:
                        controlledTransform.localScale = initialScale + vectorEnd;
                        break;
                    case Mode.TextureShift:
                        for (int i = 0; i < textureLayer.Count; ++i)
                        {
                            affectedMaterial.SetTextureOffset(textureLayer[i], textureShiftEnd);
                        }
                        break;
                    case Mode.TextureScale:
                        for (int i = 0; i < textureLayer.Count; ++i)
                        {
                            affectedMaterial.SetTextureScale(textureLayer[i], textureScaleEnd);
                        }
                        break;
                }
            }
            currentState = false;
            lastStateChange = universalTime;
        }

        public void Update(RPMVesselComputer comp, double universalTime)
        {
            float scaledValue;
            if (!variable.InverseLerp(comp, out scaledValue))
            {
                return;
            }

            float delta = Mathf.Abs(scaledValue - lastScaledValue);
            if (delta < float.Epsilon)
            {
                if (thresholdMode && flashingDelay > 0.0 && scaledValue >= threshold.x && scaledValue <= threshold.y)
                {
                    // If we're blinking our lights, they need to keep blinking
                    if (lastStateChange < universalTime - flashingDelay)
                    {
                        if (currentState)
                        {
                            TurnOff(universalTime);
                        }
                        else
                        {
                            TurnOn(universalTime);
                        }
                    }

                    if (alarmActive && audioOutput != null)
                    {
                        audioOutput.audio.volume = alarmSoundVolume * GameSettings.SHIP_VOLUME;
                    }
                }

                if (maxRateChange > 0.0f)
                {
                    lastAnimUpdate = universalTime;
                }
                return;
            }

            if (maxRateChange > 0.0f && lastScaledValue >= 0.0f)
            {
                float maxDelta = (float)(universalTime - lastAnimUpdate) * maxRateChange;

                float difference = Mathf.Abs(lastScaledValue - scaledValue);
                if (difference > maxDelta)
                {
                    bool wrapAround = usesModulo && (difference > 0.5f);
                    if (wrapAround)
                    {
                        maxDelta = Mathf.Min(maxDelta, 1.0f - difference);
                        maxDelta = -maxDelta;
                    }

                    if (scaledValue < lastScaledValue)
                    {
                        scaledValue = lastScaledValue - maxDelta;
                    }
                    else
                    {
                        scaledValue = lastScaledValue + maxDelta;
                    }

                    if(wrapAround)
                    {
                        if(scaledValue < 0.0f)
                        {
                            scaledValue += 1.0f;
                        }
                        else if(scaledValue > 1.0f)
                        {
                            scaledValue -= 1.0f;
                        }
                    }
                }
            }

            lastScaledValue = scaledValue;
            lastAnimUpdate = universalTime;

            if (thresholdMode)
            {
                if (scaledValue >= threshold.x && scaledValue <= threshold.y)
                {
                    if (flashingDelay > 0)
                    {
                        if (lastStateChange < universalTime - flashingDelay)
                        {
                            if (currentState)
                            {
                                TurnOff(universalTime);
                            }
                            else
                            {
                                TurnOn(universalTime);
                            }
                        }
                    }
                    else
                    {
                        TurnOn(universalTime);
                    }
                    if (audioOutput != null && !alarmActive)
                    {
                        audioOutput.audio.Play();
                        alarmActive = true;
                    }
                }
                else
                {
                    TurnOff(universalTime);
                    if (audioOutput != null && alarmActive)
                    {
                        if (!alarmMustPlayOnce)
                        {
                            audioOutput.audio.Stop();
                        }
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
                        Quaternion newRotation = longPath ? Quaternion.Euler(Vector3.Lerp(vectorStart, vectorEnd, scaledValue)) :
                                                 Quaternion.Slerp(rotationStart, rotationEnd, scaledValue);
                        controlledTransform.localRotation = initialRotation * newRotation;
                        break;
                    case Mode.Translation:
                        controlledTransform.localPosition = initialPosition + Vector3.Lerp(vectorStart, vectorEnd, scaledValue);
                        break;
                    case Mode.Scale:
                        controlledTransform.localScale = initialScale + Vector3.Lerp(vectorStart, vectorEnd, scaledValue);
                        break;
                    case Mode.Color:
                        affectedMaterial.SetColor(colorName, Color.Lerp(passiveColor, activeColor, scaledValue));
                        break;
                    case Mode.TextureShift:
                        for (int i = 0; i < textureLayer.Count; ++i)
                        {
                            affectedMaterial.SetTextureOffset(textureLayer[i],
                                Vector2.Lerp(textureShiftStart, textureShiftEnd, scaledValue));
                        }
                        break;
                    case Mode.TextureScale:
                        for (int i = 0; i < textureLayer.Count; ++i)
                        {
                            affectedMaterial.SetTextureScale(textureLayer[i],
                                Vector2.Lerp(textureScaleStart, textureScaleEnd, scaledValue));
                        }
                        break;
                    case Mode.LoopingAnimation:
                    // MOARdV TODO: Define what this actually does
                    case Mode.Animation:
                        float lerp = (reverse) ? (1.0f - scaledValue) : scaledValue;
                        onAnim[animationName].normalizedTime = lerp;
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

        public void UnmuteSoundWhileInIVA()
        {
            if (audioOutput != null && alarmActive)
            {
                audioOutput.audio.volume = alarmSoundVolume * GameSettings.SHIP_VOLUME;
            }
        }

        public void AlarmShutdown()
        {
            if (audioOutput != null && alarmActive && audioOutput.audio.isPlaying)
            {
                audioOutput.audio.Stop();
            }
        }
    }
}

