using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HuntHUDMarker controls one HUD arrow/marker instance
// The manager handles pooling and decides which enemy this marker belongs to
// This script handles the marker's:
// - screen position
// - edge clamping
// - arrow rotation
// - distance text
// - up/down icon
// - fade in/out
// - distance-based intensity
[DisallowMultipleComponent]
public sealed class HuntHUDMarker : MonoBehaviour {
	[Header("References")]
	[Tooltip("Root RectTransform moved around the marker canvas. Defaults to this RectTransform.")]
	[SerializeField] private RectTransform rectTransform;
	[Tooltip("CanvasGroup used to fade the whole marker in and out.")]
	[SerializeField] private CanvasGroup canvasGroup;
	[Tooltip("Arrow root to rotate toward the target direction. Defaults to this RectTransform.")]
	[SerializeField] private RectTransform arrowRoot;
	[Tooltip("Optional image used for additional alpha/color intensity updates.")]
	[SerializeField] private Image arrowImage;
	[Tooltip("Optional TMP text used to show rounded distance (in meters).")]
	[SerializeField] private TMP_Text distanceText;
	[Tooltip("Optional TMP text used to show up or down icon when the enemy is much higher or lower than the player.")]
	[SerializeField] private TMP_Text verticalIconText;

	[Header("Animation")]
	[Tooltip("How quickly the marker fades toward its desired opacity.")]
	[SerializeField] private float fadeSpeed = 8.0f;
	[Tooltip("How quickly the marker scales toward its distance-based size.")]
	[SerializeField] private float scaleLerpSpeed = 8.0f;
	[Tooltip("Scale applied at the weakest marker intensity.")]
	[SerializeField] private float minScale = 0.85f;
	[Tooltip("Scale applied at the strongest marker intensity.")]
	[SerializeField] private float maxScale = 1.15f;
	[Tooltip("Rotation offset for the arrow sprite.")]
	[SerializeField] private float arrowRotationOffset = -90.0f;

	// Enemy this marker currently points to
	private EnemyHuntTarget target;

	// Time when the current pulse marker should stop showing
	// If Time.time is > this, the pulse marker fades out
	private float pulseEndTime = -Mathf.Infinity;

	// True when this marker should stay visible permanently
	// Used for final enemy reveal
	private bool permanent;

	private bool initialized;

	// Current distance-based marker strength
	// Near enemies get stronger intensity | Far enemies get weaker intensity
	private float currentIntensity;

	public EnemyHuntTarget Target => target;

	// True when this pooled marker is currently assigned to an enemy
	public bool IsAssigned => target != null;

	// True when the marker should currently be visible
	// It can be visible because of a timed pulse or because it is permanent
	public bool WantsToShow => target != null && (permanent || Time.time < pulseEndTime);

	// Used by the manager to know when a faded-out marker can return to the pool
	public bool IsFullyHidden => canvasGroup == null || canvasGroup.alpha <= 0.01f;

	private void Reset() {
		AutoWireReferences();
	}

	private void Awake() {
		AutoWireReferences();

		// Markers should start invisible
		SetImmediateAlpha(0.0f);
	}

	private void OnEnable() {
		// Make sure newly enabled pooled markers start hidden the first time they are enabled
		if (initialized == false) {
			SetImmediateAlpha(0.0f);
			initialized = true;
		}
	}

	// Called by HuntHUDMarkerManager when this marker is taken from the pool
	public void Assign(EnemyHuntTarget newTarget) {
		target = newTarget;
		gameObject.SetActive(true);
	}

	// Called by HuntHUDMarkerManager when this marker is returned to the pool
	public void ClearTarget() {
		target = null;
		pulseEndTime = -Mathf.Infinity;
		permanent = false;
	}

	// Starts or extends a pulse reveal
	public void SetPulse(float revealEndTime) {
		// Use the later end time so repeated pulses do not shorten an active reveal
		pulseEndTime = Mathf.Max(pulseEndTime, revealEndTime);
	}

	// Ends the pulse reveal
	// The marker will fade out unless it is permanent
	public void ClearPulse() {
		pulseEndTime = -Mathf.Infinity;
	}

	// Enables/disables permanent reveal
	// Used for final enemy marker behaviour
	public void SetPermanent(bool value) {
		permanent = value;
	}

	// Called every frame by HuntHUDMarkerManager
	// This updates marker visibility + position + rotation + text + opacity + scale
	public void Tick(HuntHUDMarkerManager manager) {
		if (manager == null || rectTransform == null) {
			return;
		}

		float targetAlpha = 0.0f;
		float targetScale = minScale;

		Camera playerCamera = manager.PlayerCamera;

		// Without a camera, we cannot project the enemy into screen space
		if (playerCamera == null) {
			UpdateFade(0.0f);
			UpdateScale(targetScale);
			return;
		}

		if (target != null && target.IsAlive && WantsToShow) {
			// Visibility rule:
			// if the player can already see the enemy, hide the marker
			bool isVisibleToPlayer = EnemyVisibilityUtility.IsVisibleToCamera(
				manager.PlayerCamera,
				target,
				manager.UseLineOfSightCheck,
				manager.VisibilityBlockingLayers,
				manager.VisibilityBoundsPadding,
				manager.VisibilityRayOriginOffset
			);

			if (isVisibleToPlayer == false) {
				Vector3 targetWorldPosition = target.MarkerWorldPosition;

				// Distance is measured from the player if possible
				// If player transform is missing, fall back to the camera position
				Vector3 markerVector = 
					targetWorldPosition - 
					(manager.PlayerTransform != null ? manager.PlayerTransform.position : playerCamera.transform.position);

				float distance = markerVector.magnitude;

				// Convert distance into marker opacity/intensity
				currentIntensity = manager.CalculateMarkerIntensity(distance);

				targetAlpha = currentIntensity;
				targetScale = Mathf.Lerp(minScale, maxScale, currentIntensity);

				UpdateScreenPosition(manager, targetWorldPosition);
				UpdateTexts(manager, targetWorldPosition, distance);
			}
		}

		SetGraphicIntensity(currentIntensity);
		UpdateFade(targetAlpha);
		UpdateScale(targetScale);
	}

