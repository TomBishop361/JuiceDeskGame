using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Marks the point inside a streamed scene that should connect to the previous level's exit door
// The portal aligns this transform to its Exit Anchor after the scene loads additively
[DisallowMultipleComponent]
public sealed class SeamlessSceneEntryPoint : MonoBehaviour {
	[Header("Entry Identification")]
	[Tooltip("Unique ID used by a SeamlessScenePortal to find this entry point after the scene is loaded. Example: From_Level_1.")]
	[SerializeField] private string entryPointId = "Entry_A";

	[Header("Gizmos")]
	[Tooltip("Radius of the Scene view sphere drawn around this entry point.")]
	[SerializeField] private float gizmoRadius = 0.45f;
	[Tooltip("Length of the forward-direction arrow. Make sure the arrow points in the direction that the player will be travelling when entering this scene.")]
	[SerializeField] private float forwardArrowLength = 2.0f;
	[Tooltip("Colour used for this entry point in the Scene view.")]
	[SerializeField] private Color gizmoColor = new Color(0.1f, 0.85f, 1.0f, 1.0f);
	[Tooltip("If true, draws the entry point ID as a Scene view label.")]
	[SerializeField] private bool drawLabel = true;

	// Unique ID used by SeamlessSceneRoot.FindEntryPoint
	public string EntryPointId => entryPointId;

	private void OnValidate() {
		if (string.IsNullOrWhiteSpace(entryPointId)) {
			entryPointId = "Entry_A";
		}

		gizmoRadius = Mathf.Max(0.05f, gizmoRadius);
		forwardArrowLength = Mathf.Max(0.1f, forwardArrowLength);
	}

	private void OnDrawGizmos() {
		DrawGizmo(false);
	}

	private void OnDrawGizmosSelected() {
		DrawGizmo(true);
	}

	private void DrawGizmo(bool selected) {
		Color previousColor = Gizmos.color;
		Gizmos.color = selected ? Color.white : gizmoColor;

		Gizmos.DrawWireSphere(transform.position, gizmoRadius);
		Gizmos.DrawLine(transform.position, transform.position + transform.forward * forwardArrowLength);
		DrawArrowHead(transform.position + transform.forward * forwardArrowLength, transform.forward);

		Gizmos.color = previousColor;

#if UNITY_EDITOR
		if (drawLabel) {
			Handles.color = selected ? Color.white : gizmoColor;
			Handles.Label(transform.position + Vector3.up * (gizmoRadius + 0.25f), $"Entry: {entryPointId}");
		}
#endif
	}

	private void DrawArrowHead(Vector3 position, Vector3 direction) {
		Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
		Quaternion lookRotation = Quaternion.LookRotation(safeDirection, Vector3.up);

		Vector3 right = lookRotation * Quaternion.Euler(0.0f, 150.0f, 0.0f) * Vector3.forward;
		Vector3 left = lookRotation * Quaternion.Euler(0.0f, -150.0f, 0.0f) * Vector3.forward;

		Gizmos.DrawLine(position, position + right * 0.35f);
		Gizmos.DrawLine(position, position + left * 0.35f);
	}
}