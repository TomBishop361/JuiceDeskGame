using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// Generic reusable spawner that works with any prefab using PooledObject
// Handles spawning, pooling, and lifetime tracking
public class GenericSpawner : MonoBehaviour {
	[Header("References")]
	[SerializeField] private EnemyTracker enemyTracker;

	[Header("Spawnable Prefabs")]
	[Tooltip("All prefabs this spawner is allowed to spawn when using category-based wave entries")]
	[SerializeField] private PooledObject[] availablePrefabs;

	[Header("Waves")]
	[Tooltip("Ordered list of waves for this encounter")]
	[SerializeField] private Wave[] waves;

	[Header("Auto Spawn")]
	[Tooltip("If TRUE, uses the auto-spawn prefab at a fixed interval")]
	[SerializeField] private bool autoSpawn = false;
	[Tooltip("Prefab used by the simple automatic/manual single-type Spawn methods")]
	[SerializeField] private PooledObject autoSpawnPrefab;
	[Tooltip("Time between auto spawns")]
	[SerializeField] private float spawnInterval = 2.0f;

	[Header("Spawn Points")]
	[Tooltip("Optional list of spawn locations. One free point is chosen at random")]
	[SerializeField] private EnemySpawnPoint[] spawnPoints;

	[Header("Limits")]
	[Tooltip("Maximum number of active spawned objects at once")]
	[SerializeField] private int maxAlive = 10;

