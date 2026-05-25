using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Shows a large centre-screen title after an Airlock scene transition
// To be placed in the Player persistent scene
// Current flow:
// 1. AirlockSceneTransitionPortal moves the player to the new scene
// 2. OnAnyPlayerHandoff fires
// 3. This script waits for the blackout fade to clear
// 4. The level title fades in, holds for a moment, then fades out with subtle motion
[DisallowMultipleComponent]
public sealed class LevelIntroTitleUI : MonoBehaviour {
	// Single Inspector entry for each scene title
	[Serializable]
	private sealed class SceneTitleEntry {
		[Tooltip("Exact Unity scene name.")]
		public string sceneName;

		[Tooltip("Text shown on screen.")]
		public string displayTitle;
	}

	[Header("UI References")]
	[Tooltip("CanvasGroup on the title root. This is used to fade the whole title in and out.")]
	[SerializeField] private CanvasGroup titleGroup;
	[Tooltip("RectTransform of the title root. Used for slight scale and movement animation.")]
	[SerializeField] private RectTransform titleRoot;
	[Tooltip("TextMeshPro text used for the level title.")]
	[SerializeField] private TMP_Text titleText;

	[Header("Scene Titles")]
	[Tooltip("Scene-name to display-title lookup.")]
	[SerializeField] private SceneTitleEntry[] sceneTitles;

	[Header("Timing")]
	[Tooltip("Seconds taken for the title to fade in.")]
	[SerializeField] private float fadeInDuration = 0.35f;
	[Tooltip("Seconds the title stays fully visible.")]
	[SerializeField] private float holdDuration = 1.5f;
	[Tooltip("Seconds taken for the title to fade out.")]
	[SerializeField] private float fadeOutDuration = 0.7f;
	[Tooltip("If true, waits for the Airlock blackout to have almost finished before showing the title.")]
	[SerializeField] private bool waitForAirlockFadeToClear = true;

	[Header("Motion")]
	[Tooltip("Scale used when the title first appears.")]
	[SerializeField] private float startScale = 0.94f;
	[Tooltip("Scale used while the title is fully visible.")]
	[SerializeField] private float visibleScale = 1.0f;
	[Tooltip("Scale reached as the title fades out.")]
	[SerializeField] private float endScale = 1.04f;
	[Tooltip("How many UI pixels the title moves upward while fading out.")]
	[SerializeField] private float fadeOutMoveUp = 45.0f;

	// Stores the currently running title animation so it can be stopped
	// if another airlock transition starts before the previous title finishes
	private Coroutine titleRoutine;

	// Once the Airlock blackout's alpha is this low, it's considered finished
	private const float FadeClearAlpha = 0.02f;

	// Safety timeout in case the fade group gets stuck
	private const float MaxFadeClearWait = 5.0f;

	private void Reset() {
		titleGroup = GetComponent<CanvasGroup>();
		titleRoot = GetComponent<RectTransform>();
		titleText = GetComponentInChildren<TMP_Text>(true);
	}

	private void Awake() {
		if (titleGroup == null) {
			titleGroup = GetComponent<CanvasGroup>();
		}

		if (titleRoot == null) {
			titleRoot = GetComponent<RectTransform>();
		}

		if (titleText == null) {
			titleText = GetComponentInChildren<TMP_Text>(true);
		}

		// Ensure the title starts hidden when the scene begins
		HideInstant();
	}

	private void OnEnable() {
		// Listen for the Airlock system handing the player over to the new scene
		AirlockSceneTransitionPortal.OnAnyPlayerHandoff += HandlePlayerHandoff;
	}

	private void OnDisable() {
		AirlockSceneTransitionPortal.OnAnyPlayerHandoff -= HandlePlayerHandoff;
	}

	private void HandlePlayerHandoff() {
		// Prevent multiple title animations from overlapping.
		if (titleRoutine != null) {
			StopCoroutine(titleRoutine);
		}

		titleRoutine = StartCoroutine(ShowTitleRoutine());
	}

