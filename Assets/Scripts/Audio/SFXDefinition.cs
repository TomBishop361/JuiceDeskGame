using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio {
	[CreateAssetMenu(fileName = "SFX_NewSound", menuName = "Game/Audio/SFX Definition")]
	public class SFXDefinition : ScriptableObject {
		[Header("Clips")]
		[Tooltip("One or more clips for this sound. If multiple clips are added, one will be chosen randomly each time.")]
		[SerializeField] private AudioClip[] clips;

		[Header("Mixer")]
		[Tooltip("The Audio Mixer Group this sound should play through. Use SFX for gameplay sounds and UI for Menu/HUD sounds.")]
		[SerializeField] private AudioMixerGroup outputMixerGroup;

		[Header("Volume")]
		[Tooltip("Base volume of this sound before randomisation and manager global volume are applied.")]
		[SerializeField][Range(0.0f, 1.0f)] private float volume = 1.0f;

		[Tooltip("Random volume multiplier range. Use 1 to 1 for no randomisation.")]
		[SerializeField] private Vector2 volumeRandomRange = new Vector2(1.0f, 1.0f);

		[Header("Pitch")]
		[Tooltip("Random pitch range. Use 1 to 1 for no pitch randomisation.")]
		[SerializeField] private Vector2 pitchRandomRange = new Vector2(1.0f, 1.0f);

		[Header("3D Settings")]
		[Tooltip("0 = fully 2D sound, 1 = fully 3D positional sound.")]
		[SerializeField][Range(0.0f, 1.0f)] private float spatialBlend = 1.0f;
		[Tooltip("Distance where the sound starts reducing in volume.")]
		[SerializeField] private float minDistance = 1.0f;
		[Tooltip("Distance where the sound becomes very quiet.")]
		[SerializeField] private float maxDistance = 30.0f;

		[Tooltip("How the sound fades over distance.")]
		[SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

		[Header("Playback")]
		[Tooltip("If true, this sound will loop until manually stopped. Examples of continuous effects: alarms, lasers, charging, etc.")]
		[SerializeField] private bool loop = false;
		[Tooltip("Prevents this sound being spammed repeatedly. Examples: weapon fire, hit reactions, locked door sounds, etc.")]
		[SerializeField] private float cooldown = 0.0f;
		[Tooltip("AudioSource priority. Lower values are more important.")]
		[Range(0, 256)]
		[SerializeField] private int priority = 128;
		[Tooltip("If true, this sound can keep playing when AudioListener.pause is enabled. Useful for UI sounds.")]
		[SerializeField] private bool ignoreListenerPause = false;

		public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
		public float Volume => volume;
		public float SpatialBlend => spatialBlend;
		public float MinDistance => minDistance;
		public float MaxDistance => maxDistance;
		public AudioRolloffMode RolloffMode => rolloffMode;
		public bool Loop => loop;
		public float Cooldown => cooldown;
		public int Priority => priority;
		public bool IgnoreListenerPause => ignoreListenerPause;

		public bool HasValidClip => clips != null && clips.Length > 0;

		// Returns a random clip from the assigned variation list
		public AudioClip GetRandomClip() {
			if (clips == null || clips.Length == 0) {
				return null;
			}

			if (clips.Length == 1) {
				return clips[0];
			}

			int index = Random.Range(0, clips.Length);
			return clips[index];
		}

		// Returns final randomised volume before the manager global volume is applied
		public float GetRandomisedVolume() {
			float randomMultiplier = Random.Range(volumeRandomRange.x, volumeRandomRange.y);
			return Mathf.Clamp01(volume * randomMultiplier);
		}

		// Returns randomised pitch
		public float GetRandomisedPitch() {
			return Random.Range(pitchRandomRange.x, pitchRandomRange.y);
		}

#if UNITY_EDITOR
		private void OnValidate() {
			volume = Mathf.Clamp01(volume);

			volumeRandomRange.x = Mathf.Clamp(volumeRandomRange.x, 0.0f, 2.0f);
			volumeRandomRange.y = Mathf.Clamp(volumeRandomRange.y, 0.0f, 2.0f);

			if (volumeRandomRange.y < volumeRandomRange.x) {
				volumeRandomRange.y = volumeRandomRange.x;
			}

			pitchRandomRange.x = Mathf.Clamp(pitchRandomRange.x, 0.1f, 3.0f);
			pitchRandomRange.y = Mathf.Clamp(pitchRandomRange.y, 0.1f, 3.0f);

			if (pitchRandomRange.y < pitchRandomRange.x) {
				pitchRandomRange.y = pitchRandomRange.x;
			}

			spatialBlend = Mathf.Clamp01(spatialBlend);
			minDistance = Mathf.Max(0.01f, minDistance);
			maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);
			cooldown = Mathf.Max(0.0f, cooldown);
		}
#endif
	}
}