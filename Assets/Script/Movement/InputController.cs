using System;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows;





[RequireComponent((typeof(Rigidbody)))]
public class InputController : MonoBehaviour
{
    [Header ("Input")]
    [SerializeField] InputManagerBase _inputManager;
    //[SerializeField] Camera _camera;
    [SerializeField] GameObject _camera;

    [Header("Movement Values")]
    private float moveSpeed = 7;
    [SerializeField] float walkSpeed = 7;
    [SerializeField] float sprintSpeed = 14;
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
    private RaycastHit slopeHit;

    [Header("Grappling Feel")]
    [SerializeField] float grappleSpeedBoost = 1.5f; // Multiplier for speed after grapple
    [SerializeField] float airControlDuringGrapple = 0.5f; // How much you can steer mid-air
    [SerializeField] float grapplePullForce = 20f; // Continuous pull toward target
    private Vector3 grappleTargetPos;


    [Header("Misc")]
    [Tooltip("For instant movement set to 'Infinity'")]
    [SerializeField] float acceleration = 50;
    [SerializeField] float groundFriction = 0.4f;
    [SerializeField] LayerMask Ground;
    [SerializeField] float CharacterHeight = 2;
    [Range(0,1)]
    [SerializeField] float CoyoteTime = 0.1f;
    [Range(0,1)]
    [SerializeField] float Sensitivity = 0.15f;
    [SerializeField] float jumpForce= 10;
    [SerializeField] float jumpCoolDown = 0.3f;
    [SerializeField] bool isGrounded;
    [SerializeField] float inputLagPeriod = 0.0001f;
    [SerializeField] TextMeshProUGUI VelocityUI;

    public const float gravity = -9.81f;
    public bool jump;
    private bool sprint;
    private float crouch;
    bool crouching;
    public bool sliding;
    public bool wallRunning;
    public bool IsJumpReady;
    public bool isOnCoolDown;
    public bool freeze;
    public bool activeGrapple;
    bool exitingSlope;
    bool enableMoveOnNextTouch;
    private Vector3 velocity;



    private Vector2 lastInputEvent;
    private float InputLagTimer;

    float yRotation;
    float xRotation;

    //Data From InputManager
    Vector2 MoveDirection;
    Vector2 LookDirection;

    Coroutine speedLerpCoroutine;

    public IInputManager InputManager => _inputManager.InputManager;
    [SerializeField] Rigidbody rb;

    public event Action JumpEvent = delegate { };

    public MovementState state;
    public enum MovementState
    {
        freeze,
        walking,
        sprinting,
        wallRunning,
        crouching,
        sliding,
        air
    }
  

