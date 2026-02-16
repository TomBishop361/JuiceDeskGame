using UnityEngine;
using UnityEngine.AI;
using Game.AI;
using Game.AI.Behavior.Shield; // IEnemyAgent namespace

namespace Game.AI.Shield {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class ShieldEnemy : MonoBehaviour, IEnemyAgent, IFactionOwner {
		// Implement IFactionOwner
		public Faction OwnerFaction => Faction.Enemy;

		[Header("References")]
		[SerializeField] private Transform target;
		[SerializeField] private NavMeshAgent navMeshAgent;
		[SerializeField] private Animator animator;

		[Header("Stats")]
		[SerializeField] private int health = 5;

		[Header("Combat")]
		[SerializeField] private float damage = 1.0f; // TODO: punch dmg + slam dmg
		[SerializeField] private AttackData bulletAttackData = new AttackData();

		[Header("Ranges")]
		[Tooltip("Minimum range that shield enemy can perform punch attack (should be less than 'slam range'")]
		[SerializeField] private float punchRange = 1.6f;
		[Tooltip("Minimum range that shield enemy can perform slam attack (should be greater than 'punch range'")]
		[SerializeField] private float slamRange = 2.4f;

		[Header("Cooldowns")]
		[SerializeField] private float punchCooldown = 1.0f;
		[SerializeField] private float slamCooldown = 3.0f;

		[Header("Movement")]
		[SerializeField] private bool toggleSmoothRotation = false; // toggle between reactive rotation & smooth rotation
		[SerializeField] private float rotationSpeed = 180.0f; // degrees per second (120 - 240 good range)
		[SerializeField] private float rotationSmoothing = 6.0f; // smoothing multiplier (6 - 8 good range)

		[Header("Stun")]
		[SerializeField] private float hitStunDuration = 0.4f;

		[Header("Attack Lock Times (prevents spam)")]
		[SerializeField] private float punchLockTime = 0.85f; // anim length (approx)
		[SerializeField] private float slamLockTime = 1.0f; // anim length (approx)

		[Header("Grapple Window")]
		[SerializeField] private float grappleWindowDuration = 1.0f;

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; }
		public bool HasTarget => target != null;
		//public bool HasLOS =>{ get; private set; }

		public float DistanceToTarget { get; private set; }
		public bool InAttackRange => HasTarget && (InPunchRange || InSlamRange); // shared 'InAttackRange' node for shield enemy can mean 'InPunchRange' OR 'InSlamRange'

		public bool CanAttack => CanPunch; // shared 'CanAttack' node for shield enemy can mean 'CanPunch' (default primary attack)
		public bool IsAttacking { get; private set; }
		//public bool IsTargetTooClose { get; private set; }

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [END] -

		// - Shield Enemy Specific Properties [START] -

		public bool InPunchRange => HasTarget && DistanceToTarget <= punchRange;
		public bool InSlamRange => HasTarget && DistanceToTarget <= slamRange;
		public bool CanPunch => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextPunchTime;
		public bool CanSlam => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextSlamTime;
		public bool GrappleWindowOpen { get; private set; }

		// - Shield Enemy Specific Properties [END] -

		// Exposed params (visible in the inspector)
		[SerializeField] private float grappleWindowEndTime;

		// Cooldown/State timers
		private float nextPunchTime = -Mathf.Infinity;
		private float nextSlamTime = -Mathf.Infinity;
		private float stunEndTime = -Mathf.Infinity;
		private float attackEndTime = -Mathf.Infinity; // enforce min attack time (safety for anim not firing)

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed"); // Float
		private static readonly int AnimPunch = Animator.StringToHash("Punch"); // Trigger
		private static readonly int AnimSlam = Animator.StringToHash("Slam"); // Trigger
		private static readonly int AnimShieldRaised = Animator.StringToHash("ShieldRaised"); // Bool
		private static readonly int AnimHit = Animator.StringToHash("Hit"); // Trigger
		private static readonly int AnimDie = Animator.StringToHash("Die"); // Trigger

		// Reset to default values
		private void Reset() {
			navMeshAgent = GetComponent<NavMeshAgent>();
			animator = GetComponentInChildren<Animator>();
		}

