//FROM: https://youtu.be/bt2NSujQ4yw?si=oQHU5t0MMTnjkpwO  comments from that link. & ChatGPT for setting Pause Menu animation to Unscaled Time.
//From, Arthur Wakeman

using UnityEngine;

public class PauseMenu : MonoBehaviour
{

    public GameObject MenuContainer;
    private void Start()
    {
        MenuContainer.SetActive(false);
        Time.timeScale = 1;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            Time.timeScale = 0;
            MenuContainer.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
           
        }
    }

    public void ResumeButton ()
    {
        Time.timeScale = 1;
        MenuContainer.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
    }



}
