using UnityEngine;

public class SprintState : GroundedState
{
    [SerializeField] private float sprintSpeed = 14f;
    [SerializeField] private float acceleration = 50f;
    private float groundFriction = 9f;

    public SprintState(PlayerController context) : base(context) { }

    public override void EnterState()
    {
        // Optional: Trigger a sprinting animation or change camera Field of View here
    }

    public override void UpdateState()
    {
        base.UpdateState();
        
        if (!input.sprint || input.Movement == Vector2.zero)
        {
            context.SwitchState(context.walkState);
            return;
        }
        if (input.slide == 1 && input.Movement != Vector2.zero && motor.IsGrounded)
        {
            context.SwitchState(context.slideState);
            return;
        }


    }

    public override void FixedUpdateState()
    {
        Vector2 inputDir = input.Movement;

        Vector3 cameraFlatForward = new Vector3(motor.Camera.transform.forward.x, 0, motor.Camera.transform.forward.z);
        Vector3 desiredVelocity = (cameraFlatForward * inputDir.y + motor.Camera.transform.right * inputDir.x).normalized * sprintSpeed;

        motor.Move(desiredVelocity,sprintSpeed, acceleration, groundFriction);
    }

    public override void ExitState()
    {
        // Optional: Reset camera FOV
    }
}
