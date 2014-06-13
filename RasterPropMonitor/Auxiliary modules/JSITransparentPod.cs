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

		private bool shadersAreTransparent;

		public override void OnAwake()
		{
			// Apply shaders to transforms on startup.
			if (!string.IsNullOrEmpty(transparentTransforms)) {

				transparentShader = Shader.Find(transparentShaderName);

				foreach (string transformName in transparentTransforms.Split('|')) {
					try {
						Transform tr = part.FindModelTransform(transformName);
						if (tr != null) {
							// We both change the shader and backup the original shader so we can undo it later.
							Shader backupShader = tr.renderer.material.shader;
							tr.renderer.material.shader = transparentShader;
							shadersBackup.Add(tr, backupShader);
						}
					} catch (Exception e) {
						Debug.LogException(e, this);
					}
				}
				shadersAreTransparent = true;
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
			} else {
				// Some error-proofing. I won't bother doing this every frame, because one error message
				// should suffice, this module is not supposed to be attached to parts that have no internals in the first place.
				JUtil.LogErrorMessage(this, "Wait, where's my internal model?");
			}
		}

		public override void OnUpdate()
		{

			// In the editor, none of this logic should matter, even though the IVA probably exists already.
			if (HighLogic.LoadedSceneIsEditor)
				return;

			// If the internal does not exist, for example, because the vessel was switched, make sure it does by force-creating it.
			if (part.internalModel == null) {
				part.CreateInternalModel();
				// Notice that spawning crew also causes the internal to go invisible...
				part.SpawnCrew();
			}

			// Now we need to make sure that the list of portraits in the GUI conforms to what actually is in the active vessel.
			// This is important because IVA/EVA buttons clicked on kerbals that are not in the active vessel cause problems
			// that I can't readily debug, and it shouldn't happen anyway.
			foreach (InternalSeat seat in part.internalModel.seats) {
				if (vessel.isActiveVessel) {
					if (!KerbalGUIManager.ActiveCrew.Contains(seat.kerbalRef)) {
						KerbalGUIManager.AddActiveCrew(seat.kerbalRef);
					}
				} else {
					KerbalGUIManager.RemoveActiveCrew(seat.kerbalRef);
				}
			}


			// So we do have an internal model, right?
			if (part.internalModel != null) {

				if (JUtil.IsInIVA()) {
					// If the user is IVA, we move the internals to the original position,
					// so that they show up correctly on InternalCamera. This particularly concerns
					// the pod the user is inside of.
					part.internalModel.transform.parent = originalParent;
					part.internalModel.transform.localRotation = originalRotation;
					part.internalModel.transform.localPosition = originalPosition;

					if (!JUtil.UserIsInPod(part)) {
						// If the user is in some other pod than this one, we also hide our IVA to prevent them from being drawn above
						// everything else.
						part.internalModel.SetVisible(false);
					}

					// Unfortunately even if I do that, it means that at least one kerbal on the ship will see his own IVA twice in two different orientations,
					// one time through the InternalCamera (which I can't modify) and another through the Camera 00.
					// So we have to also undo the culling mask change as well.
					SetCameraCullingMask("Camera 00", false);

					// So once everything is hidden again, we undo the change in shaders to conceal the fact that you can't see other internals.
					if (restoreShadersOnIVA & shadersAreTransparent) {
						foreach (KeyValuePair<Transform,Shader> backup in shadersBackup) {
							backup.Key.renderer.material.shader = backup.Value;
						}
						shadersAreTransparent = false;
					}

				} else {
					// Otherwise, we're out of IVA, so we can proceed with setting up the pods for exterior view.

					SetCameraCullingMask("Camera 00", true);

					// Make the internal model visible...
					part.internalModel.SetVisible(true);

					// And for a good measure we make sure the shader change has been applied.
					if (restoreShadersOnIVA && !shadersAreTransparent) {
						foreach (KeyValuePair<Transform,Shader> backup in shadersBackup) {
							backup.Key.renderer.material.shader = transparentShader;
						}
						shadersAreTransparent = true;
					}

					// Now we attach the restored IVA directly into the pod at zero local coordinates and rotate it,
					// so that it shows up on the main outer view camera in the correct location.
					part.internalModel.transform.parent = part.transform;
					part.internalModel.transform.localRotation = MagicalVoodooRotation;
					part.internalModel.transform.localPosition = Vector3.zero;

				}

			}
		}


	}
}

