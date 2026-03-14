using Game.AI.Sword;
using System;
using System.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : ProjectileBase {

    Rigidbody rb;

    Vector3 direction;
    [SerializeField]
    GameObject testHitParticle;

    [SerializeField] float lifeTime = 2f;

    float lifeTimer;
    bool isActive;


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

    private void OnCollisionEnter(Collision collision) {
        isActive = false;
        IDamageable hit;
        if (collision.gameObject.TryGetComponent<IDamageable>(out hit)) {
            AttackData attackData = new AttackData {
                Attacker = gameObject,
                AttackerFaction = Faction.Player,
                Damage = 0.1f,
                Knockback = new KnockbackData {
                    Force = 2.0f,
                    UpwardModifier = 0.0f,
                    TorqueStrength = 0.0f
                },
                Type = DamageType.Ranged
            };

            hit.TakeDamage(attackData);
        }
        OnBulletHit(this);
        Instantiate(testHitParticle, transform.position, Quaternion.LookRotation(-direction));
    }

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