using UnityEngine;
using Game.AI.Drone;
using System.Collections.Generic;

// DroneHomingProjectile:
// Uses a Rigidbody and steers toward the target with a configurable turn rate
// Has max homing time (optional) so it doesn’t chase forever
// Has lifetime + hit handling
namespace Game.Combat.Projectiles {
	[DisallowMultipleComponent]
	[RequireComponent(typeof(PooledObject))]
	public sealed class DroneHomingProjectile : MonoBehaviour, IPoolLifecycleHandler {
		[Header("Visuals")]
		[SerializeField] private Material homingActiveMaterial;
		[SerializeField] private Material homingExpiredMaterial;

		[Space(5)]

		[Header("Physics")]
		[SerializeField] private Rigidbody rb;

		// Runtime params
		private Transform target;
		private ProjectileHitbox projectileHitbox;
		private MeshRenderer cachedRenderer;
		private PooledObject pooledObject;

		private AttackData activeAttackData;
		private AttackData timeoutAttackData;
		private LayerMask damageLayers;
		private float speed;
		private float turnRate;
		private float hitRadius;
		private float deathTime;
		private float homingEndTime;
		private bool explodeOnTimeout;
		private bool configured;
		private bool hasHit;
		private bool hasExpired;
		private Vector3 velocityDirection;
		
		private void Reset() {
			rb = GetComponent<Rigidbody>();
		}

		private void Awake() {
			if (rb == null) {
				rb = GetComponent<Rigidbody>();
			}

			cachedRenderer = GetComponentInChildren<MeshRenderer>();
			projectileHitbox = GetComponentInChildren<ProjectileHitbox>();
			pooledObject = GetComponent<PooledObject>();

			if (projectileHitbox != null) {
				// Subscribe to event handler from the hit box event
				projectileHitbox.OnProjectileHitImpact += HandleProjectileHitImpact;
			}

			// Make sure that there is consistent physics behaviour
			if (rb != null) {
				rb.useGravity = false;
				rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
				rb.interpolation = RigidbodyInterpolation.Interpolate;
			}

			ResetRuntimeState();
		}

		private void OnDestroy() {
			if (projectileHitbox != null) {
				projectileHitbox.OnProjectileHitImpact -= HandleProjectileHitImpact;
			}
		}

		// Configure the drone homing projectile stats (Called from DroneProjectileWeapon)
		public void Configure(AttackData projectileAttackData, DroneHomingProjectileStats stats) {
			activeAttackData = projectileAttackData;

			speed = stats != null ? Mathf.Max(0.01f, stats.Speed) : 24.0f;
			turnRate = stats != null ? Mathf.Max(0.0f, stats.TurnRate) : 540.0f;
			hitRadius = stats != null ? Mathf.Max(0.01f, stats.HitRadius) : 0.75f;
			explodeOnTimeout = stats == null || stats.ExplodeOnTimeout;
			timeoutAttackData = stats != null ? stats.TimeoutAttackData : projectileAttackData;
			damageLayers = stats != null ? stats.DamageLayers : ~0;

			float lifetime = stats != null ? stats.Lifetime : 3.0f;
			float homingDuration = stats != null ? stats.HomingDuration : 2.0f;

			deathTime = Time.time + lifetime;
			homingEndTime = homingDuration > 0.0f ? Time.time + homingDuration : float.PositiveInfinity;

			hasHit = false;
			hasExpired = false;
			configured = true;

			// Supply projectile hitbox with attack data
			if (projectileHitbox != null) {
				projectileHitbox.Initialise(activeAttackData);
			}
			else {
				Debug.LogWarning($"{name}: ProjectileHitbox cannot be found on projectile");
			}

			if (homingActiveMaterial != null && cachedRenderer != null) {
				cachedRenderer.material = homingActiveMaterial;
			}
		}

		// Initialise target for homing projectile upon Instantiation
		public void Init(Transform projectileTarget) {
			target = projectileTarget;
		}

		// Launch homing projectile towards target
		public void Launch(Vector3 initialDirection) {
			if (initialDirection.sqrMagnitude < 0.0001f) {
				initialDirection = transform.forward;
			}

			velocityDirection = initialDirection.normalized;
			transform.rotation = Quaternion.LookRotation(velocityDirection, Vector3.up); // Vector3.up is a change

			ApplyVelocity(velocityDirection * speed);
		}

