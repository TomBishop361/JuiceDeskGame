using TMPro;
using UnityEngine;
using UnityEngine.UI;

// World-space health bar UI for enemies
// Handles the main fill amount, delayed damage trail, hit flash, damage pulse, optional fade visibility, camera-facing follow, distance scaling, and health segment dividers
public class EnemyHealthBarUI : MonoBehaviour {
	[Header("References")]
	[Tooltip("The enemy's health component that drives their health bar.")]
	[SerializeField] private Health health;
	[Tooltip("The main filled Image used as the visible current health amount.")]
	[SerializeField] private Image fillImage;
	[Tooltip("A second filled Image placed behind the main fill. Lags behind when the enemy takes damage.")]
	[SerializeField] private Image delayedDamageFillImage;
	[Tooltip("Optional text label for displaying enemy HP.")]
	[SerializeField] private TextMeshProUGUI hpText;
	[Tooltip("Optional CanvasGroup used for smooth fade in/out. Used for hide-when-full behaviour.")]
	[SerializeField] private CanvasGroup canvasGroup;

	[Header("Health Colours")]
	[Tooltip("Fill colour used when the enemy is near full health.")]
	[SerializeField] private Color fullHealthColor = Color.green;
	[Tooltip("Fill colour used when the enemy is at low health.")]
	[SerializeField] private Color lowHealthColor = Color.red;

	[Header("World Space")]
	[Tooltip("If true, this UI follows the enemy.")]
	[SerializeField] private bool followTarget = true;
	[Tooltip("Optional follow point.")]
	[SerializeField] private Transform worldAnchor;
	[Tooltip("World-space offset applied above the enemy or anchor.")]
	[SerializeField] private Vector3 uiOffset = new Vector3(0.0f, 2.0f, 0.0f);
	[Tooltip("If true, the bar faces the main camera.")]
	[SerializeField] private bool faceCamera = true;

	[Header("Smooth Fill")]
	[Tooltip("Smooths the visible bar towards the real health value instead of snapping instantly.")]
	[SerializeField] private bool smoothFill = true;
	[Tooltip("How quickly the visible health bar catches up to the real health value.")]
	[SerializeField, Min(0.01f)] private float fillLerpSpeed = 12.0f;

	[Header("Delayed Damage Bar")]
	[Tooltip("If enabled and Delayed Damage Fill Image is assigned, the old health amount remains behind the main bar briefly before draining.")]
	[SerializeField] private bool useDelayedDamageBar = true;
	[Tooltip("How long the delayed damage bar waits before it starts draining down to the current health value.")]
	[SerializeField, Min(0.0f)] private float delayedDamageHoldTime = 0.12f;
	[Tooltip("Speed used by the delayed damage fill when catching up to the main fill image.")]
	[SerializeField, Min(0.01f)] private float delayedDamageDrainSpeed = 4.5f;

	[Header("Damage Hit Reaction")]
	[Tooltip("Flashes the health bar when damage is taken.")]
	[SerializeField] private bool flashOnDamage = true;
	[Tooltip("Colour flashed over the current health colour when damage is taken.")]
	[SerializeField] private Color damageFlashColor = Color.white;
	[Tooltip("How long the damage flash lasts.")]
	[SerializeField, Min(0.0f)] private float damageFlashDuration = 0.06f;
	[Tooltip("How strongly the flash colour is blended over the normal health colour.")]
	[SerializeField, Range(0.0f, 1.0f)] private float damageFlashStrength = 0.45f;
	[Tooltip("Colour applied to the delayed damage fill behind the main health fill.")]
	[SerializeField] private Color delayedDamageColor = new Color(1.0f, 0.42f, 0.18f, 0.78f);
	[Tooltip("Briefly scales the health bar when damage is taken.")]
	[SerializeField] private bool pulseOnDamage = true;
	[Tooltip("Maximum scale reached during the damage pulse.")]
	[SerializeField, Min(1.0f)] private float damagePulseScale = 1.08f;
	[Tooltip("How long the scale pulse lasts. Use shorter values since this is world-space UI.")]
	[SerializeField, Min(0.01f)] private float damagePulseDuration = 0.10f;

