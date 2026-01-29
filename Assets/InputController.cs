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
    [SerializeField] float moveSpeed = 7;
    [Tooltip("For instant movement set to 'Infinity'")]
    [SerializeField] float acceleration = 50;
    [SerializeField] float groundFriction = 0.4f;
    [SerializeField] LayerMask Ground;
    [SerializeField] float CharacterHeight = 2;
    [Range(0,1)]
    [SerializeField] float Sensitivity = 0.15f;
    [SerializeField] float jumpForce= 10;
    [SerializeField] float jumpCoolDown = 0.3f;
    [SerializeField] bool isGrounded;
    [SerializeField] float inputLagPeriod = 0.0001f;

    public const float gravity = -9.81f;
    private bool jump;
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


    IInputManager InputManager => _inputManager.InputManager;
    [SerializeField] Rigidbody rb;
    

    private void OnEnable()
    {       

        InputManager.OnMoveReceived += MovePressed;
        InputManager.OnLookReceived += LookMoved;
        InputManager.OnJumpReceived += JumpPressed;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        yield return new WaitForSeconds(0.1f);// new WaitForSeconds(0f);

        isGrounded = value;
    }

    IEnumerator JumpCoolDown()
    {
        Debug.Log("JumpReady");
        yield return new WaitForSeconds(jumpCoolDown);
        IsJumpReady = true;
        isOnCoolDown= false;
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

    void GroundCheck()
    {
        Collider[] hit = new Collider[1];
        _isGrounded = Physics.OverlapSphereNonAlloc(transform.position + -transform.up * (CharacterHeight * 0.5f), 0.1f, hit, Ground) > 0;
    }

    void HandleMove(Vector2 Direction)
    {
        Vector3 desiredvelocity = (_camera.transform.forward * Direction.y + _camera.transform.right * Direction.x).normalized * moveSpeed;

        velocity = Vector3.MoveTowards(velocity, desiredvelocity, acceleration * Time.deltaTime);

        Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

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
    }


    private void OnDisable()
    {
        InputManager.OnMoveReceived -= MovePressed;
        InputManager.OnLookReceived -= LookMoved;
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        rb = GetComponent<Rigidbody>();
        
    }
#endif
}