		private void FixedUpdate() {
			if (configured == false || hasHit) {
				return;
			}

			if (Time.time >= deathTime) {
				// Allows projectile to still deal damage to player if it reaches the deathtime
				if (explodeOnTimeout) {
					ExplodeOnTimeout();
				}
				else {
					ReturnToPool();
				}
				return;
			}

			TickHoming();

			// Hit check (using custom sphere cast)
			//if (target != null) {
			//	Vector3 toTarget = (target.position + Vector3.up * 1.0f) - transform.position;

			//	if (toTarget.sqrMagnitude <= hitRadius * hitRadius) {
			//		TryDamage(target.gameObject);
			//		Destroy(gameObject);
			//	}
			//}
		}

		private void TickHoming() {
			// Homing projectile steering
			if (target != null && Time.time <= homingEndTime) {
				// Aim towards chest height of target
				// Uses center of mass (helps when player is above/below drone)
				Vector3 aimPoint = target.position + Vector3.up * 1.0f;
				Vector3 desiredDirection = (aimPoint - transform.position).normalized;

				if (desiredDirection.sqrMagnitude > 0.0001f) {
					// Rotate towards the target
					float maxRadians = turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;
					velocityDirection = Vector3.RotateTowards(velocityDirection, desiredDirection, maxRadians, 0.0f);
				}
			}
			// Change visual of homing projectile (expired = no longer locked on to target)
			else if (hasExpired == false) {
				if (homingExpiredMaterial != null && cachedRenderer != null) {
					cachedRenderer.material = homingExpiredMaterial;
				}

				hasExpired = true;
			}

			if (velocityDirection.sqrMagnitude > 0.0001f) {
				velocityDirection = velocityDirection.normalized;
			}
			else {
				// Fallback
				velocityDirection = transform.forward;
			}

			ApplyVelocity(velocityDirection * speed);

			if (velocityDirection.sqrMagnitude > 0.0001f) {
				Quaternion lookRotation = Quaternion.LookRotation(velocityDirection, Vector3.up);

				if (rb != null) {
					rb.MoveRotation(lookRotation);
				}
				else {
					transform.rotation = lookRotation;
				}
			}
		}

		// Event Handler for projectile impact -> invoked from inside ProjectileHitbox when a projectile hits a trigger
		// Deals damage based on direct or in-direct hit and plays effects
		public void HandleProjectileHitImpact(AttackData attackData, IDamageable directReceiver, Vector3 hitPoint) {
			if (hasHit) {
				return;
			}

			hasHit = true;

			// Direct hit = Full damage (check if target contains a HurtBox)
			if (directReceiver != null) {
				directReceiver.TakeDamage(attackData);
			}

			// In-Direct hit = Splash Damage
			DealSplashDamage(attackData, directReceiver, hitPoint);

			// TODO: Play Camera shake + SFX + VFX regardless
			Explode(/*hitPoint*/);
		}

		// Deals splash damage to any in-direct hits that contain a HurtBox
		private void DealSplashDamage(AttackData attackData, IDamageable directReceiver, Vector3 hitPoint) {
			Collider[] overlaps = Physics.OverlapSphere(hitPoint, hitRadius, damageLayers, QueryTriggerInteraction.Ignore);

			HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

			foreach (Collider hit in overlaps) {
				if (hit == null) {
					continue;
				}

				//// Check if hits inside splash radius contains a HurtBox
				//if (hit.TryGetComponent(out IDamageable damageable) == false) {
				//	continue;
				//}

				// Check if hits inside splash radius contains a HurtBox
				IDamageable damageable = hit.GetComponentInParent<IDamageable>();
				if (damageable == null) {
					continue;
				}

				// Prevent any double damage on the direct hit target
				if (directReceiver != null && damageable == directReceiver) {
					continue;
				}

				// Prevent multiple colliders on the same target from taking damage more than once
				if (damagedTargets.Add(damageable) == false) {
					continue;
				}

				// Distance from damage target and projectile impact point
				float distance = Vector3.Distance(hit.ClosestPoint(hitPoint), hitPoint);
				float finalDamage = CalculateDamageFalloff(attackData, distance);
				if (finalDamage <= 0.0f) {
					continue;
				}

				AttackData splashAttack = attackData;
				splashAttack.Damage = finalDamage;

				// Deal splash damage to in-direct hits (must contain a HurtBox)
				damageable.TakeDamage(splashAttack);
			}
		}