	[Header("Visibility")]
	[Tooltip("If true, full-health enemy bars fade out until the enemy takes damage.")]
	[SerializeField] private bool hideWhenFullHealth = true;
	[Tooltip("How quickly the CanvasGroup fades in or out.")]
	[SerializeField, Min(0.01f)] private float fadeSpeed = 8.0f;
	[Tooltip("If true, the bar fades out when the enemy dies instead of visually resetting to full health.")]
	[SerializeField] private bool hideOnDeath = true;

	[Header("Distance Readability")]
	[Tooltip("If true, the bar becomes slightly smaller at longer distances to reduce screen clutter.")]
	[SerializeField] private bool scaleWithDistance = false;
	[Tooltip("Distance where the health bar keeps its normal scale.")]
	[SerializeField, Min(0.0f)] private float nearDistance = 8.0f;
	[Tooltip("Distance where the health bar reaches its minimum distance scale.")]
	[SerializeField, Min(0.01f)] private float farDistance = 35.0f;
	[Tooltip("Smallest scale multiplier used when the enemy is far away.")]
	[SerializeField, Range(0.25f, 1.0f)] private float farDistanceScale = 0.75f;

	[Header("Segments")]
	[Tooltip("If true, dividers are created when a segment container and divider prefab are assigned.")]
	[SerializeField] private bool useSegments = true;
	[Tooltip("Optional container for segment divider instances.")]
	[SerializeField] private RectTransform segmentsContainer;
	[Tooltip("Optional divider prefab used to visually split the health bar into chunks.")]
	[SerializeField] private GameObject segmentDividerPrefab;
	[Tooltip("Maximum number of segment dividers to create. Will prevent high-health enemies like the Shield enemy from creating a cluttered hp bar.")]
	[SerializeField, Min(0)] private int maxSegmentDividers = 12;

	private Camera mainCamera;
	private Health subscribedHealth;

	private float targetFillAmount = 1.0f;
	private float currentHealthPercent = 1.0f;
	private float delayedDamageTimer;
	private float flashTimer;
	private float pulseTimer;
	private Vector3 defaultPulseScale = Vector3.one;
	private bool hasReceivedHealthValue;
	private bool isDead;

	private const float FullHealthThreshold = 0.999f;

	private void Awake() {
		CacheReferences();
		// Store the starting scale so damage pulses always return to the correct world-space size
		defaultPulseScale = transform.localScale;
	}

	// Subscribe and reset every time the enemy is enabled so pooled enemies display the correct state
	private void OnEnable() {
		CacheReferences();
		SubscribeToHealthEvents();
		ApplyDelayedDamageColour();
		ResetRuntimeToBaseValues();
		SnapToTarget();
	}

	// Unsubscribe when disabled so pooled enemies do not keep receiving events while inactive
	private void OnDisable() {
		UnsubscribeFromHealthEvents();
		ResetPulseScale();
	}

	private void OnDestroy() {
		UnsubscribeFromHealthEvents();
	}

	// Smoothly animate fills, fading, and pulse scale
	private void Update() {
		float deltaTime = Time.deltaTime;
		
		UpdateMainFill(deltaTime);
		UpdateDelayedDamageFill(deltaTime);
		UpdateFillColour(deltaTime);
		UpdateCanvasFade(deltaTime);
		UpdatePulseAndDistanceScale(deltaTime);
	}

	// Maintain world-space bar positions after enemy movement has finished for the frame
	private void LateUpdate() {
		if (followTarget == false) {
			return;
		}

		SnapToTarget();
	}

	private void CacheReferences() {
		if (health == null) {
			health = GetComponentInParent<Health>();
		}

		if (canvasGroup == null) {
			canvasGroup = GetComponent<CanvasGroup>();
		}

		if (mainCamera == null) {
			mainCamera = Camera.main;
		}

		if (canvasGroup != null) {
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;
		}
	}

	// Reads the current value from the Health component and applies it instantly
	private void RefreshFromCurrentHealth() {
		if (health == null) {
			SetFillAmount(1.0f, true);
			SnapDelayedDamageBar(1.0f);
			SetCanvasAlphaImmediate(0.0f);
			return;
		}

		UpdateHealthBar(health.CurrentHealth, health.MaxHealth, true);
	}

	private void SubscribeToHealthEvents() {
		if (health == null || subscribedHealth == health) {
			return;
		}

		UnsubscribeFromHealthEvents();

		health.OnHealthChanged += UpdateHealthBar;
		health.OnDeath += HandleDeath;
		subscribedHealth = health;
	}

