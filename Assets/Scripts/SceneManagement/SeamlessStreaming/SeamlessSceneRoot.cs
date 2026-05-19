using UnityEngine;

// Root marker for an additively loaded level scene
// Used by the airlock transition to find the scene's single entry point (where the player should appear)
[DisallowMultipleComponent]
public sealed class SeamlessSceneRoot : MonoBehaviour {
	[Header("Entry Point")]
	[Tooltip("The single point where the player appears when this scene is loaded through an airlock transition.")]
	[SerializeField] private SeamlessSceneEntryPoint entryPoint;

	// Returns the scene's single entry point
	// The entryPointId parameter is kept so AirlockSceneTransitionPortal can still call FindEntryPoint(targetEntryPointId).
	public SeamlessSceneEntryPoint GetEntryPoint() {
		if (entryPoint == null) {
			entryPoint = GetComponentInChildren<SeamlessSceneEntryPoint>(true);
		}

		if (entryPoint == null) {
			Debug.LogWarning($"{name}: No SeamlessSceneEntryPoint was assigned or found under this scene root.", this);
			return null;
		}

		return entryPoint;
	}
}