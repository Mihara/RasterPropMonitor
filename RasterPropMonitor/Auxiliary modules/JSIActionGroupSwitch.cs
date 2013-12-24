using System;
using System.Collections.Generic;
using UnityEngine;

namespace JSI
{
	public class JSIActionGroupSwitch: InternalModule
	{
		[KSPField]
		public string animationName = string.Empty;
		[KSPField]
		public string switchTransform = string.Empty;
		[KSPField]
		public string actionName = "lights";
		[KSPField]
		public bool reverse;
		[KSPField]
		public float customSpeed = 1f;
		[KSPField]
		public string internalLightName;
		[KSPField]
		public bool needsElectricCharge = true;
		[KSPField]
		public string switchSound = "Squad/Sounds/sound_click_flick";
		[KSPField]
		public float switchSoundVolume = 0.5f;
		[KSPField]
		public string coloredObject = string.Empty;
		[KSPField]
		public string colorName = "_EmissiveColor";
		[KSPField]
		public string disabledColor = string.Empty;
		private Color disabledColorValue;
		[KSPField]
		public string enabledColor = string.Empty;
		private Color enabledColorValue;
		// Neater.
		private readonly Dictionary<string,KSPActionGroup> groupList = new Dictionary<string,KSPActionGroup> { 
			{ "gear",KSPActionGroup.Gear },
			{ "brakes",KSPActionGroup.Brakes },
			{ "lights",KSPActionGroup.Light },
			{ "rcs",KSPActionGroup.RCS },
			{ "sas",KSPActionGroup.SAS },
			{ "abort",KSPActionGroup.Abort },
			{ "stage",KSPActionGroup.Stage },
			{ "custom01",KSPActionGroup.Custom01 },
			{ "custom02",KSPActionGroup.Custom02 },
			{ "custom03",KSPActionGroup.Custom03 },
			{ "custom04",KSPActionGroup.Custom04 },
			{ "custom05",KSPActionGroup.Custom05 },
			{ "custom06",KSPActionGroup.Custom06 },
			{ "custom07",KSPActionGroup.Custom07 },
			{ "custom08",KSPActionGroup.Custom08 },
			{ "custom09",KSPActionGroup.Custom09 },
			{ "custom10",KSPActionGroup.Custom10 }
		};
		private readonly Dictionary<string,bool> customGroupList = new Dictionary<string,bool> {
			{ "intlight",false },
			{ "dummy",false },
		};
		private int actionGroupID;
		private KSPActionGroup actionGroup;
		private Animation anim;
		private bool oldState;
		private bool isCustomAction;
		// Persistence for current state variable.
		private PersistenceAccessor persistence;
		private string persistentVarName;
		private Light[] lightObjects;
		private FXGroup audioOutput;
		private double electricChargeReserve;
		private const int lightCheckRate = 60;
		private int lightCheckCountdown;
		private RasterPropMonitorComputer comp;
		private bool startupComplete;
		private Renderer colorShiftRenderer;

