using Game.Combat.Projectiles;
using UnityEngine;

public class DestroyableEnemyProjectile : MonoBehaviour, IDamageable {
	[SerializeField] private DroneHomingProjectile projectile;

	private void Awake() {
		if (projectile == null) {
			projectile = GetComponent<DroneHomingProjectile>();
		}
	}

	// TODO: When pooled replace SetActive(false) with the despawn method that the projectile uses in the pooling system
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

		// Fallback
		gameObject.SetActive(false);
	}

	public void adjustHealth(int damage) {
		throw new System.NotImplementedException();
	}
}