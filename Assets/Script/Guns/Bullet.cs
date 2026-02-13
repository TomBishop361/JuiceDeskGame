using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    
    Rigidbody rb;
    int Damage;

    Vector3 direction;
    [SerializeField]
    GameObject testHitParticle;
    
    //public delegate void BulletHit(Bullet bullet);
    public event Action<Bullet> onBulletHit = delegate(Bullet bullet) { } ;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void fire(Vector3 direction, Vector3 origin, float speed, int damage)
    {
        Damage = damage;   
        this.direction = direction;
        transform.position = origin;        
        gameObject.SetActive(true);
        rb.linearVelocity = direction * speed;
        StartCoroutine("bulletTimeOut");
    }   
    
    //Destroy bullet if it hits nothing after timelimit
    IEnumerator bulletTimeOut()
    {
        yield return new WaitForSeconds(2f);
        onBulletHit?.Invoke(this);       
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
        onBulletHit?.Invoke(this);
        Instantiate(testHitParticle,transform.position,Quaternion.LookRotation(-direction));
    }
}
