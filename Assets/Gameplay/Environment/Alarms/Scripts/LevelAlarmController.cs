using System;
using UnityEngine;
using UnityEngine.Events;

// Central state machine for level/encounter alarm behaviour
// This controller listens to EnemyTracker and broadcasts alarm state to wall signs,
// lights, audio, UI, lockdown doors, music systems, or any other scene response
[DisallowMultipleComponent]
public sealed class LevelAlarmController : MonoBehaviour {
	[Header("References")]
	[Tooltip("Enemy tracker used as the source for enemy spawn/death counts.")]
	[SerializeField] private EnemyTracker enemyTracker;

	[Header("Behaviour")]
	[Tooltip("If true, the alarm automatically starts when the first enemy is spawned by the tracker.")]
	[SerializeField] private bool startOnFirstEnemySpawn = true;
	[Tooltip("If true, the alarm automatically stops when EnemyTracker reports that all planned enemies are dead.")]
	[SerializeField] private bool stopWhenAllEnemiesDead = true;
	[Tooltip("If true, the alarm checks the EnemyTracker when this component enables. Safety fallback to prevent the alarm missing the first spawn if enemies spawned before the controller subscribed to events.")]
	[SerializeField] private bool syncWithTrackerOnEnable = true;

	[Header("Intensity Escalation")]
	[Tooltip("If true, the alarm becomes more urgent as fewer enemies remain. This affects flashing speed, glow brightness, and siren pitch.")]
	[SerializeField] private bool useEnemyCountEscalation = true;
	[Tooltip("Alarm urgency while most enemies are still alive. Lower values create a calmer early-fight alarm.")]
	[SerializeField][Range(0.0f, 1.0f)] private float normalIntensity = 0.35f;
	[Tooltip("Alarm urgency when the remaining enemy count reaches the high-intensity threshold. Used for stronger flashing and a more urgent siren.")]
	[SerializeField][Range(0.0f, 1.0f)] private float highIntensity = 0.7f;
	[Tooltip("Maximum alarm urgency when the remaining enemy count reaches the critical-intensity threshold. Used for the fastest flashing, brightest glow, and highest siren pitch.")]
	[SerializeField][Range(0.0f, 1.0f)] private float criticalIntensity = 1.0f;
	[Tooltip("When the number of remaining enemies is at or below this value, the alarm changes from normal intensity to high intensity.")]
	[SerializeField] private int highIntensityEnemyCount = 5;
	[Tooltip("When the number of remaining enemies is at or below this value, the alarm changes to critical intensity.")]
	[SerializeField] private int criticalIntensityEnemyCount = 1;

	[Header("Unity Events")]
	[Tooltip("Invoked once when the alarm starts. Example: trigger UI warnings, lockdown doors, music changes, or VFX.")]
	[SerializeField] private UnityEvent onAlarmStarted;
	[Tooltip("Invoked once when the alarm stops. Example: clear UI warnings, unlock doors, or return music to normal.")]
	[SerializeField] private UnityEvent onAlarmStopped;
	[Tooltip("Invoked whenever alarm intensity changes. Value is 0-1.")]
	[SerializeField] private UnityEvent<float> onAlarmIntensityChanged;

	public event Action AlarmStarted;
	public event Action AlarmStopped;
	public event Action<float> AlarmIntensityChanged;
	public bool IsAlarmActive => isAlarmActive;
	public float AlarmIntensity => currentIntensity;
	public EnemyTracker Tracker => enemyTracker;

	private bool isAlarmActive = false;
	private float currentIntensity = 0.0f;

	private void Reset() {
		ResolveReferences();
	}

	private void Awake() {
		ResolveReferences();
	}

	private void OnEnable() {
		ResolveReferences();
		SubscribeToTracker();

		// Safety fallback for cases where enemies spawned before this controller subscribed
		if (syncWithTrackerOnEnable) {
			SyncWithTrackerState();
		}
	}

	private void Start() {
		if (syncWithTrackerOnEnable) {
			SyncWithTrackerState();
		}
	}

	private void OnDisable() {
		UnsubscribeFromTracker();
	}

	// Allows another setup script to assign the tracker at runtime
	public void SetEnemyTracker(EnemyTracker tracker) {
		if (enemyTracker == tracker) {
			return;
		}
		// Unsubscribe from the old tracker before swapping reference
		UnsubscribeFromTracker();

		enemyTracker = tracker;

		// Subscribe to the new tracker and immediately sync to its current state
		SubscribeToTracker();
		SyncWithTrackerState();
	}

	// Manually starts the alarm (can be called repeatedly)
	public void StartAlarm() {
		if (isAlarmActive) {
			// If the alarm is already active, still refresh intensity in case the enemy count changed
			UpdateIntensityFromTracker();
			return;
		}

		isAlarmActive = true;

		// Set intensity before invoking events so listeners receive the correct urgency immediately
		UpdateIntensityFromTracker();

		onAlarmStarted?.Invoke();
		AlarmStarted?.Invoke();
	}

