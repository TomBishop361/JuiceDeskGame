using UnityEngine;

namespace Game.AI.Drone {
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Hurtbox))]
	[RequireComponent(typeof(PooledObject))]
	[RequireComponent(typeof(FlightEnemyMotor))]
	[RequireComponent(typeof(DroneFlightMotor))]
	[RequireComponent(typeof(DroneProjectileWeapon))]
	[RequireComponent(typeof(DroneKnockdownState))]
	[RequireComponent(typeof(DroneDeathFall))]
	public sealed class DroneEnemy : EnemyAgentBase, IHealthSettings, IDroneCombat {
		[Header("Stats")]
		[SerializeField] private int maxHealth = 1;
		[Tooltip("Health threshold used for low-health behaviour checks.")]
		[SerializeField] private float lowHealthThreshold = 0.1f;

		[Header("Modules")]
		[SerializeField] private DroneFlightMotor droneFlightMotor;
		[SerializeField] private DroneProjectileWeapon projectileWeapon;
		[SerializeField] private DroneKnockdownState knockdownState;
		[SerializeField] private DroneDeathFall deathFall;
		
		// Implement IHealthSettings
		public float MaxHealth => maxHealth;
		public float LowHealthThreshold => lowHealthThreshold;

		// Drone Enemy Specific Properties 

		public bool InFireRange => HasTarget && projectileWeapon != null && projectileWeapon.InFireRange(DistanceToTarget);
		public bool TargetTooClose => droneFlightMotor != null && droneFlightMotor.TargetTooClose(Target);
		public bool CanFire => projectileWeapon != null && projectileWeapon.CanFire(this);
		public bool IsKnockedDown => knockdownState != null && knockdownState.IsKnockedDown;

		public override bool InAttackRange => InFireRange;
		public override bool CanAttack => CanFire;

		// Caches required combat and movement modules if they were not assigned in the inspector
		protected override void CacheComponents() {
			if (droneFlightMotor == null) {
				droneFlightMotor = GetComponent<DroneFlightMotor>();
			}

			if (projectileWeapon == null) {
				projectileWeapon = GetComponent<DroneProjectileWeapon>();
			}

			if (knockdownState == null) {
				knockdownState = GetComponent<DroneKnockdownState>();
			}

			if (deathFall == null) {
				deathFall = GetComponent<DroneDeathFall>();
			}
		}

		// Resets all drone-specific runtime modules when the enemy is spawned or reused from a pool
		protected override void ResetEnemyRuntime() {
			droneFlightMotor?.ResetRuntime();
			projectileWeapon?.ResetRuntime();
			knockdownState?.ResetRuntime(animator);
			deathFall?.ResetRuntime();
		}

		// Updates active knockdown behaviour and pushes movement speed into the animator for locomotion blending
		protected override void TickAlive() {
			knockdownState?.Tick(this, animator);

			if (animator != null && droneFlightMotor != null) {
				animator.SetFloat("MoveSpeed", droneFlightMotor.CurrentSpeed);
			}
		}

		// Interrupts behaviour and applies a knockdown stun when the enemy takes damage but survives
		protected override void OnDamaged(float previousHealth, float currentHealth) {
			knockdownState?.EnterKnockdown(this, animator);
		}

		// Handles drone-specific death cleanup by cancelling any projectile firing + movement
		protected override void OnDieStarted() {
			projectileWeapon?.Cancel();
			knockdownState?.OnDeath(animator);
		}

		// Handles drone-specific death transition
		protected override void BeginDeathFlow() {
			if (deathFall != null) {
				deathFall.Play(this, deathHandler, animator, Target);
				return;
			}

			base.BeginDeathFlow();
		}

		// Recovery behaviour for shared behaviour nodes
		// The drone enemy stops moving during recovery
		public override void RecoverTick() {
			StopMove();
		}

		// Standard chase behaviour
		// Moves toward the target until too close, then maintains range from the target
		public override void ChaseTargetTick() {
			if (HasTarget == false || IsDead || IsStunned || IsAttacking) {
				return;
			}

			droneFlightMotor?.TickMovement(Target);

			//if (HasLineOfSight == true && Target != null) {
			//	droneFlightMotor?.TickMovement(Target);
			//	return;
			//}

			//if (HasLastSeenPosition == true) {
			//	droneFlightMotor?.TickInvestigateMovement(LastSeenPosition);
			//}
		}

		public override void StopMove() {
			// Transform-based flight stops by not moving on tick
		}

		// Shared primary attack entry point
		// For the drone enemy, this defaults to the projectile firing attack
		public override bool TryStartPrimaryAttack() {
			return TryStartFire();
		}

		public bool TryStartFire() {
			// PROTOTYPE: Fire immediately (Move this to an Anim event later)
			//projectileWeapon?.FireProjectile(Target);

			return projectileWeapon != null && projectileWeapon.TryStartFire(this, animator);
		}

		// Animation Events

		// Called by animation at the end of an attack to release the shared attack lock
		public void AnimEvent_AttackFinished() {
			EndAttackLock();
		}

		// Called by animation to start the firing a projectile
		public void AnimEvent_FireProjectile() {
			projectileWeapon?.FireProjectile(Target);
		}
	}
}