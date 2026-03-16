using Game.AI.Sword;
using System;
using System.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : ProjectileBase {
	[Header("Bullet Stats")]
	[SerializeField] private AttackData bulletAttackData = new AttackData();

	Rigidbody rb;

	Vector3 direction;
	[SerializeField]
	GameObject testHitParticle;

	[SerializeField] float lifeTime = 2f;

	float lifeTimer;
	bool isActive;

	// Runtime params
	private Transform player; 
	private PlayerSettings playerSettings; // TODO: Remove later as this script can be used by anyone not just the player
	private ProjectileHitbox projectileHitbox;
	//private AttackData attackData;

	//public AttackData attackData => projectileAttackData;

	// Runtime params
	//PlayerSettings playerSettings;

	private void Awake() {
		rb = GetComponent<Rigidbody>();
	}

	public override void Fire(Vector3 direction, Vector3 origin, float speed, float damage) {
		base.Fire(direction, origin, speed, damage);
		this.direction = direction;
		rb.linearVelocity = direction * speed;

		lifeTimer = lifeTime;
		isActive = true;
	}


	private void Update() {
		LifeTimer();
	}

	void LifeTimer() {
		if (!isActive) return;
		lifeTimer -= Time.deltaTime;
		if (lifeTimer <= 0) OnBulletHit(this);
	}

	//private void OnCollisionEnter(Collision collision) {
	//	isActive = false;
	//	IDamageable hit;
	//	if (collision.gameObject.TryGetComponent<IDamageable>(out hit)) {
			
	//		//AttackData attackData = new AttackData {
	//		//	Attacker = gameObject,
	//		//	AttackerFaction = Faction.Player,
	//		//	Damage = 0.1f,
	//		//	Knockback = new KnockbackData {
	//		//		Force = 2.0f,
	//		//		UpwardModifier = 0.0f,
	//		//		TorqueStrength = 0.0f
	//		//	},
	//		//	Type = DamageType.Ranged
	//		//};

	//		hit.TakeDamage(playerSettings.attackData);
	//	}
	//	OnBulletHit(this);
	//	Instantiate(testHitParticle, transform.position, Quaternion.LookRotation(-direction));
	//}

	public void HandleProjectileHitImpact(AttackData attackData, IDamageable directReceiver, Vector3 hitPoint) {
		if (isActive == false) {
			return;
		}
		// Disable the projectile
		isActive = false;

		// Apply damage if something damageable was hit
		if (directReceiver != null) {
			Debug.Log("direct receiver take damage");
			directReceiver.TakeDamage(attackData);
		}
		
		// Notify projectile system / pooling system
		OnBulletHit(this);

		// Spawn hit effect
		if (testHitParticle != null) {
			Debug.Log("testhit particle spawn");
			Instantiate(testHitParticle, hitPoint, Quaternion.LookRotation(-direction));
		}
	}

	//// Using Trigger for bullets - more consistent collision detection behaviour
	//private void OnTriggerEnter(Collider other) {
	//	if (isActive == false) {
	//		return;
	//	}
	//	isActive = false;

	//	IDamageable hit;
	//	if (other.TryGetComponent<IDamageable>(out hit)) {
	//		hit.TakeDamage(playerSettings.attackData);
	//	}
	//}

	// - Events and Callback handlers -

	private void OnEnable() {
		isActive = false;
		lifeTimer = 0.0f;
		if (projectileHitbox != null) {
			projectileHitbox.Initialise(bulletAttackData);
			projectileHitbox.OnProjectileHitImpact -= HandleProjectileHitImpact;
			projectileHitbox.OnProjectileHitImpact += HandleProjectileHitImpact;
		}
	}
	private void OnDisable() {
		if (projectileHitbox != null) {
			projectileHitbox.OnProjectileHitImpact -= HandleProjectileHitImpact;
		}

		rb.linearVelocity = Vector3.zero;
		isActive = false;
	}

	//private void OnDestroy() {
	//	if (projectileHitbox != null) {
	//		projectileHitbox.OnProjectileHitImpact -= HandleProjectileHitImpact;
	//	}
	//}


	//private void OnTriggerEnter(Collider collision) {
	//	isActive = false;
	//	IDamageable hit;
	//	if (collision.gameObject.TryGetComponent<IDamageable>(out hit)) {
	//		AttackData attackData = new AttackData {
	//			Attacker = gameObject,
	//			AttackerFaction = Faction.Player,
	//			Damage = 0.1f,
	//			Knockback = new KnockbackData {
	//				Force = 0.0f/*2.0f*/,
	//				UpwardModifier = 0.0f,
	//				TorqueStrength = 0.0f
	//			},
	//			Type = DamageType.Ranged
	//		};

	//		hit.TakeDamage(attackData);
	//	}
	//	OnBulletHit(this);
	//	Instantiate(testHitParticle, transform.position, Quaternion.LookRotation(-direction));

	//	//SwordEnemy sword = other.GetComponentInParent<SwordEnemy>();
	//	//if (sword != null) {
	//	//	sword.TakeDamage(damage);
	//	//	Destroy(gameObject);
	//	//	return;
	//	//}

	//	//// hit wall / environment
	//	//Destroy(gameObject);
	//}

}