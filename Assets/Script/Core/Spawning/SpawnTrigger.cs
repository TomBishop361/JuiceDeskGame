using UnityEngine;

public class SpawnTrigger : MonoBehaviour {
	[SerializeField] private GenericSpawner spawner;
	[SerializeField] private string targetTag = "Player";
	[SerializeField] private bool triggerOnce = true;

	[Header("Burst Spawn")]
	[SerializeField] private int spawnCount = 3;
	[SerializeField] private float delayBetweenSpawns = 0.5f;

	private bool hasTriggered;

	private void OnTriggerEnter(Collider other) {
		if (hasTriggered && triggerOnce) {
			return;
		}

		if (other.CompareTag(targetTag) == false) {
			return;
		}

		// NOTE: Instant spawn
		//for (int i = 0; i < spawnCount; i++) {
		//	spawner.Spawn();
		//}

		if (spawner.CanSpawn() == true) {
			spawner.SpawnBurst(this, spawnCount, delayBetweenSpawns);
		}
		
		hasTriggered = true;

		// If TRUE disable trigger collider
		if (triggerOnce == true) {
			GetComponent<Collider>().enabled = false;
		}
	}

	// Visualise the spawn trigger gizmo in scene view
	private void OnDrawGizmos() {
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(transform.position, GetComponent<BoxCollider>().size);
	}
}