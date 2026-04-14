using UnityEngine;
using UnityEngine.Events;

public class EnemyTracker : MonoBehaviour {
	public int enemiesRemaining { get; private set; }
	public UnityEvent onAllEnemiesDead;

	private bool triggered = false;

	public void AddEnemies(int count) {
		if (count <= 0) {
			return;
		}

		enemiesRemaining += count;
		triggered = false;
	}

	public void ResetTracker() {
		enemiesRemaining = 0;
		triggered = false;
	}

	// Called by enemies when they die
	public void EnemyDied() {
		if (triggered == true) {
			return;
		}

		enemiesRemaining--;

		if (enemiesRemaining <= 0) {
			enemiesRemaining = 0;
			triggered = true;
			onAllEnemiesDead.Invoke();
		}
	}

	//// Called by the spawner BEFORE enemies start spawning
	//public void SetEnemyCount(int count) {
	//	enemiesRemaining = Mathf.Max(0, count);
	//	triggered = enemiesRemaining <= 0;

	//	if (triggered == true) {
	//		onAllEnemiesDead.Invoke();
	//	}
	//}
}