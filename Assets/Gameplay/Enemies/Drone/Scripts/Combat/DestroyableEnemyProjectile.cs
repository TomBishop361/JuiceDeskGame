using Game.Combat.Projectiles;
using UnityEngine;
using Game.Audio;

public class DestroyableEnemyProjectile : MonoBehaviour, IDamageable {
	[SerializeField] private DroneHomingProjectile projectile;

	[Header("SFX")]
	[Tooltip("Played when this enemy projectile is destroyed by player damage.")]
	[SerializeField] private SFXDefinition destroyedByPlayerSFX;

	private void Awake() {
		if (projectile == null) {
			projectile = GetComponent<DroneHomingProjectile>();
		}
	}

	// When the projectile is shot, prefer returning it through the same pool path the projectile already uses
	public void TakeDamage(AttackData attackData) {
		SFXManager.PlayAtPosition(destroyedByPlayerSFX, transform.position);

		if (projectile != null) {
			projectile.ReturnToPool();
			return;
		}

		PooledObject pooledObject = GetComponent<PooledObject>();
		if (pooledObject != null) {
			pooledObject.ReturnToPool();
			return;
		}

		// Fallback for non-pooled test objects
		gameObject.SetActive(false);
	}
}