	// Manually stops the alarm (can be called repeatedly)
	public void StopAlarm() {
		if (isAlarmActive == false && currentIntensity <= 0.0f) {
			return;
		}

		isAlarmActive = false;

		// Intensity returns to zero so lights/audio know to fade out or shut down
		SetIntensity(0.0f);

		onAlarmStopped?.Invoke();
		AlarmStopped?.Invoke();
	}

	private void ResolveReferences() {
		if (enemyTracker == null) {
			enemyTracker = FindFirstObjectByType<EnemyTracker>();
		}
	}

	private void SubscribeToTracker() {
		if (enemyTracker == null) {
			return;
		}

		enemyTracker.OnFirstEnemySpawned -= HandleFirstEnemySpawned;
		enemyTracker.OnRemainingEnemiesChanged -= HandleRemainingEnemiesChanged;
		enemyTracker.OnAllEnemiesDead -= HandleAllEnemiesDead;

		enemyTracker.OnFirstEnemySpawned += HandleFirstEnemySpawned;
		enemyTracker.OnRemainingEnemiesChanged += HandleRemainingEnemiesChanged;
		enemyTracker.OnAllEnemiesDead += HandleAllEnemiesDead;
	}

	private void UnsubscribeFromTracker() {
		if (enemyTracker == null) {
			return;
		}

		enemyTracker.OnFirstEnemySpawned -= HandleFirstEnemySpawned;
		enemyTracker.OnRemainingEnemiesChanged -= HandleRemainingEnemiesChanged;
		enemyTracker.OnAllEnemiesDead -= HandleAllEnemiesDead;
	}

	private void HandleFirstEnemySpawned(EnemyCombat enemy) {
		// The first spawned enemy marks the encounter as becoming active
		if (startOnFirstEnemySpawn) {
			StartAlarm();
		}
	}

	private void HandleRemainingEnemiesChanged(int remainingEnemies) {
		// Enemy count changes only matter while the alarm is active
		// This helps drive escalation from normal -> high -> critical
		if (isAlarmActive) {
			UpdateIntensityFromTracker();
		}
	}

	private void HandleAllEnemiesDead() {
		// EnemyTracker only fires this once the whole planned encounter is cleared
		if (stopWhenAllEnemiesDead) {
			StopAlarm();
		}
	}

	private void SyncWithTrackerState() {
		if (enemyTracker == null) {
			return;
		}

		// These checks allow the alarm to recover if it missed the original first-spawn event
		bool enemiesHaveSpawned = enemyTracker.SpawnedSoFar > 0;
		bool enemiesStillRemain = enemyTracker.RemainingEnemies > 0;

		if (startOnFirstEnemySpawn && enemiesHaveSpawned && enemiesStillRemain) {
			StartAlarm();
		}
		else if (stopWhenAllEnemiesDead && enemiesHaveSpawned && enemiesStillRemain == false) {
			StopAlarm();
		}
	}

	private void UpdateIntensityFromTracker() {
		if (isAlarmActive == false) {
			SetIntensity(0.0f);
			return;
		}

		// If escalation is disabled, active alarm responders use full intensity
		if (useEnemyCountEscalation == false || enemyTracker == null) {
			SetIntensity(1.0f);
			return;
		}

		int remainingEnemies = enemyTracker.RemainingEnemies;

		// Critical takes priority over high intensity
		// Example: if critical threshold is 1, the final enemy creates the most urgent alarm state
		if (remainingEnemies <= Mathf.Max(1, criticalIntensityEnemyCount)) {
			SetIntensity(criticalIntensity);
			return;
		}

		// High intensity is used once the encounter is close to being cleared
		if (remainingEnemies <= Mathf.Max(1, highIntensityEnemyCount)) {
			SetIntensity(highIntensity);
			return;
		}

		// Normal intensity is used during the main part of the encounter
		SetIntensity(normalIntensity);
	}

	private void SetIntensity(float value) {
		float clamped = Mathf.Clamp01(value);

		// Avoid repeatedly invoking events when the value has not changed much
		if (Mathf.Approximately(currentIntensity, clamped)) {
			return;
		}

		currentIntensity = clamped;
		onAlarmIntensityChanged?.Invoke(currentIntensity);
		AlarmIntensityChanged?.Invoke(currentIntensity);
	}

	private void OnValidate() {
		normalIntensity = Mathf.Clamp01(normalIntensity);
		highIntensity = Mathf.Clamp01(highIntensity);
		criticalIntensity = Mathf.Clamp01(criticalIntensity);
		highIntensityEnemyCount = Mathf.Max(1, highIntensityEnemyCount);
		criticalIntensityEnemyCount = Mathf.Max(1, criticalIntensityEnemyCount);
	}
}