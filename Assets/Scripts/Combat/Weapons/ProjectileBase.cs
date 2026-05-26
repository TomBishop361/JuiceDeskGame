using UnityEngine;
using System;

public class ProjectileBase : MonoBehaviour
{
    protected float Damage;    
    public event Action<ProjectileBase> onBulletHit = delegate (ProjectileBase bullet) { };    

    protected virtual void OnBulletHit(ProjectileBase val)
    {
        onBulletHit?.Invoke(val);
    }

    public virtual void Fire(Vector3 direction, Vector3 origin, float speed, float damage)
    {
        Damage = damage;
        transform.position = origin;
        gameObject.SetActive(true);
    }

	// Called when something externally consumes the projectile
	// Example: Shield block
	public void ForceReturnToPool() {
		onBulletHit?.Invoke(this);
	}
}
