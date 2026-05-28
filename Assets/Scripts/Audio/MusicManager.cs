using System.Collections;
using UnityEngine;

namespace Game.Audio {
	// Persistent music playback manager
	// Uses two AudioSources so it can crossfade smoothly between music states such as Relax, Middle, and Intense
	public class MusicManager : MonoBehaviour {
		public static MusicManager Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() {
			Instance = null;
		}

		[Header("Lifetime")]
		[Tooltip("If true, the MusicManager survives scene loads.")]
		[SerializeField] private bool dontDestroyOnLoad = true;

		[Header("Sources")]
		[Tooltip("First music AudioSource. If empty, one will be created automatically.")]
		[SerializeField] private AudioSource sourceA;
		[Tooltip("Second music AudioSource. Used for crossfading. If empty, one will be created automatically.")]
		[SerializeField] private AudioSource sourceB;

		[Header("Volume")]
		[Tooltip("Global multiplier for all music.")]
		[SerializeField][Range(0.0f, 1.0f)] private float globalMusicVolume = 1.0f;

		[Header("Fade")]
		[Tooltip("Default crossfade duration when switching music states.")]
		[SerializeField] private float defaultFadeDuration = 2.0f;

		[Header("Debug")]
		[Tooltip("If true, prints logs to the Unity Console.")]
		[SerializeField] private bool debugLogging = true;

		// The source currently audible or considered the main music source
		private AudioSource activeSource;

		// The spare source used to fade in the next track
		private AudioSource inactiveSource;

		// The MusicDefinition currently playing after the latest completed/started request
		private MusicDefinition currentMusic;

		// Stored so a new music request can cancel the previous fade cleanly
		private Coroutine fadeRoutine;

		public MusicDefinition CurrentMusic => currentMusic;

		private void Awake() {
			// Singleton used so the persistent player scene only owns one MusicManager
			if (Instance != null && Instance != this) {
				Destroy(gameObject);
				return;
			}

			Instance = this;

			if (dontDestroyOnLoad) {
				DontDestroyOnLoad(gameObject);
			}

			CreateSourcesIfNeeded();

			activeSource = sourceA;
			inactiveSource = sourceB;

			PrepareSource(sourceA);
			PrepareSource(sourceB);
		}

		//  Creates the two required AudioSources automatically if they were not assigned in the Inspector
		private void CreateSourcesIfNeeded() {
			if (sourceA == null) {
				sourceA = gameObject.AddComponent<AudioSource>();
			}

			if (sourceB == null) {
				sourceB = gameObject.AddComponent<AudioSource>();
			}
		}

		// Applies default AudioSource settings for 2D music playback
		private void PrepareSource(AudioSource source) {
			source.playOnAwake = false;
			source.loop = true;

			// Music should be non-positional
			// It should not change with player/camera position
			source.spatialBlend = 0.0f;

			// Start silent
			// Tracks fade in when requested
			source.volume = 0.0f;

			// Doppler is usually not useful for 2D music since it can cause wierd pitch changes
			source.dopplerLevel = 0.0f;
		}

		// Plays or crossfades to a new music track
		// Passing fadeDuration below 0 uses the manager's default fade duration
		public static void Play(MusicDefinition music, float fadeDuration = -1.0f) {
			if (Instance == null) {
				Debug.LogWarning("MusicManager.Play failed because there is no MusicManager in the scene.");
				return;
			}

			Instance.PlayInternal(music, fadeDuration);
		}

		// Stops the current music with an optional fade out
		// Passing fadeDuration below 0 uses the manager's default fade duration
		public static void Stop(float fadeDuration = -1.0f) {
			if (Instance == null) {
				return;
			}

			Instance.StopInternal(fadeDuration);
		}

		// Sets overall music volume without changing individual MusicDefinition volumes.
		// This is intended for options/settings UI
		// Example: slider should call MusicManager.SetGlobalVolume(value)
		public static void SetGlobalVolume(float volume) {
			if (Instance == null) {
				return;
			}

			Instance.globalMusicVolume = Mathf.Clamp01(volume);
			Instance.RefreshActiveVolume();
		}

