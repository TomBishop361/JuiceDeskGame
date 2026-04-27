using UnityEngine;
using UnityEngine.AI;

namespace Game.AI.Sword {
	// Melee enemy controller that coordinates ground movement + swing combat + lunge attacks + stun behaviour
	// Chooses between close-range swings and longer-range lunges based on the target's current distance
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Hurtbox))]
	[RequireComponent(typeof(PooledObject))]
	[RequireComponent(typeof(GroundEnemyMotor))]
	[RequireComponent(typeof(SwordMeleeCombat))]
	[RequireComponent(typeof(SwordLungeAttack))]
	[RequireComponent(typeof(SwordStunState))]
	public sealed class SwordEnemy : EnemyAgentBase, IHealthSettings, ISwordCombat {
		[Header("Stats")]
		[SerializeField] private int maxHealth = 3;
		[Tooltip("Health threshold used for low-health behaviour checks.")]
		[SerializeField] private float lowHealthThreshold = 1.0f;

		[Header("Modules")]
		[Tooltip("Ground movement module responsible for NavMesh chasing + stopping + facing.")]
		[SerializeField] private GroundEnemyMotor groundMotor;
		[Tooltip("Melee combat module that manages swing range checks + cooldowns + hitboxes.")]
		[SerializeField] private SwordMeleeCombat meleeCombat;
		[Tooltip("Lunge attack module that manages lunge range checks + windup + dash + recovery.")]
		[SerializeField] private SwordLungeAttack lungeAttack;
		[Tooltip("Stun module used to apply hit stun and interrupt the enemy when damaged.")]
		[SerializeField] private SwordStunState stunState;

		// Implement IHealthSettings
		public float MaxHealth => maxHealth;
		public float LowHealthThreshold => lowHealthThreshold;

		// Sword Enemy Specific Properties 

		// True when the target is close enough for the normal sword swing
		public bool InSwingRange => HasTarget && meleeCombat != null && meleeCombat.InRange(DistanceToTarget);
		// True when the target is inside the valid lunge distance band
		public bool InLungeRange => HasTarget && lungeAttack != null && lungeAttack.InRange(DistanceToTarget);
		// True when the enemy should prefer a lunge over a swing
		public bool ShouldLunge => InLungeRange && InSwingRange == false;
		// True when the melee module says a swing can begin right now
		public bool CanSwing => meleeCombat != null && meleeCombat.CanSwing(this);
		// True when the lunge module says a lunge can begin right now
		public bool CanLunge => lungeAttack != null && lungeAttack.CanLunge(this);

		// Implement Shared Enemy Properties 

		// True when either the swing or lunge range condition is valid
		public override bool InAttackRange => InSwingRange || InLungeRange;
		// Shared primary-attack gate used by generic behaviour nodes
		// For this enemy, it determines the swing availability
		public override bool CanAttack => CanSwing;

		// Caches required combat and movement modules if they were not assigned in the inspector
		protected override void CacheComponents() {
			if (groundMotor == null) {
				groundMotor = GetComponent<GroundEnemyMotor>();
			}

			if (meleeCombat == null) {
				meleeCombat = GetComponent<SwordMeleeCombat>();
			}

			if (lungeAttack == null) {
				lungeAttack = GetComponent<SwordLungeAttack>();
			}

			if (stunState == null) {
				stunState = GetComponent<SwordStunState>();
			}
		}

		// Resets all sword-specific runtime modules when the enemy is spawned or reused from a pool
		protected override void ResetEnemyRuntime() {
			groundMotor?.ResetRuntime();
			meleeCombat?.ResetRuntime();
			lungeAttack?.ResetRuntime(this);
			stunState?.ResetRuntime();
		}

		// Updates active lunge behaviour and pushes movement speed into the animator for locomotion blending
		protected override void TickAlive() {
			lungeAttack?.TickLunge(this);

			if (animator != null && groundMotor != null) {
				animator.SetFloat("MoveSpeed", groundMotor.VelocityMagnitude);
			}
		}

		// Interrupts lunge behaviour and applies hit stun when the enemy takes damage but survives
		protected override void OnDamaged(float previousHealth, float currentHealth) {
			if (lungeAttack != null && lungeAttack.IsLunging) {
				// TODO: Play hit VFX SFX only (but do not cancel dash)
				return;
			}

			lungeAttack?.CancelLunge(this);
			stunState?.ApplyHitStun(this, animator);
		}

		// Handles sword-specific death cleanup by disabling movement + turning off hitboxes + cancelling any active lunge
		protected override void OnDieStarted() {
			base.OnDieStarted();
			groundMotor?.DisableAgent();
			meleeCombat?.DisableAllHitboxes();
			lungeAttack?.CancelLunge(this, true);
		}

		// Recovery behaviour for shared behaviour nodes
		// The sword enemy stops moving during recovery
		public override void RecoverTick() {
			StopMove();
		}

		// Standard chase behaviour
		// Moves toward the target until swing range is reached, then stops and faces the target
		public override void ChaseTargetTick() {
			if (HasTarget == false || IsDead || IsStunned || IsAttacking || groundMotor == null) {
				return;
			}

			if (InSwingRange) {
				groundMotor.Stop();
				FaceTarget(Target.position);
				return;
			}

			groundMotor.Chase(Target);
			FaceTarget(Target.position);
		}

		// Stops ground movement through the ground motor
		public override void StopMove() {
			groundMotor?.Stop();
		}

		// Enable NavMeshAgent
		// Used at the end of a lunge attack
		public void EnableNavAgent() {
			if (TryGetComponent(out NavMeshAgent navMeshAgent)) {
				if (navMeshAgent.enabled == false) {
					navMeshAgent.enabled = true;
				}

				if (navMeshAgent.isOnNavMesh == false) {
					navMeshAgent.Warp(transform.position);
				}
			}
		}

		// Enable NavMeshAgent
		// Used at the start of a lunge attack
		public void DisableNavAgent() {
			if (TryGetComponent(out NavMeshAgent navMeshAgent)) {
				navMeshAgent.enabled = false;
			}
		}

		// Rotates the enemy to face the supplied world position using the ground motor
		public void FaceTarget(Vector3 worldPosition) {
			groundMotor?.FaceTarget(worldPosition);
		}

		// Shared primary attack entry point
		// For the sword enemy, this defaults to the swing attack
		public override bool TryStartPrimaryAttack() {
			return TryStartSwing();
		}

		// Attempts to start the normal melee swing through the melee combat module
		public bool TryStartSwing() {
			return meleeCombat != null && meleeCombat.TryStartSwing(this, animator);
		}

		// Attempts to start the lunge attack through the lunge module
		public bool TryStartLunge() {
			return lungeAttack != null && lungeAttack.TryStartLunge(this, animator);
		}

		// Animation Events

		// Called by animation at the end of an attack to release the shared attack lock
		public void AnimEvent_AttackFinished() {
			EndAttackLock();
		}

		// Called by the swing animation to prepare swing attack data before the active frames begin
		public void AnimEvent_SetupSwingData() {
			meleeCombat?.SetupSwingData();
		}

		// Called by animation to enable the swing hitbox during active frames
		public void AnimEvent_EnableSwingHitbox() {
			meleeCombat?.EnableSwingHitbox();
		}

		// Called by animation to disable the swing hitbox after the active frames end
		public void AnimEvent_DisableSwingHitbox() {
			meleeCombat?.DisableSwingHitbox();
		}

		// Called by the lunge animation to prepare lunge attack data before the dash begins
		public void AnimEvent_SetupLungeData() {
			lungeAttack?.SetupLungeData();
		}

		// Called by animation to enable the lunge hitbox during active frames
		public void AnimEvent_EnableLungeHitbox() {
			lungeAttack?.EnableLungeHitbox();
		}

		// Called by animation to disable the lunge hitbox after the active frames end
		public void AnimEvent_DisableLungeHitbox() {
			lungeAttack?.DisableLungeHitbox();
		}

		// Called by animation at the exact dash-start frame to launch the lunge movement
		public void OnLungeDashStart() {
			lungeAttack?.OnLungeDashStart(this);
		}

		// Called by animation when the lunge dash motion is over to end the dash phase cleanly.
		public void OnLungeDashEnd() {
			lungeAttack?.OnLungeDashEnd(this);
		}
	}
}