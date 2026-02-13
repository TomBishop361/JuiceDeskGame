using NUnit.Framework;
using UnityEngine;

// DroneHomingProjectile:
// Uses a Rigidbody and steers toward the target with a configurable turn rate
// Has max homing time (optional) so it doesn’t chase forever
// Has lifetime + hit handling
namespace Game.Combat.Projectiles {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class DroneHomingProjectile : MonoBehaviour {
		[Header("Homing Projectile Settings")]
		[Space(10)]

		[Header("Motion")]
		[Tooltip("Speed of projectile motion")]
		[SerializeField] private float homingProjectileSpeed = 14.0f;
		[Tooltip("How fast projectile can rotate towards target (degrees per second)")]
		[SerializeField] private float homingProjectileTurnRate = 360.0f;
		[Tooltip("Time before projectiles disappear")]
		[SerializeField] private float homingProjectileLifetime = 3.0f;
		[Tooltip("Time before projectile stops homing towards target")]
		[SerializeField] private float homingDuration = 2.0f; // set <= 0 for infinite homing

		[Space(5)]

		[Header("Damage")]
		[Tooltip("Damage that homing projectile deals to target")]
		[SerializeField] private int homingProjectileDamage = 1;
		[Tooltip("Hit radius of homing projectile")]
		[SerializeField] private float hitRadius = 0.15f;
		[Tooltip("Layers that the homing projectile can interact with")]
		[SerializeField] private LayerMask hitLayers = ~0;

		[Space(5)]

		[Header("Visuals")]
		[SerializeField] private Material homingActiveMaterial;
		[SerializeField] private Material homingExpiredMaterial;

		[Space(5)]

		[Header("Physics")]
		[SerializeField] private Rigidbody rb;
		[SerializeField] private bool useTrigger = true;

		// Runtime params
		private Transform target;
		private float deathTime;
		private float homingEndTime;
		//private bool hasHit;
		private bool hasExpired = false;
		private Vector3 velocityDirection;

		private void Reset() {
			rb = GetComponent<Rigidbody>();
		}

		private void Awake() {
			if (rb == null) {
				rb = GetComponent<Rigidbody>();
			}

			if (homingActiveMaterial != null) {
				gameObject.GetComponent<MeshRenderer>().material = homingActiveMaterial;
			}
		
			deathTime = Time.time + homingProjectileLifetime;

			// Check if projectile should home for a set duration or infinitely
			homingEndTime = homingDuration > 0 ? Time.time + homingDuration : float.PositiveInfinity;

			// Make sure that there is consistent physics behaviour
			if (rb != null) {
				rb.useGravity = false;
				rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
				rb.interpolation = RigidbodyInterpolation.Interpolate;
			}
		}

		// Initialise target for homing projectile upon Instantiation
		public void Init(Transform projectileTarget) {
			target = projectileTarget;
		}

		// Launch homing projectile towards target
		public void Launch(Vector3 initialDirection) {
			//initialDirection.y = 0.0f;

			if (initialDirection.sqrMagnitude < 0.0001f) {
				initialDirection = transform.forward;
			}

			velocityDirection = initialDirection.normalized;

			transform.rotation = Quaternion.LookRotation(velocityDirection);

			//if (rb != null) {
			//	rb.linearVelocity = initialDirection * homingProjectileSpeed;
			//}
			//else {
			//	// Fallback: If there is no rigidbody
			//	transform.position += initialDirection * homingProjectileSpeed * Time.deltaTime;
			//}
		}

