using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Trigger volume used by AirlockSceneTransitionPortal
// It only tells the portal which airlock stage the player has entered
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class AirlockTransitionVolume : MonoBehaviour {
	public enum TriggerAction {
		// Starts to unlock as the player reaches the exit door
		Approach = 0,

		// Reports that the player has entered the old-scene airlock chamber so the first door can close behind them
		AirlockEntered = 1,

		// Starts the sealed airlock handoff sequence
		Commit = 2
	}

	[Header("Portal")]
	[Tooltip("Airlock portal that receives this trigger event.")]
	[SerializeField] private AirlockSceneTransitionPortal portal;
	[Tooltip("Which stage of transition this trigger represents.")]
	[SerializeField] private TriggerAction action = TriggerAction.Approach;

	[Header("Trigger Rules")]
	[Tooltip("Only colliders with this tag can activate the trigger.")]
	[SerializeField] private string targetTag = "Player";
	[Tooltip("If true, this trigger fires once then disables its collider.")]
	[SerializeField] private bool triggerOnce = false;

	[Header("Gizmos")]
	[Tooltip("Colour used to draw this trigger volume in the Scene view.")]
	[SerializeField] private Color gizmoColor = new Color(0.1f, 0.85f, 1.0f, 0.35f);
	[Tooltip("If true, draws the trigger action as a Scene view label.")]
	[SerializeField] private bool drawLabel = true;
	private Collider cachedCollider;
	private bool hasTriggered;

	// Current action assigned to this trigger
	public TriggerAction Action => action;

	private void Reset() {
		portal = GetComponentInParent<AirlockSceneTransitionPortal>();

		// Make sure the collider is configured as a trigger
		cachedCollider = GetComponent<Collider>();
		EnsureTriggerCollider();
	}

	private void Awake() {
		if (portal == null) {
			portal = GetComponentInParent<AirlockSceneTransitionPortal>();
		}

		cachedCollider = GetComponent<Collider>();
		EnsureTriggerCollider();
	}

	private void OnTriggerEnter(Collider other) {
		// If this trigger is set to fire once and has already fired, ignore later entries
		if (hasTriggered && triggerOnce) {
			return;
		}

		// Ignore anything that is not the player
		if (IsValidTarget(other) == false) {
			return;
		}

		if (portal == null) {
			Debug.LogWarning($"{name}: No AirlockSceneTransitionPortal assigned.", this);
			return;
		}

		// Tell the portal which airlock stage was reached
		// The portal owns the actual door/loading/fade/teleport logic
		portal.HandleAirlockVolumeEntered(action, other);

		hasTriggered = true;

		// Disabling only the collider prevents repeat calls without disabling the whole GameObject
		if (triggerOnce && cachedCollider != null) {
			cachedCollider.enabled = false;
		}
	}

	// Returns true if this collider is allowed to activate the trigger
	private bool IsValidTarget(Collider other) {
		if (other == null) {
			return false;
		}

		// Empty target tag means any collider can activate it
		return string.IsNullOrWhiteSpace(targetTag) || other.CompareTag(targetTag);
	}

	// Keeps the collider as a trigger so it never physically blocks the player
	private void EnsureTriggerCollider() {
		if (cachedCollider != null) {
			cachedCollider.isTrigger = true;
		}
	}

	private void OnValidate() {
		cachedCollider = GetComponent<Collider>();
		EnsureTriggerCollider();
	}

	private void OnDrawGizmos() {
		DrawTriggerGizmo(false);
	}

	private void OnDrawGizmosSelected() {
		DrawTriggerGizmo(true);
	}

	// Draws the trigger shape in the Scene view so preload, airlock-entered,
	// and commit zones are easy to position
	private void DrawTriggerGizmo(bool selected) {
		Collider triggerCollider = cachedCollider != null ? cachedCollider : GetComponent<Collider>();
		if (triggerCollider == null) {
			return;
		}

		Color previousColor = Gizmos.color;
		Matrix4x4 previousMatrix = Gizmos.matrix;

		Gizmos.color = selected ? Color.white : gizmoColor;

		if (triggerCollider is BoxCollider boxCollider) {
			// Draw box colliders in local space so rotation/scale are shown correctly
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
		}
		else if (triggerCollider is SphereCollider sphereCollider) {
			// Draw sphere colliders with world-space scaling
			Vector3 worldCenter = transform.TransformPoint(sphereCollider.center);
			float maxScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
			Gizmos.DrawWireSphere(worldCenter, sphereCollider.radius * maxScale);
		}
		else {
			// Fallback for capsule/mesh/other colliders
			Gizmos.DrawWireCube(triggerCollider.bounds.center, triggerCollider.bounds.size);
		}

		Gizmos.matrix = previousMatrix;
		Gizmos.color = previousColor;

#if UNITY_EDITOR
		if (drawLabel) {
			Handles.color = selected ? Color.white : gizmoColor;
			Handles.Label(transform.position, $"Airlock Trigger: {action}");
		}
#endif
	}
}