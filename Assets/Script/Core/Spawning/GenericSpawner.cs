using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// Generic reusable spawner that works with any prefab using PooledObject
// Handles spawning, pooling, and lifetime tracking
public class GenericSpawner : MonoBehaviour {
	[Header("Prefab")]
	[Tooltip("The prefab to spawn (must have 'PooledObject' attached")]
	[SerializeField] private PooledObject prefab;

	[Header("Spawn Points")]
	[Tooltip("Optional list of spawn locations (randomly chosen)")]
	[SerializeField] private Transform[] spawnPoints;

	[Header("Timing")]
	[Tooltip("If TRUE, spawning starts automatically")]
	[SerializeField] private bool autoSpawn = true;
	[Tooltip("Time between spawns")]
	[SerializeField] private float spawnInterval = 2.0f;

	[Header("Limits")]
	[Tooltip("Maximum number of active objects at once")]
	[SerializeField] private int maxAlive = 10;

	[Header("Spawn Clearance")]
	[Tooltip("Radius used to check if a spawn point is free of other colliders")]
	[SerializeField] private float spawnCheckRadius = 1.5f;
	[Tooltip("Layers that block spawning. If any collider in these layers is inside the radius, spawn is prevented")]
	[SerializeField] private LayerMask spawnBlockingLayers;
	[Tooltip("Whether trigger colliders should be considered when checking spawn space")]
	[SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

	[Header("Pool Settings")]
	[Tooltip("Initial number of objects pre-created in the pool (when needed)")]
	[SerializeField] private int defaultCapacity = 10;
	[Tooltip("Maximum number of pooled objects allowed before extra ones are destroyed")]
	[SerializeField] private int maxPoolSize = 20;

	private IObjectPool<PooledObject> pool;
	private readonly HashSet<PooledObject> aliveObjects = new();

	private Coroutine spawnRoutine;
	private WaitForSeconds cachedWait;

	private void Awake() {
		cachedWait = new WaitForSeconds(spawnInterval);

		// Create object pool
		pool = new ObjectPool<PooledObject>(
			CreatePooledItem,
			OnTakeFromPool,
			OnReturnedToPool,
			OnDestroyPoolObject,
			collectionCheck: true,
			defaultCapacity: defaultCapacity,
			maxSize: maxPoolSize
		);
	}

	private void Start() {
		if (autoSpawn == true) {
			StartSpawning();
		}
	}

	// Starts the continuous spawn loop
	public void StartSpawning() {
		if (spawnRoutine == null) {
			spawnRoutine = StartCoroutine(SpawnLoop());
		}
	}

	// Stops the spawn loop
	public void StopSpawning() {
		if (spawnRoutine != null) {
			StopCoroutine(spawnRoutine);
			spawnRoutine = null;
		}
	}

	public Coroutine SpawnBurst(MonoBehaviour runner, int count, float delayBetweenSpawns) {
		return runner.StartCoroutine(SpawnBurstRoutine(count, delayBetweenSpawns));
	}

	// Spawns gradually based on the 'delayBetweenSpawns' value
	private IEnumerator SpawnBurstRoutine(int count, float delayBetweenSpawns) {
		WaitForSeconds wait = new WaitForSeconds(delayBetweenSpawns);

		for (int i = 0; i < count; i++) {
			Spawn();

			if (i < count - 1) {
				yield return wait;
			}
		}
	}

	public bool CanSpawn() {
		return aliveObjects.Count < maxAlive;
	}

	// Spawns a single object (if under maxAlive limit)
	public PooledObject Spawn() {
		if (aliveObjects.Count >= maxAlive)
			return null;

		if (TryGetFreeSpawnPoint(out Transform point) == false) {
			return null;
		}
			
		PooledObject item = pool.Get();
		item.transform.SetPositionAndRotation(point.position, point.rotation);

		return item;
	}

	// Attempts to get a spawn point that is available based on a given radius and layer check
	private bool TryGetFreeSpawnPoint(out Transform chosenPoint) {
		chosenPoint = null;

		if (spawnPoints == null || spawnPoints.Length == 0) {
			if (Physics.CheckSphere(transform.position, spawnCheckRadius, spawnBlockingLayers, triggerInteraction) == false) {
				chosenPoint = transform;
				return true;
			}

			return false;
		}

		int startIndex = Random.Range(0, spawnPoints.Length);

		for (int i = 0; i < spawnPoints.Length; i++) {
			int index = (startIndex + i) % spawnPoints.Length;
			Transform point = spawnPoints[index];

			bool blocked = Physics.CheckSphere(point.position, spawnCheckRadius, spawnBlockingLayers, triggerInteraction);

			if (blocked == false) {
				chosenPoint = point;
				return true;
			}
		}

		return false;
	}

	// Despawns all active objects
	// Useful for resets, checkpoints etc
	public void DespawnAll() {
		// Copy first because 'Release' modifies the list
		var temp = new List<PooledObject>(aliveObjects);
		
		foreach (var obj in temp) {
			if (obj != null) {
				obj.ReturnToPool();
			}
		}
	}

	// Coroutine that repeatedly spawns objects
	private IEnumerator SpawnLoop() {
		while (true) {
			Spawn();

			yield return cachedWait;
		}
	}

	// Creates a brand new pooled object (only when needed)
	private PooledObject CreatePooledItem() {
		PooledObject item = Instantiate(prefab);

		// Assign this pool to the object
		item.SetPool(pool);

		// Start inactive until used
		item.gameObject.SetActive(false);

		return item;
	}

	// Called when an object is taken from the pool (spawned)
	private void OnTakeFromPool(PooledObject item) {
		aliveObjects.Add(item);
		item.gameObject.SetActive(true);

		foreach (var handler in item.GetComponents<IPoolSpawnHandler>())
			handler.OnSpawned();

		//// Move to a spawn position
		//Transform point = GetSpawnPoint();
		//item.transform.SetPositionAndRotation(point.position, point.rotation);

		//// Track active object
		//aliveObjects.Add(item);

		//// Enable the object
		//item.gameObject.SetActive(true);

		//// Notify any components that are subscribed to spawn events
		//foreach (var handler in item.GetComponents<IPoolSpawnHandler>()) {
		//	handler.OnSpawned();
		//}
	}

	// Called when an object is returned to the pool
	private void OnReturnedToPool(PooledObject item) {
		// Remove from active tracking
		aliveObjects.Remove(item);

		// Notify components before disabling
		foreach (var handler in item.GetComponents<IPoolSpawnHandler>()) {
			handler.OnDespawned();
		}

		// Disable object
		item.gameObject.SetActive(false);
	}

	// Called when the pool destroys an object permanently
	// This happens if pool exceeds max size
	private void OnDestroyPoolObject(PooledObject item) {
		if (item != null && item.gameObject != null) {
			Destroy(item.gameObject);
		}
	}

	// Gets a random spawn point or default to this transform
	private Transform GetSpawnPoint() {
		if (spawnPoints != null && spawnPoints.Length > 0) {
			return spawnPoints[Random.Range(0, spawnPoints.Length)];
		}
			
		return transform;
	}

	private void OnValidate() {
		// Prevent invalid spawn interval
		if (spawnInterval < 0.01f) {
			spawnInterval = 0.01f;
		}
	}

	private void OnDestroy() {
		StopSpawning();

		if (pool != null) {
			pool.Clear();
			pool = null;
		}

		aliveObjects.Clear();
	}


	
}