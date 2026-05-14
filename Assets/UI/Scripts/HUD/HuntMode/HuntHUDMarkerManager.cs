using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// HuntHUDMarkerManager owns the HUD marker pool
// HuntModeController gives this manager enemy targets
// This manager then:
// - creates/reuses HUD marker arrows
// - assigns markers to enemies
// - updates active markers every frame
// - hides markers when they are no longer needed
[DisallowMultipleComponent]
public sealed class HuntHUDMarkerManager : MonoBehaviour {
	[Header("HUD References")]
	[Tooltip("RectTransform under the HUD canvas that contains hunt marker instances. This can be a full-screen panel stretched to the canvas.")]
	[SerializeField] private RectTransform markerRoot;
	[Tooltip("Prefab with a HuntHUDMarker component + a CanvasGroup + arrow/text children.")]
	[SerializeField] private HuntHUDMarker markerPrefab;
	[Tooltip("Canvas that owns the marker root. If empty, the parent canvas is found automatically.")]
	[SerializeField] private Canvas parentCanvas;
	[Tooltip("Number of marker instances to create on Awake to avoid runtime lag spikes.")]
	[SerializeField] private int initialPoolSize = 8;

	[Header("Marker Display")]
	[Tooltip("Distance from the edge of the marker root (in UI pixels), where off-screen arrows are clamped.")]
	[SerializeField] private float screenEdgeMargin = 72.0f;
	[Tooltip("If true, markers display rounded distance text in meters.")]
	[SerializeField] private bool showDistanceText = true;
	[Tooltip("If true, markers display up/down icons when enemies are much higher or lower than the player.")]
	[SerializeField] private bool showVerticalIcon = true;
	[Tooltip("Minimum vertical world-space difference before an up/down icon appears.")]
	[SerializeField] private float verticalIconThreshold = 4.0f;

	[Header("Distance Intensity")]
	[Tooltip("Enemies at or closer than this distance use maximum marker opacity.")]
	[SerializeField] private float minOpacityDistance = 8.0f;
	[Tooltip("Enemies at or farther than this distance use minimum marker opacity.")]
	[SerializeField] private float maxOpacityDistance = 80.0f;
	[Tooltip("Marker opacity used for far enemies.")]
	[SerializeField] [Range(0.0f, 1.0f)] private float minMarkerOpacity = 0.25f;
	[Tooltip("Marker opacity used for nearby enemies.")]
	[SerializeField][Range(0.0f, 1.0f)] private float maxMarkerOpacity = 1.0f;

	[Header("Runtime Context")]
	[Tooltip("Player camera used to project enemy positions into screen space. Usually assigned by HuntModeController.")]
	[SerializeField] private Camera playerCamera;
	[Tooltip("Player transform used for distance and vertical icon checks. Usually assigned by HuntModeController.")]
	[SerializeField] private Transform playerTransform;
	[Tooltip("If true, a marker is hidden only when the enemy is on-screen and has LOS from the player camera.")]
	[SerializeField] private bool useLineOfSightCheck = true;
	[Tooltip("World layers that block visibility checks. Include: level geometry. Exclude: player and enemy layers.")]
	[SerializeField] private LayerMask visibilityBlockingLayers;
	[Tooltip("Padding added to enemy bounds for visibility checks.")]
	[SerializeField] private float visibilityBoundsPadding = 0.1f;
	[Tooltip("Local camera-space offset used as the LOS ray origin.")]
	[SerializeField] private Vector3 visibilityRayOriginOffset = Vector3.zero;

	// Active marker lookup
	// Key: enemy hunt target
	// Value: HUD marker assigned to that enemy
	private readonly Dictionary<EnemyHuntTarget, HuntHUDMarker> activeMarkers = new Dictionary<EnemyHuntTarget, HuntHUDMarker>(32);

	// Pool of marker instances
	// Markers are reused instead of instantiated/destroyed repeatedly
	private readonly List<HuntHUDMarker> markerPool = new List<HuntHUDMarker>(32);

	// Temporary reusable list used when removing inactive markers
	// This avoids modifying activeMarkers while iterating through it
	private readonly List<EnemyHuntTarget> cleanupBuffer = new List<EnemyHuntTarget>(16);

	// Public read-only accessors used by HuntHUDMarker
	public RectTransform MarkerRoot => markerRoot;
	public Camera PlayerCamera => playerCamera;
	public Transform PlayerTransform => playerTransform;

