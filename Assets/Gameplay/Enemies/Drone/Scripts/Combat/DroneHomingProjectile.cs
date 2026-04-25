using UnityEngine;
using Game.AI.Drone;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.ProBuilder;

// DroneHomingProjectile:
// Uses a Rigidbody and steers toward the target with a configurable turn rate
// Supports an arming phase + delayed/ramped seek strength + splash damage + timeout explosion +
// optional proximity fuse detonation + pooled reuse
namespace Game.Combat.Projectiles {
	[DisallowMultipleComponent]
	[RequireComponent(typeof(PooledObject))]
	public sealed class DroneHomingProjectile : MonoBehaviour, IPoolLifecycleHandler {
		[Header("Visuals")]
		[Tooltip("Material shown while the projectile is still charging and cannot explode yet.")]
		[SerializeField] private Material armingMaterial;
		[Tooltip("Material shown while the projectile is armed and still actively homing.")]
		[SerializeField] private Material homingActiveMaterial;
		[Tooltip("Material shown once homing has expired and the projectile is flying straight.")]
		[SerializeField] private Material homingExpiredMaterial;

		[Space(5)]

		[Header("Physics")]
		[SerializeField] private Rigidbody rb;

		// Runtime params
		private Transform target;
		private ProjectileHitbox projectileHitbox;
		private MeshRenderer cachedRenderer;
		private PooledObject pooledObject;

		// Runtime gameplay data pushed in from 'DroneProjectileWeapon'
		private AttackData activeAttackData;
		private AttackData timeoutAttackData;
		private LayerMask damageLayers;
		private float speed;
		private float turnRate;
		private float explosionRadius;
		private float deathTime;
		private float homingEndTime;
		private float armingDelay;
		private float minimumArmingDistance;
		private float seekDelay;
		private float seekRampDuration;
		private float initialSeekStrength;
		private float targetAimHeight;
		private float proximityFuseRadius;
		private bool explodeOnTimeout;
		private bool useProximityFuse;
		private bool configured;
		private bool hasHit;
		private bool hasExpired;
		private bool isArmed;
		private Vector3 velocityDirection;
		private Vector3 launchPosition;
		private float launchTime;
		
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

		// Configure the drone homing projectile stats (Called from DroneProjectileWeapon right before launch)
		public void Configure(AttackData projectileAttackData, DroneHomingProjectileStats stats) {
			activeAttackData = projectileAttackData;

			speed = stats != null ? Mathf.Max(0.01f, stats.Speed) : 24.0f;
			turnRate = stats != null ? Mathf.Max(0.0f, stats.TurnRate) : 540.0f;
			explosionRadius = stats != null ? Mathf.Max(0.01f, stats.ExplosionRadius) : 0.75f;
			explodeOnTimeout = stats == null || stats.ExplodeOnTimeout;
			timeoutAttackData = stats != null ? stats.TimeoutAttackData : projectileAttackData;
			damageLayers = stats != null ? stats.DamageLayers : ~0;

			float lifetime = stats != null ? stats.Lifetime : 3.0f;
			float homingDuration = stats != null ? stats.HomingDuration : 2.0f;

			armingDelay = stats != null ? Mathf.Max(0.0f, stats.ArmingDelay) : 0.0f;
			minimumArmingDistance = stats != null ? Mathf.Max(0.0f, stats.MinimumArmingDistance) : 0.0f;
			seekDelay = stats != null ? Mathf.Max(0.0f, stats.SeekDelay) : 0.0f;
			seekRampDuration = stats != null ? Mathf.Max(0.0f, stats.SeekRampDuration) : 0.0f;
			initialSeekStrength = stats != null ? Mathf.Clamp01(stats.InitialSeekStrength) : 1.0f;
			targetAimHeight = stats != null ? stats.TargetAimHeight : 1.0f;
			useProximityFuse = stats != null && stats.UseProximityFuse;
			proximityFuseRadius = stats != null ? Mathf.Max(0.0f, stats.ProximityFuseRadius) : 0.0f;

			deathTime = Time.time + lifetime;
			homingEndTime = homingDuration > 0.0f ? Time.time + homingDuration : float.PositiveInfinity;

			hasHit = false;
			hasExpired = false;
			isArmed = false;
			configured = true;

			// Supply projectile hitbox with attack data
			if (projectileHitbox != null) {
				projectileHitbox.Initialise(activeAttackData);
			}
			else {
				Debug.LogWarning($"{name}: ProjectileHitbox cannot be found on projectile");
			}

			ApplyCurrentVisualState();
		}

