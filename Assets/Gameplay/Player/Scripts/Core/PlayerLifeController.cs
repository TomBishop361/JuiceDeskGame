using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Healh regen
// Respawning - call checkpoint

// TODO: Can create a IRespawnable interface instead where each script can reset themselves using OnRespawn()
public class PlayerLifeController : MonoBehaviour {
	[Header("References")]
	[SerializeField] private Health health;
	[SerializeField] private Rigidbody rb;
	[SerializeField] private GameObject playerModel;

	[Header("Regen")]
	[SerializeField] private bool enableRegen = true;
	[SerializeField] private float regenDelay = 3.0f;
	[SerializeField] private float regenPerSecond = 0.5f;

	[Header("Death / Respawn")]
	[SerializeField] private float respawnDelay = 1.0f;
	[SerializeField] private float respawnInvulnerabilityTime = 1.5f;

	private float previousHealth;
	private float lastDamageTime = -Mathf.Infinity;
	private bool isDead = false;

	private void Awake() {
		if (health == null) {
			health = GetComponent<Health>();
		}
		if (rb == null) {
			rb = GetComponent<Rigidbody>();
		}
	}

	private void Start() {
		// Treat spawn as "recently damaged" so regen does not start instantly at time 0
		lastDamageTime = Time.time;
	}

	private void Update() {
		if (health == null || isDead) {
			return;
		}
		if (enableRegen == false) {
			return;
		}

		bool atFullHealth = health.CurrentHealth >= health.MaxHealth;
		bool regenDelayFinished = Time.time >= lastDamageTime + regenDelay;

		if (atFullHealth == false && regenDelayFinished) {
			health.RegenerateHealth(regenPerSecond * Time.deltaTime);
		}
	}

	private IEnumerator RespawnRoutine() {
		// Hide player visual
		playerModel.SetActive(false);

		// Disable input components
		GetComponent<PlayerInput>().enabled = false;
		GetComponent<InputController>().enabled = false;

		// Reset physics
		rb.linearVelocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;

		// Optional: disable input / play animation/particles here
		yield return new WaitForSeconds(respawnDelay);

		// Move player to checkpoint
		CheckpointManager.Instance.Respawn();

		ResetPlayerState();
	}

	private IEnumerator RespawnInvulnerability() {
		health.SetInvulnerable(true);

		yield return new WaitForSeconds(respawnInvulnerabilityTime);

		health.SetInvulnerable(false);
	}

	private void ResetPlayerState() {
		// TODO: Add more state resets here like resetting animations etc.

		// Reset health
		if (health != null) {
			health.RestoreFullHealth();
		}

		// Reset player visual
		playerModel.SetActive(true);

		// Reset death state
		isDead = false;

		StartCoroutine(RespawnInvulnerability());

		// Re-enable input components
		GetComponent<PlayerInput>().enabled = true;
		GetComponent<InputController>(). enabled = true;
	}

	private void HandleHealthChanged(float current, float max) {
		// Only reset regen timer when health goes down
		if (current < previousHealth) {
			lastDamageTime = Time.time;
		}

		previousHealth = current;
	}

	private void HandleDeath() {
		if (isDead) {
			return;
		}
		isDead = true;

		StartCoroutine(RespawnRoutine());

		// Optional extra death handling here:
		// - disable attack
		// - disable movement
		// - show death UI
		// - trigger respawn manager
	}

	private void OnEnable() {
		if (health == null) {
			return;
		}

		previousHealth = health.CurrentHealth;
		health.OnHealthChanged += HandleHealthChanged;
		health.OnDeath += HandleDeath;
	}

	private void OnDisable() {
		if (health == null) {
			return;
		}

		health.OnHealthChanged -= HandleHealthChanged;
		health.OnDeath -= HandleDeath;
	}
}