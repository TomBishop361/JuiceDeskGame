namespace Game.AI.Sword {
	public interface ISwordCombat {
		bool InSwingRange { get; }
		bool InLungeRange { get; }
		bool ShouldLunge { get; }
		bool CanSwing { get; }
		bool CanLunge { get; }

		bool TryStartSwing();
		bool TryStartLunge();
	}
}
