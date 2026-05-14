using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Coordinates Hunt Mode state + escalating reveal stages + scan pulse timing + HUD markers + silhouette reveal commands
// Enemy counting remains in EnemyTracker whilst this class only reads tracker state and drives the visuals
[DisallowMultipleComponent]
public sealed class HuntModeController : MonoBehaviour {
	[Header("References")]
	[Tooltip("Tracker that owns enemy counts and living enemy references.")]
	[SerializeField] private EnemyTracker enemyTracker;
	[Tooltip("Optional HUD marker manager. This can be left empty to disable HUD pings.")]
	[SerializeField] private HuntHUDMarkerManager hudMarkerManager;
	[Tooltip("Player camera used for visibility and screen projection.")]
	[SerializeField] private Camera playerCamera;
	[Tooltip("Player transform used for distance-based opacity and vertical HUD icons.")]
	[SerializeField] private Transform playerTransform;

	[Header("Mode")]
	[Tooltip("Master toggle for Hunt Mode logic.")]
	[SerializeField] private bool huntModeEnabled = true;
	[Tooltip("If true, arrow + silhouette + final reveal stages use separate enemy-count thresholds.")]
	[SerializeField] private bool useEscalatingRevealStages = true;
	[Tooltip("Enemy count that activates Hunt Mode when escalating stages are disabled.")]
	[SerializeField] private int huntActivationEnemyCount = 5;

	[Header("Escalating Stage Thresholds")]
	[Tooltip("At or below this remaining enemy count, pulse-based HUD direction arrows are enabled.")]
	[SerializeField] private int arrowStageEnemyCount = 5;
	[Tooltip("At or below this remaining enemy count, pulse-based through-wall silhouettes are enabled.")]
	[SerializeField] private int silhouetteStageEnemyCount = 3;
	[Tooltip("At or below this remaining enemy count, permanent final reveal is enabled.")]
	[SerializeField] private int finalRevealEnemyCount = 1;

	[Header("Feature Toggles")]
	[Tooltip("If true, Hunt Mode can show directional HUD markers.")]
	[SerializeField] private bool enableHudMarkers = true;
	[Tooltip("If true, Hunt Mode can reveal silhouette/outline components on enemies.")]
	[SerializeField] private bool enableSilhouetteReveals = true;
	[Tooltip("If true, enemies at or below finalRevealEnemyCount stay revealed instead of using timed pulses.")]
	[SerializeField] private bool permanentRevealFinalEnemy = true;
	[Tooltip("If true, enemies already visible to the player camera do not receive HUD markers or silhouette reveals.")]
	[SerializeField] private bool stopRevealingWhenVisible = true;

	[Header("Scanner Pulse Timing")]
	[Tooltip("Time (in seconds) between scan pulses while Hunt Mode is active.")]
	[SerializeField] private float scanPulseInterval = 5.0f;
	[Tooltip("Time (in seconds) that non-final enemies remain revealed after each scan pulse.")]
	[SerializeField] private float scanRevealDuration = 2.0f;
	[Tooltip("If true, Hunt Mode emits one pulse immediately when it starts instead of waiting for the first interval.")]
	[SerializeField] private bool pulseImmediatelyOnStart = true;

	[Header("Silhouette Distance Opacity")]
	[Tooltip("Enemies at or closer than this distance use maximum silhouette opacity.")]
	[SerializeField] private float minOpacityDistance = 8.0f;
	[Tooltip("Enemies at or farther than this distance use minimum silhouette opacity.")]
	[SerializeField] private float maxOpacityDistance = 80.0f;
	[Tooltip("Silhouette opacity used for far enemies.")]
	[SerializeField] [Range(0.0f, 1.0f)] private float minSilhouetteOpacity = 0.2f;
	[Tooltip("Silhouette opacity used for nearby enemies.")]
	[SerializeField] [Range(0.0f, 1.0f)] private float maxSilhouetteOpacity = 1.0f;

	[Header("Visibility Checks")]
	[Tooltip("If true, visibility requires a clear line from the player camera to enemy bounds.")]
	[SerializeField] private bool useLineOfSightCheck = true;
	[Tooltip("World layers that block camera visibility checks. Include: level geometry. Exclude: player and enemy layers.")]
	[SerializeField] private LayerMask visibilityCheckLayers;
	[Tooltip("Padding added to enemy renderer bounds during visibility checks.")]
	[SerializeField] private float visibilityBoundsPadding = 0.1f;
	[Tooltip("Local camera-space offset used as the LOS ray origin.")]
	[SerializeField] private Vector3 visibilityRayOriginOffset = Vector3.zero;

