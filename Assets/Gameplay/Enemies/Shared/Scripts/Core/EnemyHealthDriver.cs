using System.ComponentModel;
using UnityEngine;

namespace Game.AI {
	// Tracks health values for an enemy and exposes simple runtime signals such as damage taken this frame and dead state
	// Used as a communicator between the Health component and Enemy AI logic
	[DisallowMultipleComponent]
	public sealed class EnemyHealthDriver : MonoBehaviour {
		[Header("References")]
		[SerializeField] private Health healthComponent;

		[Header("Settings")]
		[Tooltip("When enabled, restores the enemy to full health each time it spawns.")]
		[SerializeField] private bool restoreFullHealthOnSpawn = true;

		public Health HealthComponent => healthComponent;
		public float CurrentHealth { get; private set; }
		public float PreviousHealth { get; private set; }
		public bool DamageTakenThisTick { get; private set; }
		public float DamageTakenAmount { get; private set; }
		public bool IsDead => CurrentHealth <= 0.0f;
		public bool HasHealthComponent => healthComponent != null;

		private bool initialised;

		private void Reset() {
			if (healthComponent == null) {
				healthComponent = GetComponent<Health>();
			}
		}

		// Assigns the Health component this driver should read from
		public void SetHealthSource(Health health) {
			healthComponent = health;
		}

		// Clears flags and restores cached values so the driver is ready for respawn + pooling + reuse
		public void ResetRuntime() {
			if (healthComponent != null && restoreFullHealthOnSpawn) {
				healthComponent.RestoreFullHealth();
			}

			ForceSyncFromComponent();
		}

		// Immediately copies values from the Health component into the cached runtime state without waiting for the next tick
		public void ForceSyncFromComponent() {
			CurrentHealth = healthComponent != null ? healthComponent.CurrentHealth : 0.0f;
			PreviousHealth = CurrentHealth;
			DamageTakenThisTick = false;
			DamageTakenAmount = 0.0f;
			initialised = true;
		}

		// Refreshes cached health values + detects whether damage was taken this tick + updates the dead flag
		public void Tick() {
			DamageTakenThisTick = false;
			DamageTakenAmount = 0.0f;

			if (healthComponent == null) {
				return;
			}

			float nextHealth = healthComponent.CurrentHealth;

			if (initialised == false) {
				CurrentHealth = nextHealth;
				PreviousHealth = nextHealth;
				initialised = true;
				return;
			}

			PreviousHealth = CurrentHealth;
			CurrentHealth = nextHealth;

			if (CurrentHealth < PreviousHealth) {
				DamageTakenThisTick = true;
				DamageTakenAmount = PreviousHealth - CurrentHealth;
			}
		}
    }
}