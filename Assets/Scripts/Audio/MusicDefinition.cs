using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio {
	// ScriptableObject data asset for a single music state/track
	// This allows music settings to be configured by designers
	/// </summary>
	[CreateAssetMenu(fileName = "MUS_NewTrack", menuName = "Game/Audio/Music Definition")]
	public class MusicDefinition : ScriptableObject {
		[Header("Clip")]
		[Tooltip("The music clip that should play for this state.")]
		[SerializeField] private AudioClip clip;

		[Header("Mixer")]
		[Tooltip("The Audio Mixer Group this music should play through.")]
		[SerializeField] private AudioMixerGroup outputMixerGroup;

		[Header("Volume")]
		[Tooltip("Base volume for this music track.")]
		[SerializeField][Range(0.0f, 1.0f)] private float volume = 1.0f;

		[Header("Playback")]
		[Tooltip("Should this music track loop?")]
		[SerializeField] private bool loop = true;

		// Read-only access for MusicManager
		public AudioClip Clip => clip;
		public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
		public float Volume => volume;
		public bool Loop => loop;

		// Used by MusicManager to avoid trying to play an empty music asset
		public bool IsValid => clip != null;

#if UNITY_EDITOR
		private void OnValidate() {
			volume = Mathf.Clamp01(volume);
		}
#endif
	}
}