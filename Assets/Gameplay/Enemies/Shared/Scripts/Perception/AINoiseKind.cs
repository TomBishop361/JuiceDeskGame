using UnityEngine;

// AINoiseKind.cs
namespace Game.AI {
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