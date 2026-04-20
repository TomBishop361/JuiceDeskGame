namespace Game.AI.Shield {
	public interface IShieldCombat {
		bool InSlamRange { get; }
		bool InFireRange { get; }
		bool CanSlam { get; }
		bool CanFireMinigun { get; }
		bool GrappleWindowOpen { get; }
		bool PlayerInFront { get; }
		bool ShieldRaised { get; }
		bool IsExposed { get; }
		bool IsFiringMinigun { get; }

		bool TryStartMinigun();
		bool TryStartSlam();
	}
}
