using UnityEngine;
using UnityEngine.UI;

// Screen-space HUD health bar for the player
// Handles the main fill amount, delayed damage trail, hit flash, damage pulse, and critical-health colour pulse for the player health bar
public class PlayerHealthBarUI : MonoBehaviour {
	[Header("References")]
	[Tooltip("The player's Health component that drives this HUD bar.")]
	[SerializeField] private Health health;
	[Tooltip("The main filled Image used as the visible current health amount.")]
	[SerializeField] private Image fillImage;
	[Tooltip("A second filled Image placed behind the main fill. Lags behind when the player takes damage.")]
	[SerializeField] private Image delayedDamageImage;
	[Tooltip("RectTransform to scale when damage is taken.")]
	[SerializeField] private RectTransform pulseTarget;

	[Header("Health Colours")]
	[Tooltip("Fill colour when the player is above the warning threshold.")]
	[SerializeField] private Color fullHealthColor = new Color(0.3f, 1.0f, 0.53f, 1.0f);
	[Tooltip("Fill colour used between warning and critical health.")]
	[SerializeField] private Color warningHealthColor = new Color(1.0f, 0.70f, 0.23f, 1.0f);
	[Tooltip("Fill colour used at critical health.")]
	[SerializeField] private Color criticalHealthColor = new Color(1.0f, 0.18f, 0.18f, 1.0f);
	[Tooltip("Health percent where the bar starts shifting from full colour to warning colour.")]
	[SerializeField][Range(0.0f, 1.0f)] private float warningHealthPercent = 0.55f;
	[Tooltip("Health percent where the bar reaches the critical colour and starts pulsing.")]
	[SerializeField][Range(0.0f, 1.0f)] private float criticalHealthPercent = 0.25f;

	[Header("Smooth Fill")]
	[Tooltip("Smooths the visible bar towards the real health value instead of snapping instantly.")]
	[SerializeField] private bool smoothFill = true;
	[Tooltip("How quickly the visible health bar catches up to the real health value.")]
	[SerializeField] private float fillLerpSpeed = 12.0f;

	[Header("Delayed Damage Bar")]
	[Tooltip("If enabled and Delayed Damage Image is assigned, the old health amount remains behind the main bar briefly before draining.")]
	[SerializeField] private bool useDelayedDamageBar = true;
	[Tooltip("How long the delayed damage bar waits before it starts draining down to the current health value.")]
	[SerializeField] private float delayedDamageHoldTime = 0.18f;
	[Tooltip("How quickly the delayed damage bar drains after the hold time.")]
	[SerializeField] private float delayedDamageDrainSpeed = 0.85f;
	[Tooltip("Colour for the delayed damage bar behind the main fill.")]
	[SerializeField] private Color delayedDamageColor = new Color(1.0f, 0.12f, 0.08f, 0.55f);

	[Header("Damage Hit Reaction")]
	[Tooltip("Flashes the health bar when damage is taken.")]
	[SerializeField] private bool flashOnDamage = true;
	[Tooltip("Colour flashed over the current health colour when damage is taken.")]
	[SerializeField] private Color damageFlashColor = Color.white;
	[Tooltip("How long the damage flash lasts.")]
	[SerializeField] private float damageFlashDuration = 0.08f;
	[Tooltip("How strongly the flash colour is blended over the normal health colour.")]
	[SerializeField][Range(0.0f, 1.0f)] private float damageFlashStrength = 0.75f;
	[Tooltip("Briefly scales the health bar when damage is taken.")]
	[SerializeField] private bool pulseOnDamage = true;
	[Tooltip("Maximum scale reached during the damage pulse.")]
	[SerializeField] private float damagePulseScale = 1.045f;
	[Tooltip("How long the scale pulse lasts.")]
	[SerializeField] private float damagePulseDuration = 0.12f;

	[Header("Critical Health Pulse")]
	[Tooltip("Pulses the bar colour while the player is at or below Critical Health Percent.")]
	[SerializeField] private bool pulseWhenCritical = true;
	[Tooltip("Colour blended into the bar during the critical health pulse.")]
	[SerializeField] private Color criticalPulseColor = new Color(1.0f, 0.0f, 0.0f, 1.0f);
	[Tooltip("How fast the critical health colour pulses.")]
	[SerializeField] private float criticalPulseSpeed = 6.0f;
	[Tooltip("How strongly the critical pulse colour affects the fill colour.")]
	[SerializeField][Range(0.0f, 1.0f)] private float criticalPulseStrength = 0.35f;