		// Calculate damage falloff based on distance of explosion to the hit target
		private float CalculateDamageFalloff(AttackData attackData, float distance) {
			float t = Mathf.Clamp01(distance / hitRadius);
			return Mathf.Lerp(attackData.Damage, 0.0f, t);
		}

		// TODO: Play Camera shake + SFX + VFX + Despawn projectile regardless of hit
		private void Explode(/*Vector3 explosionPoint*/) {
			// Play projectile explosion Camera shake + SFX + VFX at explosion point
			//CameraShakeManager.Instance.TriggerCameraShakeAtPosition(CameraShakeType.BossProjectileHit, explosionPoint);
			//SFXController.Instance.PlayProjectileExplosion(explosionPoint);
			//VFXController.Instance.SpawnProjectileExplosion(explosionPointl);

			// Return to pool once explosion effects have been started
			ReturnToPool();
		}

		// Explodes if no collision was made and lifetime timer expired - allows it to still deal damage to player
		private void ExplodeOnTimeout() {
			// Prevents double explosion damage in the case of a collision
			if (hasHit) {
				return;        
			}
			hasHit = true;

			// Timeout explosion has no directReceiver since no collision was made so it’s just splash damage
			DealSplashDamage(timeoutAttackData, null, transform.position);
			Explode(/*transform.position*/);
		}

		private void ApplyVelocity(Vector3 velocity) {
			if (rb != null) {
				rb.linearVelocity = velocity;
			}
			else {
				transform.position += velocity * Time.fixedDeltaTime;
			}
		}

		public void ReturnToPool() {
			if (pooledObject != null) {
				pooledObject.ReturnToPool();
			}
		}

		private void ResetRuntimeState() {
			target = null;
			configured = false;
			hasHit = false;
			hasExpired = false;
			velocityDirection = Vector3.zero;
			deathTime = 0.0f;
			homingEndTime = 0.0f;
			speed = 0.0f;
			turnRate = 0.0f;
			hitRadius = 0.75f;
			explodeOnTimeout = true;
			activeAttackData = default;
			timeoutAttackData = default;
			damageLayers = ~0;

			if (rb != null) {
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}

			if (homingActiveMaterial != null && cachedRenderer != null) {
				cachedRenderer.material = homingActiveMaterial;
			}
		}

		public void OnSpawned() {
			ResetRuntimeState();
		}

		public void OnDespawned() {
			ResetRuntimeState();
		}

		// Shows explosion hit radius as a gizmo (scene view only)
		// TODO: Show explosion predicted impact point as a gizmo (scene view only)
		// NOTE: DEBUG HIT RADIUS (DIRECT = DIRECT DAMAGE | ANYTHING ELSE BUT STILL IN HIT RADIUS = SPLASH DAMAGE)
		private void OnDrawGizmos() {
			//if (hasPredictedHit == false) {
			//	return;
			//}

			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(transform.position, hitRadius);
		}

		// - DEPRECATED -

		//// - Events and Callback handlers -

		//private void OnDestroy() {
		//	if (projectileHitbox != null) {
		//		projectileHitbox.OnProjectileHitImpact -= HandleProjectileHitImpact;
		//	}
		//}

		//private void TryDamage(GameObject other) {
		//	if (LayerAllowed(other) == false) {
		//		return;
		//	}

		//	// Prototype: if hit object implements IDamageable then call it
		//	if (other.TryGetComponent(out IDamageable damageable)) {
		//		damageable.TakeDamage(homingProjectileDamage);
		//	}
		//}

		//private void FixedUpdate() {
		//	if (Time.time >= deathTime) {
		//		Destroy(gameObject);
		//		return;
		//	}
		//	if (hasHit || rb == null) {
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

		//private bool LayerAllowed(GameObject other) {
		//	// Uses bitmask check so there can be multiple projectile damage/hit layers 
		//	return (hitLayers.value & (1 << other.layer)) != 0;
		//}
		
		//private void OnTriggerEnter(Collider other) {
		//	if (useTrigger == false) {
		//		return;
		//	}

		//	HandleHit(other.gameObject);
		//}

		//private void OnCollisionEnter(Collision collision) {
		//	if (useTrigger) {
		//		return;
		//	}

		//	HandleHit(collision.gameObject);
		//}

		//private void HandleHit(GameObject other) {
		//	if (hasHit) {
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
}