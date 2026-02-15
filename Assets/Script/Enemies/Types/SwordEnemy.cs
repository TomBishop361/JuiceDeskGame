using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Game.AI; // IEnemyAgent namespace

namespace Game.AI.Sword {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class SwordEnemy : MonoBehaviour, IEnemyAgent {

		[Header("References")]
		[SerializeField] private Transform target;
		[SerializeField] private NavMeshAgent navMeshAgent;
		//[SerializeField] private Rigidbody rb; // NOTE: ONLY NEED IF USING PHYSICS FOR LUNGE ATTACK
		[SerializeField] private Animator animator;

		[Header("Stats")]
		[SerializeField] private int health = 3;
		[SerializeField] private float defaultSpeed = 3.5f;
		[SerializeField] private float defaultAcceleration = 8.0f;

		[Header("Combat")]
		[SerializeField] private float damage = 2.0f; // TODO: different attack damages (swing: 1 + lunge: 2)

		[Header("Ranges")]
		[SerializeField] private float swingRange = 1.8f;
		[SerializeField] private float lungeMinRange = 3.2f; // start lunge attack if player is at least this far
		[SerializeField] private float lungeMaxRange = 5.2f; // don't lunge attack if player exceeds this range

		[Header("Cooldowns")]
		[SerializeField] private float swingCooldown = 1.0f;
		[SerializeField] private float lungeCooldown = 2.0f; // good ranges: 1.8-2.5

		[Header("Movement")]
		[SerializeField] private bool toggleSmoothRotation = false; // toggle between reactive rotation & smooth rotation
		[SerializeField] private float rotationSpeed = 360.0f; // degrees per second
		[SerializeField] private float rotationSmoothing = 12.0f; // smoothing multiplier (12 - 15 good range)

		[Header("Stun")]
		[SerializeField] private float hitStunDuration = 0.6f;

		[Header("Attack Lock Times (prevents spam)")]
		[SerializeField] private float swingLockTime = 0.55f; // anim length (approx)
		[SerializeField] private float lungeLockTime = 0.75f; // anim length (approx)

		[Header("Lunge Settings")]
		[SerializeField] private float lungeSpeed = 8.0f; // good ranges: 7-10 [NOTE: 20 for extreme difficulty)
		[SerializeField] private float lungeAcceleration = 16.0f;
		[SerializeField] private float lungeDistance = 1.5f; // distance to lunge forward (uses stopping distance below to ensure no overshooting)
		[SerializeField] private float lungeStopDistance = 0.6f; // makes sure to not overshoot into the player | good ranges: 0.6-0.9
		[SerializeField] private float lungeDuration = 0.3f; // good ranges: 0.18-0.30
		[SerializeField] private LayerMask lungeBlockers; // use for walls and obstacles 
		//[SerializeField] private float lungeForce = 15.0f; // NOTE: ONLY NEED IF USING PHYSICS FOR LUNGE ATTACK
		//[SerializeField] private float lungePastPlayer = 1.0f; // lunge overshoot amount
		//[SerializeField] private float lungeWindupTime  = 0.1f; // telegraphs lunge attack - so player can react
		//[SerializeField] private float lungeRecoverTime = 0.6f;
		

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; }
		public bool HasTarget => target != null;
		//public bool HasLOS =>{ get; private set; }

		public float DistanceToTarget { get; private set; }
		public bool InAttackRange => HasTarget && (InSwingRange || InLungeRange); // shared 'InAttackRange' node for sword enemy can mean 'InSwingRange' OR 'InLungeRange'

		public bool CanAttack => CanSwing; // shared 'CanAttack' node for sword enemy can mean 'CanSwing' (default primary attack)
		public bool IsAttacking { get; private set; }
		//public bool IsTargetTooClose { get; private set; }

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [END] -

		// - Sword Enemy Specific Properties [START] -

