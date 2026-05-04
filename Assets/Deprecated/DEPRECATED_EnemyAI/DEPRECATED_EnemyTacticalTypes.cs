using System;
using UnityEngine;

namespace Game.AI {
	// Small finite-state layer used by BT's to interrupt tactical decisions cleanly
	// Tactical choices still live in Unity BT's
	public enum EnemyFiniteState {
		Spawned,
		Alive,
		AttackLocked,
		Stunned,
		KnockedDown,
		Disabled,
		Dead
	}

	// Role assigned by the squad director
	// BT's should branch from this instead of every enemy making the same decision
	public enum EnemyTacticalRole {
		None,
		Chaser,
		Flanker,
		Interceptor,
		Suppressor,
		Anchor
	}

	// Tactical points (placed in the levels) used by route prediction and fair interception
	[Flags]
	public enum TacticalAnchorType {
		None = 0,
		Door = 1 << 0,
		Ramp = 1 << 1,
		GrappleLanding = 1 << 2,
		WallRunExit = 1 << 3,
		RailExit = 1 << 4,
		PlatformChokepoint = 1 << 5,
		ArenaSideLane = 1 << 6,
		Cover = 1 << 7,
		Fallback = 1 << 8,
		Any = ~0
	}

	// Noise categories emitted by the player and consumed by enemy hearing sensors
	public enum AINoiseKind {
		Generic,
		Movement,
		Landing,
		Weapon,
		Grapple,
		WallRun,
		Slide,
		RailGrind
	}
}