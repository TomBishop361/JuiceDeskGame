using Game.AI.Shield;
using System;
using System.Linq.Expressions;
using UnityEngine;

public class Health : MonoBehaviour {
	// Accessed inside TODO: HealthSensor.cs (MaxHealth + CurrentHealth)
	public float MaxHealth { get; private set; }
	public float CurrentHealth { get; private set; }
	//public float HealthNormalized => MaxHealth > 0.0f ? CurrentHealth / MaxHealth : 0.0f;

	// Non-AI gameplay events
	public event Action<float, float> OnHealthChanged;
	public event Action OnDamageDealt;
	public event Action OnDeath;

	private Animator animator;
	private bool isInvulnerable = false;

	private void Awake() {
		// Health settings (shared between Player & Enemies)
		if (TryGetComponent(out IHealthSettings healthSettingsInterface) == false) {
			Debug.LogError("Health requires IHealthSettings on " + gameObject.name);
			return;
		}
		// Get Animator for AI (to trigger death animation)
		if (TryGetComponent(out Animator anim)) {
			animator = anim;
		}

		MaxHealth = healthSettingsInterface.MaxHealth;
		CurrentHealth = MaxHealth;

		// Initial Health UI update
		OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
	}

	public void RegenerateHealth(float healthAmount) {
		if (CurrentHealth <= 0.0f) {
			return;
		}

		// Stop regenerating health once exceeded MaxHealth (set CurrentHealth to MaxHealth in that scenario)
		CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healthAmount);
		OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
	}

	// Reduces health + triggers death + notifies any listeners
	public void ApplyDamage(float damageAmount) {
		// Stop applying damage
		if (CurrentHealth <= 0.0f) {
			return;
		}

		if (isInvulnerable) {
			return;
		}

		CurrentHealth = Mathf.Clamp(CurrentHealth - damageAmount, 0.0f, MaxHealth);
		// Notify any listener of health change
		OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
		OnDamageDealt?.Invoke();

		if (CurrentHealth <= 0.0f) {
			// Player only
			OnDeath?.Invoke(); // TODO: REMOVE THIS ONLY IF OnHealthChanged should handle death OR KEEP AND LET Health.cs handle death instead

			//// AI only
			//if (animator != null) {
			//	animator.SetTrigger("Die");
			//}

			//animator.SetBool("IsFiring", false);
			//if (gameObject.TryGetComponent(out ShieldEnemy shield) != null) {
			//	shield.StopMinigun();
			//}
			//Debug.Log(gameObject + "died");
		}
	}

	public void RestoreFullHealth() {
		// Health settings (shared between Player & Enemies)
		if (TryGetComponent(out IHealthSettings healthSettingsInterface) == false) {
			Debug.LogError("Health requires IHealthSettings on " + gameObject.name);
			return;
		}

		MaxHealth = healthSettingsInterface.MaxHealth;
		CurrentHealth = MaxHealth;

		// Notify any listener of health change
		OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
	}

	public void SetInvulnerable(bool value) {
		isInvulnerable = value;
	}

}