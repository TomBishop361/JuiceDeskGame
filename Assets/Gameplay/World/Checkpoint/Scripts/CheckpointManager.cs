using UnityEngine;
using UnityEngine.SceneManagement;

// Tracks the current respawn checkpoint for the active gameplay scene
// Multiple level scenes can briefly be loaded additively at the same time
// This manager therefore avoids destroying duplicates immediately
// Instead, the CheckpointManager in the active gameplay scene becomes Instance
public class CheckpointManager : MonoBehaviour {
	public static CheckpointManager Instance;

	[Header("Player")]
	[Tooltip("Rigidbody of the player that should be respawned.")]
	[SerializeField] private Rigidbody player;

	[Header("Runtime Checkpoint")]
	[Tooltip("Current respawn position for this scene.")]
	[SerializeField] private Vector3 currentCheckpoint;
	[Tooltip("True after a checkpoint has been explicitly set in this scene.")]
	[SerializeField] private bool hasCheckpoint;

	private void Awake() {
		RegisterIfBestInstance();
		ResolvePlayer();
		SetDefaultCheckpointIfNeeded();

		if (Instance == null) {
			Instance = this;
		} 
		else {
			Destroy(gameObject);
			return;
		}

		if (player != null) {
			// default spawn
			currentCheckpoint = player.position; 
		}
	}

	private void OnEnable() {
		SceneManager.activeSceneChanged += HandleActiveSceneChanged;
		RegisterIfBestInstance();
	}

	private void Start() {
		ResolvePlayer();
		SetDefaultCheckpointIfNeeded();
	}

	private void OnDisable() {
		SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
	}

	private void OnDestroy() {
		if (Instance == this) {
			Instance = null;
			AssignInstanceFromActiveScene();
		}
	}

	// Assigns the player at runtime
	// Useful when the Player scene loads additively after the level scene
	public void SetPlayer(Rigidbody newPlayer) {
		player = newPlayer;
		SetDefaultCheckpointIfNeeded();
	}

	// Updates the respawn position for this scene
	public void SetCheckpoint(Vector3 newCheckpoint) {
		currentCheckpoint = newCheckpoint;
		hasCheckpoint = true;
	}

	// Moves the player back to the current checkpoint and clears velocity so respawns are stable
	public void Respawn() {
		ResolvePlayer();

		if (player == null) {
			return;
		}

		player.position = currentCheckpoint;
		player.linearVelocity = Vector3.zero;
		player.angularVelocity = Vector3.zero;
	}

	private void HandleActiveSceneChanged(Scene oldScene, Scene newScene) {
		RegisterIfBestInstance();
	}

	private void RegisterIfBestInstance() {
		Scene activeScene = SceneManager.GetActiveScene();

		// Prefer the checkpoint manager that belongs to the active gameplay scene
		if (gameObject.scene == activeScene || Instance == null) {
			Instance = this;
			ResolvePlayer();
			SetDefaultCheckpointIfNeeded();
		}
	}

	private static void AssignInstanceFromActiveScene() {
		CheckpointManager[] managers = FindObjectsByType<CheckpointManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		Scene activeScene = SceneManager.GetActiveScene();

		for (int i = 0; i < managers.Length; i++) {
			if (managers[i] != null && managers[i].gameObject.scene == activeScene) {
				Instance = managers[i];
				return;
			}
		}

		if (managers.Length > 0) {
			Instance = managers[0];
		}
	}

	private void ResolvePlayer() {
		if (player != null) {
			return;
		}

		GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
		if (playerObject != null) {
			player = playerObject.GetComponent<Rigidbody>();
		}
	}

	private void SetDefaultCheckpointIfNeeded() {
		if (player != null && hasCheckpoint == false) {
			currentCheckpoint = player.position;
		}
	}
}