	[Header("Unity Events")]
	[Tooltip("Invoked when Hunt Mode first becomes active.")]
	[SerializeField] private UnityEvent onHuntModeStarted;
	[Tooltip("Invoked when Hunt Mode becomes inactive.")]
	[SerializeField] private UnityEvent onHuntModeEnded;
	[Tooltip("Invoked at the start of every scan pulse.")]
	[SerializeField] private UnityEvent onScanPulseStarted;
	[Tooltip("Invoked when the timed scan reveal ends.")]
	[SerializeField] private UnityEvent onScanPulseEnded;
	[Tooltip("Invoked once when permanent final reveal starts.")]
	[SerializeField] private UnityEvent onFinalEnemyRevealStarted;

	public event Action HuntModeStarted;
	public event Action HuntModeEnded;
	public event Action ScanPulseStarted;
	public event Action ScanPulseEnded;
	public event Action FinalEnemyRevealStarted;

	// Public read-only state
	public bool IsHuntModeActive => huntModeActive;
	public bool IsScanPulseActive => scanPulseActive;
	public bool IsFinalRevealActive => finalRevealActive;

	// Reused buffers
	// These avoid allocations during gameplay
	private readonly List<EnemyCombat> aliveEnemiesBuffer = new List<EnemyCombat>(32);
	private readonly List<EnemyHuntTarget> pulseSilhouetteTargets = new List<EnemyHuntTarget>(32);
	private readonly List<EnemyHuntTarget> finalSilhouetteTargets = new List<EnemyHuntTarget>(4);

	private bool huntModeActive;
	private bool scanPulseActive;
	private bool finalRevealActive;
	private float nextScanPulseTime = Mathf.Infinity;
	private float scanPulseEndTime = -Mathf.Infinity;

	private void Reset() {
		ResolveReferences();
	}

	private void Awake() {
		ResolveReferences();
		ConfigureHudManager();
	}

	private void OnEnable() {
		SubscribeToTracker();
		EvaluateHuntModeState();
	}

	private void Start() {
		ResolveReferences();
		ConfigureHudManager();
		EvaluateHuntModeState();
	}

	private void Update() {
		if (huntModeEnabled == false || enemyTracker == null) {
			StopHuntModeIfNeeded();
			return;
		}

		// Check whether the current remaining enemy count should start or stop Hunt Mode
		EvaluateHuntModeState();

		if (huntModeActive == false) {
			return;
		}

		// Keep HUD manager synced with camera/player/layer settings
		ConfigureHudManager();

		// Final reveal overrides normal pulse behaviour
		if (ShouldUseFinalReveal()) {
			StartFinalRevealIfNeeded();
			UpdateFinalReveal();
			return;
		}

		// If we are no longer in final reveal range then clean it up
		StopFinalRevealIfNeeded();

		// Normal Hunt Mode pulse behaviour
		UpdateScanPulse();
	}

	private void OnDisable() {
		UnsubscribeFromTracker();

		// Clean up visuals so disabling the controller does not leave markers/outlines stuck on
		EndScanPulseIfNeeded();
		StopFinalRevealIfNeeded();
		StopHuntModeIfNeeded();
	}

