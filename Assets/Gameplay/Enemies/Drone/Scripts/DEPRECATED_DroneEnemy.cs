using UnityEngine;
using Game.AI; // IEnemyAgent namespace
using Game.Combat.Projectiles; // DroneProjectile & DroneHomingProjectile namespace
using UnityEngine.AI;
using System.Collections;

namespace Game.AI.Drone {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	[RequireComponent(typeof(Hurtbox))]
	[RequireComponent(typeof(PooledObject))]
	public class DEPRECATED_DroneEnemy : EnemyCombat, IEnemyAgent, IFactionOwner, IHealthSettings, IPoolSpawnHandler {
		// Implement IFactionOwner
		public Faction OwnerFaction => Faction.Enemy;

		// Implement IHealthSettings
		public float MaxHealth => maxHealth;
		public float LowHealthThreshold => 0.1f; // TODO: CHANGE THIS FROM MAGIC NUMBER

		[Header("References")]
		[SerializeField] private Transform target;
		//[SerializeField] private NavMeshAgent navMeshAgent; // TODO: Might want nav mesh so it avoids obstacles + flies above them
		[SerializeField] private Animator animator;
		[SerializeField] private LOSSensor losSensor;
		[SerializeField] private Health healthComponent;

		[Header("Stats")]
		[Tooltip("Maximum health of the drone")]
		[SerializeField] private int maxHealth = 1;

		[Header("Combat")]
		//[SerializeField] private float damage = 1.0f; // USED IN DroneProjectile.cs
		//[SerializeField] private float projectileSpeed = 14.0f; // USED IN DroneProjectile.cs
		[Tooltip("Attack data used to initialise projectile damage and behaviour")]
		[SerializeField] private AttackData projectileAttackData = new AttackData();
		[Tooltip("Projectile prefab the drone will spawn when firing")]
		[SerializeField] private GameObject projectilePrefab;
		[Tooltip("Transform representing where projectiles are spawned from")]
		[SerializeField] private Transform projectileSpawn;

		[Header("Ranges")]
		// NOTE: Can use distance bands later -> Close, Short, Medium, Long, Extreme (not all of these but just for reference)
		[Tooltip("Minimum distance before the drone moves away from the target")]
		[SerializeField] private float minRange = 4.0f;   // too close -> move away
		[Tooltip("Maximum distance at which the drone is allowed to fire")]
		[SerializeField] private float fireRange = 10.0f; // in this range -> can fire
		[Tooltip("Preferred distance the drone tries to maintain from the target")]
		[SerializeField] private float desiredRange = 7.0f; // aim to remain within this range
		[Tooltip("Buffer zone around desired range to prevent constant jittering (hysteresis)")]
		[SerializeField] private float rangeDeadzone = 0.75f; // deadzone where drone orbits instead of constantly re-adjusting (acts as hysterisis -> prevents oscillation around target distance) 

		[Header("Cooldowns")]
		[Tooltip("Time (in seconds) between consecutive shots")]
		[SerializeField] private float fireCooldown = 1.5f;

		[Header("Movement")]
		[Tooltip("Toggle between smooth rotation (Slerp) and snappy rotation (RotateTowards)")]
		[SerializeField] private bool toggleSmoothRotation = true; // toggle between smooth and responsive rotation
		[Tooltip("Rotation speed in degrees per second when using responsive rotation")]
		[SerializeField] private float rotationSpeed = 300.0f; // degrees per second (300 - 360 good range)
		[Tooltip("Smoothing factor for smooth rotation")]
		[SerializeField] private float rotationSmoothing = 8.0f; // smoothing multiplier (7 - 9 good range)

		[Header("Flight")]
		[Tooltip("Base hover height above the ground when not matching player height")]
		[SerializeField] private float hoverHeight = 3.0f;
		[Tooltip("Horizontal movement speed of the drone")]
		[SerializeField] private float flightMoveSpeed = 4.0f;
		[Tooltip("Speed at which the drone returns to its base height")]
		[SerializeField] private float heightLerpSpeed = 8.0f;
		[Tooltip("Speed at which the drone visually follows movement (for smoothing visuals)")]
		[SerializeField] private float visualFollowSpeed = 4.0f;

