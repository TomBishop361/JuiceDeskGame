using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseButtons : MonoBehaviour
{
   public void BackToMenuButton()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