	[Header("Spawn Clearance")]
	[Tooltip("Layers that block spawning. If any collider in these layers is inside the radius, spawn is prevented")]
	[SerializeField] private LayerMask spawnBlockingLayers;
	[Tooltip("Whether trigger colliders should count when checking spawn space")]
	[SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

	[Header("Pool Settings")]
	[Tooltip("Initial number of pooled objects created per prefab when that prefab's pool is first needed")]
	[SerializeField] private int defaultCapacityPerPrefab = 10;
	[Tooltip("Maximum number of pooled objects allowed per prefab before extra released objects are destroyed")]
	[SerializeField] private int maxPoolSizePerPrefab = 20;

	// One pool per prefab type
	private readonly Dictionary<PooledObject, IObjectPool<PooledObject>> pools = new();
	// All currently active objects spawned by this spawner
	private readonly HashSet<PooledObject> aliveObjects = new();
	// Only the currently alive objects that belong to the active wave
	private readonly HashSet<PooledObject> aliveWaveObjects = new();

	private Coroutine spawnRoutine;
	private Coroutine waveRoutine;
	private WaitForSeconds cachedWait;

	private Vector3 pendingSpawnPosition;
	private Quaternion pendingSpawnRotation;
	private bool hasPendingSpawnPose;

	public bool IsWaveRunning => waveRoutine != null;
	public int AliveCount => aliveObjects.Count;
	public int AliveWaveCount => aliveWaveObjects.Count;

	private void Awake() {
		cachedWait = new WaitForSeconds(spawnInterval);
	}

	private void Start() {
		if (autoSpawn) {
			StartSpawning();
		}
	}

	
	public void SetEnemyTracker(EnemyTracker tracker) {
		enemyTracker = tracker;
	}

	public int GetPlannedEnemyCount() {
		return GetTotalEnemiesInAllWaves();
	}

	// Starts the simple continuous auto-spawn loop using autoSpawnPrefab
	public void StartSpawning() {
		if (spawnRoutine != null) {
			return;
		}
		if (autoSpawnPrefab == null) {
			Debug.LogWarning($"{name}: Auto Spawn is enabled but no Auto Spawn Prefab is assigned.", this);
			return;
		}

		spawnRoutine = StartCoroutine(SpawnLoop());
	}

	// Stops the simple auto-spawn loop
	public void StopSpawning() {
		if (spawnRoutine != null) {
			StopCoroutine(spawnRoutine);
			spawnRoutine = null;
		}
	}

	// Starts the wave sequence once
	public void StartWaveSequence() {
		if (waveRoutine != null) {
			return;
		}

		// Tell tracker how many enemies will exist
		//if (enemyTracker != null) {
		//	enemyTracker.AddEnemies(GetTotalEnemiesInAllWaves());
		//}

		// Tell tracker how many enemies will exist
		// Precomputed at scene start
		waveRoutine = StartCoroutine(WaveSequenceRoutine());
	}

	// Stops the wave sequence if it is running
	public void StopWaveSequence() {
		if (waveRoutine != null) {
			StopCoroutine(waveRoutine);
			waveRoutine = null;
		}
	}

	// Stores total enemies for all waves in a scene
	// Useful for triggering events when all enemies are dead
	private int GetTotalEnemiesInAllWaves() {
		int total = 0;

		if (waves == null) {
			return 0;
		}

		for (int i = 0; i < waves.Length; i++) {
			Wave wave = waves[i];

			if (wave == null || wave.entries == null) {
				continue;
			}

			for (int j = 0; j < wave.entries.Length; j++) {
				SpawnEntry entry = wave.entries[j];

				if (entry == null) {
					continue;
				}

				total += Mathf.Max(0, entry.count);
			}
		}

		return total;
	}

	//// Helper spawn using the autoSpawnPrefab
	//public PooledObject Spawn() {
	//	return Spawn(autoSpawnPrefab);
	//}

	// Spawns a specific prefab once
	public PooledObject Spawn(PooledObject prefab) {
		if (prefab == null) {
			return null;
		}
			
		if (aliveObjects.Count >= maxAlive) {
			return null;
		}

		if (TryGetFreeSpawnPoint(prefab, out Transform point) == false) {
			return null;
		}

		// Store pending spawn info
		pendingSpawnPosition = point.position;
		pendingSpawnRotation = point.rotation;
		hasPendingSpawnPose = true;

		IObjectPool<PooledObject> pool = GetOrCreatePool(prefab);
		PooledObject item = pool.Get();

		hasPendingSpawnPose = false;
		return item;
	}

	//// Spawns a burst of a specific prefab over time
	//public Coroutine SpawnBurst(MonoBehaviour runner, PooledObject prefab, int count, float delayBetweenSpawns) {
	//	if (runner == null) {
	//		return null;
	//	}
			
	//	return runner.StartCoroutine(SpawnBurstRoutine(prefab, count, delayBetweenSpawns));
	//}

	// Returns TRUE if at least one more object can be spawned
	public bool CanSpawn() {
		return aliveObjects.Count < maxAlive;
	}

	// Despawns all currently active objects from this spawner
	// Useful for resets, checkpoints etc
	public void DespawnAll() {
		// Copy first because 'Release' modifies the list
		List<PooledObject> temp = new List<PooledObject>(aliveObjects);

		foreach (PooledObject obj in temp) {
			if (obj != null) {
				obj.ReturnToPool();
			}
		}
	}

	// Creates or returns the pool for a specific prefab
	private IObjectPool<PooledObject> GetOrCreatePool(PooledObject prefab) {

		if (pools.TryGetValue(prefab, out var existingPool)) {
			return existingPool;
		}
			
		IObjectPool<PooledObject> newPool = CreatePool(prefab);
		pools.Add(prefab, newPool);

		return newPool;
	}

	private IObjectPool<PooledObject> CreatePool(PooledObject prefab) {
		IObjectPool<PooledObject> createdPool = null;

		createdPool = new ObjectPool<PooledObject>(
			() => CreatePooledItem(prefab, createdPool),
			OnTakeFromPool,
			OnReturnedToPool,
			OnDestroyPoolObject,
			true,
			defaultCapacityPerPrefab,
			maxPoolSizePerPrefab
		);

		return createdPool;
	}

	private PooledObject CreatePooledItem(PooledObject prefab, IObjectPool<PooledObject> ownerPool) {
		PooledObject item = Instantiate(prefab);
		item.SetPool(ownerPool);
		item.SourcePrefab = prefab;
		item.gameObject.SetActive(false);

		return item;
	}

	// Handles the full multi-wave encounter flow
	private IEnumerator WaveSequenceRoutine() {
		StopSpawning();

		if (waves == null || waves.Length == 0) {
			waveRoutine = null;
			yield break;
		}

		for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++) {
			Wave wave = waves[waveIndex];
			aliveWaveObjects.Clear();

			if (wave.entries != null) {
				for (int entryIndex = 0; entryIndex < wave.entries.Length; entryIndex++) {
					SpawnEntry entry = wave.entries[entryIndex];

					if (entry == null || entry.prefab == null || entry.count <= 0) {
						continue;
					}
						
					float delay = Mathf.Max(0.0f, entry.delayBetweenSpawns);
					WaitForSeconds wait = new WaitForSeconds(delay);

					// Uses same prefab for every spawn in that entry
					PooledObject prefabToSpawn = ResolvePrefabForEntry(entry);

					for (int i = 0; i < entry.count; i++) {
						//PooledObject prefabToSpawn = ResolvePrefabForEntry(entry); // Uses different prefab for every spawn in that entry
						//PooledObject obj = Spawn(prefabToSpawn); // DONT NEED

						PooledObject obj = null;

						while (obj == null) {
							obj = Spawn(prefabToSpawn);

							if (obj == null) {
								yield return null; // wait until we can spawn
							}
						}

						aliveWaveObjects.Add(obj);

						if (i < entry.count - 1 && delay > 0.0f) {
							yield return wait;
						}
					}
				}
			}

			// Wait until everything from this wave has been returned to the pool
			yield return new WaitUntil(() => aliveWaveObjects.Count == 0);

			if (wave.delayAfterWaveCleared > 0.0f) {
				yield return new WaitForSeconds(wave.delayAfterWaveCleared);
			}
		}

		waveRoutine = null;
	}

