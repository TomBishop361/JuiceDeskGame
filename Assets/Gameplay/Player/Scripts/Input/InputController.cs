using Game.AI;
using System;
using System.Collections;
using TMPro;
using UnityEngine;



[RequireComponent((typeof(Rigidbody)))]
public class InputController : MonoBehaviour
{

    [Header("Input")]
    [SerializeField] InputManagerBase _inputManager;
    //[SerializeField] Camera _camera;
    [SerializeField] GameObject _camera;


    [Header("Movement Values")]
    float moveSpeed = 7;
    public float Velocity { get; private set; }
    [SerializeField] float walkSpeed = 7;
    [SerializeField] public float sprintSpeed = 14;
    [SerializeField] float wallRunSpeed = 7;
    [SerializeField] float slideSpeed = 30;

    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    [Header("Slope Slide Multiplier")]
    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;

    [Header("Crouching")]
    [SerializeField] float crouchSpeed = 4;
    [SerializeField] float crouchYscale = 0.5f; // This will likely be removed when animations are made
    [SerializeField] float startScaleYscale = 1;

    [Header("Slope Handling")]
    [SerializeField] public float maxSlopeAngle;
    [SerializeField] float Slopeforce;
    private RaycastHit slopeHit;

    [Header("Grappling Feel")]
    [SerializeField] float grappleSpeedBoost = 1.5f; // Multiplier for speed after grapple
    [SerializeField] float airControlDuringGrapple = 0.5f; // How much you can steer mid-air
    [SerializeField] float grapplePullForce = 20f; // Continuous pull toward target
    private Vector3 grappleTargetPos;
    [SerializeField] float AnchorLaunchAmount;

    [Header("Misc")]
    [SerializeField] Animator animator;
    [Tooltip("For instant movement set to 'Infinity'")]
    [SerializeField] float acceleration = 50;
    [SerializeField] float airAcceleration = 25;
    [SerializeField] float groundFriction = 0.4f;
    [SerializeField] LayerMask Ground;
    [SerializeField] float CharacterHeight = 2;
    [Range(0, 1)]
    [SerializeField] float CoyoteTime = 0.1f;
    [Range(0, 1)]
    [SerializeField] float Sensitivity = 0.15f;
    [SerializeField] float jumpForce = 10;
    [SerializeField] float jumpCoolDown = 0.3f;
    [SerializeField] bool isGrounded;
    [SerializeField] float inputLagPeriod = 0.0001f;
    


    public const float gravity = -9.81f;
    public bool jump;

    private bool sprint;
    bool crouching;
    public bool sliding;
    public bool wallRunning;
    public bool IsJumpReady;
    public bool isOnCoolDown;
    public bool freeze;
    public bool WallRunRight;
    public bool WallRunLeft;
    public bool isRailGrinding;
    public bool activeGrapple;
    bool exitingSlope;
    public bool enableMoveOnNextTouch; 

    [SerializeField] GameObject IKGunTarget;
    [SerializeField] GameObject PlayerRoot;


    private Vector2 lastInputEvent;
    private float InputLagTimer;

    float yRotation;
    float xRotation;

    //Data From InputManager
    Vector2 MoveDirection;
    Vector2 LookDirection;

    Coroutine speedLerpCoroutine;


    public IInputManager InputManager => _inputManager.InputManager;
    [SerializeField] private Rigidbody rb;
    public Rigidbody _rb => rb;

    public event Action JumpEvent = delegate { };

    public event Action OnStateChange = delegate { };

    public MovementState state;

    public enum MovementState
    {
        freeze,
        railGrinding,
        walking,
        sprinting,
        wallRunning,
        crouching,
        sliding,
        idle,
        air
    }       

    private void OnEnable()
    {
        InputManager.OnJumpReceived += JumpPressed;

        InputManager.OnSprintReceived += SprintPressed;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        startScaleYscale = transform.localScale.y;

    }
    Coroutine coyoteCoroutine;

    public bool _isGrounded
    {
        get => isGrounded;
        private set
        {
            if (value == false)
            {
                StartCoroutine(coyoteTime(value));
            }
            //True
            else
            {
                isGrounded = value;
                if (!IsJumpReady && !isOnCoolDown)
                {
                    isOnCoolDown = true;
                    StartCoroutine("JumpCoolDown");
                }
            }
        }
    }

