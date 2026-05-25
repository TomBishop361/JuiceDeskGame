using UnityEngine;

public abstract class MovementState
{
    protected PlayerController context;
    protected PlayerMotor motor;
    protected IInputManager input;

    public MovementState(PlayerController context)
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