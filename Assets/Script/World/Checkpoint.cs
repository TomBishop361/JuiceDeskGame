using UnityEngine;

public class Checkpoint : MonoBehaviour {
	[Header("Visuals")]
	[SerializeField] private Material checkpointInactiveMaterial;
	[SerializeField] private Material checkpointActivatedMaterial;

	private bool activated = false;

	private void Awake() {
		if (checkpointInactiveMaterial != null) {
			gameObject.GetComponent<MeshRenderer>().material = checkpointInactiveMaterial;
		}
	}

	private void OnTriggerEnter(Collider other) {
		if (activated == true) {
			return;
		}

		if (other.CompareTag("Player") == true) {
			CheckpointManager.Instance.SetCheckpoint(transform.position + Vector3.up * 1.5f);
			activated = true;

			// Change material to visualise checkpoint activation
			if (checkpointActivatedMaterial != null) {
				gameObject.GetComponent<MeshRenderer>().material = checkpointActivatedMaterial;
			}
		}
	}
}