	private void UnsubscribeFromHealthEvents() {
		if (subscribedHealth == null) {
			return;
		}

		subscribedHealth.OnHealthChanged -= UpdateHealthBar;
		subscribedHealth.OnDeath -= HandleDeath;
		subscribedHealth = null;
	}

	// Resets all runtime visuals when the enemy spawns or is reused from a pool
	private void ResetRuntimeToBaseValues() {
		isDead = false;
		hasReceivedHealthValue = false;
		delayedDamageTimer = 0.0f;
		flashTimer = 0.0f;
		pulseTimer = 0.0f;

		if (defaultPulseScale == Vector3.zero) {
			defaultPulseScale = transform.localScale;
		}

		if (health != null) {
			BuildSegments(Mathf.CeilToInt(health.MaxHealth));
		}

		RefreshFromCurrentHealth();
		SetCanvasAlphaImmediate(GetTargetAlpha());
		UpdatePulseAndDistanceScale(0.0f);
	}

	// Immediately snaps the bar to the supplied health values without playing damage animation
	private void SetHealthVisualsImmediate(float currentHealth, float maxHealth) {
		float healthPercent = GetSafeHealthPercent(currentHealth, maxHealth);
		SetFillAmount(healthPercent, true);
		SnapDelayedDamageBar(healthPercent);
		UpdateHealthText(currentHealth, maxHealth);
		UpdateFillColour(0.0f);
		SetCanvasAlphaImmediate(GetTargetAlpha());
		UpdatePulseAndDistanceScale(0.0f);
	}

	// Event callback used by Health.OnHealthChanged
	private void UpdateHealthBar(float currentHealth, float maxHealth) {
		UpdateHealthBar(currentHealth, maxHealth, false);
	}

	// Converts raw health values into a 0-1 fill amount and triggers damage/heal reactions
	private void UpdateHealthBar(float currentHealth, float maxHealth, bool forceInstant) {
		float healthPercent = GetSafeHealthPercent(currentHealth, maxHealth);

		// Compare against the previous target value so we only play reactions on actual damage.
		bool tookDamage = hasReceivedHealthValue && healthPercent < targetFillAmount - 0.001f;
		bool healed = hasReceivedHealthValue && healthPercent > targetFillAmount + 0.001f;

		if (tookDamage && forceInstant == false) {
			PlayDamageReaction();
			StartDelayedDamageBar();
		}
		else if (healed || forceInstant) {
			// Healing or forced refresh should not leave the delayed damage trail behind
			SnapDelayedDamageBar(healthPercent);
		}

		hasReceivedHealthValue = true;
		currentHealthPercent = healthPercent;
		SetFillAmount(healthPercent, forceInstant || smoothFill == false);
		UpdateHealthText(currentHealth, maxHealth);
	}

	// Stores the desired fill value and optionally applies it immediately
	private void SetFillAmount(float value, bool instant) {
		targetFillAmount = Mathf.Clamp01(value);

		if (fillImage == null) {
			return;
		}

		if (instant) {
			fillImage.fillAmount = targetFillAmount;
		}
	}

	// Begins the feedback effects that confirm the enemy has taken damage
	private void PlayDamageReaction() {
		if (flashOnDamage) {
			flashTimer = damageFlashDuration;
		}

		if (pulseOnDamage) {
			pulseTimer = damagePulseDuration;
		}
	}

	// Holds the delayed damage image at the previous visible health value after the enemy takes damage
	// This is done prior to draining down to the real health value
	private void StartDelayedDamageBar() {
		if (useDelayedDamageBar == false || delayedDamageFillImage == null) {
			return;
		}

		// Keep the delayed bar at the previous visible value, then drain it after a configured amount of time
		float previousVisibleFill = fillImage != null ? fillImage.fillAmount : targetFillAmount;
		delayedDamageFillImage.fillAmount = Mathf.Max(delayedDamageFillImage.fillAmount, previousVisibleFill);
		delayedDamageTimer = delayedDamageHoldTime;
	}

	// Death event callback
	// The bar hides instead of resetting to full health during the death sequence
	private void HandleDeath() {
		isDead = true;
		currentHealthPercent = 0.0f;
		delayedDamageTimer = 0.0f;
		flashTimer = 0.0f;
		pulseTimer = 0.0f;

		SetFillAmount(0.0f, true);
		SnapDelayedDamageBar(0.0f);

		if (hpText != null && health != null) {
			hpText.text = $"0 / {Mathf.CeilToInt(health.MaxHealth)}";
		}

		UpdateFillColour(0.0f);
		ResetPulseScale();

		if (hideOnDeath) {
			SetCanvasAlphaImmediate(0f);
		}
	}

