using System.Collections;
using UnityEngine;

namespace Game.AI {
	// Handles common enemy death cleanup such as optional knockback and delayed return to the object pool
	[DisallowMultipleComponent]
	public sealed class EnemyDeathHandler : MonoBehaviour {
		[Header("References")]
		[Tooltip("Pooled object component used to return this enemy to its pool after death.")]
		[SerializeField] private PooledObject pooledObject;
		[Tooltip("Optional rigidbody used to apply a small knockback force when the enemy dies.")]
		[SerializeField] private Rigidbody knockbackRB;

		[Header("Timing")]
		[Tooltip("Fallback delay before returning the enemy to the pool if no override is provided.")]
		[SerializeField] private float defaultDespawnDelay = 1.2f;

		[Header("Knockback")]
		[Tooltip("Strength of the velocity-change knockback applied on death.")]
		[SerializeField] private float deathKnockbackForce = 3.5f;
		[Tooltip("Extra upward lift added to the death knockback direction.")]
		[SerializeField] private float deathKnockbackLift = 0.2f;

		// Prevents the death sequence from being triggered more than once
		private bool deathStarted = false;

		private void Reset() {
			if (pooledObject == null) {
				pooledObject = GetComponent<PooledObject>();
			}

			if (knockbackRB == null) {
				knockbackRB = GetComponent<Rigidbody>();
			}
		}

		// Clears runtime death state so this handler can be reused after pooling or respawn
		public void ResetRuntime() {
			deathStarted = false;
		}

		// Starts the death flow once + optionally applies knockback + begins the delayed return-to-pool routine
		public void BeginDeathSequence(MonoBehaviour owner, Transform self, Transform sourceTarget, float despawnDelay = -1.0f, bool applyKnockback = true) {
			if (deathStarted) {
				return;
			}

			deathStarted = true;

			if (applyKnockback) {
				ApplyDeathKnockback(self, sourceTarget);
			}

			if (owner != null && pooledObject != null && gameObject.activeInHierarchy) {
				owner.StartCoroutine(DeathRoutine(despawnDelay > 0.0f ? despawnDelay : defaultDespawnDelay));
			}
		}

		// Immediately returns the enemy to its pool if it is active
		public void ReturnToPoolNow() {
			if (pooledObject != null && gameObject.activeInHierarchy) {
				pooledObject.ReturnToPool();
			}
		}

		// Applies a small knockback away from the damage source when the enemy dies
		private void ApplyDeathKnockback(Transform self, Transform sourceTarget) {
			if (self == null || sourceTarget == null || knockbackRB == null) {
				return;
			}

			Vector3 direction = (self.position - sourceTarget.position).normalized;
			direction.y = deathKnockbackLift;

			knockbackRB.isKinematic = false;
			knockbackRB.AddForce(direction * deathKnockbackForce, ForceMode.VelocityChange);
		}

		// Waits for the despawn delay then returns the enemy to the pool
		private IEnumerator DeathRoutine(float delay) {
			yield return new WaitForSeconds(delay);
			ReturnToPoolNow();
		}
	}

}