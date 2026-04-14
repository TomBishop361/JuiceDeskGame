using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour {
	public CanvasGroup fadeGroup;
	public float fadeSpeed = 2.0f;
	private bool transitioning = false;

	private void Start() {
		StartCoroutine(FadeIn());
	}

	public void LoadScene(string sceneName) {
		if (transitioning == false) {
			transitioning = true;
			StartCoroutine(FadeAndLoad(sceneName));
		}
	}

	private IEnumerator FadeIn() {
		while (fadeGroup.alpha > 0) {
			fadeGroup.alpha -= Time.deltaTime * fadeSpeed;
			yield return null;
		}
	}

	private IEnumerator FadeAndLoad(string sceneName) {
		while (fadeGroup.alpha < 1) {
			fadeGroup.alpha += Time.deltaTime * fadeSpeed;
			yield return null;
		}

		SceneManager.LoadScene(sceneName);
	}
}