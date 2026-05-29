using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class MainMenuButtons : MonoBehaviour
{
    public UnityEvent CreditsEvent;
    public UnityEvent BackEvent;

	private void Awake() {
		DontDestroyOnLoadCleaner.DestroyAllDontDestroyOnLoadObjects();
	}

	private void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;
    }

    public void PlayButton()
    {
        LoadingManager.SceneToLoad = "Level_1";

        // Load the loading screen ADDITIVELY so it doesn't kill this script immediately
        SceneManager.LoadScene("LoadingScene");
    }

    public void CreditsButton()
    {
        CreditsEvent.Invoke();
    }

    public void ExitButton()
    {
        Application.Quit();
    }

    public void BackButton()
    {
        BackEvent.Invoke();
    }
}
