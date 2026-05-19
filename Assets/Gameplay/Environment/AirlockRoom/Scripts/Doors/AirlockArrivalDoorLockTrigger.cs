using UnityEngine;

// Trigger placed just after the new scene's airlock exit door
// When the player walks through this trigger, it tells AirlockArrivalDoorOpener
// to close and lock the arrival door behind them
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class AirlockArrivalDoorLockTrigger : MonoBehaviour {
	[Header("Arrival Door")]
	[Tooltip("Arrival door opener that owns the door to close and lock after the player passes this trigger.")]
	[SerializeField] private AirlockArrivalDoorOpener arrivalDoorOpener;

	[Header("Trigger Rules")]
	[Tooltip("Only colliders with this tag can activate this trigger.")]
	[SerializeField] private string playerTag = "Player";
	[Tooltip("If true, this trigger disables itself after it fires once.")]
	[SerializeField] private bool triggerOnce = true;

	[Header("Debug")]
	[Tooltip("If true, trigger activations are logged to the Unity Console.")]
	[SerializeField] private bool debugLogging = false;

	private Collider cachedCollider;
	private bool hasTriggered;

	private void Reset() {
		arrivalDoorOpener = GetComponentInParent<AirlockArrivalDoorOpener>();
		cachedCollider = GetComponent<Collider>();
		EnsureTriggerCollider();
	}

	private void Awake() {
		if (arrivalDoorOpener == null) {
			arrivalDoorOpener = GetComponentInParent<AirlockArrivalDoorOpener>();
		}

		cachedCollider = GetComponent<Collider>();
		EnsureTriggerCollider();
	}

	private void OnTriggerEnter(Collider other) {
		if (hasTriggered && triggerOnce) {
			return;
		}

		if (IsValidPlayer(other) == false) {
			return;
		}

		if (arrivalDoorOpener == null) {
			Debug.LogWarning($"{name}: No AirlockArrivalDoorOpener assigned.", this);
			return;
		}

		hasTriggered = true;
		arrivalDoorOpener.CloseAndLockAfterPlayerExit();

		Log("Player passed arrival door lock trigger.");

		// Disable the trigger collider so it cannot repeatedly call the lock method
		if (triggerOnce && cachedCollider != null) {
			cachedCollider.enabled = false;
		}
	}

	private bool IsValidPlayer(Collider other) {
		if (other == null) {
			return false;
		}

		return string.IsNullOrWhiteSpace(playerTag) || other.CompareTag(playerTag);
	}

	private void EnsureTriggerCollider() {
		if (cachedCollider != null) {
			cachedCollider.isTrigger = true;
		}
	}

	private void OnValidate() {
		cachedCollider = GetComponent<Collider>();
		EnsureTriggerCollider();
	}

	private void Log(string message) {
		if (debugLogging) {
			Debug.Log($"{name}: {message}", this);
		}
	}
}
