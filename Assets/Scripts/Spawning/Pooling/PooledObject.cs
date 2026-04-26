using UnityEngine;
using UnityEngine.Pool;

// Base component required for any object that will be pooled
// Handles returning itself back to its assigned pool
public class PooledObject : MonoBehaviour {
	// Reference to the pool that owns this object
	private IObjectPool<PooledObject> pool;

	public PooledObject SourcePrefab { get; set; }


	// Called by the spawner when the object is first created
	// Assigns which pool this object belongs to
	public void SetPool(IObjectPool<PooledObject> objectPool) {
		pool = objectPool;
	}

	// Returns this object back to the pool
	public void ReturnToPool() {
		if (pool == null || this == null || gameObject == null) {
			return;
		}

		pool.Release(this);
	}
}