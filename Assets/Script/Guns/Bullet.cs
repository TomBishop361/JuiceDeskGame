using System;
using System.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : ProjectileBase
{
    
    Rigidbody rb;    

    Vector3 direction;
    [SerializeField]
    GameObject testHitParticle;

    [SerializeField] float lifeTime = 2f;

    float lifeTimer;
    bool isActive;
    

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void Fire(Vector3 direction, Vector3 origin, float speed, int damage)
    {
        base.Fire(direction, origin, speed, damage);
        this.direction = direction;                
        rb.linearVelocity = direction * speed;

        lifeTimer = lifeTime;
        isActive = true;            
    }


    private void Update()
    {
        LifeTimer();
    }

    void LifeTimer()
    {
        if (!isActive) return;
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0) OnBulletHit(this);
    }

    private void OnCollisionEnter(Collision collision)
    {
        isActive = false;
        IDamageable hit;        
        if (collision.gameObject.TryGetComponent<IDamageable>(out hit))
        {
            hit.adjustHealth(Damage);
        }
        OnBulletHit(this);
        Instantiate(testHitParticle,transform.position,Quaternion.LookRotation(-direction));
    }
}
