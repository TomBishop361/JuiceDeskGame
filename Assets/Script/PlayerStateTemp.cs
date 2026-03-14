using UnityEngine;

public class PlayerStateTemp : MonoBehaviour {
	[Header("References")]
	[SerializeField] private Health health;
	[SerializeField] private Animator animator;
	[SerializeField] private MonoBehaviour[] scriptsToDisableOnDeath;

	[Header("Regen")]
	[SerializeField] private bool enableRegen = true;
	[SerializeField] private float regenDelay = 3.0f;
	[SerializeField] private float regenPerSecond = 10.0f;

	[Header("Death")]
	[SerializeField] private bool disableInputOnDeath = true;

	private float previousHealth;
	private float lastDamageTime = -Mathf.Infinity;
	private bool isDead = false;

	private void Awake() {
		if (health == null) {
			health = GetComponent<Health>();
		}

		if (animator == null) {
			animator = GetComponentInChildren<Animator>();
		}
	}

	private void OnEnable() {
		if (health == null) return;

		previousHealth = health.CurrentHealth;
		health.OnHealthChanged += HandleHealthChanged;
		health.OnDeath += HandleDeath;
	}

	private void OnDisable() {
		if (health == null) return;

		health.OnHealthChanged -= HandleHealthChanged;
		health.OnDeath -= HandleDeath;
	}

	private void Start() {
		// Treat spawn as "recently damaged" so regen does not start instantly at time 0
		lastDamageTime = Time.time;
	}

	private void Update() {
		if (health == null || isDead) return;
		if (!enableRegen) return;

		bool atFullHealth = health.CurrentHealth >= health.MaxHealth;
		bool regenDelayFinished = Time.time >= lastDamageTime + regenDelay;

		if (!atFullHealth && regenDelayFinished) {
			health.RegenerateHealth(regenPerSecond * Time.deltaTime);
		}
	}

	private void HandleHealthChanged(float current, float max) {
		// Only reset regen timer when health goes down
		if (current < previousHealth) {
			lastDamageTime = Time.time;
		}

		previousHealth = current;
	}

	private void HandleDeath() {
		if (isDead) return;
		isDead = true;
		Destroy(gameObject);

		//if (disableInputOnDeath) {
		//	foreach (MonoBehaviour script in scriptsToDisableOnDeath) {
		//		if (script != null) {
		//			script.enabled = false;
		//		}
		//	}
		//}

		// Optional extra death handling here:
		// - disable attack
		// - disable movement
		// - show death UI
		// - trigger respawn manager
	}

	public bool IsDead => isDead;
}