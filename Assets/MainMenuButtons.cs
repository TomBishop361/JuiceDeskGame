using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class MainMenuButtons : MonoBehaviour
{
    public UnityEvent CreditsEvent;

    public void PlayButton()
    {
        LoadingManager.SceneToLoad = "Baked_Level_1.5";

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
}
