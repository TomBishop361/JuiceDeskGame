using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : ProjectileBase
{
    
    Rigidbody rb;    

    Vector3 direction;
    [SerializeField]
    GameObject testHitParticle;   
    

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void Fire(Vector3 direction, Vector3 origin, float speed, int damage)
    {
        base.Fire(direction, origin, speed, damage);
        this.direction = direction;                
        rb.linearVelocity = direction * speed;
        StartCoroutine("bulletTimeOut");
    }   
    
    //Destroy bullet if it hits nothing after timelimit
    IEnumerator bulletTimeOut()
    {
        yield return new WaitForSeconds(2f);
        OnBulletHit(this);
        yield return null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        StopCoroutine(bulletTimeOut());
        IDamageable hit;        
        if (collision.gameObject.TryGetComponent<IDamageable>(out hit))
        {
            hit.adjustHealth(Damage);
        }
        OnBulletHit(this);
        Instantiate(testHitParticle,transform.position,Quaternion.LookRotation(-direction));
    }
}
