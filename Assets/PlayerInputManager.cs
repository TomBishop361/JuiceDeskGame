using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : InputManagerBase, IInputManager
{
    public Vector2 Movement { get; private set; }
    public Vector2 Look { get; private set; }
    public bool jump { get; private set; }

    public event Action<Vector2> OnMoveReceived = delegate (Vector2 vector2) { };
    public event Action<bool> OnJumpReceived = delegate (bool value ) { };
    public event Action<Vector2> OnLookReceived = delegate (Vector2 vector2) { };
    
    void OnLook(InputValue inputValue)
    {
        Look = inputValue.Get<Vector2>();
        OnLookReceived(Look);
    }

    void OnMove(InputValue inputValue)
    {
        Movement = inputValue.Get<Vector2>();        
        OnMoveReceived(Movement);
    }
    

    void OnJump(InputValue inputValue)
    {
        Debug.Log(inputValue.Get<float>());
        jump = (inputValue.Get<float>() == 1);
        OnJumpReceived(jump);
    }
}