	private PooledObject ResolvePrefabForEntry(SpawnEntry entry) {
		if (entry == null) {
			return null;
		}
			
		if (entry.spawnSource == SpawnEntry.SpawnSource.SpecificPrefab) {
			return entry.prefab;
		}
			
		return GetRandomPrefabForCategory(entry.category);
	}

	private PooledObject GetRandomPrefabForCategory(SpawnCategory category) {
		if (availablePrefabs == null || availablePrefabs.Length == 0) {
			return null;
		}
			
		List<PooledObject> matches = new List<PooledObject>();

		for (int i = 0; i < availablePrefabs.Length; i++) {
			PooledObject prefab = availablePrefabs[i];

			if (prefab == null) {
				continue;
			}

			if (category == SpawnCategory.Any || prefab.SpawnCategory == category) {
				matches.Add(prefab);
			}
		}

		if (matches.Count == 0) {
			return null;
		}
			
		return matches[Random.Range(0, matches.Count)];
	}

	//// Spawns gradually based on delayBetweenSpawns
	//private IEnumerator SpawnBurstRoutine(PooledObject prefab, int count, float delayBetweenSpawns) {
	//	if (prefab == null || count <= 0) {
	//		yield break;
	//	}

	//	float delay = Mathf.Max(0.0f, delayBetweenSpawns);
	//	WaitForSeconds wait = new WaitForSeconds(delay);

	//	for (int i = 0; i < count; i++) {
	//		Spawn(prefab);

	//		if (i < count - 1 && delay > 0.0f) {
	//			yield return wait;
	//		}
	//	}
	//}

	// Simple repeated auto-spawn loop
	private IEnumerator SpawnLoop() {
		while (true) {
			Spawn(autoSpawnPrefab);
			yield return cachedWait;
		}
	}

	// Attempts to get a free spawn point using the clearance check
	private bool TryGetFreeSpawnPoint(PooledObject prefab, out Transform chosenPoint) {
		chosenPoint = null;

		if (spawnPoints == null || spawnPoints.Length == 0) {
			return false;
		}

		List<EnemySpawnPoint> validPoints = new List<EnemySpawnPoint>();

		//if (spawnPoints == null || spawnPoints.Length == 0) {
		//	if (Physics.CheckSphere(transform.position, spawnCheckRadius, spawnBlockingLayers, triggerInteraction) == false) {
		//		chosenPoint = transform;
		//		return true;
		//	}

		//	return false;
		//}

		//List<Transform> validPoints = new List<Transform>();

		//int startIndex = Random.Range(0, spawnPoints.Length);

		for (int i = 0; i < spawnPoints.Length; i++) {
			EnemySpawnPoint spawnPoint = spawnPoints[i];

			if (spawnPoint == null || spawnPoint.point == null) {
				continue;
			}

			if (CanPrefabUsePoint(prefab, spawnPoint) == false) {
				continue;
			}

			Vector3 checkPosition = spawnPoint.point.position + spawnPoint.clearanceOffset;
			float checkRadius = Mathf.Max(0.0f, spawnPoint.clearanceRadius);


			//int index = (startIndex + i) % spawnPoints.Length;
			//Transform point = spawnPoints[index];

			bool blocked = false;

			if (spawnPoint.ignoreClearance == false) {
				blocked = Physics.CheckSphere(checkPosition, checkRadius, spawnBlockingLayers, triggerInteraction);
			}

			if (blocked == false) {
				validPoints.Add(spawnPoint);
			}
		}

		if (validPoints.Count == 0) {
			return false;
		}

		EnemySpawnPoint chosen = validPoints[Random.Range(0, validPoints.Count)];
		chosenPoint = chosen.point;
		return true;
	}

