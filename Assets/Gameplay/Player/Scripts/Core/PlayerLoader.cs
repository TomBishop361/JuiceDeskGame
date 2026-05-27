using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLoader : MonoBehaviour
{
    public Transform PlayerSpawnPos;

    // Static flag that the loading screen can check
    public static bool IsPlayerLoadingComplete { get; private set; } = true;

    private void Reset()
    {
        PlayerSpawnPos = new GameObject("PlayerSpawnPos").transform;
        PlayerSpawnPos.parent = transform;
    }

    void Awake()
    {
        // If it's already loaded, we are immediately done
        if (SceneManager.GetSceneByName("Player").isLoaded)
        {
            IsPlayerLoadingComplete = true;
        }
        else
        {
            // Mark as NOT complete because we are about to start loading
            IsPlayerLoadingComplete = false;
            StartCoroutine(LoadAsync("Player"));
        }
    }

    IEnumerator LoadAsync(string scene)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);

        // Wait until the additive player scene is fully loaded
        while (!op.isDone)
        {
            yield return null;
        }

        // Mark as complete now that the scene is fully in memory
        IsPlayerLoadingComplete = true;
    }
}