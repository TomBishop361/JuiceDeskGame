using Game.Combat.Projectiles;
using UnityEngine;

public class DestroyableEnemyProjectile : MonoBehaviour, IDamageable {
	[SerializeField] private DroneHomingProjectile projectile;

	private void Awake() {
		if (projectile == null) {
			projectile = GetComponent<DroneHomingProjectile>();
		}
	}

	// When the projectile is shot, prefer returning it through the same pool path the projectile already uses
	public void TakeDamage(AttackData attackData) {
		if (projectile != null) {
			projectile.ReturnToPool();
			return;
		}

		PooledObject pooledObject = GetComponent<PooledObject>();
		if (pooledObject != null) {
			pooledObject.ReturnToPool();
			return;
		}

		// Fallback for non-pooled test objects.
		gameObject.SetActive(false);
	}

	public void adjustHealth(int damage) {
		throw new System.NotImplementedException();
	}
}