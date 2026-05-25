using UnityEngine;
using UnityEngine.SceneManagement;


public class PlayerLoader : MonoBehaviour
{
    public Transform PlayerSpawnPos;

    private void Reset()
    {
        PlayerSpawnPos = new GameObject("PlayerSpawnPos").transform;
        PlayerSpawnPos.parent = transform;
    }

    void Awake()
    {
        // Check if the scene is already loaded to avoid duplicates
        if (!SceneManager.GetSceneByName("Player").isLoaded)
        {
            SceneManager.LoadScene("Player", LoadSceneMode.Additive);
        }
    }
}