	private IEnumerator ShowTitleRoutine() {
		if (titleGroup == null || titleText == null) {
			Debug.LogWarning($"{name}: LevelIntroTitleUI is missing Title Group or Title Text.", this);
			yield break;
		}

		// Wait until the fade has cleared before showing the level title since the Airlock event can fire
		// whilst the blackout is still visible
		if (waitForAirlockFadeToClear) {
			yield return WaitForAirlockFadeToClear();
		}

		// Use the active scene name to find the correct display title
		string sceneName = SceneManager.GetActiveScene().name;
		string title = GetDisplayTitle(sceneName);

		titleText.text = title;

		// The title starts in the centre, then moves slightly upward as it fades away
		Vector2 centrePosition = Vector2.zero;
		Vector2 endPosition = centrePosition + Vector2.up * fadeOutMoveUp;

		// Fade in and gently scale up from the starting size to the visible size
		yield return AnimateTitle(
			0.0f,
			1.0f,
			startScale,
			visibleScale,
			centrePosition,
			centrePosition,
			fadeInDuration
		);

		// Keep the title readable for a short moment
		if (holdDuration > 0.0f) {
			yield return WaitUnscaled(holdDuration);
		}

		// Fade out while moving slightly upward and scaling a little larger
		yield return AnimateTitle(
			1.0f,
			0.0f,
			visibleScale,
			endScale,
			centrePosition,
			endPosition,
			fadeOutDuration
		);

		// Reset to hidden state so the UI is clean for the next transition
		HideInstant();
		titleRoutine = null;
	}

	private IEnumerator WaitForAirlockFadeToClear() {
		CanvasGroup airlockFadeGroup = GetAirlockFadeGroup();

		// If there is no fade group, do not block the title forever
		if (airlockFadeGroup == null) {
			yield break;
		}

		float elapsed = 0.0f;

		// Wait until the blackout is almost invisible, or until the safety timeout is reached
		while (airlockFadeGroup.alpha > FadeClearAlpha && elapsed < MaxFadeClearWait) {
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}
	}

	private CanvasGroup GetAirlockFadeGroup() {
		if (AirlockFadeGroupLocator.Instance != null) {
			return AirlockFadeGroupLocator.Instance.FadeGroup;
		}

		// Fallback search in case the locator instance was not ready yet
		AirlockFadeGroupLocator locator = FindFirstObjectByType<AirlockFadeGroupLocator>(FindObjectsInactive.Include);

		return locator != null ? locator.FadeGroup : null;
	}

	private string GetDisplayTitle(string sceneName) {
		// Search the Inspector list for a matching scene name
		if (sceneTitles != null) {
			for (int i = 0; i < sceneTitles.Length; i++) {
				SceneTitleEntry entry = sceneTitles[i];

				if (entry == null) {
					continue;
				}

				// Ignore case
				if (string.Equals(entry.sceneName, sceneName, StringComparison.OrdinalIgnoreCase)) {
					if (string.IsNullOrWhiteSpace(entry.displayTitle) == false) {
						return entry.displayTitle;
					}
				}
			}
		}

		// Fallback so the UI still works even if a scene title entry isn't added
		return string.IsNullOrWhiteSpace(sceneName) ? "New Area" : sceneName;
	}

	private IEnumerator AnimateTitle(float fromAlpha, float toAlpha, float fromScale, float toScale, Vector2 fromPosition, Vector2 toPosition, float duration) {
		if (titleGroup == null || titleRoot == null) {
			yield break;
		}

		// Prevent blocking UI input
		titleGroup.gameObject.SetActive(true);
		titleGroup.blocksRaycasts = false;
		titleGroup.interactable = false;

		// If duration is zero, instantly jump to the final animation state
		if (duration <= 0.0f) {
			titleGroup.alpha = toAlpha;
			titleRoot.localScale = Vector3.one * toScale;
			titleRoot.anchoredPosition = toPosition;
			yield break;
		}

		float elapsed = 0.0f;

		// Animate using unscaled time so the title still works if gameplay time is slowed or paused
		while (elapsed < duration) {
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);

			titleGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
			titleRoot.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t);
			titleRoot.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, t);

			yield return null;
		}

		// Snap to exact final values to in case of floating point inaccuracies
		titleGroup.alpha = toAlpha;
		titleRoot.localScale = Vector3.one * toScale;
		titleRoot.anchoredPosition = toPosition;
	}

	private IEnumerator WaitUnscaled(float duration) {
		float elapsed = 0.0f;

		// Uses unscaled time so the hold duration is not affected by Time.timeScale
		while (elapsed < duration) {
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}
	}

	private void HideInstant() {
		// Hide the title and make sure it never blocks UI interaction
		if (titleGroup != null) {
			titleGroup.alpha = 0.0f;
			titleGroup.blocksRaycasts = false;
			titleGroup.interactable = false;
		}

		// Reset the motion state ready for the next airlock transition
		if (titleRoot != null) {
			titleRoot.localScale = Vector3.one * startScale;
			titleRoot.anchoredPosition = Vector2.zero;
		}
	}

	private void OnValidate() {
		fadeInDuration = Mathf.Max(0.0f, fadeInDuration);
		holdDuration = Mathf.Max(0.0f, holdDuration);
		fadeOutDuration = Mathf.Max(0.0f, fadeOutDuration);
	}

}