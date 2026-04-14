using UnityEngine;

public class GunInputManagerBase : MonoBehaviour
{
    IGunInputManager _inputManager;

    public IGunInputManager InputManager
    {
        get { return _inputManager ??= GetComponent<IGunInputManager>(); }
    }

}
