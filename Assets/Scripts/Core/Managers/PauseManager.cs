using UnityEngine;

public class PauseManager : MonoBehaviour
{
    static public bool IsPaused { get; private set; } = false;
    [SerializeField] InputManagerBase _inputManager;
    [SerializeField] GameObject PausePanel;
    [SerializeField] GameObject HUDPanel;
    public IInputManager InputManager => _inputManager.InputManager;

    private void OnEnable()
    {
        IsPaused = false;
        InputManager.OnPauseReceived += PausePressed;
    }

    private void PausePressed(bool value)
    {
        IsPaused = (value == IsPaused) ? !value : value;        
        PausePanel.SetActive(IsPaused);
        HUDPanel.SetActive(!IsPaused);

        if (IsPaused)
        {
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