	// Camera used for converting screen points into UI positions
	public Camera UICamera {
		get {
			if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) {
				return null;
			}

			return parentCanvas.worldCamera != null ? parentCanvas.worldCamera : playerCamera;
		}
	}
	public float ScreenEdgeMargin => screenEdgeMargin;
	public bool ShowDistanceText => showDistanceText;
	public bool ShowVerticalIcon => showVerticalIcon;
	public float VerticalIconThreshold => verticalIconThreshold;
	public bool UseLineOfSightCheck => useLineOfSightCheck;
	public LayerMask VisibilityBlockingLayers => visibilityBlockingLayers;
	public float VisibilityBoundsPadding => visibilityBoundsPadding;
	public Vector3 VisibilityRayOriginOffset => visibilityRayOriginOffset;

	private void Reset() {
		AutoWireReferences();
	}

	private void Awake() {
		AutoWireReferences();

		// Pre-create marker objects so the first Hunt Mode pulse does not cause a lag spike
		PrewarmPool();
	}

	private void LateUpdate() {
		// LateUpdate is useful for HUD because player/enemy movement has usually already updated this frame
		ResolveRuntimeContextIfNeeded();
		UpdateActiveMarkers();
	}

	// Called by HuntModeController
	// Keeps this manager synced with the active player camera + player transform + visibility settings
	public void ConfigureRuntimeContext(
		Camera camera,
		Transform player,
		bool lineOfSightCheck,
		LayerMask blockingLayers,
		float boundsPadding,
		Vector3 rayOriginOffset
	) {
		if (camera != null) {
			playerCamera = camera;
		}

		if (player != null) {
			playerTransform = player;
		}

		useLineOfSightCheck = lineOfSightCheck;
		visibilityBlockingLayers = blockingLayers;
		visibilityBoundsPadding = Mathf.Max(0.0f, boundsPadding);
		visibilityRayOriginOffset = rayOriginOffset;
	}

	// Shows temporary pulse markers for the supplied enemies
	// The markers will fade out after revealDuration
	public void ShowPulseMarkers(IReadOnlyList<EnemyCombat> enemies, float revealDuration) {
		if (enemies == null || markerPrefab == null || markerRoot == null) {
			return;
		}

		float revealEndTime = Time.time + Mathf.Max(0.0f, revealDuration);

		for (int i = 0; i < enemies.Count; i++) {
			EnemyHuntTarget target = GetHuntTarget(enemies[i]);
			if (target == null || target.IsAlive == false) {
				continue;
			}

			HuntHUDMarker marker = GetOrCreateMarker(target);
			marker.SetPulse(revealEndTime);
		}
	}

	// Tells all pulse markers to begin fading out
	// Permanent final-enemy markers are not affected
	public void HidePulseMarkers() {
		foreach (KeyValuePair<EnemyHuntTarget, HuntHUDMarker> pair in activeMarkers) {
			if (pair.Value != null) {
				pair.Value.ClearPulse();
			}
		}
	}

	// Shows permanent markers for the supplied enemies
	// Used for final enemy reveal
	public void ShowPermanentMarkers(IReadOnlyList<EnemyCombat> enemies) {
		ClearPermanentFlags();

		if (enemies == null || markerPrefab == null || markerRoot == null) {
			return;
		}

		for (int i = 0; i < enemies.Count; i++) {
			EnemyHuntTarget target = GetHuntTarget(enemies[i]);
			if (target == null || target.IsAlive == false) {
				continue;
			}

			HuntHUDMarker marker = GetOrCreateMarker(target);
			marker.SetPermanent(true);
		}
	}

	// Removes permanent reveal status from markers
	// The markers will fade out unless they also have an active pulse
	public void HidePermanentMarkers() {
		ClearPermanentFlags();
	}

	// Hides every marker controlled by this manager
	// Used when Hunt Mode ends or is disabled
	public void HideAllMarkers() {
		foreach (KeyValuePair<EnemyHuntTarget, HuntHUDMarker> pair in activeMarkers) {
			if (pair.Value == null) {
				continue;
			}

			pair.Value.ClearPulse();
			pair.Value.SetPermanent(false);
		}
	}

	// Converts distance into marker opacity/intensity
	// Nearby enemies return stronger intensity
	// Far enemies return weaker intensity
	public float CalculateMarkerIntensity(float distance) {
		float nearDistance = Mathf.Max(0.01f, minOpacityDistance);
		float farDistance = Mathf.Max(nearDistance + 0.01f, maxOpacityDistance);

		// closeFactor is:
		// 1 when the enemy is near
		// 0 when the enemy is far
		float closeFactor = Mathf.InverseLerp(farDistance, nearDistance, distance);

		return Mathf.Lerp(minMarkerOpacity, maxMarkerOpacity, closeFactor);
	}

	private void AutoWireReferences() {
		if (markerRoot == null) {
			markerRoot = transform as RectTransform;
		}

		if (parentCanvas == null) {
			parentCanvas = GetComponentInParent<Canvas>();
		}
	}

	private void PrewarmPool() {
		if (markerPrefab == null || markerRoot == null) {
			return;
		}

		int count = Mathf.Max(0, initialPoolSize);
		for (int i = markerPool.Count; i < count; i++) {
			CreateMarkerInstance();
		}
	}

	// Fallback method in case references are not assigned
	private void ResolveRuntimeContextIfNeeded() {
		if (playerCamera == null) {
			playerCamera = Camera.main;
		}

		if (playerTransform == null && playerCamera != null) {
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null) {
				playerTransform = player.transform;
			}
		}
	}

	private void UpdateActiveMarkers() {
		cleanupBuffer.Clear();

		// Instead of removing from activeMarkers in this loop,
		// collect keys into cleanupBuffer and remove them afterward
		foreach (KeyValuePair<EnemyHuntTarget, HuntHUDMarker> pair in activeMarkers) {
			EnemyHuntTarget target = pair.Key;
			HuntHUDMarker marker = pair.Value;

			// Remove markers whose target is gone, dead, or whose marker reference is missing
			if (marker == null || target == null || target.IsAlive == false) {
				cleanupBuffer.Add(target);
				continue;
			}

			// Let the marker update its own position + rotation + distance text + fade
			marker.Tick(this);

			// Once a marker no longer wants to show and has fully faded out, return it to the pool
			if (marker.WantsToShow == false && marker.IsFullyHidden) {
				cleanupBuffer.Add(target);
			}
		}

		for (int i = 0; i < cleanupBuffer.Count; i++) {
			ReleaseMarker(cleanupBuffer[i]);
		}
	}

	private HuntHUDMarker GetOrCreateMarker(EnemyHuntTarget target) {
		// If this target already has a marker, reuse it
		if (activeMarkers.TryGetValue(target, out HuntHUDMarker existingMarker)) {
			return existingMarker;
		}
		// Otherwise take one from the pool
		HuntHUDMarker marker = GetAvailableMarker();

		marker.Assign(target);
		activeMarkers.Add(target, marker);

		return marker;
	}

	private HuntHUDMarker GetAvailableMarker() {
		// Look for an unused marker in the pool
		for (int i = 0; i < markerPool.Count; i++) {
			HuntHUDMarker marker = markerPool[i];

			if (marker != null && marker.IsAssigned == false) {
				return marker;
			}
		}

		// If the pool is empty/full, create another marker
		return CreateMarkerInstance();
	}

	private HuntHUDMarker CreateMarkerInstance() {
		HuntHUDMarker marker = Instantiate(markerPrefab, markerRoot);

		// Start inactive until assigned to an enemy
		marker.gameObject.SetActive(false);

		markerPool.Add(marker);

		return marker;
	}

	private void ReleaseMarker(EnemyHuntTarget target) {
		if (activeMarkers.TryGetValue(target, out HuntHUDMarker marker) == false) {
			return;
		}

		activeMarkers.Remove(target);

		if (marker != null) {
			// Clear assignment so this marker can be reused later
			marker.ClearTarget();
			marker.gameObject.SetActive(false);
		}
	}

	private void ClearPermanentFlags() {
		foreach (KeyValuePair<EnemyHuntTarget, HuntHUDMarker> pair in activeMarkers) {
			if (pair.Value != null) {
				pair.Value.SetPermanent(false);
			}
		}
	}

	private EnemyHuntTarget GetHuntTarget(EnemyCombat enemy) {
		if (enemy == null || enemy.HasReportedDeath) {
			return null;
		}

		return enemy.HuntTarget;
	}

	private void OnValidate() {
		initialPoolSize = Mathf.Max(0, initialPoolSize);
		screenEdgeMargin = Mathf.Max(0.0f, screenEdgeMargin);
		verticalIconThreshold = Mathf.Max(0.0f, verticalIconThreshold);
		minOpacityDistance = Mathf.Max(0.01f, minOpacityDistance);
		maxOpacityDistance = Mathf.Max(minOpacityDistance + 0.01f, maxOpacityDistance);
		minMarkerOpacity = Mathf.Clamp01(minMarkerOpacity);
		maxMarkerOpacity = Mathf.Clamp01(maxMarkerOpacity);
	}
}