		public bool InSwingRange => HasTarget && DistanceToTarget <= swingRange;
		public bool InLungeRange => HasTarget && DistanceToTarget >= lungeMinRange && DistanceToTarget <= lungeMaxRange;
		public bool ShouldLunge => InLungeRange && !InSwingRange;
		public bool CanSwing => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextSwingTime;
		public bool CanLunge => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextLungeTime;

		// - Sword Enemy Specific Properties [END] -

		// Cooldown/State timers
		private float nextSwingTime = -Mathf.Infinity;
		private float nextLungeTime = -Mathf.Infinity;
		private float stunEndTime = -Mathf.Infinity;
		private float attackEndTime = -Mathf.Infinity; // enforce min attack time (safety for anim not firing)
		private float lungeEndTime = -Mathf.Infinity;

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed"); // Float
		private static readonly int AnimSwing = Animator.StringToHash("Swing"); // Trigger
		private static readonly int AnimLunge = Animator.StringToHash("Lunge"); // Trigger
		private static readonly int AnimHit = Animator.StringToHash("Hit"); // Trigger
		private static readonly int AnimDie = Animator.StringToHash("Die"); // Trigger

		// Runtime params
		[SerializeField] private bool isLunging = false;

		// Reset to default values
		private void Reset() {
			navMeshAgent = GetComponent<NavMeshAgent>();
			// rb = GetComponent<Rigidbody>(); // NOTE: ONLY NEED IF USING PHYSICS FOR LUNGE ATTACK
			animator = GetComponentInChildren<Animator>();
		}

		private void Awake() {
			if (navMeshAgent == null) {
				navMeshAgent = GetComponent<NavMeshAgent>();
			}

			//if (rb == null) {
			//	rb = GetComponent<Rigidbody>(); // NOTE: ONLY NEED IF USING PHYSICS FOR LUNGE ATTACK
			//	rb.isKinematic = true; // NavMesh controls movement
			//}

			if (animator == null) {
				animator = GetComponentInChildren<Animator>();
			}

			// Set Default values (NOTE: put in a function later)
			navMeshAgent.speed = defaultSpeed;
			navMeshAgent.acceleration = defaultAcceleration;
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
				
			// Handle stun timer
			if (IsStunned == true && Time.time >= stunEndTime) {
				IsStunned = false;
			}

			// Handle Attack lock timer (prevents immediate re-trigger spam even if anim event misfires)
			// NOTE: THIS ANIM EVENT IS A SAFETY NET IN CASE ANIM DOES NOT FIRE OR ISN'T WIRED CORRECTLY
			if (IsAttacking && Time.time >= attackEndTime) {
				IsAttacking = false;
			}

			// Perform lunge attack
			if (isLunging == true) {
				LungeTick();
			}

			// Animator movement speed
			// NOTE: Can do Root motion lunge attack where animation drives movement for more precision (requires good animations but is typically the most polished)
			if (navMeshAgent != null && animator != null) {
				animator.SetFloat(AnimMoveSpeed, navMeshAgent.velocity.magnitude);
			}		
		}

