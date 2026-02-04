using System;
using System.Collections;
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

    [Header("Crouching")]
    [SerializeField] float crouchSpeed = 4;
    [SerializeField] float crouchYscale = 0.5f; // This will likely be removed when animations are made
    [SerializeField] float startScaleYscale = 1;

    [Header("Slop Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;

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

    public const float gravity = -9.81f;
    private bool jump;
    private bool sprint;
    private bool crouch;
    public bool wallRunning;
    public bool IsJumpReady;
    bool isOnCoolDown;
    private Vector3 velocity;
    
    private Vector2 lastInputEvent;
    private float InputLagTimer;

    float yRotation;
    float xRotation;

    //Data From InputManager
    Vector2 MoveDirection;
    Vector2 LookDirection;


    public IInputManager InputManager => _inputManager.InputManager;
    [SerializeField] Rigidbody rb;

    public MovementState state;
    public enum MovementState
    {
        walking,
        sprinting,
        wallRunning,
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

    bool _isGrounded
    {
        get => isGrounded;
        set
        {
            if (value == false)
            {                
                StartCoroutine(coyoteTime(value));
            }
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
        isOnCoolDown= false;
    }
        
    //Movement fsm
    private void StateHandler()
    {
        if (wallRunning) // Movement needs to change to be body relative instead of camera
        {
            state = MovementState.wallRunning;
            moveSpeed = wallRunSpeed;
        }

        if (_isGrounded && sprint)
        {
            state = MovementState.sprinting;
            moveSpeed = sprintSpeed;
        }
        else if (_isGrounded)
        {
            state = MovementState.walking;
            moveSpeed = walkSpeed;
        }
        else
        {
            state = MovementState.air;
        }
    }

    private void LookMoved(Vector2 vector)
    {
        LookDirection = vector;
    }

    private void MovePressed(Vector2 vector)
    {
        
        MoveDirection = vector;
    }

    private void JumpPressed(bool value) 
    {        
            jump = value;
            _isGrounded = false;
    }

    private void SprintPressed(bool value)
    {
        sprint = value;
    }
    private void CrouchPressed(bool value)
    {
        crouch = value;
    }

    void GroundCheck()
    {
        Collider[] hit = new Collider[1];
        _isGrounded = Physics.OverlapSphereNonAlloc(transform.position + -transform.up * (CharacterHeight * 0.5f), 0.1f, hit, Ground) > 0;
    }

    //Detects if player is on a slope
    bool OnSlope()
    {
       if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, 1.2f))
        { 
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle > 0; // Ensure slope angle is within valid bounds.        
        }
        return false;
    }

    private Vector3 GetSlopeMoveDirection( Vector3 Direction)
    {
        return Vector3.ProjectOnPlane(Direction, slopeHit.normal).normalized;
    }

    void HandleMove(Vector2 Direction)
    {
        //Converts direction according to camera Direction
        Vector3 desiredvelocity;
        if (wallRunning) desiredvelocity = (transform.forward * Direction.y + transform.right * Direction.x).normalized * moveSpeed;

        else desiredvelocity = (_camera.transform.forward * Direction.y + _camera.transform.right * Direction.x).normalized * moveSpeed;

        velocity = Vector3.MoveTowards(velocity, desiredvelocity, acceleration * Time.deltaTime);

        Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        if (OnSlope())
        {
            Debug.Log("OnSlope");

            // Project the desired velocity onto the slope and scale it by movement speed
            Vector3 slopeVel = GetSlopeMoveDirection(desiredvelocity) * moveSpeed;

            // Preserve vertical velocity, but ensure it's consistent with slope behavior
            rb.linearVelocity = new Vector3(slopeVel.x, rb.linearVelocity.y, slopeVel.z);

            // Apply an extra force to keep the player grounded
            rb.AddForce(Vector3.down * 80, ForceMode.Force);

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

        xRotation = Mathf.Clamp(xRotation, -90, 90);

        _camera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0); 
        //transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    private void Jump()
    {
        if (jump && isGrounded && IsJumpReady)
        {
            IsJumpReady = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z); // Clear vertical
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);            
        }
    }

    void HandleCrouch()
    {
        if (crouch)
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYscale, transform.localScale.z);
            rb.AddForce(Vector3.down, ForceMode.Impulse);
            moveSpeed = crouchSpeed;
        }
        else
        {
            transform.localScale = new Vector3(transform.localScale.x, startScaleYscale, transform.localScale.z);
            
        }
    }

    private void LateUpdate()
    {
        HandleLook(LookDirection);
    }
    private void Update()
    {        
        
            
    }
    private void FixedUpdate()
    {
        GroundCheck();
        HandleMove(MoveDirection);
        Jump();
        StateHandler();
        HandleCrouch();
        Debug.Log("SprintPressed? " + sprint);
    }


    private void OnDisable()
    {
        InputManager.OnMoveReceived -= MovePressed;
        InputManager.OnLookReceived -= LookMoved;
        InputManager.OnJumpReceived -= JumpPressed;
        InputManager.OnSprintReceived -= SprintPressed;
        InputManager.OnCrouchReceived -= CrouchPressed;
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        rb = GetComponent<Rigidbody>();
        
    }
#endif
}
