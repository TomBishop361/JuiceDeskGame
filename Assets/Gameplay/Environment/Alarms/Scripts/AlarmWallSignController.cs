using System;
using UnityEngine;

// Controls a wall-mounted sci-fi alarm sign
// Designed for flat alarm panels to be placed above doors or on wall
// It pulses emissive materials and optional red glow lights when the level alarm is active
[DisallowMultipleComponent]
public sealed class AlarmWallSignController : MonoBehaviour {
	[Serializable]
	private sealed class EmissiveMaterialSlot {
		[Tooltip("Renderer containing the emissive material, usually the alarm sign mesh renderer.")]
		public Renderer renderer;

		[Tooltip("Material index on the renderer to pulse. Use 0 if the model only has one material.")]
		public int materialIndex = 0;

		[Tooltip("Emission colour used while the alarm is active.")]
		public Color activeEmissionColor = Color.red;

		[Tooltip("Emission multiplier at peak brightness.")]
		public float emissionMultiplier = 5.0f;
	}

	[Header("References")]
	[Tooltip("Central alarm controller used as the source for level/encounter alarm behaviour.")]
	[SerializeField] private LevelAlarmController alarmController;
	[Tooltip("Emissive material slots to pulse. Add the LED circle material and warning triangle material here.")]
	[SerializeField] private EmissiveMaterialSlot[] emissiveSlots;
	[Tooltip("Lights used to cast red glow from the alarm sign onto the wall, door, or corridor.")]
	[SerializeField] private Light[] glowLights;

	[Header("Flash")]
	[Tooltip("Base flashes per second while the alarm is active.")]
	[SerializeField] private float flashSpeed = 3.5f;
	[Tooltip("How much faster the sign flashes when alarm intensity reaches maximum.")]
	[SerializeField] private float criticalFlashMultiplier = 2.0f;
	[Tooltip("Minimum pulse brightness while active. Higher values make the sign never fully turn off between flashes.")]
	[SerializeField][Range(0.0f, 1.0f)] private float minimumPulse = 0.15f;

	[Header("Fade")]
	[Tooltip("Seconds used to fade the sign in when the alarm starts.")]
	[SerializeField] private float fadeInDuration = 0.25f;
	[Tooltip("Seconds used to fade the sign out when the alarm stops.")]
	[SerializeField] private float fadeOutDuration = 0.5f;

	[Header("Light Settings")]
	[Tooltip("Peak intensity for all assigned glow lights.")]
	[SerializeField] private float glowLightIntensity = 6.0f;
	[Tooltip("If true, glow lights are disabled once the alarm has fully faded out.")]
	[SerializeField] private bool disableLightsWhenInactive = true;

	[Header("Shader")]
	[Tooltip("Emission colour property used by the sign material.")]
	[SerializeField] private string emissionColorProperty = "_EmissionColor";

	//private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
	private MaterialPropertyBlock propertyBlock/* = new MaterialPropertyBlock()*/;

	private bool alarmActive;
	private float fadeWeight;
	private float flashTimer;

	private void Reset() {
		AutoWireReferences();
	}

	private void Awake() {
		AutoWireReferences();
		propertyBlock = new MaterialPropertyBlock();
		EnableEmissionKeywords();
		// Force the lights into their inactive visual state when the scene starts
		ApplyVisuals(0.0f);
	}

	private void OnEnable() {
		SubscribeToAlarm();
		SyncWithAlarmState();
	}

	private void OnDisable() {
		UnsubscribeFromAlarm();
	}

	private void Update() {
		// Fade weight smoothly moves between 0 and 1
		// 0 = fully inactive, 1 = fully active
		float targetFade = alarmActive ? 1.0f : 0.0f;
		float fadeDuration = alarmActive ? fadeInDuration : fadeOutDuration;
		float fadeRate = fadeDuration <= 0.0f ? float.PositiveInfinity : 1.0f / fadeDuration;

		fadeWeight = Mathf.MoveTowards(fadeWeight, targetFade, fadeRate * Time.deltaTime);

		// Alarm intensity comes from LevelAlarmController
		// Higher intensity makes the sign flash faster near the end of the encounter
		float alarmIntensity = GetAlarmIntensity();
		float speedMultiplier = Mathf.Lerp(1.0f, criticalFlashMultiplier, alarmIntensity);

		flashTimer += Time.deltaTime * Mathf.Max(0.01f, flashSpeed) * speedMultiplier;

		// Creates a smooth 0-1 pulse using a sine wave
		float pulse = 0.5f + 0.5f * Mathf.Sin(flashTimer * Mathf.PI * 2.0f);

		// Prevents the sign from going completely dark between flashes unless minimumPulse is 0
		pulse = Mathf.Lerp(minimumPulse, 1.0f, pulse);

		ApplyVisuals(pulse);
	}

	private void AutoWireReferences() {
		if (alarmController == null) {
			alarmController = FindFirstObjectByType<LevelAlarmController>();
		}

		if (glowLights == null || glowLights.Length == 0) {
			// Finds all child lights on the alarm sign prefab
			glowLights = GetComponentsInChildren<Light>();
		}
	}

