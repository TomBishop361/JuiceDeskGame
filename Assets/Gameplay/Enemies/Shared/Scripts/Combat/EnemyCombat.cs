using UnityEngine;

public class EnemyCombat : MonoBehaviour {
	private EnemyTracker enemyTracker;
	private bool deathReported = false;

	public bool HasReportedDeath => deathReported;

	public void SetEnemyTracker(EnemyTracker tracker) {
		enemyTracker = tracker;
		deathReported = false;
	}

	public void TrackDeath() {
		if (deathReported) {
			return;
		}

		deathReported = true;

		if (enemyTracker != null) {
			enemyTracker.EnemyDied();
		}
	}
}