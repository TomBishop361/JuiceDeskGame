using UnityEngine;

public class DemoTarget : MonoBehaviour, IDamageable
{
    public void adjustHealth(int damage)
    {
       Destroy(gameObject);
    }

    // Needed to implement interface
	public void TakeDamage(AttackData attackData) {
		// Configure Attack data for sword enemy
		attackData = new AttackData {
			Attacker = gameObject,
			AttackerFaction = Faction.Player,
			Damage = 1,
			Knockback = new KnockbackData {
				Force = 0.0f,
				UpwardModifier = 0.0f,
				TorqueStrength = 0.0f
			},
			Type = DamageType.Ranged
		};
	}
}
