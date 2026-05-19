using UnityEngine;

// Runtime locator for the airlock fade CanvasGroup
// Setup:
// - Place this in the persistent Player/UI scene so level scenes can find the fade overlay
[DisallowMultipleComponent]
public sealed class AirlockFadeGroupLocator : MonoBehaviour {
	public static AirlockFadeGroupLocator Instance { get; private set; }

	[Header("Fade Reference")]
	[Tooltip("CanvasGroup used for airlock blackout / screen fade.")]
	[SerializeField] private CanvasGroup fadeGroup;

	[Header("Startup")]
	[Tooltip("If true, the fade starts invisible when this object wakes.")]
	[SerializeField] private bool startInvisible = true;

	public CanvasGroup FadeGroup => fadeGroup;

	private void Reset() {
		fadeGroup = GetComponent<CanvasGroup>();
	}

	private void Awake() {
		if (Instance != null && Instance != this) {
			Debug.LogWarning($"{name}: Duplicate AirlockFadeGroupLocator found. Destroying duplicate.", this);
			Destroy(gameObject);
			return;
		}

		Instance = this;

		if (fadeGroup == null) {
			fadeGroup = GetComponent<CanvasGroup>();
		}

		if (fadeGroup == null) {
			Debug.LogWarning($"{name}: No CanvasGroup assigned for airlock fading.", this);
			return;
		}

		if (startInvisible) {
			fadeGroup.alpha = 0f;
			fadeGroup.blocksRaycasts = false;
			fadeGroup.interactable = false;
		}
	}

	private void OnDestroy() {
		if (Instance == this) {
			Instance = null;
		}
	}
}