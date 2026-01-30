using System;
using UnityEngine;

public interface IInputManager
{
    Vector2 Movement { get;}
    Vector2 Look { get; }
    bool jump { get; }

    bool sprint { get; }
    bool crouch { get; }

    event Action<Vector2> OnMoveReceived;
    event Action<Vector2> OnLookReceived;
    event Action<bool> OnJumpReceived;
    event Action<bool> OnSprintReceived;
    event Action<bool> OnCrouchReceived;
}