		[Header("Dynamic Height")]
		[Tooltip("If enabled, drone adjusts its height relative to the player")]
		[SerializeField] private bool matchPlayerHeight = true;
		[Tooltip("Vertical offset above the player's position")]
		[SerializeField] private float heightOffsetFromPlayer = 3.0f; // drone floats above player
		[Tooltip("Minimum world Y position the drone is allowed to reach")]
		[SerializeField] private float minWorldY = -100.0f;
		[Tooltip("Maximum world Y position the drone is allowed to reach")]
		[SerializeField] private float maxWorldY = 100.0f;
		[Tooltip("Speed at which the drone follows vertical changes")]
		[SerializeField] private float verticalFollowSpeed = 6.0f; // how quickly it tracks height changes

		[Header("Drone Separation")]
		[Tooltip("Enable or disable separation behaviour between drones")]
		[SerializeField] private bool enableDroneSeparation = true;
		[Tooltip("Radius within which other drones will be considered for avoidance")]
		[SerializeField] private float separationRadius = 1.75f;
		[Tooltip("Strength of the push force away from nearby drones")]
		[SerializeField] private float separationWeight = 1.35f;
		[Tooltip("Maximum force applied by separation to prevent extreme movement")]
		[SerializeField] private float maxSeparationForce = 1.15f;
		[Tooltip("Small variation added to orbit movement so drones don't overlap paths")]
		[SerializeField] private float orbitJitter = 0.15f; // slight variation so drones don't all choose identical paths

		[Header("Death")]
		[SerializeField] private float deathFallSpeed = 6.0f;
		[SerializeField] private float deathDuration = 0.45f;
		[SerializeField] private ParticleSystem deathFX;

		[Header("Knockdown")]
		[Tooltip("Duration (in seconds) the drone remains knocked down")]
		[SerializeField] private float knockdownDuration = 1.2f;

		[Header("Attack Lock Times (prevents spam)")]
		[Tooltip("Minimum time the drone stays in attack state to prevent animation spam")]
		[SerializeField] private float fireLockTime = 0.50f; // anim length (approx)

		[Header("LOS Memory")]
		[Tooltip("Time (in seconds) the drone remembers the target after losing line of sight")]
		[SerializeField] private float targetMemoryDuration = 1.0f;

		//[Header("Obstacle Avoidance")]
		//[SerializeField] private LayerMask obstacleMask;
		//[SerializeField] private float obstacleCheckDistance = 1.5f;
		//[SerializeField] private float obstacleAvoidanceRadius = 0.4f;
		//[SerializeField] private float obstacleSteerAngle = 35.0f;
		//[SerializeField] private float obstacleSideProbeDistance = 1.25f;
		//[SerializeField] private float obstacleBuffer = 0.05f;

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; } // shared 'IsStunned' node for drone enemy uses 'IsKnockedDown'
		//public bool HasTarget => target != null;
		public bool HasTarget { get; private set; }
		//public bool HasLOS { get; private set; }

		public float DistanceToTarget { get; private set; }
		public bool InAttackRange => InFireRange; // shared 'InAttackRange' node for drone enemy means 'InFireRange'

		public bool CanAttack => CanFire;
		public bool IsAttacking { get; private set; }
		//public bool IsTargetTooClose { get; private set; }

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [END] -

		// - Drone Enemy Specific Properties [START] -

		public bool IsKnockedDown { get; private set; }
		public bool InFireRange => HasTarget && DistanceToTarget <= fireRange;
		public bool TargetTooClose => HasTarget && DistanceToTarget < minRange;
		public bool CanFire => !IsDead && !IsKnockedDown && !IsAttacking && Time.time >= nextFireTime;
		public bool HasLineOfSight { get; private set; }

		// - Drone Enemy Specific Properties [END] -

		// Cooldown/State timers
		private float nextFireTime = -Mathf.Infinity;
		private float knockdownEndTime = -Mathf.Infinity;
		private float attackEndTime = -Mathf.Infinity; // enforce min attack time (safety for anim not firing)
		private float lastSeenTime = -Mathf.Infinity;

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed"); // Float
		private static readonly int AnimFire = Animator.StringToHash("Fire"); // Trigger
		private static readonly int AnimKnocked = Animator.StringToHash("KnockedDown"); // Trigger
		private static readonly int AnimHit = Animator.StringToHash("Hit"); // Trigger
		private static readonly int AnimDie = Animator.StringToHash("Die"); // Trigger

