namespace Game.AI {
	public interface IPrimaryAttack {
		bool IsAttacking { get; }
		bool CanStartPrimaryAttack();
		bool TryStartPrimaryAttack();
	}
}
