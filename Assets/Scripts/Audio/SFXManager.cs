using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio {
	// Central manager for playing SFX across the game
	// This manager handles:
	// - 2D UI/non-positional sounds
	// - 3D world-position sounds
	// - Sounds attached to moving Transforms
	// - AudioSource pooling
	// - Per-sound cooldowns
	// - Global SFX volume
	// - Pausing, stopping, and returning AudioSources to the pool
	// Gameplay scripts don't need to manually create AudioSources
	// Instead, they should expose SFXDefinition fields and call SFXManager.Play / PlayAtPosition / PlayAttached
	public class SFXManager : MonoBehaviour {
		public static SFXManager Instance { get; private set; }

		// Resets static references before entering Play Mode
		// Useful because our Unity Enter Play Mode Options has Domain Reload disabled
		// This prevents static variables from surviving between Play sessions
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() {
			Instance = null;
		}

		[Header("Lifetime")]
		[Tooltip("If true, the manager will persist across scene loads.")]
		[SerializeField] private bool dontDestroyOnLoad = true;

		[Header("Pooling")]
		[Tooltip("How many AudioSources are created when the scene starts.")]
		[SerializeField] private int initialPoolSize = 32;
		[Tooltip("Maximum number of AudioSources allowed.")]
		[SerializeField] private int maxPoolSize = 96;

		[Header("Volume")]
		[Tooltip("Global multiplier applied to all SFX played through this manager.")]
		[SerializeField][Range(0.0f, 1.0f)] private float globalSFXVolume = 1.0f;

		[Header("Debug")]
		[Tooltip("If true, prints logs to the Unity Console.")]
		[SerializeField] private bool debugLogging = true;

		// AudioSources that are currently available to be reused
		private readonly List<AudioSource> availableSources = new List<AudioSource>();

		// AudioSources that are currently playing or reserved
		private readonly HashSet<AudioSource> activeSources = new HashSet<AudioSource>();

		// Tracks cooldown end times per SFXDefinition instance ID
		// This prevents spam sounds such as hit reactions or rapid weapon fire
		private readonly Dictionary<int, float> cooldownEndTimes = new Dictionary<int, float>();

		private Transform poolRoot;
		private bool isPaused;

		// Global volume multiplier for all SFX
		public float GlobalSFXVolume {
			get => globalSFXVolume;
			set => globalSFXVolume = Mathf.Clamp01(value);
		}

		// Sets up the singleton instance, optionally persists across scenes, creates the pool root, and prewarms AudioSources
		private void Awake() {
			// Prevent duplicate SFXManagers if another scene also contains one
			if (Instance != null && Instance != this) {
				Destroy(gameObject);
				return;
			}

			Instance = this;

			// To be used with our airlock/scene transition setup so sounds do not cut off between scenes
			if (dontDestroyOnLoad) {
				DontDestroyOnLoad(gameObject);
			}

			CreatePoolRoot();
			PrewarmPool();
		}

		// Creates a child object that stores inactive pooled AudioSources
		private void CreatePoolRoot() {
			GameObject rootObject = new GameObject("SFX AudioSource Pool");
			rootObject.transform.SetParent(transform);
			rootObject.transform.localPosition = Vector3.zero;

			poolRoot = rootObject.transform;
		}

		// Creates the starting AudioSource pool
		// This avoids creating lots of AudioSource GameObjects during combat
		private void PrewarmPool() {
			int count = Mathf.Clamp(initialPoolSize, 0, maxPoolSize);

			for (int i = 0; i < count; i++) {
				AudioSource source = CreateNewSource();
				ReturnSourceToPool(source);
			}
		}

		// Plays a 2D sound
		// Can be used for:
		// - UI button hover/click
		// - HUD feedback
		// - Menu sounds
		public static AudioSource Play(SFXDefinition sfx, float volumeMultiplier = 1.0f, float pitchMultiplier = 1.0f) {
			if (Instance == null) {
				Debug.LogWarning("SFXManager.Play failed because there is no SFXManager in the scene.");
				return null;
			}

			return Instance.PlayInternal(
				sfx,
				position: Vector3.zero,
				parent: null,
				force2D: true,
				volumeMultiplier: volumeMultiplier,
				pitchMultiplier: pitchMultiplier
			);
		}

		// Plays a 3D sound at a world position
		// Can be used for:
		// - Impacts
		// - Explosions
		// - Enemy deaths
		// - Weapon shots from a muzzle position (for the shield enemy minigun)
		public static AudioSource PlayAtPosition(SFXDefinition sfx, Vector3 position, float volumeMultiplier = 1.0f, float pitchMultiplier = 1.0f) {
			if (Instance == null) {
				Debug.LogWarning("SFXManager.PlayAtPosition failed because there is no SFXManager in the scene.");
				return null;
			}

			return Instance.PlayInternal(
				sfx,
				position: position,
				parent: null,
				force2D: false,
				volumeMultiplier: volumeMultiplier,
				pitchMultiplier: pitchMultiplier
			);
		}

		// Plays a sound attached to a transform
		// Can be used when the sound should follow a moving object, such as:
		// - Player movement sounds
		// - Moving doors
		// - Looping sounds
		public static AudioSource PlayAttached(SFXDefinition sfx, Transform parent, float volumeMultiplier = 1.0f, float pitchMultiplier = 1.0f) {
			if (Instance == null) {
				Debug.LogWarning("SFXManager.PlayAttached failed because there is no SFXManager in the scene.");
				return null;
			}

			if (parent == null) {
				if (Instance.debugLogging) {
					Debug.LogWarning($"SFXManager.PlayAttached failed because parent was null for SFX '{sfx?.name}'.");
				}

				return null;
			}

			return Instance.PlayInternal(
				sfx,
				position: parent.position,
				parent: parent,
				force2D: false,
				volumeMultiplier: volumeMultiplier,
				pitchMultiplier: pitchMultiplier
			);
		}

		// Stops a specific sound source and returns it to the pool
		// Can be used for:
		// - Alarm loop
		// - Sniper shot beam loop
		// - Drone hover hum
		public static void Stop(AudioSource source) {
			if (Instance == null || source == null) {
				return;
			}

			Instance.StopInternal(source);
		}

		// Stops every active SFX source currently managed by this SFXManager
		// Can be used for:
		// - Restarting a level
		// - Hard-cutting to a new state
		public static void StopAll() {
			if (Instance == null) {
				return;
			}

			Instance.StopAllInternal();
		}

		// Pauses or unpauses all active SFX sources managed by this SFXManager
		public static void SetPaused(bool paused) {
			if (Instance == null) {
				return;
			}

			Instance.SetPausedInternal(paused);
		}

		// Sets the global SFX volume
		// This is intended for options/settings UI
		// Example: slider should call SFXManager.SetGlobalVolume(value)
		public static void SetGlobalVolume(float volume) {
			if (Instance == null) {
				Debug.LogWarning("SFXManager.SetGlobalVolume failed because there is no SFXManager in the scene.");
				return;
			}

			Instance.GlobalSFXVolume = volume;
		}

		// Core playback function used by all public Play methods
		// This function does the following:
		// - Validates the SFXDefinition
		// - Checks cooldowns
		// - Picks a random clip
		// - Gets an AudioSource from the pool
		// - Applies volumes, pitch, mixer, and 3D settings
		// - Playes the sound
		// - Returns non-looping sounds to the pool once finished
		private AudioSource PlayInternal(SFXDefinition sfx, Vector3 position, Transform parent, bool force2D, float volumeMultiplier, float pitchMultiplier) {
			if (sfx == null) {
				if (debugLogging) {
					Debug.LogWarning("SFXManager tried to play a null SFXDefinition.");
				}

				return null;
			}

			if (sfx.HasValidClip == false) {
				if (debugLogging) {
					Debug.LogWarning($"SFXDefinition '{sfx.name}' has no AudioClips assigned.");
				}

				return null;
			}

			// Cooldowns are checked before taking an AudioSource from the pool
			// This is to avoid wasting any pooled sources on sounds that should not play yet
			if (IsOnCooldown(sfx)) {
				return null;
			}

			AudioClip[] clipsToPlay = sfx.GetClipsToPlay();

			if (clipsToPlay == null || clipsToPlay.Length == 0) {
				if (debugLogging) {
					Debug.LogWarning($"SFXDefinition '{sfx.name}' did not return any valid clips to play.");
				}

				return null;
			}

			AudioSource firstSource = null;
			bool playedAnyClip = false;

			// If the SFXDefinition is set to AllAtOnce, this loop layers every valid clip.
			// If it is set to RandomOne, clipsToPlay only contains one clip.
			for (int i = 0; i < clipsToPlay.Length; i++) {
				AudioClip clip = clipsToPlay[i];

				if (clip == null) {
					continue;
				}

				AudioSource source = GetSource();

				if (source == null) {
					if (debugLogging) {
						Debug.LogWarning($"SFXManager could not play '{sfx.name}' because the AudioSource pool is full.");
					}

					break;
				}

				// Final volume is built from:
				// SFXDefinition base/random volume * call-site multiplier * global SFX volume
				float finalVolume = sfx.GetRandomisedVolume() * Mathf.Clamp01(volumeMultiplier) * globalSFXVolume;

				// Pitch is also randomised per SFXDefinition, then adjusted by an optional call-site multiplier
				float finalPitch = sfx.GetRandomisedPitch() * Mathf.Max(0.01f, pitchMultiplier);

				// Force 2D is used by Play() so UI sounds ignore 3D spatial settings
				float finalSpatialBlend = force2D ? 0.0f : sfx.SpatialBlend;

				ConfigureSource(source, sfx, clip, finalVolume, finalPitch, finalSpatialBlend);

				// Attached sounds follow the parent object
				// Position-based sounds stay under the manager and play at a fixed world position
				if (parent != null) {
					source.transform.SetParent(parent);
					source.transform.localPosition = Vector3.zero;
				}
				else {
					source.transform.SetParent(transform);
					source.transform.position = position;
				}

				source.gameObject.SetActive(true);
				activeSources.Add(source);

				source.Play();

				if (firstSource == null) {
					firstSource = source;
				}

				playedAnyClip = true;

				// One-shot sounds automatically return to the pool.
				// Looping sounds must be stopped manually using the returned AudioSource.
				if (sfx.Loop == false) {
					StartCoroutine(ReturnWhenFinished(source));
				}
			}

			if (playedAnyClip) {
				ApplyCooldown(sfx);
			}

			return firstSource;
		}

		// Applies all settings from the SFXDefinition to the selected AudioSource
		// Ensures reused AudioSources are fully reconfigured every time they are played
		private void ConfigureSource(AudioSource source, SFXDefinition sfx, AudioClip clip, float volume, float pitch, float spatialBlend) {
			source.playOnAwake = false;
			source.clip = clip;
			source.outputAudioMixerGroup = sfx.OutputMixerGroup;

			source.volume = volume;
			source.pitch = pitch;
			source.spatialBlend = spatialBlend;

			source.minDistance = sfx.MinDistance;
			source.maxDistance = sfx.MaxDistance;
			source.rolloffMode = sfx.RolloffMode;

			source.loop = sfx.Loop;
			source.priority = sfx.Priority;
			source.ignoreListenerPause = sfx.IgnoreListenerPause;

			// Settings these defaults to 0.0 avoids any wierd positional pitch effects
			source.dopplerLevel = 0.0f;
			source.spread = 0.0f;
		}

		// Returns true if the given SFXDefinition is still on cooldown
		private bool IsOnCooldown(SFXDefinition sfx) {
			if (sfx.Cooldown <= 0.0f) {
				return false;
			}

			int id = sfx.GetInstanceID();

			if (cooldownEndTimes.TryGetValue(id, out float endTime) == false) {
				return false;
			}

			return Time.unscaledTime < endTime;
		}

		// Applies the cooldown for a sound after it successfully starts playing
		private void ApplyCooldown(SFXDefinition sfx) {
			if (sfx.Cooldown <= 0.0f) {
				return;
			}

			int id = sfx.GetInstanceID();
			cooldownEndTimes[id] = Time.unscaledTime + sfx.Cooldown;
		}

		// Gets an AudioSource from the pool
		// If none are available, the manager creates a new one as long as maxPoolSize has not been reached
		private AudioSource GetSource() {
			if (availableSources.Count > 0) {
				int lastIndex = availableSources.Count - 1;
				AudioSource source = availableSources[lastIndex];
				availableSources.RemoveAt(lastIndex);
				return source;
			}

			int totalSources = availableSources.Count + activeSources.Count;

			if (totalSources >= maxPoolSize) {
				return null;
			}

			return CreateNewSource();
		}

		// Creates a new pooled AudioSource GameObject
		// Called during prewarm and also at runtime if the pool needs to expand
		private AudioSource CreateNewSource() {
			GameObject sourceObject = new GameObject("Pooled SFX Source");
			sourceObject.transform.SetParent(poolRoot != null ? poolRoot : transform);
			sourceObject.transform.localPosition = Vector3.zero;

			AudioSource source = sourceObject.AddComponent<AudioSource>();

			// Default values
			// These are overwritten when the source is configured for a specific SFX
			source.playOnAwake = false;
			source.loop = false;
			source.spatialBlend = 1.0f;
			source.dopplerLevel = 0.0f;

			return source;
		}

		// Waits until a non-looping AudioSource has finished playing, then returns it to the pool
		// Looping sounds are not handled here because they need to be stopped manually
		private IEnumerator ReturnWhenFinished(AudioSource source) {
			// Wait one frame so AudioSource.isPlaying has a chance to update after source.Play()
			yield return null;

			while (source != null) {
				// Do not return paused sources to the pool
				// A paused source is not 'playing', but it should still resume later
				if (isPaused == false && source.loop == false && source.isPlaying == false) {
					break;
				}

				yield return null;
			}

			if (source != null) {
				ReturnSourceToPool(source);
			}
		}

		// Stops one active source and returns it to the pool
		// If the source is not managed by this SFXManager, nothing happens
		private void StopInternal(AudioSource source) {
			if (activeSources.Contains(source) == false) {
				return;
			}

			ReturnSourceToPool(source);
		}

		// Stops all active sources and returns them to the pool
		// The reason a copied array is used here is because ReturnSourceToPool modifies the activeSources collection
		private void StopAllInternal() {
			AudioSource[] sources = new AudioSource[activeSources.Count];
			activeSources.CopyTo(sources);

			for (int i = 0; i < sources.Length; i++) {
				ReturnSourceToPool(sources[i]);
			}
		}

		// Pauses or unpauses every currently active AudioSource
		private void SetPausedInternal(bool paused) {
			isPaused = paused;

			foreach (AudioSource source in activeSources) {
				if (source == null) {
					continue;
				}

				if (paused) {
					source.Pause();
				}
				else {
					source.UnPause();
				}
			}
		}

		// Resets an AudioSource and places it back into the available pool
		private void ReturnSourceToPool(AudioSource source) {
			if (source == null) {
				return;
			}

			source.Stop();

			activeSources.Remove(source);

			// Clear clip/mixer/loop settings from the previous sound
			source.clip = null;
			source.outputAudioMixerGroup = null;
			source.loop = false;

			// Reset common values so reused sources start from a predictable state
			source.volume = 1.0f;
			source.pitch = 1.0f;
			source.spatialBlend = 1.0f;

			source.transform.SetParent(poolRoot != null ? poolRoot : transform);
			source.transform.localPosition = Vector3.zero;
			source.gameObject.SetActive(false);

			if (availableSources.Contains(source) == false) {
				availableSources.Add(source);
			}
		}

#if UNITY_EDITOR
		private void OnValidate() {
			initialPoolSize = Mathf.Max(0, initialPoolSize);
			maxPoolSize = Mathf.Max(1, maxPoolSize);

			if (initialPoolSize > maxPoolSize) {
				initialPoolSize = maxPoolSize;
			}

			globalSFXVolume = Mathf.Clamp01(globalSFXVolume);
		}
#endif
	}
}