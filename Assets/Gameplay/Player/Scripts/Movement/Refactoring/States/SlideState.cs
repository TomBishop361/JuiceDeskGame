using UnityEngine;

public class SlideState : GroundedState
{
    private float maxSlideTime = 0.75f;
    private float slideForce = 35f;
    private float slideDownforce = 2f;
    private float slideYScale = 0.5f;
    private float slideNoiseInterval = 0.35f;

    private float startYScale;
    private int slideTimerID;
    private float nextSlideNoiseTime;
    private bool timerInitialized;

    

    public SlideState(PlayerController context) : base(context)
    {
        if(motor.Collider != null)
        {
            startYScale = motor.Collider.height;
        }
    }

    public override void EnterState()
    {
        //Timer
        if (!timerInitialized && TimerManager.instance != null)
        {
            slideTimerID = TimerManager.instance.NewTimer(maxSlideTime, SlideTimerComplete, "Slide Timer");
            timerInitialized = true;
        }

        motor.SetCapsuleSize(slideYScale, Vector3.up * -0.35f);

        motor.rb.AddForce(Vector3.down, ForceMode.Impulse);
        TimerManager.instance?.RestartTimer(slideTimerID);

    }

    public override void ExitState()
    {
        motor.SetCapsuleSize(startYScale, Vector3.up * 0.15f);
    }

    public override void FixedUpdateState()
    {
        Vector2 moveInput = input.Movement;
        Vector3 flatOrientationForward = new Vector3(motor.Camera.transform.forward.x, 0, motor.Camera.transform.forward.z).normalized;
        Vector3 slideDirection = flatOrientationForward * moveInput.y + motor.Camera.transform.right * moveInput.x;

        if (slideDirection == Vector3.zero) slideDirection = motor.transform.forward;

        if (motor.OnSlope() || motor.rb.linearVelocity.y > -0.1f)
        {
            motor.rb.AddForce(slideDirection.normalized * slideForce, ForceMode.Force);
        }
        else
        {
            motor.rb.AddForce(motor.GetSlopeMoveDirection(slideDirection) * slideForce, ForceMode.Force);
            TimerManager.instance.RestartTimer(slideTimerID);
        }

        motor.rb.AddForce(Vector3.down * slideDownforce, ForceMode.Force);

        if (Time.time >= nextSlideNoiseTime)
        {
            //noiseEmitter?.EmitSlideNoise(0.65f);
            nextSlideNoiseTime = Time.time + slideNoiseInterval;
        }
    }

    public override void UpdateState()
    {
        base.UpdateState();
        if (input.slide == 0) // Key released
        {
            context.SwitchState(context.walkState);
            return;
        }
    }

    private void SlideTimerComplete()
    {
        // Safety: Only force switch to walk state if the player is still actively sliding when the timer rings
        //if (context.CurrentState == this)
        //{
        //    context.SwitchState(context.WalkState);
        //}
    }
}
