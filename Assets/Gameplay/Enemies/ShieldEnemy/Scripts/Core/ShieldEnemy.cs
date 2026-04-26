using UnityEngine;

namespace Game.AI.Shield {
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Hurtbox))]
	[RequireComponent(typeof(PooledObject))]
	[RequireComponent(typeof(GroundEnemyMotor))]
	[RequireComponent(typeof(ShieldAdvanceMotor))]
	[RequireComponent(typeof(ShieldMinigunWeapon))]
	[RequireComponent(typeof(ShieldDefenseState))]
	[RequireComponent(typeof(ShieldShockwaveAttack))]
	[RequireComponent(typeof(ShieldStunState))]
	public sealed class ShieldEnemy : EnemyAgentBase, IHealthSettings, IShieldCombat {
		[Header("Stats")]
		[SerializeField] private int maxHealth = 5;
		[Tooltip("Health threshold used for low-health behaviour checks.")]
		[SerializeField] private float lowHealthThreshold = 1.0f;

		[Header("Modules")]
		[Tooltip("Ground movement module responsible for NavMesh chasing + stopping + facing.")]
		[SerializeField] private GroundEnemyMotor groundMotor;
		[Tooltip("Advance movement module used to push the shield enemy toward its target.")]
		[SerializeField] private ShieldAdvanceMotor advanceMotor;
		[Tooltip("Minigun weapon module that handles firing behaviour and sustained ranged pressure.")]
		[SerializeField] private ShieldMinigunWeapon minigunWeapon;
		[Tooltip("Defense module that manages shielded defensive behaviour and state transitions.")]
		[SerializeField] private ShieldDefenseState defenseState;
		[Tooltip("Shockwave attack module that handles close-range area attack execution.")]
		[SerializeField] private ShieldShockwaveAttack shockwaveAttack;
		[Tooltip("Stun module used to apply hit stun and interrupt the enemy when damaged.")]
		[SerializeField] private ShieldStunState stunState;

		// Implement IHealthSettings
		public float MaxHealth => maxHealth;
		public float LowHealthThreshold => lowHealthThreshold;

		// Shield Enemy Specific Properties 

		public bool InSlamRange => HasTarget && shockwaveAttack != null && shockwaveAttack.InSlamRange(DistanceToTarget);
		public bool InFireRange => HasTarget && minigunWeapon != null && minigunWeapon.InFireRange(DistanceToTarget);
		public bool CanSlam => shockwaveAttack != null && defenseState != null && defenseState.CanUseOffense() && shockwaveAttack.CanSlam(this);
		public bool CanFireMinigun => minigunWeapon != null && defenseState != null && defenseState.CanUseOffense() && minigunWeapon.CanFire(this);
		public bool GrappleWindowOpen => defenseState != null && defenseState.GrappleWindowOpen;
		public bool PlayerInFront => defenseState != null && defenseState.PlayerInFront(Target);
		public bool ShieldRaised => defenseState != null && defenseState.ShieldRaised;
		public bool IsExposed => defenseState != null && defenseState.IsExposed;
		public bool IsFiringMinigun => minigunWeapon != null && minigunWeapon.IsFiring;

		// Implement Shared Enemy Properties 

		// True when slam range condition is valid
		public override bool InAttackRange => InSlamRange;
		// Shared primary-attack gate used by generic behaviour nodes
		// For this enemy, it determines the minigun firing availability
		public override bool CanAttack => CanFireMinigun;

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed"); // Float

		// Caches required combat and movement modules if they were not assigned in the inspector
		protected override void CacheComponents() {
			if (groundMotor == null) {
				groundMotor = GetComponent<GroundEnemyMotor>();
			}

			if (advanceMotor == null) {
				advanceMotor = GetComponent<ShieldAdvanceMotor>();
			}

			if (minigunWeapon == null) {
				minigunWeapon = GetComponent<ShieldMinigunWeapon>();
			}

			if (defenseState == null) {
				defenseState = GetComponent<ShieldDefenseState>();
			}

			if (shockwaveAttack == null) {
				shockwaveAttack = GetComponent<ShieldShockwaveAttack>();
			}

			if (stunState == null) {
				stunState = GetComponent<ShieldStunState>();
			}
		}

		// Resets all shield-specific runtime modules when the enemy is spawned or reused from a pool
		protected override void ResetEnemyRuntime() {
			groundMotor?.ResetRuntime();
			advanceMotor?.ResetRuntime();
			minigunWeapon?.ResetRuntime(animator);
			defenseState?.ResetRuntime(animator);
			shockwaveAttack?.ResetRuntime();
			stunState?.ResetRuntime();
		}

		// Updates TBD
		protected override void TickAlive() {
			defenseState?.TickState(Target, animator);
			minigunWeapon?.TickFire(this, animator, Target);

			// Animator movement speed
			if (animator != null && groundMotor != null) {
				animator.SetFloat(AnimMoveSpeed, groundMotor.VelocityMagnitude);
			}
		}

		// Interrupts minigun firing behaviour and applies hit stun when the enemy takes damage but survives
		protected override void OnDamaged(float previousHealth, float currentHealth) {
			stunState?.ApplyHitStun(this, animator);
		}

		// Handles shield-specific death cleanup by TBD
		protected override void OnDieStarted() {
			base.OnDieStarted();
			StopMinigunFiring();
			defenseState?.StopAllStates(animator);
			groundMotor?.DisableAgent();
		}

		// Recovery behaviour for shared behaviour nodes
		// The shield enemy stops moving during recovery
		public override void RecoverTick() {
			StopMove();
		}

		// Standard chase behaviour
		// Advance with shield raised toward the target
		public override void ChaseTargetTick() {
			if (HasTarget == false || IsDead || IsStunned || IsExposed || IsAttacking) {
				return;
			}

			// Do not continue advancing while the minigun is firing
			if (IsFiringMinigun) {
				StopMove();
				FaceTarget(Target.position);
				return;
			}

			advanceMotor?.TickAdvance(this, Target);
		}

		// Stops ground movement through the ground motor
		public override void StopMove() {
			groundMotor?.Stop();
		}

		// Shared primary attack entry point
		// For the shield enemy, this defaults to the slam attack
		public override bool TryStartPrimaryAttack() {
			return TryStartMinigun();
		}

		public void FaceTarget(Vector3 worldPos) {
			groundMotor?.FaceTarget(worldPos);
		}

		// TBD COMMENT
		public bool TryStartMinigun() {
			bool started = minigunWeapon != null && minigunWeapon.TryStartFiring(this, animator, Target);
			if (started) {
				SetShieldRaised(true);
			}

			return started;
		}

		// TBD COMMENT
		public bool TryStartSlam() {
			bool started = shockwaveAttack != null && shockwaveAttack.TryStart(this, animator);
			if (started) {
				StopMinigunFiring();
				SetShieldRaised(true);
			}

			return started;
		}

		public void StopMinigunFiring() {
			minigunWeapon?.StopFiring(animator);
		}

		public void SetShieldRaised(bool isRaised) {
			defenseState?.SetShieldRaised(isRaised, animator);
		}

		public void EnterExposedState(float duration) {
			StopMinigunFiring();
			defenseState?.EnterExposed(duration, animator);
		}

		public void TriggerBlockReact() {
			defenseState?.TriggerBlockReact(animator);
		}

		// Animation Events

		public void AnimEvent_AttackFinished() {
			EndAttackLock();
		}

		public void AnimEvent_DoShockwave() {
			shockwaveAttack?.DoShockwave(this);
		}
	}
}