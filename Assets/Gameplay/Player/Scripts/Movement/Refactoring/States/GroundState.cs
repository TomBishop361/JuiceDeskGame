using UnityEngine;

public class GroundState : MovementState
{
    public GroundState(PlayerController context) : base(context) { }

    public override void EnterState()
    {
        
    }

    public override void ExitState()
    {
        
    }

    public override void FixedUpdateState()
    {
        
    }

    public override void UpdateState()
    {
        
        // Any state inheriting from GroundedState gets standard jumping for free!
        if (context.JumpPressed && motor.IsGrounded)  {            
            context.UseJumpInput();
            motor.Jump(10f);
            // context.SwitchState(context.AirState);
        }
    }

}
