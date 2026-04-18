namespace Game.AI.Drone {
	public interface IDroneCombat {
		bool InFireRange { get; }
		bool TargetTooClose { get; }
		bool CanFire { get; }
		bool IsKnockedDown { get; }

		bool TryStartFire();
	}
}