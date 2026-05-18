using System.Collections;
using UnityEngine;

// Screen-edge marker to point toward the enemy that damaged the player
[DisallowMultipleComponent]
public sealed class PlayerDamageDirectionIndicator : MonoBehaviour {
	[Header("References")]
	[Tooltip("CanvasGroup used to fade the marker in and out.")]
	[SerializeField] private CanvasGroup markerGroup;
	[Tooltip("RectTransform of the arrow/marker visual. The sprite should point upward by default.")]
	[SerializeField] private RectTransform markerRect;
	[Tooltip("Optional full-screen parent RectTransform. If empty, markerRect's parent is used.")]
	[SerializeField] private RectTransform screenRoot;

	[Header("Placement")]
	[Tooltip("Distance from the screen edge, in UI units.")]
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

	public void ShowFromWorldPosition(Vector3 attackerWorldPosition, Transform playerRoot, Camera worldCamera, float strength) {
		if (markerRect == null || markerGroup == null) {
			return;
		}

		Vector2 direction = BuildScreenDirection(attackerWorldPosition, playerRoot, worldCamera);
		Show(direction, strength);
	}

	public void Show(Vector2 screenDirection, float strength) {
		if (markerRect == null || markerGroup == null) {
			return;
		}

		if (screenDirection.sqrMagnitude < 0.0001f) {
			screenDirection = Vector2.up;
		}

		screenDirection.Normalize();
		PositionMarker(screenDirection);

		if (rotateMarker) {
			float angleFromUp = Mathf.Atan2(screenDirection.x, screenDirection.y) * Mathf.Rad2Deg;
			markerRect.localRotation = Quaternion.Euler(0.0f, 0.0f, -angleFromUp);
		}

		markerGroup.alpha = Mathf.Lerp(lightAlpha, strongAlpha, Mathf.Clamp01(strength));

		if (fadeRoutine != null) {
			StopCoroutine(fadeRoutine);
		}

		fadeRoutine = StartCoroutine(FadeRoutine());
	}

	private Vector2 BuildScreenDirection(Vector3 attackerWorldPosition, Transform playerRoot, Camera worldCamera) {
		if (worldCamera != null) {
			Vector3 viewportPoint = worldCamera.WorldToViewportPoint(attackerWorldPosition);
			Vector2 direction = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);

			// If the attacker is behind the camera, flip the marker to the opposite screen edge.
			if (viewportPoint.z < 0.0f) {
				direction = -direction;
			}

			if (direction.sqrMagnitude > 0.0001f) {
				return direction.normalized;
			}
		}

		if (playerRoot != null) {
			Vector3 localDirection = playerRoot.InverseTransformPoint(attackerWorldPosition);
			Vector2 fallback = new Vector2(localDirection.x, localDirection.z);
			if (fallback.sqrMagnitude > 0.0001f) {
				return fallback.normalized;
			}
		}

		return Vector2.up;
	}

	private void PositionMarker(Vector2 direction) {
		if (screenRoot == null) {
			markerRect.anchoredPosition = direction * 300.0f;
			return;
		}

		Rect rect = screenRoot.rect;
		float maxX = Mathf.Max(0.0f, rect.width * 0.5f - edgePadding);
		float maxY = Mathf.Max(0.0f, rect.height * 0.5f - edgePadding);

		float scaleX = Mathf.Abs(direction.x) > 0.0001f ? maxX / Mathf.Abs(direction.x) : float.PositiveInfinity;
		float scaleY = Mathf.Abs(direction.y) > 0.0001f ? maxY / Mathf.Abs(direction.y) : float.PositiveInfinity;
		float scale = Mathf.Min(scaleX, scaleY);

		if (float.IsInfinity(scale)) {
			scale = maxY;
		}

		markerRect.anchoredPosition = direction * scale;
	}

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

		markerGroup.alpha = 0.0f;
		fadeRoutine = null;
	}
}
