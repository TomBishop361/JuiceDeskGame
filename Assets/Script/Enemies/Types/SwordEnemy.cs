using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;
using Game.AI;// IEnemyAgent namespace

namespace Game.AI.Sword {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class SwordEnemy : MonoBehaviour, IEnemyAgent {

		[Header("References")]
		[SerializeField] private Transform target;
		[SerializeField] private NavMeshAgent navMeshAgent;
		[SerializeField] private Animator animator;

		[Header("Stats")]
		[SerializeField] private int health = 3;

		[Header("Combat")]
		[SerializeField] private float damage = 2.0f; // TODO: differen atck dmgs

		[Header("Ranges")]
		[SerializeField] private float attackRange = 1.8f;

		[Header("Cooldowns")]
		[SerializeField] private float attackCooldown = 1.2f;

		[Header("Movement")]
		[SerializeField] private bool toggleSmoothRotation = false; // toggle between reactive rotation & smooth rotation
		[SerializeField] private float rotationSpeed = 360.0f; // degrees per second
		[SerializeField] private float rotationSmoothing = 12.0f; // smoothing multiplier (12 - 15 good range)

		[Header("Stun")]
		[SerializeField] private float hitStunDuration = 0.6f;

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; }
		public bool HasTarget => target != null;
		//public bool HasLOS =>{ get; private set; }

		public float DistanceToTarget { get; private set; }
		public bool InAttackRange => HasTarget && DistanceToTarget <= attackRange;

		public bool CanAttack => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextAttackTime;
		public bool IsAttacking { get; private set; }
		//public bool IsTargetTooClose { get; private set; }

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [END] -

		// Cooldown timers
		private float nextAttackTime;
		private float stunEndTime;
		private float attackEndTime; // enforce min attack time [DELETE LATER]

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed");
		private static readonly int AnimAttack = Animator.StringToHash("Attack");
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

		public bool TryStartPrimaryAttack() {
			if (CanAttack == false) {
				return false;
			}
				
			StopMove();

			IsAttacking = true;
			nextAttackTime = Time.time + attackCooldown;
			attackEndTime = Time.time + 0.6f; // DELETE LATER (match potential animation length)

			if (animator != null) {
				animator.SetTrigger(AnimAttack);
			}
			//else {
			//	// Safety if no animator is set (ensures attack doesn't run forever)
			//	StartCoroutine(DelayAttackEnd());
			//}
				
			return true;
		}

		// DELETE ONCE ANIMATION IS IN FOR ATTACKING (THIS WILL SET IsAttacking to false instead [START]
		IEnumerator DelayAttackEnd() {
			yield return new WaitForSeconds(1.5f);

			// Set IsAttacking to false
			AnimEvent_AttackFinished();
		}
		// DELETE ONCE ANIMATION IS IN FOR ATTACKING (THIS WILL SET IsAttacking to false instead [END]

		// - Implement IEnemyAgent Methods [END] -

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