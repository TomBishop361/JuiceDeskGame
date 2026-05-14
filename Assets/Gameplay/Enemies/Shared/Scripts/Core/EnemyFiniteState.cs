using UnityEngine;

// EnemyFiniteState.cs
namespace Game.AI {
	// Small finite-state layer used by BT's to interrupt decisions cleanly
	// Choices still live in Unity BT's
	public enum EnemyFiniteState {
		Spawned,
		Alive,
		AttackLocked,
		Stunned,
		KnockedDown,
		Disabled,
		Dead
	}
}