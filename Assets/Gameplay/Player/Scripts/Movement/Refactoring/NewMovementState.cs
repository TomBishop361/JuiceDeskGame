using UnityEngine;

public abstract class NewMovementState
{
    protected PlayerController context;
    protected PlayerMotor motor;
    protected IInputManager input;

    public NewMovementState(PlayerController context)
    {
        this.context = context;
        this.motor = context.Motor;
        this.input = context.InputManager;
    }

    public abstract void EnterState();
    public abstract void UpdateState();
    public abstract void FixedUpdateState();
    public abstract void ExitState();
}