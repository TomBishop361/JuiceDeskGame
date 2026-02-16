using System;
using UnityEngine;

// Shared Melee Hitbox for Player, Enemies and Boss (if boss uses melee) 
// Determines where a melee attack hits

// NOTE: Apply to the weapon for sword enemy + in front of shield enemy slam attack
// NOTE: PLACE MELEEHITBOX.cs ON CHILD OF ROOT PARENT GAMEOBJECT WHERE THE MELEE HITBOX COLLIDER IS
// NOTE: PLACE MELEEHITBOX COLLIDER ON CHILD OF ROOT PARENT GAMEOBJECT WHERE THE MELEEHITBOX.cs SCRIPT IS
public class MeleeHitbox : MonoBehaviour {
	private bool canDealDamage = true;
	private AttackData attackData;

	public event Action<GameObject, Vector3, GameObject> OnMeleeHit;

	// This is called by the melee attack in order to configure this hitbox
	// NOTE: Sword enemy calls this at the beginning of their melee attacks via an animation event (supplies swing or lunger attack data)
	public void Initialise(AttackData attackData) {
		canDealDamage = true;
		this.attackData = attackData;
	}

	private void OnTriggerEnter(Collider other) {
		if (canDealDamage == false) {
			return;
		}

	
		// IDamageable is implemented inside Hurtbox.cs (which is on root parent gameobject)
		IDamageable damageableInterface = other.GetComponentInParent<IDamageable>();
		if (damageableInterface == null) {
			Debug.LogError("IDamageable: not found in parent of: " + other.gameObject.name);
			Debug.Log(other.gameObject.name);
			return;
		}

		// Stop further hits
		canDealDamage = false;

		// Damage handling dealt with inside target HurtBox.cs script
		damageableInterface.TakeDamage(attackData);

		Vector3 hitPoint = other.ClosestPoint(transform.position);

		// Notify executors that melee has hit something
		OnMeleeHit?.Invoke(attackData.Attacker, hitPoint, other.gameObject);
	}

	// Call these in functions that will be fired in melee attack animations (enable/disable melee hitbox)
	public void Enable() {
		canDealDamage = true;
		gameObject.SetActive(true);
	}

	public void Disable() {
		gameObject.SetActive(false);
	}
}