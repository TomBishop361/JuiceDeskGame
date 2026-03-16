using UnityEngine;
[System.Serializable]
public struct AttackData {
	public float Damage;
	public KnockbackData Knockback;
	public GameObject Attacker;
	public DamageType Type;
	public Faction AttackerFaction;

	// TODO: ADD DAMAGE LAYERMASK TO THIS STRUCT ALSO ADD STUN (DONT HAVE TO FILL IT IN FOR THOSE THAT DONT DEAL IT)
}