		// Runtime params
		private float groundY;
		//private float currentHealth;
		private float previousHealthValue = 0.0f;
		private float lastDamageTime = -Mathf.Infinity; // TODO: use for invunerability window
		private static readonly Collider[] separationHits = new Collider[16];

		// Misc
		private PooledObject pooledObject;

		// Implement IPoolSpawnHandler
		public void OnSpawned() {
			ResetRuntimeToBaseValues();
		}

		public void OnDespawned() {
			// Cleanup temporary effects, target refs etc.
			//HasTarget = false;
			//HasLineOfSight = false;
		}

		private void ResetRuntimeToBaseValues() {
			IsDead = false;
			IsKnockedDown = false;
			IsAttacking = false;
			//HasTarget = false;
			//HasLineOfSight = false;
			DistanceToTarget = Mathf.Infinity;

			nextFireTime = -Mathf.Infinity;
			knockdownEndTime = -Mathf.Infinity;
			attackEndTime = -Mathf.Infinity;
			lastSeenTime = -Mathf.Infinity;
			lastDamageTime = -Mathf.Infinity;

			groundY = transform.position.y;

			if (animator != null) {
				animator.ResetTrigger(AnimFire);
				animator.ResetTrigger(AnimHit);
				animator.ResetTrigger(AnimDie);
				animator.SetBool(AnimKnocked, false);
				//animator.Play(0, 0, 0f); // optional - depends on controller setup
			}

			// Restore health
			if (healthComponent != null) {
				healthComponent.RestoreFullHealth();
				previousHealthValue = healthComponent.CurrentHealth;
			}
		}

		private void Awake() {
			CacheComponents();
			InitialRuntimeSetup();
		}

		// - Initialisation Functions (Called inside Awake()) [START] -

		private void CacheComponents() {
			if (animator == null) {
				animator = GetComponentInChildren<Animator>();
			}
			if (losSensor == null) {
				losSensor = GetComponent<LOSSensor>();
			}
		}

		private void InitialRuntimeSetup() {
			// Sync health
			//currentHealth = maxHealth;

			// Reset ground position
			groundY = transform.position.y;

			pooledObject = GetComponent<PooledObject>();
		}

		// - Initialisation Functions (Called inside Awake()) [END] -

		private void Update() {
			if (IsDead == true) {
				return;
			}

			// LOS checker
			//UpdateTargetAwareness();

			// PROTOTYPE: Auto acquire target
			if (target == null) {
				TryFindPlayer();
			}

			// Fetch distance to target (if target is valid)
			if (HasTarget == true) {
				DistanceToTarget = Vector3.Distance(transform.position, target.position);
			}
				
			// Knockdown timer (similar to 'IsStunned' from shared node)
			if (IsKnockedDown && Time.time >= knockdownEndTime) {
				IsKnockedDown = false;

				if (animator != null) {
					animator.SetBool(AnimKnocked, false);
				}
			}

			// Maintain height (unless knocked down)
			if (IsKnockedDown == false) {
				MaintainHoverHeight();
			}
			else {
				FallToGround();
			}

			// Handle Attack lock timer (prevents immediate re-trigger spam even if anim event misfires)
			// NOTE: THIS ANIM EVENT IS A SAFETY NET IN CASE ANIM DOES NOT FIRE OR ISN'T WIRED CORRECTLY
			if (IsAttacking && Time.time >= attackEndTime) {
				IsAttacking = false;
			}

			if (animator != null) {
				// PROTOTYPE: Move speed (debug setter)
				animator.SetFloat(AnimMoveSpeed, CurrentHorizontalSpeed() /*navMeshAgent.velocity.magnitude*/);
			}
		}

		//// Update drone enemy awareness state (uses LOS to determine this)
		//private void UpdateTargetAwareness() {
		//	// PROTOTYPE: Auto acquire target
		//	if (target == null) {
		//		TryFindPlayer();
		//	}