		// Initialise target transform that the projectile will steer toward
		public void Init(Transform projectileTarget) {
			target = projectileTarget;
		}

		// Launch homing projectile in its starting direction and gather launch data that
		// is used for aiming/seek timing
		public void Launch(Vector3 initialDirection) {
			if (initialDirection.sqrMagnitude < 0.0001f) {
				initialDirection = transform.forward;
			}

			velocityDirection = initialDirection.normalized;
			launchPosition = transform.position;
			launchTime = Time.time;
			transform.rotation = Quaternion.LookRotation(velocityDirection, Vector3.up);

			ApplyVelocity(velocityDirection * speed);
			ApplyCurrentVisualState();
		}

		private void FixedUpdate() {
			if (configured == false || hasHit) {
				return;
			}

			// If the projectile never armed, it will die out safely
			if (Time.time >= deathTime) {
				// Allows projectile to still deal damage to player if it reaches the deathtime
				if (explodeOnTimeout && isArmed) {
					ExplodeOnTimeout();
				}
				else {
					ReturnToPool();
				}
				return;
			}

			TickArming();
			TickHoming();
			TickProximityFuse();

			// Hit check (using custom sphere cast)
			//if (target != null) {
			//	Vector3 toTarget = (target.position + Vector3.up * 1.0f) - transform.position;

			//	if (toTarget.sqrMagnitude <= explosionRadius * explosionRadius) {
			//		TryDamage(target.gameObject);
			//		Destroy(gameObject);
			//	}
			//}
		}

		// Advances the arming state based on the configured delay and/or minimum travel distance
		private void TickArming() {
			if (isArmed) {
				return;
			}

			bool armingDelayPassed = armingDelay <= 0.0f || Time.time >= launchTime + armingDelay;
			bool armingDistancePassed = minimumArmingDistance <= 0.0f || Vector3.Distance(launchPosition, transform.position) >= minimumArmingDistance;

			if (armingDelayPassed == false || armingDistancePassed == false) {
				return;
			}

			isArmed = true;
			ApplyCurrentVisualState();
		}


