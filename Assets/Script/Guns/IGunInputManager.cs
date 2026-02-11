using System;
using UnityEngine;

public interface IGunInputManager
{
    bool shoot { get; }
    bool reload { get; }

    event Action<bool> onShootReceived;
    event Action<bool> onReload;

}
