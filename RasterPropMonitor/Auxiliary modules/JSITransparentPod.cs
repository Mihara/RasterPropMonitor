using System;
using UnityEngine;
using System.Collections.Generic;

namespace JSI
{
	// So this is my attempt at reconstructing sfr's transparent internals plugin from first principles.
	// Because I really don't understand some of the things he's doing, why are they breaking, and
	// I'm going to attempt to recreate this module from scratch.

	public class JSITransparentPod: PartModule
	{
		[KSPField]
		public string transparentTransforms = string.Empty;
		[KSPField]
		public string transparentShaderName = "Transparent/Specular";
		[KSPField]
		public bool restoreShadersOnIVA = true;

		// I would love to know what exactly possessed Squad to have the IVA space use it's own coordinate system.
		// This rotation adjusts IVA space to match the 'real' space...
		private Quaternion MagicalVoodooRotation = new Quaternion(0, 0.7f, -0.7f, 0);

		private Quaternion originalRotation;
		private Transform originalParent;
		private Vector3 originalPosition;

		private Shader transparentShader;
		private readonly Dictionary<Transform,Shader> shadersBackup = new Dictionary<Transform, Shader>();

		public override void OnAwake()
		{
			// Apply shaders to transforms just in case the user needs us to apply shaders to ready-made models.
			if (!string.IsNullOrEmpty(transparentTransforms)) {

				transparentShader = Shader.Find(transparentShaderName);

				foreach (string transformName in transparentTransforms.Split('|')) {
					try {
						Transform tr = part.FindModelTransform(transformName);
						if (tr != null) {
							Shader backupShader = tr.renderer.material.shader;
							tr.renderer.material.shader = transparentShader;
							shadersBackup.Add(tr, backupShader);
						}
					} catch (Exception e) {
						Debug.LogException(e, this);
					}
				}
			}
		}

		private static void SetCameraCullingMask(string cameraName, bool flag)
		{
			Camera thatCamera = JUtil.GetCameraByName(cameraName);

			if (thatCamera != null) {
				if (flag) {
					thatCamera.cullingMask |= 1 << 16 | 1 << 20;
				} else {
					thatCamera.cullingMask &= ~(1 << 16 | 1 << 20);
				}
			} else
				Debug.Log("Could not find camera \"" + cameraName + "\" to change it's culling mask, check your code.");

		}

		public override void OnStart(StartState state)
		{

			JUtil.LogMessage(this, "Cleaning pod windows...");

			// In Editor, the camera we want to change is called "Main Camera". In flight, the camera to change is
			// "Camera 00", i.e. close range camera.

			if (state == StartState.Editor) {
				// I'm not sure if this change is actually needed, even. Main Camera's culling mask seems to already include IVA objects,
				// they just don't normally spawn them.
				SetCameraCullingMask("Main Camera", true);
			} else {
				SetCameraCullingMask("Camera 00", true);
			}

			// If the internal model has not yet been created, try creating it and log the exception if we fail.
			if (part.internalModel == null) {
				try {
					part.CreateInternalModel();
				} catch (Exception e) {
					Debug.LogException(e, this);
				}
			}

			// If we ended up with an existing internal model, rotate it now, so that it is shown correctly in the editor.
			if (part.internalModel != null) {
				if (state == StartState.Editor) {
					part.internalModel.transform.localRotation = MagicalVoodooRotation;
				} else {
					// Now the cruical bit. This is our first startup in a non-editor scene, so
					// we record the parent and rotation of the InternalModel so we can swap it back in.
					originalParent = part.internalModel.transform.parent;
					originalPosition = part.internalModel.transform.localPosition;
					originalRotation = part.internalModel.transform.localRotation;
				}
			}

		}

		public override void OnUpdate()
		{

			// In the editor, none of this logic should matter, even though the IVA probably exists already.
			if (HighLogic.LoadedSceneIsEditor)
				return;

			// If the internal does not exist somehow, make sure it does by force-creating it.
			if (part.internalModel == null) {
				part.CreateInternalModel();
			}

			// So we do have an internal model, right?
			if (part.internalModel != null) {

				// Check every seat in the part, if it's taken, but the kerbal model isn't spawned, respawn them all.
				if (part.protoModuleCrew.Count > 0) {
					foreach (InternalSeat seat in part.internalModel.seats) {
						if (seat.taken && seat.kerbalRef == null) {
							seat.SpawnCrew();
							break;
						}
					}
				}

				// If the internal model isn't visible, make it visible.
				if (part.internalModel.renderer == null || !part.internalModel.renderer.enabled)
					part.internalModel.SetVisible(true);
				// I don't get why the kerbal heads are flickering...

				// If the user is IVA, we undo moving the internals
				if (JUtil.IsInIVA()) {
					if (JUtil.UserIsInPod(part)) {
						// For the pod the user is actually in, we undo everthing.
						part.internalModel.transform.parent = originalParent;
						part.internalModel.transform.localRotation = originalRotation;
						part.internalModel.transform.localPosition = originalPosition;
					} else {
						// If the user is NOT in our pod but in our vessel, the IVAs go back to invisible to prevent them from showing up twice.
						if (part.internalModel.renderer == null || part.internalModel.renderer.enabled)
							part.internalModel.SetVisible(false);
					}
					// Unfortunately even if I do that, it means that at least one kerbal on the ship will see himself doubled,
					// both through the InternalCamera (which I can't modify) and the Camera 00.
					// So we have to also undo the culling mask change as well.
					SetCameraCullingMask("Camera 00", false);
					// We also undo the shaders to conceal the fact that we did anything.
					if (restoreShadersOnIVA) {
						foreach (KeyValuePair<Transform,Shader> backup in shadersBackup) {
							backup.Key.renderer.material.shader = backup.Value;
						}
					}
				} else {
					SetCameraCullingMask("Camera 00", true);
					// In all other cases we attach the IVA directly into the pod and rotate it.
					part.internalModel.transform.parent = part.transform;
					part.internalModel.transform.localRotation = MagicalVoodooRotation;
					part.internalModel.transform.localPosition = Vector3.zero;
					// And for a good measure we restore the shaders we changed.
					if (restoreShadersOnIVA) {
						foreach (KeyValuePair<Transform,Shader> backup in shadersBackup) {
							backup.Key.renderer.material.shader = transparentShader;
						}
					}
				}

			}
		}


	}
}

