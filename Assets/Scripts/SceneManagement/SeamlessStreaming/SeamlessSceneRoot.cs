using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Root component for a scene that can be streamed through a seamless portal
// Typical Setup:
// - Place this on the single parent object that contains the streamed level content
// - The portal moves this root so one of its child entry points lines up with the previous scene's exit anchor
//
// NOTES:
// - Any floors, walls, props, lights, doors, spawners, checkpoints, and entry points that should move together must be parented under this root object
[DisallowMultipleComponent]
public sealed class SeamlessSceneRoot : MonoBehaviour {
	[Header("Gizmos")]
	[Tooltip("If true, draws an approximate bounds wireframe for child renderers/colliders while this root is selected.")]
	[SerializeField] private bool drawApproximateBounds = true;
	[Tooltip("Colour used for the streamed root bounds gizmo.")]
	[SerializeField] private Color boundsGizmoColor = new Color(0.1f, 0.85f, 1.0f, 0.35f);

	// Reused list to avoid allocating a new List every time FindEntryPoint is called
	private readonly List<SeamlessSceneEntryPoint> entryPointBuffer = new List<SeamlessSceneEntryPoint>(8);

	// Scene that owns this streamed root
	// Used mainly for debug warnings and labels
	public Scene OwningScene => gameObject.scene;

	// Finds a child entry point by ID
	// Multiple entry points allow one scene to connect to different previous levels/doors (we can use this for Level1.5 <-> Level2)
	public SeamlessSceneEntryPoint FindEntryPoint(string entryPointId) {
		if (string.IsNullOrWhiteSpace(entryPointId)) {
			Debug.LogWarning($"{name}: Cannot find entry point because the ID is empty.", this);
			return null;
		}

		GetEntryPoints(entryPointBuffer);

		SeamlessSceneEntryPoint foundEntry = null;
		int matches = 0;

		for (int i = 0; i < entryPointBuffer.Count; i++) {
			SeamlessSceneEntryPoint entryPoint = entryPointBuffer[i];

			if (entryPoint == null) {
				continue;
			}

			if (entryPoint.EntryPointId == entryPointId) {
				foundEntry = entryPoint;
				matches++;
			}
		}

		// Possible setup issue: only use the first matching entry point
		if (matches > 1) {
			Debug.LogWarning($"{name}: Found {matches} entry points with ID '{entryPointId}'. The first match will be used, but IDs should be unique.", this);
		}

		return foundEntry;
	}

	// Fills the supplied list with every SeamlessSceneEntryPoint under this root
	// Includes inactive children so entry points can still be found if objects are temporarily disabled while editing
	public void GetEntryPoints(List<SeamlessSceneEntryPoint> results) {
		if (results == null) {
			return;
		}

		results.Clear();

		// The List overload fills the existing list instead of returning a new array
		GetComponentsInChildren(true, results);
	}

	// Moves and rotates this entire streamed scene root so the chosen entry point matches the current scene's exit anchor
	// Makes two separate scenes appear connected
	public bool AlignEntryPointToExit(string entryPointId, Transform exitAnchor) {
		if (exitAnchor == null) {
			Debug.LogWarning($"{name}: Cannot align streamed scene because the exit anchor is missing.", this);
			return false;
		}

		SeamlessSceneEntryPoint entryPoint = FindEntryPoint(entryPointId);
		if (entryPoint == null) {
			Debug.LogWarning($"{name}: Could not find entry point ID '{entryPointId}' in scene '{OwningScene.name}'.", this);
			return false;
		}

		// Step 1:
		// Rotate the streamed scene root so the selected entry point faces the same direction as the exit anchor
		// This handles cases where the next level was created facing a different direction in its own scene
		Quaternion rotationDelta = exitAnchor.rotation * Quaternion.Inverse(entryPoint.transform.rotation);
		transform.rotation = rotationDelta * transform.rotation;

		// Step 2:
		// After rotation, the entry point's world position may have changed
		// Move the root so the entry point sits exactly on the exit anchor
		Vector3 positionDelta = exitAnchor.position - entryPoint.transform.position;
		transform.position += positionDelta;

		return true;
	}

	// Calculates an approximate world-space bounds around this streamed scene root
	// This is only used for editor gizmos to quickly see whether the streamed content
	// is actually parented under this root
	private bool TryGetApproximateBounds(out Bounds bounds) {
		Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
		Collider[] colliders = GetComponentsInChildren<Collider>(true);

		bool hasBounds = false;
		bounds = new Bounds(transform.position, Vector3.zero);

		for (int i = 0; i < renderers.Length; i++) {
			if (renderers[i] == null) {
				continue;
			}

			if (hasBounds == false) {
				// First valid bounds becomes the starting bounds
				bounds = renderers[i].bounds;
				hasBounds = true;
			}
			else {
				// Expand the bounds so it includes this renderer too
				bounds.Encapsulate(renderers[i].bounds);
			}
		}

		for (int i = 0; i < colliders.Length; i++) {
			if (colliders[i] == null) {
				continue;
			}

			if (hasBounds == false) {
				// If there were no renderers, collider bounds can still provide a scene preview that is somewhat useful
				bounds = colliders[i].bounds;
				hasBounds = true;
			}
			else {
				bounds.Encapsulate(colliders[i].bounds);
			}
		}

		return hasBounds;
	}

	// Draws the approximate streamed scene bounds when the root is selected
	// Useful for debugging setup mistakes where objects such as floors or walls are not
	// parented under the SeamlessSceneRoot and therefore do not move during alignment
	private void OnDrawGizmosSelected() {
		if (drawApproximateBounds == false || TryGetApproximateBounds(out Bounds bounds) == false) {
			return;
		}

		Color previousColor = Gizmos.color;

		Gizmos.color = boundsGizmoColor;
		Gizmos.DrawWireCube(bounds.center, bounds.size);

		Gizmos.color = previousColor;

#if UNITY_EDITOR
		// Display labels (editor only)
		Handles.color = boundsGizmoColor;
		Handles.Label(bounds.center + Vector3.up * Mathf.Max(1.0f, bounds.extents.y), $"Seamless Root: {OwningScene.name}");
#endif
	}
}