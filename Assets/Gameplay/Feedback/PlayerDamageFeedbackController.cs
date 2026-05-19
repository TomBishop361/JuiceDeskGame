using UnityEngine;

// Plays local feedback to the player based on their health loss
// Listens to Hurbox + Health events
[DisallowMultipleComponent]
public sealed class PlayerDamageFeedbackController : MonoBehaviour {
	[Header("References")]
	[Tooltip("Player Health component.")]
	[SerializeField] private Health health;
	[Tooltip("Player Hurtbox component. Used to identify which Hurtbox events belong to the player.")]
	[SerializeField] private Hurtbox playerHurtbox;
	[Tooltip("UI script that controls the screen flash and low-health vignette.")]
	[SerializeField] private PlayerDamageFeedbackUI feedbackUI;
	[Tooltip("UI indicator that points toward the attacker.")]
	[SerializeField] private PlayerDamageDirectionIndicator directionIndicator;
	[Tooltip("Camera used by the direction indicator.")]
	[SerializeField] private Camera playerCamera;
	//[Tooltip("CinemachineImpulseSource or compatible component. The script calls it safely by reflection, so this stays optional.")]
	//[SerializeField] private MonoBehaviour cameraImpulseSource;
	[Tooltip("Audio source used for local player damage feedback sounds.")]
	[SerializeField] private AudioSource audioSource;

	[Header("Feedback Strength")]
	[Tooltip("Damage amount that produces the smallest visible hit feedback.")]
	[SerializeField] private float lightDamageAmount = 0.5f;
	[Tooltip("Damage amount that produces full-strength feedback.")]
	[SerializeField] private float fullFeedbackDamageAmount = 4.0f;
	[Tooltip("Damage greater than or equal to this value is counted as a heavy hit.")]
	[SerializeField] private float heavyDamageThreshold = 3.0f;
	[Tooltip("Knockback force greater than or equal to this value is counted as a heavy hit regardless of damage.")]
	[SerializeField] private float heavyKnockbackThreshold = 9.0f;

	[Header("Feedback Timing")]
	[Tooltip("How long recent enemy attack data is remembered after the player is hit.")]
	[SerializeField] private float attackDataCacheWindow = 0.08f;
	[Tooltip("Minimum time between normal hit flashes.")]
	[SerializeField] private float normalFeedbackInterval = 0.12f;
	[Tooltip("Minimum time between ranged hit flashes.")]
	[SerializeField] private float rangedFeedbackInterval = 0.18f;
	//[Tooltip("Minimum time between camera impulses for non-heavy hits.")]
	//[SerializeField] private float cameraImpulseInterval = 0.12f;

	//[Header("Camera Impulse")]
	//[Tooltip("Smallest camera impulse force used for light hits.")]
	//[SerializeField] private float lightCameraImpulse = 0.12f;
	//[Tooltip("Strongest camera impulse force used for heavy hits.")]
	//[SerializeField] private float heavyCameraImpulse = 0.45f;

	[Header("Audio")]
	[Tooltip("Clips used when the player is hit by melee attacks.")]
	[SerializeField] private AudioClip[] meleeHitClips;
	[Tooltip("Clips used when the player is hit by ranged attacks.")]
	[SerializeField] private AudioClip[] rangedHitClips;
	[Tooltip("Clips used for heavy hits. Examples: lunge or slam shockwave damage.")]
	[SerializeField] private AudioClip[] heavyHitClips;
	[Tooltip("Random pitch range applied to one-shot hit sounds.")]
	[SerializeField] private Vector2 randomPitchRange = new Vector2(0.94f, 1.06f);
	[Tooltip("Base volume for player damage feedback sounds.")]
	[SerializeField] private float hitVolume = 0.85f;

	// Used to calculate how much health was lost
	private float previousHealth;

	// Cached low-health threshold read from IHealthSettings
	private float lowHealthThreshold;

	// Prevents repeated light/ranged hits from spamming UI and audio feedback
	private float nextFeedbackTime = -Mathf.Infinity;

	//private float nextCameraImpulseTime = -Mathf.Infinity;

	// Cached AttackData from the Hurtbox damage attempt event
	// Health.OnHealthChanged confirms whether the cached attack actually reduced health
	private AttackData cachedAttackData;
	private bool hasCachedAttackData;
	private float cachedAttackDataTime = -Mathf.Infinity;

	private void Reset() {
		AutoWireReferences();
	}

