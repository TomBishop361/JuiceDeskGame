using System.Collections.Generic;
using UnityEngine;

// Add this component to every enemy prefab that should participate in Hunt Mode
// This script does not decide when Hunt Mode activates
// It provides HuntModeController with:
// - where this enemy is in the world
// - where the HUD marker should point
// - what bounds should be used for visibility checks
// - what visual components should be turned on/off for silhouette/outline reveals
[DisallowMultipleComponent]
public sealed class EnemyHuntTarget : MonoBehaviour {
	[Header("Target Points")]
	[Tooltip("World-space point used by camera visibility checks and directional marker aiming. Defaults to this transform.")]
	[SerializeField] private Transform targetTransform;
	[Tooltip("Offset from the target transform used for HUD marker placement (usually above the enemy body).")]
	[SerializeField] private Vector3 markerOffset = new Vector3(0.0f, 1.4f, 0.0f);

	[Header("Visibility Bounds")]
	[Tooltip("Renderers used to estimate whether the enemy is currently visible to the player camera. If empty, child renderers are found automatically.")]
	[SerializeField] private Renderer[] visibilityRenderers;
	[Tooltip("Fallback local-space bounds used used only if no renderers exist.")]
	[SerializeField] private Bounds fallbackLocalBounds = new Bounds(new Vector3(0.0f, 1.0f, 0.0f), new Vector3(1.0f, 2.0f, 1.0f));

	[Header("Reveal Visuals")]
	[Tooltip("If true,  this enemy searches its children for components implementing IHuntRevealVisual.")]
	[SerializeField] private bool autoFindRevealVisuals = true;
	[Tooltip("Optional manual list of reveal visual components. Usually this can be left empty.")]
	[SerializeField] private MonoBehaviour[] revealVisualBehaviours;

	// Cached reveal visual interfaces
	// These are the components that actually turn outlines/silhouettes on and off
	private readonly List<IHuntRevealVisual> revealVisuals = new List<IHuntRevealVisual>(4);

	// Cached EnemyCombat reference so we can tell whether this enemy has already died
	private EnemyCombat enemyCombat;

	// Stores the current reveal state
	// This lets SetRevealOpacity preserve whether the reveal is currently active
	private bool revealVisible;
	private float revealOpacity;

	// Main transform used by Hunt Mode
	// If no custom target is assigned, this enemy's own transform is used
	public Transform TargetTransform => targetTransform != null ? targetTransform : transform;

	// Position used for visibility checks and general target direction
	public Vector3 TargetPosition => TargetTransform.position;

	// Position used for HUD marker placement
	public Vector3 MarkerWorldPosition => TargetPosition + markerOffset;

	// True while the enemy object is active and has not reported death
	public bool IsAlive => isActiveAndEnabled && (enemyCombat == null || enemyCombat.HasReportedDeath == false);

	private void Reset() {
		targetTransform = transform;
		AutoCollectVisibilityRenderers();
	}

	private void Awake() {
		// If no target transform was assigned, use this GameObject
		if (targetTransform == null) {
			targetTransform = transform;
		}

		enemyCombat = GetComponent<EnemyCombat>();

		// If no visibility renderers were assigned manually, collect renderers from children
		if (visibilityRenderers == null || visibilityRenderers.Length == 0) {
			AutoCollectVisibilityRenderers();
		}

		// Find outline/silhouette components
		CacheRevealVisuals();

		// Make sure pooled enemies start hidden
		SetRevealVisible(false, 0.0f);
	}

	private void OnEnable() {
		// When an enemy is reused, we do not want it to keep an old reveal state
		SetRevealVisible(false, 0.0f);
	}

	private void OnDisable() {
		// Hide reveal visuals when the enemy is disabled / despawned
		SetRevealVisible(false, 0.0f);
	}

	public bool TryGetVisibilityBounds(out Bounds bounds) {
		bool hasBounds = false;

		// Start with a tiny bounds at the target position
		bounds = new Bounds(TargetPosition, Vector3.zero);

		// Prefer actual renderer bounds because they match the enemy's visible body
		if (visibilityRenderers != null) {
			for (int i = 0; i < visibilityRenderers.Length; i++) {
				Renderer targetRenderer = visibilityRenderers[i];

				// Ignore missing, disabled, or inactive renderers
				if (targetRenderer == null || targetRenderer.enabled == false || targetRenderer.gameObject.activeInHierarchy == false) {
					continue;
				}

				// First valid renderer becomes the starting bounds
				if (hasBounds == false) {
					bounds = targetRenderer.bounds;
					hasBounds = true;
				}
				else {
					// Additional renderers expand the total enemy bounds
					bounds.Encapsulate(targetRenderer.bounds);
				}
			}
		}

		// If renderer bounds were found then use them
		if (hasBounds) {
			return true;
		}

		// Fallback is only used if the enemy has no renderers
		bounds = TransformLocalBounds(fallbackLocalBounds);
		return bounds.size.sqrMagnitude > 0.0001f;
	}

	public void SetRevealVisible(bool visible, float opacity) {
		revealVisible = visible;
		revealOpacity = Mathf.Clamp01(opacity);

		// Apply the reveal state to every visual component
		for (int i = revealVisuals.Count - 1; i >= 0; i--) {
			IHuntRevealVisual visual = revealVisuals[i];

			// Remove destroyed/missing visuals
			if (visual == null) {
				revealVisuals.RemoveAt(i);
				continue;
			}

			visual.SetRevealVisible(revealVisible);
			visual.SetRevealOpacity(revealVisible ? revealOpacity : 0.0f);
		}
	}

	public void SetRevealOpacity(float opacity) {
		// Keep the current visible/hidden state, but update the opacity
		SetRevealVisible(revealVisible, opacity);
	}

	private void AutoCollectVisibilityRenderers() {
		// Only runs during setup
		visibilityRenderers = GetComponentsInChildren<Renderer>(true);
	}

	private void CacheRevealVisuals() {
		revealVisuals.Clear();

		// First add manually assigned reveal visual behaviours (Optional)
		// This is optional and the array will usually be empty
		if (revealVisualBehaviours != null) {
			for (int i = 0; i < revealVisualBehaviours.Length; i++) {
				if (revealVisualBehaviours[i] is IHuntRevealVisual visual && revealVisuals.Contains(visual) == false) {
					revealVisuals.Add(visual);
				}
			}
		}

		// If auto-find is disabled, only manually assigned visuals are used
		if (autoFindRevealVisuals == false) {
			return;
		}

		// Automatically find any child MonoBehaviour that implements IHuntRevealVisual
		// This runs once on Awake and not every frame
		MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
		for (int i = 0; i < behaviours.Length; i++) {
			if (behaviours[i] is IHuntRevealVisual visual && revealVisuals.Contains(visual) == false) {
				revealVisuals.Add(visual);
			}
		}
	}

	private Bounds TransformLocalBounds(Bounds localBounds) {
		// Converts fallback local-space bounds into world-space bounds
		// This is only needed if no renderer bounds exist
		Vector3 center = TargetTransform.TransformPoint(localBounds.center);
		Vector3 extents = localBounds.extents;

		Vector3 axisX = TargetTransform.TransformVector(extents.x, 0.0f, 0.0f);
		Vector3 axisY = TargetTransform.TransformVector(0.0f, extents.y, 0.0f);
		Vector3 axisZ = TargetTransform.TransformVector(0.0f, 0.0f, extents.z);

		Vector3 worldExtents = new Vector3(
			Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
			Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
			Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)
		);

		return new Bounds(center, worldExtents * 2.0f);
	}
}