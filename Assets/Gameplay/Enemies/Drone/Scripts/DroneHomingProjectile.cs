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
		[Tooltip("If true, exploding on lifetime expiry can deal splash damage")]
		[SerializeField] private bool explodeOnTimeout = true;
		[Tooltip("Attack data to use when projectile times out and explodes - for now copy the same values used for drone projectile attack data on the Drone Gameobject")]
		[SerializeField] private AttackData timeoutAttackData;
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
		private bool hasHit = false;
		private bool hasExpired = false;
		private Vector3 velocityDirection;
		private ProjectileHitbox projectileHitbox;

		private void Reset() {
			rb = GetComponent<Rigidbody>();
		}

		private void Awake() {
			if (rb == null) {
				rb = GetComponent<Rigidbody>();
			}

			if (homingActiveMaterial != null) {
				gameObject.GetComponentInChildren<MeshRenderer>().material = homingActiveMaterial;
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

			projectileHitbox = GetComponentInChildren<ProjectileHitbox>();
			if (projectileHitbox != null) {
				// Subscribe to event handler from the hit box event
				projectileHitbox.OnProjectileHitImpact += HandleProjectileHitImpact;
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
				// Allows projectile to still deal damage to player if it reaches the deathtime
				if (explodeOnTimeout == true) {
					ExplodeOnTimeout();
				}
				else {
					Destroy(gameObject);
				}
				return;
			}

			SteerProjectile();


			// Hit check (using custom sphere cast)
			//if (target != null) {
			//	Vector3 toTarget = (target.position + Vector3.up * 1.0f) - transform.position;

			//	if (toTarget.sqrMagnitude <= hitRadius * hitRadius) {
			//		TryDamage(target.gameObject);
			//		Destroy(gameObject);
			//	}
			//}
		}

		public void SteerProjectile() {
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
					gameObject.GetComponentInChildren<MeshRenderer>().material = homingExpiredMaterial;
				}
				hasExpired = true;
			}

			// Move forward
			transform.position += velocityDirection * homingProjectileSpeed * Time.deltaTime;

			if (velocityDirection.sqrMagnitude > 0.0001f) {
				transform.rotation = Quaternion.LookRotation(velocityDirection);
			}
		}

		// Event Handler for projectile impact - invoked from inside TBD when a projectile hits a trigger
		// Deals damage based on direct or in-direct hit and plays effects
		public void HandleProjectileHitImpact(AttackData attackData, IDamageable directReceiver, Vector3 hitPoint) {
			if (hasHit == true) {
				return;
			}
			Debug.Log("PLAYER DESTORY DRONE PROJ");
			hasHit = true;

			// Direct hit = Full damage (check if target contains a HurtBox)
			if (directReceiver != null) {
				Debug.Log("DIRECT DAMAGE = " + attackData.Damage);
				directReceiver.TakeDamage(attackData);
			}

			// In-Direct hit = Splash Damage
			DealSplashDamage(attackData, directReceiver, hitPoint);

			// TODO: Play Camera shake + SFX + VFX regardless
			Explode(/*hitPoint*/);
		}

		// Deals splash damage to any in-direct hits that contain a HurtBox
		private void DealSplashDamage(AttackData attackData, IDamageable directReceiver, Vector3 hitPoint) {
			Collider[] colliderOverlapsArray = Physics.OverlapSphere(hitPoint, hitRadius, hitLayers);

			foreach (Collider hit in colliderOverlapsArray) {

				//// Check if hits inside splash radius contains a HurtBox
				//if (hit.TryGetComponent(out IDamageable damageable) == false) {
				//	continue;
				//}

				// Check if hits inside splash radius contains a HurtBox
				IDamageable damageable = hit.GetComponentInParent<IDamageable>();
				if (damageable == null) {
					continue;
				}

				// Prevent any double damage on direct hits
				if (directReceiver != null && damageable == directReceiver) {
					continue;
				}

				// Distance from damage target and projectile impact point
				float distance = Vector3.Distance(hit.ClosestPoint(hitPoint), hitPoint);

				float finalDamage = CalculateDamageFalloff(distance);

				if (finalDamage <= 0.0f) {
					continue;
				}

				AttackData splashAttack = attackData;
				splashAttack.Damage = finalDamage;

				Debug.Log("SPLASH DAMAGE = " + finalDamage);

				// Deal splash damage to in-direct hits (must contain a HurtBox)
				damageable.TakeDamage(splashAttack);
			}
		}

		// Calculate damage falloff based on distance of explosion to the hit target
		private float CalculateDamageFalloff(float distance) {
			float t = Mathf.Clamp01(distance / hitRadius);

			return Mathf.Lerp(homingProjectileDamage, 0.0f, t);
		}

		// TODO: Play Camera shake + SFX + VFX + Destroy projectile regardless of hit
		private void Explode(/*Vector3 explosionPoint*/) {
			// Play projectile explosion Camera shake + SFX + VFX at explosion point
			//CameraShakeManager.Instance.TriggerCameraShakeAtPosition(CameraShakeType.BossProjectileHit, explosionPoint);
			//SFXController.Instance.PlayProjectileExplosion(explosionPoint);
			//VFXController.Instance.SpawnProjectileExplosion(explosionPointl);
			Debug.Log("HOMING PROJECTILE EXPLODED");
			// Destroy once explosion effects have been started 
			Destroy(gameObject);
		}

		// Explodes if no collision was made and lifetime timer expired - allows it to still deal damage to player
		private void ExplodeOnTimeout() {
			// Prevents double explosion damage in the case of a collision
			if (hasHit == true) {
				return;        
			}
			hasHit = true;

			Vector3 explosionPoint = transform.position;

			// Timeout explosion has no directReceiver since no collision was made so it’s just splash damage
			DealSplashDamage(timeoutAttackData, null, explosionPoint);

			Explode();
		}

		// Shows explosion hit radius as a gizmo (scene view only)
		// TODO: Show explosion predicted impact point as a gizmo (scene view only)
		// NOTE: DEBUG HIT RADIUS (DIRECT = DIRECT DAMAGE | ANYTHING ELSE BUT STILL IN HIT RADIUS IS SPLASH DAMAGE)
		private void OnDrawGizmos() {
			//if (hasPredictedHit == false) {
			//	return;
			//}

			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(transform.position, hitRadius);
			//Gizmos.DrawSphere(transform.position, hitRadius);
		}

		// - Events and Callback handlers -

		private void OnDestroy() {
			if (projectileHitbox != null) {
				projectileHitbox.OnProjectileHitImpact -= HandleProjectileHitImpact;
			}
		}

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
}