namespace Game.AI {
	// Shared interface so Custom Behaviour nodes can work with Sword/Shield/Drone enemies consistently
	// This is to be implemented on SwordEnemy, ShieldEnemy, DroneEnemy (TODO: common base class that implemnts this)
	public interface IEnemyAgent {
		// Core State
		bool IsDead { get; }
		bool IsStunned { get; }
		bool HasTarget { get; }
		//bool HasLOS { get; } // OPTIONAL (Same as HasTarget?)

		// Targeting / Ranges
		float DistanceToTarget { get; }
		bool InAttackRange { get; }

		// Combat gating
		bool CanAttack { get; } // REMOVE LATER - rely on Enemy-specific combat interfaces per enemy type
		bool IsAttacking { get; }
		//bool IsTargetTooClose { get; }

		// Execution hooks (called by Custom Behaviour action node classes)
		void AcquireTarget(); // OPTIONAL
		void Die();
		void RecoverTick();
		void ChaseTargetTick();
		void StopMove(); // OPTIONAL
		//void FaceTarget(); // OPTIONAL
		bool TryStartPrimaryAttack();
	}
}
// NOTE: Can remove these later and rely on Enemy-specific interfaces & other methods
// VARIABLES:
// - DistanceToTarget -> Move to shared 'EnemyBlackboard.cs'
// - InAttackRange (no need for mult-attack enemies) -> Replace w/ enemy specific range conditions
// - CanAttack (no need for mult-attack enemies) -> Replace w/ enemy specific attack conditions
// - IsAttacking (no need if using windup/active/recoverey) -> Replace w/ enum 'AttackState' OR per-attack flags/timers
// FUNCTIONS:
// - AcquireTarget() -> Replace w/ pereception component OR 'AIController.cs' assigning targets
// - RecoverTick() -> Replace w/ EnterRecovery() + ExitRecovery() OR state/anim driven logic
// - ChaseTargetTick() -> Replace w/ calling shared movement actions that use NavAgent like 'EnemyMotor.cs' OR use shared IMovable interface()
// - StopMove() -> Replace w/ placing inside IEnemyMotor OR IMovable interface
// - TryStartPrimaryAttack() -> Replace w/ combat specifc interfaces per enemy type

// Interfaces for later:
// - IEnemyCore -> Bools: IsDead, IsStunned, HasTarget | Functions: Die()
// - IEnemyMovement
// - ISwordCombat
// - IShieldCombat
// - IDroneCombat
// - IEnemyPrimaryCombat (maybe -> could use as generic primary attack fallback)