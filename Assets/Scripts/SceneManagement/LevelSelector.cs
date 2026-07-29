using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelector : MonoBehaviour
{
    [SerializeField] private string f2Scene;
    [SerializeField] private string f3Scene;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            SceneManager.LoadScene(f2Scene);
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            SceneManager.LoadScene(f3Scene);
        }
    }
}