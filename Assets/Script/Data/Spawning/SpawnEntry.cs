using UnityEngine;

[System.Serializable]
public class SpawnEntry {
	public enum SpawnSource {
		SpecificPrefab,
		Category
	}

	[Tooltip("Whether this entry spawns one exact prefab or chooses from a category")]
	public SpawnSource spawnSource = SpawnSource.SpecificPrefab;

	[Tooltip("Used when Spawn Source is 'Specific Prefab'")]
	public PooledObject prefab;

	[Tooltip("Used when Spawn Source is 'Category'")]
	public SpawnCategory category = SpawnCategory.Any;

	[Tooltip("How many enemies to spawn for this entry")]
	public int count = 1;

	[Tooltip("Delay between each spawn")]
	public float delayBetweenSpawns = 0.25f;
}