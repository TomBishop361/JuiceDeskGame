using UnityEngine;

public class WalkState : GroundedState
{
    [SerializeField] float walkSpeed = 7;
    [SerializeField] float acceleration = 50;
    private float groundFriction = 9f;

    public WalkState(PlayerController context) : base(context) { }

    public override void EnterState()
    {
        
    }

    public override void ExitState()
    {
        
    }

    public override void FixedUpdateState()
    {
        Vector3 inputDir = input.Movement;
        Vector3 cameraFlatForward = new Vector3(motor.Camera.transform.forward.x, 0, motor.Camera.transform.forward.z);
        Vector3 desiredvelocity = (cameraFlatForward * inputDir.y + motor.Camera.transform.right * inputDir.x).normalized * walkSpeed;

        motor.Move(desiredvelocity,walkSpeed, acceleration, groundFriction);
    }

    public override void UpdateState()
    {
        base.UpdateState();
        if (input.sprint && input.Movement != Vector2.zero)
        {
            context.SwitchState(context.SprintState);
        }
        if (input.slide == 1 && input.Movement != Vector2.zero && motor.IsGrounded)
        {
            context.SwitchState(context.slideState);
            return;
        }
    }
}
