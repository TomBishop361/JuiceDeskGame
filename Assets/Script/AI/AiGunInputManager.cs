using System;
using UnityEngine;

public class AiGunInputManager : GunInputManagerBase, IGunInputManager
{
    //Gun Input
    public bool shoot { get; private set; }
    public bool reload { get; private set; }

    //Gun
    public event Action<bool> onShootReceived = delegate (bool value) { };
    public event Action<bool> onReload = delegate (bool value) { };


    public void AiShoot(bool val)
    {
        shoot = val;

        onShootReceived(val);
    }
}