	private void ResolveReferences() {
		if (enemyTracker == null) {
			enemyTracker = FindFirstObjectByType<EnemyTracker>();
		}

		if (hudMarkerManager == null) {
			hudMarkerManager = FindFirstObjectByType<HuntHUDMarkerManager>();
		}

		if (playerCamera == null) {
			playerCamera = Camera.main;
		}

		if (playerTransform == null) {
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null) {
				playerTransform = player.transform;
			}
		}
	}

	private void SubscribeToTracker() {
		if (enemyTracker == null) {
			return;
		}

		// Remove before adding to avoid double subscription if OnEnable() runs again
		enemyTracker.OnRemainingEnemiesChanged -= HandleRemainingEnemiesChanged;
		enemyTracker.OnEnemyRegistered -= HandleEnemyReferenceChanged;
		enemyTracker.OnEnemyUnregistered -= HandleEnemyReferenceChanged;

		enemyTracker.OnRemainingEnemiesChanged += HandleRemainingEnemiesChanged;
		enemyTracker.OnEnemyRegistered += HandleEnemyReferenceChanged;
		enemyTracker.OnEnemyUnregistered += HandleEnemyReferenceChanged;
	}

	private void UnsubscribeFromTracker() {
		if (enemyTracker == null) {
			return;
		}

		enemyTracker.OnRemainingEnemiesChanged -= HandleRemainingEnemiesChanged;
		enemyTracker.OnEnemyRegistered -= HandleEnemyReferenceChanged;
		enemyTracker.OnEnemyUnregistered -= HandleEnemyReferenceChanged;
	}

	private void HandleRemainingEnemiesChanged(int remainingEnemies) {
		// EnemyTracker tells us the count changed, so re-check Hunt Mode state
		EvaluateHuntModeState();
	}

	private void HandleEnemyReferenceChanged(EnemyCombat enemy) {
		// If final reveal is active, the permanent target list should stay accurate
		if (finalRevealActive) {
			RefreshFinalRevealTargets();
		}
	}

	private void ConfigureHudManager() {
		if (hudMarkerManager == null) {
			return;
		}

		hudMarkerManager.ConfigureRuntimeContext(
			playerCamera,
			playerTransform,
			useLineOfSightCheck,
			visibilityCheckLayers,
			visibilityBoundsPadding,
			visibilityRayOriginOffset
		);
	}

	private void EvaluateHuntModeState() {
		if (enemyTracker == null) {
			StopHuntModeIfNeeded();
			return;
		}

		int remainingEnemies = enemyTracker.RemainingEnemies;

		// Hunt Mode becomes active once remaining enemies are above 0 and at or below the configured activation threshold
		bool shouldBeActive = huntModeEnabled && remainingEnemies > 0 && remainingEnemies <= GetHuntActivationThreshold();

		if (shouldBeActive && huntModeActive == false) {
			StartHuntMode();
		}
		else if (shouldBeActive == false && huntModeActive) {
			StopHuntModeIfNeeded();
		}
	}

	private int GetHuntActivationThreshold() {
		// If escalation is disabled, use a single threshold
		if (useEscalatingRevealStages == false) {
			return Mathf.Max(0, huntActivationEnemyCount);
		}

		// If escalation is enabled, Hunt Mode starts at the highest enabled stage threshold
		int threshold = 0;
		if (enableHudMarkers) {
			threshold = Mathf.Max(threshold, arrowStageEnemyCount);
		}

		if (enableSilhouetteReveals) {
			threshold = Mathf.Max(threshold, silhouetteStageEnemyCount);
		}

		if (permanentRevealFinalEnemy) {
			threshold = Mathf.Max(threshold, finalRevealEnemyCount);
		}

		return threshold;
	}

	private void StartHuntMode() {
		huntModeActive = true;

		// Either pulse immediately or wait one interval
		nextScanPulseTime = pulseImmediatelyOnStart ? Time.time : Time.time + scanPulseInterval;

		onHuntModeStarted?.Invoke();
		HuntModeStarted?.Invoke();
	}

	private void StopHuntModeIfNeeded() {
		if (huntModeActive == false) {
			return;
		}

		EndScanPulseIfNeeded();
		StopFinalRevealIfNeeded();
		hudMarkerManager?.HideAllMarkers();
		huntModeActive = false;
		nextScanPulseTime = Mathf.Infinity;
		onHuntModeEnded?.Invoke();
		HuntModeEnded?.Invoke();
	}

	private void UpdateScanPulse() {
		// If a pulse is currently active, keep updating silhouette opacity/visibility
		if (scanPulseActive) {
			if (ShouldUseSilhouettePulse()) {
				UpdateSilhouetteTargets(pulseSilhouetteTargets);
			}

			// End the pulse after the configured reveal duration
			if (Time.time >= scanPulseEndTime) {
				EndScanPulseIfNeeded();
			}
		}

		// Start the next pulse when the interval timer is reached
		if (ShouldRunScanPulse() && Time.time >= nextScanPulseTime) {
			BeginScanPulse();
		}
	}

	private bool ShouldRunScanPulse() {
		return ShouldUseHudPulse() || ShouldUseSilhouettePulse();
	}

	private bool ShouldUseHudPulse() {
		if (enableHudMarkers == false || hudMarkerManager == null) {
			return false;
		}

		// Without escalation, HUD pulse is allowed whenever Hunt Mode is active
		if (useEscalatingRevealStages == false) {
			return huntModeActive;
		}

		// With escalation, HUD arrows start at or below arrowStageEnemyCount
		return enemyTracker != null && enemyTracker.RemainingEnemies <= arrowStageEnemyCount;
	}

	private bool ShouldUseSilhouettePulse() {
		if (enableSilhouetteReveals == false) {
			return false;
		}

		// Without escalation, silhouette pulse is allowed whenever Hunt Mode is active
		if (useEscalatingRevealStages == false) {
			return huntModeActive;
		}

		// With escalation, silhouettes start at or below silhouetteStageEnemyCount
		return enemyTracker != null && enemyTracker.RemainingEnemies <= silhouetteStageEnemyCount;
	}

	private bool ShouldUseFinalReveal() {
		return permanentRevealFinalEnemy && enemyTracker != null && enemyTracker.RemainingEnemies > 0 && enemyTracker.RemainingEnemies <= finalRevealEnemyCount;
	}

	private void BeginScanPulse() {
		// Make sure any old pulse is fully cleaned before starting a new one
		EndScanPulseIfNeeded();

		scanPulseActive = true;
		scanPulseEndTime = Time.time + scanRevealDuration;
		nextScanPulseTime = Time.time + scanPulseInterval;

		// Pull current living enemies from EnemyTracker
		FillAliveEnemyBuffer();

		// HUD markers are pulse-based unless this is final reveal mode
		if (ShouldUseHudPulse()) {
			hudMarkerManager.ShowPulseMarkers(aliveEnemiesBuffer, scanRevealDuration);
		}

		// Silhouette reveal is also pulse-based unless this is final reveal mode
		if (ShouldUseSilhouettePulse()) {
			BuildSilhouetteTargetList(aliveEnemiesBuffer, pulseSilhouetteTargets);
			UpdateSilhouetteTargets(pulseSilhouetteTargets);
		}

		onScanPulseStarted?.Invoke();
		ScanPulseStarted?.Invoke();
	}

	private void EndScanPulseIfNeeded() {
		if (scanPulseActive == false) {
			return;
		}

		// Hide silhouettes that were enabled by this pulse
		HideSilhouetteTargets(pulseSilhouetteTargets);
		pulseSilhouetteTargets.Clear();

		// Tell HUD markers to fade out
		hudMarkerManager?.HidePulseMarkers();

		scanPulseActive = false;
		scanPulseEndTime = -Mathf.Infinity;

		onScanPulseEnded?.Invoke();
		ScanPulseEnded?.Invoke();
	}

	private void StartFinalRevealIfNeeded() {
		if (finalRevealActive) {
			return;
		}

		// Final reveal replaces normal pulse behaviour
		EndScanPulseIfNeeded();

		finalRevealActive = true;
		RefreshFinalRevealTargets();

		onFinalEnemyRevealStarted?.Invoke();
		FinalEnemyRevealStarted?.Invoke();
	}

	private void StopFinalRevealIfNeeded() {
		if (finalRevealActive == false) {
			return;
		}

		hudMarkerManager?.HidePermanentMarkers();

		HideSilhouetteTargets(finalSilhouetteTargets);
		finalSilhouetteTargets.Clear();

		finalRevealActive = false;
	}

	private void UpdateFinalReveal() {
		// Rebuild list so death/despawn/pool changes do not leave redudent references
		RefreshFinalRevealTargets();

		// Keep opacity and visible-camera functionality updated
		UpdateSilhouetteTargets(finalSilhouetteTargets);
	}

	private void RefreshFinalRevealTargets() {
		FillAliveEnemyBuffer();

		// Permanent HUD markers for final reveal
		if (enableHudMarkers && hudMarkerManager != null) {
			hudMarkerManager.ShowPermanentMarkers(aliveEnemiesBuffer);
		}
		else {
			hudMarkerManager?.HidePermanentMarkers();
		}

		// Rebuild permanent silhouette target list
		HideSilhouetteTargets(finalSilhouetteTargets);
		finalSilhouetteTargets.Clear();

		if (enableSilhouetteReveals) {
			BuildSilhouetteTargetList(aliveEnemiesBuffer, finalSilhouetteTargets);
		}
	}

	private void FillAliveEnemyBuffer() {
		aliveEnemiesBuffer.Clear();

		if (enemyTracker == null) {
			return;
		}

		// EnemyTracker fills our existing list to avoid allocations
		enemyTracker.FillAliveEnemies(aliveEnemiesBuffer);
	}

	private void BuildSilhouetteTargetList(IReadOnlyList<EnemyCombat> enemies, List<EnemyHuntTarget> results) {
		results.Clear();

		if (enemies == null) {
			return;
		}

		for (int i = 0; i < enemies.Count; i++) {
			EnemyCombat enemy = enemies[i];
			if (enemy == null || enemy.HasReportedDeath) {
				continue;
			}

			EnemyHuntTarget huntTarget = enemy.HuntTarget;
			if (huntTarget == null || huntTarget.IsAlive == false || results.Contains(huntTarget)) {
				continue;
			}

			results.Add(huntTarget);
		}
	}

	private void UpdateSilhouetteTargets(List<EnemyHuntTarget> targets) {
		if (targets == null || enableSilhouetteReveals == false) {
			return;
		}

		for (int i = targets.Count - 1; i >= 0; i--) {
			EnemyHuntTarget target = targets[i];
			if (target == null || target.IsAlive == false) {
				targets.RemoveAt(i);
				continue;
			}

			// Visibility rule:
			// if the player can already see the enemy, do not apply through-wall reveal
			bool targetVisible = stopRevealingWhenVisible && EnemyVisibilityUtility.IsVisibleToCamera(
				playerCamera,
				target,
				useLineOfSightCheck,
				visibilityCheckLayers,
				visibilityBoundsPadding,
				visibilityRayOriginOffset
			);

			if (targetVisible) {
				target.SetRevealVisible(false, 0.0f);
				continue;
			}

			// Far enemies get weaker silhouette opacity
			// Nearby enemies get stronger silhouette opacity
			float distance = GetDistanceToPlayer(target.TargetPosition);
			float opacity = CalculateSilhouetteOpacity(distance);

			target.SetRevealVisible(true, opacity);
		}
	}

	private void HideSilhouetteTargets(List<EnemyHuntTarget> targets) {
		if (targets == null) {
			return;
		}

		for (int i = 0; i < targets.Count; i++) {
			EnemyHuntTarget target = targets[i];
			if (target != null) {
				target.SetRevealVisible(false, 0.0f);
			}
		}
	}

	private float GetDistanceToPlayer(Vector3 worldPosition) {
		if (playerTransform != null) {
			return Vector3.Distance(playerTransform.position, worldPosition);
		}

		if (playerCamera != null) {
			return Vector3.Distance(playerCamera.transform.position, worldPosition);
		}

		// Fallback if no player/camera exists
		return maxOpacityDistance;
	}

	private float CalculateSilhouetteOpacity(float distance) {
		float nearDistance = Mathf.Max(0.01f, minOpacityDistance);
		float farDistance = Mathf.Max(nearDistance + 0.01f, maxOpacityDistance);

		// closeFactor is:
		// 1 when enemy is near
		// 0 when enemy is far
		float closeFactor = Mathf.InverseLerp(farDistance, nearDistance, distance);

		return Mathf.Lerp(minSilhouetteOpacity, maxSilhouetteOpacity, closeFactor);
	}

	private void OnValidate() {
		huntActivationEnemyCount = Mathf.Max(0, huntActivationEnemyCount);
		arrowStageEnemyCount = Mathf.Max(0, arrowStageEnemyCount);
		silhouetteStageEnemyCount = Mathf.Max(0, silhouetteStageEnemyCount);
		finalRevealEnemyCount = Mathf.Max(1, finalRevealEnemyCount);

		scanPulseInterval = Mathf.Max(0.05f, scanPulseInterval);
		scanRevealDuration = Mathf.Max(0.05f, scanRevealDuration);

		minOpacityDistance = Mathf.Max(0.01f, minOpacityDistance);
		maxOpacityDistance = Mathf.Max(minOpacityDistance + 0.01f, maxOpacityDistance);

		minSilhouetteOpacity = Mathf.Clamp01(minSilhouetteOpacity);
		maxSilhouetteOpacity = Mathf.Clamp01(maxSilhouetteOpacity);

		visibilityBoundsPadding = Mathf.Max(0.0f, visibilityBoundsPadding);
	}
}