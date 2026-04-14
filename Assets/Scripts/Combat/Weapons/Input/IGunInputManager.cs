using System;
using UnityEngine;

public interface IGunInputManager
{
    bool shoot { get; }
    bool reload { get; }

    bool secondfire { get; }    

    event Action<bool> onShootReceived;
    event Action<bool> onReload;
    event Action<bool> onSecondFire;

}
