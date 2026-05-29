using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingManager : MonoBehaviour
{
    public static string SceneToLoad;

    [Header("UI Elements")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private GameObject loadingCanvas; 

    void Start()
    {
        if (string.IsNullOrEmpty(SceneToLoad))
        {
            Debug.LogError("No scene was specified to load!");
            return;
        }        
        DontDestroyOnLoad(gameObject);
        if (loadingCanvas != null) DontDestroyOnLoad(loadingCanvas);

        StartCoroutine(LoadSceneAsync(SceneToLoad));
    }

    IEnumerator LoadSceneAsync(string sceneName)
    {
        yield return new WaitForSecondsRealtime(0.3f);
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName,LoadSceneMode.Additive);

        
        operation.allowSceneActivation = true;

        // 1. Load Environment Assets (0% to 50%)
        while (operation.progress < 0.9f)
        {
            float progress = (operation.progress / 0.9f) * 0.5f;
            if (progressBar != null) progressBar.value = progress;
            if (progressText != null) progressText.text = $"Loading Environment... {Mathf.RoundToInt(progress * 100)}%";
            yield return null;
        }

        
        while (!operation.isDone)
        {
            if (progressBar != null) progressBar.value = 0.5f;
            if (progressText != null) progressText.text = "Initializing Scene... 50%";
            yield return null;
        }
        
        
        if (progressText != null) progressText.text = "Loading Player... 80%";

        float playerProgress = 0.5f;
        while (!PlayerLoader.IsPlayerLoadingComplete)
        {
            
            playerProgress = Mathf.MoveTowards(playerProgress, 0.95f, Time.deltaTime * 0.2f);
            if (progressBar != null) progressBar.value = playerProgress;
            if (progressText != null) progressText.text = $"Loading Player... {Mathf.RoundToInt(playerProgress * 100)}%";
            yield return null;
        }

        // 4. Snap to 100% and Clean Up
        if (progressBar != null) progressBar.value = 1f;
        if (progressText != null) progressText.text = "Ready! 100%";
        yield return new WaitForSeconds(0.3f); // Brief pause so the player sees 100%

        SceneManager.UnloadSceneAsync("LoadingScene");

        Destroy(gameObject);
    }
}