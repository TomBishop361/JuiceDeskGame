using System;
using UnityEngine;

public interface IInputManager
{
    Vector2 Movement { get;}
    Vector2 Look { get; }
    bool jump { get; }
    event Action<Vector2> OnMoveReceived;
    event Action<Vector2> OnLookReceived;
    event Action<bool> OnJumpReceived;
}
