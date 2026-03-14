using UnityEngine;
using Game.AI; // IEnemyAgent namespace
using Game.Combat.Projectiles; // DroneProjectile & DroneHomingProjectile namespace

namespace Game.AI.Drone {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class DroneEnemy : MonoBehaviour, IEnemyAgent, IFactionOwner, IHealthSettings {
		// Implement IFactionOwner
		public Faction OwnerFaction => Faction.Enemy;

		// Implement IHealthSettings
		public float MaxHealth => maxHealth;
		public float LowHealthThreshold => 0.1f; // TODO: CHANGE THIS FROM MAGIC NUMBER

		[Header("References")]
		[SerializeField] private Transform target;
		//[SerializeField] private NavMeshAgent navMeshAgent; // TODO: Might want nav mesh so it avoids obstacles + flies above them
		[SerializeField] private Animator animator;

		[Header("Stats")]
		[SerializeField] private int maxHealth = 1;

		[Header("Combat")]
		//[SerializeField] private float damage = 1.0f; // USED IN DroneProjectile.cs
		//[SerializeField] private float projectileSpeed = 14.0f; // USED IN DroneProjectile.cs
		[SerializeField] private AttackData projectielAttackData = new AttackData();
		[SerializeField] private GameObject projectilePrefab;
		[SerializeField] private Transform projectileSpawn;

		[Header("Ranges")]
		// NOTE: Can use distance bands later -> Close, Short, Medium, Long, Extreme (not all of these but just for reference)
		[SerializeField] private float minRange = 4.0f;   // too close -> move away
		[SerializeField] private float fireRange = 10.0f; // in this range -> can fire
		[SerializeField] private float desiredRange = 7.0f; // aim to remain within this range
		[SerializeField] private float rangeDeadzone = 0.75f; // deadzone where drone orbits instead of constantly re-adjusting (acts as hysterisis -> prevents oscillation around target distance) 

		[Header("Cooldowns")]
		[SerializeField] private float fireCooldown = 1.5f;

		[Header("Movement")]
		[SerializeField] private bool toggleSmoothRotation = true; // toggle between smooth and responsive rotation
		[SerializeField] private float rotationSpeed = 300.0f; // degrees per second (300 - 360 good range)
		[SerializeField] private float rotationSmoothing = 8.0f; // smoothing multiplier (7 - 9 good range)

		[Header("Flight")]
		[SerializeField] private float hoverHeight = 3.0f;
		[SerializeField] private float flightMoveSpeed = 4.0f;
		[SerializeField] private float heightLerpSpeed = 8.0f;

		[Header("Dynamic Height")]
		[SerializeField] private bool matchPlayerHeight = true;
		[SerializeField] private float heightOffsetFromPlayer = 3.0f; // drone floats above player
		[SerializeField] private float minWorldY = -100.0f;
		[SerializeField] private float maxWorldY = 100.0f;
		[SerializeField] private float verticalFollowSpeed = 6.0f; // how quickly it tracks height changes

		[Header("Knockdown")]
		[SerializeField] private float knockdownDuration = 1.2f;

		[Header("Attack Lock Times (prevents spam)")]
		[SerializeField] private float fireLockTime = 0.50f; // anim length (approx)

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; } // shared 'IsStunned' node for drone enemy uses 'IsKnockedDown'
		public bool HasTarget => target != null;
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

		// - Drone Enemy Specific Properties [END] -

		// Cooldown/State timers
		private float nextFireTime = -Mathf.Infinity;
		private float knockdownEndTime = -Mathf.Infinity;
		private float attackEndTime = -Mathf.Infinity; // enforce min attack time (safety for anim not firing)

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed"); // Float
		private static readonly int AnimFire = Animator.StringToHash("Fire"); // Trigger
		private static readonly int AnimKnocked = Animator.StringToHash("KnockedDown"); // Trigger
		private static readonly int AnimHit = Animator.StringToHash("Hit"); // Trigger
		private static readonly int AnimDie = Animator.StringToHash("Die"); // Trigger

		// - Cached Components -
		private Health healthComponent;

		// Runtime params
		private float groundY;
		[SerializeField] private float currentHealth;
		[SerializeField] private float previousHealthValue = 0.0f;
		[SerializeField] private float lastDamageTime = -Mathf.Infinity; // TODO: use for invunerability window

		private void Awake() {
			// Sync health
			currentHealth = maxHealth;

			groundY = transform.position.y;

			if (animator == null) {
				animator = GetComponentInChildren<Animator>();
			}
		}

		private void Update() {
			if (IsDead == true) {
				return;
			}

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

		// PROTOTYPE: Find player automatically
		private void TryFindPlayer() {
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null) {
				target = player.transform;
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
			// PROTOTYPE: not tracking velocity precisely
			// Instead return 0 or an estimate at the moment
			return 0.0f;
		}

		// - Implement IEnemyAgent Methods [START] -

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

			// Play death animation for drone enemy
			if (animator != null) {
				animator.SetTrigger(AnimDie);
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

			awayDirection.Normalize();

			// Move towards away direction
			Vector3 move = awayDirection * flightMoveSpeed * Time.deltaTime;
			transform.position += move;

			FaceTarget(target.position);
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

			Vector3 direction;
			if (distance > desiredRange + rangeDeadzone) {
				direction = toTarget.normalized; // move closer
			}
			else if (distance < desiredRange - rangeDeadzone) {
				direction = -toTarget.normalized; // move away
			}
			else {
				// Create vector perpendicular to target direction (circles rather than moving directly in/out)
				direction = Vector3.Cross(Vector3.up, toTarget.normalized); // orbit sideways
			}
			
			// Move towwards direction
			transform.position += direction * flightMoveSpeed * Time.deltaTime;

			FaceTarget(target.position);
		}

		public bool TryStartFire() {
			if (CanFire == false) {
				return false;
			}
			if (HasTarget == false) {
				return false;
			}
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
				projectileHitbox.Initialise(projectielAttackData);
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
			currentHealth -= amount;

			// Death on 0 health
			if (currentHealth <= 0) {
				Die();
				return;
			}

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
				healthComponent.OnHealthChanged += OnHealthChanged;
			}
		}
		private void OnDisable() {
			if (healthComponent != null) {
				healthComponent.OnHealthChanged -= OnHealthChanged;
			}
		}

		private void OnHealthChanged(float current, float max) {
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