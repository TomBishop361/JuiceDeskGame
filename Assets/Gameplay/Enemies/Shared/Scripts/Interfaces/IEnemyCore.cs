using UnityEngine;

namespace Game.AI {
	public interface IEnemyCore {
		bool IsDead { get; }
		bool IsStunned { get; }
		bool HasTarget { get; }
		void Die();
	}
}