	private void Awake() {
		AutoWireReferences();
		CacheLowHealthThreshold();
	}

	private void OnEnable() {
		if (health == null) {
			return;
		}

		// Store starting health so the next health change can detect real damage
		previousHealth = health.CurrentHealth;

		// Health change confirms actual damage, regen, or death
		health.OnHealthChanged += HandleHealthChanged;

		// Provide the attacker/source data before health changes
		Hurtbox.OnAnyDamageAttempted += HandleAnyDamageAttempted;

		// Immediately sync low-health UI
		UpdateLowHealthVisual(health.CurrentHealth, health.MaxHealth);
	}

	private void OnDisable() {
		if (health != null) {
			health.OnHealthChanged -= HandleHealthChanged;
		}

		Hurtbox.OnAnyDamageAttempted -= HandleAnyDamageAttempted;
	}

	private void AutoWireReferences() {
		if (health == null) {
			health = GetComponent<Health>();
		}

		if (playerHurtbox == null) {
			playerHurtbox = GetComponent<Hurtbox>();
		}

		if (feedbackUI == null) {
			feedbackUI = GetComponentInChildren<PlayerDamageFeedbackUI>(true);
		}

		if (directionIndicator == null) {
			directionIndicator = GetComponentInChildren<PlayerDamageDirectionIndicator>(true);
		}

		if (audioSource == null) {
			audioSource = GetComponent<AudioSource>();
		}
	}

	// Reads the player's low-health value from PlayerSettings through IHealthSettings
	// Fallback: If no value is found, then use 30% of max health
	private void CacheLowHealthThreshold() {
		IHealthSettings healthSettings = GetComponent<IHealthSettings>();
		lowHealthThreshold = healthSettings != null && healthSettings.LowHealthThreshold > 0.0f
			? healthSettings.LowHealthThreshold
			: Mathf.Max(1.0f, health != null ? health.MaxHealth * 0.3f : 1.0f);
	}

	// Caches enemy AttackData before damage is applied
	// This lets HandleHealthChanged know what caused the confirmed health loss
	private void HandleAnyDamageAttempted(Hurtbox hurtbox, AttackData attackData) {
		// Ignore damage events from other enemies
		if (hurtbox == null || hurtbox != playerHurtbox) {
			return;
		}

		// Keep player damage feedback focused only on enemy hits
		if (attackData.AttackerFaction != Faction.Enemy) {
			return;
		}

		// Cache the most recent enemy AttackData for a short time window
		cachedAttackData = attackData;
		hasCachedAttackData = true;
		cachedAttackDataTime = Time.time;
	}

	// Called whenever Health changes
	private void HandleHealthChanged(float current, float max) {
		// Detects whether health went down (positive value = health reduced)
		float damageTaken = previousHealth - current;

		// Update previousHealth immediately so regen does not create false damage later
		previousHealth = current;

		// Update low-health feedback on damage, regen, and respawn
		UpdateLowHealthVisual(current, max);

		// No health loss = no damage feedback
		if (damageTaken <= 0.0f) {
			return;
		}

		// Use cached AttackData if the Hurtbox event happened recently
		// Fallback: use a generic ranged hit
		AttackData attackData = GetRecentAttackData(damageTaken);

		// Heavy hits ignore most feedback timing so they always feel impactful
		bool isHeavyHit = IsHeavyHit(damageTaken, attackData);

		// Converts damage amount into a 0-1 feedback strength value
		float strength = Mathf.InverseLerp(lightDamageAmount, fullFeedbackDamageAmount, damageTaken);
		strength = Mathf.Clamp01(strength);

		// Ranged hits use a longer interval to stop shield enemy miniguns from spamming feedback
		float feedbackInterval = GetFeedbackInterval(attackData, isHeavyHit);

		// Skip non-heavy feedback if it is still on cooldown
		if (isHeavyHit == false && Time.time < nextFeedbackTime) {
			return;
		}

		nextFeedbackTime = Time.time + feedbackInterval;

		// Play visual screen feedback
		feedbackUI?.PlayDamageFlash(strength, attackData.Type, isHeavyHit);

		// Display damage direction indicator
		ShowDirectionIndicator(attackData, strength);
		//PlayCameraImpulse(strength, isHeavyHit, attackData);

		// Play hit sound after visual feedback has been triggered
		PlayHitAudio(attackData.Type, isHeavyHit, strength);
	}