	private void AutoWireReferences() {
		if (rectTransform == null) {
			rectTransform = GetComponent<RectTransform>();
		}

		if (canvasGroup == null) {
			canvasGroup = GetComponent<CanvasGroup>();
		}

		// Add a CanvasGroup automatically if the prefab does not already have one
		if (canvasGroup == null) {
			canvasGroup = gameObject.AddComponent<CanvasGroup>();
		}

		if (arrowRoot == null) {
			arrowRoot = rectTransform;
		}

		if (arrowImage == null) {
			arrowImage = GetComponentInChildren<Image>(true);
		}
	}

	private void UpdateScreenPosition(HuntHUDMarkerManager manager, Vector3 targetWorldPosition) {
		Camera playerCamera = manager.PlayerCamera;
		RectTransform markerRoot = manager.MarkerRoot;

		if (playerCamera == null || markerRoot == null) {
			return;
		}

		// Convert target world position into screen pixels
		Vector3 screenPoint = playerCamera.WorldToScreenPoint(targetWorldPosition);

		bool targetBehindCamera = screenPoint.z < 0.0f;

		// If target is behind the camera, flip the screen position
		// This lets the arrow still point in a useful direction from the screen edge
		if (targetBehindCamera) {
			screenPoint.x = Screen.width - screenPoint.x;
			screenPoint.y = Screen.height - screenPoint.y;
		}

		// Convert screen pixel position into local UI position inside markerRoot
		Camera uiCamera = manager.UICamera;
		if (RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRoot, screenPoint, uiCamera, out Vector2 localPoint) == false) {
			return;
		}

		Vector3 viewportPoint = playerCamera.WorldToViewportPoint(targetWorldPosition);

		// Off-screen markers are clamped to the screen edge
		bool offScreen = targetBehindCamera || EnemyVisibilityUtility.IsViewportPointOnScreen(viewportPoint) == false;

		if (offScreen) {
			Vector2 halfSize = markerRoot.rect.size * 0.5f;
			float margin = Mathf.Max(0.0f, manager.ScreenEdgeMargin);

			localPoint.x = Mathf.Clamp(localPoint.x, -halfSize.x + margin, halfSize.x - margin);
			localPoint.y = Mathf.Clamp(localPoint.y, -halfSize.y + margin, halfSize.y - margin);
		}

		rectTransform.anchoredPosition = localPoint;

		// Direction from screen center to marker position
		// This is used to rotate the arrow toward the enemy
		Vector2 directionFromCenter = localPoint.sqrMagnitude > 0.001f ? localPoint.normalized : Vector2.up;

		float angle = Mathf.Atan2(directionFromCenter.y, directionFromCenter.x) * Mathf.Rad2Deg;

		if (arrowRoot != null) {
			arrowRoot.localRotation = Quaternion.Euler(0.0f, 0.0f, angle + arrowRotationOffset);
		}
	}

	private void UpdateTexts(HuntHUDMarkerManager manager, Vector3 targetWorldPosition, float distance) {
		// Optional distance text
		if (distanceText != null) {
			distanceText.gameObject.SetActive(manager.ShowDistanceText);

			if (manager.ShowDistanceText) {
				distanceText.text = $"{Mathf.RoundToInt(distance)}m";
			}
		}

		if (verticalIconText == null) {
			return;
		}

		bool showDirectionalIcon = false;
		string directionalIcon = string.Empty;

		// Optional up/down indicator
		// Shows when the enemy is much higher or lower than the player
		if (manager.ShowVerticalIcon && manager.PlayerTransform != null) {
			float verticalDelta = targetWorldPosition.y - manager.PlayerTransform.position.y;

			if (Mathf.Abs(verticalDelta) >= manager.VerticalIconThreshold) {
				showDirectionalIcon = true;

				directionalIcon = verticalDelta > 0.0f ? "▲" : "▼";
			}
		}

		verticalIconText.gameObject.SetActive(showDirectionalIcon);
		if (showDirectionalIcon) {
			verticalIconText.text = directionalIcon;
		}
	}

	private void UpdateFade(float targetAlpha) {
		if (canvasGroup == null) {
			return;
		}

		// Smoothly fade toward the desired alpha
		canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * Mathf.Max(0.01f, fadeSpeed));

		// HUD marker is visual only
		// It should not block mouse UI interactions
		canvasGroup.blocksRaycasts = false;
		canvasGroup.interactable = false;
	}

	private void UpdateScale(float targetScale) {
		Vector3 desiredScale = Vector3.one * Mathf.Max(0.01f, targetScale);

		// Smoothly scale based on distance/intensity
		transform.localScale = Vector3.Lerp(transform.localScale, desiredScale, Time.deltaTime * Mathf.Max(0.01f, scaleLerpSpeed));
	}

	private void SetGraphicIntensity(float intensity) {
		if (arrowImage == null) {
			return;
		}

		// This gives the arrow image its own opacity/intensity
		// The CanvasGroup still controls the final overall fade
		Color color = arrowImage.color;
		color.a = Mathf.Clamp01(intensity);
		arrowImage.color = color;
	}

	private void SetImmediateAlpha(float alpha) {
		if (canvasGroup != null) {
			canvasGroup.alpha = Mathf.Clamp01(alpha);
		}
	}
}