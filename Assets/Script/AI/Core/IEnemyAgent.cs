namespace Game.AI {
	// Shared interface so Custom Behaviour nodes can work with Sword/Shield/Drone enemies consistently
	// This is to be implemented on SwordEnemy, ShieldEnemy, DroneEnemy (or a common base class)
	public interface IEnemyAgent {
		// Core State
		bool IsDead { get; }
		bool IsStunned { get; }
		bool HasTarget { get; }

		// Targeting / ranges
		float DistanceToTarget { get; }
		bool InAttackRange { get; }

		// Combate gating
		bool CanAttack { get; }
		bool IsAttacking { get; }

		// Execution hooks (called by Custom Behaviour action node classes)
		void Die();
		void RecoverTick();
		void AcquireTarget(); // OPTIONAL
		void ChaseTargetTick();
		void StopMove(); // OPTIONAL
		//void FaceTarget(); // OPTIONAL
		bool TryStartPrimaryAttack();
	}
}