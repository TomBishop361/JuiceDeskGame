using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;
using Game.AI; // IEnemyAgent namespace

namespace Game.AI.Shield {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class ShieldEnemy : MonoBehaviour, IEnemyAgent {

		[Header("References")]
		[SerializeField] private Transform target;
		[SerializeField] private NavMeshAgent navMeshAgent;
		[SerializeField] private Animator animator;

		[Header("Stats")]
		[SerializeField] private int health = 5;

		[Header("Combat")]
		[SerializeField] private float damage = 1.0f; // TODO: punch dmg + slam dmg

		[Header("Ranges")]
		[SerializeField] private float punchRange = 1.6f;
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

		[Header("Grapple Window")]
		[SerializeField] private float grappleWindowDuration = 1.0f;

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; }
		public bool HasTarget => target != null;
		//public bool HasLOS =>{ get; private set; }

		public float DistanceToTarget { get; private set; }
		// Shared nodes ask for 'InAttackRange' and 'CanAttack'
		// For shield enemy, 'attack range' can mean punch OR slam
		public bool InAttackRange => HasTarget && (InPunchRange || InSlamRange);

		public bool CanAttack => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextPunchTime; // used for punch
		public bool IsAttacking { get; private set; }
		//public bool IsTargetTooClose { get; private set; }

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [END] -

		// - Shield Enemy Specific Properties [START] -

		public bool InPunchRange => HasTarget && DistanceToTarget <= punchRange;
		public bool InSlamRange => HasTarget && DistanceToTarget <= slamRange;

		//public bool CanPunch => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextPunchTime;
		public bool CanSlam => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextSlamTime;

		public bool GrappleWindowOpen { get; private set; }
		[SerializeField] private float grappleWindowEndTime;

		// - Shield Enemy Specific Properties [END] -

		// Cooldown timers
		private float nextPunchTime;
		private float nextSlamTime;
		private float stunEndTime;
		private float attackEndTime; // enforce min attack time [DELETE LATER]

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed");
		private static readonly int AnimPunch = Animator.StringToHash("Punch");
		private static readonly int AnimSlam = Animator.StringToHash("Slam");
		private static readonly int AnimShieldRaised = Animator.StringToHash("ShieldRaised");
		private static readonly int AnimHit = Animator.StringToHash("Hit");
		private static readonly int AnimDie = Animator.StringToHash("Die");

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

			// Auto acquire target (in prototype)
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
			if (GrappleWindowOpen == true && Time.time >= grappleWindowEndTime) {
				GrappleWindowOpen = false;
			}

			// DELETE LATER START
			if (IsAttacking && Time.time >= attackEndTime) {
				IsAttacking = false;
			}
			// DELETE LATER END

			// Animator movement speed
			if (navMeshAgent != null && animator != null) {
				animator.SetFloat(AnimMoveSpeed, navMeshAgent.velocity.magnitude);
			}
		}

		// Find player automatically (in prototype)
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

		// Shared action node 'PrimaryAttack' can be used, but for shield we want
		// punch vs slam selection in the BT for shield enemy combat actions
		public bool TryStartPrimaryAttack() {
			// Prototype fallback: if slam is ready and not too close to target -> slam, otherwise punch
			if (CanSlam == true && InSlamRange == true && InPunchRange == false) {
				return TryStartSlam();
			}
				
			// Punch if not in slam attack range
			return TryStartPunch();
		}

		// - Implement IEnemyAgent Methods [END] -

		// - Shield Enemy Specifc Action Node Execution -

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

		// Called by TryStartPrimaryAttack() function as fallback if slam attack is not possible
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
			attackEndTime = Time.time + 0.3f; // DELETE LATER (match potential animation length)

			if (animator != null) {
				animator.SetBool(AnimShieldRaised, true);
				animator.SetTrigger(AnimPunch);
			}

			return true;
		}

		// Called by TryStartPrimaryAttack() function if slam attack is possible
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
			attackEndTime = Time.time + 0.8f; // DELETE LATER (match potential animation length)

			// Prototype: Open grapple window immediately (or trigger it via anim event)
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
			// Prototype: keep shield raised during combat
			// To lower it, set 'ShieldRaised' false somewhere
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