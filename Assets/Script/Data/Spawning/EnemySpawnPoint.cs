using UnityEngine;
[System.Serializable]
public class EnemySpawnPoint {
	[Tooltip("Transform used as the actual spawn position/rotation")]
	public Transform point;
	[Tooltip("Which categories can spawn here")]
	public SpawnCategory[] allowedCategories;

	[Tooltip("If TRUE, ignore clearance check for this point")]
	public bool ignoreClearance = false;
	[Tooltip("Local offset for the spawn clearance check")]
	public Vector3 clearanceOffset = Vector3.up * 1.0f;
	[Tooltip("Radius used for this point's clearance check")]
	public float clearanceRadius = 1.0f;
}