	private float targetFillAmount = 1.0f;
	private float currentHealthPercent = 1.0f;
	private float delayedDamageTimer;
	private float flashTimer;
	private float pulseTimer;
	private Vector3 defaultPulseScale = Vector3.one;
	private bool hasReceivedHealthValue;

	private void Awake() {
		if (fillImage == null) {
			TryGetComponent(out fillImage);
		}

		// If no custom pulse target is assigned then use a fallback by pulsing the fill image itself
		if (pulseTarget == null && fillImage != null) {
			pulseTarget = fillImage.rectTransform;
		}

		// Store the starting scale so damage pulses can always return to the correct size
		if (pulseTarget != null) {
			defaultPulseScale = pulseTarget.localScale;
		}
	}

	// Subscribe to the Health event and sync the UI with the player's current health
	private void OnEnable() {
		if (health != null) {
			health.OnHealthChanged += UpdateHealthBar;
		}

		ApplyDelayedDamageColour();
		RefreshFromCurrentHealth();
	}

	// Unsubscribe from Health events and restore the pulse target scale when the UI is disabled
	private void OnDisable() {
		if (health != null) {
			health.OnHealthChanged -= UpdateHealthBar;
		}

		ResetPulseScale();
	}

	// Updates smooth HUD effects every frame using unscaled time so the UI still behaves during slow motion
	private void Update() {
		float deltaTime = Time.unscaledDeltaTime;

		UpdateMainFill(deltaTime);
		UpdateDelayedDamageFill(deltaTime);
		UpdateDamagePulse(deltaTime);
		UpdateFillColour(deltaTime);
	}

	// Reads the current value from the Health component and applies it instantly
	private void RefreshFromCurrentHealth() {
		if (health == null) {
			SetFillAmount(1.0f, true);
			return;
		}

		UpdateHealthBar(health.CurrentHealth, health.MaxHealth, true);
	}

	// Event callback used by Health.OnHealthChanged
	private void UpdateHealthBar(float currentHealth, float maxHealth) {
		UpdateHealthBar(currentHealth, maxHealth, false);
	}

	// Converts raw health values into a 0-1 fill amount and triggers damage/heal reactions
	private void UpdateHealthBar(float currentHealth, float maxHealth, bool forceInstant) {
		float healthPercent = maxHealth > 0.0f ? currentHealth / maxHealth : 0.0f;
		healthPercent = Mathf.Clamp01(healthPercent);

		// Compare against the previous target value so we only play reactions on actual damage
		bool tookDamage = hasReceivedHealthValue && healthPercent < targetFillAmount - 0.001f;
		bool healed = hasReceivedHealthValue && healthPercent > targetFillAmount + 0.001f;

		if (tookDamage && forceInstant == false) {
			PlayDamageReaction();
			StartDelayedDamageBar();
		}
		else if (healed || forceInstant) {
			// Healing should not leave the delayed damage trail behind
			SnapDelayedDamageBar(healthPercent);
		}

		hasReceivedHealthValue = true;
		currentHealthPercent = healthPercent;
		SetFillAmount(healthPercent, forceInstant || smoothFill == false);
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

	// Smoothly moves the visible health fill towards the latest health value
	private void UpdateMainFill(float deltaTime) {
		if (fillImage == null || smoothFill == false) {
			return;
		}

		// Smooth only the visible HUD fill
		// The actual Health component value changes immediately
		fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFillAmount, fillLerpSpeed * deltaTime);
	}

	// Holds the delayed damage image at the previous health value after the player takes damage
	private void StartDelayedDamageBar() {
		if (useDelayedDamageBar == false || delayedDamageImage == null) {
			return;
		}

		// Keep the delayed bar at the previous visible value, then drain it after a configured amount of time
		float previousVisibleFill = fillImage != null ? fillImage.fillAmount : targetFillAmount;
		delayedDamageImage.fillAmount = Mathf.Max(delayedDamageImage.fillAmount, previousVisibleFill);
		delayedDamageTimer = delayedDamageHoldTime;
	}

	// Drains the delayed damage image down towards the real health value after its hold timer ends
	private void UpdateDelayedDamageFill(float deltaTime) {
		if (useDelayedDamageBar == false || delayedDamageImage == null) {
			return;
		}

		if (delayedDamageTimer > 0.0f) {
			delayedDamageTimer -= deltaTime;
			return;
		}

		delayedDamageImage.fillAmount = Mathf.MoveTowards(delayedDamageImage.fillAmount, targetFillAmount, delayedDamageDrainSpeed * deltaTime);
	}

