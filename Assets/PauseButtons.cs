using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseButtons : MonoBehaviour {
	[SerializeField] PauseManager pauseManager;
	[SerializeField] SceneTransition sceneTransition;

	private IEnumerator FadeAndLoad(string sceneName) {
		Cursor.visible = true;
		Cursor.lockState = CursorLockMode.None;
		SceneManager.LoadScene(sceneName);

		yield return null;
	}

	public void BackToMenuButton() {
		//pauseManager.Pause(false);
		Time.timeScale = 1;
		StartCoroutine(FadeAndLoad("MainMenu"));
		//sceneTransition.LoadScene("MainMenu");
		//SceneManager.LoadScene("MainMenu");
	}
}