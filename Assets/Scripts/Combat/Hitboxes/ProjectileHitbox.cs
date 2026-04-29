using Game.Combat.Projectiles;
using System;
using UnityEngine;
using UnityEngine.ProBuilder;

// Shared Projectile Hitbox for Drone especially but can be used for any entity that uses projectiles
// Determines what the projectile impacted and delegates damage resolution to the owning projectile
// NOTE: Should be applied to each Projectile GameObject
public class ProjectileHitbox : MonoBehaviour {
	[Header("Hit Filtering")]
	[Tooltip("Only colliders on these layers can register as valid projectile hits.")]
	[SerializeField] private LayerMask validHitLayers = ~0;

	// Cached projectile parent
	// Used as a fallback position reference when resolving hit points
	private MonoBehaviour parentProjectile;

	// Attack data assigned by the weapon or projectile when spawned/fired
	private AttackData attackData;

	private bool hasHit = false;

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
			// Try the generic projectile base
			parentProjectile = GetComponentInParent<ProjectileBase>();
		}

		// Fallback to the drone homing projectile implementation
		if (parentProjectile == null) {
			parentProjectile = GetComponentInParent<DroneHomingProjectile>();
		}
	}

	// This is called by the ranged attack in order to configure this hitbox
	public void Initialise(AttackData attackData) {
		this.attackData = attackData;
		hasHit = false;
	}

	// Handles trigger-based impact detection
	// Used when the projectile collider is configured as a trigger
	private void OnTriggerEnter(Collider other) {
		HandleImpact(other, other != null ? other.ClosestPoint(GetReferencePosition()) : transform.position);
	}

	// Handles collision-based impact detection
	// Used when the projectile collider is not configured as a trigger
	private void OnCollisionEnter(Collision collision) {
		Vector3 hitPoint;
		if (collision.contactCount > 0) {
			hitPoint = collision.contacts[0].point;
		}
		else {
			hitPoint = GetReferencePosition();
		}

		HandleImpact(collision.collider, hitPoint);
	}

	// Impact validation
	// Prevents duplicate hits + invalid layers +utility triggers with no damageable target
	private void HandleImpact(Collider other, Vector3 hitPoint) {
		if (hasHit || other == null) {
			return;
		}

		// Ignore layers this projectile should not hit
		if ((validHitLayers.value & (1 << other.gameObject.layer)) == 0) {
			return;
		}

		// Try to find something damageable on the impacted object or one of its parents
		IDamageable target = other.GetComponentInParent<IDamageable>();

		// Ignore utility triggers that cannot take damage
		if (other.isTrigger && target == null) {
			return;
		}

		hasHit = true;
		OnProjectileHitImpact?.Invoke(attackData, target, hitPoint);
	}

	// Returns the best available reference position for impact calculations
	// Prefers the owning projectile's transform if one was found
	private Vector3 GetReferencePosition() {
		if (parentProjectile != null) {
			return parentProjectile.transform.position;
		}

		return transform.position;
	}

	// - DEPRECATED-

	//private void OnTriggerEnter(Collider other) {
	//	if (hasHit) {
	//		return;
	//	}

	//	// Ignore layers this projectile should not hit
	//	if ((validHitLayers.value & (1 << other.gameObject.layer)) == 0) {
	//		return;
	//	}

	//	// Ignore non-damageable utility triggers
	//	IDamageable target = other.GetComponentInParent<IDamageable>();
	//	if (other.isTrigger && target == null) {
	//		return;
	//	}

	//	hasHit = true;

	//	// Notify executors that projectile hit something
	//	// They handle damage dealing due to projectiles dealing either direct or in-direct splash damage (For drone homing projectiles only at the moment)
	//	Vector3 hitPoint = other.ClosestPoint(parentProjectile.transform.position);
	//	OnProjectileHitImpact?.Invoke(attackData, target, hitPoint);

	//	Debug.Log("parent = " + parentProjectile.gameObject.name);

	//	// IDamageable is implemented inside Hurtbox.cs (which is on root parent gameobject)
	//	// Hurtbox exists on enemies, but environment usually won't have one
	//	// IDamageable target = other.GetComponentInParent<IDamageable>();

	//	////IDamageable is implemented inside Hurtbox.cs(which is on root parent gameobject)
	//	//IDamageable target = other.GetComponentInParent<IDamageable>();
	//	//if (target == null) {
	//	//	Debug.LogError("IDamageable: not found in parent of: " + other.gameObject.name);
	//	//	Debug.Log(other.gameObject.name);
	//	//	return;
	//	//}

	//	// Dictates if a direct or in-direct hit was made based on whether IDamageable exists on the collided object
	//	//IDamageable target = other.TryGetComponent(out IDamageable damageable) ? damageable : null;
	//	//Debug.Log("TARGET =  " + other.name);

	//	//if (isDrone == false) {
	//	//	target.TakeDamage(attackData);
	//	//}

	//	//// Inform Projectile.cs script that a collision has occured
	//	//// It will deal with damage handling because there is falloff based on AOE impact
	//	//if (other.TryGetComponent(out IDamageable damageableInterface)) {
	//	//	// Direct hit
	//	//	Vector3 hitPoint = other.ClosestPoint(parentProjectile.transform.position);

	//	//	// Notify executors that projectile made a direct hit
	//	//	OnProjectileHitImpact?.Invoke(attackData, damageableInterface, hitPoint);

	//	//	//parentProjectile.OnHit(attackData, damageableInterface, hitPoint);
	//	//}
	//	//else {
	//	//	// In-direct hit
	//	//	Vector3 hitPoint = parentProjectile.transform.position;

	//	//	// Notify executors that projectile made an in-direct hit
	//	//	OnProjectileHitImpact?.Invoke(attackData, null, hitPoint);

	//	//	//parentProjectile.OnHit(attackData, null, parentProjectile.transform.position);
	//	//}
	//}
}