		private void PlayInternal(MusicDefinition music, float fadeDuration) {
			if (music == null || music.IsValid == false) {
				if (debugLogging) {
					Debug.LogWarning("MusicManager tried to play a missing or invalid MusicDefinition.");
				}

				return;
			}

			// Do not restart/crossfade if this exact track is already playing
			if (currentMusic == music && activeSource.isPlaying && fadeRoutine == null) {
				return;
			}

			float finalFadeDuration = fadeDuration >= 0.0f ? fadeDuration : defaultFadeDuration;

			// A new request replaces any fade currently in progress
			if (fadeRoutine != null) {
				StopCoroutine(fadeRoutine);
			}

			fadeRoutine = StartCoroutine(CrossfadeRoutine(music, finalFadeDuration));
		}

		private void StopInternal(float fadeDuration) {
			float finalFadeDuration = fadeDuration >= 0.0f ? fadeDuration : defaultFadeDuration;

			if (fadeRoutine != null) {
				StopCoroutine(fadeRoutine);
			}

			fadeRoutine = StartCoroutine(StopRoutine(finalFadeDuration));
		}

		// Fades the current active source out while fading the inactive source in with the new track
		private IEnumerator CrossfadeRoutine(MusicDefinition newMusic, float fadeDuration) {
			AudioSource oldSource = activeSource;
			AudioSource newSource = inactiveSource;

			// Prepare the inactive source with the incoming track before fading it in
			newSource.clip = newMusic.Clip;
			newSource.outputAudioMixerGroup = newMusic.OutputMixerGroup;
			newSource.loop = newMusic.Loop;
			newSource.volume = 0.0f;
			newSource.Play();

			float oldStartVolume = oldSource.isPlaying ? oldSource.volume : 0.0f;
			float newTargetVolume = newMusic.Volume * globalMusicVolume;

			// Instant switching is supported for cutscene or zero fade settings
			if (fadeDuration <= 0.0f) {
				oldSource.Stop();
				oldSource.volume = 0.0f;

				newSource.volume = newTargetVolume;

				SwapSources();
				currentMusic = newMusic;
				fadeRoutine = null;
				yield break;
			}

			float timer = 0.0f;

			while (timer < fadeDuration) {
				// Use unscaled time so music fades still complete during pause/slow-motion transitions
				timer += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(timer / fadeDuration);

				oldSource.volume = Mathf.Lerp(oldStartVolume, 0.0f, t);
				newSource.volume = Mathf.Lerp(0.0f, newTargetVolume, t);

				yield return null;
			}

			// Fully stop the old source so it is silent and ready to be reused as the next inactive source
			oldSource.Stop();
			oldSource.volume = 0.0f;

			newSource.volume = newTargetVolume;

			SwapSources();

			currentMusic = newMusic;
			fadeRoutine = null;
		}

		// Fades out and stops the currently active music source
		private IEnumerator StopRoutine(float fadeDuration) {
			AudioSource oldSource = activeSource;
			float startVolume = oldSource.volume;

			if (fadeDuration <= 0.0f) {
				oldSource.Stop();
				oldSource.volume = 0.0f;
				currentMusic = null;
				fadeRoutine = null;
				yield break;
			}

			float timer = 0.0f;

			while (timer < fadeDuration) {
				// Use unscaled time so fade-outs still happen while Time.timeScale is 0
				timer += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(timer / fadeDuration);

				oldSource.volume = Mathf.Lerp(startVolume, 0.0f, t);

				yield return null;
			}

			oldSource.Stop();
			oldSource.volume = 0.0f;

			currentMusic = null;
			fadeRoutine = null;
		}

		// After a crossfade, the faded-in source becomes active and the faded-out source becomes inactive
		private void SwapSources() {
			AudioSource previousActive = activeSource;
			activeSource = inactiveSource;
			inactiveSource = previousActive;
		}

		// Re-applies the current music volume after the global music volume changes
		private void RefreshActiveVolume() {
			if (currentMusic == null || activeSource == null) {
				return;
			}

			activeSource.volume = currentMusic.Volume * globalMusicVolume;
		}

#if UNITY_EDITOR
		private void OnValidate() {
			globalMusicVolume = Mathf.Clamp01(globalMusicVolume);
			defaultFadeDuration = Mathf.Max(0.0f, defaultFadeDuration);
		}
#endif
	}
}