		private void LungeTick() {
			if (navMeshAgent == null) {
				isLunging = false;
				return;
			}
			// IMPORTANT NOTE: CONDITIONS LIKE THIS SHOULD BE CHECKED IN ONE PLACE (SOME ARE BEING CHECKED IN BT AND CODE ATM I BELIVE)
			if (HasTarget == false) {
				isLunging = false;
				return;
			}

			// Finish lunge attack before 'lungeEndTime' (safety for anim not firing correctly)
			if (Time.time >= lungeEndTime) {
				isLunging = false;
				return;
			}

			// Don't overshoot into the player
			if (DistanceToTarget <= lungeStopDistance) {
				isLunging = false;
				return;
			}

			// Get forward direction
			Vector3 forward = transform.forward;

			// OPTIONAL: Stop lunging if there is a wall in front of the sword enemy
			// 0.8f = height offset to check for obstacles | 0.6f = max ray distance
			if (Physics.Raycast(transform.position + Vector3.up * 0.8f, forward, 0.6f, lungeBlockers)) {
				isLunging = false;
				return;
			}

			// Calculate target point (past the player for commitment - deliberate overshoot on miss)
			//Vector3 direction = (target.position - transform.position).normalized;
			//Vector3 lungeTarget = target.position + direction * lungePastPlayer;

			// #1 Lunge forward (sword enemy remains on navmesh)
			// + Best way to work with NavMesh since using '.Move' and keeping nav active | - idk at the moment
			//navMeshAgent.speed = lungeSpeed;
			//navMeshAgent.acceleration = lungeAcceleration;
			navMeshAgent.isStopped = true;
			navMeshAgent.Move(forward * (lungeSpeed * Time.deltaTime)); // set speed + accel to test (possibly)

			// #2 Increase NavMesh agent speed and set lunge destination (bursts forward - but uses normal pathing)
			// + Simple | - Pathfinding can curve the lunge if NavMesh agent is avoiding obstacles
			//navMeshAgent.speed = lungeSpeed * 1.5f;// 150% of default speed
			//navMeshAgent.acceleration = acceleration * 1.5f; // 150% of default acceleration
			//navMeshAgent.SetDestination(transform.position + forward * lungeDistance);
			// NOTE: USING RestoreSpeedAccel() with the above method (#2)

			// #3 Disable NavMesh agent + use Rigidbody to dash
			// + Uses physics | - Have to resync NavMesh agent afte (can be difficult to get right)
			//navMeshAgent.enabled = false;
			// Use Rigidbody here

			// Restore normal stats
			//navMeshAgent.speed = defaultSpeed;
			//navMeshAgent.acceleration = defaultAcceleration;
		}

		// PROTOTYPE: Find player automatically
		private void TryFindPlayer() {
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null) {
				target = player.transform;
			}		
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
			IsStunned = false;
			IsAttacking = false;

			// Disable nav mesh agent upon death
			if (navMeshAgent != null) {
				navMeshAgent.isStopped = true;
				navMeshAgent.enabled = false;
			}

			// Play death animation for sword enemy
			if (animator != null) {
				animator.SetTrigger(AnimDie);
			}
		}

		public void RecoverTick() {
			if (IsDead == true) {
				return;
			}

			StopMove();
		}

		public void ChaseTargetTick() {
			// Chase interrupts
			if (IsDead == true || IsStunned == true || IsAttacking == true) {
				return;
			}
			// No target
			if (HasTarget == false || navMeshAgent == null) {
				return;
			}
				
			navMeshAgent.isStopped = false;
			navMeshAgent.SetDestination(target.position);

			// Continuously adjust direction when chasing target
			FaceTarget(target.position);
		}

		public void StopMove() {
			if (navMeshAgent != null) {
				navMeshAgent.isStopped = true;
			}	
		}

		// Shared action node 'PrimaryAttack' can be used
		// But for sword map primary attack to 'Swing' instead (default attack)
		public bool TryStartPrimaryAttack() {
			return TryStartSwing();
		}

		// - Implement IEnemyAgent Methods [END] -

		// - Sword Enemy Specifc Action Node Execution -

		public bool TryStartSwing() {
			if (CanSwing == false) {
				return false;
			}
			// OPTIONAL: Remove if you want swing attack to begin slightly out of range
			if (InSwingRange == false) {
				return false;
			}

			StopMove();

			IsAttacking = true;
			nextSwingTime = Time.time + swingCooldown;
			attackEndTime = Time.time + swingLockTime;

			if (animator != null) {
				animator.SetTrigger(AnimSwing);
			}
				
			return true;
		}

		public bool TryStartLunge() {
			if (CanLunge == false) {
				return false;
			}
			if (InLungeRange == false) {
				return false;
			}

			StopMove();

			IsAttacking = true;
			isLunging = true;
			lungeEndTime = Time.time + lungeDuration;

			nextLungeTime = Time.time + lungeCooldown;
			attackEndTime = Time.time + lungeLockTime;

			if (animator != null) {
				animator.SetTrigger(AnimLunge);
			}

			return true;
		}

		// - Damge / Stun -

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

			Stun(hitStunDuration);

			if (animator != null) {
				animator.SetTrigger(AnimHit);
			}
		}

		// Prevent/Interrupt enemy from attacking once they take damage (for a 'short' time)
		private void Stun(float duration) {
			if (IsDead == true) {
				return;
			}

			IsStunned = true;
			IsAttacking = false;
			stunEndTime = Time.time + duration;

			StopMove();
		}

		// - Animation Events -

		// Call this at end of attack animation
		public void AnimEvent_AttackFinished() {
			IsAttacking = false;
		}

		// Hitbox toggles (call inside anim event)
		public void AnimEvent_EnableHitbox() {
			// TODO: ADD LOGIC TO AnimEvent_EnableHitbox()
		}
		public void AnimEvent_DisableHitbox() {
			// TODO: ADD LOGIC TO AnimEvent_DisableHitbox()
		}

		// - Movement Helpers -
		// TODO: Place inside MovementHelpers.cs script later

		// Turn to face the targets direction (used when chasing to maintain LOS)
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
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothing);
			} else {
				// Responsive rotation towards target direction (using RotateTowards)
				// [BETTER FOR SWORD since precise, melee facing]
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}
	}
}

