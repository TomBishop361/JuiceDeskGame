using UnityEngine;

public class GunInputBase : MonoBehaviour
{
    IGunInputManager _inputManager;

    public IGunInputManager InputManager
    {
        get { return _inputManager ??= GetComponent<IGunInputManager>(); }
    }
}
