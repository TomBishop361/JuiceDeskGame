using UnityEngine;

public class SpawnTrigger : MonoBehaviour {
	[SerializeField] private GenericSpawner spawner;
	[SerializeField] private string targetTag = "Player";
	[SerializeField] private bool triggerOnce = true;

	private bool hasTriggered;

	private void OnTriggerEnter(Collider other) {
		if (hasTriggered && triggerOnce) {
			return;
		}

		if (other.CompareTag(targetTag) == false) {
			return;
		}

		if (spawner.IsWaveRunning) {
			return;
		}
			
		spawner.StartWaveSequence();
		
		hasTriggered = true;

		// If TRUE disable trigger collider
		if (triggerOnce == true) {
			GetComponent<Collider>().enabled = false;
		}
	}

	// Visualise the spawn trigger gizmo in scene view
	private void OnDrawGizmos() {
		Gizmos.color = Color.green;

		BoxCollider box = GetComponent<BoxCollider>();
		if (box != null) {
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube(box.center, box.size);
		}
	}
}