    IEnumerator coyoteTime(bool value)
    {
        Debug.Log("CoyoteTimer");
        yield return new WaitForSeconds(CoyoteTime);
        isGrounded = value;
    }

    IEnumerator JumpCoolDown()
    {
        Debug.Log("JumpReady");
        yield return new WaitForSeconds(jumpCoolDown);
        IsJumpReady = true;
        isOnCoolDown = false;
    }

    //Movement fsm
    private void StateHandler()
    {

        if (isRailGrinding)
        {
            state = MovementState.railGrinding;
            desiredMoveSpeed = 0;
            rb.useGravity = false;
        }
        else if (freeze)
        {
            state = MovementState.freeze;
            desiredMoveSpeed = 0;
            rb.linearVelocity = Vector3.zero;
        }
        else if (wallRunning) // Movement needs to change to be body relative instead of camera
        {
            state = MovementState.wallRunning;
            desiredMoveSpeed = wallRunSpeed;

        }
        else if (sliding)
        {
            state = MovementState.sliding;
            if (OnSlope() && rb.linearVelocity.y < 0.1f)
            {
                desiredMoveSpeed = slideSpeed;
            }

            else desiredMoveSpeed = sprintSpeed;

        }
        else if (crouching)
        {
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }

        else if (_isGrounded && sprint)
        {
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed;
        }
        else if (_isGrounded && MoveDirection != Vector2.zero)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }
        else if (_isGrounded && MoveDirection == Vector2.zero)
        {
            state = MovementState.idle;
            desiredMoveSpeed = walkSpeed;
        }
        else
        {
            state = MovementState.air;

        }

