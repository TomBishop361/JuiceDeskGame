using System;
using System.Collections;
using Unity.AppUI.Core;
using UnityEngine;

// Shared Hurtbox for Player, Enemies and Boss 
// Determines where the entity can be hit by a hitbox

// NOTE: PLACE HURTBOX.cs ON ROOT PARENT GAMEOBJECT
// NOTE: PLACE HURTBOX COLLIDER ON CHILD OF ROOT PARENT GAMEOBJECT
[RequireComponent(typeof(Health))]
public class Hurtbox : MonoBehaviour, IDamageable {
	[SerializeField] private Animator animator;
	private IFactionOwner factionOwner;

	public static event Action<Hurtbox, AttackData> OnAnyDamaged;

	private void Awake() {
		factionOwner = GetComponentInParent<IFactionOwner>();
		if (factionOwner == null) {
			Debug.LogError("Hurtbox: IFactionOwner not found on parent");
		}
	}

	public void TakeDamage(AttackData attackData) {
		
		// Ignore damaging own faction
		if (CanBeDamaged(attackData.AttackerFaction) == false) {
			Debug.LogError("Hurtbox: " + gameObject.name + "Cannot be damaged by " + attackData.Attacker.name);
			return;
		}

		ApplyDamage(attackData);
		ApplyKnockback(attackData);

		// Notify executors that damage has been received
		OnAnyDamaged?.Invoke(this, attackData);

		// Trigger damage reaction animation
		if (animator != null) {
			animator.SetTrigger("TakeDamage");
			StartCoroutine(ResetTakeDamageTriggerOnNextFrame());
		}	
	}

	private bool CanBeDamaged(Faction attackerFaction) {
		// If no faction owner is specified -> deal damage anyway
		if (factionOwner == null) {
			return true;
		}

		// Same faction = ignore (don't apply damage or knockback)
		Faction thisFaction = factionOwner.OwnerFaction;
		if (thisFaction == attackerFaction) {
			return false;
		}

		return true;
	}

	private void ApplyDamage(AttackData attackData) {
		// Check if entity has any damage buff or nerfs active
		// TODO: ADD COMBAT HELPER FUNCTIONALITY LATER
		//float finalDamage = CombatHelper.CalculateDamage(gameObject, attackData.Damage);

		// Apply final incoming damage to entity health component
		//CombatHelper.ApplyDamageToTarget(gameObject, finalDamage);

		if (gameObject.TryGetComponent(out Health health)) {
			health.ApplyDamage(attackData.Damage);
		}
	}

	private void ApplyKnockback(AttackData attackData) {
		if (transform.gameObject.TryGetComponent(out Rigidbody rigidbody) == false) {
			Debug.LogWarning("Hurtbox: Rigidbody component not found on " + gameObject.name);
			return;
		}

		// Compute the direction of knockback
		Vector3 knockbackDirection = transform.position - attackData.Attacker.transform.position/*.normalized*/;
		knockbackDirection.y = 0.0f;

		if (knockbackDirection.sqrMagnitude < 0.0001f) {
			knockbackDirection = -attackData.Attacker.transform.forward;
		}

		knockbackDirection.Normalize();

		Vector3 forceToApply = knockbackDirection * attackData.Knockback.Force;
		// Fixed upward modifier - prevents doubling vertical height knockback to entity
		forceToApply.y = attackData.Knockback.UpwardModifier;

		// Apply forces - NOTE: Can directly set velocity instead (more consistent)
		rigidbody.AddForce(forceToApply, ForceMode.VelocityChange);
		//rigidbody.AddForce(knockbackDirection * attackData.Knockback.Force + Vector3.up * attackData.Knockback.UpwardModifier, ForceMode.Impulse);

		// Clamp upward velocity (in the case of the plater already rising, they won't be sent too high)
		float maxUpVelocity = 8.0f; // TODO: MAKE THIS A VARIABLE [STORE INSIDE AttackData]

		Vector3 velocity = rigidbody.linearVelocity;

		if (velocity.y > maxUpVelocity) {
			velocity.y = maxUpVelocity;

			rigidbody.linearVelocity = velocity;
		}

		// Optional torque strength if configured
		if (attackData.Knockback.TorqueStrength > 0.0f) {
			// Random.insideUnitSphere is used here -> adds a chaotic spin effect to the target object
			rigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * attackData.Knockback.TorqueStrength, ForceMode.Impulse);
		}

		//// Apply knockback via Rigidbody
		//if (gameObject.TryGetComponent(out Rigidbody rigidbody) == true) {
		//	CombatHelper.ApplyRigidbodyKnockback(transform, attackData.Attacker.transform, attackData.Knockback);
		//}
		//// Apply knockback via CharacterController
		//else if (gameObject.TryGetComponent(out ThirdPersonController thirdPersonController) == true) {
		//	CombatHelper.ApplyCharacterControllerKnockback(transform, attackData.Attacker.transform, attackData.Knockback);
		//}
	}

	// Called in TakeDamge() after damage reaction animation has triggered
	private IEnumerator ResetTakeDamageTriggerOnNextFrame() {
		// Wait one frame
		yield return null;
		// Reset after one frame to prevent multiple triggers from firing
		animator.ResetTrigger("TakeDamage");
	}

	// TODO: REMOVE LATER -> REPLACED WITH TakeDamage()
	// FOR NOW COPIED OVER SOME LOGIC FROM TakeDamage above
	public void adjustHealth(int damage) {
		// Ignore damaging own faction
		//if (CanBeDamaged(attackData.AttackerFaction)) {
		//return;
		//}

		// NOTE: Initialise here as don't want to add param to adjustHealth (merge conflicts)
		// Configure Attack data for sword enemy
		AttackData attackData = new AttackData {
			Attacker = gameObject,
			AttackerFaction = Faction.Player, // if you have IFactionOwner
			Damage = damage,
			Knockback = new KnockbackData {
			Force = 8.0f,
			UpwardModifier = 2.0f,
			TorqueStrength = 0.0f
			},
			Type = DamageType.Melee
		};

		ApplyDamage(attackData);
		ApplyKnockback(attackData);

		// Notify executors that damage has been received
		OnAnyDamaged?.Invoke(this, attackData);

		// Trigger damage reaction animation
		if (animator != null) {
			animator.SetTrigger("TakeDamage");
			StartCoroutine(ResetTakeDamageTriggerOnNextFrame());
		}
	}
}
