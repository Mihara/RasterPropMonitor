using UnityEngine;

namespace JSI
{
	public class JSIInternalBackgroundNoise: InternalModule
	{
		[KSPField]
		public string soundURL;
		[KSPField]
		public float soundVolume = 0.1f;
		private FXGroup audioOutput;
		private bool isPlaying;

		public void Start()
		{
			audioOutput = new FXGroup("RPM" + internalModel.internalName + vessel.id);
			audioOutput.audio = internalModel.gameObject.AddComponent<AudioSource>();
			audioOutput.audio.clip = GameDatabase.Instance.GetAudioClip(soundURL);
			audioOutput.audio.Stop();
			audioOutput.audio.volume = GameSettings.SHIP_VOLUME * soundVolume;
			audioOutput.audio.rolloffMode = AudioRolloffMode.Logarithmic;
			audioOutput.audio.maxDistance = 10f;
			audioOutput.audio.minDistance = 8f;
			audioOutput.audio.dopplerLevel = 0f;
			audioOutput.audio.panLevel = 0f;
			audioOutput.audio.playOnAwake = false;
			audioOutput.audio.priority = 255;
			audioOutput.audio.loop = true;
			audioOutput.audio.pitch = 1f;
		}

		private void StopPlaying()
		{
			if (isPlaying) {
				audioOutput.audio.Stop();
				isPlaying = false;
			}
		}

		private void StartPlaying(){
			if (!isPlaying) {
				audioOutput.audio.Play();
				isPlaying = true;
			}
		}

		public override void OnUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight) {
				StopPlaying();
				return;
			}
			if (vessel != FlightGlobals.ActiveVessel) {
				StopPlaying();
				return;
			}
			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
			    CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal)) {
				StopPlaying();
				return;
			}
			StartPlaying();
		}
	}
}

