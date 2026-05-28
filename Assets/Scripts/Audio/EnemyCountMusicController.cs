using System.Collections;
using UnityEngine;

namespace Game.Audio {
	// Controls which music intensity should be playing based on the number of currently active/alive enemies
	// This lives in the level scene because it depends on the level's EnemyTracker and level-specific music assets
	// The MusicManager itself lives in the persistent player scene
	public class EnemyCountMusicController : MonoBehaviour {
		[Header("References")]
		[Tooltip("EnemyTracker used as the source for active enemy count changes.")]
		[SerializeField] private EnemyTracker enemyTracker;

		[Header("Music States")]
		[Tooltip("Played before combat starts, when no enemies are alive, or after combat ends.")]
		[SerializeField] private MusicDefinition relaxMusic;
		[Tooltip("Played when combat has started and there are a small number of active enemies.")]
		[SerializeField] private MusicDefinition middleMusic;
		[Tooltip("Played when combat has started and there are enough active enemies to make combat feel intense.")]
		[SerializeField] private MusicDefinition intenseMusic;

		[Header("Active Enemy Thresholds")]
		[Tooltip("If active alive enemies are greater than or equal to this value, intense music plays.")]
		[SerializeField] private int intenseActiveEnemyThreshold = 6;
		[Tooltip("If active alive enemies are greater than or equal to this value, middle music plays.")]
		[SerializeField] private int middleActiveEnemyThreshold = 1;

		[Header("Fade Settings")]
		[Tooltip("Fade duration when switching between music states.")]
		[SerializeField] private float crossfadeDuration = 2.0f;
		[Tooltip("If true, relax music starts when the controller is ready and no enemies are active.")]
		[SerializeField] private bool playRelaxOnStart = true;

		// Tracks the last music asset requested so enemy count changes do not repeatedly restart the same track
		private MusicDefinition currentRequestedMusic;

		// Prevents planned enemy totals / startup events from triggering combat music before any enemy has actually spawned
		private bool combatStarted;

		// Stored so the startup wait coroutine can be safely stopped when this object disables
		private Coroutine startupRoutine;

		private void OnEnable() {
			if (enemyTracker == null) {
				enemyTracker = FindFirstObjectByType<EnemyTracker>();
			}

			// Subscribe to active enemy events
			// Music intensity is based on currently alive enemies, not total planned enemies
			if (enemyTracker != null) {
				enemyTracker.OnFirstEnemySpawned += HandleFirstEnemySpawned;
				enemyTracker.OnAliveEnemyCountChanged += HandleAliveEnemyCountChanged;
				enemyTracker.OnAllEnemiesDead += HandleAllEnemiesDead;
			}

			startupRoutine = StartCoroutine(StartupRoutine());
		}

		private void OnDisable() {
			if (startupRoutine != null) {
				StopCoroutine(startupRoutine);
				startupRoutine = null;
			}

			// Always unsubscribe from events to avoid duplicate callbacks when scenes reload
			if (enemyTracker != null) {
				enemyTracker.OnFirstEnemySpawned -= HandleFirstEnemySpawned;
				enemyTracker.OnAliveEnemyCountChanged -= HandleAliveEnemyCountChanged;
				enemyTracker.OnAllEnemiesDead -= HandleAllEnemiesDead;
			}
		}

		// Waits for the persistent MusicManager to exist, then starts the correct music for the current enemy state
		// This avoids console warnings when the level scene loads before the persistent player/audio scene is fully ready
		private IEnumerator StartupRoutine() {
			while (MusicManager.Instance == null) {
				yield return null;
			}

			// If this controller enables while enemies are already alive, immediately sync the music to the current active enemy count
			// This helps if an encounter has already started before this controller finishes its startup wait
			if (enemyTracker != null && enemyTracker.AliveNow > 0) {
				combatStarted = true;
				EvaluateMusic(enemyTracker.AliveNow);
				yield break;
			}

			// Default level state before combat begins
			if (playRelaxOnStart) {
				RequestMusic(relaxMusic);
			}
		}

		// Marks combat as started when the first enemy spawns
		private void HandleFirstEnemySpawned(EnemyCombat enemy) {
			combatStarted = true;
		}

		// Updates music whenever the number of currently alive enemies changes
		private void HandleAliveEnemyCountChanged(int activeEnemies) {
			if (combatStarted == false && activeEnemies <= 0) {
				return;
			}

			// If active enemies appear without OnFirstEnemySpawned being received, still count combat as started so music remains correct
			if (activeEnemies > 0) {
				combatStarted = true;
			}

			EvaluateMusic(activeEnemies);
		}

		// Returns to relax music after the encounter is fully cleared
		private void HandleAllEnemiesDead() {
			combatStarted = false;
			RequestMusic(relaxMusic);
		}

		// Chooses the correct music state from the current active enemy count
		private void EvaluateMusic(int activeEnemies) {
			if (activeEnemies >= intenseActiveEnemyThreshold) {
				RequestMusic(intenseMusic);
				return;
			}

			if (activeEnemies >= middleActiveEnemyThreshold) {
				RequestMusic(middleMusic);
				return;
			}

			RequestMusic(relaxMusic);
		}

		// Sends a music change request to the MusicManager, while avoiding any unnecessary duplicate requests
		private void RequestMusic(MusicDefinition music) {
			if (music == null) {
				return;
			}

			// Prevent restarting/crossfading into the same music every time the enemy count changes
			if (currentRequestedMusic == music) {
				return;
			}
			// This can happen sometimes if the level scene enables before the persistent player/audio scene is ready
			if (MusicManager.Instance == null) {
				return;
			}

			currentRequestedMusic = music;
			MusicManager.Play(music, crossfadeDuration);
		}

#if UNITY_EDITOR
		private void OnValidate() {
			intenseActiveEnemyThreshold = Mathf.Max(1, intenseActiveEnemyThreshold);
			middleActiveEnemyThreshold = Mathf.Max(0, middleActiveEnemyThreshold);

			if (middleActiveEnemyThreshold > intenseActiveEnemyThreshold) {
				middleActiveEnemyThreshold = intenseActiveEnemyThreshold;
			}

			crossfadeDuration = Mathf.Max(0.0f, crossfadeDuration);
		}
#endif
	}
}