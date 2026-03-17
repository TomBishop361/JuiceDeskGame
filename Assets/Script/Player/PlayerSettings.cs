using System.ComponentModel;
using UnityEngine;

public class PlayerSettings : MonoBehaviour, IFactionOwner, IHealthSettings {
	// Implement IFactionOwner
	public Faction OwnerFaction => Faction.Player;

	// Implement IHealthSettings
	public float MaxHealth => maxHealth;
	public float LowHealthThreshold => lowHealth;

	[Header("References")]
	[SerializeField] private Health healthComponent;

	[Header("Stats")]
	[SerializeField] private int maxHealth = 10;
	[SerializeField] private int lowHealth = 3;

	// Runtime params
	[Header("DEBUG: Runtime params")]
	[SerializeField] private float currentHealth;
	private float previousHealthValue = 0.0f;
	private float lastDamageTime = -Mathf.Infinity; // TODO: use for invunerability window

	private void Awake() {
		// Sync health
		currentHealth = maxHealth;
	}

	private void Update() {
		// Update health for debugging purposes
		currentHealth = healthComponent.CurrentHealth;
	}

	// - Event & Callback handlers -

	private void OnEnable() {
		if (healthComponent != null) {
			previousHealthValue = healthComponent.CurrentHealth;
			healthComponent.OnHealthChanged += OnHealthChanged;
		}
	}
	private void OnDisable() {
		if (healthComponent != null) {
			healthComponent.OnHealthChanged -= OnHealthChanged;
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