		public void Start()
		{
			if (!groupList.ContainsKey(actionName)) {
				if (!customGroupList.ContainsKey(actionName)) {
					JUtil.LogErrorMessage(this, "Action \"{0}\" not known, the switch will not work correctly.", actionName);
				} else {
					isCustomAction = true;
				}
			} else {
				actionGroup = groupList[actionName];
				actionGroupID = BaseAction.GetGroupIndex(actionGroup);

				oldState = FlightGlobals.ActiveVessel.ActionGroups.groups[actionGroupID];
			}

			// Load our state from storage...
			if (isCustomAction) {
				if (actionName == "intlight")
					persistentVarName = internalLightName;
				else
					persistentVarName = "switch" + internalProp.propID + "_" + moduleID;

				persistence = new PersistenceAccessor(part);

				oldState = customGroupList[actionName] = (persistence.GetBool(persistentVarName) ?? oldState);

			}

			// set up the toggle switch
			SmarterButton.CreateButton(internalProp, switchTransform, Click);

			// Set up the custom actions..
			switch (actionName) {
				case "intlight":
					lightObjects = internalModel.FindModelComponents<Light>();
					if (needsElectricCharge) {
						comp = RasterPropMonitorComputer.Instantiate(internalProp);
						comp.UpdateRefreshRates(lightCheckRate, lightCheckRate);
						electricChargeReserve = (double)comp.ProcessVariable("ELECTRIC");
					}
					SetInternalLights(customGroupList[actionName]);
					break;
			}

			if (!string.IsNullOrEmpty(animationName)) {
				// Set up the animation
				anim = internalProp.FindModelAnimators(animationName)[0];
				if (anim != null) {
					anim[animationName].wrapMode = WrapMode.Once;

				} else {
					JUtil.LogErrorMessage(this, "Animation \"{0}\" not found, the switch will not work correctly.", animationName);
				}

				if (oldState ^ reverse) {
					anim[animationName].speed = float.MaxValue;
					anim[animationName].normalizedTime = 0;

				} else {

					anim[animationName].speed = float.MinValue;
					anim[animationName].normalizedTime = 1;

				}
				anim.Play(animationName);
			} else if (!string.IsNullOrEmpty(coloredObject)) {
				// Set up the color shift.
				colorShiftRenderer = internalProp.FindModelComponent<Renderer>(coloredObject);
				disabledColorValue = ConfigNode.ParseColor32(disabledColor);
				enabledColorValue = ConfigNode.ParseColor32(enabledColor);
				colorShiftRenderer.material.SetColor(colorName, (oldState ^ reverse ? enabledColorValue : disabledColorValue));
			} else
				JUtil.LogMessage(this, "Warning, neither color nor animation are defined.");

			audioOutput = JUtil.SetupIVASound(internalProp, switchSound, switchSoundVolume, false);

			startupComplete = true;
		}

		private void SetInternalLights(bool value)
		{
			if (needsElectricCharge && electricChargeReserve < 0.01d)
				value = false;
			foreach (Light lightobject in lightObjects) {
				// I probably shouldn't filter them every time, but I am getting
				// serously confused by this hierarchy.
				if (lightobject.name == internalLightName) {
					lightobject.enabled = value;
				}
			}
		}

		public void Click()
		{
			if (isCustomAction) {
				customGroupList[actionName] = !customGroupList[actionName];
				persistence.SetVar(persistentVarName, customGroupList[actionName]);
			} else
				FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(actionGroup);
			// Now we do extra things that with regular actions can't happen.
			switch (actionName) {
				case "intlight":
					SetInternalLights(customGroupList[actionName]);
					break;
				case "stage":
					Staging.ActivateNextStage();
					break;
			}
		}

		public override void OnUpdate()
		{
			if (!JUtil.IsActiveVessel(vessel))
				return;

			if (!startupComplete)
				return;

			// Bizarre, but looks like I need to animate things offscreen if I want them in the right condition when camera comes back.
			// So there's no check for internal cameras.

			bool state;
			state = isCustomAction ? customGroupList[actionName] : FlightGlobals.ActiveVessel.ActionGroups.groups[actionGroupID];

			if (state != oldState) {
				if (audioOutput != null && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
				    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal)) {
					audioOutput.audio.Play();
				}
				if (anim != null) {
					if (state ^ reverse) {
						anim[animationName].normalizedTime = 0;
						anim[animationName].speed = 1f * customSpeed;
						anim.Play(animationName);
					} else {
						anim[animationName].normalizedTime = 1;
						anim[animationName].speed = -1f * customSpeed;
						anim.Play(animationName);
					}
				} else if (colorShiftRenderer != null) {
					colorShiftRenderer.material.SetColor(colorName, (state ^ reverse ? enabledColorValue : disabledColorValue));
				}
				oldState = state;
			}

			if (actionName == "intlight" && needsElectricCharge) {
				if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
				    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal))
					return;

				lightCheckCountdown--;
				if (lightCheckCountdown <= 0) {
					lightCheckCountdown = lightCheckRate;
					electricChargeReserve = (double)comp.ProcessVariable("ELECTRIC");
					if (customGroupList["intlight"]) {
						SetInternalLights(true);
					}
				}
			}

		}

		public void LateUpdate()
		{
			if (JUtil.IsActiveVessel(vessel) && !startupComplete)
				JUtil.AnnoyUser(this);
		}
	}
}

