using System;
using UnityEngine;
using UnityEngine.UI;

[Obsolete("LowHealthHUD is obsolete. Use PlayerDamageFeedbackController for low health feedback instead.")]
public class LowHealthHUD : MonoBehaviour {
	[Header("References")]
	[SerializeField] private PlayerSettings playerSettings;
	[SerializeField] private Health health;

	[Header("Overlay")]
	[Range(0f, 1f)] public float maxOverlayAlpha = 0.6f;
	public float smoothSpeed = 5.0f;

	[Header("Pulse")]
	public Image redOverlay;
	public float pulseSpeed = 6f;
	public float pulseStrength = 0.15f;

	private float currentHealth = 10.0f;
	private float maxHealth = 10.0f;

	private Color baseColor;
	private float targetAlpha;
	private float lowHealthThreshold;

	private void Awake() {
		GameObject player = GameObject.FindGameObjectWithTag("Player");
		if (playerSettings == null) {
			playerSettings = player.GetComponent<PlayerSettings>();
		}
		if (health == null) {
			health = player.GetComponent<Health>();
		}
	}

	void Start() {
		//if (redOverlay != null) {
		//	baseColor = redOverlay.color;
		//	baseColor.a = 0f;
		//	redOverlay.color = baseColor;
		//}

		//health.OnHealthChanged += UpdateHealthBar;

		lowHealthThreshold = playerSettings.LowHealthThreshold / 10;

		maxHealth = playerSettings.MaxHealth;
		currentHealth = maxHealth;
	}

	void Update() {
		float healthPercent = (health.CurrentHealth / maxHealth );

		if (healthPercent <= lowHealthThreshold) {
			// Base fade (0 -> max as health drops)
			float t = (lowHealthThreshold - healthPercent) / lowHealthThreshold;
			targetAlpha = t * maxOverlayAlpha;

			// Add pulse
			float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) * 0.5f; // 0–1
			targetAlpha += pulse * pulseStrength;

			//float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
			//targetAlpha += pulse * 0.15f;
			//float t = 1.0f - (healthPercent / lowHealthThreshold);
			//targetAlpha = t * maxOverlayAlpha;
		}
		else {
			//// Base fade (0 -> max as health drops)
			//float t = (lowHealthThreshold - healthPercent) / lowHealthThreshold;
			//targetAlpha = t * maxOverlayAlpha;

			//// Add pulse
			//float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) * 0.5f; // 0–1
			//targetAlpha += pulse * pulseStrength;

			targetAlpha = 0.0f;
		}

		// Clamp so it never exceeds max
		targetAlpha = Mathf.Clamp(targetAlpha, 0f, maxOverlayAlpha);

		// Smooth transition
		Color col = redOverlay.color;
		col.a = Mathf.Lerp(col.a, targetAlpha, Time.deltaTime * smoothSpeed);
		redOverlay.color = col;
	}
}