	// Turns the low-health vignette on/off based on the current player health
	private void UpdateLowHealthVisual(float current, float max) {
		// Do not show low-health feedback after death
		bool lowHealth = current > 0.0f && current <= lowHealthThreshold;

		// Normalized health allows the UI to make the damage warning stronger at lower health
		float normalizedHealth = max > 0.0f ? current / max : 0.0f;

		feedbackUI?.SetLowHealthVisible(lowHealth, normalizedHealth);
	}

	// Returns the cached AttackData if it is still recent enough to match this health loss
	// This helps prevent feedback from using old attacker data after regen or respawn events
	private AttackData GetRecentAttackData(float damageTaken) {
		if (hasCachedAttackData && Time.time <= cachedAttackDataTime + attackDataCacheWindow) {
			return cachedAttackData;
		}

		// Fallback in case damage somehow occured without a valid cached AttackData
		return new AttackData { 
			Damage = damageTaken,
			Attacker = null,
			AttackerFaction = Faction.Enemy,
			Type = DamageType.Ranged
		};
	}

	// Decides whether the hit should use heavy feedback
	// Heavy feedback is based on either damage or knockback strength
	private bool IsHeavyHit(float damageTaken, AttackData attackData) {
		return damageTaken >= heavyDamageThreshold || attackData.Knockback.Force >= heavyKnockbackThreshold;
	}

	// Returns the feedback cooldown for the current hit type
	// Heavy hits return 0 so they are never prevented by feedback timing
	private float GetFeedbackInterval(AttackData attackData, bool isHeavyHit) {
		if (isHeavyHit) {
			return 0.0f;
		}

		return attackData.Type == DamageType.Ranged ? rangedFeedbackInterval : normalFeedbackInterval;
	}

	private void ShowDirectionIndicator(AttackData attackData, float strength) {
		if (directionIndicator == null || attackData.Attacker == null) {
			return;
		}

		Camera cam = playerCamera != null ? playerCamera : Camera.main;
		directionIndicator.ShowFromWorldPosition(attackData.Attacker.transform.position, transform, cam, strength);
	}

	//private void PlayCameraImpulse(float strength, bool isHeavyHit, AttackData attackData) {
	//	if (cameraImpulseSource == null) {
	//		return;
	//	}

	//	if (isHeavyHit == false && Time.time < nextCameraImpulseTime) {
	//		return;
	//	}

	//	nextCameraImpulseTime = Time.time + cameraImpulseInterval;
	//	float impulseForce = Mathf.Lerp(lightCameraImpulse, heavyCameraImpulse, Mathf.Clamp01(strength));

	//	Vector3 impulseDirection = Vector3.up;
	//	if (attackData.Attacker != null) {
	//		Vector3 fromAttacker = transform.position - attackData.Attacker.transform.position;
	//		if (fromAttacker.sqrMagnitude > 0.001f) {
	//			impulseDirection = fromAttacker.normalized;
	//		}
	//	}

	//	TriggerImpulse(cameraImpulseSource, impulseForce, impulseDirection);
	//}

	// Plays the correct hit audio for the damage type and strength
	private void PlayHitAudio(DamageType type, bool isHeavyHit, float strength) {
		if (audioSource == null) {
			return;
		}

		AudioClip clip = PickClip(type, isHeavyHit);
		if (clip == null) {
			return;
		}

		// Temporarily randomise pitch for the one-shot sound
		// Prevents repeated hits from sounding identical
		float oldPitch = audioSource.pitch;
		audioSource.pitch = Random.Range(randomPitchRange.x, randomPitchRange.y);

		// Stronger hits play slightly louder
		audioSource.PlayOneShot(clip, hitVolume * Mathf.Lerp(0.65f, 1.0f, strength));

		// Restore original pitch so other audio on the source is not permanently affected
		audioSource.pitch = oldPitch;
	}

	// Chooses which audio category to use for this hit
	private AudioClip PickClip(DamageType type, bool isHeavyHit) {
		if (isHeavyHit) {
			AudioClip heavyClip = PickRandomClip(heavyHitClips);
			if (heavyClip != null) {
				return heavyClip;
			}
		}

		return type == DamageType.Melee ? PickRandomClip(meleeHitClips) : PickRandomClip(rangedHitClips);
	}

	// Picks a random audio clip from an array
	private AudioClip PickRandomClip(AudioClip[] clips) {
		if (clips == null || clips.Length == 0) {
			return null;
		}

		return clips[Random.Range(0, clips.Length)];
	}
}