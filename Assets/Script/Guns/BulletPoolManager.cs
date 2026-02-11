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
    List<Bullet> bullets = new List<Bullet>();
    [SerializeField]
    List<Bullet> inUse = new List<Bullet>();



    private void OnEnable()
    {
        for (int i = 0; i < bulletPoolSize; i++)
        {
            GameObject Bullet = Instantiate(bulletPrefab,transform);
            bullets.Add(Bullet.GetComponent<Bullet>());
            Bullet.SetActive(false);
            Bullet.GetComponent<Bullet>().onBulletHit += returnBulletToPool;
        }
    }

    public void ShootBullet(Vector3 direction, Vector3 origin, float speed, int dmg)
    {
        Bullet Bullet = bullets[0];
        bullets.Remove(Bullet);        
        inUse.Add(Bullet);
        Bullet.fire(direction,origin,speed,dmg);
    }

    void returnBulletToPool(Bullet bullet)
    {
        inUse.Remove(bullet);
        bullets.Add(bullet);
        bullet.gameObject.SetActive(false);
    }

}
