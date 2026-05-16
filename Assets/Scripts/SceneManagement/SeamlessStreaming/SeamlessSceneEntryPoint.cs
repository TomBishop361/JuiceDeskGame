using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Marks where the player should appear after an airlock scene transition
[DisallowMultipleComponent]
public sealed class SeamlessSceneEntryPoint : MonoBehaviour {
	[Header("Gizmos")]
	[Tooltip("Radius of the Scene view sphere drawn around this entry point.")]
	[SerializeField] private float gizmoRadius = 0.45f;
	[Tooltip("Length of the forward-direction arrow. The arrow should point in the direction the player faces after the transition.")]
	[SerializeField] private float forwardArrowLength = 2.0f;
	[Tooltip("Colour used for this entry point in the Scene view.")]
	[SerializeField] private Color gizmoColor = new Color(0.1f, 0.85f, 1.0f, 1.0f);
	[Tooltip("If true, draws a Scene view label above the entry point.")]
	[SerializeField] private bool drawLabel = true;
	[Tooltip("Text shown above the entry point in the Scene view.")]
	[SerializeField] private string labelText = "Scene Entry Point";

	private void OnValidate() {
		gizmoRadius = Mathf.Max(0.05f, gizmoRadius);
		forwardArrowLength = Mathf.Max(0.1f, forwardArrowLength);

		if (string.IsNullOrWhiteSpace(labelText)) {
			labelText = "Scene Entry Point";
		}
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
			Handles.Label(transform.position + Vector3.up * (gizmoRadius + 0.25f), labelText);
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