using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : InputManagerBase, IInputManager
{
    //Movement Input
    public Vector2 Movement { get; private set; }
    public Vector2 Look { get; private set; }
    public bool jump { get; private set; }
    public bool sprint { get; private set; }
    public float crouch{ get; private set; }
    public float slide{ get; private set; }

    public bool grapple { get; private set; }


    //Movement
    public event Action<Vector2> OnMoveReceived = delegate (Vector2 vector2) { };
    public event Action<bool> OnJumpReceived = delegate (bool value ) { };
    public event Action<bool> OnSprintReceived = delegate (bool value ) { };
    public event Action<float> OnCrouchReceived = delegate (float value ) { };
    public event Action<float> OnSlideReceived = delegate (float value ) { };
    public event Action<Vector2> OnLookReceived = delegate (Vector2 vector2) { };
    public event Action<bool> OnGrappleReceived = delegate (bool value) { };



    //Movement
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
        crouch = inputvalue.Get<float>();
        OnCrouchReceived(crouch);
    }

    void OnSlide(InputValue inputvalue)
    {
        slide = inputvalue.Get<float>();
        OnSlideReceived(slide);
    }

   void OnGrapple(InputValue inputvalue)
    {
        grapple = (inputvalue.Get<float>() == 1);
        OnGrappleReceived(grapple);
    }
}