		private void Update() {
			if (Time.time >= deathTime) {
				Destroy(gameObject);
				return;
			}

			// Homing projectile steering
			if (target != null && Time.time <= homingEndTime) {
				// Aim towards chest height of target
				// Uses center of mass (helps when player is above/below drone)
				Vector3 aimPoint = target.position + Vector3.up * 1.0f;
				Vector3 desiredDirection = (aimPoint - transform.position).normalized;

				// Rotate toward the target using 'homingProjectileTurnRate'
				float maxRadians = homingProjectileTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;
				velocityDirection = Vector3.RotateTowards(velocityDirection, desiredDirection, maxRadians, 0.0f);
			}
			// Change visual of homing projectile (expired - no longer locked on to target)
			else if (hasExpired == false) {
				if (homingExpiredMaterial != null) {
					gameObject.GetComponent<MeshRenderer>().material = homingExpiredMaterial;
				}
				hasExpired = false;
			}

			// Move forward
			transform.position += velocityDirection * homingProjectileSpeed * Time.deltaTime;

			if (velocityDirection.sqrMagnitude > 0.0001f) {
				transform.rotation = Quaternion.LookRotation(velocityDirection);
			}
			
			// Hit check (using custom sphere cast)
			if (target != null) {
				Vector3 toTarget = (target.position + Vector3.up * 1.0f) - transform.position;

				if (toTarget.sqrMagnitude <= hitRadius * hitRadius) {
					TryDamage(target.gameObject);
					Destroy(gameObject);
				}
			}
		}

		private void TryDamage(GameObject other) {
			if (LayerAllowed(other) == false) {
				return;
			}

			// Prototype: if hit object implements IDamageable then call it
			if (other.TryGetComponent(out IDamageableHomingTemp damageable)) {
				damageable.TakeDamage(homingProjectileDamage);
			}
			
		}

		//private void FixedUpdate() {
		//	if (Time.time >= deathTime) {
		//		Destroy(gameObject);
		//		return;
		//	}
		//	if (hasHit == true || rb == null) {
		//		return;
		//	}
		//	if (target == null) {
		//		return;
		//	}
		//	if (Time.time > homingEndTime) {
		//		return;
		//	}

		//	// Aim point: uses center of mass (helps when player is above/below drone)
		//	Vector3 targetPos = target.position + Vector3.up * 1.0f;

		//	Vector3 toTarget = targetPos - rb.position;
		//	if (toTarget.sqrMagnitude < 0.0001f) {
		//		return;
		//	}

		//	Vector3 desiredDirection = toTarget.normalized;

		//	// Current velocity direction
		//	Vector3 currentVelocity = rb.linearVelocity;
		//	Vector3 currentDirection = currentVelocity.sqrMagnitude > 0.001f ? currentVelocity.normalized : transform.forward;

		//	// Rotate toward the target using 'homingProjectileTurnRate'
		//	float maxRadians = homingProjectileTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;
		//	Vector3 newDirection = Vector3.RotateTowards(currentDirection, desiredDirection, maxRadians, 0.0f);

		//	// Apply new velocity + face the travel direction
		//	rb.linearVelocity = newDirection * homingProjectileSpeed;

		//	if (newDirection.sqrMagnitude > 0.0001f) {
		//		rb.MoveRotation(Quaternion.LookRotation(newDirection));
		//	}
		//}

		private bool LayerAllowed(GameObject other) {
			// Uses bitmask check so there can be multiple projectile damage/hit layers 
			return (hitLayers.value & (1 << other.layer)) != 0;
		}
		
		//private void OnTriggerEnter(Collider other) {
		//	if (useTrigger == false) {
		//		return;
		//	}

		//	HandleHit(other.gameObject);
		//}

		//private void OnCollisionEnter(Collision collision) {
		//	if (useTrigger == true) {
		//		return;
		//	}

		//	HandleHit(collision.gameObject);
		//}

		//private void HandleHit(GameObject other) {
		//	if (hasHit == true) {
		//		return;
		//	}
		//	if (LayerAllowed(other) == false) {
		//		return;
		//	}

		//	hasHit = true;

		//	// Prototype: if hit object implements IDamageable then call it
		//	if (other.TryGetComponent(out IDamageableHomingTemp damageable)) {
		//		damageable.TakeDamage(homingProjectileDamage);
		//	}

		//	Destroy(gameObject);
		//}
	}

	// Prototype: Fallback interface - DELETE LATER
	public interface IDamageableHomingTemp {
		void TakeDamage(float amount);
	}
}