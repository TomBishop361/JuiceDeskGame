using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Controls the player's screen-space damage feedback
// Currently handles two seperate effects:
// 1. A quick flash when the player takes damage
// 2. A pulsing vignette when the player is at low health
[DisallowMultipleComponent]
public sealed class PlayerDamageFeedbackUI : MonoBehaviour {
	[Header("Damage Flash Feedback")]
	[Tooltip("CanvasGroup controlling the damage flash overlay.")]
	[SerializeField] private CanvasGroup damageFlashGroup;
	[Tooltip("Image used for the damage flash.")]
	[SerializeField] private Image damageFlashImage;
	[Tooltip("Flash colour for melee hits.")]
	[SerializeField] private Color meleeFlashColor = new Color(1.0f, 0.12f, 0.08f, 1.0f);
	[Tooltip("Flash colour for ranged or projectile hits.")]
	[SerializeField] private Color rangedFlashColor = new Color(0.25f, 0.75f, 1.0f, 1.0f);
	[Tooltip("Flash colour for heavy hits.")]
	[SerializeField] private Color heavyFlashColor = new Color(1.0f, 0.45f, 0.08f, 1.0f);
	[Tooltip("Maximum alpha for light damage flashes.")]
	[SerializeField] private float lightFlashAlpha = 0.25f;
	[Tooltip("Maximum alpha for heavy damage flashes.")]
	[SerializeField] private float heavyFlashAlpha = 0.65f;
	[Tooltip("How quickly the flash appears.")]
	[SerializeField] private float flashInTime = 0.035f;
	[Tooltip("How long the flash stays at max alpha.")]
	[SerializeField] private float flashHoldTime = 0.025f;
	[Tooltip("How quickly the flash fades away.")]
	[SerializeField] private float flashOutTime = 0.18f;

	[Header("Low Health Feedback")]
	[Tooltip("CanvasGroup controlling the low-health vignette.")]
	[SerializeField] private CanvasGroup lowHealthGroup;
	[Tooltip("Optional image used for the low-health vignette.")]
	[SerializeField] private Image lowHealthImage;
	[Tooltip("Low-health vignette colour.")]
	[SerializeField] private Color lowHealthColor = new Color(1.0f, 0.05f, 0.03f, 1.0f);
	[Tooltip("Minimum alpha reached by the low-health pulse.")]
	[SerializeField] private float lowHealthMinAlpha = 0.12f;
	[Tooltip("Maximum alpha reached by the low-health pulse.")]
	[SerializeField] private float lowHealthMaxAlpha = 0.38f;
	[Tooltip("Pulse speed for the low-health warning.")]
	[SerializeField] private float lowHealthPulseSpeed = 3.0f;
	[Tooltip("Fade speed used when entering or leaving low-health state.")]
	[SerializeField] private float lowHealthFadeSpeed = 5.0f;

	// Stores the currently running damage flash coroutine so it can be restarted cleanly
	private Coroutine flashRoutine;

	// True while the player is at or below the low-health threshold
	private bool lowHealthVisible;

	// Controls how intense the low-health pulse should be
	// Lower player health gives this a higher value
	private float lowHealthTargetWeight;

	private void Awake() {
		if (damageFlashGroup != null) {
			damageFlashGroup.alpha = 0.0f;
			damageFlashGroup.blocksRaycasts = false;
			damageFlashGroup.interactable = false;
		}

		if (lowHealthGroup != null) {
			lowHealthGroup.alpha = 0.0f;
			lowHealthGroup.blocksRaycasts = false;
			lowHealthGroup.interactable = false;
		}

		if (lowHealthImage != null) {
			// Apply configured low-health colour once at startup
			lowHealthImage.color = lowHealthColor;
		}
	}

	// Updates the low-health pulse every frame
	private void Update() {
		TickLowHealthWarning();
	}

