using Unity.VisualScripting;
using UnityEngine;
namespace Game.AI {
	// Shared runtime base for all enemy agents
	[DisallowMultipleComponent]
	[RequireComponent(typeof(EnemyBlackboard))]
	[RequireComponent(typeof(EnemyPerception))]
	[RequireComponent(typeof(EnemyHealthDriver))]
	[RequireComponent(typeof(EnemyDeathHandler))]
	public abstract class EnemyAgentBase : EnemyCombat, IEnemyAgent, IEnemyCore, IEnemyTargeting, IEnemyMotor, IPrimaryAttack, IFactionOwner, IPoolSpawnHandler {
		[Header("Shared References")]
		[SerializeField] protected Animator animator;
		[SerializeField] protected Health healthComponent;
		[SerializeField] protected EnemyBlackboard blackboard;
		[SerializeField] protected EnemyPerception perception;
		[SerializeField] protected EnemyHealthDriver healthDriver;
		[SerializeField] protected EnemyDeathHandler deathHandler;

		[Header("Shared Timings")]
		[Tooltip("Default stun duration applied by simple damage reactions.")]
		[SerializeField] protected float defaultStunDuration = 0.4f;
		[Tooltip("Fallback despawn delay used by the shared death handler.")]
		[SerializeField] protected float defaultDeathDespawnDelay = 1.2f;

		// Shared timer state used by all derived enemies
		protected float stunEndTime = -Mathf.Infinity;
		protected float attackEndTime = -Mathf.Infinity;

		// Implement IFactionOwner
		public virtual Faction OwnerFaction => Faction.Enemy;

		// Core state exposed to behaviour nodes and helper systems
		public bool IsDead => blackboard != null && blackboard.IsDead;
		public bool IsStunned { get; protected set; }
		public bool HasTarget => blackboard != null && blackboard.HasTarget;
		public Transform Target => blackboard != null ? blackboard.Target : null;
		public bool HasLineOfSight => blackboard != null && blackboard.HasLineOfSight;
		public float DistanceToTarget => blackboard != null ? blackboard.DistanceToTarget : Mathf.Infinity;
		//public Vector3 LastSeenPosition => blackboard != null ? blackboard.LastSeenPosition : transform.position;
		//public bool HasLastSeenPosition => blackboard != null && blackboard.HasLastSeenPosition;
		public bool IsAttacking { get; protected set; }

		// Derived enemies define what "attack range" and "attack ready" mean
		public abstract bool InAttackRange { get; }
		public abstract bool CanAttack { get; }

		// Animator IDs
		private static readonly int AnimDie = Animator.StringToHash("Die"); // Trigger

		protected virtual void Reset() {
			AutoWireBaseComponents();
			CacheComponents();
		}

		// Runtime setup for shared and enemy-specific references
		protected virtual void Awake() {
			AutoWireBaseComponents();
			CacheComponents();
			ConfigureBaseModules();
			ResetRuntimeToBaseValues();
		}

		// Shared update
		protected virtual void Update() {
			if (IsDead) {
				return;
			}

			if (perception != null) {
				perception.Tick(transform, blackboard);
			}

			if (healthDriver != null) {
				healthDriver.Tick();

				if (healthDriver.DamageTakenThisTick && healthDriver.CurrentHealth > 0.0f) {
					OnDamaged(healthDriver.PreviousHealth, healthDriver.CurrentHealth);
				}

				if (healthDriver.IsDead) {
					Die();
					return;
				}
			}

			TickSharedTimers();
			TickAlive();
		}

		// Pool callback
		// Reset the agent every time it is spawned
		public virtual void OnSpawned() {
			ResetRuntimeToBaseValues();
		}

		// Pool callback
		// Override when the enemy needs custom cleanup on despawn
		public virtual void OnDespawned() {
		}

		// Shared gate used by generic attack nodes
		public virtual bool CanStartPrimaryAttack() {
			return HasTarget && CanAttack && IsDead == false && IsStunned == false;
		}

		// Forces perception to immediately look for a target
		public virtual void AcquireTarget() {
			if (perception == null || blackboard == null) {
				return;
			}

			perception.AcquireTargetImmediately(transform, blackboard);
		}

		// Default recovery behaviour is to stop moving
		public virtual void RecoverTick() {
			StopMove();
		}