		private void Awake() {
			if (navMeshAgent == null) {
				navMeshAgent = GetComponent<NavMeshAgent>();
			}

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

			// Handle stun timer
			if (IsStunned == true && Time.time >= stunEndTime) {
				IsStunned = false;
			}

			// Handle grapple window timer
			// NOTE: SHIELD ENEMY WILL DECIDE WHETHER PLAYER CAN GRAPPLE
			// IDEA: GRAPPLING COULD BE LIKE DEALING DAMAGE -> THE SHIELD ENEMY'S HURTBOX CAN DECIDE THE OUTCOME
			if (GrappleWindowOpen == true && Time.time >= grappleWindowEndTime) {
				GrappleWindowOpen = false;
			}

			// Handle Attack lock timer (prevents immediate re-trigger spam even if anim event misfires)
			// NOTE: THIS ANIM EVENT IS A SAFETY NET IN CASE ANIM DOES NOT FIRE OR ISN'T WIRED CORRECTLY
			if (IsAttacking && Time.time >= attackEndTime) {
				IsAttacking = false;
			}

			// Animator movement speed
			if (navMeshAgent != null && animator != null) {
				animator.SetFloat(AnimMoveSpeed, navMeshAgent.velocity.magnitude);
			}
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
			GrappleWindowOpen = false;

			// Disable nav mesh agent upon death
			if (navMeshAgent != null) {
				navMeshAgent.isStopped = true;
				navMeshAgent.enabled = false;
			}

			// Play death animation for shield enemy
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

		// Shared 'Chase' action node uses this (so we need to implement it)
		// For shield enemy specific movement, we are using 'AdvanceRaised' action node
		public void ChaseTargetTick() {
			AdvanceRaisedTick();
		}

		public void StopMove() {
			if (navMeshAgent != null) {
				navMeshAgent.isStopped = true;
			}
		}
		// Shared action node 'PrimaryAttack' can be used
		// But for sword map primary attack to 'Swing' instead

		// Shared action node 'PrimaryAttack' can be used
		// But for shield we want 'Punch' Vs 'Slam' selection in the BT for shield enemy combat actions
		public bool TryStartPrimaryAttack() {
			return TryStartPunch();
		}

		// - Implement IEnemyAgent Methods [END] -

		// - Shield Enemy Specifc BT Action Node Execution -

		public void AdvanceRaisedTick() {
			// Advanced raised interrupts
			if (IsDead == true || IsStunned == true || IsAttacking == true) {
				return;
			}
			// No target
			if (HasTarget == false || navMeshAgent == null) {
				return;
			}

			navMeshAgent.isStopped = false;
			navMeshAgent.SetDestination(target.position);

			// Continuously adjust direction when advanceing towards target
			FaceTarget(target.position);

			// Play shield raised animation whilst advancing
			if (animator != null) {
				animator.SetBool(AnimShieldRaised, true);
			}
		}

		public bool TryStartPunch() {
			// Punch interrupts
			if (IsDead == true || IsStunned == true || IsAttacking == true) {
				return false;
			}
			// No target
			if (HasTarget == false) {
				return false;
			}
			// Punch is out of range
			if (InPunchRange == false) {
				return false;
			}
			// Punch is still on cooldown
			if (Time.time < nextPunchTime) {
				return false;
			}

			StopMove();

			IsAttacking = true;
			nextPunchTime = Time.time + punchCooldown;
			attackEndTime = Time.time + punchLockTime;

			if (animator != null) {
				animator.SetBool(AnimShieldRaised, true);
				animator.SetTrigger(AnimPunch);
			}

			return true;
		}

		public bool TryStartSlam() {
			// Slam interrupts
			if (IsDead == true || IsStunned == true || IsAttacking == true) {
				return false;
			}
			// No target
			if (HasTarget == false) {
				return false;
			}
			// Slam is out of range
			if (InSlamRange == false) {
				return false;
			}
			// Slam is still on cooldown
			if (Time.time < nextSlamTime) {
				return false;
			}

			StopMove();

			IsAttacking = true;
			nextSlamTime = Time.time + slamCooldown;
			attackEndTime = Time.time + slamLockTime;

			// PROTOTYPE: Open grapple window immediately (or trigger it via anim event)
			GrappleWindowOpen = true;
			grappleWindowEndTime = Time.time + grappleWindowDuration;

			if (animator != null) {
				animator.SetBool(AnimShieldRaised, true);
				animator.SetTrigger(AnimSlam);
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
			if (IsDead) return;

			IsStunned = true;
			IsAttacking = false;
			stunEndTime = Time.time + duration;

			StopMove();
		}

		// - Animation Events -

		// Call this at end of attack animation
		public void AnimEvent_AttackFinished() {
			IsAttacking = false;
			// PROTOTYPE: keep shield raised during combat
			// To lower it -> set 'ShieldRaised' FALSE somewhere
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
			}
			else {
				// Responsive rotation towards target direction (using RotateTowards)
				// [BETTER FOR SHIELD since heavy, deliberate]
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}
	}
}

// - DEPRECATED -

// REASON: No longer needed as TryStartPunch() & TryStartSlam() are exposed to BT -> BT decides which attack not C#
// Shared action node 'PrimaryAttack' can be used
// But for shield we want 'Punch' Vs 'Slam' selection in the BT for shield enemy combat actions
//public bool TryStartPrimaryAttack() {
//	// PROTOTYPE fallback: if slam is ready and not too close to target -> slam, otherwise punch
//	if (CanSlam == true && InSlamRange == true && InPunchRange == false) {
//		return TryStartSlam();
//	}

//	// Punch if not in slam attack range
//	return TryStartPunch();
//}