// NOTE: CAN USE LATER FOR LUNGE ATTACK - Physics Method

//public void StartLungeAttack() {
//	if (isLunging == true)
//		StartCoroutine(LungeCoroutine());
//}

//private IEnumerator LungeCoroutine() {
//	if (navMeshAgent == null) {
//		isLunging = false;
//		yield return null;
//	}
//	// IMPORTANT NOTE: CONDITIONS LIKE THIS SHOULD BE CHECKED IN ONE PLACE (SOME ARE BEING CHECKED IN BT AND CODE ATM I BELIVE)
//	if (HasTarget == false) {
//		isLunging = false;
//		yield return null;
//	}

//	// Finish lunge attack before 'lungeEndTime' (safety for anim not firing correctly)
//	if (Time.time >= lungeEndTime) {
//		isLunging = false;
//		yield return null;
//	}

//	// Don't overshoot into the player
//	if (DistanceToTarget <= lungeStopDistance) {
//		isLunging = false;
//		yield return null;
//	}

//	// Wind up phase
//	navMeshAgent.isStopped = true;

//	// Get forward direction
//	Vector3 lungeDirection = (target.position - transform.position).normalized;
//	lungeDirection.y = 0.0f;

//	// Face player during windup - use helper later
//	Quaternion targetRotation =  Quaternion.LookRotation(lungeDirection);
//	float windupTimer = 0.0f;

//	while (windupTimer < lungeWindupTime) {
//		transform.rotation =  Quaternion.Slerp( transform.rotation, targetRotation, windupTimer / lungeWindupTime);
//		windupTimer += Time.deltaTime;
//		// TODO: Trigger windup animation here
//		yield return null;
//	}

//	// Lunge phase
//	// Disable NavMesh + enable physics
//	navMeshAgent.enabled = false;
//	rb.isKinematic = false;

//	// Apply lunge force
//	rb.AddForce(lungeDirection * lungeForce, ForceMode.VelocityChange);

//	yield return new WaitForSeconds(lungeDuration);

//	// Recovery Phase
//	rb.linearVelocity = Vector3.zero;
//	rb.isKinematic = true;

//	// Re-enable NavMesh (NOTE: Can warp to a valid position - but may look too much like teleporting)
//	// NOTE: Main disadvantage of disabling NavMesh to use physics during lunge
//	if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) {
//		transform.position = hit.position;
//	}

//	navMeshAgent.enabled = true;
//	navMeshAgent.isStopped = false;

//	yield return new WaitForSeconds(lungeRecoverTime);

//	isLunging = false;
//}