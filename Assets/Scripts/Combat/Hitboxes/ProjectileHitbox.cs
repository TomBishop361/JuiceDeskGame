using Game.Combat.Projectiles;
using System;
using UnityEngine;
using UnityEngine.ProBuilder;

// Shared Projectile Hitbox for Drone especially but can be used for any entity that uses projectiles
// Determines where a projectile attack hits

// NOTE: Should be applied to each Projectile GameObject
public class ProjectileHitbox : MonoBehaviour {
	[SerializeField] private LayerMask validHitLayers;

	//private ProjectileBase parentProjectile;
	private MonoBehaviour parentProjectile;
	private AttackData attackData;
	private bool hasHit = false;
	//private bool isDrone = false;

	public event Action<AttackData, IDamageable, Vector3> OnProjectileHitImpact;

	private void Awake() {
		//// TODO: search only for ProjectileBase when drones use the base script
		//if (parentProjectile == null) {
		//	parentProjectile = GetComponentInParent<DroneHomingProjectile>();
		//	//isDrone = true;
		//}
		// NOTE: TEMP WORKAROUND - Tries to get player bullet, if not then tried drone bullet
		// TODO: search only for ProjectileBase when drones use the base script
		if (parentProjectile == null) {
			parentProjectile = GetComponentInParent<ProjectileBase>();
			if (parentProjectile == null) {
				parentProjectile = GetComponentInParent<DroneHomingProjectile>();
			}

		}
	}

	// This is called by the ranged attack in order to configure this hitbox
	public void Initialise(AttackData attackData) {
		this.attackData = attackData;
		hasHit = false;
	}

	private void OnTriggerEnter(Collider other) {
		if (hasHit == true) {
			return;
		}

		// Ignore layers this projectile should not hit
		if ((validHitLayers.value & (1 << other.gameObject.layer)) == 0) {
			return;
		}

		// Ignore non-damageable utility triggers
		IDamageable target = other.GetComponentInParent<IDamageable>();
		if (other.isTrigger && target == null) {
			return;
		}

		hasHit = true;

		// Notify executors that projectile hit something
		// They handle damage dealing due to projectiles dealing either direct or in-direct splash damage (For drone homing projectiles only at the moment)
		Vector3 hitPoint = other.ClosestPoint(parentProjectile.transform.position);
		OnProjectileHitImpact?.Invoke(attackData, target, hitPoint);

		Debug.Log("parent = " + parentProjectile.gameObject.name);

		// IDamageable is implemented inside Hurtbox.cs (which is on root parent gameobject)
		// Hurtbox exists on enemies, but environment usually won't have one
		// IDamageable target = other.GetComponentInParent<IDamageable>();

		////IDamageable is implemented inside Hurtbox.cs(which is on root parent gameobject)
		//IDamageable target = other.GetComponentInParent<IDamageable>();
		//if (target == null) {
		//	Debug.LogError("IDamageable: not found in parent of: " + other.gameObject.name);
		//	Debug.Log(other.gameObject.name);
		//	return;
		//}

		// Dictates if a direct or in-direct hit was made based on whether IDamageable exists on the collided object
		//IDamageable target = other.TryGetComponent(out IDamageable damageable) ? damageable : null;
		//Debug.Log("TARGET =  " + other.name);

		//if (isDrone == false) {
		//	target.TakeDamage(attackData);
		//}

		//// Inform Projectile.cs script that a collision has occured
		//// It will deal with damage handling because there is falloff based on AOE impact
		//if (other.TryGetComponent(out IDamageable damageableInterface) == true) {
		//	// Direct hit
		//	Vector3 hitPoint = other.ClosestPoint(parentProjectile.transform.position);

		//	// Notify executors that projectile made a direct hit
		//	OnProjectileHitImpact?.Invoke(attackData, damageableInterface, hitPoint);

		//	//parentProjectile.OnHit(attackData, damageableInterface, hitPoint);
		//}
		//else {
		//	// In-direct hit
		//	Vector3 hitPoint = parentProjectile.transform.position;

		//	// Notify executors that projectile made an in-direct hit
		//	OnProjectileHitImpact?.Invoke(attackData, null, hitPoint);

		//	//parentProjectile.OnHit(attackData, null, parentProjectile.transform.position);
		//}
	}
}