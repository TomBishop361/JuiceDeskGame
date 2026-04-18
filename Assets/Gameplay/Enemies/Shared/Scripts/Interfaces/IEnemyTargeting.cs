using UnityEngine;

namespace Game.AI {
	public interface IEnemyTargeting {
		Transform Target { get; }
		bool HasLineOfSight { get; }
		float DistanceToTarget { get; }
	}
}
