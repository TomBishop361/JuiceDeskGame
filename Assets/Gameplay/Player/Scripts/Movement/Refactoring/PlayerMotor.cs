using UnityEngine;

using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GameObject _camera;
    public Rigidbody rb { get; private set; }
    public GameObject Camera => _camera;

    [Header("Settings")]
    [SerializeField] LayerMask Ground;
    [SerializeField] float CharacterHeight;
    [SerializeField] public float maxSlopeAngle;
    [SerializeField] float Slopeforce;
    [SerializeField] float airAcceleration = 25;

    [Header("Look Settings")]
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float inputLagPeriod = 0.0001f;

    private float xRotation;
    private float yRotation;
    private float inputLagTimer;
    private Vector2 lastInputEvent;

    public bool IsGrounded { get; private set; }
    public RaycastHit slopeHit { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void GroundCheck()
    {

        IsGrounded = Physics.CheckSphere(transform.position + -transform.up * ((CharacterHeight * 0.5f) * transform.localScale.y), 0.1f, Ground);
        //if (IsGrounded)
        //    exitingSlope = false;
    }

    public bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.2f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            slopeHit = hit;
            return angle < maxSlopeAngle && angle != 0; // Ensure slope angle is within valid bounds.        
        }
        return false;
    }
    public Vector3 GetSlopeMoveDirection(Vector3 Direction)
    {
        return Vector3.ProjectOnPlane(Direction, slopeHit.normal).normalized;
    }

    public void Jump(float force)
    {
        
        // Directly sets the Y velocity, preserving the current horizontal (X/Z) momentum
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, force, rb.linearVelocity.z);
    }

    //Move the player 
    public void Move(Vector3 desiredVelocity, float accel, float friction)
    {
        if (OnSlope())
        {
            // Project the desired velocity onto the slope and scale it by movement speed
            Vector3 slopeVel = GetSlopeMoveDirection(desiredVelocity);
            // Preserve vertical velocity, but ensure it's consistent with slope behavior
            rb.linearVelocity = slopeVel;
            // Apply an extra force to keep the player grounded          
            rb.AddForce(-slopeHit.normal * Slopeforce, ForceMode.Force);
        }
        else if (IsGrounded)
        {
            if (desiredVelocity == Vector3.zero)
            {
                // Apply friction/deceleration when on ground with no input
                Vector3 horizontal = Vector3.Lerp(new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z), Vector3.zero, friction * Time.deltaTime);
                rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
                // velocity = Vector3.zero; 
            }
            else
            {
                Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                Vector3 moveDir = Vector3.MoveTowards(currentHorizontal, desiredVelocity, accel * Time.deltaTime);
                rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z);
            }
        }
        else // AIR LOGIC
        {
            if (desiredVelocity != Vector3.zero)
            {
                // Use the desiredvelocity (which is based on input) 
                // instead of just lerping from current velocity
                Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

                // Ensure airAcceleration is high enough to overcome gravity's feel
                Vector3 moveDir = Vector3.MoveTowards(currentHorizontalVel, desiredVelocity, airAcceleration * Time.deltaTime);
                rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z);
            }           
        }
        rb.useGravity = !OnSlope();
        RotateTowardsVelocity();
    }

    void RotateTowardsVelocity()
    {
        //5.Rotation Logic
        Vector3 horizontalView = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (horizontalView.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalView, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10 * Time.deltaTime);
        }
    }

   public void HandleLook(Vector2 Direction)
    {
        inputLagTimer += Time.deltaTime;


        if ((Mathf.Approximately(0, Direction.x) && Mathf.Approximately(0, Direction.y)) == false || inputLagTimer >= inputLagPeriod)
        {
            lastInputEvent = Direction;
            inputLagTimer = 0;
        }

        yRotation += lastInputEvent.x * sensitivity;
        xRotation -= lastInputEvent.y * sensitivity;

        xRotation = Mathf.Clamp(xRotation, -89, 89);

        _camera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);

    }
}
