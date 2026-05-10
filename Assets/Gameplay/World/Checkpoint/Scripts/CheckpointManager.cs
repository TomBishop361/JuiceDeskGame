using UnityEngine;
using UnityEngine.InputSystem;

public class CheckpointManager : MonoBehaviour {
	public static CheckpointManager Instance;

	[SerializeField] private Rigidbody player;
	[SerializeField] private Vector3 currentCheckpoint;
	[SerializeField] private bool hasCheckpoint;

	private void Awake() {
		if (Instance == null) {
			Instance = this;
		} 
		else {
			Destroy(gameObject);
			return;
		}

		if (player != null) {
			currentCheckpoint = player.position; // default spawn
		}
	}

	private void Start()
	{
		player = GameObject.FindGameObjectWithTag("Player").GetComponent<Rigidbody>();
	}

    public void SetPlayer(Rigidbody newPlayer) {
		player = newPlayer;

		if (hasCheckpoint == false) {
			currentCheckpoint = player.position;
		}
	}

	public void SetCheckpoint(Vector3 newCheckpoint) {
		currentCheckpoint = newCheckpoint;
		hasCheckpoint = true;
	}

	public void Respawn() {
		if (player == null) {
			return;
		}

		player.position = currentCheckpoint;
	}
}