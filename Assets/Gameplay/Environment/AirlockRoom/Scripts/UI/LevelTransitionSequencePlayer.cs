using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Plays a fullscreen PNG/sprite sequence during the airlock blackout.
// Place this on a persistent UI object, usually in the Player scene, so it survives level unloads.
[DisallowMultipleComponent]
public sealed class LevelTransitionSequencePlayer : MonoBehaviour {
	[Header("UI References")]
	[Tooltip("CanvasGroup on the fullscreen transition animation root. Used to show/hide the sequence cleanly without disabling the whole canvas.")]
	[SerializeField] private CanvasGroup sequenceGroup;
	[Tooltip("Fullscreen UI Image that displays each sprite frame in the PNG sequence.")]
	[SerializeField] private Image frameImage;

	[Header("Frames")]
	[Tooltip("Ordered sprite frames for the transition animation. Drag Intro_00000 through Intro_00032 here in filename order.")]
	[SerializeField] private Sprite[] frames;
	[Tooltip("How many sprite frames are shown per second. 24 is usually a good cinematic value for exported PNG sequences.")]
	[SerializeField] private float framesPerSecond = 24.0f;

	[Header("Playback")]
	[Tooltip("If true, playback uses unscaled time so the animation still plays correctly if gameplay time is paused or slowed during transitions.")]
	[SerializeField] private bool useUnscaledTime = true;
	[Tooltip("If true, the transition animation is hidden automatically after the final frame.")]
	[SerializeField] private bool hideWhenFinished = true;
	[Tooltip("If true, the Image sprite is cleared when hidden so the final frame cannot flash later.")]
	[SerializeField] private bool clearSpriteWhenHidden = false;

	[Header("Startup")]
	[Tooltip("If true, the sequence starts hidden when this object wakes. Keep this enabled for normal airlock use.")]
	[SerializeField] private bool startHidden = true;

	[Header("Debug")]
	[Tooltip("If true, missing references and playback issues are logged to the Unity Console.")]
	[SerializeField] private bool debugLogging = false;

	public bool IsPlaying => isPlaying;

	// The portal checks this before yielding the animation routine.
	public bool CanPlay => sequenceGroup != null && frameImage != null && frames != null && frames.Length > 0;

	private bool isPlaying;

	private void Reset() {
		ResolveReferences();
	}

	private void Awake() {
		ResolveReferences();

		if (startHidden) {
			HideInstant();
		}
	}

	// Plays the sprite sequence from the first frame to the last frame.
	// AirlockSceneTransitionPortal yields this coroutine so fade-in waits until playback has finished.
	public IEnumerator PlayRoutine() {
		ResolveReferences();

		if (CanPlay == false) {
			LogWarning("Cannot play level transition sequence because the CanvasGroup, Image, or Frames list is missing.");
			HideInstant();
			yield break;
		}

		isPlaying = true;
		ShowInstant();

		float safeFramesPerSecond = Mathf.Max(1.0f, framesPerSecond);
		float secondsPerFrame = 1.0f / safeFramesPerSecond;

		for (int i = 0; i < frames.Length; i++) {
			// Null checks allow one missing frame without breaking the whole transition.
			if (frames[i] != null) {
				frameImage.sprite = frames[i];
			}

			yield return WaitForFrameDuration(secondsPerFrame);
		}

		if (hideWhenFinished) {
			HideInstant();
		}

		isPlaying = false;
	}

	// Immediately shows the transition UI above the black fade panel.
	public void ShowInstant() {
		if (sequenceGroup != null) {
			sequenceGroup.gameObject.SetActive(true);
			sequenceGroup.alpha = 1.0f;

			// This overlay is visual only; it should never block player/UI input by itself.
			sequenceGroup.blocksRaycasts = false;
			sequenceGroup.interactable = false;
		}

		if (frameImage != null) {
			frameImage.enabled = true;
		}
	}

	// Immediately hides the transition UI and optionally clears the last displayed sprite.
	public void HideInstant() {
		if (sequenceGroup != null) {
			sequenceGroup.alpha = 0.0f;
			sequenceGroup.blocksRaycasts = false;
			sequenceGroup.interactable = false;
		}

		if (frameImage != null) {
			frameImage.enabled = false;

			if (clearSpriteWhenHidden) {
				frameImage.sprite = null;
			}
		}
	}

	private IEnumerator WaitForFrameDuration(float duration) {
		float elapsed = 0.0f;

		while (elapsed < duration) {
			elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
			yield return null;
		}
	}

	private void ResolveReferences() {
		if (sequenceGroup == null) {
			sequenceGroup = GetComponent<CanvasGroup>();
		}

		if (frameImage == null) {
			frameImage = GetComponentInChildren<Image>(true);
		}
	}

	private void OnValidate() {
		framesPerSecond = Mathf.Max(1.0f, framesPerSecond);
	}

	private void LogWarning(string message) {
		if (debugLogging) {
			Debug.LogWarning($"{name}: {message}", this);
		}
	}
}