        if (Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 7f && moveSpeed != 0)
        {
            if (speedLerpCoroutine != null)
                StopCoroutine(speedLerpCoroutine);
            speedLerpCoroutine = StartCoroutine(SmoothLerpSpeed());
        }
        else
        {
            moveSpeed = desiredMoveSpeed;

        }
        lastDesiredMoveSpeed = desiredMoveSpeed;


    }

    private IEnumerator SmoothLerpSpeed()
    {
        float t = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed) - 1;
        float startValue = moveSpeed;
        while (t < difference)
        {
            if (state == MovementState.air)
            {
                yield return null;
                continue;
            }
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, t / difference);
            if (OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);

                t += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }

            else
                t += Time.deltaTime * speedIncreaseMultiplier;

            yield return null;
        }
        moveSpeed = desiredMoveSpeed;
    }

    #region Inputs   

    private void JumpPressed(bool value)
    {
        jump = value;

    }

    private void SprintPressed(bool value)
    {
        sprint = value;
    }

    #endregion

    public void GroundCheck()
    {

        _isGrounded = Physics.CheckSphere(transform.position + -transform.up * ((CharacterHeight * 0.5f) * transform.localScale.y), 0.1f, Ground);
        if (_isGrounded)
            exitingSlope = false;
    }

    //Detects if player is on a slope
    public bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, 1.2f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0; // Ensure slope angle is within valid bounds.        
        }
        return false;
    }

    public void OverrideMoveSpeed(float speed)
    {
        desiredMoveSpeed = speed;
        moveSpeed = speed;
    }


    public Vector3 GetSlopeMoveDirection(Vector3 Direction)
    {
        return Vector3.ProjectOnPlane(Direction, slopeHit.normal).normalized;
    }

    void HandleMove(Vector2 Direction)
    {
        if (isRailGrinding || activeGrapple || wallRunning) return;

        // 1. Calculate desired velocity based on camera
        Vector3 cameraFlatForward = new Vector3(_camera.transform.forward.x, 0, _camera.transform.forward.z);
        Vector3 desiredvelocity = (cameraFlatForward * Direction.y + _camera.transform.right * Direction.x).normalized * moveSpeed;

        Vector3 moveDir = Vector3.MoveTowards(rb.linearVelocity, desiredvelocity, acceleration * Time.deltaTime);

        //3. Slop Logic
        if (OnSlope() && !exitingSlope)
        {
            // Project the desired velocity onto the slope and scale it by movement speed
            Vector3 slopeVel = GetSlopeMoveDirection(desiredvelocity) * moveSpeed;
            // Preserve vertical velocity, but ensure it's consistent with slope behavior
            rb.linearVelocity = slopeVel;
            // Apply an extra force to keep the player grounded          
            rb.AddForce(-slopeHit.normal * Slopeforce, ForceMode.Force);
        }

        // 4. Ground vs Air Movement Logic
        if (isGrounded)
        {
            if (Direction == Vector2.zero)
            {
                // Apply friction/deceleration when on ground with no input
                Vector3 horizontal = Vector3.Lerp(new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z), Vector3.zero, groundFriction * Time.deltaTime);
                rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
                // velocity = Vector3.zero; 
            }
            else
            {
                moveDir = Vector3.MoveTowards(rb.linearVelocity, desiredvelocity, acceleration * Time.deltaTime);
                rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z);
            }
        }
        else // AIR LOGIC
        {
            if (Direction != Vector2.zero)
            {
                // Use the desiredvelocity (which is based on input) 
                // instead of just lerping from current velocity
                Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

                // Ensure airAcceleration is high enough to overcome gravity's feel
                moveDir = Vector3.MoveTowards(currentHorizontalVel, desiredvelocity, airAcceleration * Time.deltaTime);
                rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z);
            }
        }

        // 5. Rotation Logic
        Vector3 horizontalView = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (horizontalView.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalView, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10 * Time.deltaTime);
        }

        rb.useGravity = !OnSlope();
        ClampSpeed();
    }

    void ClampSpeed()
    {
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        float maxSpeed = 20;

        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            Vector3 limited = rb.linearVelocity.normalized * maxSpeed;
            rb.linearVelocity = limited;
            moveSpeed = maxSpeed;
        }
    }

    void HandleLook(Vector2 Direction)
    {
        InputLagTimer += Time.deltaTime;


        if ((Mathf.Approximately(0, Direction.x) && Mathf.Approximately(0, Direction.y)) == false || InputLagTimer >= inputLagPeriod)
        {
            lastInputEvent = Direction;
            InputLagTimer = 0;
        }

        yRotation += lastInputEvent.x * Sensitivity;
        xRotation -= lastInputEvent.y * Sensitivity;

        xRotation = Mathf.Clamp(xRotation, -89, 89);

        _camera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);

    }

    private void Jump()
    {

        if (jump && ((isGrounded && IsJumpReady) || isRailGrinding))
        {
            float slideJumpMultiplier = sliding ? 1.25f : 1f;

            JumpEvent();

            IsJumpReady = false;
            exitingSlope = true;
            sliding = false;
            rb.useGravity = true;

            // Directly set the Y velocity. 
            // We keep the current X and Z (horizontal) velocity so you don't lose momentum.
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce * slideJumpMultiplier, rb.linearVelocity.z);

            // Reset the jump trigger so we don't double jump if the button is held
            jump = false;
        }
    }



    private void LateUpdate()
    {
        if (PauseManager.IsPaused) return;
        HandleLook(LookDirection);
    }

    private void Update()
    {
        //Look and move can be polled
        if (!activeGrapple)
        {
            MoveDirection = InputManager.Movement;
        }
        LookDirection = InputManager.Look;    

        Velocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        
    }
    private void FixedUpdate()
    {
        GroundCheck();
        Jump();
        StateHandler();
        HandleMove(MoveDirection);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (enableMoveOnNextTouch)
        {
            enableMoveOnNextTouch = false;

            // Instead of instant reset, let the SpeedLerp handle the slowdown
            // This keeps the "Zoom" feeling when you hit the ground
            ResetRestrictions();

            if (TryGetComponent<Grapple>(out var g)) g.StopGrapple();
        }

    }


    private void OnDisable()
    {
        InputManager.OnJumpReceived -= JumpPressed;
        InputManager.OnSprintReceived -= SprintPressed;
    }

    public void ResetRestrictions()
    {
        rb.linearDamping = 0.75f;
        activeGrapple = false;


    }



    //MoveToInputController?


#if UNITY_EDITOR

    private void OnValidate()
    {
        rb = GetComponent<Rigidbody>();

    }
#endif
}
