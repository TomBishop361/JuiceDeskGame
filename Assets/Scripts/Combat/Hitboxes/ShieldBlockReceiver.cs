using Game.AI.Shield;
using UnityEngine;

public class ShieldBlockReceiver : MonoBehaviour {
	private ShieldEnemy shieldEnemy;

	private void Awake() {
		shieldEnemy = GetComponentInParent<ShieldEnemy>();
	}

	// Blocks incoming projectiles when the shield is raised and facing the player
	public bool TryBlockProjectile(Bullet bullet, Vector3 hitPoint) {
		if (shieldEnemy == null || bullet == null) {
			return false;
		}

		// Only react if actually raised + front-facing
		if (shieldEnemy.ShieldRaised == false || shieldEnemy.PlayerInFront == false) {
			return false;
		}

		// Play shield block feedback
		shieldEnemy.TriggerBlockReact();

		// Return the bullet to the existing pool
		bullet.ResolveBlockedHit(hitPoint);
		return true;
	}

	private void OnCollisionEnter(Collision collision) {
		Bullet bullet = collision.gameObject.GetComponentInParent<Bullet>();
		if (bullet == null) {
			return;
		}

		Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

		TryBlockProjectile(bullet, hitPoint);
	}

	private void OnTriggerEnter(Collider other) {
		Bullet bullet = other.GetComponentInParent<Bullet>();
		if (bullet == null) {
			return;
		}

		TryBlockProjectile(bullet, other.ClosestPoint(transform.position));
	}
}