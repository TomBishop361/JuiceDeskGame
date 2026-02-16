using UnityEngine;
[System.Serializable]
public struct AttackData {
	public float Damage;
	public KnockbackData Knockback;
	public GameObject Attacker;
	public DamageType Type;
	public Faction AttackerFaction;
}