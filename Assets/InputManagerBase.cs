using UnityEngine;

//This Class Exists so i can expose the interface in the unity inspector
public class InputManagerBase : MonoBehaviour
{
    IInputManager _inputManager;

    public IInputManager InputManager
    {
        get { return _inputManager ??= GetComponent<IInputManager>(); }
    }
}
