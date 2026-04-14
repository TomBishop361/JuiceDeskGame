using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.UI.Image;

public class BulletPoolManager : MonoBehaviour
{
    //BulletPool 40 bullets?
    [SerializeField]
    GameObject bulletPrefab;
    [SerializeField]
    int bulletPoolSize = 40;
    [SerializeField]
    List<ProjectileBase> bullets = new List<ProjectileBase>();
    [SerializeField]
    List<ProjectileBase> inUse = new List<ProjectileBase>();



    private void Start()
    {
        for (int i = 0; i < bulletPoolSize; i++)
        {
            createBullet();
        }
    }

    void createBullet()
    {
        GameObject Bullet = Instantiate(bulletPrefab, transform);
        bullets.Add(Bullet.GetComponent<Bullet>());
        Bullet.SetActive(false);
        Bullet.GetComponent<ProjectileBase>().onBulletHit += returnBulletToPool;
    }

    public void ShootBullet(Vector3 direction, Vector3 origin, float speed, float dmg)
    {
        if (bullets.Count <= 0)
        {
            createBullet();
        }
        ProjectileBase Bullet = bullets[0];
        bullets.Remove(Bullet);        
        inUse.Add(Bullet);
        Bullet.Fire(direction,origin,speed,dmg);
    }

    void returnBulletToPool(ProjectileBase bullet)
    {
        inUse.Remove(bullet);
        bullets.Add(bullet);
        bullet.gameObject.SetActive(false);
    }

}
