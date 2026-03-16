using System;
using UnityEngine.InputSystem;

public class GunInputManager : GunInputManagerBase, IGunInputManager
{

    //Gun Input
    public bool shoot { get; private set; }
    public bool reload { get; private set; }

    public bool secondfire { get; private set; }

    //Gun
    public event Action<bool> onShootReceived = delegate (bool value) { };
    public event Action<bool> onReload = delegate (bool value) { };
    public event Action<bool> onSecondFire = delegate (bool value) { };


    //Gun 
    void OnShoot(InputValue inputValue)
    {

        shoot = (inputValue.Get<float>() == 1);
        onShootReceived(shoot);
    }

    void OnReload(InputValue inputValue)
    {
        reload = ((inputValue.Get<float>() == 1));
        onReload(reload);

    }

    void OnSecondFire(InputValue inputValue)
    {
        secondfire = (inputValue.Get<float>() == 1);
        onSecondFire(secondfire);    
    }
}