	// Instantly matches the delayed damage image to the current health value
	private void SnapDelayedDamageBar(float fillAmount) {
		if (delayedDamageImage == null) {
			return;
		}

		delayedDamageImage.fillAmount = Mathf.Clamp01(fillAmount);
		delayedDamageTimer = 0.0f;
	}

	// Applies the configured colour to the delayed damage image
	private void ApplyDelayedDamageColour() {
		if (delayedDamageImage == null) {
			return;
		}

		delayedDamageImage.color = delayedDamageColor;
	}

	// Starts the short visual reactions that happen when the player takes damage
	private void PlayDamageReaction() {
		if (flashOnDamage) {
			flashTimer = damageFlashDuration;
		}

		if (pulseOnDamage && pulseTarget != null) {
			pulseTimer = damagePulseDuration;
		}
	}

	// Scales the pulse target up and back down using a sine curve
	private void UpdateDamagePulse(float deltaTime) {
		if (pulseTarget == null || pulseOnDamage == false) {
			return;
		}

		if (pulseTimer <= 0.0f) {
			ResetPulseScale();
			return;
		}

		// Progress goes from 0 to 1 during the pulse lifetime
		float progress = 1.0f - Mathf.Clamp01(pulseTimer / Mathf.Max(0.001f, damagePulseDuration));

		// Sine creates a quick pop out and smooth return as apose to a linear scale snap
		float pulse = Mathf.Sin(progress * Mathf.PI);
		float scale = Mathf.Lerp(1.0f, damagePulseScale, pulse);

		pulseTarget.localScale = defaultPulseScale * scale;
		pulseTimer -= deltaTime;

		if (pulseTimer <= 0.0f) {
			ResetPulseScale();
		}
	}

	// Restores the pulse target to its original scale
	private void ResetPulseScale() {
		if (pulseTarget != null) {
			pulseTarget.localScale = defaultPulseScale;
		}
	}

	// Calculates and applies the health bar colour, including critical pulse and damage flash blending
	private void UpdateFillColour(float deltaTime) {
		if (fillImage == null) {
			return;
		}

		Color finalColour = EvaluateHealthColour(currentHealthPercent);

		// At critical health, add a subtle pulse so the player notices danger
		if (pulseWhenCritical && currentHealthPercent <= criticalHealthPercent && currentHealthPercent > 0.0f) {
			float pulse = (Mathf.Sin(Time.unscaledTime * criticalPulseSpeed) + 1.0f) * 0.5f;
			finalColour = Color.Lerp(finalColour, criticalPulseColor, pulse * criticalPulseStrength);
		}

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

		float warning = Mathf.Clamp01(warningHealthPercent);
		float critical = Mathf.Clamp01(criticalHealthPercent);

		// Prevent inverted thresholds from producing wierd colour blending
		if (critical > warning) {
			float temp = critical;
			critical = warning;
			warning = temp;
		}

		if (healthPercent <= critical) {
			// Blend from critical to warning as the player rises out of critical health
			return criticalHealthColor;
		}

		if (healthPercent <= warning) {
			float t = Mathf.InverseLerp(critical, warning, healthPercent);
			return Color.Lerp(criticalHealthColor, warningHealthColor, t);
		}

		// Blend from warning to full colour above the warning threshold
		float fullT = Mathf.InverseLerp(warning, 1.0f, healthPercent);
		return Color.Lerp(warningHealthColor, fullHealthColor, fullT);
	}

	private void OnValidate() {
		warningHealthPercent = Mathf.Clamp01(warningHealthPercent);
		criticalHealthPercent = Mathf.Clamp01(criticalHealthPercent);
		fillLerpSpeed = Mathf.Max(0.0f, fillLerpSpeed);
		delayedDamageHoldTime = Mathf.Max(0.0f, delayedDamageHoldTime);
		delayedDamageDrainSpeed = Mathf.Max(0.0f, delayedDamageDrainSpeed);
		damageFlashDuration = Mathf.Max(0.0f, damageFlashDuration);
		damagePulseDuration = Mathf.Max(0.0f, damagePulseDuration);
		damagePulseScale = Mathf.Max(1.0f, damagePulseScale);
		criticalPulseSpeed = Mathf.Max(0.0f, criticalPulseSpeed);
	}
}