		//	if (target == null) {
		//		HasLineOfSight = false;
		//		HasTarget = false;
		//		DistanceToTarget = Mathf.Infinity;
		//		return;
		//	}

		//	DistanceToTarget = Vector3.Distance(transform.position, target.position);

		//	if (losSensor != null && losSensor.HasLOS()) {
		//		HasLineOfSight = true;
		//		HasTarget = true;
		//		lastSeenTime = Time.time;
		//	}
		//	else {
		//		// fallback if sensor missing
		//		HasLineOfSight = false;
		//		HasTarget = (Time.time - lastSeenTime) <= targetMemoryDuration;
		//	}
		//}

		// PROTOTYPE: Find player automatically
		private void TryFindPlayer() {
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null) {
				target = player.transform;
				HasTarget = true;
			}
			else {
				target = null;
				HasTarget = false;
			}
		}

		// Dynamically maintain hover height based on enemy movement
		// Called in Update() function (as long as drone is not knocked down)
		private void MaintainHoverHeight() {

			if (matchPlayerHeight == false || target == null) {
				// Fallback to fixed hoverHeight -> around about the spawns groundY (if there is no target)
				float fixedY = groundY + hoverHeight;

				SetHeight(fixedY, verticalFollowSpeed);

				return;
			}

			float desiredY = target.position.y + heightOffsetFromPlayer;

			// Keep drone within world bounds (OPTIONAL)
			desiredY = Mathf.Clamp(desiredY, minWorldY, maxWorldY);

			SetHeight(desiredY, verticalFollowSpeed);
		}

		// Sets the height that the Drone should attempt to maintain
		// Called above in MaintainHoverHeight() function
		private void SetHeight(float desiredY, float followSpeed) {
			Vector3 pos = transform.position;

			pos.y = Mathf.Lerp(pos.y, desiredY, Time.deltaTime * followSpeed);

			transform.position = pos;
		}

		// Called in Update() function (as soon as drone is considered to be knocked down)
		private void FallToGround() {
			Vector3 position = transform.position;

			position.y = Mathf.Lerp(position.y, groundY, Time.deltaTime * heightLerpSpeed);

			transform.position = position;
		}

		// PROTOTYPE: Called in Update() function
		// NOTE: IF DRONE GETS NAV MESH THEN THIS IS NOT REQUIRED
		private float CurrentHorizontalSpeed() {
			return 0.0f;
			// PROTOTYPE: not tracking velocity precisely
			// Instead return 0 or an estimate at the moment
			//return 0.0f;
		}

		// - Implement IEnemyAgent Methods [START] -

		//public void CheckLOS() {
		//	if (gameObject.TryGetComponent(out LOSSensor losSensor)) {
		//		HasTarget = losSensor.HasLOS();
		//	}
		//}

		public void AcquireTarget() {
			TryFindPlayer();
		}

		public void Die() {
			if (IsDead == true) {
				return;
			}

			IsDead = true;
			IsKnockedDown = false;
			IsAttacking = false;
			//HasTarget = false;
			//HasLineOfSight = false;

			TrackDeath();

			// Stop any remaining attack state
			if (animator != null) {
				animator.SetBool(AnimKnocked, false);
				//animator.SetTrigger(AnimDie);
			}

			// TODO: death FX
			if (deathFX != null) {
				Instantiate(deathFX, transform.position, Quaternion.identity);
			}

			// Delay despawn so the death can actually be seen
			StartCoroutine(DeathRoutine());

			//// NOTE: Immediate despawn - no visible death animation
			//// TODO: Use AnimEvent_DeathFinished and remove this
			//if (pooledObject != null && gameObject.activeInHierarchy) {
			//	pooledObject.ReturnToPool();
			//}
		}

		private IEnumerator DeathRoutine() {
			float timer = 0.0f;

			Vector3 startPos = transform.position;
			Vector3 endPos = startPos + Vector3.down * (deathFallSpeed * deathDuration);

			while (timer < deathDuration) {
				timer += Time.deltaTime;

				float t = timer / deathDuration;
				transform.position = Vector3.Lerp(startPos, endPos, t);

				// Small spin while falling
				transform.Rotate(0.0f, 360.0f * Time.deltaTime, 180.0f * Time.deltaTime, Space.Self);

				yield return null;
			}

			if (pooledObject != null && gameObject.activeInHierarchy) {
				pooledObject.ReturnToPool();
			}
		}

		public void RecoverTick() {
			// Shared 'Recover' action node if we eventually want logic for that - place here
			// For drone, knockdown recovery is instaed handled by 'DroneRecoverFromKnockdown' action node
		}

		// Shared 'Chase' action node uses this (so we need to implement it)
		// For drone enemy specific movement, we are using 'MaintainRange' action node
		public void ChaseTargetTick() {
			MaintainRangeTick();
		}
		
		public void StopMove() {
			// Used for manual movement, not persistent movement, so not needed 
			// Required to implement it from IEnemyAgent
		}

		// Shared action node 'PrimaryAttack' can be used
		// But for drone map primary attack to 'Fire' instead (default attack)
		public bool TryStartPrimaryAttack() {
			return TryStartFire();
		}

		// - Implement IEnemyAgent Methods [END] -

		// - Drone Enemy Specifc BT Action Node Execution -

		public void RecoverFromKnockdownTick() {
			//// While knocked down, remain at ground height
			//Vector3 position = transform.position;

			//// Attempt to maintain ground height (constantly attempt to reach target height - hovering)
			//// Drone is knocked but still hovering slightly
			//position.y = Mathf.Lerp(position.y, groundY, Time.deltaTime * heightLerpSpeed);

			//transform.position = position;

			// Face target whilst knocked down (OPTIONAL)
			if (HasTarget == true) {
				FaceTarget(target.position);
			}
		}

		public void MoveAwayTick() {
			if (IsDead == true || IsKnockedDown == true || IsAttacking == true) {
				return;
			}
			// No target
			if (HasTarget == false) {
				return;
			}

			// Calculate direction from target to the drone
			// (drone - target = away from target)
			Vector3 awayDirection = transform.position - target.position;
			awayDirection.y = 0.0f;

			// Safety - ensures that an away direction exists before moving towards it
			if (awayDirection.sqrMagnitude < 0.0001f) {
				awayDirection = transform.forward;
			}

			// Move towards away direction
			MoveInDirection(awayDirection.normalized);

			FaceTarget(target.position);

			//awayDirection.Normalize();

			// Move towards away direction
			//transform.position += awayDirection * flightMoveSpeed * Time.deltaTime;

			// Pick a point away from the target instead of moving directly by transform.position
			//Vector3 desiredPoint = transform.position + awayDirection * 3.0f;

			//MoveToNavPoint(desiredPoint);

			// Avoid obstacles movement temp
			//Vector3 moveDir = GetAvoidedDirection(awayDirection);
			//transform.position += moveDir * flightMoveSpeed * Time.deltaTime;
		}

		private void MoveInDirection(Vector3 baseDirection) {
			if (baseDirection.sqrMagnitude < 0.0001f) {
				return;
			}

			Vector3 desiredDirection = baseDirection.normalized;

			if (enableDroneSeparation == true) {
				desiredDirection += CalculateDroneSeparation();
			}

			desiredDirection.y = 0.0f;

			if (desiredDirection.sqrMagnitude < 0.0001f) {
				desiredDirection = baseDirection.normalized;
			}

			transform.position += desiredDirection.normalized * flightMoveSpeed * Time.deltaTime;
		}

		private Vector3 CalculateDroneSeparation() {
			if (separationRadius <= 0.0f) {
				return Vector3.zero;
			}

			int hitCount = Physics.OverlapSphereNonAlloc(transform.position,separationRadius,separationHits,Physics.AllLayers, QueryTriggerInteraction.Ignore);

			if (hitCount <= 0) {
				return Vector3.zero;
			}

			Vector3 separation = Vector3.zero;

			for (int i = 0; i < hitCount; i++) {
				Collider hit = separationHits[i];
				if (hit == null) {
					continue;
				}

				DroneEnemy otherDrone = hit.GetComponentInParent<DroneEnemy>();
				if (otherDrone == null || otherDrone == this || otherDrone.IsDead == true) {
					continue;
				}

				Vector3 awayFromOther = transform.position - otherDrone.transform.position;
				awayFromOther.y = 0.0f;

				float sqrDistance = awayFromOther.sqrMagnitude;
				if (sqrDistance < 0.0001f) {
					awayFromOther = transform.right;
					sqrDistance = awayFromOther.sqrMagnitude;
				}

				float distance = Mathf.Sqrt(sqrDistance);
				float falloff = 1.0f - Mathf.Clamp01(distance / separationRadius);
				separation += awayFromOther.normalized * falloff;
			}

			if (separation.sqrMagnitude < 0.0001f) {
				return Vector3.zero;
			}

			return Vector3.ClampMagnitude(separation * separationWeight, maxSeparationForce);
		}

		public void MaintainRangeTick() {
			if (IsDead == true || IsKnockedDown == true || IsAttacking == true) {
				return;
			}
			// No target
			if (HasTarget == false) {
				return;
			}

			// Distance band control: if too far -> move closer | if too close -> move away | otherwise small orbit motion
			float distance = DistanceToTarget;

			// Calculate direction from the drone to the target
			// (target - drone  = toward target)
			Vector3 toTarget = target.position - transform.position;
			toTarget.y = 0.0f;

			if (toTarget.sqrMagnitude < 0.0001f) {
				return;
			}

			Vector3 direction;
			if (distance > desiredRange + rangeDeadzone) {
				direction = toTarget.normalized; // move closer
			}
			else if (distance < desiredRange - rangeDeadzone) {
				direction = -toTarget.normalized; // move away
			}
			else {
				// Orbit sideways, with a tiny per-drone variation so they don't all collapse into same path
				Vector3 orbitDir = Vector3.Cross(Vector3.up, toTarget.normalized);
				float jitter = Mathf.Sin((Time.time * 1.5f) + transform.GetInstanceID()) * orbitJitter;
				direction = (orbitDir + transform.right * jitter).normalized;

				// Create vector perpendicular to target direction (circles rather than moving directly in/out)
				//direction = Vector3.Cross(Vector3.up, toTarget.normalized); // orbit sideways
			}

			MoveInDirection(direction);

			// Avoid obstacles movement temp
			//Vector3 moveDir = GetAvoidedDirection(direction);
			//transform.position += moveDir * flightMoveSpeed * Time.deltaTime;

			// Move towwards direction
			//transform.position += direction * flightMoveSpeed * Time.deltaTime;

			// Instead of moving every frame manually, choose a world-space goal for the agent
			//Vector3 desiredPoint = transform.position + direction * 2.5f;

			FaceTarget(target.position);
		}
		
		public bool TryStartFire() {
			if (CanFire == false) {
				return false;
			}
			if (HasTarget == false) {
				return false;
			}
			//if (HasLineOfSight == false) {
			//	return false;
			//}
			if (InFireRange == false) {
				return false;
			}
			if (projectilePrefab == null || projectileSpawn == null || HasTarget == false) {
				return false;
			}

			IsAttacking = true;
			nextFireTime = Time.time + fireCooldown;
			attackEndTime = Time.time + fireLockTime;

			if (animator != null) {
				animator.SetTrigger(AnimFire);
			}

			// PROTOTYPE: Fire immediately (Move this to an Anim event later)
			FireProjectileNow();

			return true;
		}

		private void FireProjectileNow() {
			// Get aim direction towards target (aim at chest height -> + Vector3.up * 1.0f)
			Vector3 direction = (target.position + Vector3.up * 1.0f - projectileSpawn.position);

			// Spawm projectile
			GameObject projectileObj = Instantiate(projectilePrefab, projectileSpawn.position, Quaternion.identity);

			// Supply projectile hitbox with attack data
			ProjectileHitbox projectileHitbox = projectileObj.GetComponentInChildren<ProjectileHitbox>();
			if (projectileHitbox != null) {
				projectileHitbox.Initialise(projectileAttackData);
			}
			else {
				Debug.Log("DroneEnemy: ProjectileHitbox cannot be found on instantiated projectile object " + projectileObj.name);
			}

			// Launch projectile
			//if (projectileObj.TryGetComponent(out DroneProjectile droneProjectile)) {
			//	droneProjectile.Launch(direction);
			//}
			// Launch homing projectile
			if (projectileObj.TryGetComponent(out DroneHomingProjectile droneHomingProjectile)) {
				droneHomingProjectile.Init(target);
				droneHomingProjectile.Launch(direction);
			}

			//// Get direction away from drone
			//Vector3 direction = target.position - projectileSpawn.position.normalized;

			//// Instantiate Projectile GameObject
			//GameObject projectileObj = Instantiate(projectilePrefab, projectileSpawn.position, Quaternion.LookRotation(direction));

			//if (projectileObj.TryGetComponent(out Rigidbody rigidbody)) {
			//	rigidbody.linearVelocity = direction * projectileSpeed;
			//}
		}

		// - Damge / Knock -

		public void TakeDamage(int amount) {
			if (IsDead == true) {
				return;
			}

			// Decrement health by 'x' amount
			//currentHealth -= amount;

			//// Death on 0 health
			//if (currentHealth <= 0) {
			//	Die();
			//	return;
			//}

			if (animator != null) {
				animator.SetTrigger(AnimHit);
			}
		}

		// NOTE: Later on 'knockdownDurationOverride' can be used for boss ability causing longer stun or difficulty scaling etc.
		public void KnockDown(/*float knockdownDurationOverride = -1.0f*/) {
			if (IsDead == true) {
				return;
			}

			IsKnockedDown = true;
			IsAttacking = false;

			// Use override duration if it's been set (> 1)
			//float duration = knockdownDurationOverride > 0 ? knockdownDurationOverride : knockdownDuration;
			knockdownEndTime = Time.time + knockdownDuration;

			if (animator != null) {
				animator.SetBool(AnimKnocked, true);
			}
		}

		// - Animation Events -

		// Call this at end of attack animation
		public void AnimEvent_AttackFinished() {
			IsAttacking = false;
		}

		// TODO: Call this at the end of death animation
		public void AnimEvent_DeathFinished() {
			if (pooledObject == null || gameObject == null) {
				return;
			}

			pooledObject.ReturnToPool();
		}

		// - Movement Helpers -
		// TODO: Place inside MovementHelpers.cs script later

		private void FaceTarget(Vector3 worldPos) {
			Vector3 direction = worldPos - transform.position;
			direction.y = 0.0f;

			// Safety - ensures that a direction exists before rotating towards it
			if (direction.sqrMagnitude < 0.0001f) {
				return;
			}

			Quaternion targetRotation = Quaternion.LookRotation(direction);

			if (toggleSmoothRotation == true) {
				// Smooth rotation towards target direction (using Slerp)
				// [BETTER FOR DRONE since floaty hovering]
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothing);
			}
			else {
				// Responsive rotation towards target direction (using RotateTowards)
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}

		// - Event & Callback handlers -

		private void OnEnable() {
			if (healthComponent != null) {
				previousHealthValue = healthComponent.CurrentHealth;
				healthComponent.OnHealthChanged += HandleHealthChanged;
			}
		}
		private void OnDisable() {
			if (healthComponent != null) {
				healthComponent.OnHealthChanged -= HandleHealthChanged;
			}
		}

		private void HandleHealthChanged(float current, float max) {
			// Death on 0 health
			if (current <= 0) {
				Die();
				return;
			}

			// Trigger hit animation
			if (animator != null) {
				animator.SetTrigger(AnimHit);
			}

			// Damage only if health has gone down
			if (current < previousHealthValue) {
				// Reset regen delay timer - only regen when out of combat
				lastDamageTime = Time.time;
			}
			previousHealthValue = current;
		}
	}
}

// - DEPRECATED -

// REASON: Have a solution to maintain hover height dynamically instead that is based on the players current height through platforming movement
// Called in Update() function (as long as drone is not knocked down)
//private void MaintainHoverHeightOld() {
//	Vector3 position = transform.position;
//	float targetY = groundY + hoverHeight;

//	// Attempt to maintain height (constantly attempt to reach target height - hovering)
//	// Drone is actively hovering whilst adjusting
//	position.y = Mathf.Lerp(position.y, targetY, Time.deltaTime * heightLerpSpeed);

//	// Move towards target height
//	transform.position = position;
//}