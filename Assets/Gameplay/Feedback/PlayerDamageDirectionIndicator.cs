using System.Collections;
using UnityEngine;

// Handles the screen-edge damage direction marker shown when the player is hit
// The marker points toward the attacker using either camera-space direction or a player-relative fallback
[DisallowMultipleComponent]
public sealed class PlayerDamageDirectionIndicator : MonoBehaviour {
	[Header("References")]
	[Tooltip("CanvasGroup used to fade the marker in and out.")]
	[SerializeField] private CanvasGroup markerGroup;
	[Tooltip("RectTransform of the arrow/marker visual.")]
	[SerializeField] private RectTransform markerRect;
	[Tooltip("Optional full-screen parent RectTransform used to calculate the screen edge position.")]
	[SerializeField] private RectTransform screenRoot;

	[Header("Placement")]
	[Tooltip("Distance from the screen edge (in UI units).")]
	[SerializeField] private float edgePadding = 90.0f;
	[Tooltip("If true, the marker rotates to point toward the damage source.")]
	[SerializeField] private bool rotateMarker = true;

	[Header("Fade")]
	[Tooltip("Maximum marker alpha for light hits.")]
	[SerializeField] private float lightAlpha = 0.45f;
	[Tooltip("Maximum marker alpha for strong hits.")]
	[SerializeField] private float strongAlpha = 1.0f;
	[Tooltip("How long the marker stays visible before fading.")]
	[SerializeField] private float holdTime = 0.12f;
	[Tooltip("How quickly the marker fades out.")]
	[SerializeField] private float fadeOutTime = 0.35f;

	// Stores the active fade coroutine so repeated hits can restart the fade cleanly
	private Coroutine fadeRoutine;

	private void Awake() {
		if (markerGroup == null) {
			markerGroup = GetComponent<CanvasGroup>();
		}

		if (markerRect == null) {
			markerRect = transform as RectTransform;
		}

		if (screenRoot == null && markerRect != null) {
			screenRoot = markerRect.parent as RectTransform;
		}

		if (markerGroup != null) {
			markerGroup.alpha = 0.0f;
			markerGroup.blocksRaycasts = false;
			markerGroup.interactable = false;
		}
	}

	// Shows the damage marker using an attacker world position
	// This is the entry point used when the damage source has a world-space location
	public void ShowFromWorldPosition(Vector3 attackerWorldPosition, Transform playerRoot, Camera worldCamera, float strength) {
		if (markerRect == null || markerGroup == null) {
			return;
		}

		// Convert the attacker's world position into a 2D screen direction
		Vector2 direction = BuildScreenDirection(attackerWorldPosition, playerRoot, worldCamera);
		ShowDamageMarker(direction, strength);
	}

	// Shows the damage marker using a precomputed 2D screen direction
	// screenDirection should point from the screen centre toward the damage source
	public void ShowDamageMarker(Vector2 screenDirection, float strength) {
		if (markerRect == null || markerGroup == null) {
			return;
		}

		if (screenDirection.sqrMagnitude < 0.0001f) {
			screenDirection = Vector2.up;
		}

		screenDirection.Normalize();

		// Move the damage marker to the correct edge of the screen
		PositionDamageMarker(screenDirection);

		if (rotateMarker) {
			// The marker art is pointing upward by default
			// This rotates it so its 'up' direction faces the incoming damage direction
			float angleFromUp = Mathf.Atan2(screenDirection.x, screenDirection.y) * Mathf.Rad2Deg;
			markerRect.localRotation = Quaternion.Euler(0.0f, 0.0f, -angleFromUp);
		}

		// Strength is expected to be 0-1
		// Light hits use lower alpha, stronger hits become more visible
		markerGroup.alpha = Mathf.Lerp(lightAlpha, strongAlpha, Mathf.Clamp01(strength));

		if (fadeRoutine != null) {
			StopCoroutine(fadeRoutine);
		}

		fadeRoutine = StartCoroutine(FadeRoutine());
	}

	// Converts a world-space attacker position into a 2D direction from screen centre
	// Uses the camera first, then falls back to player-relative direction if needed
	private Vector2 BuildScreenDirection(Vector3 attackerWorldPosition, Transform playerRoot, Camera worldCamera) {
		if (worldCamera != null) {
			Vector3 viewportPoint = worldCamera.WorldToViewportPoint(attackerWorldPosition);
			Vector2 direction = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);

			// If the attacker is behind the camera, flip the marker to the opposite screen edge
			// If we didn't have this, then behind-camera attackers can point in a misleading direction
			if (viewportPoint.z < 0.0f) {
				direction = -direction;
			}

			if (direction.sqrMagnitude > 0.0001f) {
				return direction.normalized;
			}
		}

		if (playerRoot != null) {
			// Fallback: used for cases where the camera is missing or the attacker is too close to screen centre
			// Converts attacker position into player-local space so left/right/front/back still makes sense
			Vector3 localDirection = playerRoot.InverseTransformPoint(attackerWorldPosition);
			Vector2 fallback = new Vector2(localDirection.x, localDirection.z);

			if (fallback.sqrMagnitude > 0.0001f) {
				return fallback.normalized;
			}
		}

		// Final safe fallback
		return Vector2.up;
	}

	// Places the marker on the edge of the screen root in the supplied direction
	// The direction is treated as a ray from the centre of the screen to the UI boundary
	private void PositionDamageMarker(Vector2 direction) {
		if (screenRoot == null) {
			// Fallback: use this placement if no screen root exists
			// This still shows the marker, but it will not perfectly hug the screen edge
			markerRect.anchoredPosition = direction * 300.0f;
			return;
		}

		Rect rect = screenRoot.rect;

		// Calculate the furthest usable UI position while keeping padding from the edge
		float maxX = Mathf.Max(0.0f, rect.width * 0.5f - edgePadding);
		float maxY = Mathf.Max(0.0f, rect.height * 0.5f - edgePadding);

		// Calculate how far we can travel in this direction before hitting the horizontal or vertical edge
		float scaleX = Mathf.Abs(direction.x) > 0.0001f ? maxX / Mathf.Abs(direction.x) : float.PositiveInfinity;
		float scaleY = Mathf.Abs(direction.y) > 0.0001f ? maxY / Mathf.Abs(direction.y) : float.PositiveInfinity;

		// Use the smaller scale so the damage marker stays inside both X and Y bounds
		float scale = Mathf.Min(scaleX, scaleY);

		// Handles perfectly vertical directions where X scale becomes infinity
		if (float.IsInfinity(scale)) {
			scale = maxY;
		}

		markerRect.anchoredPosition = direction * scale;
	}

	// Keeps the marker visible briefly, then fades it out using unscaled time
	// Unscaled time means the indicator still fades correctly during slow motion or hit pause
	private IEnumerator FadeRoutine() {
		if (holdTime > 0.0f) {
			yield return new WaitForSeconds(holdTime);
		}

		float startAlpha = markerGroup.alpha;
		float elapsed = 0.0f;

		while (elapsed < fadeOutTime) {
			elapsed += Time.unscaledDeltaTime;

			float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, fadeOutTime));

			markerGroup.alpha = Mathf.Lerp(startAlpha, 0.0f, t);

			yield return null;
		}

		// Force the final alpha to zero so floating inaccuracies do not keep it visible
		markerGroup.alpha = 0.0f;

		// Clear the coroutine reference so future hits know that there is no fade currently active
		fadeRoutine = null;
	}
}
