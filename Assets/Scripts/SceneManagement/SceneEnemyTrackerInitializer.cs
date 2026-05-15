using UnityEngine;

// Stores the total number of enemies planned for this scene and assigns this scene's EnemyTracker to this scene's spawners
// Additive scenes can be loaded at simultaneously, so this initializer only inspects GenericSpawner components
// that belong to the same Unity scene as this object
// This prevents a streamed-in scene from accidentally counting or assigning spawners from the previous level
public class SceneEnemyTrackerInitializer : MonoBehaviour {
	[Header("References")]
	[Tooltip("EnemyTracker that represents this scene/encounter.")]
	[SerializeField] private EnemyTracker enemyTracker;

	[Header("Spawner Search")]
	[Tooltip("If true, inactive spawners in this same scene are included when calculating the planned enemy total.")]
	[SerializeField] private bool includeInactiveSpawners = true;
	[Tooltip("If true, this scene's EnemyTracker is assigned to every GenericSpawner found in this same scene.")]
	[SerializeField] private bool autoAssignTrackerToSpawners = true;
	[Tooltip("If true, logs the calculated enemy count for this scene. This is useful when testing additive streaming.")]
	[SerializeField] private bool debugLogging = true;

	private void Awake() {
		InitialiseTrackerForThisScene();
	}

	// Recalculates this scene's planned enemy count
	[ContextMenu("Initialise Tracker For This Scene")]
	public void InitialiseTrackerForThisScene() {
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
		int sceneSpawnerCount = 0;

		for (int i = 0; i < spawners.Length; i++) {
			GenericSpawner spawner = spawners[i];

			if (spawner == null) {
				continue;
			}

			// For additive loading, ignore spawners from other loaded scenes
			if (spawner.gameObject.scene != gameObject.scene) {
				continue;
			}

			sceneSpawnerCount++;
			totalPlanned += spawner.GetPlannedEnemyCount();

			if (autoAssignTrackerToSpawners) {
				spawner.SetEnemyTracker(enemyTracker);
			}
		}

		// Set the total once, after all same-scene spawners have been counted
		enemyTracker.SetSceneTotal(totalPlanned);

		if (debugLogging) {
			Debug.Log($"SceneEnemyTrackerInitializer: Scene '{gameObject.scene.name}' found {sceneSpawnerCount} spawner(s), planned enemies = {totalPlanned}.", this);
		}
	}
}