	private void SubscribeToAlarm() {
		if (alarmController == null) {
			return;
		}

		alarmController.AlarmStarted -= HandleAlarmStarted;
		alarmController.AlarmStopped -= HandleAlarmStopped;

		alarmController.AlarmStarted += HandleAlarmStarted;
		alarmController.AlarmStopped += HandleAlarmStopped;
	}

	private void UnsubscribeFromAlarm() {
		if (alarmController == null) {
			return;
		}

		alarmController.AlarmStarted -= HandleAlarmStarted;
		alarmController.AlarmStopped -= HandleAlarmStopped;
	}

	private void SyncWithAlarmState() {
		// Ensures the sign visually looks correct if it is enabled while the alarm is already active
		alarmActive = alarmController != null && alarmController.IsAlarmActive;
		fadeWeight = alarmActive ? 1.0f : 0.0f;
	}

	private void HandleAlarmStarted() {
		// Avoids instantly forcing full brightness
		// Update() fades the sign in using fadeInDuration
		alarmActive = true;
	}

	private void HandleAlarmStopped() {
		// Avoids instantly turning the lights off
		// Update() fades the sign out using fadeOutDuration
		alarmActive = false;
	}

	private float GetAlarmIntensity() {
		if (alarmController == null || alarmController.IsAlarmActive == false) {
			return 0.0f;
		}

		return alarmController.AlarmIntensity;
	}

	private void EnableEmissionKeywords() {
		if (emissiveSlots == null) {
			return;
		}

		for (int i = 0; i < emissiveSlots.Length; i++) {
			EmissiveMaterialSlot slot = emissiveSlots[i];

			if (slot == null || slot.renderer == null) {
				continue;
			}

			Material[] materials = slot.renderer.materials;

			if (materials == null || materials.Length == 0) {
				continue;
			}

			int index = Mathf.Clamp(slot.materialIndex, 0, materials.Length - 1);
			Material material = materials[index];

			if (material != null) {
				material.EnableKeyword("_EMISSION");
			}
		}
	}

	private void ApplyVisuals(float pulse) {
		float alarmIntensity = GetAlarmIntensity();

		// finalWeight combines:
		// - pulse: flashing brightness
		// - fadeWeight: smooth alarm on/off transition
		float finalWeight = pulse * fadeWeight;

		ApplyEmission(finalWeight, alarmIntensity);
		ApplyGlowLights(finalWeight, alarmIntensity);
	}

	private void ApplyEmission(float finalWeight, float alarmIntensity) {
		if (emissiveSlots == null) {
			return;
		}

		for (int i = 0; i < emissiveSlots.Length; i++) {
			EmissiveMaterialSlot slot = emissiveSlots[i];

			if (slot == null || slot.renderer == null) {
				continue;
			}

			Color finalEmission = slot.activeEmissionColor * slot.emissionMultiplier * Mathf.Lerp(1.0f, 1.35f, alarmIntensity) * finalWeight;

			slot.renderer.GetPropertyBlock(propertyBlock, slot.materialIndex);
			propertyBlock.SetColor(emissionColorProperty, finalEmission);
			slot.renderer.SetPropertyBlock(propertyBlock, slot.materialIndex);
		}
	}

	private void ApplyGlowLights(float finalWeight, float alarmIntensity) {
		if (glowLights == null) {
			return;
		}

		// Alarm intensity slightly boosts brightness as the encounter becomes more urgent
		float urgencyBrightnessMultiplier = Mathf.Lerp(1.0f, 1.35f, alarmIntensity);
		float finalIntensity = glowLightIntensity * urgencyBrightnessMultiplier * finalWeight;

		for (int i = 0; i < glowLights.Length; i++) {
			Light glowLight = glowLights[i];

			if (glowLight == null) {
				continue;
			}

			glowLight.enabled = disableLightsWhenInactive == false || fadeWeight > 0.01f;

			// Force red here so every assigned light behaves consistently even if the prefab colours are different
			glowLight.color = Color.red;
			glowLight.intensity = finalIntensity;
		}
	}

	private void OnValidate() {
		flashSpeed = Mathf.Max(0.01f, flashSpeed);
		criticalFlashMultiplier = Mathf.Max(1.0f, criticalFlashMultiplier);
		fadeInDuration = Mathf.Max(0.0f, fadeInDuration);
		fadeOutDuration = Mathf.Max(0.0f, fadeOutDuration);
		minimumPulse = Mathf.Clamp01(minimumPulse);
		glowLightIntensity = Mathf.Max(0.0f, glowLightIntensity);

		if (emissiveSlots == null) {
			return;
		}

		for (int i = 0; i < emissiveSlots.Length; i++) {
			if (emissiveSlots[i] != null) {
				emissiveSlots[i].materialIndex = Mathf.Max(0, emissiveSlots[i].materialIndex);
				emissiveSlots[i].emissionMultiplier = Mathf.Max(0.0f, emissiveSlots[i].emissionMultiplier);
			}
		}
	}
}