    private void OnEnable()
    {       

        InputManager.OnMoveReceived += MovePressed;
        InputManager.OnLookReceived += LookMoved;
        InputManager.OnJumpReceived += JumpPressed;
        InputManager.OnSprintReceived += SprintPressed;
        InputManager.OnCrouchReceived += CrouchPressed;        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        startScaleYscale = transform.localScale.y;
    }

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
        yield return new WaitForSeconds(CoyoteTime);// new WaitForSeconds(0f);
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
        //Freeze for grapple;
        if (freeze)
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
        else if (_isGrounded)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }
        else
        {
            state = MovementState.air;
        }

        if (Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 7f && moveSpeed != 0)
        {
            if(speedLerpCoroutine != null)
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
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
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

    private void LookMoved(Vector2 vector)
    {
        LookDirection = vector;
    }

    private void MovePressed(Vector2 vector)
    {
        if (activeGrapple) return;
        MoveDirection = vector;
    }

    private void JumpPressed(bool value) 
    {        
            jump = value;        
            //_isGrounded = false;
    }

    private void SprintPressed(bool value)
    {
        sprint = value;
    }
    private void CrouchPressed(float value)
    {
        crouch = value;
    }

    void GroundCheck()
    {
        Collider[] hit = new Collider[1];
        
        _isGrounded = Physics.OverlapSphereNonAlloc(transform.position + -transform.up * ((CharacterHeight * 0.5f)*transform.localScale.y), 0.1f, hit, Ground) > 0;
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

   
    

    public void JumpToPosition(Vector3 targetPos, float trajectoryHeight)
    {
        activeGrapple = true;
        grappleTargetPos = targetPos; // Store this for the pull force

        // Calculate initial burst
        velocityToSet = JumpVelocityCalc.CalculateJumpVelocity(transform.position, targetPos, trajectoryHeight);

        // Slight delay to allow the "launch" to feel distinct
        Invoke(nameof(setVelocity), 0.05f);
    }

    Vector3 velocityToSet;
    void setVelocity()
    {
        rb.linearVelocity = velocityToSet;
        enableMoveOnNextTouch = true;

        // Boost max speed so the lerp doesn't immediately throttle us
        moveSpeed = sprintSpeed * grappleSpeedBoost;
    }

    public Vector3 GetSlopeMoveDirection( Vector3 Direction)
    {        
        return Vector3.ProjectOnPlane(Direction, slopeHit.normal).normalized;
    }

    void HandleMove(Vector2 Direction)
    {
        // 1. Calculate desired velocity based on camera
        Vector3 cameraFlatForward = new Vector3(_camera.transform.forward.x, 0, _camera.transform.forward.z);
        Vector3 desiredvelocity = (cameraFlatForward * Direction.y + _camera.transform.right * Direction.x).normalized * moveSpeed;

        // 2. grapple-specific logic
        if (activeGrapple)
        {
            
            Vector3 pullDir = (grappleTargetPos - transform.position).normalized;
            rb.AddForce(pullDir * grapplePullForce, ForceMode.Acceleration);

            // Allow slight steering (Air Control)
            rb.AddForce(desiredvelocity * airControlDuringGrapple, ForceMode.Acceleration);
            return;
        }

        velocity = Vector3.MoveTowards(velocity, desiredvelocity, acceleration * Time.deltaTime);

        Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        if (OnSlope() && !exitingSlope && !sliding)
        {
            

            // Project the desired velocity onto the slope and scale it by movement speed
            Vector3 slopeVel = GetSlopeMoveDirection(desiredvelocity) * moveSpeed * 20;

            // Preserve vertical velocity, but ensure it's consistent with slope behavior
            rb.linearVelocity = new Vector3(slopeVel.x , rb.linearVelocity.y, slopeVel.z);

            // Apply an extra force to keep the player grounded          
            rb.AddForce(-slopeHit.normal * 80, ForceMode.Force);

        }

        if (isGrounded && Direction == Vector2.zero)
        {

            horizontal = Vector3.Lerp(rb.linearVelocity, Vector3.zero, groundFriction * Time.deltaTime);
            rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
        }

        else
        {
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);

            if (horizontal != Vector3.zero)
            {
                
                Quaternion targetRotation = Quaternion.LookRotation(horizontal, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10 * Time.deltaTime);
            }
            
            
        }
        rb.useGravity = !OnSlope();
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
        //transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    private void Jump()
    {
        if (jump && isGrounded && IsJumpReady)
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

    void HandleCrouch()
    {
        if (crouch ==1 )
        {
            crouch = -1;
            crouching = true;
            transform.localScale = new Vector3(transform.localScale.x, crouchYscale, transform.localScale.z);
            rb.AddForce(Vector3.down, ForceMode.Impulse);
            
        }
        if(crouch == 0)
        {
            crouch = -1;
            crouching = false;
            transform.localScale = new Vector3(transform.localScale.x, startScaleYscale, transform.localScale.z);
            
        }
    }

    private void LateUpdate()
    {
        HandleLook(LookDirection);
    }
    private void Update()
    {
       // VelocityUI.text = Mathf.Abs(rb.linearVelocity.magnitude).ToString();
        
    }
    private void FixedUpdate()
    {
        GroundCheck();
        Jump();
        HandleMove(MoveDirection);        
        StateHandler();
        HandleCrouch();
        
    }


    private void OnDisable()
    {
        InputManager.OnMoveReceived -= MovePressed;
        InputManager.OnLookReceived -= LookMoved;
        InputManager.OnJumpReceived -= JumpPressed;
        InputManager.OnSprintReceived -= SprintPressed;
        InputManager.OnCrouchReceived -= CrouchPressed;
    }

  

    public void ResetRestrictions()
    {
        activeGrapple = false;
    }



private void OnCollisionEnter(Collision collision)
{
    if (enableMoveOnNextTouch)
    {
        enableMoveOnNextTouch = false;
        
        // Instead of instant reset, let the SpeedLerp handle the slowdown
        // This keeps the "Zoom" feeling when you hit the ground
        ResetRestrictions();
        
        if(TryGetComponent<Grapple>(out var g)) g.StopGrapple();
    }
}

#if UNITY_EDITOR

    private void OnValidate()
    {
        rb = GetComponent<Rigidbody>();
        
    }
#endif
}
