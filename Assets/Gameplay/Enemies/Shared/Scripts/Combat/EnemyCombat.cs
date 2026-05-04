using UnityEngine;

public class EnemyCombat : MonoBehaviour {
	private EnemyTracker enemyTracker;
	private EnemyHuntTarget cachedHuntTarget;
	private bool deathReported = false;

	public bool HasReportedDeath => deathReported;
	public EnemyTracker Tracker => enemyTracker;

	public EnemyHuntTarget HuntTarget {
		get {
			if (cachedHuntTarget == null) {
				cachedHuntTarget = GetComponent<EnemyHuntTarget>();
			}

			return cachedHuntTarget;
		}
	}

	public void SetEnemyTracker(EnemyTracker tracker) {
		enemyTracker = tracker;
		deathReported = false;
		cachedHuntTarget = GetComponent<EnemyHuntTarget>();
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