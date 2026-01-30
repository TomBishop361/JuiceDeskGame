using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : InputManagerBase, IInputManager
{
    public Vector2 Movement { get; private set; }
    public Vector2 Look { get; private set; }
    public bool jump { get; private set; }
    public bool sprint { get; private set; }
    public bool crouch{ get; private set; }

    public event Action<Vector2> OnMoveReceived = delegate (Vector2 vector2) { };
    public event Action<bool> OnJumpReceived = delegate (bool value ) { };
    public event Action<bool> OnSprintReceived = delegate (bool value ) { };
    public event Action<bool> OnCrouchReceived = delegate (bool value ) { };
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
        
        jump = (inputValue.Get<float>() == 1);
        OnJumpReceived(jump);
    }

    void OnSprint(InputValue inputvalue) {
        Debug.Log(inputvalue.Get<float>());
        sprint = (inputvalue.Get<float>() == 1);
        OnSprintReceived(sprint);
    }

    void OnCrouch(InputValue inputvalue)
    {
        crouch = (inputvalue.Get<float>() == 1);
        OnCrouchReceived(crouch);
    }
}