	// Smoothly moves the visible health fill towards the latest health value
	private void UpdateMainFill(float deltaTime) {
		if (fillImage == null || smoothFill == false) {
			return;
		}

		// Smooth only the visible health bar fill
		// The actual Health component value changes immediately
		fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFillAmount, fillLerpSpeed * deltaTime);
	}

	// Moves the delayed damage fill after a short delay, creating the chip-damage effect
	private void UpdateDelayedDamageFill(float deltaTime) {
		if (useDelayedDamageBar == false || delayedDamageFillImage == null) {
			return;
		}

		if (delayedDamageTimer > 0.0f) {
			delayedDamageTimer -= deltaTime;
			return;
		}

		if (delayedDamageFillImage.fillAmount > targetFillAmount) {
			delayedDamageFillImage.fillAmount = Mathf.MoveTowards(delayedDamageFillImage.fillAmount, targetFillAmount, delayedDamageDrainSpeed * deltaTime);
		}
		else {
			// If the enemy healed (currently we don't have enemies that can heal) or the delayed fill fell behind, snap it to the true target
			delayedDamageFillImage.fillAmount = targetFillAmount;
		}
	}

	// Instantly matches the delayed damage image to the current health value
	private void SnapDelayedDamageBar(float fillAmount) {
		if (delayedDamageFillImage == null) {
			return;
		}

		delayedDamageFillImage.fillAmount = Mathf.Clamp01(fillAmount);
		delayedDamageTimer = 0.0f;
	}

	// Applies the configured colour to the delayed damage image
	private void ApplyDelayedDamageColour() {
		if (delayedDamageFillImage == null) {
			return;
		}

		delayedDamageFillImage.color = delayedDamageColor;
	}

	// Calculates and applies the health bar colour, including the damage flash blend
	private void UpdateFillColour(float deltaTime) {
		if (fillImage == null) {
			return;
		}

		Color finalColour = EvaluateHealthColour(currentHealthPercent);

		// Damage flash temporarily blends over the health colour, then fades out as the timer counts down
		if (flashTimer > 0.0f && damageFlashDuration > 0.0f) {
			float flashBlend = Mathf.Clamp01(flashTimer / damageFlashDuration) * damageFlashStrength;
			finalColour = Color.Lerp(finalColour, damageFlashColor, flashBlend);
			flashTimer -= deltaTime;
		}

		fillImage.color = finalColour;
	}

	// Returns the correct fill colour based on the current health percentage
	private Color EvaluateHealthColour(float healthPercent) {
		healthPercent = Mathf.Clamp01(healthPercent);
		return Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);
	}

	// Fades the bar using CanvasGroup if one is assigned
	private void UpdateCanvasFade(float deltaTime) {
		if (canvasGroup == null) {
			return;
		}

		float targetAlpha = GetTargetAlpha();
		canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * deltaTime);
	}

	// Scales the world-space enemy health bar up and back down using a sine curve
	// This is similar to the player HUD UI pulse behaviour, but also preserves optional distance scaling
	private void UpdatePulseAndDistanceScale(float deltaTime) {
		if (pulseOnDamage == false || pulseTimer <= 0.0f) {
			ResetPulseScale();
			return;
		}

		// Progress goes from 0 to 1 during the pulse lifetime
		float progress = 1.0f - Mathf.Clamp01(pulseTimer / Mathf.Max(0.001f, damagePulseDuration));

		// Sine creates a quick pop out and smooth return as opposed to a linear scale snap
		float pulse = Mathf.Sin(progress * Mathf.PI);
		float pulseScale = Mathf.Lerp(1.0f, damagePulseScale, pulse);

		// Enemy health bars are world-space, so optional distance scaling is included
		float distanceScale = GetDistanceScale();

		transform.localScale = defaultPulseScale * pulseScale * distanceScale;
		pulseTimer -= deltaTime;

		if (pulseTimer <= 0.0f) {
			ResetPulseScale();
		}
	}

	// Restores the world-space UI to its base scale while preserving optional distance scaling
	private void ResetPulseScale() {
		pulseTimer = 0.0f;
		transform.localScale = defaultPulseScale * GetDistanceScale();
	}

	// Keeps the UI above the target and facing the camera if requested
	private void SnapToTarget() {
		Transform target = worldAnchor != null ? worldAnchor : health != null ? health.transform : null;
		if (target == null) {
			return;
		}

		transform.position = target.position + uiOffset;

		if (faceCamera == false) {
			return;
		}

		if (mainCamera == null) {
			mainCamera = Camera.main;
		}

		if (mainCamera != null) {
			transform.forward = mainCamera.transform.forward;
		}
	}

	// Rebuilds optional segment dividers while capping the count to avoid visual clutter for enemy health bar UI
	private void BuildSegments(int maxHealth) {
		ClearSegments();

		if (useSegments == false || segmentsContainer == null || segmentDividerPrefab == null || maxHealth <= 1 || maxSegmentDividers <= 0) {
			return;
		}

		int dividerCount = Mathf.Min(maxHealth - 1, maxSegmentDividers);
		float width = segmentsContainer.rect.width;

		for (int i = 1; i <= dividerCount; i++) {
			GameObject divider = Instantiate(segmentDividerPrefab, segmentsContainer);
			RectTransform rt = divider.GetComponent<RectTransform>();

			if (rt == null) {
				continue;
			}

			float x = width * i / (dividerCount + 1);

			rt.anchorMin = new Vector2(0.0f, 0.0f);
			rt.anchorMax = new Vector2(0.0f, 1.0f);
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = new Vector2(x, 0.0f);
		}
	}

	// Clears old dividers before rebuilding, which is vital for pooled enemies and any enemy prefab changes
	private void ClearSegments() {
		if (segmentsContainer == null) {
			return;
		}

		for (int i = segmentsContainer.childCount - 1; i >= 0; i--) {
			Destroy(segmentsContainer.GetChild(i).gameObject);
		}
	}

	// Updates optional HP text using whole numbers
	private void UpdateHealthText(float currentHealth, float maxHealth) {
		if (hpText == null) {
			return;
		}

		hpText.text = $"{Mathf.Max(0, Mathf.CeilToInt(currentHealth))} / {Mathf.CeilToInt(maxHealth)}";
	}

	// Calculates a 0-1 value even if Health data is temporarily invalid
	private float GetSafeHealthPercent(float currentHealth, float maxHealth) {
		if (maxHealth <= 0.0f) {
			return 0.0f;
		}

		return Mathf.Clamp01(currentHealth / maxHealth);
	}

	// Determines whether the bar should currently be visible
	private float GetTargetAlpha() {
		if (isDead && hideOnDeath) {
			return 0.0f;
		}

		if (hideWhenFullHealth && targetFillAmount >= FullHealthThreshold) {
			return 0.0f;
		}

		return 1.0f;
	}

	// Calculates optional distance scaling from the cached camera reference
	private float GetDistanceScale() {
		if (scaleWithDistance == false) {
			return 1.0f;
		}

		if (mainCamera == null) {
			mainCamera = Camera.main;
		}

		if (mainCamera == null) {
			return 1.0f;
		}

		float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
		float t = Mathf.InverseLerp(nearDistance, farDistance, distance);
		return Mathf.Lerp(1.0f, farDistanceScale, t);
	}

	// Applies alpha instantly for spawn/death resets so pooled enemies do not show redundant fade states
	private void SetCanvasAlphaImmediate(float alpha) {
		if (canvasGroup == null) {
			return;
		}

		canvasGroup.alpha = alpha;
	}

	private void OnValidate() {
		if (health == null) {
			health = GetComponentInParent<Health>();
		}

		if (canvasGroup == null) {
			canvasGroup = GetComponent<CanvasGroup>();
		}

		fillLerpSpeed = Mathf.Max(0.01f, fillLerpSpeed);
		delayedDamageHoldTime = Mathf.Max(0.0f, delayedDamageHoldTime);
		delayedDamageDrainSpeed = Mathf.Max(0.01f, delayedDamageDrainSpeed);
		damageFlashDuration = Mathf.Max(0.0f, damageFlashDuration);
		fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
		damagePulseScale = Mathf.Max(1.0f, damagePulseScale);
		damagePulseDuration = Mathf.Max(0.01f, damagePulseDuration);
		farDistance = Mathf.Max(nearDistance + 0.01f, farDistance);
		maxSegmentDividers = Mathf.Max(0, maxSegmentDividers);

		ApplyDelayedDamageColour();
	}
}