		private void TickHoming() {
			// Homing projectile steering while lock-on is still active
			if (target != null && Time.time <= homingEndTime) {
				Vector3 aimPoint = GetAimPoint();
				Vector3 desiredDirection = (aimPoint - transform.position).normalized;

				if (desiredDirection.sqrMagnitude > 0.0001f) {
					// Delayed seek lets the projectile launch outward first, then ramp up into a stronger turn
					float effectiveTurnRate = turnRate * EvaluateSeekStrength();
					float maxRadians = effectiveTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;
					velocityDirection = Vector3.RotateTowards(velocityDirection, desiredDirection, maxRadians, 0.0f);
				}
			}
			// Change visual once the projectile is armed but no longer locked onto the target
			else if (hasExpired == false) {
				hasExpired = true;
				ApplyCurrentVisualState();
			}

			if (velocityDirection.sqrMagnitude > 0.0001f) {
				velocityDirection = velocityDirection.normalized;
			}
			else {
				// Fallback if some external force cleared velocity direction
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

		// Proximity fuse that detonates near the target rather than requiring a direct hit (Optional)
		private void TickProximityFuse() {
			if (useProximityFuse == false || isArmed == false || target == null || proximityFuseRadius <= 0.0f) {
				return;
			}

			Vector3 aimPoint = GetAimPoint();
			if ((aimPoint - transform.position).sqrMagnitude > proximityFuseRadius * proximityFuseRadius) {
				return;
			}

			hasHit = true;
			DealSplashDamage(activeAttackData, null, transform.position);
			Explode();
		}

		// Returns the steering/fuse point used for target tracking
		// Chest height aiming helps when the target is above or below the drone
		private Vector3 GetAimPoint() {
			if (target == null) {
				return transform.position + transform.forward;
			}

			return target.position + Vector3.up * targetAimHeight;
		}

		// Evaluates seek strength over time so the projectile can launch straight first,
		// then ramp into stronger tracking
		private float EvaluateSeekStrength() {
			float timeSinceLaunch = Time.time - launchTime;

			if (timeSinceLaunch <= seekDelay) {
				return initialSeekStrength;
			}

			if (seekRampDuration <= 0.0f) {
				return 1.0f;
			}

			float rampTime = timeSinceLaunch - seekDelay;
			float t = Mathf.Clamp01(rampTime / seekRampDuration);

			return Mathf.Lerp(initialSeekStrength, 1.0f, t);
		}

		// Swaps materials to make each projectile state more visually readable to the player
		// (Charging + Armed & homing + Homing expired)
		private void ApplyCurrentVisualState() {
			if (cachedRenderer == null) {
				return;
			}

			Material desiredMaterial;
			if (isArmed == false) {
				desiredMaterial = armingMaterial != null ? armingMaterial : homingActiveMaterial;
			}
			else if (hasExpired) {
				desiredMaterial = homingExpiredMaterial != null ? homingExpiredMaterial : homingActiveMaterial;
			}
			else {
				desiredMaterial = homingActiveMaterial;
			}

			if (desiredMaterial != null) {
				cachedRenderer.material = desiredMaterial;
			}
		}

		// Event Handler for projectile impact -> invoked from inside ProjectileHitbox when a projectile hits a trigger
		// Damage is ignored until the projectile is armed so short-range launches remain fair
		// Deals damage based on direct or in-direct hit and plays effects
		public void HandleProjectileHitImpact(AttackData attackData, IDamageable directReceiver, Vector3 hitPoint) {
			if (hasHit || isArmed == false) {
				return;
			}

			hasHit = true;

			// Direct hit = Full damage
			if (directReceiver != null) {
				directReceiver.TakeDamage(attackData);
			}

			// In-Direct hit = Splash Damage
			DealSplashDamage(attackData, directReceiver, hitPoint);

			Explode(/*hitPoint*/);
		}

		// Deals splash damage to indirect targets found inside the explosion radius
		private void DealSplashDamage(AttackData attackData, IDamageable directReceiver, Vector3 hitPoint) {
			Collider[] overlaps = Physics.OverlapSphere(hitPoint, explosionRadius, damageLayers, QueryTriggerInteraction.Ignore);

			HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

			foreach (Collider hit in overlaps) {
				if (hit == null) {
					continue;
				}

				//// Check if hits inside splash radius contains a HurtBox
				//if (hit.TryGetComponent(out IDamageable damageable) == false) {
				//	continue;
				//}

				// Check if hits inside splash radius contain a damage receiver on the collider or one of its parents
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
				damageable.TakeDamage(splashAttack);
			}
		}

		// Calculate damage falloff based on distance from the explosion cener
		private float CalculateDamageFalloff(AttackData attackData, float distance) {
			float t = Mathf.Clamp01(distance / explosionRadius);
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

		// Explodes if no collision was made and the lifetime timer expired
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

		public void SetSpawnPose(Vector3 position, Quaternion rotation) {
			if (rb != null) {
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
				rb.position = position;
				rb.rotation = rotation;
				transform.SetPositionAndRotation(position, rotation);
				rb.WakeUp();
			}
			else {
				transform.SetPositionAndRotation(position, rotation);
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
			isArmed = false;
			velocityDirection = Vector3.zero;
			launchPosition = Vector3.zero;
			launchTime = 0.0f;
			deathTime = 0.0f;
			homingEndTime = 0.0f;
			speed = 0.0f;
			turnRate = 0.0f;
			explosionRadius = 0.75f;
			armingDelay = 0.0f;
			minimumArmingDistance = 0.0f;
			seekDelay = 0.0f;
			seekRampDuration = 0.0f;
			initialSeekStrength = 1.0f;
			targetAimHeight = 1.0f;
			proximityFuseRadius = 0.0f;
			explodeOnTimeout = true;
			useProximityFuse = false;
			activeAttackData = default;
			timeoutAttackData = default;
			damageLayers = ~0;

			if (rb != null) {
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}

			ApplyCurrentVisualState();
		}

		public void OnSpawned() {
			ResetRuntimeState();
		}

		public void OnDespawned() {
			ResetRuntimeState();
		}

		// Shows explosion hit radius + proximity fuse radius as gizmos (scene view only)
		// TODO: Show explosion predicted impact point as a gizmo (scene view only)
		// NOTE: DEBUG HIT RADIUS (DIRECT = DIRECT DAMAGE | ANYTHING ELSE BUT STILL IN HIT RADIUS = SPLASH DAMAGE)
		private void OnDrawGizmos() {
			//if (hasPredictedHit == false) {
			//	return;
			//}

			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(transform.position, explosionRadius);

			if (useProximityFuse && proximityFuseRadius > 0.0f) {
				Gizmos.color = Color.cyan;
				Gizmos.DrawWireSphere(transform.position, proximityFuseRadius);
			}
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