	// Smoothly fades and pulses the low-health vignette
	// Runs every frame but only becomes visible when lowHealthVisible is true
	private void TickLowHealthWarning() {
		if (lowHealthGroup == null) {
			return;
		}

		// If low-health is inactive and the vignette is already hidden then stop updating
		if (lowHealthVisible == false && lowHealthGroup.alpha <= 0.001f) {
			lowHealthGroup.alpha = 0.0f;
			return;
		}

		float desiredAlpha = 0.0f;
		if (lowHealthVisible) {
			// Creates a smooth 0-1 pulse using unscaled time so the effect still works during slow motion
			float pulse = (Mathf.Sin(Time.unscaledTime * lowHealthPulseSpeed) + 1.0f) * 0.5f;

			// Makes the minimum pulse alpha stronger as health gets lower
			float minAlpha = Mathf.Lerp(lowHealthMinAlpha, lowHealthMaxAlpha * 0.5f, lowHealthTargetWeight);

			// Final alpha moves between the minimum pulse alpha and max alpha
			desiredAlpha = Mathf.Lerp(minAlpha, lowHealthMaxAlpha, pulse);
		}

		// Smoothly move toward the desired alpha instead of snapping on/off
		lowHealthGroup.alpha = Mathf.MoveTowards(lowHealthGroup.alpha, desiredAlpha, lowHealthFadeSpeed * Time.unscaledDeltaTime);
	}

	// Plays a quick damage flash
	// Strength is 0-1 where: 0 = light hit | 1 = strong hit
	public void PlayDamageFlash(float strength, DamageType damageType, bool heavyHit) {
		if (damageFlashGroup == null) {
			return;
		}

		if (damageFlashImage != null) {
			// Pick a different colour depending on melee, ranged, or heavy damage
			damageFlashImage.color = GetFlashColor(damageType, heavyHit);
		}

		// Scale flash alpha based on damage strength
		float targetAlpha = Mathf.Lerp(lightFlashAlpha, heavyFlashAlpha, Mathf.Clamp01(strength));

		if (heavyHit) {
			// Heavy hits are always visible, even if their damage strength is low
			targetAlpha = Mathf.Max(targetAlpha, heavyFlashAlpha * 0.85f);
		}

		if (flashRoutine != null) {
			// Restart the flash if another hit happens before the previous flash finished
			StopCoroutine(flashRoutine);
		}

		flashRoutine = StartCoroutine(FlashRoutine(targetAlpha));
	}

	// Chooses the correct flash colour for the incoming damage type
	private Color GetFlashColor(DamageType damageType, bool heavyHit) {
		if (heavyHit) {
			return heavyFlashColor;
		}

		return damageType == DamageType.Melee ? meleeFlashColor : rangedFlashColor;
	}

	// Handles the full flash sequence which includes:
	// Fade in -> briefly hold -> then fade out
	private IEnumerator FlashRoutine(float targetAlpha) {
		// Quickly fade from current alpha to peak alpha
		yield return FadeFlash(damageFlashGroup.alpha, targetAlpha, flashInTime);

		if (flashHoldTime > 0.0f) {
			// Keep the flash visible briefly so the hit is readable
			yield return new WaitForSeconds(flashHoldTime);
		}

		// Fade the flash back out
		yield return FadeFlash(damageFlashGroup.alpha, 0.0f, flashOutTime);

		flashRoutine = null;
	}

	// Fades the damage flash CanvasGroup between two alpha values
	private IEnumerator FadeFlash(float from, float to, float duration) {
		if (duration <= 0.0f) {
			// Instant fallback if the duration is set to 0
			damageFlashGroup.alpha = to;
			yield break;
		}

		float elapsed = 0.0f;
		while (elapsed < duration) {
			elapsed += Time.unscaledDeltaTime;

			// Clamp t so the alpha never overshoots
			float t = Mathf.Clamp01(elapsed / duration);

			damageFlashGroup.alpha = Mathf.Lerp(from, to, t);

			yield return null;
		}

		// Force the exact final alpha to avoid any floating point inaccuracies
		damageFlashGroup.alpha = to;
	}

	// Enables/disables the low-health vignette
	// normalizedHealth is 0-1 where: 0 = empty health | 1 = full health
	public void SetLowHealthVisible(bool visible, float normalizedHealth) {
		lowHealthVisible = visible;

		// Lower health makes the low-health pulse a bit stronger
		lowHealthTargetWeight = visible ? 1.0f - Mathf.Clamp01(normalizedHealth) : 0.0f;
	}
}