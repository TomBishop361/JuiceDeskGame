using UnityEngine;
using System;

public class ProjectileBase : MonoBehaviour
{
    protected int Damage;    
    public event Action<ProjectileBase> onBulletHit = delegate (ProjectileBase bullet) { };    

    protected virtual void OnBulletHit(ProjectileBase val)
    {
        onBulletHit?.Invoke(val);
    }

    public virtual void Fire(Vector3 direction, Vector3 origin, float speed, int damage)
    {
        Damage = damage;
        transform.position = origin;
        gameObject.SetActive(true);
    }

}
