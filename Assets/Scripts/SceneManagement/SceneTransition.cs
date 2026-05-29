using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour {
	[Header("References")]
	[Tooltip("Optional. If left empty, this will be found from the Player Persistent scene.")]
	[SerializeField] private CanvasGroup fadeGroup;

	[Header("Fade Settings")]
	[Tooltip("How quickly the screen fades in/out.")]
	[SerializeField] private float fadeSpeed = 2.0f;

	private bool transitioning = false;

	private void Start() {
		FindFadeGroup();

		if (fadeGroup != null) {
			StartCoroutine(FadeIn());
		}
		else {
			Debug.LogWarning("SceneTransition could not find a fade group.");
		}
	}

	public void LoadScene(string sceneName) {
		if (transitioning == true) {
			return;
		}

		transitioning = true;
		StartCoroutine(FadeAndLoad(sceneName));
	}

	private void FindFadeGroup() {
		if (fadeGroup != null) {
			return;
		}

		if (PersistentFadeGroupLocator.Instance != null) {
			fadeGroup = PersistentFadeGroupLocator.Instance.FadeGroup;
		}
	}

	private IEnumerator FadeIn() {
		FindFadeGroup();

		if (fadeGroup == null) {
			yield break;
		}

		fadeGroup.blocksRaycasts = true;

		while (fadeGroup.alpha > 0f) {
			fadeGroup.alpha -= Time.deltaTime * fadeSpeed;
			fadeGroup.alpha = Mathf.Clamp01(fadeGroup.alpha);
			yield return null;
		}

		fadeGroup.blocksRaycasts = false;
		fadeGroup.interactable = false;
	}

	private IEnumerator FadeAndLoad(string sceneName) {
		FindFadeGroup();

		if (fadeGroup == null) {
			Debug.LogWarning("No fade group found. Loading scene without fade.");
			SceneManager.LoadScene(sceneName);
			yield break;
		}

		fadeGroup.blocksRaycasts = true;
		fadeGroup.interactable = true;

		while (fadeGroup.alpha < 1f) {
			fadeGroup.alpha += Time.deltaTime * fadeSpeed;
			fadeGroup.alpha = Mathf.Clamp01(fadeGroup.alpha);
			yield return null;
		}

		SceneManager.LoadScene(sceneName);
	}
}