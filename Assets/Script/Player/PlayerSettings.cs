using System.ComponentModel;
using UnityEngine;

public class PlayerSettings : MonoBehaviour, IFactionOwner, IHealthSettings {
	// Implement IFactionOwner
	public Faction OwnerFaction => Faction.Player;

	// Implement IHealthSettings
	public float MaxHealth => maxHealth;
	public float LowHealthThreshold => lowHealth;

	[Header("References")]
	[SerializeField] private Health playerHealthComponent;
	[SerializeField] private Animator playerAnimator;

	[Header("Stats")]
	[Tooltip("Maximum health for the player")]
	[SerializeField] private int maxHealth = 10;
	[Tooltip("Low health threshold for the player (once reached, low health indicator is activated)")]
	[SerializeField] private int lowHealth = 3;

	public Animator PlayerAnimator => playerAnimator;

	// Runtime params
	private float currentHealth;
	private float previousHealthValue = 0.0f;
	private float lastDamageTime = -Mathf.Infinity; // TODO: use for invunerability window

	private void Awake() {
		// Sync health
		currentHealth = maxHealth;

		if (playerAnimator == null) {
			playerAnimator = GetComponent<Animator>();
		}
	}

	private void Update() {
		// Update health for debugging purposes
		currentHealth = playerHealthComponent.CurrentHealth;
	}

	// - Event & Callback handlers -

	private void OnEnable() {
		if (playerHealthComponent != null) {
			previousHealthValue = playerHealthComponent.CurrentHealth;
			playerHealthComponent.OnHealthChanged += OnHealthChanged;
		}
	}
	private void OnDisable() {
		if (playerHealthComponent != null) {
			playerHealthComponent.OnHealthChanged -= OnHealthChanged;
		}
	}

	private void OnHealthChanged(float current, float max) {
		Debug.Log("ON HEALTH CHANGED IS CALLED");
		// Damage only if health has gone down
		if (current < previousHealthValue) {
			// Reset regen delay timer - only regen when out of combat
			lastDamageTime = Time.time;
		}
		previousHealthValue = current;
	}

}