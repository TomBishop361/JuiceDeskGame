using UnityEngine;
using UnityEngine.Events;

public class EnemyTracker : MonoBehaviour {
	public int enemiesAlive;
	public UnityEvent onAllEnemiesDead;

	private bool triggered = false;

	private void Start() {
		EnemyCombat[] enemies = FindObjectsOfType<EnemyCombat>();
		enemiesAlive = enemies.Length;
	}

	public void EnemyDied() {
		if (triggered == true) {
			return;
		}

		enemiesAlive--;

		if (enemiesAlive <= 0) {
			triggered = true;
			onAllEnemiesDead.Invoke();
		}
	}
}