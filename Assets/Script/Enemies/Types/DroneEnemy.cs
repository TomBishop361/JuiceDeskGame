using UnityEngine;
using System.Collections;
using System;
using Game.AI; // IEnemyAgent namespace

namespace Game.AI.Drone {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class DroneEnemy : MonoBehaviour, IEnemyAgent {

		[Header("References")]
		[SerializeField] private Transform target;
		//[SerializeField] private NavMeshAgent navMeshAgent;
		[SerializeField] private Animator animator;

		[Header("Stats")]
		[SerializeField] private int health = 1;

		[Header("Combat")]
		[SerializeField] private float damage = 1.0f; // TODO: projectileDamage
		[SerializeField] private float projectileSpeed = 14.0f;
		[SerializeField] private GameObject projectilePrefab;
		[SerializeField] private Transform projectileSpawn;

		[Header("Ranges")]
		[SerializeField] private float minRange = 4.0f;   // too close -> move away
		[SerializeField] private float fireRange = 10.0f; // in this range -> can fire
		[SerializeField] private float desiredRange = 7.0f;
		[SerializeField] private float rangeDeadzone = 0.75f; // deadzone where drone orbits instead of constantly re-adjusting (hysterisis prevents oscillation around target distance) 

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

		[Header("Knockdown")]
		[SerializeField] private float knockdownDuration = 1.2f;

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; } // use knocked down as 'stunned' for shared nodes
		public bool HasTarget => target != null;
		//public bool HasLOS =>{ get; private set; }

		public float DistanceToTarget { get; private set; }
		public bool InAttackRange => InFireRange; // Drone: 'attack range' = fire range

		public bool CanAttack => CanFire;
		public bool IsAttacking { get; private set; }
		//public bool IsTargetTooClose { get; private set; }

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [END] -

		// - Drone Enemy Specific Properties [START] -

		public bool IsKnockedDown { get; private set; }
		public bool InFireRange => HasTarget && DistanceToTarget <= fireRange;
		public bool PlayerTooClose => HasTarget && DistanceToTarget < minRange;
		public bool CanFire => !IsDead && !IsKnockedDown && !IsAttacking && Time.time >= nextFireTime;

		// - Drone Enemy Specific Properties [END] -

		// Cooldown timers
		private float nextFireTime;
		private float knockdownEndTime;
		private float attackEndTime; // enforce min attack time [DELETE LATER]

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed");
		private static readonly int AnimFire = Animator.StringToHash("Fire");
		private static readonly int AnimKnocked = Animator.StringToHash("KnockedDown");
		private static readonly int AnimHit = Animator.StringToHash("Hit");
		private static readonly int AnimDie = Animator.StringToHash("Die");

		// Runtime params
		private float groundY;

		private void Awake() {
			groundY = transform.position.y;

			if (animator == null) {
				animator = GetComponentInChildren<Animator>();
			}
		}

		private void Update() {
			if (IsDead == true) {
				return;
			}

			// Auto acquire target (in prototype)
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

			// Move speed (Prototype: simple debug)
			if (animator != null) {
				animator.SetFloat(AnimMoveSpeed, CurrentHorizontalSpeed());
			}		
		}

		// Find player automatically (in prototype)
		private void TryFindPlayer() {
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null) {
				target = player.transform;
			}
		}

		// Called in Update() function (as long as drone is not knocked down)
		private void MaintainHoverHeight() {
			Vector3 position = transform.position;
			float targetY = groundY + hoverHeight;

			// Attempt to maintain height (constantly attempt to reach target height - hovering)
			// Drone is actively hovering whilst adjusting
			position.y = Mathf.Lerp(position.y, targetY, Time.deltaTime * heightLerpSpeed);

			// Move towards target height
			transform.position = position;
		}

		// Prototype: Called in Update() function
		private float CurrentHorizontalSpeed() {
			// Prototype: not tracking velocity precisely
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

		// Shared action node 'PrimaryAttack' can be used, but for drone we want
		// firing so map primary attack to that instead
		public bool TryStartPrimaryAttack() {
			return TryStartFire();
		}

		// - Implement IEnemyAgent Methods [END] -

		// - Drone Enemy Specifc Action Node Execution -

		public void RecoverFromKnockdownTick() {
			// While knocked down, remain at ground height
			Vector3 position = transform.position;

			// Attempt to maintain ground height (constantly attempt to reach target height - hovering)
			// Drone is knocked but still hovering slightly
			position.y = Mathf.Lerp(position.y, groundY, Time.deltaTime * heightLerpSpeed);

			transform.position = position;

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

			// Simple band control: if too far -> move closer | if too close -> move away |
			// otherwise small orbit motion
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
				
			IsAttacking = true;
			nextFireTime = Time.time + fireCooldown;

			if (animator != null) {
				animator.SetTrigger(AnimFire);
			}
				
			// Prototype: Fire immediately (Move this to an Anim event later)
			FireProjectileNow();

			return true;
		}

		private void FireProjectileNow() {
			if (projectilePrefab == null || projectileSpawn == null || HasTarget == false) {
				return;
			}

			// Get direction away from drone
			Vector3 direction = target.position - projectileSpawn.position.normalized;

			// Instantiate Projectile GameObject
			GameObject projectileObj = Instantiate(projectilePrefab, projectileSpawn.position, Quaternion.LookRotation(direction));

			if (projectileObj.TryGetComponent<Rigidbody>(out Rigidbody rigidbody)) {
				rigidbody.linearVelocity = direction * projectileSpeed;
			}
		}

		// - Damge / Knock -

		public void TakeDamage(int amount) {
			if (IsDead == true) {
				return;
			}

			// Decrement health by 'x' amount
			health -= amount;

			// Death on 0 health
			if (health <= 0) {
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
	}
}