		public abstract void ChaseTargetTick();
		public abstract void StopMove();
		public abstract bool TryStartPrimaryAttack();

		// Shared death entry point
		// Enemies can extend 'OnDieStarted' for visuals or cleanup
		public virtual void Die() {
			if (IsDead) {
				return;
			}

			blackboard.SetDead(true);
			IsStunned = false;
			IsAttacking = false;

			TrackDeath();
			StopMove();
			OnDieStarted();
			BeginDeathFlow();
		}

		// Applies a temporary stun window
		// Passing zero or less clears the stun state
		public void SetStunnedFor(float duration) {
			if (duration <= 0.0f) {
				IsStunned = false;
				stunEndTime = -Mathf.Infinity;
				return;
			}

			IsStunned = true;
			stunEndTime = Time.time + duration;
		}

		// Prevents the enemy from starting another attack until the lock expires
		public void BeginAttackLock(float duration) {
			if (duration <= 0.0f) {
				return;
			}

			IsAttacking = true;
			attackEndTime = Time.time + duration;
		}

		// Clears the current attack lock immediately
		// Useful for animation events
		public void EndAttackLock() {
			IsAttacking = false;
			attackEndTime = -Mathf.Infinity;
		}

		// Manually assigns a target and updates the blackboard immediately
		public void AssignTarget(Transform target) {
			if (perception == null || blackboard == null) {
				return;
			}

			perception.SetExplicitTarget(target);
			blackboard.SetTarget(target);
		}

		protected virtual void CacheComponents() {
		}

		// Enemy-specific runtime reset
		// Override for cooldowns, flags, and local state
		protected virtual void ResetEnemyRuntime() {
		}

		// Enemy-specific alive tick
		// Override for movement, attacks, and animation updates
		protected virtual void TickAlive() {
		}

		// Called when the health driver detects damage but the enemy is still alive
		protected virtual void OnDamaged(float previousHealth, float currentHealth) {
		}

		// Shared death reaction
		// Plays the die trigger by default
		protected virtual void OnDieStarted() {
			if (animator != null) {
				animator.SetTrigger(AnimDie);
			}
		}

		// Starts the shared death handler sequence
		protected virtual void BeginDeathFlow() {
			if (deathHandler != null) {
				deathHandler.BeginDeathSequence(this, transform, Target, defaultDeathDespawnDelay);
			}
		}

		// Resets all shared runtime state and then lets the enemy reset its own data
		protected void ResetRuntimeToBaseValues() {
			if (blackboard != null) {
				blackboard.ResetRuntime();
			}

			if (perception != null) {
				perception.ResetRuntime();
			}

			if (healthDriver != null) {
				healthDriver.ResetRuntime();
			}

			if (deathHandler != null) {
				deathHandler.ResetRuntime();
			}

			IsStunned = false;
			IsAttacking = false;
			stunEndTime = -Mathf.Infinity;
			attackEndTime = -Mathf.Infinity;

			ResetEnemyRuntime();
		}

		// Attempts to auto-assign all shared references from the same GameObject
		private void AutoWireBaseComponents() {
			if (animator == null) {
				animator = GetComponentInChildren<Animator>();
			}

			if (healthComponent == null) {
				healthComponent = GetComponent<Health>();
			}

			if (blackboard == null) {
				blackboard = GetComponent<EnemyBlackboard>();
			}

			if (perception == null) {
				perception = GetComponent<EnemyPerception>();
			}

			if (healthDriver == null) {
				healthDriver = GetComponent<EnemyHealthDriver>();
			}

			if (deathHandler == null) {
				deathHandler = GetComponent<EnemyDeathHandler>();
			}
		}

		// Passes the required shared references into the helper modules
		private void ConfigureBaseModules() {
			if (healthDriver != null) {
				healthDriver.SetHealthSource(healthComponent);
				healthDriver.ForceSyncFromComponent();
			}

			if (perception != null) {
				perception.SetSensor(GetComponent<LOSSensor>());
			}
		}

		// Resolves stun and attack lock timers
		private void TickSharedTimers() {
			if (IsStunned && Time.time >= stunEndTime) {
				IsStunned = false;
				stunEndTime = -Mathf.Infinity;
			}

			// Handle Attack lock timer (prevents immediate re-trigger spam even if anim event misfires)
			if (IsAttacking && Time.time >= attackEndTime) {
				EndAttackLock();
			}
		}
	}
}