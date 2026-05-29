using UnityEngine;

public sealed class PersistentFadeGroupLocator : MonoBehaviour {
	public static PersistentFadeGroupLocator Instance { get; private set; }

	[Header("References")]
	[Tooltip("CanvasGroup used for full-screen fade transitions.")]
	[SerializeField] private CanvasGroup fadeGroup;

	public CanvasGroup FadeGroup => fadeGroup;

	private void Awake() {
		if (Instance != null && Instance != this) {
			Destroy(gameObject);
			return;
		}

		Instance = this;

		if (fadeGroup == null) {
			fadeGroup = GetComponent<CanvasGroup>();
		}
	}
}