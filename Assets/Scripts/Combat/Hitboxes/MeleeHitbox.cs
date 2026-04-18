using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

// Shared Melee Hitbox for Player, Enemies and Boss (if boss uses melee) 
// Determines where a melee attack hits

// NOTE: Apply to the weapon for sword enemy + in front of shield enemy slam attack
// NOTE: PLACE MELEEHITBOX.cs ON CHILD OF ROOT PARENT GAMEOBJECT WHERE THE MELEE HITBOX COLLIDER IS
// NOTE: PLACE MELEEHITBOX COLLIDER ON CHILD OF ROOT PARENT GAMEOBJECT WHERE THE MELEEHITBOX.cs SCRIPT IS
public class MeleeHitbox : MonoBehaviour {
	[Header("Hit Filtering")]
	[Tooltip("Only colliders on these layers can register as valid melee hits.")]
	[SerializeField] private LayerMask validTargetLayers = ~0;

	private bool canDealDamage = true;
	private AttackData attackData;

	private readonly HashSet<int> hitRootsThisActivation = new();

	public event Action<GameObject, Vector3, GameObject> OnMeleeHit; // TODO: use later

	// This is called by the melee attack in order to configure this hitbox
	// NOTE: Sword enemy calls this at the beginning of their melee attacks via an animation event (supplies swing or lunger attack data)
	public void Initialise(AttackData attackData) {
		canDealDamage = true;
		this.attackData = attackData;
		hitRootsThisActivation.Clear();
	}

	private void OnTriggerEnter(Collider other) {
		if (canDealDamage == false || other == null) {
			return;
		}

		GameObject attackerRoot = attackData.Attacker.transform.root.gameObject;
		GameObject otherRoot = other.transform.root.gameObject;

		// Ignore self
		if (otherRoot == attackerRoot) {
			return;
		}

		// Only damage intended targets
		if ((validTargetLayers.value & (1 << otherRoot.layer)) == 0) {
			return;
		}

		// IDamageable is implemented inside Hurtbox.cs (which is on root parent gameobject)
		IDamageable damageableInterface = other.GetComponentInParent<IDamageable>();
		if (damageableInterface == null) {
			return;
		}

		// Prevent multiple hits on the same root during a single activation
		int rootId = otherRoot.GetInstanceID();
		if (hitRootsThisActivation.Contains(rootId)) {
			return;
		}

		hitRootsThisActivation.Add(rootId);

		// Stop further hits
		canDealDamage = false;

		// Damage handling dealt with inside target HurtBox.cs script
		damageableInterface.TakeDamage(attackData);

		Vector3 hitPoint = other.ClosestPoint(transform.position);
		OnMeleeHit?.Invoke(attackData.Attacker, hitPoint, other.gameObject);
	}

	// Call these in functions that will be fired in melee attack animations (enable/disable melee hitbox)
	public void Enable() {
		canDealDamage = true;
		hitRootsThisActivation.Clear();
		gameObject.SetActive(true);
	}

	public void Disable() {
		gameObject.SetActive(false);
	}
}