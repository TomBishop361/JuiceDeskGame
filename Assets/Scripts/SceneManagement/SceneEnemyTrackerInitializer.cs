using UnityEngine;

// Stores the total number of enemies to be spawned in the scene
public class SceneEnemyTrackerInitializer : MonoBehaviour {
	[SerializeField] private EnemyTracker enemyTracker;
	[SerializeField] private bool includeInactiveSpawners = true;
	[SerializeField] private bool autoAssignTrackerToSpawners = true;

	private void Awake() {
		if (enemyTracker == null) {
			enemyTracker = GetComponent<EnemyTracker>();
		}

		if (enemyTracker == null) {
			Debug.LogWarning("SceneEnemyTrackerInitializer: needs an EnemyTracker reference.", this);
			return;
		}

		GenericSpawner[] spawners = FindObjectsByType<GenericSpawner>(
			includeInactiveSpawners ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, 
			FindObjectsSortMode.None
		);

		int totalPlanned = 0;

		for (int i = 0; i < spawners.Length; i++) {
			GenericSpawner spawner = spawners[i];

			if (spawner == null) {
				continue;
			}

			totalPlanned += spawner.GetPlannedEnemyCount();

			if (autoAssignTrackerToSpawners) {
				spawner.SetEnemyTracker(enemyTracker);
			}
		}

		enemyTracker.SetSceneTotal(totalPlanned);
	}
}