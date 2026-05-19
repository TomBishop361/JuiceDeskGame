// Implemented inside PlayerHurtBox.cs + SwordHurtBox.cs + ShieldHurtBox.cs + DroneHurtBox.cs
// Entities access this to apply damage to target
// e.g Allows shield enemy to receive reduced damage in its defensive stance
public interface IDamageable {
	void TakeDamage(AttackData attackData);
}