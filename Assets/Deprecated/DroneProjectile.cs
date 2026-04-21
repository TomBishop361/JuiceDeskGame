using UnityEngine;

// TODO: TRANSFER MOST OF THIS INTO A BASE PROJECTILE CLASS - which can be inherited from to create specifc projectile functionality for different enemy types / player etc

namespace Game.Combat.Projectiles {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class DroneProjectile : MonoBehaviour {
		[Header("Projectile Settings")]
		[Space(10)]

		[Header("Motion")]
		[Tooltip("Speed of projectile motion")]
		[SerializeField] private float projectileSpeed = 14.0f;
		//[Tooltip("Arc height of projectiles thrown")]
		//[SerializeField] private float projectileArcHeight = 6.0f;
		[Tooltip("Time before projectiles disappear")]
		[SerializeField] private float projectileLifetime = 3.5f;

		[Space(5)]

		[Header("Damage")]
		[Tooltip("Damage dealt by projectile (used as max damage - actual damage falls off with distance")]
		[SerializeField] private float projectileDamage = 1.0f;
		//[Tooltip("Radius of the projectile explosion that will cause damage to any damage layer inside of it")]
		//[SerializeField] private float projectileExplosionRadius = 3.5f;
		[Tooltip("Layers that can be damaged/hit by drone projectiles")]
		[SerializeField] private LayerMask projectileDamageLayers;
		//[Tooltip("Knockback data for drone ranged attack (includes force + upwardModifier + torqueStrength")]
		//[SerializeField] private KnockbackData projectileKnockbackData;

		[Space(5)]

		[Header("Visuals")]
		//[SerializeField] private float projectileVisualSpinSpeed = 720.0f;
		[SerializeField] private float gizmoHitRadius = 0.30f;
		[SerializeField] private LayerMask groundLayers;

		[Space(5)]

		[Header("Physics")]
		[SerializeField] private Rigidbody rb;
		[SerializeField] private bool useTrigger = true;

		// Runtime params
		private float deathTime = 0.0f;
		private bool hasHit = false;

		// Reset to default values
		private void Reset() {
			rb = GetComponent<Rigidbody>();
		}

		private void Awake() {
			if (rb == null) {
				rb = GetComponent<Rigidbody>();
			}

			deathTime = Time.time + projectileLifetime;

			// Make sure that there is consistent physics behaviour
			if (rb != null) {
				rb.useGravity = false;
				rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
			}
		}

		private void Update() {
			// Projectile lifetime exceeded - destroy it
			if (Time.time >= deathTime) {
				Destroy(gameObject);
			}
		}

		// Call this immediately after Instantiation
		// Direction is in World Space
		public void Launch(Vector3 direction) {
			direction.y = 0.0f;

			// Safety - ensures that a direction exists before rotating towards it
			if (direction.sqrMagnitude < 0.0001f) {
				direction = transform.forward;
			}
				
			direction.Normalize();

			transform.rotation = Quaternion.LookRotation(direction);

			if (rb != null) {
				rb.linearVelocity = direction * projectileSpeed;

			}
			else {
				// Fallback: If there is no rigidbody
				transform.position += direction * projectileSpeed * Time.deltaTime;
			}
		}

		private bool LayerAllowed(GameObject other) {
			// Uses bitmask check so there can be multiple projectile damage/hit layers 
			return (projectileDamageLayers.value & (1 << other.layer)) != 0;
		}

		// Use Trigger physics
		private void OnTriggerEnter(Collider other) {
			if (useTrigger) {
				return;
			}
			HandleHit(other.gameObject, other.ClosestPoint(transform.position));
		}

		// Use Collider physics
		private void OnCollisionEnter(Collision collision) {
			if (useTrigger == false) {
				return;
			}
			HandleHit(collision.gameObject, collision.GetContact(0).point);
		}

		private void HandleHit(GameObject other, Vector3 hitPoint) {
			if (hasHit) {
				return;
			}
			if (LayerAllowed(other) == false) {
				return;
			}

			hasHit = true;

			// Prototype: if hit object implements IDamageable then call it
			if (other.TryGetComponent(out IDamageableProjectileTemp damageable)) {
				damageable.TakeDamage(projectileDamage);
			}

			Destroy(gameObject);
		}
	}

	// Prototype: Fallback interface - DELETE LATER
	public interface IDamageableProjectileTemp {
		void TakeDamage(float amount);
	}
}