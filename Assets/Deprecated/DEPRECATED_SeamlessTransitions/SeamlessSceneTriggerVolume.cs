using UnityEngine;
using System;


#if UNITY_EDITOR
using UnityEditor;
#endif

[Obsolete("SeamlessSceneTriggerVolume is now obsolete. Use the AirlockTransitionVolume instead.")]
// Trigger volume used by SeamlessScenePortal
// Typical setup:
// - A larger trigger before the door uses Preload
// - A thinner trigger at / just beyond the doorway uses Commit
//
// The trigger itself does not load scenes
// It only tells the linked portal what stage the player has reached
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class SeamlessSceneTriggerVolume : MonoBehaviour {
	// Defines what this trigger asks the linked SeamlessScenePortal to do
	public enum TriggerAction {
		// Start loading the next scene additively before the player reaches the doorway
		Preload = 0,

		// Commit the player into the streamed scene once they cross the doorway
		Commit = 1
	}

	[Header("Portal")]
	[Tooltip("Portal that receives this trigger event.")]
	[SerializeField] private SeamlessScenePortal portal;
	[Tooltip("Preload should be used before the door | Commit should be used at or just beyond the doorway.")]
	[SerializeField] private TriggerAction action = TriggerAction.Preload;

	[Header("Trigger Rules")]
	[Tooltip("Only colliders with this tag can activate the trigger.")]
	[SerializeField] private string targetTag = "Player";
	[Tooltip("If true, this trigger fires once then disables itself.")]
	[SerializeField] private bool triggerOnce = false;
	[Tooltip("If true, tells the portal when the player exits this trigger.")]
	[SerializeField] private bool notifyPortalOnExit = true;

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
		portal = GetComponentInParent<SeamlessScenePortal>();
		cachedCollider = GetComponent<Collider>();

		if (cachedCollider != null) {
			cachedCollider.isTrigger = true;
		}
	}

	private void Awake() {
		if (portal == null) {
			portal = GetComponentInParent<SeamlessScenePortal>();
		}

		cachedCollider = GetComponent<Collider>();
		if (cachedCollider != null) {
			cachedCollider.isTrigger = true;
		}
	}

	private void OnTriggerEnter(Collider other) {
		if (hasTriggered && triggerOnce) {
			return;
		}

		if (IsValidTarget(other) == false) {
			return;
		}

		if (portal == null) {
			Debug.LogWarning($"{name}: No SeamlessScenePortal assigned.", this);
			return;
		}

		// This trigger reports that the player entered the preload/commit zone (the portal owns the actual scene-loading logic)
		portal.HandleTriggerEntered(action, other);
		hasTriggered = true;

		if (triggerOnce && cachedCollider != null) {
			// Disabling the collider prevents repeat calls without disabling the whole GameObject
			cachedCollider.enabled = false;
		}
	}

	private void OnTriggerExit(Collider other) {
		if (notifyPortalOnExit == false || portal == null || IsValidTarget(other) == false) {
			return;
		}

		// Exit notifications let the portal know whether the player is still waiting near the door
		// Example: if loading finishes while the player is still inside the preload zone, the door can auto-open
		portal.HandleTriggerExited(action, other);
	}

	// Returns true if the collider is allowed to activate this trigger
	private bool IsValidTarget(Collider other) {
		if (other == null) {
			return false;
		}

		// Empty target tag allows any collider to activate the trigger
		// Otherwise, only the configured player tag is accepted
		return string.IsNullOrWhiteSpace(targetTag) || other.CompareTag(targetTag);
	}

	private void OnValidate() {
		cachedCollider = GetComponent<Collider>();

		if (cachedCollider != null) {
			// Keeps the collider correctly configured while editing
			// Prevents accidentally turning the transition zone into a physical wall
			cachedCollider.isTrigger = true;
		}
	}

	private void OnDrawGizmos() {
		DrawTriggerGizmo(false);
	}

	private void OnDrawGizmosSelected() {
		DrawTriggerGizmo(true);
	}

	// Draws the trigger shape in the Scene view so preload and commit zones are easy to place
	private void DrawTriggerGizmo(bool selected) {
		Collider triggerCollider = cachedCollider != null ? cachedCollider : GetComponent<Collider>();
		if (triggerCollider == null) {
			return;
		}

		Color previousColor = Gizmos.color;
		Gizmos.color = selected ? Color.white : gizmoColor;

		if (triggerCollider is BoxCollider boxCollider) {
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
			Gizmos.matrix = Matrix4x4.identity;
		}
		else if (triggerCollider is SphereCollider sphereCollider) {
			Vector3 worldCenter = transform.TransformPoint(sphereCollider.center);
			float maxScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
			Gizmos.DrawWireSphere(worldCenter, sphereCollider.radius * maxScale);
		}
		else {
			Gizmos.DrawWireCube(triggerCollider.bounds.center, triggerCollider.bounds.size);
		}

		Gizmos.color = previousColor;

#if UNITY_EDITOR
		if (drawLabel) {
			Handles.color = selected ? Color.white : gizmoColor;
			Handles.Label(transform.position, $"Seamless Trigger: {action}");
		}
#endif
	}
}