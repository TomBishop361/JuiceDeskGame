using Game.AI.Shield;
using UnityEngine;

public class ShieldBlockReceiver : MonoBehaviour {
	private ShieldEnemy shieldEnemy;

	private void Awake() {
		shieldEnemy = GetComponentInParent<ShieldEnemy>();
	}

	private void OnCollisionEnter(Collision collision) {
		if (shieldEnemy == null) {
			return;
		}

		// Only react if actually raised + front-facing
		if (shieldEnemy.ShieldRaised && shieldEnemy.PlayerInFront) {
			shieldEnemy.TriggerBlockReact();
		}
	}

	private void OnTriggerEnter(Collider other) {
		if (shieldEnemy == null) return;

		if (shieldEnemy.ShieldRaised && shieldEnemy.PlayerInFront) {
			shieldEnemy.TriggerBlockReact();
		}
	}
}