	// Check if prefab can use the spawn point
	private bool CanPrefabUsePoint(PooledObject prefab, EnemySpawnPoint spawnPoint) {
		// If no restriction -> allow everything
		if (spawnPoint.allowedCategories == null || spawnPoint.allowedCategories.Length == 0) {
			return true;
		}

		SpawnCategory category = prefab.SpawnCategory;

		for (int i = 0; i < spawnPoint.allowedCategories.Length; i++) {
			if (spawnPoint.allowedCategories[i] == category || spawnPoint.allowedCategories[i] == SpawnCategory.Any) {
				return true;
			}
		}

		return false;
	}

	// Called when an object is taken from its pool (spawned in)
	private void OnTakeFromPool(PooledObject item) {
		if (hasPendingSpawnPose) {
			item.transform.SetPositionAndRotation(pendingSpawnPosition, pendingSpawnRotation);
		}

		aliveObjects.Add(item);

		EnemyCombat enemyCombat = item.GetComponent<EnemyCombat>();
		if (enemyCombat != null && enemyTracker != null) {
			enemyCombat.SetEnemyTracker(enemyTracker);
			enemyTracker.EnemySpawned();
		}

		item.gameObject.SetActive(true);

		foreach (IPoolSpawnHandler handler in item.GetComponents<IPoolSpawnHandler>()) {
			handler.OnSpawned();
		}
	}

	// Called when an object is returned to its pool
	private void OnReturnedToPool(PooledObject item) {
		// Remove from active tracking
		aliveObjects.Remove(item);
		aliveWaveObjects.Remove(item);

		// Tracks enemies that have despawned but not died (i.e. despawn on player death)
		EnemyCombat enemyCombat = item.GetComponent<EnemyCombat>();
		if (enemyCombat != null && enemyTracker != null && enemyCombat.HasReportedDeath == false) {
			enemyTracker.EnemyDespawnedAlive();
		}

		// Notify components before disabling
		foreach (IPoolSpawnHandler handler in item.GetComponents<IPoolSpawnHandler>()) {
			handler.OnDespawned();
		}

		// Disable object
		item.gameObject.SetActive(false);
	}

	// Called if a pool permanently destroys an object because the pool exceeded max size
	private void OnDestroyPoolObject(PooledObject item) {
		if (item != null && item.gameObject != null) {
			Destroy(item.gameObject);
		}
	}

	// Prevent invalid parameter values
	private void OnValidate() {
		if (spawnInterval < 0.01f) {
			spawnInterval = 0.01f;
		}

		if (defaultCapacityPerPrefab < 0) {
			defaultCapacityPerPrefab = 0;
		}
			
		if (defaultCapacityPerPrefab < 1) {
			defaultCapacityPerPrefab = 1;
		}
		
		if (maxAlive < 1) {
			maxAlive = 1;
		}
	}

	private void OnDestroy() {
		StopSpawning();
		StopWaveSequence();

		foreach (KeyValuePair<PooledObject, IObjectPool<PooledObject>> keyValuePair in pools) {
			keyValuePair.Value.Clear();
		}

		pools.Clear();
		aliveObjects.Clear();
		aliveWaveObjects.Clear();
	}

	// Visualise the spawn point check radius as a gizmo in scene view
	private void OnDrawGizmosSelected() {
		if (spawnPoints == null) {
			return;
		}

		for (int i = 0; i < spawnPoints.Length; i++) {
			EnemySpawnPoint spawnPoint = spawnPoints[i];

			if (spawnPoint == null || spawnPoint.point == null) {
				continue;
			}

			Vector3 checkPosition = spawnPoint.point.position + spawnPoint.clearanceOffset;
			float checkRadius = Mathf.Max(0f, spawnPoint.clearanceRadius);

			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(checkPosition, checkRadius);

			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(spawnPoint.point.position, checkPosition);
		}
	}

	//// Gets a random spawn point or default to this transform
	//private Transform GetSpawnPoint() {
	//	if (spawnPoints != null && spawnPoints.Length > 0) {
	//		return spawnPoints[Random.Range(0, spawnPoints.Length)];
	//	}

	//	return transform;
	//}
}